namespace ProjectMind.Application.WorkItems;

/// <summary>Ağaçta gösterilecek bir iş satırı: WBS numarası (ör. "4.1"), derinlik ve alt işlerden toplanan değerlerle.</summary>
public sealed record WorkItemTreeRow(
    WorkItemResponse Item, string Code, int Depth, bool HasChildren, decimal RollupHours, decimal RollupPercent);

/// <summary>
/// WBS hiyerarşisi hesapları (veritabanından bağımsız, test edilebilir).
/// Üst işin eforu = yaprak alt işlerin eforları toplamı; ilerlemesi = yaprakların efora göre ağırlıklı ortalaması.
/// </summary>
public static class WorkItemTree
{
    private const decimal FullPercent = 100m;

    /// <summary>Kök dahil tüm alt ağacın id'leri.</summary>
    public static HashSet<int> SubtreeIds(IReadOnlyDictionary<int, int?> parentOf, int rootId)
    {
        var children = ChildrenMap(parentOf);
        var result = new HashSet<int>();
        var stack = new Stack<int>([rootId]);
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (!result.Add(id) || !children.TryGetValue(id, out var kids))
                continue;
            foreach (var kid in kids)
                stack.Push(kid);
        }
        return result;
    }

    /// <summary>İşleri ağaç sırasında (üst iş, ardından alt işleri) ve toplanmış değerleriyle döner.</summary>
    public static IReadOnlyList<WorkItemTreeRow> Build(IReadOnlyList<WorkItemResponse> items)
    {
        var byId = items.ToDictionary(i => i.Id);
        var children = items
            .Where(i => i.ParentId is { } p && byId.ContainsKey(p))
            .GroupBy(i => i.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.Id).ToList());

        var rows = new List<WorkItemTreeRow>();
        var rootNumber = 0;
        foreach (var root in items.Where(i => i.ParentId is not { } p || !byId.ContainsKey(p)).OrderBy(i => i.Id))
            Visit(root, 0, (++rootNumber).ToString());
        return rows;

        (decimal Hours, decimal DoneHours) Visit(WorkItemResponse item, int depth, string code)
        {
            var index = rows.Count;
            rows.Add(null!); // yer tutucu: değerler alt işler gezildikten sonra yazılır

            if (!children.TryGetValue(item.Id, out var kids))
            {
                rows[index] = new WorkItemTreeRow(item, code, depth, false, item.EstimatedHours, item.PercentComplete);
                return (item.EstimatedHours, item.EstimatedHours * item.PercentComplete / FullPercent);
            }

            decimal hours = 0, done = 0;
            var childNumber = 0;
            foreach (var kid in kids)
            {
                var (h, d) = Visit(kid, depth + 1, $"{code}.{++childNumber}");
                hours += h;
                done += d;
            }

            var percent = hours == 0 ? 0 : Math.Round(done / hours * FullPercent, 1);
            rows[index] = new WorkItemTreeRow(item, code, depth, true, hours, percent);
            return (hours, done);
        }
    }

    /// <summary>Alt işi olmayan işler (efor toplamında çift sayımı önlemek için sadece bunlar sayılır).</summary>
    public static IEnumerable<WorkItemResponse> Leaves(IReadOnlyList<WorkItemResponse> items)
    {
        var parents = items.Where(i => i.ParentId is not null).Select(i => i.ParentId!.Value).ToHashSet();
        return items.Where(i => !parents.Contains(i.Id));
    }

    private static Dictionary<int, List<int>> ChildrenMap(IReadOnlyDictionary<int, int?> parentOf) =>
        parentOf.Where(p => p.Value is not null)
            .GroupBy(p => p.Value!.Value)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Key).ToList());
}
