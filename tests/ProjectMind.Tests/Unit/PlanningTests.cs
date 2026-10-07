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
        new(id, $"İş {id}", hours, skill, Priority.Medium, null, preds);

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
        Assert.NotEqual(r.Activities[0].AssigneeId, r.Activities[1].AssigneeId);
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
        Assert.Null(r.Activities.Single().AssigneeId);
        Assert.Equal((0, 1), Slot(r, 1));
        Assert.Contains(r.Warnings, w => w.Contains("Test"));
    }

    [Fact]
    public void Fixed_assignee_is_respected()
    {
        var tasks = new[] { new PlanActivity(1, "İş", 8, Skill.Backend, Priority.Medium, 2, []) };
        var r = Run(tasks, Person(1, Skill.Backend), Person(2, Skill.Backend));
        Assert.Equal(2, r.Activities.Single().AssigneeId);
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
