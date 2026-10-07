using Microsoft.EntityFrameworkCore;
using ProjectMind.Application.Abstractions;
using ProjectMind.Application.Common;
using ProjectMind.Domain.Entities;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.WorkItems;

public sealed class WorkItemService(IAppDbContext db)
{
    private const int FullyComplete = 100;

    public async Task<IReadOnlyList<WorkItemResponse>> ListAsync(int projectId, CancellationToken ct)
    {
        await EnsureProjectExistsAsync(projectId, ct);
        var items = await db.WorkItems.AsNoTracking()
            .Where(w => w.ProjectId == projectId).OrderBy(w => w.Id).ToListAsync(ct);
        return items.Select(WorkItemResponse.From).ToList();
    }

    public async Task<WorkItemResponse> GetAsync(int projectId, int id, CancellationToken ct) =>
        WorkItemResponse.From(await FindAsync(projectId, id, ct));

    public async Task<WorkItemResponse> CreateAsync(int projectId, WorkItemRequest request, CancellationToken ct)
    {
        await EnsureProjectExistsAsync(projectId, ct);
        await ValidateAsync(projectId, request, ct);
        var item = new WorkItem { ProjectId = projectId, Name = request.Name };
        Apply(item, request);
        db.WorkItems.Add(item);
        await db.SaveChangesAsync(ct);
        return WorkItemResponse.From(item);
    }

    public async Task<WorkItemResponse> UpdateAsync(int projectId, int id, WorkItemRequest request, CancellationToken ct)
    {
        var item = await FindAsync(projectId, id, ct);
        await ValidateAsync(projectId, request, ct);
        Apply(item, request);
        await db.SaveChangesAsync(ct);
        return WorkItemResponse.From(item);
    }

    public async Task DeleteAsync(int projectId, int id, CancellationToken ct)
    {
        var item = await FindAsync(projectId, id, ct);
        await db.WorkItemDependencies
            .Where(d => d.PredecessorId == id || d.SuccessorId == id)
            .ExecuteDeleteAsync(ct);
        db.WorkItems.Remove(item);
        await db.SaveChangesAsync(ct);
    }

    private async Task<WorkItem> FindAsync(int projectId, int id, CancellationToken ct) =>
        await db.WorkItems.FirstOrDefaultAsync(w => w.Id == id && w.ProjectId == projectId, ct)
        ?? throw new NotFoundException("İş", id);

    private async Task EnsureProjectExistsAsync(int projectId, CancellationToken ct)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == projectId, ct))
            throw new NotFoundException("Proje", projectId);
    }

    private async Task ValidateAsync(int projectId, WorkItemRequest request, CancellationToken ct)
    {
        if (!SkillRules.IsSingle(request.RequiredSkill))
            throw new BusinessRuleException("İş için tek bir gerekli beceri seçilmelidir.");

        if (request.PlannedStart is { } start && request.PlannedEnd is { } end && end < start)
            throw new BusinessRuleException("Planlanan bitiş, planlanan başlangıçtan önce olamaz.");

        if (request.Status == WorkItemStatus.Done && request.PercentComplete != FullyComplete)
            throw new BusinessRuleException("Tamamlanan bir işin ilerlemesi %100 olmalıdır.");

        if (request.AssigneeId is { } assigneeId &&
            !await db.People.AnyAsync(p => p.Id == assigneeId && p.ProjectId == projectId, ct))
            throw new BusinessRuleException("Atanan kişi bu projeye ait değil.");
    }

    private static void Apply(WorkItem item, WorkItemRequest request)
    {
        item.Name = request.Name.Trim();
        item.Description = request.Description;
        item.Phase = request.Phase;
        item.RequiredSkill = request.RequiredSkill;
        item.Priority = request.Priority;
        item.EstimatedHours = request.EstimatedHours;
        item.AssigneeId = request.AssigneeId;
        item.PlannedStart = request.PlannedStart;
        item.PlannedEnd = request.PlannedEnd;
        item.Status = request.Status;
        item.PercentComplete = request.PercentComplete;
        item.ActualHours = request.ActualHours;
    }
}
