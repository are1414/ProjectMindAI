using Microsoft.Extensions.Options;
using ProjectMind.Application.Ai;

namespace ProjectMind.Infrastructure.Ai;

/// <summary>API anahtarı yokken (veya Provider = Mock) kullanılır; ücretsizdir ve neden bağlanamadığını anlatır.</summary>
public sealed class NotConfiguredChatModel(IOptions<AiOptions> options) : IChatModel
{
    public const string ModelName = "mock";

    public Task<ChatTurnResult> CompleteTurnAsync(ChatTurnRequest request, IChatToolExecutor tools, CancellationToken ct) =>
        Task.FromResult(new ChatTurnResult(
            "AI bağlı değil (Mock mod). Gemini anahtarını src/ProjectMind.Web/appsettings.Local.json dosyasına " +
            "\"AI\": { \"Provider\": \"Gemini\", \"Gemini\": { \"ApiKey\": \"...\" } } şeklinde yazıp uygulamayı yeniden başlatın.\n\n" +
            "Teşhis:\n" + (options.Value.Diagnostics ?? "—"),
            ModelName));
}
