using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectMind.Application.Abstractions;
using ProjectMind.Application.Ml;
using ProjectMind.Application.Overview;
using ProjectMind.Application.Planning;
using ProjectMind.Application.WorkItems;
using ProjectMind.Domain.Entities;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.Analytics;

public sealed record SnapshotPoint(DateOnly Date, decimal PlannedValue, decimal EarnedValue, decimal ActualCost, decimal? HealthScore);

public sealed record ProjectStatusReport(
    ProjectOverview Overview,
    DateOnly StatusDate,
    BaselineSummary? Baseline,
    EvmResult? Evm,
    HealthResult Health,
    IReadOnlyList<RiskAlert> Alerts,
    decimal ScopeGrowthPercent,
    PlanResult Plan,
    IReadOnlyList<SnapshotPoint> History,
    DelayFeatures? DelayFeatures = null,
    DelayPrediction? Delay = null);

/// <summary>
/// Projenin güncel durumu: EVM (baseline varsa), AHP ağırlıklı sağlık skoru, kural tabanlı riskler.
/// Her hesaplamada günün snapshot'ı eklenir/güncellenir (S-eğrisi ve ML veri seti için).
/// </summary>
public sealed class ProjectStatusService(
    IAppDbContext db, ScheduleService schedules, TimeProvider clock, IOptions<HealthOptions> options,
    IDelayPredictor predictor)
{
    private const int FullPercent = 100;
    private const int ProbabilityDecimals = 4;

    public async Task<ProjectStatusReport> GetAsync(int projectId, CancellationToken ct)
    {
        var o = options.Value;
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var preview = await schedules.PreviewAsync(projectId, ct);
        var overview = preview.Overview;
        var leaves = WorkItemTree.Leaves(overview.WorkItems).Where(w => w.Status != WorkItemStatus.Cancelled).ToList();

        var baseline = await db.Baselines.AsNoTracking().Include(b => b.Items)
            .Where(b => b.ProjectId == projectId).OrderByDescending(b => b.Id).FirstOrDefaultAsync(ct);

        EvmResult? evm = null;
        decimal scopeGrowth = 0;
        if (baseline is { Items.Count: > 0 })
        {
            var completedOn = await CompletionDatesAsync(projectId, ct);
            var progress = overview.WorkItems.Select(w => new EvmProgress(w.Id, w.PercentComplete, w.ActualHours,
                w.Status == WorkItemStatus.Done, w.Status == WorkItemStatus.Cancelled, completedOn.GetValueOrDefault(w.Id))).ToList();
            // Baseline'da olup artık projede olmayan (silinmiş) iş kapsam dışı sayılır — iptal ile aynı (D27).
            var existing = progress.Select(p => p.WorkItemId).ToHashSet();
            progress.AddRange(baseline.Items.Select(i => i.WorkItemId).Distinct().Where(id => !existing.Contains(id))
                .Select(id => new EvmProgress(id, 0, 0, false, IsCancelled: true)));
            evm = EarnedValue.Compute(
                baseline.Items.Select(i => new EvmBaselineItem(i.WorkItemId, i.PlannedStart, i.PlannedEnd, i.Hours, i.HourlyCost)).ToList(),
                progress, today);
            // Kapsam büyümesi, iptal edilen (kapsamdan çıkarılan) baseline işleri düşüldükten sonraki baseline'a göre ölçülür.
            var current = leaves.Sum(w => w.EstimatedHours);
            var baselineScope = baseline.TotalHours - evm.DescopedHours;
            scopeGrowth = baselineScope > 0 ? Math.Max(0, Math.Round((current - baselineScope) / baselineScope * 100, 1)) : 0;
        }

        var remaining = preview.Plan.TotalHours;
        var unassigned = preview.Plan.Activities.Where(a => a.AssigneeId is null).Sum(a => a.Hours);
        var blocked = leaves.Where(w => w.Status == WorkItemStatus.Blocked).ToList();
        var overdue = leaves.Where(w => w.PlannedEnd is { } end && end < today && w.Status != WorkItemStatus.Done).ToList();
        var troubled = blocked.Concat(overdue).DistinctBy(w => w.Id)
            .Sum(w => w.EstimatedHours * (100 - w.PercentComplete) / 100m);

        var health = ProjectHealth.Compute(new Dictionary<HealthCriterion, decimal?>
        {
            [HealthCriterion.Schedule] = ProjectHealth.IndexScore(evm?.SpiTime ?? evm?.Spi),
            [HealthCriterion.Cost] = ProjectHealth.IndexScore(evm?.Cpi),
            [HealthCriterion.Scope] = baseline is null ? null : 100 - scopeGrowth * o.ScopePenaltyPerPercent,
            [HealthCriterion.Resource] = remaining > 0 ? Math.Round(100 * (1 - unassigned / remaining), 1) : null,
            [HealthCriterion.Risk] = remaining > 0 ? Math.Round(100 * (1 - Math.Min(troubled, remaining) / remaining), 1) : null
        }, o);

        var alerts = RiskRules.Evaluate(new RiskInput(
            baseline is not null, evm, overview.Project.TargetEndDate, preview.Plan.Activities.Count > 0 ? preview.Plan.Finish : null,
            scopeGrowth, blocked.Select(w => w.Name).ToList(), overdue.Select(w => w.Name).ToList(), preview.Plan.Warnings), o);

        // Kritik bir uyarı varken skor yüksek olsa bile seviye "Dikkat"in üstünde gösterilmez.
        if (health.Level == HealthLevel.Good && alerts.Any(a => a.Severity == AlertSeverity.Critical))
            health = health with { Level = HealthLevel.Warning };

        // ML gecikme tahmini (baseline ve başlamış proje gerekir). Özellik tanımları sentetik veriyle aynıdır.
        // Tahmin snapshot'tan önce hesaplanır ki günün snapshot'ına da yazılsın (RQ1: EVM ↔ ML zaman çizelgesi).
        // Hız penceresi yalnız önceki günleri kullanır; bugünün snapshot'ı özelliklere girmez.
        var baselineId = baseline?.Id;
        DelayFeatures? features = null;
        DelayPrediction? delay = null;
        if (evm is not null)
        {
            var earlier = await HistoryAsync(projectId, baselineId, today, ct);
            var remainingEffort = leaves.Sum(w => w.EstimatedHours * (100 - w.PercentComplete) / 100m);
            var blockedRemaining = blocked.Sum(w => w.EstimatedHours * (100 - w.PercentComplete) / 100m);
            features = DelayFeatureBuilder.FromStatus(evm, earlier, scopeGrowth,
                remainingEffort > 0 ? blockedRemaining / remainingEffort : 0, overview.People.Count);
            if (features is not null && evm.EarnedValue < evm.BudgetAtCompletion)
                delay = await predictor.PredictAsync(features, evm, ct);
        }

        await UpsertSnapshotAsync(projectId, today, baseline, evm, health, leaves.Sum(w => w.EstimatedHours), blocked.Count,
            overview.People.Count, delay, ct);

        var history = await HistoryAsync(projectId, baselineId, null, ct);

        return new ProjectStatusReport(overview, today, preview.LatestBaseline, evm, health, alerts, scopeGrowth, preview.Plan, history,
            features, delay);
    }

    /// <summary>Aynı baseline'a ait snapshot'lar (tarih sırasıyla); before verilirse yalnız o günden öncekiler.</summary>
    private async Task<List<SnapshotPoint>> HistoryAsync(int projectId, int? baselineId, DateOnly? before, CancellationToken ct) =>
        await db.ProjectSnapshots.AsNoTracking()
            .Where(s => s.ProjectId == projectId && s.BaselineId == baselineId && (before == null || s.Date < before))
            .OrderBy(s => s.Date)
            .Select(s => new SnapshotPoint(s.Date, s.PlannedValue, s.EarnedValue, s.ActualCost, s.HealthScore))
            .ToListAsync(ct);

    /// <summary>Her işin son tamamlanma günü (ilerleme geçmişinden; bkz. <see cref="EarnedValue.CompletedOn"/>).</summary>
    private async Task<Dictionary<int, DateOnly?>> CompletionDatesAsync(int projectId, CancellationToken ct)
    {
        var updates = await db.StatusUpdates.AsNoTracking()
            .Where(u => u.ProjectId == projectId)
            .OrderBy(u => u.Date).ThenBy(u => u.Id)
            .Select(u => new { u.WorkItemId, u.Date, u.Status, u.PercentComplete })
            .ToListAsync(ct);
        return updates.GroupBy(u => u.WorkItemId).ToDictionary(g => g.Key,
            g => EarnedValue.CompletedOn(g.Select(u =>
                (u.Date, u.Status == WorkItemStatus.Done || u.PercentComplete >= FullPercent))));
    }

    private async Task UpsertSnapshotAsync(int projectId, DateOnly today, Baseline? baseline, EvmResult? evm, HealthResult health,
        decimal scopeHours, int blocked, int teamSize, DelayPrediction? delay, CancellationToken ct)
    {
        var snapshot = await db.ProjectSnapshots.FirstOrDefaultAsync(s => s.ProjectId == projectId && s.Date == today, ct);
        if (snapshot is null)
        {
            snapshot = new ProjectSnapshot { ProjectId = projectId, Date = today };
            db.ProjectSnapshots.Add(snapshot);
        }

        snapshot.BaselineId = baseline?.Id;
        snapshot.PlannedValue = evm?.PlannedValue ?? 0;
        snapshot.EarnedValue = evm?.EarnedValue ?? 0;
        snapshot.ActualCost = evm?.ActualCost ?? 0;
        snapshot.BudgetAtCompletion = evm?.BudgetAtCompletion ?? 0;
        snapshot.Spi = evm?.Spi;
        snapshot.Cpi = evm?.Cpi;
        snapshot.SpiTime = evm?.SpiTime;
        snapshot.EstimateAtCompletion = evm?.EstimateAtCompletion;
        snapshot.ForecastFinish = evm?.ForecastFinish;
        snapshot.PercentComplete = evm?.PercentComplete ?? 0;
        snapshot.HealthScore = health.Score;
        snapshot.ScopeHours = scopeHours;
        snapshot.BlockedItems = blocked;
        snapshot.TeamSize = teamSize;
        snapshot.DelayProbability = delay is null ? null : Math.Round((decimal)delay.Probability, ProbabilityDecimals);
        snapshot.MlForecastFinish = delay?.ForecastFinish;
        await db.SaveChangesAsync(ct);
    }
}
