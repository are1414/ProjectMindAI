using ProjectMind.Application.Dependencies;

namespace ProjectMind.Tests.Unit;

public class DependencyGraphTests
{
    // 1 → 2 → 3
    private static readonly (int, int)[] Chain = [(1, 2), (2, 3)];

    [Fact]
    public void Self_dependency_is_a_cycle() =>
        Assert.True(DependencyGraph.WouldCreateCycle([], 5, 5));

    [Fact]
    public void Closing_edge_back_to_start_creates_cycle() =>
        Assert.True(DependencyGraph.WouldCreateCycle(Chain, 3, 1));

    [Fact]
    public void Direct_reverse_edge_creates_cycle() =>
        Assert.True(DependencyGraph.WouldCreateCycle(Chain, 2, 1));

    [Fact]
    public void Forward_shortcut_is_not_a_cycle() =>
        Assert.False(DependencyGraph.WouldCreateCycle(Chain, 1, 3));

    [Fact]
    public void Edge_to_unrelated_node_is_not_a_cycle() =>
        Assert.False(DependencyGraph.WouldCreateCycle(Chain, 3, 4));

    [Fact]
    public void Diamond_shape_is_not_a_cycle()
    {
        // 1 → 2 → 4 ve 1 → 3; 3 → 4 eklemek döngü değildir.
        (int, int)[] edges = [(1, 2), (2, 4), (1, 3)];
        Assert.False(DependencyGraph.WouldCreateCycle(edges, 3, 4));
    }
}
