using ProjectMind.Application.Common;
using ProjectMind.Application.Planning;
using ProjectMind.Application.WorkItems;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.WhatIf;

/// <summary>
/// Senaryo değişikliklerini plan girdisine uygular (veri değişmez). Yeni kişiler için Brooks etkisi:
/// katılım gününe kadar kapasite 0, ısınma süresince düşük verim, aynı sürede mevcut ekipte mentorluk yükü.
/// Çıkarılan işin öncülleri ardıllarına aktarılır (bağımlılık zinciri kopmaz).
/// </summary>
public static class ScenarioApplier
{
    public sealed record Applied(PlanInput Input, DateOnly TargetDate, IReadOnlyList<string> Notes);

    /// <summary>Bir haftadaki saat sayısı: haftalık kapasitenin üst sınırı (kişi formundaki sınırla aynı).</summary>
    public const decimal MaxWeeklyHours = 7 * 24;

    public static Applied Apply(PlanInput input, IReadOnlyDictionary<int, int?> parentOf, DateOnly targetDate,
        IReadOnlyList<ScenarioChange> changes, WhatIfOptions o)
    {
        var calendar = new WorkCalendar(input.Start);
        var people = input.People.ToList();
        var activities = input.Activities.ToList();
        var notes = new List<string>();
        var newcomers = new List<(PlanResource Person, int JoinDay)>();
        var nextId = -1;

        foreach (var c in changes)
        {
            switch (c.Kind)
            {
                case ScenarioChangeKind.AddPerson:
                {
                    if (c.Count < 1 || c.Count > o.MaxAddedPeople)
                        throw new BusinessRuleException($"Eklenecek kişi sayısı 1 ile {o.MaxAddedPeople} arasında olmalı.");
                    var skills = (c.Skills ?? []).Aggregate(Skill.None, (a, s) => a | s);
                    if (skills == Skill.None)
                        throw new BusinessRuleException("Eklenecek kişinin en az bir becerisi olmalı.");
                    var weekly = c.WeeklyHours ?? o.DefaultWeeklyHours;
                    ValidateWeeklyHours(weekly);
                    if (c.HourlyCost is < 0)
                        throw new BusinessRuleException("Saatlik maliyet negatif olamaz.");
                    var cost = c.HourlyCost ?? (people.Count > 0 ? Math.Round(people.Average(p => p.HourlyCost), 2) : 0);
                    var joinDay = c.Date is { } d && d > calendar.FirstDay ? WorkCalendar.WorkdaysBetween(calendar.FirstDay, d) : 0;
                    for (var i = 0; i < c.Count; i++)
                    {
                        var name = (c.Name ?? "Yeni kişi") + (c.Count > 1 ? $" {i + 1}" : "");
                        var person = new PlanResource(nextId--, name, skills, weekly, cost);
                        newcomers.Add((person, joinDay));
                        people.Add(person);
                    }
                    notes.Add($"+{c.Count} kişi ({Format.Flags(skills)}, haftalık {Format.Number(weekly)} saat), " +
                              $"katılım {Format.Date(calendar.ToDate(joinDay))}");
                    break;
                }
                case ScenarioChangeKind.RemovePerson:
                {
                    var person = FindPerson(people, c.PersonId);
                    people.Remove(person);
                    activities = activities.Select(a => a.FixedAssigneeId == person.Id ? a with { FixedAssigneeId = null } : a).ToList();
                    notes.Add($"{person.Name} ekipten çıkarıldı; işleri yeniden dağıtılır");
                    break;
                }
                case ScenarioChangeKind.ChangeCapacity:
                {
                    var person = FindPerson(people, c.PersonId);
                    if (c.WeeklyHours is not { } hours)
                        throw new BusinessRuleException("Haftalık kapasite 0'dan büyük olmalı.");
                    ValidateWeeklyHours(hours);
                    people[people.IndexOf(person)] = person with { WeeklyCapacityHours = hours };
                    notes.Add($"{person.Name}: haftalık {Format.Number(person.WeeklyCapacityHours)} → {Format.Number(hours)} saat");
                    break;
                }
                case ScenarioChangeKind.RemoveWorkItem:
                {
                    if (c.WorkItemId is not { } id || !parentOf.ContainsKey(id))
                        throw new BusinessRuleException("Çıkarılacak iş bulunamadı.");
                    var removed = WorkItemTree.SubtreeIds(parentOf, id);
                    var before = activities.Sum(a => a.Hours);
                    activities = RemoveActivities(activities, removed);
                    notes.Add($"İş çıkarıldı (alt işleriyle): kalan efor {Format.Number(before - activities.Sum(a => a.Hours))} saat azaldı");
                    break;
                }
                case ScenarioChangeKind.ChangeDeadline:
                {
                    if (c.Date is not { } date)
                        throw new BusinessRuleException("Yeni hedef tarih verilmeli.");
                    notes.Add($"Hedef tarih {Format.Date(targetDate)} → {Format.Date(date)}");
                    targetDate = date;
                    break;
                }
                default:
                    throw new BusinessRuleException("Bilinmeyen senaryo değişikliği.");
            }
        }

        people = ApplyBrooks(people, newcomers, o);
        return new Applied(input with { People = people, Activities = activities }, targetDate, notes);
    }

    /// <summary>Kapasite pencereleri: yeni kişi katılana kadar 0, ısınmada düşük verim; mevcut ekipte mentorluk yükü.</summary>
    public static List<PlanResource> ApplyBrooks(List<PlanResource> people, IReadOnlyList<(PlanResource Person, int JoinDay)> newcomers,
        WhatIfOptions o)
    {
        if (newcomers.Count == 0)
            return people;

        var rampDays = o.RampUpWeeks * WorkCalendar.WorkDaysPerWeek;
        var newIds = newcomers.Select(n => n.Person.Id).ToHashSet();
        var existing = people.Where(p => !newIds.Contains(p.Id)).ToList();
        var existingDaily = existing.Sum(p => p.DailyCapacity);
        var windows = people.ToDictionary(p => p.Id, p => new List<CapacityWindow>(p.Windows ?? []));

        foreach (var (person, join) in newcomers)
        {
            if (join > 0)
                windows[person.Id].Add(new CapacityWindow(0, join - 1, 0));
            if (rampDays <= 0)
                continue;
            windows[person.Id].Add(new CapacityWindow(join, join + rampDays - 1, o.RampUpProductivity));
            if (existingDaily > 0)
            {
                var factor = Math.Max(o.MinRemainingCapacity, 1 - o.MentoringShare * person.DailyCapacity / existingDaily);
                foreach (var e in existing)
                    windows[e.Id].Add(new CapacityWindow(join, join + rampDays - 1, factor));
            }
        }

        return people.Select(p => p with { Windows = windows[p.Id] }).ToList();
    }

    /// <summary>İşleri çıkarır; çıkarılan işin öncülleri ardıllarına aktarılır (A→B→C'den B çıkınca A→C).</summary>
    public static List<PlanActivity> RemoveActivities(IReadOnlyList<PlanActivity> activities, IReadOnlySet<int> removed)
    {
        var byId = activities.ToDictionary(a => a.Id);

        IEnumerable<int> Expand(int id, HashSet<int> seen)
        {
            if (!removed.Contains(id))
                return [id];
            if (!seen.Add(id) || !byId.TryGetValue(id, out var a))
                return [];
            return a.Predecessors.SelectMany(p => Expand(p, seen));
        }

        return activities
            .Where(a => !removed.Contains(a.Id))
            .Select(a => a with { Predecessors = a.Predecessors.SelectMany(p => Expand(p, [])).Distinct().ToList() })
            .ToList();
    }

    private static void ValidateWeeklyHours(decimal hours)
    {
        if (hours <= 0 || hours > MaxWeeklyHours)
            throw new BusinessRuleException($"Haftalık kapasite 0'dan büyük ve en fazla {MaxWeeklyHours} saat olmalı.");
    }

    private static PlanResource FindPerson(List<PlanResource> people, int? id) =>
        people.FirstOrDefault(p => p.Id == id) ?? throw new BusinessRuleException("Kişi bulunamadı.");
}
