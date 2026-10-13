using ProjectMind.Application.Ai;
using ProjectMind.Domain.Entities;

namespace ProjectMind.Application.Evaluation;

/// <summary>
/// Kapsam (scope) başına ölçüm bağlamı (D31). Normal kullanımda boştur. Ölçüm koşucusu her görev için yeni bir DI kapsamı
/// açar ve servisleri çözmeden önce bunu doldurur: <see cref="ModelOverride"/> o kapsamdaki <see cref="IChatModel"/>'i
/// belirler (Infrastructure kaydı önce buna bakar), AI kayıtları ve kartlar koşuya etiketlenir, what-if çalıştırması
/// "Ölçüm koşusu" kaynağıyla yazılır. Böylece koşu verisi kullanıcı verisinden (RQ3/RQ4) ayrılır.
/// </summary>
public sealed class EvaluationRunContext
{
    public int? RunId { get; private set; }
    public string? Provider { get; private set; }
    public string? TaskId { get; private set; }

    /// <summary>Bu kapsamda kullanılacak sağlayıcı (null: ayarlı sağlayıcı).</summary>
    public IChatModel? ModelOverride { get; private set; }

    public bool IsActive => RunId is not null;

    public void Begin(int runId, string? provider, string taskId, IChatModel? model = null)
    {
        RunId = runId;
        Provider = provider;
        TaskId = taskId;
        ModelOverride = model;
    }

    /// <summary>Denetim kaydını koşuya etiketler ve sağlayıcının bildirdiği token kullanımını yazar.</summary>
    public void Apply(AiAnalysisLog log, ChatUsage? usage)
    {
        log.InputTokens = usage?.InputTokens;
        log.OutputTokens = usage?.OutputTokens;
        if (!IsActive)
            return;
        log.EvaluationRunId = RunId;
        log.EvaluationProvider = Provider;
        log.EvaluationTask = TaskId;
    }
}
