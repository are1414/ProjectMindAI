using ProjectMind.Application.Analytics;
using ProjectMind.Application.WhatIf;

namespace ProjectMind.Application.Ai;

/// <summary>
/// Salt okunur araçların ve analiz yorumlarının ortak, deterministik JSON yükleri (tek kaynak): sohbetteki
/// get_project_status / simulate_what_if ile Durum ve What-if yorum kartları aynı sayıları görür.
/// </summary>
public static class AnalysisPayloads
{
    public const string DeltaMeaning = "pozitif = senaryoda P80 bitiş daha geç, negatif = daha erken (iş günü)";

    private const int PercentScale = 100;
    private const int RatioDecimals = 2;

    public static object ProjectStatus(ProjectStatusReport s)
    {
        var e = s.Evm;
        return new
        {
            statusDate = s.StatusDate,
            hasBaseline = s.Baseline is not null,
            evm = e is null ? null : new
            {
                unit = "saat (efor)",
                budgetAtCompletion = e.BudgetAtCompletion,
                descopedHours = e.DescopedHours,
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
            currency = s.Overview.Project.Currency,
            healthScore = s.Health.Score,
            healthLevel = s.Health.Level?.ToString(),
            healthComponents = s.Health.Components.Select(c => new { criterion = c.Criterion.ToString(), c.Weight, c.Score }),
            ahpConsistencyRatio = s.Health.Ahp.ConsistencyRatio,
            scopeGrowthPercent = s.ScopeGrowthPercent,
            mlDelayPrediction = s.Delay is not { } d ? null : new
            {
                delayProbabilityPercent = Math.Round(d.Probability * PercentScale),
                risk = d.Risk.ToString(),
                forecastDurationRatio = Math.Round(d.DurationRatio, RatioDecimals),
                forecastFinish = d.ForecastFinish,
                model = d.Model.Classifier,
                outsideTrainingRange = d.OutsideTrainingRange,
                note = "Sentetik veriyle eğitilmiş model; EVM tahminiyle birlikte yorumlanmalı."
            },
            alerts = s.Alerts.Select(a => new { severity = a.Severity.ToString(), a.Title, a.Detail })
        };
    }

    public static object Scenario(ScenarioResult r) => new
    {
        name = r.Name,
        changes = r.Notes,
        targetEndDate = r.TargetDate,
        deterministicFinish = r.Plan.Finish,
        p50Finish = r.MonteCarlo.P50Finish,
        p80Finish = r.MonteCarlo.P80Finish,
        onTimeProbabilityPercent = Math.Round(r.MonteCarlo.OnTimeProbability * PercentScale),
        p50Cost = r.MonteCarlo.P50Cost,
        p80Cost = r.MonteCarlo.P80Cost,
        p80DeltaWorkdaysVsCurrent = r.P80DeltaWorkdays,
        warnings = r.Plan.Warnings
    };

    public static object Assumptions(WhatIfOptions o) => new
    {
        monteCarloIterations = o.Iterations,
        effortMultiplier = new { min = o.EffortMin, mostLikely = o.EffortMode, max = o.EffortMax },
        newPersonRampUpWeeks = o.RampUpWeeks,
        newPersonRampUpProductivityPercent = o.RampUpProductivity * PercentScale,
        mentoringSharePercent = o.MentoringShare * PercentScale
    };

    /// <summary>What-if karşılaştırması (mevcut plan + en fazla 4 senaryo) — senaryo yorumunun girdisi.</summary>
    public static object Comparison(WhatIfComparison c, string currency) => new
    {
        currency,
        current = Scenario(c.Current),
        scenarios = c.Scenarios.Select(Scenario).ToList(),
        assumptions = Assumptions(c.Assumptions),
        deltaMeaning = DeltaMeaning
    };
}
