using Microsoft.AspNetCore.Mvc;
using ProjectMind.Application.Common;
using ProjectMind.Application.Dependencies;
using ProjectMind.Application.People;
using ProjectMind.Application.Projects;
using ProjectMind.Application.WorkItems;
using ProjectMind.Web.Infrastructure;
using ProjectMind.Web.ViewModels;

namespace ProjectMind.Web.Controllers;

[Route("projects")]
public sealed class ProjectsController(
    ProjectService projects,
    PersonService people,
    WorkItemService workItems,
    DependencyService dependencies) : AppController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await projects.ListAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var model = new ProjectDetailsViewModel(
            await projects.GetAsync(id, ct),
            await people.ListAsync(id, ct),
            await workItems.ListAsync(id, ct),
            await dependencies.ListAsync(id, ct));
        return View(model);
    }

    [HttpGet("new")]
    public IActionResult Create()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return View(new ProjectRequest { StartDate = today, TargetEndDate = today.AddMonths(3) });
    }

    [HttpPost("new"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProjectRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(request);
        try
        {
            var created = await projects.CreateAsync(request, ct);
            Success("Proje oluşturuldu.");
            return RedirectToAction(nameof(Details), new { id = created.Id });
        }
        catch (BusinessRuleException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(request);
        }
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        ViewBag.ProjectId = id;
        return View(ProjectRequest.From(await projects.GetAsync(id, ct)));
    }

    [HttpPost("{id:int}/edit"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProjectRequest request, CancellationToken ct)
    {
        ViewBag.ProjectId = id;
        if (!ModelState.IsValid)
            return View(request);
        try
        {
            await projects.UpdateAsync(id, request, ct);
            Success("Proje güncellendi.");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (BusinessRuleException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(request);
        }
    }

    [HttpPost("{id:int}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await projects.DeleteAsync(id, ct);
        Success("Proje silindi.");
        return RedirectToAction(nameof(Index));
    }
}
