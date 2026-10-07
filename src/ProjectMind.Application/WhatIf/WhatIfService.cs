using Microsoft.Extensions.Options;
using ProjectMind.Application.Common;
using ProjectMind.Application.Planning;

namespace ProjectMind.Application.WhatIf;

/// <summary>Mevcut planı ve senaryoları aynı varsayımlarla (deterministik plan + Monte Carlo) karşılaştırır. Veri değişmez.</summary>
public sealed class WhatIfService(ScheduleService schedules, IOptions<WhatIfOptions> options)
{
    public const string CurrentPlanName = "Mevcut plan";

    public async Task<WhatIfComparison> CompareAsync(int projectId, IReadOnlyList<WhatIfScenario> scenarios, CancellationToken ct)
    {
        var o = options.Value;
        if (scenarios.Count > o.MaxScenarios)
            throw new BusinessRuleException($"En fazla {o.MaxScenarios} senaryo karşılaştırılabilir.");

        var (overview, input) = await schedules.BuildInputAsync(projectId, ct);
        if (input.Activities.Count == 0)
            throw new BusinessRuleException("Planlanacak iş yok; önce iş ekleyin.");

        var parentOf = overview.WorkItems.ToDictionary(w => w.Id, w => w.ParentId);
        var target = overview.Project.TargetEndDate;

        return await Task.Run(() =>
        {
            var current = Evaluate(CurrentPlanName, [], [], input, target, null, o, ct);
            var results = scenarios.Select(s =>
            {
                var applied = ScenarioApplier.Apply(input, parentOf, target, s.Changes, o);
                return Evaluate(s.Name, s.Changes, applied.Notes, applied.Input, applied.TargetDate, current, o, ct);
            }).ToList();
            return new WhatIfComparison(current, results, o);
        }, ct);
    }

    private static ScenarioResult Evaluate(string name, IReadOnlyList<ScenarioChange> changes, IReadOnlyList<string> notes,
        PlanInput input, DateOnly target, ScenarioResult? current, WhatIfOptions o, CancellationToken ct)
    {
        var plan = ResourceScheduler.Schedule(input);
        var mc = MonteCarlo.Run(input, target, o, ct);
        var delta = current is null ? 0 : WorkCalendar.WorkdaysBetween(current.MonteCarlo.P80Finish, mc.P80Finish);
        return new ScenarioResult(name, changes, notes, target, plan, mc, delta);
    }
}
