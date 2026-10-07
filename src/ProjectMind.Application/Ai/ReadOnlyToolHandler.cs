using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectMind.Application.Abstractions;
using ProjectMind.Application.Analytics;
using ProjectMind.Application.Common;
using ProjectMind.Application.MissingWork;
using ProjectMind.Application.Planning;

namespace ProjectMind.Application.Ai;

/// <summary>Veri değiştirmeyen araçları çalıştırır; sonuç modele JSON olarak döner, öneri kartı oluşmaz.</summary>
public sealed class ReadOnlyToolHandler(
    IAppDbContext db, MissingWorkService missingWork, ScheduleService schedules, ProjectStatusService statuses)
{
    public async Task<ToolExecutionResult> ExecuteAsync(int sessionId, string toolName, CancellationToken ct)
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
            default:
                throw new BusinessRuleException($"Bilinmeyen araç: {toolName}");
        }
    }
}
