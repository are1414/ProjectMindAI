using ProjectMind.Application.WorkItems;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Tests.Unit;

public class WorkItemTreeTests
{
    private static WorkItemResponse Item(int id, int? parent, decimal hours, int percent) => new(
        id, 1, $"İş {id}", null, WorkPhase.Development, Skill.Backend, Priority.Medium, hours,
        null, parent, null, null, WorkItemStatus.NotStarted, percent, 0);

    // 1 Backend (üst)
    //   2 Login API     10 saat, %100
    //   3 Ödeme API     30 saat, %0
    //     4 Kart ödeme  20 saat, %50   (3'ün alt işi → 3 de üst iş olur)
    //     5 Havale      20 saat, %0
    // 6 Test            8 saat, %25
    private static readonly WorkItemResponse[] Items =
    [
        Item(1, null, 999, 0), Item(2, 1, 10, 100), Item(3, 1, 30, 0),
        Item(4, 3, 20, 50), Item(5, 3, 20, 0), Item(6, null, 8, 25)
    ];

    [Fact]
    public void Rows_are_in_tree_order_with_depth()
    {
        var rows = WorkItemTree.Build(Items);

        Assert.Equal([1, 2, 3, 4, 5, 6], rows.Select(r => r.Item.Id));
        Assert.Equal([0, 1, 1, 2, 2, 0], rows.Select(r => r.Depth));
        Assert.Equal([true, false, true, false, false, false], rows.Select(r => r.HasChildren));
        Assert.Equal(["1", "1.1", "1.2", "1.2.1", "1.2.2", "2"], rows.Select(r => r.Code));
    }

    [Fact]
    public void Parent_effort_is_sum_of_leaves_and_progress_is_effort_weighted()
    {
        var rows = WorkItemTree.Build(Items).ToDictionary(r => r.Item.Id);

        // Ödeme API: 20 + 20 = 40 saat; yapılan 10 + 0 = 10 saat → %25
        Assert.Equal(40, rows[3].RollupHours);
        Assert.Equal(25, rows[3].RollupPercent);

        // Backend: 10 + 40 = 50 saat (kendi 999'u yok sayılır); yapılan 10 + 10 = 20 saat → %40
        Assert.Equal(50, rows[1].RollupHours);
        Assert.Equal(40, rows[1].RollupPercent);

        // Yaprak iş kendi değerini korur
        Assert.Equal(8, rows[6].RollupHours);
        Assert.Equal(25, rows[6].RollupPercent);
    }

    [Fact]
    public void Leaves_exclude_parents_so_total_effort_is_not_double_counted() =>
        Assert.Equal(10 + 20 + 20 + 8, WorkItemTree.Leaves(Items).Sum(i => i.EstimatedHours));

    [Fact]
    public void Subtree_contains_root_and_all_descendants()
    {
        var parents = Items.ToDictionary(i => i.Id, i => i.ParentId);

        var subtree = WorkItemTree.SubtreeIds(parents, 1);
        Assert.Equal([1, 2, 3, 4, 5], subtree.Order());
        Assert.Equal([3, 4, 5], WorkItemTree.SubtreeIds(parents, 3).Order());
    }

    [Fact]
    public void Parent_with_zero_effort_children_has_zero_progress()
    {
        var rows = WorkItemTree.Build([Item(1, null, 0, 0), Item(2, 1, 0, 100)]);
        Assert.Equal(0, rows[0].RollupPercent);
    }
}
