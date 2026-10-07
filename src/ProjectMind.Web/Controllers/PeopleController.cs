using Microsoft.AspNetCore.Mvc;
using ProjectMind.Application.Common;
using ProjectMind.Application.People;
using ProjectMind.Application.Projects;
using ProjectMind.Web.Infrastructure;

namespace ProjectMind.Web.Controllers;

[Route("projects/{projectId:int}/people")]
public sealed class PeopleController(PersonService people, ProjectService projects) : AppController
{
    [HttpGet("new")]
    public async Task<IActionResult> Create(int projectId, CancellationToken ct)
    {
        await SetContextAsync(projectId, null, ct);
        return View("Form", new PersonRequest());
    }

    [HttpPost("new"), ValidateAntiForgeryToken]
    public Task<IActionResult> Create(int projectId, PersonRequest request, CancellationToken ct) =>
        SaveAsync(projectId, null, request, ct);

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int projectId, int id, CancellationToken ct)
    {
        await SetContextAsync(projectId, id, ct);
        return View("Form", PersonRequest.From(await people.GetAsync(projectId, id, ct)));
    }

    [HttpPost("{id:int}/edit"), ValidateAntiForgeryToken]
    public Task<IActionResult> Edit(int projectId, int id, PersonRequest request, CancellationToken ct) =>
        SaveAsync(projectId, id, request, ct);

    [HttpPost("{id:int}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int projectId, int id, CancellationToken ct)
    {
        await people.DeleteAsync(projectId, id, ct);
        Success("Kişi silindi; atandığı işler boşa çıkarıldı.");
        return BackToProject(projectId);
    }

    private async Task<IActionResult> SaveAsync(int projectId, int? id, PersonRequest request, CancellationToken ct)
    {
        await SetContextAsync(projectId, id, ct);
        if (!ModelState.IsValid)
            return View("Form", request);
        try
        {
            if (id is { } personId)
                await people.UpdateAsync(projectId, personId, request, ct);
            else
                await people.CreateAsync(projectId, request, ct);
            Success("Kişi kaydedildi.");
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
        ViewBag.PersonId = id;
    }

    private RedirectToActionResult BackToProject(int projectId) =>
        RedirectToAction("Details", "Projects", new { id = projectId }, "people");
}
