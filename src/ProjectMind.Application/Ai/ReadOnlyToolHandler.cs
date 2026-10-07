using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectMind.Application.Abstractions;
using ProjectMind.Application.Analytics;
using ProjectMind.Application.Common;
using ProjectMind.Application.MissingWork;
using ProjectMind.Application.Planning;
using ProjectMind.Application.WhatIf;

namespace ProjectMind.Application.Ai;

/// <summary>Veri değiştirmeyen araçları çalıştırır; sonuç modele JSON olarak döner, öneri kartı oluşmaz.</summary>
public sealed class ReadOnlyToolHandler(
    IAppDbContext db, MissingWorkService missingWork, ScheduleService schedules, ProjectStatusService statuses,
    WhatIfService whatIf)
{
    public async Task<ToolExecutionResult> ExecuteAsync(int sessionId, string toolName, JsonElement input, CancellationToken ct)
    {
        var projectId = await db.ChatSessions.Where(s => s.Id == sessionId).Select(s => s.ProjectId).FirstOrDefaultAsync(ct);

        switch (toolName)
        {
            case AiTools.CheckMissingWork:
            {
                if (projectId is not { } id)
                    return new ToolExecutionResult("Henüz proje yok; önce proje oluşturulmalı.", true);

                var (result, impact) = await missingWork.CheckAsync(id, ct);
                var payload = new
                {
                    templateVersion = WorkTemplateCatalog.Version,
                    missing = result.Missing.Select(m => new
                    {
                        name = m.Name,
                        phase = m.Phase.ToString(),
                        requiredSkill = m.Skill.ToString(),
                        suggestedHours = m.DefaultHours,
                        reason = m.Reason,
                        mustFinishBefore = m.SuggestedSuccessors
                    }),
                    alreadyCovered = result.Covered.ToDictionary(c => c.Key, c => c.Value),
                    totalSuggestedHours = result.TotalDefaultHours,
                    impactIfAdded = impact is null ? null : new
                    {
                        currentPlannedFinish = impact.CurrentFinish,
                        plannedFinishWithMissingWork = impact.FinishWithMissing,
                        extraWorkdays = impact.ExtraWorkdays,
                        extraHours = impact.ExtraHours,
                        extraCost = impact.ExtraCost
                    }
                };
                return new ToolExecutionResult(JsonSerializer.Serialize(payload, AiJson.Options), false);
            }
            case AiTools.PreviewSchedule:
            {
                if (projectId is not { } id)
                    return new ToolExecutionResult("Henüz proje yok; önce proje oluşturulmalı.", true);

                var p = await schedules.PreviewAsync(id, ct);
                var names = p.Overview.People.ToDictionary(x => x.Id, x => x.Name);
                var payload = new
                {
                    plannedStart = p.Plan.Start,
                    plannedFinish = p.Plan.Finish,
                    targetEndDate = p.Overview.Project.TargetEndDate,
                    varianceWorkdays = p.VarianceWorkdays,
                    varianceMeaning = "pozitif = hedeften geç, negatif = erken (iş günü)",
                    finishWithUnlimitedPeople = p.Plan.UnconstrainedFinish,
                    durationWorkdays = p.Plan.DurationWorkdays,
                    remainingHours = p.Plan.TotalHours,
                    plannedCost = p.Plan.PlannedCost,
                    budget = p.Overview.Project.Budget,
                    currency = p.Overview.Project.Currency,
                    criticalPath = p.Plan.Activities.Where(a => a.IsCritical).Select(a => a.Name),
                    activities = p.Plan.Activities.Select(a => new
                    {
                        a.Name, start = a.Start, finish = a.Finish,
                        assignee = a.AssigneeId is { } pid ? names.GetValueOrDefault(pid) : null,
                        a.Hours, critical = a.IsCritical, a.SlackDays
                    }),
                    peopleUtilizationPercent = p.Plan.Loads.Select(l => new { l.Name, l.AssignedHours, l.UtilizationPercent }),
                    warnings = p.Plan.Warnings,
                    latestBaselineFinish = p.LatestBaseline?.PlannedFinish
                };
                return new ToolExecutionResult(JsonSerializer.Serialize(payload, AiJson.Options), false);
            }
            case AiTools.GetProjectStatus:
            {
                if (projectId is not { } id)
                    return new ToolExecutionResult("Henüz proje yok; önce proje oluşturulmalı.", true);

                var s = await statuses.GetAsync(id, ct);
                var e = s.Evm;
                var payload = new
                {
                    statusDate = s.StatusDate,
                    hasBaseline = s.Baseline is not null,
                    evm = e is null ? null : new
                    {
                        unit = "saat (efor)",
                        budgetAtCompletion = e.BudgetAtCompletion,
                        plannedValue = e.PlannedValue,
                        earnedValue = e.EarnedValue,
                        actualCost = e.ActualCost,
                        scheduleVariance = e.ScheduleVariance,
                        costVariance = e.CostVariance,
                        spi = e.Spi,
                        cpi = e.Cpi,
                        spiTime = e.SpiTime,
                        estimateAtCompletion = e.EstimateAtCompletion,
                        percentComplete = e.PercentComplete,
                        plannedFinish = e.PlannedFinish,
                        forecastFinish = e.ForecastFinish,
                        budgetAtCompletionCost = e.BudgetAtCompletionCost,
                        estimateAtCompletionCost = e.EstimateAtCompletionCost
                    },
                    targetEndDate = s.Overview.Project.TargetEndDate,
                    healthScore = s.Health.Score,
                    healthLevel = s.Health.Level?.ToString(),
                    healthComponents = s.Health.Components.Select(c => new { criterion = c.Criterion.ToString(), c.Weight, c.Score }),
                    ahpConsistencyRatio = s.Health.Ahp.ConsistencyRatio,
                    scopeGrowthPercent = s.ScopeGrowthPercent,
                    mlDelayPrediction = s.Delay is not { } d ? null : new
                    {
                        delayProbabilityPercent = Math.Round(d.Probability * 100),
                        risk = d.Risk.ToString(),
                        forecastDurationRatio = Math.Round(d.DurationRatio, 2),
                        forecastFinish = d.ForecastFinish,
                        model = d.Model.Classifier,
                        outsideTrainingRange = d.OutsideTrainingRange,
                        note = "Sentetik veriyle eğitilmiş model; EVM tahminiyle birlikte yorumlanmalı."
                    },
                    alerts = s.Alerts.Select(a => new { severity = a.Severity.ToString(), a.Title, a.Detail })
                };
                return new ToolExecutionResult(JsonSerializer.Serialize(payload, AiJson.Options), false);
            }
            case AiTools.SimulateWhatIf:
            {
                if (projectId is not { } id)
                    return new ToolExecutionResult("Henüz proje yok; önce proje oluşturulmalı.", true);

                var scenario = await ToScenarioAsync(id, Parse<SimulateWhatIfPayload>(input), ct);
                var result = await whatIf.CompareAsync(id, [scenario], ct);
                object Describe(ScenarioResult r) => new
                {
                    name = r.Name,
                    changes = r.Notes,
                    targetEndDate = r.TargetDate,
                    deterministicFinish = r.Plan.Finish,
                    p50Finish = r.MonteCarlo.P50Finish,
                    p80Finish = r.MonteCarlo.P80Finish,
                    onTimeProbabilityPercent = Math.Round(r.MonteCarlo.OnTimeProbability * 100),
                    p50Cost = r.MonteCarlo.P50Cost,
                    p80Cost = r.MonteCarlo.P80Cost,
                    p80DeltaWorkdaysVsCurrent = r.P80DeltaWorkdays,
                    warnings = r.Plan.Warnings
                };
                var o = result.Assumptions;
                var payload = new
                {
                    current = Describe(result.Current),
                    scenario = Describe(result.Scenarios[0]),
                    assumptions = new
                    {
                        monteCarloIterations = o.Iterations,
                        effortMultiplier = new { min = o.EffortMin, mostLikely = o.EffortMode, max = o.EffortMax },
                        newPersonRampUpWeeks = o.RampUpWeeks,
                        newPersonRampUpProductivityPercent = o.RampUpProductivity * 100,
                        mentoringSharePercent = o.MentoringShare * 100
                    },
                    deltaMeaning = "pozitif = senaryoda P80 bitiş daha geç, negatif = daha erken (iş günü)"
                };
                return new ToolExecutionResult(JsonSerializer.Serialize(payload, AiJson.Options), false);
            }
            default:
                throw new BusinessRuleException($"Bilinmeyen araç: {toolName}");
        }
    }

    /// <summary>Modelin verdiği adları kişi/iş id'lerine çevirir.</summary>
    private async Task<WhatIfScenario> ToScenarioAsync(int projectId, SimulateWhatIfPayload p, CancellationToken ct)
    {
        var people = await db.People.Where(x => x.ProjectId == projectId).Select(x => new { x.Id, x.Name }).ToListAsync(ct);
        var items = await db.WorkItems.Where(x => x.ProjectId == projectId).Select(x => new { x.Id, x.Name }).ToListAsync(ct);
        int PersonId(string name) => people.FirstOrDefault(x => Same(x.Name, name))?.Id
            ?? throw new BusinessRuleException($"'{name}' adlı kişi projede yok.");

        var changes = new List<ScenarioChange>();
        if (p.AddPeopleCount is > 0)
            changes.Add(new ScenarioChange(ScenarioChangeKind.AddPerson, Name: "Yeni kişi", Skills: p.AddPeopleSkills,
                WeeklyHours: p.AddPeopleWeeklyHours, Date: p.AddPeopleJoinDate, Count: p.AddPeopleCount.Value));
        if (!string.IsNullOrWhiteSpace(p.RemovePersonName))
            changes.Add(new ScenarioChange(ScenarioChangeKind.RemovePerson, PersonId: PersonId(p.RemovePersonName)));
        if (!string.IsNullOrWhiteSpace(p.CapacityPersonName))
            changes.Add(new ScenarioChange(ScenarioChangeKind.ChangeCapacity, PersonId: PersonId(p.CapacityPersonName),
                WeeklyHours: p.NewWeeklyHours));
        if (!string.IsNullOrWhiteSpace(p.RemoveWorkItemName))
            changes.Add(new ScenarioChange(ScenarioChangeKind.RemoveWorkItem,
                WorkItemId: items.FirstOrDefault(x => Same(x.Name, p.RemoveWorkItemName))?.Id
                            ?? throw new BusinessRuleException($"'{p.RemoveWorkItemName}' adlı iş projede yok.")));
        if (p.NewDeadline is { } deadline)
            changes.Add(new ScenarioChange(ScenarioChangeKind.ChangeDeadline, Date: deadline));
        if (changes.Count == 0)
            throw new BusinessRuleException("Senaryoda hiçbir değişiklik yok.");

        return new WhatIfScenario(string.IsNullOrWhiteSpace(p.ScenarioName) ? "Senaryo" : p.ScenarioName.Trim(), changes);
    }

    private static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.CurrentCultureIgnoreCase);

    private static T Parse<T>(JsonElement input)
    {
        try
        {
            return input.Deserialize<T>(AiJson.Options) ?? throw new BusinessRuleException("Araç girdisi boş.");
        }
        catch (JsonException ex)
        {
            throw new BusinessRuleException($"Araç girdisi geçersiz: {ex.Message}");
        }
    }
}
