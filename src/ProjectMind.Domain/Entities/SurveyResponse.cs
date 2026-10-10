namespace ProjectMind.Domain.Entities;

/// <summary>
/// Kullanıcı testi anket cevabı (Faz 9: SUS + RQ3/RQ4 Likert maddeleri). Kişisel veri yok: yalnız anonim katılımcı kodu
/// (ör. P01), onay bilgisi, gönderim zamanı ve 1–5 cevaplar saklanır. SUS puanı C#'ta hesaplanıp kayıtla saklanır.
/// </summary>
public class SurveyResponse : Entity
{
    public required string ParticipantCode { get; set; }

    /// <summary>Anket metinlerinin sürümü (ör. survey-v1); maddeler değişirse eski cevaplar ayrılabilsin.</summary>
    public required string QuestionnaireVersion { get; set; }

    /// <summary>Katılımcı bilgilendirilmiş onam metnini onayladı (onaysız kayıt alınmaz).</summary>
    public bool ConsentGiven { get; set; }

    public DateTime SubmittedAt { get; set; }

    /// <summary>SUS puanı (0–100, Brooke formülü).</summary>
    public decimal SusScore { get; set; }

    public List<SurveyAnswer> Answers { get; set; } = [];
}

/// <summary>Bir anket maddesinin cevabı (madde kodu, ör. SUS1 / RQ3_1; değer 1–5).</summary>
public class SurveyAnswer
{
    public int Id { get; set; }
    public int SurveyResponseId { get; set; }
    public required string ItemCode { get; set; }
    public int Value { get; set; }
}
