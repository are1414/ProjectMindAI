using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectMind.Application.Abstractions;
using ProjectMind.Application.Ai;
using ProjectMind.Application.Common;
using ProjectMind.Application.MissingWork;
using ProjectMind.Application.Projects;
using ProjectMind.Domain.Entities;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.Chat;

public sealed record ChatSessionResponse(int Id, int? ProjectId, string Title, DateTime UpdatedAt);

public sealed record ChatMessageResponse(int Id, ChatRole Role, string Content, DateTime CreatedAt);

public sealed class ChatService(
    IAppDbContext db,
    IChatModel model,
    AiActionService actions,
    ReadOnlyToolHandler readOnlyTools,
    ProjectService projects,
    ProjectContextBuilder contextBuilder,
    AiOptions options,
    MissingWorkService missingWork)
{
    /// <summary>"Eksik iş var mı?" kısayolunun sohbete yazılan metni.</summary>
    public const string MissingWorkShortcut = "Eksik iş var mı?";

    private const string NewSessionTitle = "Yeni proje sohbeti";
    private const int MaxToolsLength = 1000, MaxUnverifiedLength = 2000, MaxErrorLength = 1000;

    private static string Truncate(string text, int max) => text.Length > max ? text[..max] : text;

    public async Task<IReadOnlyList<ChatSessionResponse>> ListSessionsAsync(CancellationToken ct) =>
        await db.ChatSessions.AsNoTracking()
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => new ChatSessionResponse(s.Id, s.ProjectId, s.Title, s.UpdatedAt))
            .ToListAsync(ct);

    public async Task<ChatSessionResponse> GetSessionAsync(int sessionId, CancellationToken ct) =>
        await db.ChatSessions.AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => new ChatSessionResponse(s.Id, s.ProjectId, s.Title, s.UpdatedAt))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Sohbet", sessionId);

    public async Task<ChatSessionResponse> CreateSessionAsync(CancellationToken ct)
    {
        var session = new ChatSession { Title = NewSessionTitle };
        db.ChatSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return new ChatSessionResponse(session.Id, null, session.Title, session.UpdatedAt);
    }

    /// <summary>Projenin en son sohbetini döner; yoksa projeye bağlı yeni sohbet açar.</summary>
    public async Task<ChatSessionResponse> GetOrCreateForProjectAsync(int projectId, CancellationToken ct)
    {
        var existing = await db.ChatSessions.AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => new ChatSessionResponse(s.Id, s.ProjectId, s.Title, s.UpdatedAt))
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
            return existing;

        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw new NotFoundException("Proje", projectId);
        var session = new ChatSession { ProjectId = projectId, Title = project.Name };
        db.ChatSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return new ChatSessionResponse(session.Id, projectId, session.Title, session.UpdatedAt);
    }

    /// <summary>
    /// Sohbeti siler (mesajlar ve bekleyen AI önerileri dahil; karara bağlanmış öneriler sohbetsiz kalır). deleteProject true ise bağlı proje ve tüm verisi de silinir.
    /// Projeye bağlı bir sohbet, proje silinmeden kaldırılamaz (proje listede görünmez olurdu); onun için ClearHistoryAsync.
    /// </summary>
    public async Task DeleteSessionAsync(int sessionId, bool deleteProject, CancellationToken ct)
    {
        var session = await db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new NotFoundException("Sohbet", sessionId);

        if (session.ProjectId is { } projectId)
        {
            if (!deleteProject)
                throw new BusinessRuleException("Bu sohbet bir projeye bağlı. Geçmişi temizleyebilir veya projeyle birlikte silebilirsiniz.");
            await projects.DeleteAsync(projectId, ct);
        }

        await RemoveHistoryAsync(sessionId, ct);
        await db.AiActions.Where(a => a.ChatSessionId == sessionId)
            .ExecuteUpdateAsync(u => u.SetProperty(a => a.ChatSessionId, (int?)null), ct);
        await db.ChatSessions.Where(s => s.Id == sessionId).ExecuteDeleteAsync(ct);
    }

    /// <summary>
    /// Sohbet geçmişini (mesajlar ve bekleyen AI önerileri) siler; sohbet ve bağlı proje kalır. Karara bağlanmış öneriler
    /// (uygulandı / reddedildi / hata) RQ4 kabul verisi olarak kalır, mesaj bağlantısı boşaltılır.
    /// </summary>
    public async Task ClearHistoryAsync(int sessionId, CancellationToken ct)
    {
        if (!await db.ChatSessions.AnyAsync(s => s.Id == sessionId, ct))
            throw new NotFoundException("Sohbet", sessionId);
        await RemoveHistoryAsync(sessionId, ct);
    }

    private async Task RemoveHistoryAsync(int sessionId, CancellationToken ct)
    {
        // Öneriler mesajlara NO ACTION ile bağlı: önce bekleyenler silinir, kalanların mesaj bağlantısı boşaltılır, sonra mesajlar.
        await db.AiActions.Where(a => a.ChatSessionId == sessionId && a.Status == AiActionStatus.Pending).ExecuteDeleteAsync(ct);
        await db.AiActions.Where(a => a.ChatSessionId == sessionId && a.ChatMessageId != null)
            .ExecuteUpdateAsync(u => u.SetProperty(a => a.ChatMessageId, (int?)null), ct);
        await db.ChatMessages.Where(m => m.ChatSessionId == sessionId).ExecuteDeleteAsync(ct);
    }

    public async Task<IReadOnlyList<ChatMessageResponse>> ListMessagesAsync(int sessionId, CancellationToken ct) =>
        await db.ChatMessages.AsNoTracking()
            .Where(m => m.ChatSessionId == sessionId)
            .OrderBy(m => m.Id)
            .Select(m => new ChatMessageResponse(m.Id, m.Role, m.Content, m.CreatedAt))
            .ToListAsync(ct);

    /// <summary>
    /// Kullanıcı mesajını kaydeder, modeli proje bağlamıyla çağırır, AI cevabını ve oluşan önerileri kaydeder.
    /// </summary>
    public async Task<ChatMessageResponse> SendAsync(int sessionId, string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new BusinessRuleException("Mesaj boş olamaz.");

        var session = await db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new NotFoundException("Sohbet", sessionId);

        var history = await db.ChatMessages.AsNoTracking()
            .Where(m => m.ChatSessionId == sessionId)
            .OrderByDescending(m => m.Id)
            .Take(options.MaxHistoryMessages)
            .OrderBy(m => m.Id)
            .Select(m => new ChatHistoryItem(m.Role, m.Content))
            .ToListAsync(ct);

        db.ChatMessages.Add(new ChatMessage { ChatSessionId = sessionId, Role = ChatRole.User, Content = text.Trim() });
        session.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var context = await contextBuilder.BuildAsync(sessionId, ct);
        var executor = new SessionToolExecutor(actions, readOnlyTools, sessionId);
        var request = new ChatTurnRequest(
            ChatPrompts.System,
            history,
            $"<proje_durumu>\n{context}\n</proje_durumu>\n\n{text.Trim()}",
            AiTools.All);

        string reply;
        string? modelName = null;
        IReadOnlyList<string> unverified = [];
        var outcome = AiAnalysisOutcome.Success;
        string? error = null;
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await model.CompleteTurnAsync(request, executor, ct);
            reply = string.IsNullOrWhiteSpace(result.Text) ? "Önerilerimi aşağıda görebilirsiniz." : result.Text.Trim();
            modelName = result.Model;

            // Kanıt: bu turun mesajı (bağlam dahil), önceki kullanıcı mesajları ve bu turun araç sonuçları.
            // Önceki asistan cevapları kanıt değildir; aksi halde işaretlenen bir sayı sonraki turda "doğrulanmış" olur.
            var evidence = string.Join("\n", [
                request.UserMessage,
                .. history.Where(h => h.Role == ChatRole.User).Select(h => h.Content),
                .. executor.Evidence]);
            unverified = NumberGuard.FindUnverified(reply, evidence);
            if (unverified.Count > 0)
                reply += $"\n\n⚠ Doğrulanamayan sayılar: {string.Join(", ", unverified)} — bu değerler sistem verisinde yok, kontrol edin.";
        }
        catch (ChatModelException ex)
        {
            reply = error = ex.Message;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Sağlayıcının çevirmediği zaman aşımı: sayfa çökmesin, kullanıcı mesajı cevapsız kalmasın.
            reply = error = ChatModelMessages.Timeout;
        }
        catch (HttpRequestException)
        {
            reply = error = ChatModelMessages.Unreachable;
        }
        if (error is not null)
            outcome = AiAnalysisOutcome.ModelError;
        var durationMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        var assistant = new ChatMessage
        {
            ChatSessionId = sessionId,
            Role = ChatRole.Assistant,
            Content = reply,
            Model = modelName,
            PromptVersion = ChatPrompts.Version
        };
        db.ChatMessages.Add(assistant);
        await db.SaveChangesAsync(ct);

        if (executor.CreatedActionIds.Count > 0)
            await actions.AttachToMessageAsync(executor.CreatedActionIds, assistant.Id, ct);

        // Denetim kaydı (Faz 8): tam bağlam metni ve anahtar saklanmaz, yalnız ölçümler.
        db.AiAnalysisLogs.Add(new AiAnalysisLog
        {
            Kind = AiAnalysisKind.Chat,
            ProjectId = await db.ChatSessions.Where(s => s.Id == sessionId).Select(s => s.ProjectId).FirstOrDefaultAsync(ct),
            ChatSessionId = sessionId,
            ChatMessageId = assistant.Id,
            PromptVersion = ChatPrompts.Version,
            Model = modelName,
            ToolsCalled = executor.ToolNames.Count == 0 ? null : Truncate(string.Join(",", executor.ToolNames), MaxToolsLength),
            ContextLength = context.Length,
            UnverifiedNumberCount = unverified.Count,
            UnverifiedNumbers = unverified.Count == 0 ? null : Truncate(JsonSerializer.Serialize(unverified, AiJson.Options), MaxUnverifiedLength),
            Outcome = outcome,
            ErrorMessage = error is null ? null : Truncate(error, MaxErrorLength),
            DurationMs = durationMs
        });
        await db.SaveChangesAsync(ct);

        return new ChatMessageResponse(assistant.Id, assistant.Role, assistant.Content, assistant.CreatedAt);
    }

    /// <summary>
    /// "Eksik iş var mı?" kısayolu (D26): hibrit eksik iş kontrolünü sohbet modeli olmadan çalıştırır ve her öneriyi kaynağı
    /// C#'ta belirlenmiş bir kart olarak ekler — önce şablon (Rule) işleri ve bağımlılıkları, ardından AI bağlıysa LLM (Llm)
    /// ek işleri. Mock modda yalnız kural katmanı çalışır ve AI önerileri için anahtar gerektiği notu yazılır.
    /// Cevap metni modelden değil veriden üretilir; kartlar onaylanmadan veri değişmez.
    /// </summary>
    public async Task<ChatMessageResponse> CheckMissingWorkAsync(int sessionId, CancellationToken ct)
    {
        var session = await db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new NotFoundException("Sohbet", sessionId);
        if (session.ProjectId is not { } projectId)
            throw new BusinessRuleException("Eksik iş kontrolü için sohbetin bir projeye bağlı olması gerekir.");

        db.ChatMessages.Add(new ChatMessage { ChatSessionId = sessionId, Role = ChatRole.User, Content = MissingWorkShortcut });
        session.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var hybrid = await missingWork.CheckHybridAsync(projectId, sessionId, ct);
        var currency = await db.Projects.AsNoTracking().Where(p => p.Id == projectId).Select(p => p.Currency).FirstAsync(ct);

        var created = new List<int>();
        async Task ProposeAsync(string tool, object payload, AiActionSource source)
        {
            try
            {
                var card = await actions.ProposeAsync(sessionId, tool, JsonSerializer.SerializeToElement(payload, AiJson.Options), ct, source);
                created.Add(card.Id);
            }
            catch (BusinessRuleException)
            {
                // Geçersiz öneri kart olmaz (ör. aynı adlı iş); diğer öneriler etkilenmez.
            }
        }

        foreach (var m in hybrid.Rule.Missing)
            await ProposeAsync(AiTools.AddWorkItem,
                new AddWorkItemPayload(m.Name, m.Phase, m.Skill, m.DefaultHours, m.Reason), AiActionSource.Rule);
        foreach (var m in hybrid.Rule.Missing)
            foreach (var successor in m.SuggestedSuccessors)
                await ProposeAsync(AiTools.AddDependency, new AddDependencyPayload(m.Name, successor), AiActionSource.Rule);
        foreach (var l in hybrid.Llm)
            await ProposeAsync(AiTools.AddWorkItem,
                new AddWorkItemPayload(l.Name, l.Phase, l.Skill, l.Hours, l.Reason), AiActionSource.Llm);

        var assistant = new ChatMessage
        {
            ChatSessionId = sessionId,
            Role = ChatRole.Assistant,
            Content = MissingWorkReply(hybrid, currency),
            Model = hybrid.LlmModel,
            PromptVersion = hybrid.LlmStatus == LlmLayerStatus.NotConfigured ? WorkTemplateCatalog.Version : ChatPrompts.MissingWorkVersion
        };
        db.ChatMessages.Add(assistant);
        await db.SaveChangesAsync(ct);
        if (created.Count > 0)
            await actions.AttachToMessageAsync(created, assistant.Id, ct);

        return new ChatMessageResponse(assistant.Id, assistant.Role, assistant.Content, assistant.CreatedAt);
    }

    /// <summary>Kısayol cevabı: yalnız hesaplanmış veriden (şablon gerekçeleri, C# etkisi, LLM ad/gerekçeleri).</summary>
    public static string MissingWorkReply(HybridMissingWorkResult hybrid, string currency)
    {
        var lines = new List<string>();
        var rule = hybrid.Rule.Missing;
        lines.Add(rule.Count == 0
            ? $"Şablon kontrolü ({WorkTemplateCatalog.Version}): proje tipine göre beklenen işlerin hepsi listede görünüyor."
            : $"Şablon kontrolü ({WorkTemplateCatalog.Version}): {rule.Count} eksik iş bulundu — {string.Join(", ", rule.Select(m => m.Name))}. " +
              "Saatler şablonun varsayılan tahminidir.");

        if (hybrid.Llm.Count > 0)
        {
            lines.Add($"AI ek önerileri ({hybrid.Llm.Count}, şablonların kapsamadığı işler; saat büyüklük sınıfından):");
            lines.AddRange(hybrid.Llm.Select(l => $"• {l.Name} ({l.Size}): {l.Reason}"));
        }
        else if (hybrid.LlmStatus == LlmLayerStatus.Success)
            lines.Add("AI katmanı şablonlar dışında ek eksik iş önermedi.");

        // Elenen AI önerileri görünür olsun (RQ4: LLM katmanının ne önerdiği ve neden gösterilmediği).
        if (hybrid.Duplicates.Count > 0)
            lines.Add($"Tekrar sayılıp elenen AI önerileri ({hybrid.Duplicates.Count}): " +
                      string.Join(", ", hybrid.Duplicates.Select(d => $"{d.Name} (≈ {d.DuplicateOf})")) + ".");
        var notShown = hybrid.OverLimit.Count + hybrid.LlmTruncated;
        if (notShown > 0)
            lines.Add($"Üst sınır nedeniyle gösterilmeyen AI önerisi: {notShown}" +
                      (hybrid.OverLimit.Count > 0 ? $" ({string.Join(", ", hybrid.OverLimit.Select(d => d.Name))})" : "") + ".");

        if (hybrid.LlmMessage is not null)
            lines.Add(hybrid.LlmMessage);
        if (hybrid.UnverifiedNumbers.Count > 0)
            lines.Add($"⚠ Doğrulanamayan sayılar: {string.Join(", ", hybrid.UnverifiedNumbers)} — bu değerler sistem verisinde yok, kontrol edin.");

        if (hybrid.Impact is { } i)
            lines.Add($"Önerilerin hepsi eklenirse planlanan bitiş {Format.Date(i.CurrentFinish)} → {Format.Date(i.FinishWithMissing)} " +
                      $"(+{i.ExtraWorkdays} iş günü), +{Format.Number(i.ExtraHours)} saat, +{Format.Money(i.ExtraCost, currency)}.");
        if (rule.Count > 0 || hybrid.Llm.Count > 0)
            lines.Add("Öneriler aşağıda kart olarak; istemediklerinizi reddedebilirsiniz.");
        return string.Join("\n", lines);
    }

    /// <summary>Modelin araç çağrılarını öneri olarak kaydeder; hataları modele geri bildirir ki düzeltsin.</summary>
    private sealed class SessionToolExecutor(AiActionService actions, ReadOnlyToolHandler readOnlyTools, int sessionId)
        : IChatToolExecutor
    {
        public List<int> CreatedActionIds { get; } = [];

        /// <summary>Bu turda çağrılan araçlar (sırasıyla; denetim kaydı için).</summary>
        public List<string> ToolNames { get; } = [];

        /// <summary>Bu turda modele dönen araç sonuçları ve kart özetleri (sayı doğrulaması için kanıt).</summary>
        public List<string> Evidence { get; } = [];

        public async Task<ToolExecutionResult> ExecuteAsync(string toolName, JsonElement input, CancellationToken ct)
        {
            ToolNames.Add(toolName);
            try
            {
                Evidence.Add(input.GetRawText());
                if (AiTools.ReadOnly.Contains(toolName))
                {
                    var readOnly = await readOnlyTools.ExecuteAsync(sessionId, toolName, input, ct);
                    Evidence.Add(readOnly.Content);
                    return readOnly;
                }

                var action = await actions.ProposeAsync(sessionId, toolName, input, ct);
                CreatedActionIds.Add(action.Id);
                Evidence.Add(action.Summary);
                return new ToolExecutionResult(
                    $"Öneri #{action.Id} oluşturuldu ve kullanıcı onayı bekliyor: {action.Summary}", false);
            }
            catch (BusinessRuleException ex)
            {
                return new ToolExecutionResult($"Öneri oluşturulamadı: {ex.Message}", true);
            }
        }
    }
}
