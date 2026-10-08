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

/// <summary>
/// Araçsız, JSON şemalı tek model çağrısı (analiz yorumları). Sağlayıcı cevabı şemaya zorlar; cevap yine de
/// C# tarafında şemaya göre doğrulanır (bkz. <see cref="AnalysisCommentSchema"/>).
/// </summary>
public sealed record ChatJsonRequest(string SystemPrompt, string UserMessage, string SchemaName, JsonElement Schema);

/// <summary>Modelin döndürdüğü ham JSON metni (doğrulanmamış) ve kullanılan model.</summary>
public sealed record ChatJsonResult(string Json, string Model);

/// <summary>Bir sohbet turu sırasında modelin çağırdığı araçları çalıştırır.</summary>
public interface IChatToolExecutor
{
    Task<ToolExecutionResult> ExecuteAsync(string toolName, JsonElement input, CancellationToken ct);
}

/// <summary>LLM sağlayıcısı soyutlaması (D8). Araç döngüsünü sağlayıcı yürütür, araçları executor çalıştırır.</summary>
public interface IChatModel
{
    /// <summary>Gerçek bir LLM'e bağlı mı? Mock (anahtar yok) modda false; analiz yorumu üretilmez.</summary>
    bool IsConfigured { get; }

    Task<ChatTurnResult> CompleteTurnAsync(ChatTurnRequest request, IChatToolExecutor tools, CancellationToken ct);

    /// <summary>Araçsız, cevabı JSON şemasına zorlanmış tek çağrı. Hata durumunda <see cref="ChatModelException"/>.</summary>
    Task<ChatJsonResult> CompleteJsonAsync(ChatJsonRequest request, CancellationToken ct);
}

/// <summary>Sağlayıcıya ulaşılamadı vb. — kullanıcıya gösterilebilir mesaj taşır.</summary>
public sealed class ChatModelException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Sağlayıcılardan bağımsız, kullanıcıya gösterilen ortak hata metinleri.</summary>
public static class ChatModelMessages
{
    public const string Timeout = "AI servisi zamanında cevap vermedi. Biraz sonra mesajınızı tekrar gönderin.";
    public const string Unreachable = "AI servisine bağlanılamadı. İnternet bağlantısını kontrol edin.";
    public const string NotConfigured = "AI bağlı değil — ⚙ Ayarlar'dan API anahtarı ekleyin. Yorum yalnızca gerçek bir AI sağlayıcısıyla üretilir.";
    public const string EmptyJson = "AI servisinden boş cevap geldi.";
}
