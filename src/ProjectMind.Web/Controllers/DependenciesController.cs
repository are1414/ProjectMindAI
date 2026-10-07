using Microsoft.AspNetCore.Mvc;
using ProjectMind.Application.Common;
using ProjectMind.Application.Dependencies;
using ProjectMind.Web.Infrastructure;

namespace ProjectMind.Web.Controllers;

[Route("projects/{projectId:int}/dependencies")]
public sealed class DependenciesController(DependencyService dependencies) : AppController
{
    [HttpPost("new"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int projectId, DependencyRequest request, CancellationToken ct)
    {
        try
        {
            await dependencies.CreateAsync(projectId, request, ct);
            Success("Bağımlılık eklendi.");
        }
        catch (BusinessRuleException ex)
        {
            Error(ex.Message);
        }

        return BackToProject(projectId);
    }

    [HttpPost("{id:int}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int projectId, int id, CancellationToken ct)
    {
        await dependencies.DeleteAsync(projectId, id, ct);
        Success("Bağımlılık silindi.");
        return BackToProject(projectId);
    }

    private RedirectToActionResult BackToProject(int projectId) =>
        RedirectToAction("Details", "Projects", new { id = projectId }, "dependencies");
}
