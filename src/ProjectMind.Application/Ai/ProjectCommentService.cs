using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectMind.Application.Abstractions;
using ProjectMind.Application.Analytics;
using ProjectMind.Application.Common;
using ProjectMind.Application.WhatIf;
using ProjectMind.Domain.Entities;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.Ai;

public enum AnalysisCommentStatus
{
    /// <summary>Şemaya uygun yorum var (yeni üretildi veya önbellekten).</summary>
    Ready,

    /// <summary>AI bağlı değil (Mock); yorum üretilmedi.</summary>
    NotConfigured,

    /// <summary>Model hatası veya şemaya uymayan cevap; yorum gösterilmez.</summary>
    Failed
}

public sealed record AnalysisCommentResult(
    AnalysisCommentStatus Status,
    AnalysisComment? Comment = null,
    IReadOnlyList<string>? UnverifiedNumbers = null,
    string? Message = null,
    string? Model = null,
    DateTime? CreatedAt = null,
    bool FromCache = false);

/// <summary>
/// Durum ve What-if sekmelerindeki "AI yorumu" kartları (Faz 8). LLM hiçbir sayı hesaplamaz: girdi, salt okunur
/// araçların ürettiği deterministik JSON'dur (<see cref="AnalysisPayloads"/>); cevap JSON şemasıyla istenir ve C#'ta
/// doğrulanır, geçersizse gösterilmez. Yorumdaki her sayı NumberGuard ile yalnız bu girdiye karşı kontrol edilir.
/// Yorum yalnız istek üzerine üretilir; her çağrı <see cref="AiAnalysisLog"/>'a yazılır ve girdi değişmediyse son
/// başarılı yorum yeniden gösterilir. Yorum veriyi değiştirmez.
/// </summary>
public sealed class ProjectCommentService(IAppDbContext db, IChatModel model)
{
    public const string InvalidAnswerMessage =
        "AI cevabı beklenen biçimde değildi; doğrulanamayan yorum gösterilmedi. Biraz sonra tekrar deneyin.";

    /// <summary>Durum raporu için girdisi aynı olan son başarılı yorum (yoksa null). LLM çağrılmaz.</summary>
    public Task<AnalysisCommentResult?> FindProjectCommentAsync(ProjectStatusReport report, CancellationToken ct) =>
        FindAsync(report.Overview.Project.Id, AiAnalysisKind.ProjectComment, ProjectInput(report), ct);

    public Task<AnalysisCommentResult> GenerateProjectCommentAsync(ProjectStatusReport report, CancellationToken ct) =>
        GenerateAsync(report.Overview.Project.Id, AiAnalysisKind.ProjectComment, ChatPrompts.ProjectCommentSystem,
            ProjectInput(report), ct);

    /// <summary>Senaryo karşılaştırması için girdisi aynı olan son başarılı yorum (yoksa null). LLM çağrılmaz.</summary>
    public async Task<AnalysisCommentResult?> FindScenarioCommentAsync(int projectId, WhatIfComparison comparison, CancellationToken ct) =>
        await FindAsync(projectId, AiAnalysisKind.ScenarioComment, await ScenarioInputAsync(projectId, comparison, ct), ct);

    public async Task<AnalysisCommentResult> GenerateScenarioCommentAsync(int projectId, WhatIfComparison comparison, CancellationToken ct) =>
        await GenerateAsync(projectId, AiAnalysisKind.ScenarioComment, ChatPrompts.ScenarioCommentSystem,
            await ScenarioInputAsync(projectId, comparison, ct), ct);

    private static string ProjectInput(ProjectStatusReport report) =>
        JsonSerializer.Serialize(AnalysisPayloads.ProjectStatus(report), AiJson.Options);

    private async Task<string> ScenarioInputAsync(int projectId, WhatIfComparison comparison, CancellationToken ct)
    {
        if (comparison.Scenarios.Count == 0)
            throw new BusinessRuleException("Yorumlanacak senaryo yok.");
        var currency = await db.Projects.AsNoTracking().Where(p => p.Id == projectId).Select(p => p.Currency).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Proje", projectId);
        return JsonSerializer.Serialize(AnalysisPayloads.Comparison(comparison, currency), AiJson.Options);
    }

    /// <summary>Girdi + tür + prompt sürümünün SHA-256 özeti (önbellek anahtarı).</summary>
    public static string InputHash(AiAnalysisKind kind, string input) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{kind}|{ChatPrompts.CommentVersion}|{input}")));

    private async Task<AnalysisCommentResult?> FindAsync(int projectId, AiAnalysisKind kind, string input, CancellationToken ct)
    {
        var hash = InputHash(kind, input);
        var log = await db.AiAnalysisLogs.AsNoTracking()
            .Where(l => l.ProjectId == projectId && l.Kind == kind && l.InputHash == hash
                        && l.Outcome == AiAnalysisOutcome.Success && l.ResultJson != null)
            .OrderByDescending(l => l.Id)
            .FirstOrDefaultAsync(ct);
        if (log is null)
            return null;

        var comment = AnalysisCommentSchema.TryParse(log.ResultJson, out _);
        if (comment is null)
            return null;
        var unverified = string.IsNullOrEmpty(log.UnverifiedNumbers)
            ? []
            : JsonSerializer.Deserialize<List<string>>(log.UnverifiedNumbers) ?? [];
        return new AnalysisCommentResult(AnalysisCommentStatus.Ready, comment, unverified, null, log.Model, log.CreatedAt, FromCache: true);
    }

    private async Task<AnalysisCommentResult> GenerateAsync(
        int projectId, AiAnalysisKind kind, string systemPrompt, string input, CancellationToken ct)
    {
        if (!model.IsConfigured)
            return new AnalysisCommentResult(AnalysisCommentStatus.NotConfigured, Message: ChatModelMessages.NotConfigured);

        var log = new AiAnalysisLog
        {
            Kind = kind,
            ProjectId = projectId,
            PromptVersion = ChatPrompts.CommentVersion,
            ContextLength = input.Length,
            InputHash = InputHash(kind, input)
        };
        var request = new ChatJsonRequest(systemPrompt, $"<analiz_verisi>\n{input}\n</analiz_verisi>",
            AnalysisCommentSchema.Name, AnalysisCommentSchema.Schema);

        var started = Stopwatch.GetTimestamp();
        AnalysisCommentResult result;
        try
        {
            var answer = await model.CompleteJsonAsync(request, ct);
            log.Model = answer.Model;
            var comment = AnalysisCommentSchema.TryParse(answer.Json, out var error);
            if (comment is null)
            {
                log.Outcome = AiAnalysisOutcome.InvalidSchema;
                log.SchemaValid = false;
                log.ErrorMessage = Truncate(error);
                result = new AnalysisCommentResult(AnalysisCommentStatus.Failed, Message: InvalidAnswerMessage, Model: answer.Model);
            }
            else
            {
                // Kanıt yalnız modele verilen deterministik girdidir (araç girdisi / önceki cevaplar değil).
                var unverified = NumberGuard.FindUnverified(comment.AllText(), input);
                log.Outcome = AiAnalysisOutcome.Success;
                log.SchemaValid = true;
                log.UnverifiedNumberCount = unverified.Count;
                log.UnverifiedNumbers = unverified.Count == 0 ? null : JsonSerializer.Serialize(unverified, AiJson.Options);
                log.ResultJson = JsonSerializer.Serialize(comment, AiJson.Options);
                result = new AnalysisCommentResult(AnalysisCommentStatus.Ready, comment, unverified, null, answer.Model, DateTime.UtcNow);
            }
        }
        catch (ChatModelException ex)
        {
            result = Failed(log, ex.Message);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            result = Failed(log, ChatModelMessages.Timeout);
        }
        catch (HttpRequestException)
        {
            result = Failed(log, ChatModelMessages.Unreachable);
        }

        log.DurationMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        db.AiAnalysisLogs.Add(log);
        await db.SaveChangesAsync(ct);
        return result with { CreatedAt = result.Status == AnalysisCommentStatus.Ready ? log.CreatedAt : null };
    }

    private const int MaxErrorLength = 1000;

    private static string? Truncate(string? text) => text is { Length: > MaxErrorLength } ? text[..MaxErrorLength] : text;

    private static AnalysisCommentResult Failed(AiAnalysisLog log, string message)
    {
        log.Outcome = AiAnalysisOutcome.ModelError;
        log.ErrorMessage = Truncate(message);
        return new AnalysisCommentResult(AnalysisCommentStatus.Failed, Message: message);
    }
}
