using ProjectMind.Application.Overview;
using ProjectMind.Application.Planning;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.MissingWork;

/// <summary>Eksik işler eklenirse planın nasıl değişeceği (kaynak kısıtlı çizelgeyle hesaplanır).</summary>
public sealed record MissingWorkImpact(
    DateOnly CurrentFinish, DateOnly FinishWithMissing, int ExtraWorkdays, decimal ExtraHours, decimal ExtraCost);

public sealed class MissingWorkService(ProjectOverviewService overviews, ScheduleService schedules)
{
    public async Task<(MissingWorkResult Result, MissingWorkImpact? Impact)> CheckAsync(int projectId, CancellationToken ct)
    {
        var overview = await overviews.GetAsync(projectId, ct);
        var result = MissingWorkDetector.Detect(overview.Project.Type, overview.WorkItems);
        if (result.Missing.Count == 0)
            return (result, null);

        // Ek işlere çakışmasın diye negatif id; önerilen ardıllar ada göre eşleşen mevcut işlerdir.
        var extra = result.Missing.Select((m, i) => new ExtraActivity(
                new PlanActivity(-(i + 1), m.Name, m.DefaultHours, m.Skill, Priority.Medium, null, []),
                overview.WorkItems.Where(w => m.SuggestedSuccessors.Contains(w.Name)).Select(w => w.Id).ToList()))
            .ToList();

        var (current, withMissing) = await schedules.SimulateAsync(projectId, extra, ct);
        return (result, new MissingWorkImpact(
            current.Finish, withMissing.Finish,
            WorkCalendar.WorkdaysBetween(current.Finish, withMissing.Finish),
            withMissing.TotalHours - current.TotalHours,
            withMissing.PlannedCost - current.PlannedCost));
    }
}
