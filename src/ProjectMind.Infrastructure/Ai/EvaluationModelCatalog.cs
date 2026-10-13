using Microsoft.Extensions.DependencyInjection;
using ProjectMind.Application.Ai;
using ProjectMind.Application.Evaluation;

namespace ProjectMind.Infrastructure.Ai;

/// <summary>
/// Sağlayıcı karşılaştırmasının adayları (D31): API anahtarı olan her sağlayıcı (seçili olup olmamasından bağımsız) somut
/// tipiyle döner; anahtarsız sağlayıcı ve Mock atlanır ve nedeni raporda yazılır.
/// </summary>
public sealed class EvaluationModelCatalog(IServiceProvider services, AiOptions options) : IEvaluationModelCatalog
{
    public const string MockName = "Mock";
    public const string NoKeyReason = "API anahtarı yok — atlandı.";
    public const string MockReason = "LLM değil (anahtarsız mod) — karşılaştırmaya alınmaz.";

    public IReadOnlyList<EvaluationProvider> GetProviders() =>
    [
        string.IsNullOrWhiteSpace(options.Gemini.ApiKey)
            ? new EvaluationProvider(AiOptions.GeminiProvider, options.Gemini.Model, null, NoKeyReason)
            : new EvaluationProvider(AiOptions.GeminiProvider, options.Gemini.Model, services.GetRequiredService<GeminiChatModel>()),
        string.IsNullOrWhiteSpace(options.Claude.ApiKey)
            ? new EvaluationProvider(AiOptions.ClaudeProvider, options.Claude.Model, null, NoKeyReason)
            : new EvaluationProvider(AiOptions.ClaudeProvider, options.Claude.Model, services.GetRequiredService<ClaudeChatModel>()),
        new EvaluationProvider(MockName, NotConfiguredChatModel.ModelName, null, MockReason)
    ];
}
