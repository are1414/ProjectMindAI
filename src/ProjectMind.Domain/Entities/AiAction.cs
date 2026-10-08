using ProjectMind.Domain.Enums;

namespace ProjectMind.Domain.Entities;

/// <summary>
/// AI'ın önerdiği veri değişikliği. Proje yöneticisi uygulayana kadar veriye dokunmaz;
/// kabul/red kaydı akademik değerlendirmede (öneri kabul oranı) kullanılır.
/// </summary>
public class AiAction : Entity
{
    /// <summary>Sohbet silinince karara bağlanmış öneri RQ4 ölçümü için kalır; bağlantı boşalır.</summary>
    public int? ChatSessionId { get; set; }
    public int? ChatMessageId { get; set; }
    public required string ToolName { get; set; }
    public required string PayloadJson { get; set; }
    public required string Summary { get; set; }
    public AiActionStatus Status { get; set; } = AiActionStatus.Pending;
    public string? ResultMessage { get; set; }
    public DateTime? DecidedAt { get; set; }

    /// <summary>"Hepsini uygula" ile (kartlara tek tek bakılmadan) karara bağlandı mı? Kabul oranında ayrı sayılır.</summary>
    public bool DecidedInBulk { get; set; }

    /// <summary>Önerinin kaynağı (kural / LLM / kullanıcı). Eski kayıtlar ve henüz ayrıştırılmayan öneriler: Unknown.</summary>
    public AiActionSource Source { get; set; } = AiActionSource.Unknown;

    public ChatSession? ChatSession { get; set; }
}
