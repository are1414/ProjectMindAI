using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectMind.Application.Abstractions;
using ProjectMind.Application.Ai;
using ProjectMind.Application.Common;
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
    AiOptions options)
{
    private const string NewSessionTitle = "Yeni proje sohbeti";

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
    /// Sohbeti siler (mesajlar ve AI önerileri dahil). deleteProject true ise bağlı proje ve tüm verisi de silinir.
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
        await db.ChatSessions.Where(s => s.Id == sessionId).ExecuteDeleteAsync(ct);
    }

    /// <summary>Sohbet geçmişini (mesajlar ve AI önerileri) siler; sohbet ve bağlı proje kalır.</summary>
    public async Task ClearHistoryAsync(int sessionId, CancellationToken ct)
    {
        if (!await db.ChatSessions.AnyAsync(s => s.Id == sessionId, ct))
            throw new NotFoundException("Sohbet", sessionId);
        await RemoveHistoryAsync(sessionId, ct);
    }

    private async Task RemoveHistoryAsync(int sessionId, CancellationToken ct)
    {
        // Öneriler mesajlara NO ACTION ile bağlı; önce öneriler, sonra mesajlar silinir.
        await db.AiActions.Where(a => a.ChatSessionId == sessionId).ExecuteDeleteAsync(ct);
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
        try
        {
            var result = await model.CompleteTurnAsync(request, executor, ct);
            reply = string.IsNullOrWhiteSpace(result.Text) ? "Önerilerimi aşağıda görebilirsiniz." : result.Text.Trim();
            modelName = result.Model;

            var evidence = string.Join("\n", [request.UserMessage, .. history.Select(h => h.Content), .. executor.Evidence]);
            var unverified = NumberGuard.FindUnverified(reply, evidence);
            if (unverified.Count > 0)
                reply += $"\n\n⚠ Doğrulanamayan sayılar: {string.Join(", ", unverified)} — bu değerler sistem verisinde yok, kontrol edin.";
        }
        catch (ChatModelException ex)
        {
            reply = ex.Message;
        }

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

        return new ChatMessageResponse(assistant.Id, assistant.Role, assistant.Content, assistant.CreatedAt);
    }

    /// <summary>Modelin araç çağrılarını öneri olarak kaydeder; hataları modele geri bildirir ki düzeltsin.</summary>
    private sealed class SessionToolExecutor(AiActionService actions, ReadOnlyToolHandler readOnlyTools, int sessionId)
        : IChatToolExecutor
    {
        public List<int> CreatedActionIds { get; } = [];

        /// <summary>Bu turda modele dönen araç sonuçları ve kart özetleri (sayı doğrulaması için kanıt).</summary>
        public List<string> Evidence { get; } = [];

        public async Task<ToolExecutionResult> ExecuteAsync(string toolName, JsonElement input, CancellationToken ct)
        {
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
