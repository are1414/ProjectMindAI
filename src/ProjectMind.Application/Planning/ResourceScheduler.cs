using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.Planning;

public sealed record PlanActivity(
    int Id, string Name, decimal Hours, Skill Skill, Priority Priority, int? FixedAssigneeId, IReadOnlyList<int> Predecessors);

/// <summary>Bir gün aralığında [FromDay, ToDay] kapasite çarpanı (what-if: henüz katılmamış = 0, ısınma, mentorluk yükü).</summary>
public sealed record CapacityWindow(int FromDay, int ToDay, decimal Factor);

public sealed record PlanResource(
    int Id, string Name, Skill Skills, decimal WeeklyCapacityHours, decimal HourlyCost,
    IReadOnlyList<CapacityWindow>? Windows = null)
{
    public decimal DailyCapacity => WeeklyCapacityHours / WorkCalendar.WorkDaysPerWeek;

    public decimal CapacityOn(int day)
    {
        var capacity = DailyCapacity;
        if (Windows is null)
            return capacity;
        foreach (var w in Windows)
            if (day >= w.FromDay && day <= w.ToDay)
                capacity *= w.Factor;
        return capacity;
    }
}

public sealed record PlanInput(
    DateOnly Start, decimal HoursPerDay, IReadOnlyList<PlanActivity> Activities, IReadOnlyList<PlanResource> People);

public sealed record ScheduledActivity(
    int Id, string Name, int StartDay, int FinishDay, DateOnly Start, DateOnly Finish, int? AssigneeId,
    decimal Hours, bool IsCritical, decimal SlackDays);

public sealed record ResourceLoad(int PersonId, string Name, decimal AssignedHours, decimal CapacityHours, decimal UtilizationPercent);

public sealed record PlanResult(
    IReadOnlyList<ScheduledActivity> Activities,
    DateOnly Start,
    DateOnly Finish,
    int DurationWorkdays,
    DateOnly UnconstrainedFinish,
    decimal TotalHours,
    decimal PlannedCost,
    IReadOnlyList<ResourceLoad> Loads,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Kaynak kısıtlı seri çizelgeleme (Serial Schedule Generation Scheme):
/// öncülleri planlanmış işler arasından en küçük CPM geç başlangıcı (LS) olan seçilir (eşitlikte yüksek öncelik, sonra id);
/// gereken beceriye sahip kişiler arasından işi en erken bitirecek olana günlük kapasitesi aşılmadan yerleştirilir.
/// Varsayımlar: gün hassasiyeti; bağımlı iş, öncülü bittikten sonraki iş günü başlar; bir kişi aynı gün birden fazla
/// bağımsız işe kalan kapasitesi kadar çalışabilir; uygun kişi yoksa iş, günde HoursPerDay saatle kişisiz planlanır.
/// </summary>
public static class ResourceScheduler
{
    private const decimal Epsilon = 0.0001m;

    public static PlanResult Schedule(PlanInput input)
    {
        var calendar = new WorkCalendar(input.Start);
        var warnings = new List<string>();
        var activities = input.Activities.ToDictionary(a => a.Id);

        var (cpm, cpmDurationHours) = CriticalPath.Compute(
            input.Activities.Select(a => new CpmActivity(a.Id, a.Hours, a.Predecessors)).ToList());

        var used = input.People.ToDictionary(p => p.Id, _ => new Dictionary<int, decimal>());
        var assignedHours = input.People.ToDictionary(p => p.Id, _ => 0m);
        var scheduled = new Dictionary<int, (int Start, int Finish, int? Person)>();
        var missingSkills = new HashSet<Skill>();

        while (scheduled.Count < activities.Count)
        {
            var next = activities.Values
                .Where(a => !scheduled.ContainsKey(a.Id) && a.Predecessors.All(scheduled.ContainsKey))
                .OrderBy(a => cpm[a.Id].LateStart)
                .ThenByDescending(a => a.Priority)
                .ThenBy(a => a.Id)
                .First();

            var earliest = next.Predecessors.Select(p => ReadyDay(scheduled[p], activities[p].Hours)).DefaultIfEmpty(0).Max();

            var candidates = Candidates(next, input.People, warnings);
            if (candidates.Count == 0)
            {
                missingSkills.Add(next.Skill);
                var days = next.Hours <= Epsilon ? 1 : (int)Math.Ceiling(next.Hours / input.HoursPerDay);
                scheduled[next.Id] = (earliest, earliest + days - 1, null);
                continue;
            }

            var best = candidates
                .Select(person => (Person: person, Slot: Simulate(next.Hours, earliest, person, used[person.Id])))
                .OrderBy(c => c.Slot.Finish)
                .ThenBy(c => assignedHours[c.Person.Id])
                .ThenBy(c => c.Person.Id)
                .First();

            Allocate(next.Hours, best.Slot.Start, best.Person, used[best.Person.Id]);
            assignedHours[best.Person.Id] += next.Hours;
            scheduled[next.Id] = (best.Slot.Start, best.Slot.Finish, best.Person.Id);
        }

        foreach (var skill in missingSkills)
            warnings.Add($"'{Format(skill)}' becerisine sahip kişi yok; bu işler kişisiz planlandı (kişi ekleyin).");

        var finishDay = scheduled.Values.Select(s => s.Finish).DefaultIfEmpty(0).Max();
        var durationWorkdays = activities.Count == 0 ? 0 : finishDay + 1;
        var unconstrainedDays = (int)Math.Ceiling(cpmDurationHours / input.HoursPerDay);

        var results = scheduled
            .OrderBy(s => s.Value.Start).ThenBy(s => s.Key)
            .Select(s => new ScheduledActivity(
                s.Key, activities[s.Key].Name, s.Value.Start, s.Value.Finish,
                calendar.ToDate(s.Value.Start), calendar.ToDate(s.Value.Finish), s.Value.Person,
                activities[s.Key].Hours, cpm[s.Key].IsCritical, Math.Round(cpm[s.Key].Slack / input.HoursPerDay, 1)))
            .ToList();

        var loads = input.People
            .Select(p =>
            {
                var capacity = Enumerable.Range(0, durationWorkdays).Sum(p.CapacityOn);
                var utilization = capacity <= 0 ? 0 : Math.Round(assignedHours[p.Id] / capacity * 100, 1);
                return new ResourceLoad(p.Id, p.Name, assignedHours[p.Id], capacity, utilization);
            })
            .ToList();

        var cost = input.People.Sum(p => assignedHours[p.Id] * p.HourlyCost);

        return new PlanResult(
            results, calendar.FirstDay, calendar.ToDate(Math.Max(finishDay, 0)), durationWorkdays,
            calendar.ToDate(Math.Max(unconstrainedDays - 1, 0)), input.Activities.Sum(a => a.Hours), cost, loads, warnings);
    }

    /// <summary>Ardılın en erken başlayabileceği gün: süreli işte bitişten sonraki gün, süresiz işte aynı gün.</summary>
    private static int ReadyDay((int Start, int Finish, int? Person) slot, decimal hours) =>
        hours <= Epsilon ? slot.Finish : slot.Finish + 1;

    private static List<PlanResource> Candidates(PlanActivity activity, IReadOnlyList<PlanResource> people, List<string> warnings)
    {
        if (activity.FixedAssigneeId is { } fixedId && people.FirstOrDefault(p => p.Id == fixedId) is { } fixedPerson)
        {
            if (!fixedPerson.Skills.HasFlag(activity.Skill))
                warnings.Add($"'{activity.Name}' işine atanan {fixedPerson.Name}, '{Format(activity.Skill)}' becerisine sahip değil.");
            return fixedPerson.DailyCapacity > 0 ? [fixedPerson] : [];
        }

        return people.Where(p => p.Skills.HasFlag(activity.Skill) && p.DailyCapacity > 0).ToList();
    }

    private static (int Start, int Finish) Simulate(decimal hours, int earliest, PlanResource person, Dictionary<int, decimal> used)
    {
        if (hours <= Epsilon)
            return (earliest, earliest);

        var remaining = hours;
        int? start = null;
        var day = earliest;
        while (true)
        {
            var free = person.CapacityOn(day) - used.GetValueOrDefault(day);
            if (free > Epsilon)
            {
                start ??= day;
                remaining -= Math.Min(free, remaining);
                if (remaining <= Epsilon)
                    return (start.Value, day);
            }
            day++;
        }
    }

    private static void Allocate(decimal hours, int start, PlanResource person, Dictionary<int, decimal> used)
    {
        var remaining = hours;
        for (var day = start; remaining > Epsilon; day++)
        {
            var take = Math.Min(person.CapacityOn(day) - used.GetValueOrDefault(day), remaining);
            if (take <= Epsilon)
                continue;
            used[day] = used.GetValueOrDefault(day) + take;
            remaining -= take;
        }
    }

    private static string Format(Skill skill) => Common.Format.Name(skill);
}
