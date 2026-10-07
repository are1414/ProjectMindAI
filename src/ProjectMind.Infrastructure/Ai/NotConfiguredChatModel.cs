using ProjectMind.Application.Ai;

namespace ProjectMind.Infrastructure.Ai;

/// <summary>API anahtarı yokken (veya Provider = Mock) kullanılır; ücretsizdir ve nasıl bağlanacağını anlatır.</summary>
public sealed class NotConfiguredChatModel : IChatModel
{
    public const string ModelName = "mock";

    public Task<ChatTurnResult> CompleteTurnAsync(ChatTurnRequest request, IChatToolExecutor tools, CancellationToken ct) =>
        Task.FromResult(new ChatTurnResult(
            "AI bağlı değil (Mock mod). Claude'u bağlamak için proje klasöründe şu komutu çalıştırın:\n" +
            "dotnet user-secrets set \"AI:ApiKey\" \"<anahtar>\" --project src/ProjectMind.Web\n" +
            "ve uygulamayı yeniden başlatın.",
            ModelName));
}
