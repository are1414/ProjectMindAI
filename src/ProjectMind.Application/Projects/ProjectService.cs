using Microsoft.EntityFrameworkCore;
using ProjectMind.Application.Abstractions;
using ProjectMind.Application.Common;
using ProjectMind.Domain.Entities;

namespace ProjectMind.Application.Projects;

public sealed class ProjectService(IAppDbContext db)
{
    public async Task<IReadOnlyList<ProjectResponse>> ListAsync(CancellationToken ct)
    {
        var projects = await db.Projects.AsNoTracking().OrderBy(p => p.Id).ToListAsync(ct);
        return projects.Select(ProjectResponse.From).ToList();
    }

    public async Task<ProjectResponse> GetAsync(int id, CancellationToken ct) =>
        ProjectResponse.From(await FindAsync(id, ct));

    public async Task<ProjectResponse> CreateAsync(ProjectRequest request, CancellationToken ct)
    {
        Validate(request);
        var project = new Project { Name = request.Name };
        Apply(project, request);
        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);
        return ProjectResponse.From(project);
    }

    public async Task<ProjectResponse> UpdateAsync(int id, ProjectRequest request, CancellationToken ct)
    {
        Validate(request);
        var project = await FindAsync(id, ct);
        Apply(project, request);
        await db.SaveChangesAsync(ct);
        return ProjectResponse.From(project);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var project = await FindAsync(id, ct);
        // Bağımlılıklar iş kayıtlarına NO ACTION ile bağlı; önce onları siliyoruz.
        await db.WorkItemDependencies.Where(d => d.ProjectId == id).ExecuteDeleteAsync(ct);
        // İşlerin kendi aralarındaki üst-alt bağlantısı NO ACTION; toplu silmeden önce koparılır.
        await db.WorkItems.Where(w => w.ProjectId == id && w.ParentId != null)
            .ExecuteUpdateAsync(u => u.SetProperty(w => w.ParentId, (int?)null), ct);
        db.Projects.Remove(project);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Project> FindAsync(int id, CancellationToken ct) =>
        await db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct)
        ?? throw new NotFoundException("Proje", id);

    private static void Validate(ProjectRequest request)
    {
        if (request.TargetEndDate < request.StartDate)
            throw new BusinessRuleException("Hedef bitiş tarihi başlangıç tarihinden önce olamaz.");
    }

    private static void Apply(Project project, ProjectRequest request)
    {
        project.Name = request.Name.Trim();
        project.Description = request.Description;
        project.Type = request.Type;
        project.Status = request.Status;
        project.StartDate = request.StartDate;
        project.TargetEndDate = request.TargetEndDate;
        project.Budget = request.Budget;
        project.Currency = request.Currency.ToUpperInvariant();
        project.HoursPerDay = request.HoursPerDay;
    }
}
