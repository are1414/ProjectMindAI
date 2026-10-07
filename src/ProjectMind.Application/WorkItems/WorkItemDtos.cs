using System.ComponentModel.DataAnnotations;
using ProjectMind.Domain.Entities;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.WorkItems;

public sealed record WorkItemRequest(
    [Required, StringLength(300)] string Name,
    [StringLength(4000)] string? Description,
    WorkPhase Phase,
    Skill RequiredSkill,
    Priority Priority,
    [Range(0.5, 10000)] decimal EstimatedHours,
    int? AssigneeId,
    DateOnly? PlannedStart,
    DateOnly? PlannedEnd,
    WorkItemStatus Status,
    [Range(0, 100)] int PercentComplete,
    [Range(0, 100000)] decimal ActualHours);

public sealed record WorkItemResponse(
    int Id,
    int ProjectId,
    string Name,
    string? Description,
    WorkPhase Phase,
    Skill RequiredSkill,
    Priority Priority,
    decimal EstimatedHours,
    int? AssigneeId,
    DateOnly? PlannedStart,
    DateOnly? PlannedEnd,
    WorkItemStatus Status,
    int PercentComplete,
    decimal ActualHours)
{
    public static WorkItemResponse From(WorkItem w) => new(
        w.Id, w.ProjectId, w.Name, w.Description, w.Phase, w.RequiredSkill, w.Priority, w.EstimatedHours,
        w.AssigneeId, w.PlannedStart, w.PlannedEnd, w.Status, w.PercentComplete, w.ActualHours);
}
