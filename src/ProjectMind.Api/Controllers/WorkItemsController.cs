using Microsoft.AspNetCore.Mvc;
using ProjectMind.Application.WorkItems;

namespace ProjectMind.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:int}/work-items")]
public sealed class WorkItemsController(WorkItemService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<WorkItemResponse>> List(int projectId, CancellationToken ct) =>
        service.ListAsync(projectId, ct);

    [HttpGet("{id:int}")]
    public Task<WorkItemResponse> Get(int projectId, int id, CancellationToken ct) =>
        service.GetAsync(projectId, id, ct);

    [HttpPost]
    public async Task<ActionResult<WorkItemResponse>> Create(int projectId, WorkItemRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(projectId, request, ct);
        return CreatedAtAction(nameof(Get), new { projectId, id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public Task<WorkItemResponse> Update(int projectId, int id, WorkItemRequest request, CancellationToken ct) =>
        service.UpdateAsync(projectId, id, request, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int projectId, int id, CancellationToken ct)
    {
        await service.DeleteAsync(projectId, id, ct);
        return NoContent();
    }
}
