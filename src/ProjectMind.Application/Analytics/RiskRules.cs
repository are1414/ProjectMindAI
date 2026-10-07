namespace ProjectMind.Application.Analytics;

public enum AlertSeverity { Info, Warning, Critical }

public sealed record RiskAlert(AlertSeverity Severity, string Title, string Detail);

public sealed record RiskInput(
    bool HasBaseline,
    EvmResult? Evm,
    DateOnly TargetEnd,
    DateOnly? PlanFinish,
    decimal ScopeGrowthPercent,
    IReadOnlyList<string> BlockedItems,
    IReadOnlyList<string> OverdueItems,
    IReadOnlyList<string> PlanWarnings);

/// <summary>Kural tabanlı risk uyarıları (açıklanabilir; eşikler HealthOptions'tan).</summary>
public static class RiskRules
{
    public static IReadOnlyList<RiskAlert> Evaluate(RiskInput input, HealthOptions o)
    {
        var alerts = new List<RiskAlert>();

        if (!input.HasBaseline)
            alerts.Add(new(AlertSeverity.Info, "Baseline yok",
                "İlerleme ölçülebilmesi için Plan sekmesinden planı uygulayıp baseline kaydedin."));

        if (input.Evm is { } evm)
        {
            Index(alerts, "Takvim performansı (SPI(t))", evm.SpiTime ?? evm.Spi, o,
                "plana göre geride; aynı hızla devam edilirse gecikme olur");
            Index(alerts, "Maliyet/efor performansı (CPI)", evm.Cpi, o,
                "harcanan efor, kazanılan değerden fazla; tahmini toplam efor bütçeyi aşıyor");

            if (evm.ForecastFinish is { } forecast && forecast > input.TargetEnd)
                alerts.Add(new(AlertSeverity.Critical, "Hedef tarih riski",
                    $"Mevcut hızla tahmini bitiş {Common.Format.Date(forecast)}, hedef {Common.Format.Date(input.TargetEnd)}."));
        }
        else if (input.PlanFinish is { } finish && finish > input.TargetEnd)
        {
            alerts.Add(new(AlertSeverity.Warning, "Plan hedefi aşıyor",
                $"Otomatik plana göre bitiş {Common.Format.Date(finish)}, hedef {Common.Format.Date(input.TargetEnd)}."));
        }

        if (input.ScopeGrowthPercent >= o.ScopeGrowthWarningPercent)
            alerts.Add(new(AlertSeverity.Warning, "Kapsam büyümesi",
                $"Toplam efor baseline'a göre %{Common.Format.Number(input.ScopeGrowthPercent)} arttı."));

        if (input.BlockedItems.Count > 0)
            alerts.Add(new(AlertSeverity.Warning, "Bloke işler", string.Join(", ", input.BlockedItems)));

        if (input.OverdueItems.Count > 0)
            alerts.Add(new(AlertSeverity.Critical, "Planlanan bitişi geçmiş işler", string.Join(", ", input.OverdueItems)));

        alerts.AddRange(input.PlanWarnings.Select(w => new RiskAlert(AlertSeverity.Warning, "Kaynak", w)));
        return alerts.OrderByDescending(a => a.Severity).ToList();
    }

    private static void Index(List<RiskAlert> alerts, string title, decimal? value, HealthOptions o, string meaning)
    {
        if (value is not { } v)
            return;
        var text = v.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
        if (v < o.IndexCritical)
            alerts.Add(new(AlertSeverity.Critical, title, $"{text}: {meaning}."));
        else if (v < o.IndexWarning)
            alerts.Add(new(AlertSeverity.Warning, title, $"{text}: {meaning}."));
    }
}
