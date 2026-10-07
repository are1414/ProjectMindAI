using ProjectMind.Application.Common;
using ProjectMind.Application.Planning;
using ProjectMind.Application.WhatIf;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Tests.Unit;

public class WhatIfTests
{
    // 2 Kasım 2026 Pazartesi.
    private static readonly DateOnly Monday = new(2026, 11, 2);

    private static PlanActivity Task(int id, decimal hours, params int[] predecessors) =>
        new(id, $"İş {id}", hours, Skill.Backend, Priority.Medium, null, predecessors);

    /// <summary>İki bağımsız 80 saatlik backend işi, tek backendci (8 s/gün).</summary>
    private static PlanInput TwoTasksOnePerson() =>
        new(Monday, 8, [Task(1, 80), Task(2, 80)], [new PlanResource(1, "Ayşe", Skill.Backend, 40, 100)]);

    private static readonly Dictionary<int, int?> NoParents = new() { [1] = null, [2] = null, [3] = null };

    private static ScenarioChange AddBackend(DateOnly? join = null) =>
        new(ScenarioChangeKind.AddPerson, Name: "Yeni", Skills: [Skill.Backend], WeeklyHours: 40, Date: join);

    [Fact]
    public void Capacity_windows_multiply()
    {
        var p = new PlanResource(1, "x", Skill.Backend, 40, 0, [new(0, 4, 0), new(5, 9, 0.5m), new(5, 6, 0.5m)]);
        Assert.Equal(0, p.CapacityOn(0));
        Assert.Equal(2, p.CapacityOn(5));    // 8 × 0,5 × 0,5
        Assert.Equal(4, p.CapacityOn(7));
        Assert.Equal(8, p.CapacityOn(10));
    }

    [Fact]
    public void Brooks_effect_new_person_does_not_help_in_short_term()
    {
        var o = new WhatIfOptions();   // ısınma 4 hafta, verim %50, mentorluk %25
        var current = ResourceScheduler.Schedule(TwoTasksOnePerson());
        Assert.Equal(20, current.DurationWorkdays);   // 160 saat / 8

        // Yeni kişi ilk gün katılır: Ayşe mentorluk yüzünden 6 s/gün → iş 1: 80/6 → gün 0–13.
        // Yeni kişi 4 s/gün → iş 2: 80/4 → gün 0–19. Bitiş yine 20. iş günü (Brooks yasası).
        var withBrooks = ResourceScheduler.Schedule(
            ScenarioApplier.Apply(TwoTasksOnePerson(), NoParents, Monday, [AddBackend()], o).Input);
        Assert.Equal(20, withBrooks.DurationWorkdays);

        // Isınma ve mentorluk olmasaydı iki iş paralel: 10 gün.
        var noRamp = new WhatIfOptions { RampUpWeeks = 0 };
        var ideal = ResourceScheduler.Schedule(ScenarioApplier.Apply(TwoTasksOnePerson(), NoParents, Monday, [AddBackend()], noRamp).Input);
        Assert.Equal(10, ideal.DurationWorkdays);
    }

    [Fact]
    public void Late_joiner_has_zero_capacity_before_joining()
    {
        // 9 Kasım (5. iş günü) katılan kişi: o güne kadar kapasitesi 0.
        var applied = ScenarioApplier.Apply(TwoTasksOnePerson(), NoParents, Monday, [AddBackend(new DateOnly(2026, 11, 9))],
            new WhatIfOptions { RampUpWeeks = 0 });
        var newcomer = applied.Input.People.Single(p => p.Id < 0);
        Assert.Equal(0, newcomer.CapacityOn(4));
        Assert.Equal(8, newcomer.CapacityOn(5));

        // İş 2 yeni kişiye: gün 5–14 → 15 iş günü.
        Assert.Equal(15, ResourceScheduler.Schedule(applied.Input).DurationWorkdays);
    }

    [Fact]
    public void Removing_a_task_keeps_dependency_chain()
    {
        // 1 → 2 → 3; 2 çıkınca 3, 1'e bağlı kalır.
        List<PlanActivity> chain = [Task(1, 8), Task(2, 8, 1), Task(3, 8, 2)];
        var result = ScenarioApplier.RemoveActivities(chain, new HashSet<int> { 2 });

        Assert.Equal([1, 3], result.Select(a => a.Id));
        Assert.Equal([1], result.Single(a => a.Id == 3).Predecessors);
    }

    [Fact]
    public void Remove_person_and_capacity_change_and_deadline()
    {
        var input = TwoTasksOnePerson() with
        {
            People = [new PlanResource(1, "Ayşe", Skill.Backend, 40, 100), new PlanResource(2, "Ali", Skill.Backend, 40, 100)],
            Activities = [Task(1, 80) with { FixedAssigneeId = 2 }, Task(2, 80)]
        };

        var applied = ScenarioApplier.Apply(input, NoParents, Monday,
        [
            new ScenarioChange(ScenarioChangeKind.RemovePerson, PersonId: 2),
            new ScenarioChange(ScenarioChangeKind.ChangeCapacity, PersonId: 1, WeeklyHours: 20),
            new ScenarioChange(ScenarioChangeKind.ChangeDeadline, Date: new DateOnly(2027, 1, 29))
        ], new WhatIfOptions());

        Assert.Single(applied.Input.People);
        Assert.Null(applied.Input.Activities[0].FixedAssigneeId);   // Ali'nin işi yeniden dağıtılır
        Assert.Equal(new DateOnly(2027, 1, 29), applied.TargetDate);
        Assert.Equal(40, ResourceScheduler.Schedule(applied.Input).DurationWorkdays);   // 160 saat / 4 s/gün
        Assert.Equal(3, applied.Notes.Count);

        Assert.Throws<BusinessRuleException>(() => ScenarioApplier.Apply(input, NoParents, Monday,
            [new ScenarioChange(ScenarioChangeKind.RemovePerson, PersonId: 99)], new WhatIfOptions()));
    }

    [Fact]
    public void Triangular_and_percentile_match_hand_values()
    {
        Assert.Equal(0.85, MonteCarlo.Triangular(0, 0.85, 1.0, 1.5), 9);
        Assert.Equal(1.5, MonteCarlo.Triangular(1, 0.85, 1.0, 1.5), 9);
        // Tepe noktasında CDF = (mode − min) / (max − min) = 0,15 / 0,65.
        Assert.Equal(1.0, MonteCarlo.Triangular(0.15 / 0.65, 0.85, 1.0, 1.5), 9);

        int[] sorted = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        Assert.Equal(5, MonteCarlo.Percentile(sorted, 0.5));
        Assert.Equal(8, MonteCarlo.Percentile(sorted, 0.8));
        Assert.Equal(10, MonteCarlo.Percentile(sorted, 1.0));
    }

    [Fact]
    public void Monte_carlo_without_uncertainty_equals_deterministic_plan()
    {
        var o = new WhatIfOptions { Iterations = 20, EffortMin = 1, EffortMode = 1, EffortMax = 1 + 1e-9 };
        var r = MonteCarlo.Run(TwoTasksOnePerson(), new DateOnly(2026, 11, 27), o);

        Assert.Equal(new DateOnly(2026, 11, 27), r.P50Finish);   // 20. iş günü
        Assert.Equal(r.P50Finish, r.P90Finish);
        Assert.Equal(16000, r.P50Cost);
        Assert.Equal(1.0, r.OnTimeProbability);
    }

    [Fact]
    public void Monte_carlo_is_reproducible_and_ordered()
    {
        var o = new WhatIfOptions { Iterations = 200 };
        var a = MonteCarlo.Run(TwoTasksOnePerson(), new DateOnly(2026, 11, 27), o);
        var b = MonteCarlo.Run(TwoTasksOnePerson(), new DateOnly(2026, 11, 27), o);

        Assert.Equal(a.P80Finish, b.P80Finish);
        Assert.Equal(a.Distribution, b.Distribution);
        Assert.True(a.P50Finish <= a.P80Finish && a.P80Finish <= a.P90Finish);
        Assert.True(a.P50Cost <= a.P80Cost);
        // Çarpan ≥ 0,85: en iyi durumda 136 saat → en az 17 iş günü; ortalama çarpan ~1,12 → hedefe yetişme olasılığı düşük.
        Assert.True(a.Distribution[0].Date >= new DateOnly(2026, 11, 24));
        Assert.InRange(a.OnTimeProbability, 0, 0.5);
        Assert.Equal(1.0, a.Distribution[^1].Cumulative);
    }
}
