using System.ComponentModel.DataAnnotations;
using ProjectMind.Domain.Entities;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.Projects;

public sealed record ProjectRequest(
    [Required, StringLength(200)] string Name,
    [StringLength(2000)] string? Description,
    ProjectType Type,
    ProjectStatus Status,
    DateOnly StartDate,
    DateOnly TargetEndDate,
    [Range(0, double.MaxValue)] decimal Budget,
    [Required, StringLength(3, MinimumLength = 3)] string Currency,
    [Range(1, 24)] decimal HoursPerDay);

public sealed record ProjectResponse(
    int Id,
    string Name,
    string? Description,
    ProjectType Type,
    ProjectStatus Status,
    DateOnly StartDate,
    DateOnly TargetEndDate,
    decimal Budget,
    string Currency,
    decimal HoursPerDay,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static ProjectResponse From(Project p) => new(
        p.Id, p.Name, p.Description, p.Type, p.Status, p.StartDate, p.TargetEndDate,
        p.Budget, p.Currency, p.HoursPerDay, p.CreatedAt, p.UpdatedAt);
}
