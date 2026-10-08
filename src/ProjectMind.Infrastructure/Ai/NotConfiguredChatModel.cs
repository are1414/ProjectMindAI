using ProjectMind.Application.Ai;

namespace ProjectMind.Infrastructure.Ai;

/// <summary>API anahtarı yokken (veya Provider = Mock) kullanılır; ücretsizdir ve neden bağlanamadığını anlatır.</summary>
public sealed class NotConfiguredChatModel(AiOptions options) : IChatModel
{
    public const string ModelName = "mock";

    public bool IsConfigured => false;

    public string ProviderKey => ModelName;

    /// <summary>Mock modda analiz yorumu üretilmez (LLM'siz metin yok); çağıran bu durumu uyarıya çevirir.</summary>
    public Task<ChatJsonResult> CompleteJsonAsync(ChatJsonRequest request, CancellationToken ct) =>
        throw new ChatModelException(ChatModelMessages.NotConfigured);

    public Task<ChatTurnResult> CompleteTurnAsync(ChatTurnRequest request, IChatToolExecutor tools, CancellationToken ct) =>
        Task.FromResult(new ChatTurnResult(
            "AI bağlı değil (Mock mod). Sol menüdeki ⚙ Ayarlar sayfasından Gemini API anahtarını girip kaydedin.\n\n" +
            "Teşhis:\n" + (options.Diagnostics ?? "—"),
            ModelName));
}
