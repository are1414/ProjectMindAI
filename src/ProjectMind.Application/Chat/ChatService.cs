using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectMind.Application.Abstractions;
using ProjectMind.Application.Ai;
using ProjectMind.Application.Common;
using ProjectMind.Domain.Entities;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.Chat;

public sealed record ChatSessionResponse(int Id, int? ProjectId, string Title, DateTime UpdatedAt);

public sealed record ChatMessageResponse(int Id, ChatRole Role, string Content, DateTime CreatedAt);

public sealed class ChatService(
    IAppDbContext db,
    IChatModel model,
    AiActionService actions,
    ProjectContextBuilder contextBuilder,
    IOptions<AiOptions> options)
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
            .Take(options.Value.MaxHistoryMessages)
            .OrderBy(m => m.Id)
            .Select(m => new ChatHistoryItem(m.Role, m.Content))
            .ToListAsync(ct);

        db.ChatMessages.Add(new ChatMessage { ChatSessionId = sessionId, Role = ChatRole.User, Content = text.Trim() });
        session.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var context = await contextBuilder.BuildAsync(sessionId, ct);
        var executor = new SessionToolExecutor(actions, sessionId);
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
    private sealed class SessionToolExecutor(AiActionService actions, int sessionId) : IChatToolExecutor
    {
        public List<int> CreatedActionIds { get; } = [];

        public async Task<ToolExecutionResult> ExecuteAsync(string toolName, JsonElement input, CancellationToken ct)
        {
            try
            {
                var action = await actions.ProposeAsync(sessionId, toolName, input, ct);
                CreatedActionIds.Add(action.Id);
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
