namespace ProjectMind.Application.Analytics;

/// <summary>appsettings "Health": sağlık skoru (AHP) ve risk uyarı eşikleri.</summary>
public sealed class HealthOptions
{
    public const string SectionName = "Health";

    /// <summary>
    /// AHP ikili karşılaştırma matrisi (Saaty 1–9 ölçeği). Satır/sütun sırası: Takvim, Maliyet, Kapsam, Kaynak, Risk.
    /// Örn. 1. satır 2. sütun "2": takvim, maliyetten biraz daha önemli.
    /// </summary>
    public List<string> PairwiseMatrix { get; set; } =
    [
        "1   2   3   3   2",
        "1/2 1   2   2   1",
        "1/3 1/2 1   1   1/2",
        "1/3 1/2 1   1   1/2",
        "1/2 1   2   2   1"
    ];

    public decimal GoodThreshold { get; set; } = 75;
    public decimal WarningThreshold { get; set; } = 50;

    public decimal IndexWarning { get; set; } = 0.95m;   // SPI/CPI bunun altı: dikkat
    public decimal IndexCritical { get; set; } = 0.85m;  // bunun altı: kritik
    public decimal ScopeGrowthWarningPercent { get; set; } = 10;
    public decimal ScopePenaltyPerPercent { get; set; } = 2;   // kapsam %1 büyürse kapsam puanı 2 düşer
}
