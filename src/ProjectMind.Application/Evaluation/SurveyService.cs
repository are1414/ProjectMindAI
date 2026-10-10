using Microsoft.EntityFrameworkCore;
using ProjectMind.Application.Abstractions;
using ProjectMind.Application.Common;
using ProjectMind.Domain.Entities;

namespace ProjectMind.Application.Evaluation;

/// <summary>Anket gönderimi: katılımcı kodu, onay ve madde kodu → cevap (1–5).</summary>
public sealed record SurveySubmission(string? ParticipantCode, bool ConsentGiven, IReadOnlyDictionary<string, int?> Answers);

public sealed record SurveyResponseRow(int Id, string ParticipantCode, DateTime SubmittedAt, decimal SusScore, string Adjective);

/// <summary>Değerlendirme sayfası için anket özeti: SUS istatistiği, ortalamanın Bangor sıfatı ve Likert maddeleri.</summary>
public sealed record SurveySummary(
    DescriptiveStats Sus, string? MeanAdjective, IReadOnlyList<LikertSummary> Likert, IReadOnlyList<SurveyResponseRow> Responses)
{
    /// <summary>Bir araştırma sorusunun (RQ3/RQ4) Likert maddeleri.</summary>
    public IReadOnlyList<LikertSummary> LikertFor(SurveyItemGroup group) =>
        Likert.Where(l => SurveyQuestionnaire.LikertItems.Any(i => i.Code == l.ItemCode && i.Group == group)).ToList();
}

/// <summary>Uygulama içi SUS + RQ3/RQ4 Likert anketi (Faz 9, D30). Kişisel veri saklanmaz; puan C#'ta hesaplanır.</summary>
public sealed class SurveyService(IAppDbContext db, TimeProvider clock)
{
    public async Task<SurveyResponseRow> SubmitAsync(SurveySubmission submission, CancellationToken ct)
    {
        var code = SurveyQuestionnaire.NormalizeParticipantCode(submission.ParticipantCode);
        if (!SurveyQuestionnaire.IsValidParticipantCode(code))
            throw new BusinessRuleException(
                $"Katılımcı kodu P ve 2–3 rakamdan oluşmalı (ör. {SurveyQuestionnaire.ParticipantCodeExample}). Ad veya e-posta yazmayın.");
        if (!submission.ConsentGiven)
            throw new BusinessRuleException("Devam etmek için bilgilendirilmiş onam metnini onaylamanız gerekir.");

        var unknown = submission.Answers.Keys.Where(k => SurveyQuestionnaire.AllItems.All(i => i.Code != k)).ToList();
        if (unknown.Count > 0)
            throw new BusinessRuleException($"Bilinmeyen anket maddesi: {string.Join(", ", unknown)}.");

        var missing = SurveyQuestionnaire.AllItems
            .Where(i => submission.Answers.GetValueOrDefault(i.Code) is not { } v || v < SusScore.MinAnswer || v > SusScore.MaxAnswer)
            .Select(i => i.Code).ToList();
        if (missing.Count > 0)
            throw new BusinessRuleException($"Şu maddeler cevaplanmadı: {string.Join(", ", missing)}.");

        if (await db.SurveyResponses.AnyAsync(r => r.ParticipantCode == code, ct))
            throw new BusinessRuleException($"{code} kodlu katılımcının cevabı zaten kayıtlı.");

        var answers = SurveyQuestionnaire.AllItems.ToDictionary(i => i.Code, i => submission.Answers[i.Code]!.Value);
        var score = SusScore.Compute(SurveyQuestionnaire.SusItems.Select(i => answers[i.Code]).ToList());
        var response = new SurveyResponse
        {
            ParticipantCode = code,
            QuestionnaireVersion = SurveyQuestionnaire.Version,
            ConsentGiven = true,
            SubmittedAt = clock.GetUtcNow().UtcDateTime,
            SusScore = score,
            Answers = answers.Select(a => new SurveyAnswer { ItemCode = a.Key, Value = a.Value }).ToList()
        };
        db.SurveyResponses.Add(response);
        await db.SaveChangesAsync(ct);
        return Row(response);
    }

    /// <summary>Katılımcı çekilirse veya deneme kaydıysa cevabı siler.</summary>
    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var response = await db.SurveyResponses.FirstOrDefaultAsync(r => r.Id == id, ct)
                       ?? throw new NotFoundException("Anket cevabı", id);
        db.SurveyResponses.Remove(response);
        await db.SaveChangesAsync(ct);
    }

    public async Task<SurveySummary> GetSummaryAsync(CancellationToken ct)
    {
        var responses = await LoadAsync(ct);
        var sus = DescriptiveStats.Of(responses.Select(r => r.SusScore));
        var likert = SurveyQuestionnaire.LikertItems
            .Select(i => LikertSummary.Of(i.Code, i.Text,
                responses.SelectMany(r => r.Answers).Where(a => a.ItemCode == i.Code).Select(a => a.Value)))
            .ToList();
        return new SurveySummary(sus, sus.Mean is { } m ? SusScore.Adjective(m) : null, likert, responses.Select(Row).ToList());
    }

    internal async Task<List<SurveyResponse>> LoadAsync(CancellationToken ct) =>
        await db.SurveyResponses.AsNoTracking().Include(r => r.Answers).OrderBy(r => r.Id).ToListAsync(ct);

    private static SurveyResponseRow Row(SurveyResponse r) =>
        new(r.Id, r.ParticipantCode, r.SubmittedAt, r.SusScore, SusScore.Adjective(r.SusScore));
}
