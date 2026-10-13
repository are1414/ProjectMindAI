using ProjectMind.Application.Ai;

namespace ProjectMind.Application.Evaluation;

/// <summary>
/// Karşılaştırmaya aday bir sağlayıcı. <see cref="Model"/> null ise atlanır ve <see cref="SkipReason"/> raporda gösterilir
/// (anahtar yok, Mock).
/// </summary>
public sealed record EvaluationProvider(string Name, string ModelName, IChatModel? Model, string? SkipReason = null)
{
    public bool IsAvailable => Model is { IsConfigured: true };
}

/// <summary>Ayarlı sağlayıcıların listesi (Infrastructure: anahtarı olan Gemini / Claude). Testlerde sahte modellerle değiştirilir.</summary>
public interface IEvaluationModelCatalog
{
    IReadOnlyList<EvaluationProvider> GetProviders();
}
