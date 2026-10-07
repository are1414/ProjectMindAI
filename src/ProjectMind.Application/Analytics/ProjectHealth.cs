namespace ProjectMind.Application.Analytics;

public enum HealthCriterion { Schedule, Cost, Scope, Resource, Risk }

public enum HealthLevel { Good, Warning, Critical }

public sealed record HealthComponent(HealthCriterion Criterion, decimal Weight, decimal? Score);

public sealed record HealthResult(decimal? Score, HealthLevel? Level, IReadOnlyList<HealthComponent> Components, AhpResult Ahp);

/// <summary>
/// Proje sağlık skoru (0–100) = AHP ağırlıklarıyla bileşen puanlarının ağırlıklı ortalaması.
/// Hesaplanamayan bileşen (ör. baseline yokken takvim) dışarıda bırakılır, ağırlıklar kalanlar arasında yeniden ölçeklenir.
/// </summary>
public static class ProjectHealth
{
    public static HealthResult Compute(IReadOnlyDictionary<HealthCriterion, decimal?> scores, HealthOptions options)
    {
        var ahp = Ahp.Compute(Ahp.Parse(options.PairwiseMatrix));
        var criteria = Enum.GetValues<HealthCriterion>();
        var components = criteria
            .Select((c, i) => new HealthComponent(c, ahp.Weights[i], scores.GetValueOrDefault(c) is { } s ? Math.Clamp(s, 0, 100) : null))
            .ToList();

        var available = components.Where(c => c.Score is not null).ToList();
        var weight = available.Sum(c => c.Weight);
        decimal? score = weight == 0 ? null : Math.Round(available.Sum(c => c.Weight * c.Score!.Value) / weight, 1);
        HealthLevel? level = score switch
        {
            null => null,
            >= 0 when score >= options.GoodThreshold => HealthLevel.Good,
            >= 0 when score >= options.WarningThreshold => HealthLevel.Warning,
            _ => HealthLevel.Critical
        };
        return new HealthResult(score, level, components, ahp);
    }

    /// <summary>Endeks (SPI/CPI) → puan: 1 ve üzeri 100, altı orantılı.</summary>
    public static decimal? IndexScore(decimal? index) => index is { } i ? Math.Round(Math.Min(i, 1m) * 100, 1) : null;
}
