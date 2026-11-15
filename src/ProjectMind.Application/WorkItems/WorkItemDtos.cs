using System.ComponentModel.DataAnnotations;
using ProjectMind.Domain.Entities;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.WorkItems;

public sealed class WorkItemRequest
{
    [Display(Name = "İş adı"), Required(ErrorMessage = "İş adı zorunludur."),
     StringLength(300, ErrorMessage = "İş adı en fazla 300 karakter olabilir.")]
    public string Name { get; set; } = "";

    [Display(Name = "Açıklama"), StringLength(4000, ErrorMessage = "Açıklama en fazla 4000 karakter olabilir.")]
    public string? Description { get; set; }

    [Display(Name = "Faz")]
    public WorkPhase Phase { get; set; } = WorkPhase.Development;

    [Display(Name = "Gereken beceri")]
    public Skill RequiredSkill { get; set; } = Skill.Backend;

    [Display(Name = "Öncelik")]
    public Priority Priority { get; set; } = Priority.Medium;

    [Display(Name = "Tahmini efor (saat)"), Range(0.5, 10000, ErrorMessage = "Tahmini efor 0,5 ile 10.000 saat arasında olmalı.")]
    public decimal EstimatedHours { get; set; } = 8;

    /// <summary>Atanan kişiler; birden çok kişi işi birlikte yapar (D32). Boşsa otomatik plan bir kişi seçer.</summary>
    [Display(Name = "Atanan kişiler")]
    public List<int> AssigneeIds { get; set; } = [];

    [Display(Name = "Üst iş")]
    public int? ParentId { get; set; }

    [Display(Name = "Planlanan başlangıç")]
    public DateOnly? PlannedStart { get; set; }

    [Display(Name = "Planlanan bitiş")]
    public DateOnly? PlannedEnd { get; set; }

    [Display(Name = "Durum")]
    public WorkItemStatus Status { get; set; }

    [Display(Name = "Tamamlanma (%)"), Range(0, 100, ErrorMessage = "Tamamlanma 0 ile 100 arasında olmalı.")]
    public int PercentComplete { get; set; }

    [Display(Name = "Harcanan saat"), Range(0, 100000, ErrorMessage = "Harcanan saat 0 ile 100.000 arasında olmalı.")]
    public decimal ActualHours { get; set; }

    public static WorkItemRequest From(WorkItemResponse w) => new()
    {
        Name = w.Name, Description = w.Description, Phase = w.Phase, RequiredSkill = w.RequiredSkill,
        Priority = w.Priority, EstimatedHours = w.EstimatedHours, AssigneeIds = [.. w.AssigneeIds], ParentId = w.ParentId,
        PlannedStart = w.PlannedStart, PlannedEnd = w.PlannedEnd, Status = w.Status,
        PercentComplete = w.PercentComplete, ActualHours = w.ActualHours
    };
}

public sealed record WorkItemResponse(
    int Id,
    int ProjectId,
    string Name,
    string? Description,
    WorkPhase Phase,
    Skill RequiredSkill,
    Priority Priority,
    decimal EstimatedHours,
    IReadOnlyList<int> AssigneeIds,
    int? ParentId,
    DateOnly? PlannedStart,
    DateOnly? PlannedEnd,
    WorkItemStatus Status,
    int PercentComplete,
    decimal ActualHours)
{
    public static WorkItemResponse From(WorkItem w) => new(
        w.Id, w.ProjectId, w.Name, w.Description, w.Phase, w.RequiredSkill, w.Priority, w.EstimatedHours,
        w.Assignments.Select(a => a.PersonId).Order().ToList(), w.ParentId, w.PlannedStart, w.PlannedEnd, w.Status, w.PercentComplete, w.ActualHours);
}
