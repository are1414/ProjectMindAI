using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectMind.Application.Abstractions;
using ProjectMind.Application.Common;
using ProjectMind.Application.MissingWork;

namespace ProjectMind.Application.Ai;

/// <summary>Veri değiştirmeyen araçları çalıştırır; sonuç modele JSON olarak döner, öneri kartı oluşmaz.</summary>
public sealed class ReadOnlyToolHandler(IAppDbContext db, MissingWorkService missingWork)
{
    public async Task<ToolExecutionResult> ExecuteAsync(int sessionId, string toolName, CancellationToken ct)
    {
        var projectId = await db.ChatSessions.Where(s => s.Id == sessionId).Select(s => s.ProjectId).FirstOrDefaultAsync(ct);

        switch (toolName)
        {
            case AiTools.CheckMissingWork:
            {
                if (projectId is not { } id)
                    return new ToolExecutionResult("Henüz proje yok; önce proje oluşturulmalı.", true);

                var result = await missingWork.CheckAsync(id, ct);
                var payload = new
                {
                    templateVersion = WorkTemplateCatalog.Version,
                    missing = result.Missing.Select(m => new
                    {
                        name = m.Name,
                        phase = m.Phase.ToString(),
                        requiredSkill = m.Skill.ToString(),
                        suggestedHours = m.DefaultHours,
                        reason = m.Reason,
                        mustFinishBefore = m.SuggestedSuccessors
                    }),
                    alreadyCovered = result.Covered.ToDictionary(c => c.Key, c => c.Value),
                    totalSuggestedHours = result.TotalDefaultHours
                };
                return new ToolExecutionResult(JsonSerializer.Serialize(payload, AiJson.Options), false);
            }
            default:
                throw new BusinessRuleException($"Bilinmeyen araç: {toolName}");
        }
    }
}
