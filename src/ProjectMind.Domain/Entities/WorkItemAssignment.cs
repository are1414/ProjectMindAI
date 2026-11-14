namespace ProjectMind.Domain.Entities;

/// <summary>
/// İş ↔ kişi ataması. Bir işe birden çok kişi atanabilir; atananlar işi birlikte yapar (efor paylaşılır, D32).
/// </summary>
public class WorkItemAssignment
{
    public int WorkItemId { get; set; }
    public int PersonId { get; set; }

    public WorkItem? WorkItem { get; set; }
    public Person? Person { get; set; }
}
