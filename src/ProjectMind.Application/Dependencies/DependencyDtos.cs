using System.ComponentModel.DataAnnotations;
using ProjectMind.Domain.Entities;

namespace ProjectMind.Application.Dependencies;

public sealed class DependencyRequest
{
    [Display(Name = "Önce bitmesi gereken iş")]
    public int PredecessorId { get; set; }

    [Display(Name = "Sonra başlayacak iş")]
    public int SuccessorId { get; set; }
}

public sealed record DependencyResponse(int Id, int ProjectId, int PredecessorId, int SuccessorId)
{
    public static DependencyResponse From(WorkItemDependency d) =>
        new(d.Id, d.ProjectId, d.PredecessorId, d.SuccessorId);
}
