using Microsoft.AspNetCore.Mvc;
using ProjectMind.Application.Projects;

namespace ProjectMind.Api.Controllers;

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController(ProjectService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ProjectResponse>> List(CancellationToken ct) => service.ListAsync(ct);

    [HttpGet("{id:int}")]
    public Task<ProjectResponse> Get(int id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<ProjectResponse>> Create(ProjectRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public Task<ProjectResponse> Update(int id, ProjectRequest request, CancellationToken ct) =>
        service.UpdateAsync(id, request, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
