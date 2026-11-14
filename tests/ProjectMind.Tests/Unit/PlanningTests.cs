using ProjectMind.Application.Common;
using ProjectMind.Application.Planning;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Tests.Unit;

public class CriticalPathTests
{
    // Ders kitabı örneği (saat; 1 gün = 8 saat):
    // A(24) → B(16) → D(8)
    // A(24) → C(32) → D(8)
    // ES/EF: A 0-24, B 24-40, C 24-56, D 56-64 → proje 64 saat
    // Bolluk: B = 56-40 = 16, diğerleri 0 → kritik yol A-C-D
    private static readonly CpmActivity[] Example =
    [
        new(1, 24, []), new(2, 16, [1]), new(3, 32, [1]), new(4, 8, [2, 3])
    ];

    [Fact]
    public void Forward_and_backward_pass_match_hand_calculation()
    {
        var (r, duration) = CriticalPath.Compute(Example);

        Assert.Equal(64, duration);
        Assert.Equal((0m, 24m), (r[1].EarlyStart, r[1].EarlyFinish));
        Assert.Equal((24m, 40m), (r[2].EarlyStart, r[2].EarlyFinish));
        Assert.Equal((40m, 56m), (r[2].LateStart, r[2].LateFinish));
        Assert.Equal((24m, 56m), (r[3].EarlyStart, r[3].EarlyFinish));
        Assert.Equal((56m, 64m), (r[4].EarlyStart, r[4].EarlyFinish));
        Assert.Equal(16, r[2].Slack);
        Assert.Equal([1, 3, 4], r.Values.Where(x => x.IsCritical).Select(x => x.Id).Order());
    }

    [Fact]
    public void Cycle_is_reported() =>
        Assert.Throws<BusinessRuleException>(() => CriticalPath.Compute([new(1, 8, [2]), new(2, 8, [1])]));
}

public class ResourceSchedulerTests
{
    private static readonly DateOnly Monday = new(2026, 11, 2);

    private static PlanResource Person(int id, Skill skills, decimal weekly = 40, decimal cost = 100) =>
        new(id, $"Kişi {id}", skills, weekly, cost);

    private static PlanActivity Task(int id, decimal hours, Skill skill = Skill.Backend, params int[] preds) =>
        new(id, $"İş {id}", hours, skill, Priority.Medium, [], preds);

    private static PlanResult Run(IReadOnlyList<PlanActivity> tasks, params PlanResource[] people) =>
        ResourceScheduler.Schedule(new PlanInput(Monday, 8, tasks, people));

    private static (int Start, int Finish) Slot(PlanResult r, int id) =>
        r.Activities.Single(a => a.Id == id) is var a ? (a.StartDay, a.FinishDay) : default;

    [Fact]
    public void One_person_cannot_do_two_full_days_of_work_in_one_day()
    {
        var r = Run([Task(1, 8), Task(2, 8)], Person(1, Skill.Backend));
        Assert.Equal((0, 0), Slot(r, 1));
        Assert.Equal((1, 1), Slot(r, 2));
        Assert.Equal(2, r.DurationWorkdays);
    }

    [Fact]
    public void Two_people_work_in_parallel()
    {
        var r = Run([Task(1, 8), Task(2, 8)], Person(1, Skill.Backend), Person(2, Skill.Backend));
        Assert.Equal((0, 0), Slot(r, 1));
        Assert.Equal((0, 0), Slot(r, 2));
        Assert.NotEqual(r.Activities[0].AssigneeIds.Single(), r.Activities[1].AssigneeIds.Single());
    }

    [Fact]
    public void Successor_starts_after_predecessor_and_part_time_capacity_stretches_work()
    {
        // 20 saat/hafta = 4 saat/gün: 8 saatlik iş 2 gün sürer; ardılı 3. gün başlar.
        var r = Run([Task(1, 8), Task(2, 4, Skill.Backend, 1)], Person(1, Skill.Backend, weekly: 20));
        Assert.Equal((0, 1), Slot(r, 1));
        Assert.Equal((2, 2), Slot(r, 2));
    }

    [Fact]
    public void Critical_work_is_scheduled_before_work_with_slack()
    {
        // X(8, bolluk var), Y(8) → Z(16): tek kişi. Önce kritik Y, sonra Z, en son X.
        var r = Run([Task(1, 8), Task(2, 8), Task(3, 16, Skill.Backend, 2)], Person(1, Skill.Backend));
        Assert.Equal((0, 0), Slot(r, 2));
        Assert.Equal((1, 2), Slot(r, 3));
        Assert.Equal((3, 3), Slot(r, 1));
        Assert.False(r.Activities.Single(a => a.Id == 1).IsCritical);
        Assert.True(r.Activities.Single(a => a.Id == 3).IsCritical);
        Assert.Equal(new DateOnly(2026, 11, 4), r.UnconstrainedFinish);   // kaynak sınırsız: 24 saat = 3 gün (Pzt-Çar)
        Assert.Equal(new DateOnly(2026, 11, 5), r.Finish);               // tek kişiyle 4 gün
    }

    [Fact]
    public void Missing_skill_is_planned_without_person_and_warned()
    {
        var r = Run([Task(1, 16, Skill.Test)], Person(1, Skill.Backend));
        Assert.Empty(r.Activities.Single().AssigneeIds);
        Assert.Equal((0, 1), Slot(r, 1));
        Assert.Contains(r.Warnings, w => w.Contains("Test"));
    }

    [Fact]
    public void Fixed_assignee_is_respected()
    {
        var tasks = new[] { new PlanActivity(1, "İş", 8, Skill.Backend, Priority.Medium, [2], []) };
        var r = Run(tasks, Person(1, Skill.Backend), Person(2, Skill.Backend));
        Assert.Equal([2], r.Activities.Single().AssigneeIds);
    }

    [Fact]
    public void Fixed_assignee_with_zero_capacity_gets_its_own_warning()
    {
        // İş, kapasitesi 0 olan Kişi 2'ye sabit atanmış; Kişi 1 aynı beceriye sahip ama sabit atama korunur.
        var tasks = new[] { new PlanActivity(1, "Ödeme", 16, Skill.Backend, Priority.Medium, [2], []) };
        var r = Run(tasks, Person(1, Skill.Backend), Person(2, Skill.Backend, weekly: 0));

        Assert.Empty(r.Activities.Single().AssigneeIds);
        Assert.Equal((0, 1), Slot(r, 1));
        Assert.Contains(r.Warnings, w => w.Contains("'Ödeme' işine atanan Kişi 2") && w.Contains("kapasitesi 0"));
        Assert.DoesNotContain(r.Warnings, w => w.Contains("becerisine sahip kişi yok"));
    }

    [Fact]
    public void Cost_and_utilization_are_computed()
    {
        var r = Run([Task(1, 16)], Person(1, Skill.Backend, weekly: 40, cost: 100));
        Assert.Equal(1600, r.PlannedCost);
        var load = r.Loads.Single();
        Assert.Equal(16, load.CapacityHours);
        Assert.Equal(100, load.UtilizationPercent);
    }

    private static PlanActivity TeamTask(int id, decimal hours, int[] assignees, Skill skill = Skill.Backend, params int[] preds) =>
        new(id, $"İş {id}", hours, skill, Priority.Medium, assignees, preds);

    private static Dictionary<int, decimal> HoursByPerson(PlanResult r, int id) =>
        r.Activities.Single(a => a.Id == id).Allocations.ToDictionary(x => x.PersonId, x => x.Hours);

    [Fact]
    public void Team_members_work_on_the_same_task_together()
    {
        // 32 saat, iki kişi günde 8'er saat: gün 0'da 16, gün 1'de 16 → 2 gün; kişi başı 16 saat.
        var r = Run([TeamTask(1, 32, [1, 2])], Person(1, Skill.Backend, cost: 100), Person(2, Skill.Backend, cost: 200));

        Assert.Equal((0, 1), Slot(r, 1));
        Assert.Equal([1, 2], r.Activities.Single().AssigneeIds);
        Assert.Equal(new Dictionary<int, decimal> { [1] = 16, [2] = 16 }, HoursByPerson(r, 1));
        Assert.Equal(16 * 100 + 16 * 200, r.PlannedCost);
        Assert.All(r.Loads, l => Assert.Equal(16, l.AssignedHours));
        Assert.Equal(2, r.DurationWorkdays);
    }

    [Fact]
    public void Team_hours_follow_each_members_capacity()
    {
        // Günde 8 + 4 = 12 saat: 24 saatlik iş 2 gün; Kişi 1 16, Kişi 2 8 saat.
        var r = Run([TeamTask(1, 24, [1, 2])], Person(1, Skill.Backend, weekly: 40), Person(2, Skill.Backend, weekly: 20));

        Assert.Equal((0, 1), Slot(r, 1));
        Assert.Equal(new Dictionary<int, decimal> { [1] = 16, [2] = 8 }, HoursByPerson(r, 1));
    }

    [Fact]
    public void Busy_team_member_joins_when_free()
    {
        // İş 1 (8 s, Kişi 1) → İş 3 (8 s, Kişi 1) kritik zincir; İş 2 (12 s, Kişi 1 + 2) bolluklu.
        // Sıra: İş 1 gün 0 (Kişi 1 dolu) → İş 2: gün 0 yalnız Kişi 2 (8 s), gün 1 Kişi 1 kalan 4 s → bitiş gün 1.
        // İş 3: gün 1'de Kişi 1'in kalan 4 saati + gün 2'de 4 saat.
        var r = Run([TeamTask(1, 8, [1]), TeamTask(2, 12, [1, 2]), TeamTask(3, 8, [1], Skill.Backend, 1)],
            Person(1, Skill.Backend), Person(2, Skill.Backend));

        Assert.Equal((0, 0), Slot(r, 1));
        Assert.Equal((0, 1), Slot(r, 2));
        Assert.Equal(new Dictionary<int, decimal> { [1] = 4, [2] = 8 }, HoursByPerson(r, 2));
        Assert.Equal((1, 2), Slot(r, 3));
    }

    [Fact]
    public void Team_member_without_skill_works_with_warning_and_zero_capacity_member_is_left_out()
    {
        var r = Run([TeamTask(1, 16, [1, 2])],
            Person(1, Skill.Frontend), Person(2, Skill.Backend, weekly: 0), Person(3, Skill.Backend));

        Assert.Equal([1], r.Activities.Single().AssigneeIds);   // Kişi 3 atanmamış; sabit ekip korunur
        Assert.Equal((0, 1), Slot(r, 1));
        Assert.Contains(r.Warnings, w => w.Contains("Kişi 1, 'Backend' becerisine sahip değil"));
        Assert.Contains(r.Warnings, w => w.Contains("Kişi 2 kişisinin kapasitesi 0; iş onsuz planlandı"));
        Assert.DoesNotContain(r.Warnings, w => w.Contains("becerisine sahip kişi yok"));
    }

    [Fact]
    public void Team_with_no_capacity_is_planned_without_person()
    {
        var r = Run([TeamTask(1, 16, [1, 2])], Person(1, Skill.Backend, weekly: 0), Person(2, Skill.Backend, weekly: 0));

        Assert.Empty(r.Activities.Single().AssigneeIds);
        Assert.Equal((0, 1), Slot(r, 1));
        Assert.Equal(2, r.Warnings.Count(w => w.Contains("kapasitesi 0; iş kişisiz planlandı")));
    }

    [Fact]
    public void Automatic_assignment_still_picks_one_person_with_all_hours()
    {
        var r = Run([Task(1, 16)], Person(1, Skill.Backend), Person(2, Skill.Backend));

        var a = r.Activities.Single();
        Assert.Single(a.AssigneeIds);
        Assert.Equal(16, a.Allocations.Single().Hours);
        Assert.Equal((0, 1), Slot(r, 1));
    }

    [Fact]
    public void Weekends_are_skipped()
    {
        var calendar = new WorkCalendar(new DateOnly(2026, 11, 7)); // Cumartesi
        Assert.Equal(new DateOnly(2026, 11, 9), calendar.FirstDay); // Pazartesi
        Assert.Equal(new DateOnly(2026, 11, 16), calendar.ToDate(5));
        Assert.Equal(5, WorkCalendar.WorkdaysBetween(new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 16)));
        Assert.Equal(-5, WorkCalendar.WorkdaysBetween(new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 9)));
    }
}
