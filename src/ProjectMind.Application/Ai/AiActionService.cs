using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectMind.Application.Abstractions;
using ProjectMind.Application.Common;
using ProjectMind.Application.Dependencies;
using ProjectMind.Application.People;
using ProjectMind.Application.Projects;
using ProjectMind.Application.WorkItems;
using ProjectMind.Domain.Entities;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.Ai;

public sealed record AiActionResponse(
    int Id, int ChatSessionId, int? ChatMessageId, string ToolName, string Summary,
    AiActionStatus Status, string? ResultMessage)
{
    public static AiActionResponse From(AiAction a) =>
        new(a.Id, a.ChatSessionId, a.ChatMessageId, a.ToolName, a.Summary, a.Status, a.ResultMessage);
}

/// <summary>
/// AI önerilerinin yaşam döngüsü: öner (Pending) → uygula (Applied/Failed) veya reddet (Rejected).
/// Uygulama her zaman mevcut servisler üzerinden yapılır; iş kuralları aynen geçerlidir.
/// </summary>
public sealed class AiActionService(
    IAppDbContext db,
    ProjectService projects,
    PersonService people,
    WorkItemService workItems,
    DependencyService dependencies)
{
    private const string DefaultCurrency = "TRY";
    private const decimal DefaultHoursPerDay = 8;

    public async Task<IReadOnlyList<AiActionResponse>> ListAsync(int sessionId, CancellationToken ct)
    {
        var actions = await db.AiActions.AsNoTracking()
            .Where(a => a.ChatSessionId == sessionId).OrderBy(a => a.Id).ToListAsync(ct);
        return actions.Select(AiActionResponse.From).ToList();
    }

    /// <summary>Modelin araç çağrısını doğrular ve onay bekleyen öneri olarak kaydeder.</summary>
    public async Task<AiActionResponse> ProposeAsync(int sessionId, string toolName, JsonElement input, CancellationToken ct)
    {
        var session = await FindSessionAsync(sessionId, ct);
        var summary = await DescribeAsync(session, toolName, input, ct);

        var action = new AiAction
        {
            ChatSessionId = sessionId,
            ToolName = toolName,
            PayloadJson = input.GetRawText(),
            Summary = summary
        };
        db.AiActions.Add(action);
        await db.SaveChangesAsync(ct);
        return AiActionResponse.From(action);
    }

    public async Task AttachToMessageAsync(IEnumerable<int> actionIds, int messageId, CancellationToken ct)
    {
        var ids = actionIds.ToList();
        await db.AiActions.Where(a => ids.Contains(a.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.ChatMessageId, messageId), ct);
    }

    public async Task<AiActionResponse> ApplyAsync(int actionId, CancellationToken ct)
    {
        var action = await db.AiActions.FirstOrDefaultAsync(a => a.Id == actionId, ct)
            ?? throw new NotFoundException("Öneri", actionId);
        if (action.Status != AiActionStatus.Pending)
            throw new BusinessRuleException("Bu öneri zaten karara bağlanmış.");

        var session = await FindSessionAsync(action.ChatSessionId, ct);
        try
        {
            action.ResultMessage = await ExecuteAsync(session, action, ct);
            action.Status = AiActionStatus.Applied;
        }
        catch (Exception ex) when (ex is BusinessRuleException or NotFoundException or JsonException)
        {
            action.Status = AiActionStatus.Failed;
            action.ResultMessage = ex is JsonException ? "Öneri verisi geçersiz." : ex.Message;
        }

        action.DecidedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return AiActionResponse.From(action);
    }

    /// <summary>Sohbetteki tüm bekleyen önerileri mantıklı sırayla (proje → kişi → iş → bağımlılık) uygular.</summary>
    public async Task<IReadOnlyList<AiActionResponse>> ApplyAllPendingAsync(int sessionId, CancellationToken ct)
    {
        var pending = await db.AiActions.AsNoTracking()
            .Where(a => a.ChatSessionId == sessionId && a.Status == AiActionStatus.Pending)
            .Select(a => new { a.Id, a.ToolName, a.PayloadJson })
            .ToListAsync(ct);

        // Aynı türde önce üst işler (parentName'siz), sonra öneri sırası.
        var results = new List<AiActionResponse>();
        foreach (var a in pending
                     .OrderBy(a => AiTools.ApplyOrder(a.ToolName))
                     .ThenBy(a => HasParent(a.PayloadJson))
                     .ThenBy(a => a.Id))
            results.Add(await ApplyAsync(a.Id, ct));
        return results;
    }

    public async Task<AiActionResponse> RejectAsync(int actionId, CancellationToken ct)
    {
        var action = await db.AiActions.FirstOrDefaultAsync(a => a.Id == actionId, ct)
            ?? throw new NotFoundException("Öneri", actionId);
        if (action.Status != AiActionStatus.Pending)
            throw new BusinessRuleException("Bu öneri zaten karara bağlanmış.");

        action.Status = AiActionStatus.Rejected;
        action.DecidedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return AiActionResponse.From(action);
    }

    // ---- Öneri doğrulama ve özet (kartta gösterilen metin modelden değil, veriden üretilir) ----

    private async Task<string> DescribeAsync(ChatSession session, string toolName, JsonElement input, CancellationToken ct)
    {
        if (toolName != AiTools.CreateProject)
            await EnsureProjectAvailableAsync(session, ct);

        switch (toolName)
        {
            case AiTools.CreateProject:
            {
                if (session.ProjectId is not null)
                    throw new BusinessRuleException("Bu sohbet zaten bir projeye bağlı; yeni proje yerine update_project kullan.");
                var p = Parse<CreateProjectPayload>(input);
                var budget = p.Budget is { } b ? $" · {Format.Money(b, (p.Currency ?? DefaultCurrency).ToUpperInvariant())}" : "";
                return $"Proje oluştur: {p.Name} · {Format.Date(p.StartDate)} → {Format.Date(p.TargetEndDate)}{budget}";
            }
            case AiTools.UpdateProject:
            {
                var p = Parse<UpdateProjectPayload>(input);
                return "Projeyi güncelle: " + Changes(
                    ("ad", p.Name), ("açıklama", p.Description), ("tip", p.Type is { } t ? Format.Name(t) : null),
                    ("durum", p.Status is { } s ? Format.Name(s) : null), ("başlangıç", Dt(p.StartDate)),
                    ("hedef bitiş", Dt(p.TargetEndDate)), ("bütçe", Num(p.Budget)), ("para birimi", p.Currency),
                    ("günlük saat", Num(p.HoursPerDay)));
            }
            case AiTools.AddPerson:
            {
                var p = Parse<AddPersonPayload>(input);
                return $"Kişi ekle: {p.Name} · {SkillList(p.Skills)} · {Format.Number(p.WeeklyCapacityHours)} saat/hafta";
            }
            case AiTools.UpdatePerson:
            {
                var p = Parse<UpdatePersonPayload>(input);
                var name = await PersonNameAsync(session, p.PersonId, ct);
                return $"Kişiyi güncelle ({name}): " + Changes(
                    ("ad", p.Name), ("beceriler", p.Skills is { } s ? SkillList(s) : null),
                    ("kapasite", Num(p.WeeklyCapacityHours)), ("saatlik maliyet", Num(p.HourlyCost)));
            }
            case AiTools.AddWorkItem:
            {
                var p = Parse<AddWorkItemPayload>(input);
                var assignee = string.IsNullOrWhiteSpace(p.AssigneeName) ? "" : $" · {p.AssigneeName}";
                var kind = string.IsNullOrWhiteSpace(p.ParentName) ? "İş ekle" : $"Alt iş ekle ({p.ParentName} altına)";
                return $"{kind}: {p.Name} · {Format.Name(p.Phase)} · {Format.Name(p.RequiredSkill)} · " +
                       $"{Format.Number(p.EstimatedHours)} saat{assignee}";
            }
            case AiTools.UpdateWorkItem:
            {
                var p = Parse<UpdateWorkItemPayload>(input);
                var name = await WorkItemNameAsync(session, p.WorkItemId, ct);
                return $"İşi güncelle ({name}): " + Changes(
                    ("ad", p.Name), ("açıklama", p.Description), ("faz", p.Phase is { } f ? Format.Name(f) : null),
                    ("beceri", p.RequiredSkill is { } r ? Format.Name(r) : null),
                    ("öncelik", p.Priority is { } pr ? Format.Name(pr) : null), ("efor", Num(p.EstimatedHours)),
                    ("atanan", p.AssigneeName), ("durum", p.Status is { } st ? Format.Name(st) : null),
                    ("ilerleme", p.PercentComplete is { } pc ? $"%{pc}" : null), ("harcanan saat", Num(p.ActualHours)),
                    ("planlanan başlangıç", Dt(p.PlannedStart)), ("planlanan bitiş", Dt(p.PlannedEnd)),
                    ("üst iş", p.ParentName is null ? null : p.ParentName.Trim().Length == 0 ? "yok (en üst seviye)" : p.ParentName));
            }
            case AiTools.RemoveWorkItem:
            {
                var p = Parse<RemoveWorkItemPayload>(input);
                return $"İşi sil: {await WorkItemNameAsync(session, p.WorkItemId, ct)}";
            }
            case AiTools.AddDependency:
            {
                var p = Parse<AddDependencyPayload>(input);
                return $"Bağımlılık ekle: {p.PredecessorName} → {p.SuccessorName}";
            }
            default:
                throw new BusinessRuleException($"Bilinmeyen araç: {toolName}");
        }
    }

    private async Task EnsureProjectAvailableAsync(ChatSession session, CancellationToken ct)
    {
        if (session.ProjectId is not null)
            return;
        var hasPendingProject = await db.AiActions.AnyAsync(a => a.ChatSessionId == session.Id
            && a.ToolName == AiTools.CreateProject && a.Status == AiActionStatus.Pending, ct);
        if (!hasPendingProject)
            throw new BusinessRuleException("Sohbet henüz bir projeye bağlı değil; önce create_project öner.");
    }

    // ---- Uygulama ----

    private async Task<string> ExecuteAsync(ChatSession session, AiAction action, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(action.PayloadJson);
        var input = doc.RootElement;

        if (action.ToolName == AiTools.CreateProject)
        {
            var p = Parse<CreateProjectPayload>(input);
            var created = await projects.CreateAsync(new ProjectRequest
            {
                Name = p.Name,
                Description = p.Description,
                Type = p.Type ?? ProjectType.Other,
                StartDate = p.StartDate,
                TargetEndDate = p.TargetEndDate,
                Budget = p.Budget ?? 0,
                Currency = p.Currency ?? DefaultCurrency,
                HoursPerDay = p.HoursPerDay ?? DefaultHoursPerDay
            }, ct);
            session.ProjectId = created.Id;
            session.Title = created.Name;
            return "Proje oluşturuldu.";
        }

        var projectId = session.ProjectId
            ?? throw new BusinessRuleException("Önce proje önerisini uygulayın.");

        switch (action.ToolName)
        {
            case AiTools.UpdateProject:
            {
                var p = Parse<UpdateProjectPayload>(input);
                var r = ProjectRequest.From(await projects.GetAsync(projectId, ct));
                r.Name = p.Name ?? r.Name;
                r.Description = p.Description ?? r.Description;
                r.Type = p.Type ?? r.Type;
                r.Status = p.Status ?? r.Status;
                r.StartDate = p.StartDate ?? r.StartDate;
                r.TargetEndDate = p.TargetEndDate ?? r.TargetEndDate;
                r.Budget = p.Budget ?? r.Budget;
                r.Currency = p.Currency ?? r.Currency;
                r.HoursPerDay = p.HoursPerDay ?? r.HoursPerDay;
                await projects.UpdateAsync(projectId, r, ct);
                if (p.Name is not null)
                    session.Title = p.Name;
                return "Proje güncellendi.";
            }
            case AiTools.AddPerson:
            {
                var p = Parse<AddPersonPayload>(input);
                await people.CreateAsync(projectId, new PersonRequest
                {
                    Name = p.Name, Skills = p.Skills, WeeklyCapacityHours = p.WeeklyCapacityHours, HourlyCost = p.HourlyCost ?? 0
                }, ct);
                return "Kişi eklendi.";
            }
            case AiTools.UpdatePerson:
            {
                var p = Parse<UpdatePersonPayload>(input);
                var r = PersonRequest.From(await people.GetAsync(projectId, p.PersonId, ct));
                r.Name = p.Name ?? r.Name;
                r.Skills = p.Skills ?? r.Skills;
                r.WeeklyCapacityHours = p.WeeklyCapacityHours ?? r.WeeklyCapacityHours;
                r.HourlyCost = p.HourlyCost ?? r.HourlyCost;
                await people.UpdateAsync(projectId, p.PersonId, r, ct);
                return "Kişi güncellendi.";
            }
            case AiTools.AddWorkItem:
            {
                var p = Parse<AddWorkItemPayload>(input);
                await workItems.CreateAsync(projectId, new WorkItemRequest
                {
                    Name = p.Name,
                    Description = p.Description,
                    Phase = p.Phase,
                    RequiredSkill = p.RequiredSkill,
                    Priority = p.Priority ?? Priority.Medium,
                    EstimatedHours = p.EstimatedHours,
                    AssigneeId = await ResolvePersonAsync(projectId, p.AssigneeName, ct),
                    ParentId = string.IsNullOrWhiteSpace(p.ParentName) ? null : await ResolveWorkItemAsync(projectId, p.ParentName, ct)
                }, ct);
                return "İş eklendi.";
            }
            case AiTools.UpdateWorkItem:
            {
                var p = Parse<UpdateWorkItemPayload>(input);
                var r = WorkItemRequest.From(await workItems.GetAsync(projectId, p.WorkItemId, ct));
                r.Name = p.Name ?? r.Name;
                r.Description = p.Description ?? r.Description;
                r.Phase = p.Phase ?? r.Phase;
                r.RequiredSkill = p.RequiredSkill ?? r.RequiredSkill;
                r.Priority = p.Priority ?? r.Priority;
                r.EstimatedHours = p.EstimatedHours ?? r.EstimatedHours;
                if (p.AssigneeName is not null)
                    r.AssigneeId = await ResolvePersonAsync(projectId, p.AssigneeName, ct);
                r.Status = p.Status ?? r.Status;
                r.PercentComplete = p.PercentComplete ?? r.PercentComplete;
                r.ActualHours = p.ActualHours ?? r.ActualHours;
                r.PlannedStart = p.PlannedStart ?? r.PlannedStart;
                r.PlannedEnd = p.PlannedEnd ?? r.PlannedEnd;
                if (p.ParentName is not null)
                    r.ParentId = p.ParentName.Trim().Length == 0 ? null : await ResolveWorkItemAsync(projectId, p.ParentName, ct);
                await workItems.UpdateAsync(projectId, p.WorkItemId, r, ct);
                return "İş güncellendi.";
            }
            case AiTools.RemoveWorkItem:
            {
                var p = Parse<RemoveWorkItemPayload>(input);
                await workItems.DeleteAsync(projectId, p.WorkItemId, ct);
                return "İş silindi.";
            }
            case AiTools.AddDependency:
            {
                var p = Parse<AddDependencyPayload>(input);
                await dependencies.CreateAsync(projectId, new DependencyRequest
                {
                    PredecessorId = await ResolveWorkItemAsync(projectId, p.PredecessorName, ct),
                    SuccessorId = await ResolveWorkItemAsync(projectId, p.SuccessorName, ct)
                }, ct);
                return "Bağımlılık eklendi.";
            }
            default:
                throw new BusinessRuleException($"Bilinmeyen araç: {action.ToolName}");
        }
    }

    private async Task<int?> ResolvePersonAsync(int projectId, string? name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        var matches = await db.People.AsNoTracking()
            .Where(p => p.ProjectId == projectId && p.Name == name.Trim())
            .Select(p => p.Id).ToListAsync(ct);
        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new BusinessRuleException($"'{name}' adlı kişi projede yok. Önce kişiyi ekleyin."),
            _ => throw new BusinessRuleException($"'{name}' adında birden fazla kişi var.")
        };
    }

    private async Task<int> ResolveWorkItemAsync(int projectId, string name, CancellationToken ct)
    {
        var matches = await db.WorkItems.AsNoTracking()
            .Where(w => w.ProjectId == projectId && w.Name == name.Trim())
            .Select(w => w.Id).ToListAsync(ct);
        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new BusinessRuleException($"'{name}' adlı iş projede yok."),
            _ => throw new BusinessRuleException($"'{name}' adında birden fazla iş var.")
        };
    }

    private async Task<string> PersonNameAsync(ChatSession session, int personId, CancellationToken ct) =>
        await db.People.Where(p => p.Id == personId && p.ProjectId == session.ProjectId)
            .Select(p => p.Name).FirstOrDefaultAsync(ct)
        ?? throw new BusinessRuleException($"{personId} id'li kişi bu projede yok.");

    private async Task<string> WorkItemNameAsync(ChatSession session, int workItemId, CancellationToken ct) =>
        await db.WorkItems.Where(w => w.Id == workItemId && w.ProjectId == session.ProjectId)
            .Select(w => w.Name).FirstOrDefaultAsync(ct)
        ?? throw new BusinessRuleException($"{workItemId} id'li iş bu projede yok.");

    private async Task<ChatSession> FindSessionAsync(int sessionId, CancellationToken ct) =>
        await db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
        ?? throw new NotFoundException("Sohbet", sessionId);

    private static T Parse<T>(JsonElement input)
    {
        try
        {
            return input.Deserialize<T>(AiJson.Options)
                ?? throw new BusinessRuleException("Araç girdisi boş.");
        }
        catch (JsonException ex)
        {
            throw new BusinessRuleException($"Araç girdisi geçersiz: {ex.Message}");
        }
    }

    private static bool HasParent(string payloadJson)
    {
        using var doc = JsonDocument.Parse(payloadJson);
        return doc.RootElement.TryGetProperty("parentName", out var parent)
               && parent.ValueKind == JsonValueKind.String
               && !string.IsNullOrWhiteSpace(parent.GetString());
    }

    private static string SkillList(IEnumerable<Skill> skills) => string.Join(", ", skills.Select(s => Format.Name(s)));

    private static string? Dt(DateOnly? d) => d is null ? null : Format.Date(d);

    private static string? Num(decimal? n) => n is { } v ? Format.Number(v) : null;

    private static string Changes(params (string Label, string? Value)[] fields)
    {
        var changed = fields.Where(f => f.Value is not null).Select(f => $"{f.Label} = {f.Value}").ToList();
        if (changed.Count == 0)
            throw new BusinessRuleException("Değişecek alan belirtilmedi.");
        return string.Join(", ", changed);
    }
}
