using ProjectMind.Application.Analytics;

namespace ProjectMind.Tests.Unit;

public class HealthAndRiskTests
{
    private static readonly HealthOptions Options = new();

    [Fact]
    public void Default_ahp_matrix_is_consistent_and_schedule_weighs_most()
    {
        var r = ProjectHealth.Compute(new Dictionary<HealthCriterion, decimal?>(), Options);
        Assert.True(r.Ahp.IsConsistent);
        Assert.Equal(HealthCriterion.Schedule, r.Components.MaxBy(c => c.Weight)!.Criterion);
        Assert.Null(r.Score);
    }

    [Fact]
    public void Score_is_weighted_average_of_available_components()
    {
        // Sadece Takvim (80) ve Maliyet (100) var → ağırlıklar ikisi arasında yeniden ölçeklenir.
        var r = ProjectHealth.Compute(new Dictionary<HealthCriterion, decimal?>
        {
            [HealthCriterion.Schedule] = 80, [HealthCriterion.Cost] = 100
        }, Options);

        var ws = r.Components.Single(c => c.Criterion == HealthCriterion.Schedule).Weight;
        var wc = r.Components.Single(c => c.Criterion == HealthCriterion.Cost).Weight;
        Assert.Equal(Math.Round((ws * 80 + wc * 100) / (ws + wc), 1), r.Score);
        Assert.Equal(HealthLevel.Good, r.Level);
    }

    [Theory]
    [InlineData(0.80, 80)]
    [InlineData(1.20, 100)]
    public void Index_score_caps_at_100(decimal index, decimal expected) =>
        Assert.Equal(expected, ProjectHealth.IndexScore(index));

    [Fact]
    public void Risk_rules_raise_expected_alerts()
    {
        var evm = EarnedValue.Compute(
            [new(1, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 6), 40, 0)],
            [new(1, 20, 30, false)], new DateOnly(2026, 11, 6));   // PV 40, EV 8, AC 30 → SPI 0,2, CPI 0,27

        var alerts = RiskRules.Evaluate(new RiskInput(true, evm, new DateOnly(2026, 11, 6), null, 15,
            ["Ödeme API"], ["Login API"], []), Options);

        Assert.Contains(alerts, a => a.Title.StartsWith("Takvim") && a.Severity == AlertSeverity.Critical);
        Assert.Contains(alerts, a => a.Title.StartsWith("Maliyet") && a.Severity == AlertSeverity.Critical);
        Assert.Contains(alerts, a => a.Title == "Hedef tarih riski");
        Assert.Contains(alerts, a => a.Title == "Kapsam büyümesi");
        Assert.Contains(alerts, a => a.Title == "Bloke işler" && a.Detail == "Ödeme API");
        Assert.Contains(alerts, a => a.Title.StartsWith("Planlanan bitişi geçmiş"));
        Assert.Equal(AlertSeverity.Critical, alerts[0].Severity);   // en önemli uyarı başta
    }

    [Fact]
    public void Missing_baseline_is_reported_as_info() =>
        Assert.Contains(RiskRules.Evaluate(new RiskInput(false, null, new DateOnly(2026, 12, 1), null, 0, [], [], []), Options),
            a => a.Severity == AlertSeverity.Info && a.Title == "Baseline yok");
}
