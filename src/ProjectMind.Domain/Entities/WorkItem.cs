using ProjectMind.Domain.Enums;

namespace ProjectMind.Domain.Entities;

public class WorkItem : Entity
{
    public int ProjectId { get; set; }

    /// <summary>Üst iş (WBS hiyerarşisi). Boşsa en üst seviyedeki iştir.</summary>
    public int? ParentId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public WorkPhase Phase { get; set; }
    public Skill RequiredSkill { get; set; }
    public Priority Priority { get; set; } = Priority.Medium;
    public decimal EstimatedHours { get; set; }

    public int? AssigneeId { get; set; }
    public DateOnly? PlannedStart { get; set; }
    public DateOnly? PlannedEnd { get; set; }

    public WorkItemStatus Status { get; set; } = WorkItemStatus.NotStarted;
    public int PercentComplete { get; set; }
    public decimal ActualHours { get; set; }

    public Project? Project { get; set; }
    public Person? Assignee { get; set; }
    public WorkItem? Parent { get; set; }
}
