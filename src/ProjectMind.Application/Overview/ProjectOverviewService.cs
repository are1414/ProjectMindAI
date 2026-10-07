using ProjectMind.Application.Dependencies;
using ProjectMind.Application.People;
using ProjectMind.Application.Projects;
using ProjectMind.Application.WorkItems;

namespace ProjectMind.Application.Overview;

public sealed record ProjectOverview(
    ProjectResponse Project,
    IReadOnlyList<PersonResponse> People,
    IReadOnlyList<WorkItemResponse> WorkItems,
    IReadOnlyList<DependencyResponse> Dependencies)
{
    public decimal TotalEstimatedHours => WorkItems.Sum(w => w.EstimatedHours);

    public string PersonName(int? id) => People.FirstOrDefault(p => p.Id == id)?.Name ?? "—";

    public string WorkItemName(int id) => WorkItems.FirstOrDefault(w => w.Id == id)?.Name ?? $"#{id}";
}

public sealed class ProjectOverviewService(
    ProjectService projects, PersonService people, WorkItemService workItems, DependencyService dependencies)
{
    public async Task<ProjectOverview> GetAsync(int projectId, CancellationToken ct) => new(
        await projects.GetAsync(projectId, ct),
        await people.ListAsync(projectId, ct),
        await workItems.ListAsync(projectId, ct),
        await dependencies.ListAsync(projectId, ct));
}
