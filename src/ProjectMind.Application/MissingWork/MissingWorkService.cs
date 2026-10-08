using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectMind.Application.Abstractions;
using ProjectMind.Application.Ai;
using ProjectMind.Application.Overview;
using ProjectMind.Application.Planning;
using ProjectMind.Domain.Entities;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.MissingWork;

/// <summary>Eksik işler eklenirse planın nasıl değişeceği (kaynak kısıtlı çizelgeyle hesaplanır).</summary>
public sealed record MissingWorkImpact(
    DateOnly CurrentFinish, DateOnly FinishWithMissing, int ExtraWorkdays, decimal ExtraHours, decimal ExtraCost);

/// <summary>Hibrit eksik işin LLM katmanının durumu.</summary>
public enum LlmLayerStatus
{
    /// <summary>AI bağlı değil (Mock): yalnız kural katmanı çalıştı.</summary>
    NotConfigured,

    /// <summary>LLM çağrıldı, cevap şemaya uygun (öneri listesi boş olabilir).</summary>
    Success,

    /// <summary>Cevap şemaya uymadı; LLM önerisi yok, kural sonucu geçerli.</summary>
    InvalidSchema,

    /// <summary>Model hatası / zaman aşımı; LLM önerisi yok, kural sonucu geçerli.</summary>
    ModelError
}

/// <summary>
/// Hibrit eksik iş sonucu (D15, D26): önce kural (şablon) katmanı, ardından AI bağlıysa LLM'in şablonların kapsamadığı
/// ek önerileri. Etki, iki katmanın birlikte eklenmesi varsayımıyla hesaplanır.
/// </summary>
public sealed record HybridMissingWorkResult(
    MissingWorkResult Rule,
    IReadOnlyList<LlmMissingWorkItem> Llm,
    LlmLayerStatus LlmStatus,
    string? LlmMessage,
    IReadOnlyList<DroppedSuggestion> Dropped,
    IReadOnlyList<string> UnverifiedNumbers,
    MissingWorkImpact? Impact,
    string? LlmModel = null,
    int LlmTruncated = 0)
{
    /// <summary>Tekrar sayılıp elenen LLM önerileri (cevapta ve denetim kaydında gösterilir).</summary>
    public IReadOnlyList<DroppedSuggestion> Duplicates => Dropped.Where(d => !d.OverLimit).ToList();

    /// <summary>Tekrar değil ama üst sınır (MaxLlmSuggestions) dolduğu için gösterilmeyen LLM önerileri.</summary>
    public IReadOnlyList<DroppedSuggestion> OverLimit => Dropped.Where(d => d.OverLimit).ToList();
}

public sealed class MissingWorkService(
    ProjectOverviewService overviews,
    ScheduleService schedules,
    IAppDbContext db,
    IChatModel model,
    IOptions<MissingWorkOptions> options)
{
    public const string NotConfiguredMessage =
        "AI ek önerileri için ⚙ Ayarlar'dan API anahtarı ekleyin; şu an yalnız kural (şablon) tabanlı öneriler gösteriliyor.";

    public const string InvalidAnswerMessage =
        "AI ek önerisi beklenen biçimde değildi; yalnız kural (şablon) tabanlı öneriler gösteriliyor.";

    private const int MaxErrorLength = 1000, MaxUnverifiedLength = 2000;

    /// <summary>Yalnız kural katmanı (şablonlar) ve etkisi.</summary>
    public async Task<(MissingWorkResult Result, MissingWorkImpact? Impact)> CheckAsync(int projectId, CancellationToken ct)
    {
        var overview = await overviews.GetAsync(projectId, ct);
        var result = MissingWorkDetector.Detect(overview.Project.Type, overview.WorkItems);
        return (result, await ImpactAsync(projectId, overview, result, [], ct));
    }

    /// <summary>
    /// Kural katmanı + (AI bağlıysa) LLM katmanı. LLM hata verirse veya şema dışı cevap dönerse kural sonucu yine döner.
    /// LLM çağrısı <see cref="AiAnalysisLog"/>'a (<see cref="AiAnalysisKind.MissingWork"/>) yazılır.
    /// </summary>
    public async Task<HybridMissingWorkResult> CheckHybridAsync(int projectId, int? chatSessionId, CancellationToken ct)
    {
        var overview = await overviews.GetAsync(projectId, ct);
        var rule = MissingWorkDetector.Detect(overview.Project.Type, overview.WorkItems);

        var llm = await RunLlmLayerAsync(overview, rule, chatSessionId, ct);
        var impact = await ImpactAsync(projectId, overview, rule, llm.Kept, ct);
        return new HybridMissingWorkResult(rule, llm.Kept, llm.Status, llm.Message, llm.Dropped, llm.Unverified, impact, llm.Model,
            llm.Truncated);
    }

    /// <summary>Projede en son başarılı LLM katmanı çağrısının önerdiği iş adları (kart kaynağını belirlemek için).</summary>
    public static async Task<IReadOnlyList<string>> LatestLlmSuggestionNamesAsync(IAppDbContext db, int projectId, CancellationToken ct)
    {
        var json = await db.AiAnalysisLogs.AsNoTracking()
            .Where(l => l.ProjectId == projectId && l.Kind == AiAnalysisKind.MissingWork
                        && l.Outcome == AiAnalysisOutcome.Success && l.ResultJson != null)
            .OrderByDescending(l => l.Id)
            .Select(l => l.ResultJson)
            .FirstOrDefaultAsync(ct);
        if (json is null)
            return [];
        try
        {
            var stored = JsonSerializer.Deserialize<StoredLlmResult>(json, AiJson.Options);
            return stored?.Kept.Select(k => k.Name).ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed record LlmLayerOutcome(
        IReadOnlyList<LlmMissingWorkItem> Kept, IReadOnlyList<DroppedSuggestion> Dropped, LlmLayerStatus Status,
        string? Message, IReadOnlyList<string> Unverified, string? Model = null, int Truncated = 0);

    /// <summary>
    /// Denetim kaydında saklanan doğrulanmış LLM sonucu (ResultJson): kalanlar, elenenler (tekrar: duplicateOf dolu; üst sınır:
    /// overLimit), şema dışı düşen ve sınır aşımıyla kesilen madde sayıları.
    /// </summary>
    private sealed record StoredLlmResult(
        IReadOnlyList<LlmMissingWorkItem> Kept, IReadOnlyList<DroppedSuggestion> Dropped, int DroppedInvalid, int Truncated = 0);

    private async Task<LlmLayerOutcome> RunLlmLayerAsync(
        ProjectOverview overview, MissingWorkResult rule, int? chatSessionId, CancellationToken ct)
    {
        if (!model.IsConfigured)
            return new LlmLayerOutcome([], [], LlmLayerStatus.NotConfigured, NotConfiguredMessage, []);

        var o = options.Value;
        var input = JsonSerializer.Serialize(BuildInput(overview, rule, o), AiJson.Options);
        var log = new AiAnalysisLog
        {
            Kind = AiAnalysisKind.MissingWork,
            ProjectId = overview.Project.Id,
            ChatSessionId = chatSessionId,
            PromptVersion = ChatPrompts.MissingWorkVersion,
            ContextLength = input.Length
        };
        var request = new ChatJsonRequest(ChatPrompts.MissingWorkSystem, $"<proje_verisi>\n{input}\n</proje_verisi>",
            LlmMissingWorkSchema.Name, LlmMissingWorkSchema.Schema);

        var started = Stopwatch.GetTimestamp();
        LlmLayerOutcome outcome;
        try
        {
            var answer = await model.CompleteJsonAsync(request, ct);
            log.Model = answer.Model;
            var parsed = LlmMissingWorkSchema.TryParse(answer.Json, out var error);
            if (parsed is null)
            {
                log.Outcome = AiAnalysisOutcome.InvalidSchema;
                log.SchemaValid = false;
                log.ErrorMessage = Truncate(error, MaxErrorLength);
                outcome = new LlmLayerOutcome([], [], LlmLayerStatus.InvalidSchema, InvalidAnswerMessage, [], answer.Model);
            }
            else
            {
                var (kept, dropped) = HybridMissingWork.Filter(parsed.Suggestions,
                    overview.WorkItems.Select(w => w.Name), rule.Missing.Select(m => m.Name), o);
                // LLM sayı üretmez: ad ve gerekçedeki sayılar yalnız gönderilen proje verisinde olabilir.
                var unverified = NumberGuard.FindUnverified(
                    string.Join("\n", kept.Select(k => $"{k.Name}\n{k.Reason}")), input);
                log.Outcome = AiAnalysisOutcome.Success;
                log.SchemaValid = parsed.DroppedInvalid == 0 && parsed.Truncated == 0;
                log.UnverifiedNumberCount = unverified.Count;
                log.UnverifiedNumbers = unverified.Count == 0
                    ? null
                    : Truncate(JsonSerializer.Serialize(unverified, AiJson.Options), MaxUnverifiedLength);
                log.ResultJson = JsonSerializer.Serialize(
                    new StoredLlmResult(kept, dropped, parsed.DroppedInvalid, parsed.Truncated), AiJson.Options);
                outcome = new LlmLayerOutcome(kept, dropped, LlmLayerStatus.Success, null, unverified, answer.Model, parsed.Truncated);
            }
        }
        catch (ChatModelException ex)
        {
            outcome = Failed(log, ex.Message);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            outcome = Failed(log, ChatModelMessages.Timeout);
        }
        catch (HttpRequestException)
        {
            outcome = Failed(log, ChatModelMessages.Unreachable);
        }

        log.DurationMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        db.AiAnalysisLogs.Add(log);
        await db.SaveChangesAsync(ct);
        return outcome;
    }

    private static LlmLayerOutcome Failed(AiAnalysisLog log, string message)
    {
        log.Outcome = AiAnalysisOutcome.ModelError;
        log.ErrorMessage = Truncate(message, MaxErrorLength);
        return new LlmLayerOutcome([], [], LlmLayerStatus.ModelError,
            $"AI ek önerisi alınamadı ({message}); yalnız kural (şablon) tabanlı öneriler gösteriliyor.", []);
    }

    /// <summary>LLM'e giden özet: veritabanı değil, yalnız gerekli adlar (CLAUDE.md §3). Sayı yok.</summary>
    private static object BuildInput(ProjectOverview overview, MissingWorkResult rule, MissingWorkOptions o)
    {
        var templates = WorkTemplateCatalog.For(overview.Project.Type).ToDictionary(t => t.Key, t => t.Name);
        return new
        {
            project = new
            {
                name = overview.Project.Name,
                type = overview.Project.Type.ToString(),
                description = overview.Project.Description
            },
            existingWork = overview.WorkItems.Take(o.MaxContextWorkItems).Select(w => new { name = w.Name, phase = w.Phase.ToString() }),
            ruleSuggestions = rule.Missing.Select(m => m.Name),
            coveredTemplates = rule.Covered.Keys.Select(k => templates.GetValueOrDefault(k, k))
        };
    }

    private async Task<MissingWorkImpact?> ImpactAsync(
        int projectId, ProjectOverview overview, MissingWorkResult rule, IReadOnlyList<LlmMissingWorkItem> llm, CancellationToken ct)
    {
        if (rule.Missing.Count == 0 && llm.Count == 0)
            return null;

        // Ek işlere çakışmasın diye negatif id; önerilen ardıllar ada göre eşleşen mevcut işlerdir. LLM işlerinin ardılı yok.
        var extra = rule.Missing.Select((m, i) => new ExtraActivity(
                new PlanActivity(-(i + 1), m.Name, m.DefaultHours, m.Skill, Priority.Medium, null, []),
                overview.WorkItems.Where(w => m.SuggestedSuccessors.Contains(w.Name)).Select(w => w.Id).ToList()))
            .Concat(llm.Select((l, i) => new ExtraActivity(
                new PlanActivity(-(rule.Missing.Count + i + 1), l.Name, l.Hours, l.Skill, Priority.Medium, null, []), [])))
            .ToList();

        var (current, withMissing) = await schedules.SimulateAsync(projectId, extra, ct);
        return new MissingWorkImpact(
            current.Finish, withMissing.Finish,
            WorkCalendar.WorkdaysBetween(current.Finish, withMissing.Finish),
            withMissing.TotalHours - current.TotalHours,
            withMissing.PlannedCost - current.PlannedCost);
    }

    private static string? Truncate(string? text, int max) => text is { } t && t.Length > max ? t[..max] : text;
}
