using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProjectMind.Application.Abstractions;
using ProjectMind.Application.Ai;
using ProjectMind.Application.Common;
using ProjectMind.Application.Demo;
using ProjectMind.Application.MissingWork;
using ProjectMind.Application.Overview;
using ProjectMind.Domain.Entities;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.Evaluation;

/// <summary>Hibrit modun bir tekrarı: LLM katmanının durumu, kalan öneriler ve puan.</summary>
public sealed record HybridRecallRepeat(
    int Repeat, LlmLayerStatus Status, RecallScore Score, IReadOnlyList<string> Suggestions, int DroppedAsDuplicate, string? Model);

public sealed record RecallExperimentReport(
    int ProjectId, int Seed, IReadOnlyList<RemovedItem> Removed, RecallScore Rule, IReadOnlyList<HybridRecallRepeat> Hybrid,
    string? HybridProvider, int? RunId, string? Note, DateTimeOffset CreatedAt)
{
    public static readonly IReadOnlyList<string> CsvHeader =
    [
        "experiment", "seed", "mode", "repeat", "llm_status", "removed_template", "recovered_template", "recall_template",
        "removed_other", "recovered_other", "recall_other", "recall_all", "suggestions", "matched_suggestions", "precision",
        "pre_existing", "recovered", "removed", "provider", "run_id"
    ];

    /// <summary>LLM katmanı başarılı olan hibrit tekrarları (ortalama yalnız bunlardan).</summary>
    public IReadOnlyList<HybridRecallRepeat> SuccessfulHybrid => Hybrid.Where(h => h.Status == LlmLayerStatus.Success).ToList();

    public DescriptiveStats HybridRecall(Func<RecallScore, GroupRecall> group) =>
        DescriptiveStats.Of(SuccessfulHybrid.Select(h => group(h.Score).Recall).Where(r => r is not null).Select(r => r!.Value));

    public DescriptiveStats HybridPrecision =>
        DescriptiveStats.Of(SuccessfulHybrid.Select(h => h.Score.Precision).Where(p => p is not null).Select(p => p!.Value));

    public CsvFile ToCsv()
    {
        var removed = string.Join(" | ", Removed.Select(r => $"{r.Name} ({(r.Group == RemovedGroup.Template ? "şablon" : "diğer")})"));
        IReadOnlyList<string> Row(string mode, string repeat, string status, RecallScore s, string? provider) =>
        [
            MissingWorkRecall.Version, Csv.Int(Seed), mode, repeat, status, Csv.Int(s.Template.Removed), Csv.Int(s.Template.Recovered),
            Csv.Num(s.Template.Recall), Csv.Int(s.Other.Removed), Csv.Int(s.Other.Recovered), Csv.Num(s.Other.Recall),
            Csv.Num(s.All.Recall), Csv.Int(s.Suggestions), Csv.Int(s.MatchedSuggestions), Csv.Num(s.Precision), Csv.Int(s.PreExisting),
            string.Join(" | ", s.RecoveredNames), removed, provider ?? "", Csv.Int(RunId)
        ];

        var rows = new List<IReadOnlyList<string>> { Row("rule", "", "", Rule, null) };
        rows.AddRange(Hybrid.Select(h => Row("hybrid", Csv.Int(h.Repeat), h.Status.ToString(), h.Score, HybridProvider)));
        return new CsvFile($"rq4-eksik-is-bulma-{CreatedAt:yyyyMMdd-HHmm}.csv", Csv.Build(CsvHeader, rows));
    }
}

/// <summary>
/// RQ4 "tohumlanmış eksik iş" deneyi (D31): demo projesinin iş listesinden k şablon işi ve k projeye özgü iş bellekte çıkarılır;
/// kural-yalnız (deterministik) ve hibrit (ayarlı LLM, n tekrar) mod çıkarılan işlerin ne kadarını geri buluyor ölçülür.
/// Veritabanındaki işler değişmez, kart oluşmaz. Hibrit kısım Mock modda atlanır; LLM çağrıları koşuya etiketli kayda yazılır.
/// </summary>
public sealed class MissingWorkRecallService(
    IServiceScopeFactory scopes,
    IAppDbContext db,
    IChatModel model,
    ProjectOverviewService overviews,
    IOptions<EvaluationOptions> options,
    TimeProvider clock)
{
    public const string MockNote = "AI bağlı değil (Mock): yalnız kural-yalnız mod ölçüldü; hibrit kısım API anahtarı gerektirir.";
    public const string TaskId = "missing-work-recall";

    public async Task<RecallExperimentReport> RunAsync(IProgress<string>? progress, CancellationToken ct)
    {
        var o = options.Value;
        if (o.RecallRepeats < 1 || o.RecallRepeats > o.MaxRepeats)
            throw new BusinessRuleException($"Tekrar sayısı 1–{o.MaxRepeats} arasında olmalı.");

        progress?.Report("Demo projesi hazırlanıyor…");
        int projectId;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DemoProjectSeeder>();
            projectId = await seeder.FindExistingAsync(ct) ?? (await seeder.CreateAsync(ct)).ProjectId;
        }

        var overview = await overviews.GetAsync(projectId, ct);
        var type = overview.Project.Type;
        var setup = MissingWorkRecall.SelectRemovals(type, overview.WorkItems, o.RecallTemplateItems, o.RecallOtherItems, o.RecallSeed);
        var full = MissingWorkDetector.Detect(type, overview.WorkItems);
        var reduced = MissingWorkDetector.Detect(type, setup.Remaining);
        var rule = MissingWorkRecall.Score(setup.Removed, full, reduced, []);

        if (!model.IsConfigured)
            return new RecallExperimentReport(projectId, o.RecallSeed, setup.Removed, rule, [], null, null, MockNote, clock.GetUtcNow());

        var run = new EvaluationRun
        {
            Kind = EvaluationRunKind.MissingWorkRecall,
            TaskSetVersion = MissingWorkRecall.Version,
            Providers = model.ProviderKey,
            Repeats = o.RecallRepeats,
            ProjectId = projectId,
            StartedAt = clock.GetUtcNow().UtcDateTime
        };
        db.EvaluationRuns.Add(run);
        await db.SaveChangesAsync(ct);

        var hybrid = new List<HybridRecallRepeat>();
        string? note = null;
        try
        {
            var reducedOverview = overview with { WorkItems = setup.Remaining };
            for (var repeat = 1; repeat <= o.RecallRepeats; repeat++)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report($"Hibrit mod · tekrar {repeat}/{o.RecallRepeats}");
                await using var scope = scopes.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<EvaluationRunContext>().Begin(run.Id, model.ProviderKey, TaskId);
                var result = await scope.ServiceProvider.GetRequiredService<MissingWorkService>()
                    .CheckHybridForItemsAsync(reducedOverview, ct);
                var names = result.LlmStatus == LlmLayerStatus.Success ? result.Llm.Select(l => l.Name).ToList() : [];
                hybrid.Add(new HybridRecallRepeat(repeat, result.LlmStatus,
                    MissingWorkRecall.Score(setup.Removed, full, result.Rule, names), names, result.Duplicates.Count, result.LlmModel));
            }
            run.Status = EvaluationRunStatus.Completed;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            run.Status = EvaluationRunStatus.Cancelled;
            note = $"İptal edildi: {hybrid.Count}/{o.RecallRepeats} hibrit tekrar tamamlandı.";
        }

        run.FinishedAt = clock.GetUtcNow().UtcDateTime;
        run.Notes = note;
        await db.SaveChangesAsync(CancellationToken.None);
        progress?.Report("Deney tamamlandı.");
        return new RecallExperimentReport(projectId, o.RecallSeed, setup.Removed, rule, hybrid, model.ProviderKey, run.Id, note,
            clock.GetUtcNow());
    }
}
