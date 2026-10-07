using Microsoft.AspNetCore.Mvc;
using ProjectMind.Application.Dependencies;

namespace ProjectMind.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:int}/dependencies")]
public sealed class DependenciesController(DependencyService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<DependencyResponse>> List(int projectId, CancellationToken ct) =>
        service.ListAsync(projectId, ct);

    [HttpPost]
    public async Task<ActionResult<DependencyResponse>> Create(int projectId, DependencyRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(projectId, request, ct);
        return Created($"/api/projects/{projectId}/dependencies/{created.Id}", created);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int projectId, int id, CancellationToken ct)
    {
        await service.DeleteAsync(projectId, id, ct);
        return NoContent();
    }
}
