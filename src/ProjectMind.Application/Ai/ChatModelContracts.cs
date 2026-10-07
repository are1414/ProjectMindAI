using System.Text.Json;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.Ai;

public sealed record ChatToolDefinition(string Name, string Description, JsonElement InputSchema);

public sealed record ChatHistoryItem(ChatRole Role, string Content);

public sealed record ChatTurnRequest(
    string SystemPrompt,
    IReadOnlyList<ChatHistoryItem> History,
    string UserMessage,
    IReadOnlyList<ChatToolDefinition> Tools);

public sealed record ChatTurnResult(string Text, string Model);

public sealed record ToolExecutionResult(string Content, bool IsError);

/// <summary>Bir sohbet turu sırasında modelin çağırdığı araçları çalıştırır.</summary>
public interface IChatToolExecutor
{
    Task<ToolExecutionResult> ExecuteAsync(string toolName, JsonElement input, CancellationToken ct);
}

/// <summary>LLM sağlayıcısı soyutlaması (D8). Araç döngüsünü sağlayıcı yürütür, araçları executor çalıştırır.</summary>
public interface IChatModel
{
    Task<ChatTurnResult> CompleteTurnAsync(ChatTurnRequest request, IChatToolExecutor tools, CancellationToken ct);
}

/// <summary>Sağlayıcıya ulaşılamadı vb. — kullanıcıya gösterilebilir mesaj taşır.</summary>
public sealed class ChatModelException(string message, Exception? inner = null) : Exception(message, inner);
