using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ProjectMind.Application.Abstractions;
using ProjectMind.Application.Ai;
using ProjectMind.Application.Common;
using ProjectMind.Application.Planning;
using ProjectMind.Domain.Entities;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.WhatIf;

/// <summary>Bir what-if çalıştırma kaydındaki senaryo satırı (<see cref="WhatIfRunLog.ResultsJson"/>).</summary>
public sealed record WhatIfRunScenario(
    string Name, bool IsCurrent, IReadOnlyList<string> Changes, DateOnly PlanFinish, DateOnly P50Finish, DateOnly P80Finish,
    DateOnly TargetDate, double OnTimeProbability, int P80DeltaWorkdays);

/// <summary>
/// Mevcut planı ve senaryoları aynı varsayımlarla (deterministik plan + Monte Carlo) karşılaştırır. Proje verisi değişmez;
/// her başarılı karşılaştırma RQ3 için <see cref="WhatIfRunLog"/>'a yazılır (kim çalıştırdı, sonuçlar, süre).
/// </summary>
public sealed class WhatIfService(ScheduleService schedules, IAppDbContext db, IOptions<WhatIfOptions> options)
{
    public const string CurrentPlanName = "Mevcut plan";

    public Task<WhatIfComparison> CompareAsync(int projectId, IReadOnlyList<WhatIfScenario> scenarios, CancellationToken ct) =>
        CompareAsync(projectId, scenarios, WhatIfRunSource.Panel, ct);

    public async Task<WhatIfComparison> CompareAsync(
        int projectId, IReadOnlyList<WhatIfScenario> scenarios, WhatIfRunSource source, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        var o = options.Value;
        if (scenarios.Count > o.MaxScenarios)
            throw new BusinessRuleException($"En fazla {o.MaxScenarios} senaryo karşılaştırılabilir.");

        var (overview, input) = await schedules.BuildInputAsync(projectId, ct);
        if (input.Activities.Count == 0)
            throw new BusinessRuleException("Planlanacak iş yok; önce iş ekleyin.");

        var parentOf = overview.WorkItems.ToDictionary(w => w.Id, w => w.ParentId);
        var target = overview.Project.TargetEndDate;

        var comparison = await Task.Run(() =>
        {
            var current = Evaluate(CurrentPlanName, [], [], input, target, null, o, ct);
            var results = scenarios.Select(s =>
            {
                var applied = ScenarioApplier.Apply(input, parentOf, target, s.Changes, o);
                return Evaluate(s.Name, s.Changes, applied.Notes, applied.Input, applied.TargetDate, current, o, ct);
            }).ToList();
            return new WhatIfComparison(current, results, o);
        }, ct);

        db.WhatIfRunLogs.Add(new WhatIfRunLog
        {
            ProjectId = projectId,
            Source = source,
            ScenarioCount = comparison.Scenarios.Count,
            Iterations = o.Iterations,
            Seed = o.Seed,
            DurationMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            ResultsJson = JsonSerializer.Serialize(RunScenarios(comparison), AiJson.Options)
        });
        await db.SaveChangesAsync(ct);
        return comparison;
    }

    /// <summary>Kayda yazılan senaryo satırları (mevcut plan ilk satır).</summary>
    public static IReadOnlyList<WhatIfRunScenario> RunScenarios(WhatIfComparison c) =>
        new[] { c.Current }.Concat(c.Scenarios)
            .Select((s, i) => new WhatIfRunScenario(s.Name, i == 0, s.Changes.Select(Describe).ToList(), s.Plan.Finish,
                s.MonteCarlo.P50Finish, s.MonteCarlo.P80Finish, s.TargetDate, s.MonteCarlo.OnTimeProbability, s.P80DeltaWorkdays))
            .ToList();

    /// <summary>Değişikliğin kısa, kültürden bağımsız tanımı (ör. "AddPerson count=2 skills=Backend weeklyHours=40").</summary>
    public static string Describe(ScenarioChange c)
    {
        var inv = CultureInfo.InvariantCulture;
        var parts = new List<string> { c.Kind.ToString() };
        if (c.Kind == ScenarioChangeKind.AddPerson)
            parts.Add($"count={c.Count.ToString(inv)}");
        if (c.PersonId is { } p) parts.Add($"personId={p.ToString(inv)}");
        if (c.WorkItemId is { } w) parts.Add($"workItemId={w.ToString(inv)}");
        if (c.Skills is { Count: > 0 } skills) parts.Add($"skills={string.Join('+', skills)}");
        if (c.WeeklyHours is { } h) parts.Add($"weeklyHours={h.ToString(inv)}");
        if (c.Date is { } d) parts.Add($"date={d.ToString("yyyy-MM-dd", inv)}");
        return string.Join(' ', parts);
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
