using ProjectMind.Application.Common;

namespace ProjectMind.Application.Planning;

public sealed record CpmActivity(int Id, decimal DurationHours, IReadOnlyList<int> Predecessors);

public sealed record CpmResult(int Id, decimal EarlyStart, decimal EarlyFinish, decimal LateStart, decimal LateFinish)
{
    public decimal Slack => LateStart - EarlyStart;
    public bool IsCritical => Slack <= CriticalPath.Tolerance;
}

/// <summary>
/// Kritik Yol Metodu (kaynak sınırı olmadan): ileri geçişle ES/EF, geri geçişle LS/LF, bolluk = LS − ES.
/// Birim: çalışma saati. Bağımlılıklar Finish-to-Start.
/// </summary>
public static class CriticalPath
{
    public const decimal Tolerance = 0.0001m;

    public static (IReadOnlyDictionary<int, CpmResult> Results, decimal ProjectDuration) Compute(IReadOnlyList<CpmActivity> activities)
    {
        var order = TopologicalOrder(activities);
        var byId = activities.ToDictionary(a => a.Id);
        var successors = activities.ToDictionary(a => a.Id, _ => new List<int>());
        foreach (var a in activities)
            foreach (var p in a.Predecessors)
                successors[p].Add(a.Id);

        var es = new Dictionary<int, decimal>();
        var ef = new Dictionary<int, decimal>();
        foreach (var id in order)
        {
            es[id] = byId[id].Predecessors.Select(p => ef[p]).DefaultIfEmpty(0).Max();
            ef[id] = es[id] + byId[id].DurationHours;
        }

        var duration = ef.Values.DefaultIfEmpty(0).Max();
        var lf = new Dictionary<int, decimal>();
        var ls = new Dictionary<int, decimal>();
        foreach (var id in order.AsEnumerable().Reverse())
        {
            lf[id] = successors[id].Select(s => ls[s]).DefaultIfEmpty(duration).Min();
            ls[id] = lf[id] - byId[id].DurationHours;
        }

        var results = order.ToDictionary(id => id, id => new CpmResult(id, es[id], ef[id], ls[id], lf[id]));
        return (results, duration);
    }

    /// <summary>Kahn algoritması; döngü varsa açıklayıcı iş kuralı hatası.</summary>
    public static List<int> TopologicalOrder(IReadOnlyList<CpmActivity> activities)
    {
        var inDegree = activities.ToDictionary(a => a.Id, a => a.Predecessors.Count);
        var successors = activities.ToDictionary(a => a.Id, _ => new List<int>());
        foreach (var a in activities)
            foreach (var p in a.Predecessors)
            {
                if (!successors.ContainsKey(p))
                    throw new BusinessRuleException($"Bağımlılık listede olmayan bir işe işaret ediyor (#{p}).");
                successors[p].Add(a.Id);
            }

        var queue = new Queue<int>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key).Order());
        var order = new List<int>();
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            order.Add(id);
            foreach (var s in successors[id])
                if (--inDegree[s] == 0)
                    queue.Enqueue(s);
        }

        if (order.Count != activities.Count)
            throw new BusinessRuleException("Bağımlılıklarda döngü var (üst iş bağımlılıkları alt işlere açıldığında oluşabilir). Planlanamıyor.");
        return order;
    }
}
