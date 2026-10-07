using Microsoft.AspNetCore.Mvc;
using ProjectMind.Application.Common;
using ProjectMind.Application.People;
using ProjectMind.Application.Projects;
using ProjectMind.Application.WorkItems;
using ProjectMind.Web.Infrastructure;

namespace ProjectMind.Web.Controllers;

[Route("projects/{projectId:int}/work-items")]
public sealed class WorkItemsController(WorkItemService workItems, PersonService people, ProjectService projects)
    : AppController
{
    [HttpGet("new")]
    public async Task<IActionResult> Create(int projectId, CancellationToken ct)
    {
        await SetContextAsync(projectId, null, ct);
        return View("Form", new WorkItemRequest());
    }

    [HttpPost("new"), ValidateAntiForgeryToken]
    public Task<IActionResult> Create(int projectId, WorkItemRequest request, CancellationToken ct) =>
        SaveAsync(projectId, null, request, ct);

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int projectId, int id, CancellationToken ct)
    {
        await SetContextAsync(projectId, id, ct);
        return View("Form", WorkItemRequest.From(await workItems.GetAsync(projectId, id, ct)));
    }

    [HttpPost("{id:int}/edit"), ValidateAntiForgeryToken]
    public Task<IActionResult> Edit(int projectId, int id, WorkItemRequest request, CancellationToken ct) =>
        SaveAsync(projectId, id, request, ct);

    [HttpPost("{id:int}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int projectId, int id, CancellationToken ct)
    {
        await workItems.DeleteAsync(projectId, id, ct);
        Success("İş ve bağımlılıkları silindi.");
        return BackToProject(projectId);
    }

    private async Task<IActionResult> SaveAsync(int projectId, int? id, WorkItemRequest request, CancellationToken ct)
    {
        await SetContextAsync(projectId, id, ct);
        if (!ModelState.IsValid)
            return View("Form", request);
        try
        {
            if (id is { } itemId)
                await workItems.UpdateAsync(projectId, itemId, request, ct);
            else
                await workItems.CreateAsync(projectId, request, ct);
            Success("İş kaydedildi.");
            return BackToProject(projectId);
        }
        catch (BusinessRuleException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View("Form", request);
        }
    }

    private async Task SetContextAsync(int projectId, int? id, CancellationToken ct)
    {
        ViewBag.Project = await projects.GetAsync(projectId, ct);
        ViewBag.People = await people.ListAsync(projectId, ct);
        ViewBag.WorkItemId = id;
    }

    private RedirectToActionResult BackToProject(int projectId) =>
        RedirectToAction("Details", "Projects", new { id = projectId }, "work-items");
}
