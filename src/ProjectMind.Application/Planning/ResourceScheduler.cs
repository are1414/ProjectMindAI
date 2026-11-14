using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.Planning;

/// <param name="FixedAssigneeIds">Sabit atanan kişiler (birlikte çalışırlar, D32); boşsa çizelgeleyici bir kişi seçer.</param>
public sealed record PlanActivity(
    int Id, string Name, decimal Hours, Skill Skill, Priority Priority, IReadOnlyList<int> FixedAssigneeIds,
    IReadOnlyList<int> Predecessors);

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

/// <summary>Bir kişinin bir işe ayrılan saati.</summary>
public sealed record ActivityAllocation(int PersonId, decimal Hours);

/// <param name="AssigneeIds">İşi yapan kişiler (kişi id sırasıyla); boşsa iş kişisiz planlandı.</param>
/// <param name="Allocations">Kişi başına ayrılan saat (doluluk ve maliyet bundan hesaplanır).</param>
public sealed record ScheduledActivity(
    int Id, string Name, int StartDay, int FinishDay, DateOnly Start, DateOnly Finish, IReadOnlyList<int> AssigneeIds,
    decimal Hours, bool IsCritical, decimal SlackDays, IReadOnlyList<ActivityAllocation> Allocations);

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
/// İşe sabit atanmış kişiler varsa iş yalnız onlara verilir; birden çok kişi işi birlikte yapar (D32): her gün kişi id
/// sırasıyla o günkü boş kapasitesi kadar iş alır, iş kalan efor bitince biter.
/// Varsayımlar: gün hassasiyeti; bağımlı iş, öncülü bittikten sonraki iş günü başlar; bir kişi aynı gün birden fazla
/// bağımsız işe kalan kapasitesi kadar çalışabilir; uygun kişi yoksa iş, günde HoursPerDay saatle kişisiz planlanır;
/// aynı iş içindeki iletişim yükü (Brooks) modellenmez.
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
        var scheduled = new Dictionary<int, (int Start, int Finish, IReadOnlyList<ActivityAllocation> Allocations)>();
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

            var (team, candidates, fixedUnavailable) = Candidates(next, input.People, warnings);
            if (candidates.Count == 0)
            {
                if (!fixedUnavailable)
                    missingSkills.Add(next.Skill);
                var days = next.Hours <= Epsilon ? 1 : (int)Math.Ceiling(next.Hours / input.HoursPerDay);
                scheduled[next.Id] = (earliest, earliest + days - 1, []);
                continue;
            }

            // Sabit ekip işi birlikte yapar; otomatik atamada işi en erken bitirecek tek kişi seçilir.
            List<PlanResource> crew = team
                ? candidates
                : [candidates
                    .Select(person => (Person: person, Slot: Simulate(next.Hours, earliest, [person], used)))
                    .OrderBy(c => c.Slot.Finish)
                    .ThenBy(c => assignedHours[c.Person.Id])
                    .ThenBy(c => c.Person.Id)
                    .First().Person];

            var slot = Simulate(next.Hours, earliest, crew, used);
            var allocations = Allocate(next.Hours, slot.Start, crew, used);
            foreach (var a in allocations)
                assignedHours[a.PersonId] += a.Hours;
            scheduled[next.Id] = (slot.Start, slot.Finish, allocations);
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
                calendar.ToDate(s.Value.Start), calendar.ToDate(s.Value.Finish),
                s.Value.Allocations.Select(a => a.PersonId).ToList(),
                activities[s.Key].Hours, cpm[s.Key].IsCritical, Math.Round(cpm[s.Key].Slack / input.HoursPerDay, 1),
                s.Value.Allocations))
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
    private static int ReadyDay((int Start, int Finish, IReadOnlyList<ActivityAllocation> Allocations) slot, decimal hours) =>
        hours <= Epsilon ? slot.Finish : slot.Finish + 1;

    /// <summary>
    /// Aday kişiler. Sabit atamada (Team = true) yalnız atananlar: becerisi uymayan için uyarı verilir ama ekipte kalır;
    /// kapasitesi 0 olan uyarıyla dışarıda kalır. Hiçbiri çalışamıyorsa iş kişisiz planlanır ve bunun nedeni ayrı uyarıyla
    /// bildirilir (FixedUnavailable = true; "beceriye sahip kişi yok" uyarısı verilmez). Girdide olmayan (ör. senaryoda
    /// çıkarılmış) kişi yok sayılır; sabit atananların hiçbiri girdide yoksa otomatik atamaya dönülür.
    /// </summary>
    private static (bool Team, List<PlanResource> People, bool FixedUnavailable) Candidates(
        PlanActivity activity, IReadOnlyList<PlanResource> people, List<string> warnings)
    {
        var assigned = people.Where(p => activity.FixedAssigneeIds.Contains(p.Id)).OrderBy(p => p.Id).ToList();
        if (assigned.Count == 0)
            return (false, people.Where(p => p.Skills.HasFlag(activity.Skill) && p.DailyCapacity > 0).ToList(), false);

        foreach (var person in assigned.Where(p => !p.Skills.HasFlag(activity.Skill)))
            warnings.Add($"'{activity.Name}' işine atanan {person.Name}, '{Format(activity.Skill)}' becerisine sahip değil.");

        var team = assigned.Where(p => p.DailyCapacity > 0).ToList();
        var idle = assigned.Where(p => p.DailyCapacity <= 0).ToList();
        foreach (var person in idle)
            warnings.Add(team.Count == 0
                ? $"'{activity.Name}' işine atanan {person.Name} kişisinin kapasitesi 0; iş kişisiz planlandı " +
                  "(kapasite verin veya atamayı kaldırın)."
                : $"'{activity.Name}' işine atanan {person.Name} kişisinin kapasitesi 0; iş onsuz planlandı.");
        return (true, team, team.Count == 0);
    }

    /// <summary>
    /// Ekibin (bir veya daha çok kişi) işi nerede başlayıp bitireceği: her gün kişiler id sırasıyla o günkü boş
    /// kapasitesi kadar iş alır. Veri değişmez.
    /// </summary>
    private static (int Start, int Finish) Simulate(
        decimal hours, int earliest, IReadOnlyList<PlanResource> crew, Dictionary<int, Dictionary<int, decimal>> used)
    {
        if (hours <= Epsilon)
            return (earliest, earliest);

        var remaining = hours;
        int? start = null;
        for (var day = earliest; ; day++)
        {
            foreach (var person in crew)
            {
                var free = person.CapacityOn(day) - used[person.Id].GetValueOrDefault(day);
                if (free <= Epsilon)
                    continue;
                start ??= day;
                remaining -= Math.Min(free, remaining);
                if (remaining <= Epsilon)
                    return (start.Value, day);
            }
        }
    }

    /// <summary><see cref="Simulate"/> ile aynı yerleştirmeyi kapasiteye yazar; kişi başına ayrılan saati döndürür.</summary>
    private static List<ActivityAllocation> Allocate(
        decimal hours, int start, IReadOnlyList<PlanResource> crew, Dictionary<int, Dictionary<int, decimal>> used)
    {
        var taken = crew.ToDictionary(p => p.Id, _ => 0m);
        var remaining = hours;
        for (var day = start; remaining > Epsilon; day++)
        {
            foreach (var person in crew)
            {
                var take = Math.Min(person.CapacityOn(day) - used[person.Id].GetValueOrDefault(day), remaining);
                if (take <= Epsilon)
                    continue;
                used[person.Id][day] = used[person.Id].GetValueOrDefault(day) + take;
                taken[person.Id] += take;
                remaining -= take;
                if (remaining <= Epsilon)
                    break;
            }
        }

        // Ondalık kapasitede (ör. mentorluk çarpanı) ε'dan küçük kalan, son çalışana yazılır: kişi saatlerinin toplamı = efor.
        if (remaining > 0 && crew.LastOrDefault(p => taken[p.Id] > 0) is { } last)
            taken[last.Id] += remaining;

        // Süresiz iş (0 saat) de atananlarıyla görünür; ayrılan saati 0'dır.
        return crew.Where(p => taken[p.Id] > 0 || hours <= Epsilon)
            .Select(p => new ActivityAllocation(p.Id, taken[p.Id])).ToList();
    }

    private static string Format(Skill skill) => Common.Format.Name(skill);
}
