using ProjectMind.Application.Planning;

namespace ProjectMind.Application.Analytics;

public sealed record EvmBaselineItem(int WorkItemId, DateOnly Start, DateOnly End, decimal Hours, decimal HourlyCost);

/// <param name="CompletedOn">İşin son kez tamamlandığı gün (ilerleme geçmişinden; bilinmiyorsa null). Biten projede AT'yi dondurur.</param>
public sealed record EvmProgress(
    int WorkItemId, int PercentComplete, decimal ActualHours, bool IsDone, bool IsCancelled = false, DateOnly? CompletedOn = null);

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
    IReadOnlyList<decimal> PlannedCurve,
    decimal DescopedHours = 0)
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
/// Baseline'daki bir iş sonradan iptal edilirse kapsamdan çıkarılır (descope): BAC'den ve PV eğrisinden düşülür,
/// <see cref="EvmResult.DescopedHours"/> olarak raporlanır; o işe harcanmış saat ise gerçekleşen maliyet olarak AC'de kalır.
/// Kalan kapsamın tamamı kazanıldıysa (EV ≥ BAC) AT, tamamlanma gününde dondurulur (standart ES: SPI(t) = PD / gerçek süre);
/// tamamlanma günü, kalan işlerin <see cref="EvmProgress.CompletedOn"/> değerlerinin en geç olanıdır (biri bilinmiyorsa durum günü).
/// Kapsamın tamamı iptal edildiyse (BAC = 0) SPI, SPI(t), CPI ve EAC anlamsızdır: null döner.
/// </summary>
public static class EarnedValue
{
    private const decimal FullPercent = 100m;

    public static EvmResult Compute(
        IReadOnlyList<EvmBaselineItem> baseline, IReadOnlyList<EvmProgress> progress, DateOnly statusDate)
    {
        var cancelled = progress.Where(p => p.IsCancelled).Select(p => p.WorkItemId).ToHashSet();
        var active = baseline.Where(b => !cancelled.Contains(b.WorkItemId)).ToList();
        var descoped = baseline.Where(b => cancelled.Contains(b.WorkItemId)).Sum(b => b.Hours);

        // Takvim kalan kapsamdan kurulur; her şey iptal edildiyse (BAC = 0) baseline'ın tarihleri kullanılır.
        var scope = active.Count > 0 ? active : baseline;
        var calendar = new WorkCalendar(scope.Min(b => b.Start));
        var plannedFinish = scope.Max(b => b.End);
        var days = WorkCalendar.WorkdaysBetween(calendar.FirstDay, plannedFinish) + 1;

        // Kümülatif planlanan saat: curve[k] = k. iş gününün sonuna kadar planlanan toplam.
        var daily = new decimal[days];
        foreach (var item in active)
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

        var bac = active.Sum(b => b.Hours);
        var statusIndex = statusDate < calendar.FirstDay ? -1 : WorkCalendar.WorkdaysBetween(calendar.FirstDay, statusDate);
        var pv = statusIndex < 0 ? 0 : statusIndex >= days ? bac : curve[statusIndex];

        var progressById = progress.ToDictionary(p => p.WorkItemId);
        decimal ev = 0, ac = 0, evCost = 0, acCost = 0;
        // Bir iş baseline'da birden çok satırla (kazanılmış + kalan kısım) bulunabilir: EV satır bazında, AC iş başına bir kez.
        foreach (var work in baseline.GroupBy(b => b.WorkItemId))
        {
            if (!progressById.TryGetValue(work.Key, out var p))
                continue;
            var percent = p.IsCancelled ? 0 : p.IsDone ? FullPercent : p.PercentComplete;
            var hours = work.Sum(i => i.Hours);
            var earned = hours * percent / FullPercent;
            var rate = hours > 0 ? work.Sum(i => i.Hours * i.HourlyCost) / hours : work.First().HourlyCost;
            ev += earned;
            ac += p.ActualHours;
            evCost += earned * rate;
            acCost += p.ActualHours * rate;
        }

        var hasScope = bac > 0;
        decimal? spi = hasScope && pv > 0 ? Round(ev / pv) : null;
        decimal? cpi = hasScope && ac > 0 ? Round(ev / ac) : null;
        decimal? eac = cpi is > 0 ? Round(bac / cpi.Value) : null;

        decimal? es = null, at = null, spiT = null;
        DateOnly? forecast = null;
        if (statusIndex >= 0 && hasScope)
        {
            var finished = ev >= bac;
            // Biten projede gerçek süre tamamlanma gününde durur; aksi halde durum gününe kadar geçen iş günü (dahil).
            var atIndex = statusIndex;
            // Plan başlangıcından önceki bir tamamlanma günü tutarsız kayıttır (saat farkı vb.): yok sayılır.
            if (finished && CompletionDate(active, progressById) is { } completed
                && completed < statusDate && completed >= calendar.FirstDay)
                atIndex = WorkCalendar.WorkdaysBetween(calendar.FirstDay, completed);
            at = atIndex + 1;
            es = EarnedScheduleDays(curve, ev);
            spiT = Round(es.Value / at.Value);
            if (finished)
                forecast = calendar.ToDate(atIndex);   // iş bitti: tamamlanma günü
            else if (spiT is > 0)
                forecast = calendar.ToDate(Math.Max((int)Math.Ceiling(days / spiT.Value) - 1, 0));
        }

        var bacCost = active.Sum(b => b.Hours * b.HourlyCost);
        decimal? costCpi = hasScope && acCost > 0 ? evCost / acCost : null;
        decimal? eacCost = costCpi is > 0 ? Math.Round(bacCost / costCpi.Value, 0) : null;

        return new EvmResult(statusDate, bac, Round(pv), Round(ev), Round(ac), spi, cpi, es is null ? null : Round(es.Value),
            at, spiT, eac, days, calendar.FirstDay, plannedFinish, forecast, bacCost, eacCost, curve, descoped);
    }

    /// <summary>Kalan kapsamdaki işlerin en geç tamamlanma günü; herhangi birinin günü bilinmiyorsa null.</summary>
    private static DateOnly? CompletionDate(IEnumerable<EvmBaselineItem> active, IReadOnlyDictionary<int, EvmProgress> progress)
    {
        DateOnly? latest = null;
        foreach (var id in active.Select(b => b.WorkItemId).Distinct())
        {
            if (!progress.TryGetValue(id, out var p) || p.CompletedOn is not { } day)
                return null;
            if (latest is null || day > latest)
                latest = day;
        }
        return latest;
    }

    /// <summary>
    /// İlerleme geçmişinden (eskiden yeniye) işin son tamamlanma günü: tamamlanmış (Bitti veya %100) son kesintisiz kayıt
    /// dizisinin ilk günü. Son kayıt tamamlanmış değilse null. Bitti işe sonradan saat girilmesi günü değiştirmez.
    /// </summary>
    public static DateOnly? CompletedOn(IEnumerable<(DateOnly Date, bool Complete)> history)
    {
        DateOnly? since = null;
        foreach (var (date, complete) in history)
            since = complete ? since ?? date : null;
        return since;
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
