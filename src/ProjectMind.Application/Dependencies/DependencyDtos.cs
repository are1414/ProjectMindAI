using ProjectMind.Domain.Entities;

namespace ProjectMind.Application.Dependencies;

public sealed record DependencyRequest(int PredecessorId, int SuccessorId);

public sealed record DependencyResponse(int Id, int ProjectId, int PredecessorId, int SuccessorId)
{
    public static DependencyResponse From(WorkItemDependency d) =>
        new(d.Id, d.ProjectId, d.PredecessorId, d.SuccessorId);
}
