using ProjectMind.Domain.Enums;

namespace ProjectMind.Domain.Entities;

/// <summary>
/// AI analiz cevabı denetim kaydı (sohbet turu, proje yorumu, senaryo yorumu): hangi model/prompt sürümü, hangi araçlar,
/// bağlamın boyutu, doğrulanamayan sayılar, şema geçerliliği ve süre. RQ4 ve sağlayıcı karşılaştırması (Faz 9) için.
/// API anahtarı ve tam bağlam metni saklanmaz; yorumlarda yalnız doğrulanmış yorum JSON'u (önbellek için) saklanır.
/// Kayıtlar proje/sohbet silinse de kalır (yabancı anahtar yok).
/// </summary>
public class AiAnalysisLog : Entity
{
    public AiAnalysisKind Kind { get; set; }
    public int? ProjectId { get; set; }
    public int? ChatSessionId { get; set; }
    public int? ChatMessageId { get; set; }
    public required string PromptVersion { get; set; }
    public string? Model { get; set; }

    /// <summary>Çağrılan araçların adları, virgülle (sohbet turu).</summary>
    public string? ToolsCalled { get; set; }

    /// <summary>Modele gönderilen bağlamın (proje durumu / analiz verisi) karakter uzunluğu.</summary>
    public int ContextLength { get; set; }

    public int UnverifiedNumberCount { get; set; }
    public string? UnverifiedNumbers { get; set; }

    /// <summary>Şemalı cevaplarda geçerli mi; serbest metin sohbet cevabında null.</summary>
    public bool? SchemaValid { get; set; }

    public AiAnalysisOutcome Outcome { get; set; }
    public string? ErrorMessage { get; set; }
    public long DurationMs { get; set; }

    /// <summary>Yorum girdisinin (deterministik JSON + prompt sürümü) SHA-256 özeti; girdi değişmediyse son yorum gösterilir.</summary>
    public string? InputHash { get; set; }

    /// <summary>Doğrulanmış yorum JSON'u (yalnız başarılı yorumlarda).</summary>
    public string? ResultJson { get; set; }

    /// <summary>Yorum kartında "Bu açıklama anlaşılır mıydı?" cevabı (yalnız başarılı yorumlarda; RQ4).</summary>
    public bool? Helpful { get; set; }

    /// <summary>İsteğe bağlı anlaşılırlık puanı (1–5).</summary>
    public int? ClarityRating { get; set; }

    public DateTime? RatedAt { get; set; }

    /// <summary>
    /// Ölçüm koşusu (<see cref="EvaluationRun"/>) kaydı mı? Doluysa satır kullanıcı kullanımı değil değerlendirme verisidir:
    /// RQ4 kullanıcı özetlerine ve yorum önbelleğine girmez (D31).
    /// </summary>
    public int? EvaluationRunId { get; set; }

    /// <summary>Koşudaki sağlayıcı adı (Gemini / Claude); yalnız ölçüm kayıtlarında.</summary>
    public string? EvaluationProvider { get; set; }

    /// <summary>Görev setindeki görevin kimliği (ör. "chat-status"); yalnız ölçüm kayıtlarında.</summary>
    public string? EvaluationTask { get; set; }

    /// <summary>Sağlayıcının bildirdiği giriş token sayısı (araç turlarının toplamı); bildirmezse null.</summary>
    public int? InputTokens { get; set; }

    /// <summary>Sağlayıcının bildirdiği çıkış token sayısı (Gemini'de düşünce token'ları dahil); bildirmezse null.</summary>
    public int? OutputTokens { get; set; }
}
