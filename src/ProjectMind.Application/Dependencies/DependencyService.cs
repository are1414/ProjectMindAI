using Microsoft.EntityFrameworkCore;
using ProjectMind.Application.Abstractions;
using ProjectMind.Application.Common;
using ProjectMind.Domain.Entities;

namespace ProjectMind.Application.Dependencies;

public sealed class DependencyService(IAppDbContext db)
{
    public async Task<IReadOnlyList<DependencyResponse>> ListAsync(int projectId, CancellationToken ct)
    {
        await EnsureProjectExistsAsync(projectId, ct);
        var deps = await db.WorkItemDependencies.AsNoTracking()
            .Where(d => d.ProjectId == projectId).OrderBy(d => d.Id).ToListAsync(ct);
        return deps.Select(DependencyResponse.From).ToList();
    }

    public async Task<DependencyResponse> CreateAsync(int projectId, DependencyRequest request, CancellationToken ct)
    {
        await EnsureProjectExistsAsync(projectId, ct);

        if (request.PredecessorId == request.SuccessorId)
            throw new BusinessRuleException("Bir iş kendisine bağımlı olamaz.");

        var itemCount = await db.WorkItems.CountAsync(w => w.ProjectId == projectId &&
            (w.Id == request.PredecessorId || w.Id == request.SuccessorId), ct);
        if (itemCount != 2)
            throw new BusinessRuleException("Bağımlılıktaki iki iş de bu projeye ait olmalıdır.");

        var edges = await db.WorkItemDependencies.AsNoTracking()
            .Where(d => d.ProjectId == projectId)
            .Select(d => new { d.PredecessorId, d.SuccessorId })
            .ToListAsync(ct);

        if (edges.Any(e => e.PredecessorId == request.PredecessorId && e.SuccessorId == request.SuccessorId))
            throw new BusinessRuleException("Bu bağımlılık zaten tanımlı.");

        if (DependencyGraph.WouldCreateCycle(
                edges.Select(e => (e.PredecessorId, e.SuccessorId)), request.PredecessorId, request.SuccessorId))
            throw new BusinessRuleException("Bu bağımlılık döngü oluşturur.");

        var dependency = new WorkItemDependency
        {
            ProjectId = projectId,
            PredecessorId = request.PredecessorId,
            SuccessorId = request.SuccessorId
        };
        db.WorkItemDependencies.Add(dependency);
        await db.SaveChangesAsync(ct);
        return DependencyResponse.From(dependency);
    }

    public async Task DeleteAsync(int projectId, int id, CancellationToken ct)
    {
        var dependency = await db.WorkItemDependencies
            .FirstOrDefaultAsync(d => d.Id == id && d.ProjectId == projectId, ct)
            ?? throw new NotFoundException("Bağımlılık", id);
        db.WorkItemDependencies.Remove(dependency);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureProjectExistsAsync(int projectId, CancellationToken ct)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == projectId, ct))
            throw new NotFoundException("Proje", projectId);
    }
}
