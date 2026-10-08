using Microsoft.EntityFrameworkCore;
using ProjectMind.Application.Abstractions;
using ProjectMind.Application.Common;
using ProjectMind.Domain.Entities;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.WorkItems;

/// <summary>Silme isteğinin sonucu: silindi ya da (baseline'da ilerlemesi olduğu için) iptal edildi.</summary>
public enum WorkItemRemoval { Deleted, Cancelled }

public sealed class WorkItemService(IAppDbContext db, TimeProvider? clock = null)
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
        await ValidateAsync(projectId, null, request, ct);
        var item = new WorkItem { ProjectId = projectId, Name = request.Name };
        Apply(item, request);
        db.WorkItems.Add(item);
        await db.SaveChangesAsync(ct);
        if (item.Status != WorkItemStatus.NotStarted || item.PercentComplete > 0 || item.ActualHours > 0)
            await RecordProgressAsync(item, ct);
        return WorkItemResponse.From(item);
    }

    public async Task<WorkItemResponse> UpdateAsync(int projectId, int id, WorkItemRequest request, CancellationToken ct)
    {
        var item = await FindAsync(projectId, id, ct);
        await ValidateAsync(projectId, id, request, ct);
        var progressChanged = item.Status != request.Status || item.PercentComplete != request.PercentComplete
                              || item.ActualHours != request.ActualHours;
        Apply(item, request);
        await db.SaveChangesAsync(ct);
        if (progressChanged)
            await RecordProgressAsync(item, ct);
        return WorkItemResponse.From(item);
    }

    /// <summary>
    /// Silme isteği ne yapacak? İş veya alt işlerinden biri son baseline'da ve ilerlemesi / harcanan saati varsa iş silinmez,
    /// iptal edilir (silinirse baseline satırı hiç kazanılamaz ve ilerleme geçmişi kaybolur, D27). İptal edilecek bitmemiş iş
    /// yoksa (hepsi tamamlanmış) Türkçe hata verir. Veri değiştirmez (öneri kartı için).
    /// </summary>
    public async Task<WorkItemRemoval> PreviewDeleteAsync(int projectId, int id, CancellationToken ct) =>
        (await PlanDeleteAsync(projectId, id, ct)).Removal;

    private async Task<(WorkItemRemoval Removal, HashSet<int> Subtree, List<WorkItem> Open)> PlanDeleteAsync(
        int projectId, int id, CancellationToken ct)
    {
        var target = await FindAsync(projectId, id, ct);
        var subtree = WorkItemTree.SubtreeIds(await ParentMapAsync(projectId, ct), id);
        if (!await HasBaselinedProgressAsync(projectId, subtree, ct))
            return (WorkItemRemoval.Deleted, subtree, []);

        var open = await db.WorkItems
            .Where(w => subtree.Contains(w.Id) && w.Status != WorkItemStatus.Done && w.Status != WorkItemStatus.Cancelled)
            .ToListAsync(ct);
        if (open.Count == 0)
            throw new BusinessRuleException(
                $"'{target.Name}' baseline'da ve tamamlanmış; ilerleme geçmişi korunmak için silinmez veya iptal edilmez.");
        return (WorkItemRemoval.Cancelled, subtree, open);
    }

    /// <summary>
    /// İşi, tüm alt işlerini ve bunlara ait bağımlılıkları siler. İş baseline'da ve ilerlemesi/harcaması varsa silinmez;
    /// bitmemiş işler (alt işler dahil) İptal'e çevrilir ve kapsam dışı sayılır (D24, D27). Bitti işler olduğu gibi kalır.
    /// </summary>
    public async Task<WorkItemRemoval> DeleteAsync(int projectId, int id, CancellationToken ct)
    {
        var (removal, subtree, open) = await PlanDeleteAsync(projectId, id, ct);
        if (removal == WorkItemRemoval.Cancelled)
        {
            foreach (var item in open)
                item.Status = WorkItemStatus.Cancelled;
            await db.SaveChangesAsync(ct);
            foreach (var item in open)
                await RecordProgressAsync(item, ct);
            return WorkItemRemoval.Cancelled;
        }

        // Tek kayıtta: önce bağımlılıklar, sonra işler. EF kendine referanslı kayıtları doğru sırada
        // (önce alt işler) siler ve bellekteki kayıtları da günceller.
        var dependencies = await db.WorkItemDependencies
            .Where(d => subtree.Contains(d.PredecessorId) || subtree.Contains(d.SuccessorId))
            .ToListAsync(ct);
        db.WorkItemDependencies.RemoveRange(dependencies);
        var items = await db.WorkItems.Where(w => subtree.Contains(w.Id)).ToListAsync(ct);
        db.WorkItems.RemoveRange(items);
        await db.SaveChangesAsync(ct);
        return WorkItemRemoval.Deleted;
    }

    private async Task<bool> HasBaselinedProgressAsync(int projectId, IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        var baselineId = await db.Baselines.Where(b => b.ProjectId == projectId)
            .OrderByDescending(b => b.Id).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
        if (baselineId is null)
            return false;
        var baselined = await db.Baselines.Where(b => b.Id == baselineId)
            .SelectMany(b => b.Items).Select(i => i.WorkItemId).Where(w => ids.Contains(w)).Distinct().ToListAsync(ct);
        return await db.WorkItems.AnyAsync(w => baselined.Contains(w.Id)
            && (w.PercentComplete > 0 || w.ActualHours > 0 || w.Status == WorkItemStatus.Done), ct);
    }

    /// <summary>İlerleme geçmişi (EVM trendi ve ML veri seti için).</summary>
    private async Task RecordProgressAsync(WorkItem item, CancellationToken ct)
    {
        db.StatusUpdates.Add(new StatusUpdate
        {
            WorkItemId = item.Id,
            ProjectId = item.ProjectId,
            Date = DateOnly.FromDateTime((clock ?? TimeProvider.System).GetLocalNow().DateTime),
            Status = item.Status,
            PercentComplete = item.PercentComplete,
            ActualHours = item.ActualHours
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task<Dictionary<int, int?>> ParentMapAsync(int projectId, CancellationToken ct) =>
        await db.WorkItems.AsNoTracking()
            .Where(w => w.ProjectId == projectId)
            .ToDictionaryAsync(w => w.Id, w => w.ParentId, ct);

    private async Task<WorkItem> FindAsync(int projectId, int id, CancellationToken ct) =>
        await db.WorkItems.FirstOrDefaultAsync(w => w.Id == id && w.ProjectId == projectId, ct)
        ?? throw new NotFoundException("İş", id);

    private async Task EnsureProjectExistsAsync(int projectId, CancellationToken ct)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == projectId, ct))
            throw new NotFoundException("Proje", projectId);
    }

    private async Task ValidateAsync(int projectId, int? itemId, WorkItemRequest request, CancellationToken ct)
    {
        if (request.ParentId is { } parentId)
        {
            var parents = await ParentMapAsync(projectId, ct);
            if (!parents.ContainsKey(parentId))
                throw new BusinessRuleException("Üst iş bu projeye ait değil.");
            if (itemId is { } self && WorkItemTree.SubtreeIds(parents, self).Contains(parentId))
                throw new BusinessRuleException("Bir iş kendisinin veya kendi alt işinin altına taşınamaz.");
        }

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
        item.ParentId = request.ParentId;
        item.PlannedStart = request.PlannedStart;
        item.PlannedEnd = request.PlannedEnd;
        item.Status = request.Status;
        item.PercentComplete = request.PercentComplete;
        item.ActualHours = request.ActualHours;
    }
}
