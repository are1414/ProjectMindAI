namespace ProjectMind.Application.Ml;

public sealed record SyntheticOptions(int Projects = 400, int Seed = 42, float DelayTolerance = 0.10f);

/// <summary>Simüle edilen bir projenin özeti (gizli değişkenler sadece analiz/raporlama içindir, modele verilmez).</summary>
public sealed record SyntheticProject(
    int Id, int TeamSize, int PlannedWeeks, int ActualWeeks, bool Delayed, float DurationRatio,
    float HiddenProductivity, float HiddenEstimationBias, float HiddenVolatility, float HiddenBlockerRate,
    IReadOnlyList<DelaySample> Samples);

/// <summary>
/// Haftalık ayrık zaman simülasyonu ile sentetik yazılım projeleri üretir. Gecikme bir formülden değil, gizli değişkenlerin
/// (ekip verimliliği, tahmin iyimserliği, kapsam oynaklığı, engel oranı) ve rastlantısal gürültünün (izin, devir, tek seferlik
/// olaylar) haftalık etkileşiminden ortaya çıkar. Aynı tohum (seed) aynı veri setini üretir.
/// </summary>
public static class SyntheticProjectGenerator
{
    public static readonly int[] Checkpoints = [25, 50, 75];

    private const float HoursPerPersonWeek = 40f;
    private const float PlannedFocusFactor = 0.8f;   // planlamada kişi başı haftalık 32 saat varsayılır
    private const int MaxWeeksFactor = 4;
    private const int VelocityWindowWeeks = 2;
    private const float ReportingNoise = 0.03f;

    public static IReadOnlyList<SyntheticProject> Generate(SyntheticOptions options)
    {
        var random = new Random(options.Seed);
        return Enumerable.Range(1, options.Projects).Select(id => Simulate(id, random, options.DelayTolerance)).ToList();
    }

    private static SyntheticProject Simulate(int id, Random r, float tolerance)
    {
        var team = r.Next(2, 9);
        var tasks = r.Next(10, 61);
        var bac = 0f;
        for (var i = 0; i < tasks; i++)
            bac += Math.Clamp(LogNormal(r, MathF.Log(24), 0.5f), 4, 120);

        var plannedWeeks = Math.Max(2, (int)MathF.Ceiling(bac / (team * HoursPerPersonWeek * PlannedFocusFactor)));

        // Gizli değişkenler
        var productivity = LogNormal(r, 0.15f, 0.2f);        // 1 = planlandığı gibi
        var estimationBias = Uniform(r, 0.8f, 1.35f);         // gerçek efor / tahmini efor
        var volatility = Uniform(r, 0f, 0.05f);               // haftalık kapsam ekleme olasılığı ölçeği
        var blockerRate = Uniform(r, 0f, 0.12f);              // kapasitenin engellerle kaybolan ortalama payı

        var trueTotal = bac * estimationBias;
        var trueDone = 0f;
        var added = 0f;
        var ac = 0f;
        var evHistory = new List<float> { 0 };
        var pvHistory = new List<float> { 0 };
        var samples = new List<(int Checkpoint, DelayFeatures F)>();
        var checkpointWeeks = Checkpoints.ToDictionary(c => c, c => Math.Max(1, (int)MathF.Round(plannedWeeks * c / 100f)));

        var week = 0;
        var turnoverWeeks = 0;
        while (trueDone < trueTotal && week < plannedWeeks * MaxWeeksFactor)
        {
            week++;
            if (r.NextDouble() < 0.03)
                turnoverWeeks = 2;                               // ekipten biri ayrıldı / devir teslim
            var blockedShare = Math.Clamp(blockerRate + Normal(r, 0, 0.05f), 0, 0.6f);
            var availability = Uniform(r, 0.82f, 0.98f);       // izin, toplantı, destek işleri
            var hours = team * HoursPerPersonWeek * availability * (1 - blockedShare);
            var effectiveness = productivity * Math.Max(0.4f, Normal(r, 1, 0.12f)) * (turnoverWeeks-- > 0 ? 0.7f : 1f);

            trueDone = Math.Min(trueTotal, trueDone + hours * PlannedFocusFactor * effectiveness);
            ac += hours;

            if (r.NextDouble() < volatility * 10)               // kapsam değişikliği
            {
                var extra = bac * Uniform(r, 0.01f, 0.05f);
                added += extra;
                trueTotal += extra * estimationBias;
            }

            // "%90 sendromu": ekip ilerlemeyi tahmini efora göre raporlar; gerçek iş büyüklüğü ilerledikçe ortaya çıkar.
            // Ayrıca raporlama gürültülüdür. Bu yüzden erken dönemde EVM iyimser kalır.
            var revealed = bac + (trueTotal - bac) * (trueDone / trueTotal);
            var ev = trueDone >= trueTotal ? bac : Math.Clamp(bac * trueDone / revealed * (1 + Normal(r, 0, ReportingNoise)), 0, bac);
            var pv = bac * Math.Min(1f, (float)week / plannedWeeks);
            evHistory.Add(ev);
            pvHistory.Add(pv);

            foreach (var (checkpoint, cw) in checkpointWeeks)
            {
                if (cw != week)
                    continue;
                var es = ev / bac * plannedWeeks;
                var back = Math.Max(0, week - VelocityWindowWeeks);
                var recentPv = pv - pvHistory[back];
                samples.Add((checkpoint, new DelayFeatures(
                    ElapsedRatio: (float)week / plannedWeeks,
                    PercentComplete: ev / bac,
                    Spi: pv > 0 ? ev / pv : 1,
                    Cpi: ac > 0 ? ev / ac : 1,
                    SpiTime: es / week,
                    RecentVelocity: recentPv > 0 ? (ev - evHistory[back]) / recentPv : 1,
                    ScopeGrowth: added / bac,
                    BlockedShare: blockedShare,
                    TeamSize: team)));
            }
        }

        var actualWeeks = week;
        var ratio = (float)actualWeeks / plannedWeeks;
        var delayed = ratio > 1 + tolerance;
        return new SyntheticProject(id, team, plannedWeeks, actualWeeks, delayed, ratio,
            productivity, estimationBias, volatility, blockerRate,
            samples.Select(s => new DelaySample(id, s.Checkpoint, s.F, delayed, ratio)).ToList());
    }

    private static float Uniform(Random r, float min, float max) => min + (float)r.NextDouble() * (max - min);

    private static float Normal(Random r, float mean, float sd)
    {
        var u1 = 1 - r.NextDouble();
        var u2 = r.NextDouble();
        return mean + sd * (float)(Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2));
    }

    private static float LogNormal(Random r, float mu, float sigma) => MathF.Exp(Normal(r, mu, sigma));
}
