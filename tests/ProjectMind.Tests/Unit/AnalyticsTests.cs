using ProjectMind.Application.Analytics;

namespace ProjectMind.Tests.Unit;

public class EarnedValueTests
{
    // Baseline (Pzt 2 Kasım 2026'dan): A 2–3 Kasım 16 s, B 4–6 Kasım 24 s → BAC 40 s, PD 5 iş günü.
    // Kümülatif PV: 8, 16, 24, 32, 40.
    private static readonly EvmBaselineItem[] Baseline =
    [
        new(1, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 3), 16, 100),
        new(2, new DateOnly(2026, 11, 4), new DateOnly(2026, 11, 6), 24, 100)
    ];

    [Fact]
    public void Metrics_match_hand_calculation()
    {
        // Durum: Çarşamba 4 Kasım (3. iş günü). A bitti (20 s harcandı), B %25 (4 s harcandı).
        // PV = 24, EV = 16 + 6 = 22, AC = 24 → SPI = CPI = 0,92; EAC = 40 / 0,92 = 43,48
        // ES = 2 + (22 − 16)/8 = 2,75; AT = 3; SPI(t) = 0,92; tahmini süre = 5 / 0,92 = 5,43 → 6 gün → 9 Kasım Pzt.
        var r = EarnedValue.Compute(Baseline,
            [new(1, 100, 20, true), new(2, 25, 4, false)], new DateOnly(2026, 11, 4));

        Assert.Equal(40, r.BudgetAtCompletion);
        Assert.Equal(24, r.PlannedValue);
        Assert.Equal(22, r.EarnedValue);
        Assert.Equal(24, r.ActualCost);
        Assert.Equal(0.92m, r.Spi);
        Assert.Equal(0.92m, r.Cpi);
        Assert.Equal(43.48m, r.EstimateAtCompletion);
        Assert.Equal(2.75m, r.EarnedSchedule);
        Assert.Equal(3, r.ActualTime);
        Assert.Equal(0.92m, r.SpiTime);
        Assert.Equal(new DateOnly(2026, 11, 9), r.ForecastFinish);
        Assert.Equal(-2, r.ScheduleVariance);
        Assert.Equal(-2, r.CostVariance);
        Assert.Equal(55, r.PercentComplete);
        Assert.Equal(4000, r.BudgetAtCompletionCost);
        Assert.Equal(4364, r.EstimateAtCompletionCost);   // 4000 / (2200 / 2400)
        Assert.Equal([8m, 16m, 24m, 32m, 40m], r.PlannedCurve);
    }

    [Fact]
    public void Before_start_there_is_no_index()
    {
        var r = EarnedValue.Compute(Baseline, [new(1, 0, 0, false), new(2, 0, 0, false)], new DateOnly(2026, 10, 20));
        Assert.Equal(0, r.PlannedValue);
        Assert.Null(r.Spi);
        Assert.Null(r.SpiTime);
        Assert.Null(r.ForecastFinish);
    }

    [Fact]
    public void Finished_project_forecasts_status_date_and_late_work_is_detected()
    {
        // Plan 6 Kasım'da bitecekti; her şey 10 Kasım Salı'da bitti → SPI(t) = 5 / 7 < 1, tahmin = bugün.
        var done = EarnedValue.Compute(Baseline, [new(1, 100, 16, true), new(2, 100, 24, true)], new DateOnly(2026, 11, 10));
        Assert.Equal(1m, done.Spi);                           // klasik SPI sonda 1'e döner…
        Assert.Equal(0.71m, done.SpiTime);                     // …SPI(t) gecikmeyi göstermeye devam eder
        Assert.Equal(new DateOnly(2026, 11, 10), done.ForecastFinish);
    }

    [Fact]
    public void Work_not_in_baseline_is_ignored()
    {
        var r = EarnedValue.Compute(Baseline, [new(1, 100, 16, true), new(99, 100, 50, true)], new DateOnly(2026, 11, 3));
        Assert.Equal(16, r.EarnedValue);
        Assert.Equal(16, r.ActualCost);
    }
}

public class AhpTests
{
    [Fact]
    public void Saaty_three_criteria_example()
    {
        // Saaty'nin klasik örneği: ağırlıklar ≈ 0,637 / 0,258 / 0,105; λmax ≈ 3,039; CR ≈ 0,033
        var r = Ahp.Compute(Ahp.Parse(["1 3 5", "1/3 1 3", "1/5 1/3 1"]));

        Assert.Equal(0.637, (double)r.Weights[0], 2);
        Assert.Equal(0.258, (double)r.Weights[1], 2);
        Assert.Equal(0.105, (double)r.Weights[2], 2);
        Assert.Equal(3.039, (double)r.LambdaMax, 2);
        Assert.Equal(0.033, (double)r.ConsistencyRatio, 2);
        Assert.True(r.IsConsistent);
        Assert.Equal(1m, Math.Round(r.Weights.Sum(), 3));
    }

    [Fact]
    public void Perfectly_consistent_matrix_has_zero_cr()
    {
        var r = Ahp.Compute(Ahp.Parse(["1 2 4", "1/2 1 2", "1/4 1/2 1"]));
        Assert.Equal(0, (double)r.ConsistencyRatio, 3);
        Assert.Equal(4.0 / 7, (double)r.Weights[0], 3);
    }

    [Fact]
    public void Inconsistent_matrix_is_flagged()
    {
        // A > B (9), B > C (9), ama C > A (9): tutarsız
        var r = Ahp.Compute(Ahp.Parse(["1 9 1/9", "1/9 1 9", "9 1/9 1"]));
        Assert.False(r.IsConsistent);
    }
}
