using ProjectMind.Application.Planning;

namespace ProjectMind.Application.WhatIf;

/// <summary>
/// Efor belirsizliğiyle Monte Carlo: her turda her işin kalan eforu üçgen dağılımlı bir çarpanla ölçeklenir ve kaynak kısıtlı
/// çizelge yeniden çıkarılır. Çarpan (tohum, tur, iş) üçlüsünden türetilir: senaryolar aynı rastgele sayıları paylaşır
/// (ortak rastgele sayılar), farklar gürültüden değil senaryodan gelir.
/// </summary>
public static class MonteCarlo
{
    public const double P50 = 0.5, P80 = 0.8, P90 = 0.9;

    public static MonteCarloResult Run(PlanInput input, DateOnly targetDate, WhatIfOptions o, CancellationToken ct = default)
    {
        var finishes = new int[o.Iterations];
        var costs = new decimal[o.Iterations];
        for (var i = 0; i < o.Iterations; i++)
        {
            ct.ThrowIfCancellationRequested();
            var iteration = i;
            var sampled = input with
            {
                Activities = input.Activities
                    .Select(a => a with { Hours = Math.Round(a.Hours * (decimal)Triangular(Uniform(o.Seed, iteration, a.Id), o.EffortMin, o.EffortMode, o.EffortMax), 2) })
                    .ToList()
            };
            var plan = ResourceScheduler.Schedule(sampled);
            finishes[i] = plan.Activities.Count == 0 ? 0 : plan.DurationWorkdays - 1;
            costs[i] = plan.PlannedCost;
        }

        Array.Sort(finishes);
        Array.Sort(costs);
        var calendar = new WorkCalendar(input.Start);
        var targetDay = WorkCalendar.WorkdaysBetween(calendar.FirstDay, targetDate);

        var distribution = finishes
            .Select((day, index) => (day, index))
            .GroupBy(x => x.day)
            .Select(g => new FinishProbability(calendar.ToDate(g.Key), (g.Max(x => x.index) + 1) / (double)finishes.Length))
            .ToList();

        return new MonteCarloResult(
            o.Iterations,
            calendar.ToDate(Percentile(finishes, P50)),
            calendar.ToDate(Percentile(finishes, P80)),
            calendar.ToDate(Percentile(finishes, P90)),
            Percentile(costs, P50),
            Percentile(costs, P80),
            finishes.Count(f => f <= targetDay) / (double)finishes.Length,
            distribution);
    }

    /// <summary>En yakın sıra yöntemi: sıralı dizide ⌈p·n⌉. eleman.</summary>
    public static T Percentile<T>(IReadOnlyList<T> sorted, double p) =>
        sorted[Math.Clamp((int)Math.Ceiling(p * sorted.Count) - 1, 0, sorted.Count - 1)];

    /// <summary>Üçgen dağılımın ters CDF'i.</summary>
    public static double Triangular(double u, double min, double mode, double max)
    {
        var split = (mode - min) / (max - min);
        return u < split
            ? min + Math.Sqrt(u * (max - min) * (mode - min))
            : max - Math.Sqrt((1 - u) * (max - min) * (max - mode));
    }

    /// <summary>(tohum, tur, iş) için [0,1) aralığında belirlenimci sayı (SplitMix64).</summary>
    public static double Uniform(int seed, int iteration, int activityId)
    {
        var x = unchecked((ulong)seed * 0x9E3779B97F4A7C15UL ^ (ulong)iteration * 0xBF58476D1CE4E5B9UL ^ (ulong)(uint)activityId * 0x94D049BB133111EBUL);
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        x ^= x >> 31;
        return (x >> 11) * (1.0 / (1UL << 53));
    }
}
