using ProjectMind.Domain.Enums;

namespace ProjectMind.Domain.Entities;

/// <summary>
/// Bir ölçüm koşusu (Faz 9, D31): sabit, sürümlü görev seti; hangi sağlayıcılarla, kaç tekrar, ne zaman. Koşunun AI kayıtları
/// <see cref="AiAnalysisLog.EvaluationRunId"/>, kartları <see cref="AiAction.EvaluationRunId"/> ile bu satıra bağlıdır
/// (yabancı anahtar yok; proje silinse de kalır).
/// </summary>
public class EvaluationRun : Entity
{
    public EvaluationRunKind Kind { get; set; }

    /// <summary>Görev seti sürümü (ör. "eval-v1", "recall-v1").</summary>
    public required string TaskSetVersion { get; set; }

    /// <summary>Çalıştırılan sağlayıcılar, virgülle (ör. "Gemini/gemini-3.5-flash,Claude/claude-opus-5-5").</summary>
    public required string Providers { get; set; }

    public int Repeats { get; set; }
    public int? ProjectId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public EvaluationRunStatus Status { get; set; } = EvaluationRunStatus.Running;

    /// <summary>Atlanan sağlayıcılar ve görev hataları (kısa, kullanıcıya gösterilebilir metin).</summary>
    public string? Notes { get; set; }
}
