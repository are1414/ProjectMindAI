namespace ProjectMind.Application.Planning;

/// <summary>Hafta içi (Pazartesi–Cuma) çalışma takvimi. Gün indeksi 0 = planın ilk iş günü.</summary>
public sealed class WorkCalendar
{
    public const int WorkDaysPerWeek = 5;

    public WorkCalendar(DateOnly start) => FirstDay = NextWorkday(start);

    public DateOnly FirstDay { get; }

    public static bool IsWorkday(DateOnly date) => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    public static DateOnly NextWorkday(DateOnly date)
    {
        while (!IsWorkday(date))
            date = date.AddDays(1);
        return date;
    }

    public DateOnly ToDate(int dayIndex)
    {
        var date = FirstDay;
        for (var i = 0; i < dayIndex; i++)
            date = NextWorkday(date.AddDays(1));
        return date;
    }

    /// <summary>İki tarih arasındaki iş günü farkı (to - from); to önceyse negatif.</summary>
    public static int WorkdaysBetween(DateOnly from, DateOnly to)
    {
        var sign = 1;
        if (to < from)
            (from, to, sign) = (to, from, -1);
        var count = 0;
        for (var d = from; d < to; d = d.AddDays(1))
            if (IsWorkday(d.AddDays(1)))
                count++;
        return sign * count;
    }
}
