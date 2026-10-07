using Microsoft.AspNetCore.Mvc;
using ProjectMind.Application.People;

namespace ProjectMind.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:int}/people")]
public sealed class PeopleController(PersonService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<PersonResponse>> List(int projectId, CancellationToken ct) =>
        service.ListAsync(projectId, ct);

    [HttpGet("{id:int}")]
    public Task<PersonResponse> Get(int projectId, int id, CancellationToken ct) =>
        service.GetAsync(projectId, id, ct);

    [HttpPost]
    public async Task<ActionResult<PersonResponse>> Create(int projectId, PersonRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(projectId, request, ct);
        return CreatedAtAction(nameof(Get), new { projectId, id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public Task<PersonResponse> Update(int projectId, int id, PersonRequest request, CancellationToken ct) =>
        service.UpdateAsync(projectId, id, request, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int projectId, int id, CancellationToken ct)
    {
        await service.DeleteAsync(projectId, id, ct);
        return NoContent();
    }
}
