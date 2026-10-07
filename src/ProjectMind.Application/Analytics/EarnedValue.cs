using ProjectMind.Application.Planning;

namespace ProjectMind.Application.Analytics;

public sealed record EvmBaselineItem(int WorkItemId, DateOnly Start, DateOnly End, decimal Hours, decimal HourlyCost);

public sealed record EvmProgress(int WorkItemId, int PercentComplete, decimal ActualHours, bool IsDone);

public sealed record EvmResult(
    DateOnly StatusDate,
    decimal BudgetAtCompletion,
    decimal PlannedValue,
    decimal EarnedValue,
    decimal ActualCost,
    decimal? Spi,
    decimal? Cpi,
    decimal? EarnedSchedule,
    decimal? ActualTime,
    decimal? SpiTime,
    decimal? EstimateAtCompletion,
    decimal PlannedDurationDays,
    DateOnly PlannedStart,
    DateOnly PlannedFinish,
    DateOnly? ForecastFinish,
    decimal BudgetAtCompletionCost,
    decimal? EstimateAtCompletionCost,
    IReadOnlyList<decimal> PlannedCurve)
{
    public decimal ScheduleVariance => EarnedValue - PlannedValue;
    public decimal CostVariance => EarnedValue - ActualCost;
    public decimal PercentComplete => BudgetAtCompletion == 0 ? 0 : Math.Round(EarnedValue / BudgetAtCompletion * 100, 1);
}

/// <summary>
/// Kazanılmış Değer Yönetimi (EVM) ve Kazanılmış Takvim (Earned Schedule), efor (saat) bazında:
///   PV = baseline'a göre durum tarihine kadar planlanan saat (her iş, iş günlerine eşit yayılır),
///   EV = baseline saati × % tamamlanma, AC = harcanan saat,
///   SPI = EV/PV, CPI = EV/AC, EAC = BAC/CPI,
///   ES = PV eğrisinin EV'ye ulaştığı zaman (iş günü), SPI(t) = ES/AT, tahmini süre = PD/SPI(t).
/// Para birimi değerleri saat × saatlik maliyetten türetilir. Baseline'da olmayan (sonradan eklenen) işler EV/AC'ye girmez.
/// </summary>
public static class EarnedValue
{
    private const decimal FullPercent = 100m;

    public static EvmResult Compute(
        IReadOnlyList<EvmBaselineItem> baseline, IReadOnlyList<EvmProgress> progress, DateOnly statusDate)
    {
        var calendar = new WorkCalendar(baseline.Min(b => b.Start));
        var plannedFinish = baseline.Max(b => b.End);
        var days = WorkCalendar.WorkdaysBetween(calendar.FirstDay, plannedFinish) + 1;

        // Kümülatif planlanan saat: curve[k] = k. iş gününün sonuna kadar planlanan toplam.
        var daily = new decimal[days];
        foreach (var item in baseline)
        {
            var first = WorkCalendar.WorkdaysBetween(calendar.FirstDay, WorkCalendar.NextWorkday(item.Start));
            var last = WorkCalendar.WorkdaysBetween(calendar.FirstDay, item.End);
            var span = Math.Max(last - first + 1, 1);
            for (var d = first; d < first + span && d < days; d++)
                daily[d] += item.Hours / span;
        }
        var curve = new decimal[days];
        decimal running = 0;
        for (var d = 0; d < days; d++)
            curve[d] = running += daily[d];

        var bac = baseline.Sum(b => b.Hours);
        var statusIndex = statusDate < calendar.FirstDay ? -1 : WorkCalendar.WorkdaysBetween(calendar.FirstDay, statusDate);
        var pv = statusIndex < 0 ? 0 : statusIndex >= days ? bac : curve[statusIndex];

        var progressById = progress.ToDictionary(p => p.WorkItemId);
        decimal ev = 0, ac = 0, evCost = 0, acCost = 0;
        foreach (var item in baseline)
        {
            if (!progressById.TryGetValue(item.WorkItemId, out var p))
                continue;
            var earned = item.Hours * (p.IsDone ? FullPercent : p.PercentComplete) / FullPercent;
            ev += earned;
            ac += p.ActualHours;
            evCost += earned * item.HourlyCost;
            acCost += p.ActualHours * item.HourlyCost;
        }

        decimal? spi = pv > 0 ? Round(ev / pv) : null;
        decimal? cpi = ac > 0 ? Round(ev / ac) : null;
        decimal? eac = cpi is > 0 ? Round(bac / cpi.Value) : null;

        decimal? es = null, at = null, spiT = null;
        DateOnly? forecast = null;
        if (statusIndex >= 0)
        {
            at = statusIndex + 1;   // geçen iş günü (durum günü dahil)
            es = EarnedScheduleDays(curve, ev);
            spiT = at > 0 ? Round(es.Value / at.Value) : null;
            if (ev >= bac && bac > 0)
                forecast = calendar.ToDate(Math.Max((int)Math.Ceiling(at.Value) - 1, 0));   // iş bitti: bugün
            else if (spiT is > 0)
                forecast = calendar.ToDate(Math.Max((int)Math.Ceiling(days / spiT.Value) - 1, 0));
        }

        var bacCost = baseline.Sum(b => b.Hours * b.HourlyCost);
        decimal? costCpi = acCost > 0 ? evCost / acCost : null;
        decimal? eacCost = costCpi is > 0 ? Math.Round(bacCost / costCpi.Value, 0) : null;

        return new EvmResult(statusDate, bac, Round(pv), Round(ev), Round(ac), spi, cpi, es is null ? null : Round(es.Value),
            at, spiT, eac, days, calendar.FirstDay, plannedFinish, forecast, bacCost, eacCost, curve);
    }

    /// <summary>PV eğrisinde EV'ye ulaşılan zaman: tam gün sayısı + kesirli kısım (doğrusal ara değer).</summary>
    public static decimal EarnedScheduleDays(IReadOnlyList<decimal> curve, decimal ev)
    {
        var c = 0;
        while (c < curve.Count && curve[c] <= ev)
            c++;
        if (c >= curve.Count)
            return curve.Count;
        var previous = c == 0 ? 0 : curve[c - 1];
        var step = curve[c] - previous;
        return c + (step > 0 ? (ev - previous) / step : 0);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2);
}
