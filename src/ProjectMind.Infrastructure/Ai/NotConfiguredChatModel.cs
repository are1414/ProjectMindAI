using ProjectMind.Application.Ai;

namespace ProjectMind.Infrastructure.Ai;

/// <summary>API anahtarı yokken (veya Provider = Mock) kullanılır; ücretsizdir ve neden bağlanamadığını anlatır.</summary>
public sealed class NotConfiguredChatModel(AiOptions options) : IChatModel
{
    public const string ModelName = "mock";

    public Task<ChatTurnResult> CompleteTurnAsync(ChatTurnRequest request, IChatToolExecutor tools, CancellationToken ct) =>
        Task.FromResult(new ChatTurnResult(
            "AI bağlı değil (Mock mod). Sol menüdeki ⚙ Ayarlar sayfasından Gemini API anahtarını girip kaydedin.\n\n" +
            "Teşhis:\n" + (options.Diagnostics ?? "—"),
            ModelName));
}
