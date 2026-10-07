namespace ProjectMind.Domain.Entities;

/// <summary>Finish-to-Start bağımlılık: Successor, Predecessor bitmeden başlayamaz.</summary>
public class WorkItemDependency : Entity
{
    public int ProjectId { get; set; }
    public int PredecessorId { get; set; }
    public int SuccessorId { get; set; }

    public WorkItem? Predecessor { get; set; }
    public WorkItem? Successor { get; set; }
}
