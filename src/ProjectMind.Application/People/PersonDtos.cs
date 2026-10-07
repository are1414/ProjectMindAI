using System.ComponentModel.DataAnnotations;
using ProjectMind.Domain.Entities;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.People;

public sealed record PersonRequest(
    [Required, StringLength(200)] string Name,
    Skill Skills,
    [Range(0, 168)] decimal WeeklyCapacityHours,
    [Range(0, double.MaxValue)] decimal HourlyCost);

public sealed record PersonResponse(
    int Id,
    int ProjectId,
    string Name,
    Skill Skills,
    decimal WeeklyCapacityHours,
    decimal HourlyCost)
{
    public static PersonResponse From(Person p) =>
        new(p.Id, p.ProjectId, p.Name, p.Skills, p.WeeklyCapacityHours, p.HourlyCost);
}
