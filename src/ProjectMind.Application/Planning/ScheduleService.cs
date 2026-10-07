using Microsoft.EntityFrameworkCore;
using ProjectMind.Application.Abstractions;
using ProjectMind.Application.Common;
using ProjectMind.Application.Overview;
using ProjectMind.Application.WorkItems;
using ProjectMind.Domain.Entities;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.Planning;

/// <summary>Simülasyon için ek iş: SuccessorIds'teki mevcut işler bu iş bitmeden başlayamaz.</summary>
public sealed record ExtraActivity(PlanActivity Activity, IReadOnlyList<int> SuccessorIds);

public sealed record BaselineSummary(int Id, DateTime CreatedAt, DateOnly PlannedFinish, decimal TotalHours, decimal PlannedCost);

public sealed record SchedulePreview(
    ProjectOverview Overview,
    PlanResult Plan,
    int VarianceWorkdays,
    BaselineSummary? LatestBaseline)
{
    /// <summary>Hedef bitişe göre sapma: pozitif = gecikme (iş günü).</summary>
    public bool IsLate => VarianceWorkdays > 0;
}

/// <summary>
/// Projenin verisinden plan girdisi üretir (sadece yaprak işler; üst iş bağımlılıkları alt işlere açılır;
/// kalan efor = tahmini efor × (1 − %tamamlanma)), çizelgeler, uygular ve baseline kaydeder.
/// </summary>
public sealed class ScheduleService(IAppDbContext db, ProjectOverviewService overviews, TimeProvider clock)
{
    private const decimal FullPercent = 100m;

    public async Task<SchedulePreview> PreviewAsync(int projectId, CancellationToken ct)
    {
        var overview = await overviews.GetAsync(projectId, ct);
        var plan = ResourceScheduler.Schedule(BuildInput(overview, []));
        return new SchedulePreview(overview, plan,
            WorkCalendar.WorkdaysBetween(overview.Project.TargetEndDate, plan.Finish),
            await LatestBaselineAsync(projectId, ct));
    }

    /// <summary>Ek (henüz eklenmemiş) işlerle planın nasıl değişeceğini hesaplar; veri değişmez.</summary>
    public async Task<(PlanResult Current, PlanResult WithExtra)> SimulateAsync(
        int projectId, IReadOnlyList<ExtraActivity> extra, CancellationToken ct)
    {
        var overview = await overviews.GetAsync(projectId, ct);
        return (ResourceScheduler.Schedule(BuildInput(overview, [])), ResourceScheduler.Schedule(BuildInput(overview, extra)));
    }

    /// <summary>What-if için plan girdisi (veri değişmez).</summary>
    public async Task<(ProjectOverview Overview, PlanInput Input)> BuildInputAsync(int projectId, CancellationToken ct)
    {
        var overview = await overviews.GetAsync(projectId, ct);
        return (overview, BuildInput(overview, []));
    }

    /// <summary>Planı işlere yazar (tarih + boş atamalar) ve baseline olarak dondurur.</summary>
    public async Task<SchedulePreview> ApplyAsync(int projectId, CancellationToken ct)
    {
        var preview = await PreviewAsync(projectId, ct);
        var plan = preview.Plan;
        if (plan.Activities.Count == 0)
            throw new BusinessRuleException("Planlanacak iş yok.");

        var items = await db.WorkItems.Where(w => w.ProjectId == projectId).ToListAsync(ct);
        var byId = items.ToDictionary(w => w.Id);
        foreach (var a in plan.Activities)
        {
            var item = byId[a.Id];
            item.PlannedStart = a.Start;
            item.PlannedEnd = a.Finish;
            item.AssigneeId ??= a.AssigneeId;
        }

        // Üst işlerin tarihleri alt işlerinden türetilir.
        var parentOf = items.ToDictionary(w => w.Id, w => w.ParentId);
        foreach (var parent in items.Where(w => items.Any(c => c.ParentId == w.Id)))
        {
            var leaves = WorkItemTree.SubtreeIds(parentOf, parent.Id).Where(id => id != parent.Id)
                .Select(id => byId[id]).Where(w => w.PlannedStart is not null).ToList();
            parent.PlannedStart = leaves.Min(w => w.PlannedStart);
            parent.PlannedEnd = leaves.Max(w => w.PlannedEnd);
        }

        // Baseline'da işin TAM eforu saklanır: EVM'de EV = baseline eforu × %tamamlanma.
        var cost = preview.Overview.People.ToDictionary(p => p.Id, p => p.HourlyCost);
        var fullHours = preview.Overview.WorkItems.ToDictionary(w => w.Id, w => w.EstimatedHours);
        db.Baselines.Add(new Baseline
        {
            ProjectId = projectId,
            PlannedStart = plan.Start,
            PlannedFinish = plan.Finish,
            TotalHours = plan.Activities.Sum(a => fullHours[a.Id]),
            PlannedCost = plan.PlannedCost,
            Items = plan.Activities.Select(a => new BaselineItem
            {
                WorkItemId = a.Id,
                Name = a.Name,
                PlannedStart = a.Start,
                PlannedEnd = a.Finish,
                Hours = fullHours[a.Id],
                AssigneeId = a.AssigneeId,
                HourlyCost = a.AssigneeId is { } p ? cost.GetValueOrDefault(p) : 0
            }).ToList()
        });
        await db.SaveChangesAsync(ct);
        return preview with { LatestBaseline = await LatestBaselineAsync(projectId, ct) };
    }

    public async Task<BaselineSummary?> LatestBaselineAsync(int projectId, CancellationToken ct) =>
        await db.Baselines.AsNoTracking()
            .Where(b => b.ProjectId == projectId)
            .OrderByDescending(b => b.Id)
            .Select(b => new BaselineSummary(b.Id, b.CreatedAt, b.PlannedFinish, b.TotalHours, b.PlannedCost))
            .FirstOrDefaultAsync(ct);

    private PlanInput BuildInput(ProjectOverview overview, IReadOnlyList<ExtraActivity> extra)
    {
        var items = overview.WorkItems;
        var leaves = WorkItemTree.Leaves(items).Where(w => w.Status != WorkItemStatus.Cancelled).ToList();
        var leafIds = leaves.Select(l => l.Id).ToHashSet();
        var parentOf = items.ToDictionary(w => w.Id, w => w.ParentId);

        IEnumerable<int> LeavesOf(int id) => WorkItemTree.SubtreeIds(parentOf, id).Where(leafIds.Contains);

        var predecessors = leaves.ToDictionary(l => l.Id, _ => new HashSet<int>());
        foreach (var d in overview.Dependencies)
            foreach (var p in LeavesOf(d.PredecessorId))
                foreach (var s in LeavesOf(d.SuccessorId))
                    if (p != s)
                        predecessors[s].Add(p);

        // Ek işler (simülasyon): ardılı olarak verilen mevcut işlere (üst işse alt işlerine) öncül olarak eklenir.
        foreach (var e in extra)
            foreach (var successor in e.SuccessorIds.Where(parentOf.ContainsKey).SelectMany(LeavesOf))
                predecessors[successor].Add(e.Activity.Id);

        var activities = leaves
            .Select(w => new PlanActivity(
                w.Id, w.Name, RemainingHours(w), w.RequiredSkill, w.Priority, w.AssigneeId, predecessors[w.Id].ToList()))
            .Concat(extra.Select(e => e.Activity with { Predecessors = [] }))
            .ToList();

        var people = overview.People
            .Select(p => new PlanResource(p.Id, p.Name, p.Skills, p.WeeklyCapacityHours, p.HourlyCost))
            .ToList();

        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var start = overview.Project.StartDate > today ? overview.Project.StartDate : today;
        return new PlanInput(start, overview.Project.HoursPerDay, activities, people);
    }

    private static decimal RemainingHours(WorkItemResponse w) =>
        w.Status == WorkItemStatus.Done ? 0 : Math.Round(w.EstimatedHours * (FullPercent - w.PercentComplete) / FullPercent, 2);
}
