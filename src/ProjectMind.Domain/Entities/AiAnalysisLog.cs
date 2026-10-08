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
}
