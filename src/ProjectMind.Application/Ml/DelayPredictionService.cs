using Microsoft.Extensions.Options;
using ProjectMind.Application.Analytics;
using ProjectMind.Application.Planning;

namespace ProjectMind.Application.Ml;

/// <summary>Gerçek projenin durumundan model özelliklerini çıkarır (simülatördeki tanımlarla aynı oranlar).</summary>
public static class DelayFeatureBuilder
{
    public const int VelocityWindowDays = 14;   // simülatördeki 2 haftalık pencere

    /// <param name="history">Aynı baseline'a ait önceki snapshot'lar (tarih sırasız olabilir).</param>
    /// <param name="blockedRemainingShare">Bloke işlerin kalan efor payı (0–1).</param>
    public static DelayFeatures? FromStatus(EvmResult evm, IReadOnlyList<SnapshotPoint> history, decimal scopeGrowthPercent,
        decimal blockedRemainingShare, int teamSize)
    {
        if (evm.ActualTime is not { } at || at <= 0 || evm.PlannedDurationDays <= 0 || evm.BudgetAtCompletion <= 0)
            return null;

        var spi = evm.Spi ?? 1;
        var windowStart = evm.StatusDate.AddDays(-VelocityWindowDays);
        var earlier = history.Where(h => h.Date <= windowStart).OrderByDescending(h => h.Date).FirstOrDefault();
        var (oldPv, oldEv) = earlier is null ? (0m, 0m) : (earlier.PlannedValue, earlier.EarnedValue);
        var recentPv = evm.PlannedValue - oldPv;
        var velocity = recentPv > 0 ? (evm.EarnedValue - oldEv) / recentPv : spi;

        return new DelayFeatures(
            ElapsedRatio: (float)(at / evm.PlannedDurationDays),
            PercentComplete: (float)(evm.EarnedValue / evm.BudgetAtCompletion),
            Spi: (float)spi,
            Cpi: (float)(evm.Cpi ?? 1),
            SpiTime: (float)(evm.SpiTime ?? spi),
            RecentVelocity: (float)velocity,
            ScopeGrowth: (float)(scopeGrowthPercent / 100),
            BlockedShare: (float)blockedRemainingShare,
            TeamSize: teamSize);
    }
}

public interface IDelayPredictor
{
    DelayModelInfo? CurrentModel { get; }
    Task<DelayPrediction?> PredictAsync(DelayFeatures features, EvmResult evm, CancellationToken ct);
    Task<DelayExperimentReport> RunExperimentAsync(CancellationToken ct);
    DelayExperimentReport? LastReport { get; }

    /// <summary>
    /// Deneyi <paramref name="seeds"/> farklı tohumla çalıştırır ve ortalama ± SS raporu üretir (RQ1/RQ2 sağlamlık, D31).
    /// Üretim modeli değişmez; arka planda çalışır ve iptal edilebilir.
    /// </summary>
    Task<MultiSeedReport> RunMultiSeedAsync(int seeds, IProgress<string>? progress, CancellationToken ct);

    MultiSeedReport? LastMultiSeedReport { get; }
}

/// <summary>
/// Uygulamadaki tahmin servisi (tekil): kayıtlı modeli yükler; yoksa ilk ihtiyaçta sentetik veriyle eğitip kaydeder.
/// Deney (yeniden eğitim) çalıştırılınca seçilen model kaydedilir ve hemen kullanılmaya başlanır.
/// </summary>
public sealed class DelayPredictionService(
    IDelayLearner learner, IDelayModelStore store, IOptions<MlOptions> options, TimeProvider clock) : IDelayPredictor
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DelayModelBundle? _model;
    private bool _loadAttempted;

    public DelayModelInfo? CurrentModel => _model?.Info;
    public DelayExperimentReport? LastReport { get; private set; }
    public MultiSeedReport? LastMultiSeedReport { get; private set; }

    public async Task<MultiSeedReport> RunMultiSeedAsync(int seeds, IProgress<string>? progress, CancellationToken ct)
    {
        var o = options.Value;
        if (seeds < 2 || seeds > o.MaxRobustnessSeeds)
            throw new Common.BusinessRuleException($"Tohum sayısı 2–{o.MaxRobustnessSeeds} arasında olmalı.");

        var report = await Task.Run(() =>
        {
            var experiment = new DelayExperiment(learner);
            var reports = new List<DelayExperimentReport>();
            for (var i = 0; i < seeds; i++)
            {
                ct.ThrowIfCancellationRequested();
                var seed = o.Seed + i;
                progress?.Report($"Tohum {seed} ({i + 1}/{seeds}) çalışıyor…");
                var seeded = WithSeed(o, seed);
                reports.Add(experiment.Run(seeded, clock.GetUtcNow(), computeImportance: false, ct).Report);
            }
            return MultiSeedReport.Aggregate(reports, clock.GetUtcNow());
        }, ct);

        LastMultiSeedReport = report;
        progress?.Report("Tamamlandı.");
        return report;
    }

    /// <summary>Ayarların kopyası, yalnız tohum değişik (üretici, ayrım ve öğrenici tohumu).</summary>
    public static MlOptions WithSeed(MlOptions o, int seed) => new()
    {
        Projects = o.Projects, Seed = seed, DelayTolerance = o.DelayTolerance, TrainShare = o.TrainShare, Threshold = o.Threshold,
        PermutationRepeats = o.PermutationRepeats, ModelDirectory = o.ModelDirectory, MinElapsedRatio = o.MinElapsedRatio,
        MaxElapsedRatio = o.MaxElapsedRatio, RobustnessSeeds = o.RobustnessSeeds, MaxRobustnessSeeds = o.MaxRobustnessSeeds
    };

    public async Task<DelayPrediction?> PredictAsync(DelayFeatures features, EvmResult evm, CancellationToken ct)
    {
        var model = await EnsureModelAsync(ct);
        var o = options.Value;
        var probability = model.Classifier.PredictProbabilities([features])[0];
        var ratio = Math.Max(model.Regressor.PredictRatios([features])[0], (float)(evm.ActualTime / evm.PlannedDurationDays ?? 0));

        DateOnly? finish = null;
        if (ratio > 0 && evm.PlannedDurationDays > 0)
            finish = new WorkCalendar(evm.PlannedStart).ToDate(Math.Max((int)Math.Ceiling(evm.PlannedDurationDays * (decimal)ratio) - 1, 0));

        var outside = features.ElapsedRatio < o.MinElapsedRatio || features.ElapsedRatio > o.MaxElapsedRatio;
        return new DelayPrediction(probability, ratio, finish, outside, model.Info);
    }

    public async Task<DelayExperimentReport> RunExperimentAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var (report, model) = await Task.Run(() => new DelayExperiment(learner).Run(options.Value, clock.GetUtcNow()), ct);
            store.Save(model);
            _model = model;
            LastReport = report;
            return report;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<DelayModelBundle> EnsureModelAsync(CancellationToken ct)
    {
        if (_model is not null)
            return _model;

        await _gate.WaitAsync(ct);
        try
        {
            if (_model is null && !_loadAttempted)
            {
                _loadAttempted = true;
                _model = store.Load();
            }
        }
        finally
        {
            _gate.Release();
        }

        if (_model is null)
            await RunExperimentAsync(ct);
        return _model!;
    }
}
