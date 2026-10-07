using ProjectMind.Application.Ai;

namespace ProjectMind.Infrastructure.Ai;

/// <summary>API anahtarı yokken (veya Provider = Mock) kullanılır; ücretsizdir ve nasıl bağlanacağını anlatır.</summary>
public sealed class NotConfiguredChatModel : IChatModel
{
    public const string ModelName = "mock";

    public Task<ChatTurnResult> CompleteTurnAsync(ChatTurnRequest request, IChatToolExecutor tools, CancellationToken ct) =>
        Task.FromResult(new ChatTurnResult(
            "AI bağlı değil (Mock mod). Gemini anahtarını src/ProjectMind.Web/appsettings.Local.json dosyasına " +
            "\"AI\": { \"Gemini\": { \"ApiKey\": \"...\" } } şeklinde yazıp uygulamayı yeniden başlatın.",
            ModelName));
}
