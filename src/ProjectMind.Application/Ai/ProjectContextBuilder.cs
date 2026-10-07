using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectMind.Application.Abstractions;
using ProjectMind.Application.Overview;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.Ai;

/// <summary>
/// LLM'e gönderilen kontrollü bağlam. Veritabanının tamamı değil, sadece bu sohbetin projesinin
/// özeti ve bekleyen öneriler gider. Enum'lar araç şemalarıyla aynı İngilizce adlarla yazılır.
/// </summary>
public sealed class ProjectContextBuilder(IAppDbContext db, ProjectOverviewService overviews, TimeProvider clock)
{
    public async Task<string> BuildAsync(int sessionId, CancellationToken ct)
    {
        var session = await db.ChatSessions.AsNoTracking().FirstAsync(s => s.Id == sessionId, ct);
        var pending = await db.AiActions.AsNoTracking()
            .Where(a => a.ChatSessionId == sessionId && a.Status == AiActionStatus.Pending)
            .OrderBy(a => a.Id)
            .Select(a => new { a.Id, tool = a.ToolName, a.Summary })
            .ToListAsync(ct);

        object? project = null;
        if (session.ProjectId is { } projectId)
        {
            var o = await overviews.GetAsync(projectId, ct);
            project = new
            {
                o.Project.Id,
                o.Project.Name,
                o.Project.Description,
                type = o.Project.Type.ToString(),
                status = o.Project.Status.ToString(),
                o.Project.StartDate,
                o.Project.TargetEndDate,
                o.Project.Budget,
                o.Project.Currency,
                o.Project.HoursPerDay,
                people = o.People.Select(p => new
                {
                    p.Id,
                    p.Name,
                    skills = Enum.GetValues<Skill>().Where(s => s != Skill.None && p.Skills.HasFlag(s)).Select(s => s.ToString()),
                    p.WeeklyCapacityHours,
                    p.HourlyCost
                }),
                workItems = o.WorkItems.Select(w => new
                {
                    w.Id,
                    w.Name,
                    parent = w.ParentId is { } parentId ? o.WorkItemName(parentId) : null,
                    phase = w.Phase.ToString(),
                    requiredSkill = w.RequiredSkill.ToString(),
                    priority = w.Priority.ToString(),
                    w.EstimatedHours,
                    assignee = w.AssigneeId is null ? null : o.PersonName(w.AssigneeId),
                    status = w.Status.ToString(),
                    w.PercentComplete,
                    w.ActualHours
                }),
                dependencies = o.Dependencies.Select(d => new
                {
                    predecessor = o.WorkItemName(d.PredecessorId),
                    successor = o.WorkItemName(d.SuccessorId)
                })
            };
        }

        var context = new
        {
            today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime),
            project,
            pendingSuggestions = pending
        };
        return JsonSerializer.Serialize(context, AiJson.Options);
    }
}
