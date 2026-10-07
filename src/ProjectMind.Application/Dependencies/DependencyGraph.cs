namespace ProjectMind.Application.Dependencies;

/// <summary>Finish-to-Start bağımlılık grafiği üzerinde saf (veritabanından bağımsız) kontroller.</summary>
public static class DependencyGraph
{
    /// <summary>
    /// predecessor → successor kenarı eklenirse döngü oluşur mu?
    /// Döngü, successor'dan mevcut kenarlarla predecessor'a ulaşılabiliyorsa oluşur.
    /// </summary>
    public static bool WouldCreateCycle(
        IEnumerable<(int PredecessorId, int SuccessorId)> existingEdges, int predecessorId, int successorId)
    {
        if (predecessorId == successorId)
            return true;

        var adjacency = existingEdges
            .GroupBy(e => e.PredecessorId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.SuccessorId).ToList());

        var visited = new HashSet<int>();
        var stack = new Stack<int>();
        stack.Push(successorId);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node == predecessorId)
                return true;
            if (!visited.Add(node) || !adjacency.TryGetValue(node, out var next))
                continue;
            foreach (var n in next)
                stack.Push(n);
        }

        return false;
    }
}
