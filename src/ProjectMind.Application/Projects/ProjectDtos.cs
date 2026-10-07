using System.ComponentModel.DataAnnotations;
using ProjectMind.Domain.Entities;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.Projects;

public sealed class ProjectRequest
{
    [Display(Name = "Proje adı"), Required(ErrorMessage = "Proje adı zorunludur."), StringLength(200)]
    public string Name { get; set; } = "";

    [Display(Name = "Açıklama"), StringLength(2000)]
    public string? Description { get; set; }

    [Display(Name = "Proje tipi")]
    public ProjectType Type { get; set; }

    [Display(Name = "Durum")]
    public ProjectStatus Status { get; set; }

    [Display(Name = "Başlangıç tarihi")]
    public DateOnly StartDate { get; set; }

    [Display(Name = "Hedef bitiş tarihi")]
    public DateOnly TargetEndDate { get; set; }

    [Display(Name = "Bütçe"), Range(0, double.MaxValue)]
    public decimal Budget { get; set; }

    [Display(Name = "Para birimi"), Required, StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "TRY";

    [Display(Name = "Günlük çalışma saati"), Range(1, 24)]
    public decimal HoursPerDay { get; set; } = 8;

    public static ProjectRequest From(ProjectResponse p) => new()
    {
        Name = p.Name, Description = p.Description, Type = p.Type, Status = p.Status,
        StartDate = p.StartDate, TargetEndDate = p.TargetEndDate, Budget = p.Budget,
        Currency = p.Currency, HoursPerDay = p.HoursPerDay
    };
}

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
