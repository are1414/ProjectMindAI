using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.Ai;

// AI araçlarının girdileri. Alan adları araç şemalarıyla (AiTools) birebir aynıdır.

public sealed record CreateProjectPayload(
    string Name, DateOnly StartDate, DateOnly TargetEndDate, string? Description = null, ProjectType? Type = null,
    decimal? Budget = null, string? Currency = null, decimal? HoursPerDay = null);

public sealed record UpdateProjectPayload(
    string? Name = null, string? Description = null, ProjectType? Type = null, ProjectStatus? Status = null, DateOnly? StartDate = null,
    DateOnly? TargetEndDate = null, decimal? Budget = null, string? Currency = null, decimal? HoursPerDay = null);

public sealed record AddPersonPayload(string Name, List<Skill> Skills, decimal WeeklyCapacityHours, decimal? HourlyCost = null);

public sealed record UpdatePersonPayload(
    int PersonId, string? Name = null, List<Skill>? Skills = null, decimal? WeeklyCapacityHours = null, decimal? HourlyCost = null);

public sealed record AddWorkItemPayload(
    string Name, WorkPhase Phase, Skill RequiredSkill, decimal EstimatedHours, string? Description = null,
    Priority? Priority = null, string? AssigneeName = null, string? ParentName = null);

public sealed record UpdateWorkItemPayload(
    int WorkItemId, string? Name = null, string? Description = null, WorkPhase? Phase = null, Skill? RequiredSkill = null, Priority? Priority = null,
    decimal? EstimatedHours = null, string? AssigneeName = null, WorkItemStatus? Status = null, int? PercentComplete = null,
    decimal? ActualHours = null, DateOnly? PlannedStart = null, DateOnly? PlannedEnd = null, string? ParentName = null);

public sealed record RemoveWorkItemPayload(int WorkItemId);

public sealed record AddDependencyPayload(string PredecessorName, string SuccessorName);

public static class AiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true
    };
}
