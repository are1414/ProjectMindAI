using ProjectMind.Application.Overview;

namespace ProjectMind.Application.MissingWork;

public sealed class MissingWorkService(ProjectOverviewService overviews)
{
    public async Task<MissingWorkResult> CheckAsync(int projectId, CancellationToken ct)
    {
        var overview = await overviews.GetAsync(projectId, ct);
        return MissingWorkDetector.Detect(overview.Project.Type, overview.WorkItems);
    }
}
