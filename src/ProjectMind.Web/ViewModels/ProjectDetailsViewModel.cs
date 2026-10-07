using ProjectMind.Application.Dependencies;
using ProjectMind.Application.People;
using ProjectMind.Application.Projects;
using ProjectMind.Application.WorkItems;

namespace ProjectMind.Web.ViewModels;

public sealed record ProjectDetailsViewModel(
    ProjectResponse Project,
    IReadOnlyList<PersonResponse> People,
    IReadOnlyList<WorkItemResponse> WorkItems,
    IReadOnlyList<DependencyResponse> Dependencies)
{
    public decimal TotalEstimatedHours => WorkItems.Sum(w => w.EstimatedHours);

    public string PersonName(int? id) => People.FirstOrDefault(p => p.Id == id)?.Name ?? "—";

    public string WorkItemName(int id) => WorkItems.FirstOrDefault(w => w.Id == id)?.Name ?? $"#{id}";
}
