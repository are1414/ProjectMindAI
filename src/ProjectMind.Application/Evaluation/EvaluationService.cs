using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectMind.Application.Abstractions;
using ProjectMind.Application.Ai;
using ProjectMind.Application.Common;
using ProjectMind.Application.Ml;
using ProjectMind.Application.WhatIf;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.Evaluation;

public enum EvaluationExport
{
    /// <summary>RQ1: snapshot zaman serisi, EVM (Earned Schedule) ve ML tahmini yan yana.</summary>
    Rq1Timeline,

    /// <summary>RQ2: son deneyin permütasyon özellik önemi.</summary>
    Rq2Importance,

    /// <summary>RQ3: what-if çalıştırmaları (senaryo başına bir satır).</summary>
    Rq3WhatIfRuns,

    /// <summary>RQ4: öneri kartları kaynak × araç × durum × toplu karar sayıları.</summary>
    Rq4Acceptance,

    /// <summary>RQ4 + sağlayıcı: AI denetim kaydı (NumberGuard, şema, süre, yorum geri bildirimi). Tam bağlam/yorum metni yok.</summary>
    Rq4AiLog,

    /// <summary>SUS + RQ3/RQ4 Likert anket cevapları (katılımcı başına bir satır).</summary>
    Survey
}

public sealed record Rq1ProjectRow(
    int ProjectId, string ProjectName, int Snapshots, int WithMlPrediction, DateOnly? FirstDate, DateOnly? LastDate,
    decimal? LastSpiTime, decimal? LastDelayProbability, DateOnly? LastEvmForecast, DateOnly? LastMlForecast);

public sealed record Rq3Summary(int Runs, int PanelRuns, int AiToolRuns, int Projects, int Scenarios, DescriptiveStats DurationMs);

public sealed record Rq4Summary(
    IReadOnlyList<SourceAcceptance> Acceptance, int Comments, int ValidComments, int CommentsWithUnverified,
    int Rated, int RatedHelpful, DescriptiveStats Clarity)
{
    /// <summary>"Anlaşılır mıydı?" sorusuna Evet payı (puanlanan yorumlar içinde).</summary>
    public decimal? HelpfulShare => Rated == 0 ? null : (decimal)RatedHelpful / Rated;

    /// <summary>Şemaya uygun yorumlarda doğrulanamayan sayı içeren yorum payı.</summary>
    public decimal? UnverifiedShare => ValidComments == 0 ? null : (decimal)CommentsWithUnverified / ValidComments;
}

public sealed record EvaluationSummary(
    IReadOnlyList<Rq1ProjectRow> Rq1, DelayExperimentReport? Rq2, Rq3Summary Rq3, Rq4Summary Rq4, SurveySummary Survey);

/// <summary>
/// Değerlendirme sayfası (Faz 9, D30): RQ1–RQ4 ve anket için mevcut kayıtların özeti ve CSV dışa aktarımları.
/// Tüm sayılar C#'ta hesaplanır; CSV'ler kültürden bağımsızdır ve API anahtarı, tam bağlam veya yorum metni içermez.
/// </summary>
public sealed class EvaluationService(IAppDbContext db, IDelayPredictor predictor, SurveyService surveys, TimeProvider clock)
{
    public static readonly IReadOnlyList<string> Rq1Header =
    [
        "project_id", "project", "date", "baseline_id", "percent_complete", "spi", "spi_t", "cpi", "health_score",
        "evm_forecast", "ml_probability", "ml_forecast"
    ];

    public static readonly IReadOnlyList<string> Rq2Header =
        ["feature", "auc_drop", "classifier", "dataset_projects", "dataset_seed", "created_at"];

    public static readonly IReadOnlyList<string> Rq3Header =
    [
        "run_id", "created_at", "project_id", "source", "iterations", "seed", "duration_ms", "scenario", "is_current",
        "change_count", "changes", "plan_finish", "p50_finish", "p80_finish", "target_date", "on_time_probability",
        "p80_delta_workdays"
    ];

    public static readonly IReadOnlyList<string> Rq4AcceptanceHeader = ["source", "tool", "status", "decided_in_bulk", "count"];

    public static readonly IReadOnlyList<string> Rq4AiLogHeader =
    [
        "log_id", "created_at", "kind", "project_id", "model", "prompt_version", "tools_called", "context_length",
        "outcome", "schema_valid", "unverified_count", "unverified_numbers", "duration_ms", "helpful", "clarity_rating"
    ];

    public static IReadOnlyList<string> SurveyHeader =>
    [
        "response_id", "participant", "submitted_at", "questionnaire_version", "consent",
        .. SurveyQuestionnaire.SusItems.Select(i => i.Code.ToLowerInvariant()), "sus_score", "sus_adjective",
        .. SurveyQuestionnaire.LikertItems.Select(i => i.Code.ToLowerInvariant())
    ];

    public async Task<EvaluationSummary> GetSummaryAsync(CancellationToken ct)
    {
        // RQ1: her projenin snapshot zaman serisi (EVM + ML) özeti.
        var snapshots = await db.ProjectSnapshots.AsNoTracking().ToListAsync(ct);
        var names = await db.Projects.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var rq1 = snapshots.GroupBy(s => s.ProjectId)
            .Select(g =>
            {
                var ordered = g.OrderBy(s => s.Date).ToList();
                var last = ordered[^1];
                return new Rq1ProjectRow(g.Key, names.GetValueOrDefault(g.Key, "?"), ordered.Count,
                    ordered.Count(s => s.DelayProbability is not null), ordered[0].Date, last.Date, last.SpiTime,
                    last.DelayProbability, last.ForecastFinish, last.MlForecastFinish);
            })
            .OrderBy(r => r.ProjectId).ToList();

        // RQ3: what-if çalıştırmaları.
        var runs = await db.WhatIfRunLogs.AsNoTracking()
            .Select(r => new { r.ProjectId, r.Source, r.ScenarioCount, r.DurationMs }).ToListAsync(ct);
        var rq3 = new Rq3Summary(runs.Count, runs.Count(r => r.Source == WhatIfRunSource.Panel),
            runs.Count(r => r.Source == WhatIfRunSource.AiTool), runs.Select(r => r.ProjectId).Distinct().Count(),
            runs.Sum(r => r.ScenarioCount), DescriptiveStats.Of(runs.Select(r => (decimal)r.DurationMs)));

        // RQ4: kaynağa göre kabul + yorum kartları (NumberGuard, anlaşılırlık).
        var acceptance = await db.AiActions.AsNoTracking().Select(a => new { a.Source, a.Status, a.DecidedInBulk }).ToListAsync(ct);
        var comments = await db.AiAnalysisLogs.AsNoTracking()
            .Where(l => l.Kind == AiAnalysisKind.ProjectComment || l.Kind == AiAnalysisKind.ScenarioComment)
            .Select(l => new { l.Outcome, l.UnverifiedNumberCount, l.Helpful, l.ClarityRating }).ToListAsync(ct);
        var valid = comments.Where(c => c.Outcome == AiAnalysisOutcome.Success).ToList();
        var rated = valid.Where(c => c.Helpful is not null).ToList();
        var rq4 = new Rq4Summary(SourceAcceptance.Compute(acceptance.Select(a => (a.Source, a.Status, a.DecidedInBulk))),
            comments.Count, valid.Count, valid.Count(c => c.UnverifiedNumberCount > 0), rated.Count,
            rated.Count(c => c.Helpful == true),
            DescriptiveStats.Of(valid.Where(c => c.ClarityRating is not null).Select(c => (decimal)c.ClarityRating!.Value)));

        return new EvaluationSummary(rq1, predictor.LastReport, rq3, rq4, await surveys.GetSummaryAsync(ct));
    }

    public async Task<CsvFile> ExportAsync(EvaluationExport export, CancellationToken ct)
    {
        var (name, content) = export switch
        {
            EvaluationExport.Rq1Timeline => ("rq1-evm-ml-zaman-serisi", await Rq1Async(ct)),
            EvaluationExport.Rq2Importance => ("rq2-ozellik-onemi", Rq2()),
            EvaluationExport.Rq3WhatIfRuns => ("rq3-whatif-calistirmalari", await Rq3Async(ct)),
            EvaluationExport.Rq4Acceptance => ("rq4-oneri-kabul", await Rq4AcceptanceAsync(ct)),
            EvaluationExport.Rq4AiLog => ("rq4-ai-kayitlari", await Rq4AiLogAsync(ct)),
            EvaluationExport.Survey => ("anket-sus-likert", await SurveyAsync(ct)),
            _ => throw new BusinessRuleException($"Bilinmeyen dışa aktarım: {export}")
        };
        return new CsvFile($"{name}-{clock.GetUtcNow():yyyyMMdd-HHmm}.csv", content);
    }

    private async Task<string> Rq1Async(CancellationToken ct)
    {
        var snapshots = await db.ProjectSnapshots.AsNoTracking().ToListAsync(ct);
        var names = await db.Projects.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        return Csv.Build(Rq1Header, snapshots.OrderBy(s => s.ProjectId).ThenBy(s => s.Date).Select(s => (IReadOnlyList<string>)
        [
            Csv.Int(s.ProjectId), names.GetValueOrDefault(s.ProjectId, ""), Csv.Date(s.Date), Csv.Int(s.BaselineId),
            Csv.Num(s.PercentComplete), Csv.Num(s.Spi), Csv.Num(s.SpiTime), Csv.Num(s.Cpi), Csv.Num(s.HealthScore),
            Csv.Date(s.ForecastFinish), Csv.Num(s.DelayProbability), Csv.Date(s.MlForecastFinish)
        ]));
    }

    private string Rq2()
    {
        var report = predictor.LastReport
                     ?? throw new BusinessRuleException("Özellik önemi için önce Deneyler sayfasında deneyi çalıştırın.");
        return Csv.Build(Rq2Header, report.Importance.Select(f => (IReadOnlyList<string>)
        [
            f.Feature, Csv.Num(f.AucDrop, "0.000000"), report.SelectedClassifier, Csv.Int(report.Synthetic.Projects),
            Csv.Int(report.Synthetic.Seed), Csv.Time(report.CreatedAt.UtcDateTime)
        ]));
    }

    private async Task<string> Rq3Async(CancellationToken ct)
    {
        var runs = await db.WhatIfRunLogs.AsNoTracking().OrderBy(r => r.Id).ToListAsync(ct);
        var rows = new List<IReadOnlyList<string>>();
        foreach (var run in runs)
        {
            var scenarios = JsonSerializer.Deserialize<List<WhatIfRunScenario>>(run.ResultsJson, AiJson.Options) ?? [];
            rows.AddRange(scenarios.Select(s => (IReadOnlyList<string>)
            [
                Csv.Int(run.Id), Csv.Time(run.CreatedAt), Csv.Int(run.ProjectId), run.Source.ToString(), Csv.Int(run.Iterations),
                Csv.Int(run.Seed), Csv.Int(run.DurationMs), s.Name, Csv.Bool(s.IsCurrent), Csv.Int(s.Changes.Count),
                string.Join("; ", s.Changes), Csv.Date(s.PlanFinish), Csv.Date(s.P50Finish), Csv.Date(s.P80Finish),
                Csv.Date(s.TargetDate), Csv.Num(s.OnTimeProbability), Csv.Int(s.P80DeltaWorkdays)
            ]));
        }

        return Csv.Build(Rq3Header, rows);
    }

    private async Task<string> Rq4AcceptanceAsync(CancellationToken ct)
    {
        var actions = await db.AiActions.AsNoTracking()
            .Select(a => new { a.Source, a.ToolName, a.Status, a.DecidedInBulk }).ToListAsync(ct);
        var groups = actions.GroupBy(a => (a.Source, a.ToolName, a.Status, a.DecidedInBulk))
            .OrderBy(g => g.Key.Source).ThenBy(g => g.Key.ToolName, StringComparer.Ordinal).ThenBy(g => g.Key.Status)
            .ThenBy(g => g.Key.DecidedInBulk);
        return Csv.Build(Rq4AcceptanceHeader, groups.Select(g => (IReadOnlyList<string>)
        [
            g.Key.Source.ToString(), g.Key.ToolName, g.Key.Status.ToString(), Csv.Bool(g.Key.DecidedInBulk), Csv.Int(g.Count())
        ]));
    }

    private async Task<string> Rq4AiLogAsync(CancellationToken ct)
    {
        // Bilerek okunmayan alanlar: ResultJson (yorum metni), ErrorMessage, InputHash. Tam bağlam zaten saklanmaz.
        var logs = await db.AiAnalysisLogs.AsNoTracking().OrderBy(l => l.Id)
            .Select(l => new
            {
                l.Id, l.CreatedAt, l.Kind, l.ProjectId, l.Model, l.PromptVersion, l.ToolsCalled, l.ContextLength, l.Outcome,
                l.SchemaValid, l.UnverifiedNumberCount, l.UnverifiedNumbers, l.DurationMs, l.Helpful, l.ClarityRating
            }).ToListAsync(ct);
        return Csv.Build(Rq4AiLogHeader, logs.Select(l => (IReadOnlyList<string>)
        [
            Csv.Int(l.Id), Csv.Time(l.CreatedAt), l.Kind.ToString(), Csv.Int(l.ProjectId), l.Model ?? "", l.PromptVersion,
            l.ToolsCalled ?? "", Csv.Int(l.ContextLength), l.Outcome.ToString(), Csv.Bool(l.SchemaValid),
            Csv.Int(l.UnverifiedNumberCount), UnverifiedList(l.UnverifiedNumbers), Csv.Int(l.DurationMs), Csv.Bool(l.Helpful),
            Csv.Int(l.ClarityRating)
        ]));
    }

    /// <summary>Kayıttaki JSON listesini "a | b" biçimine çevirir (tablo programında tek hücre).</summary>
    private static string UnverifiedList(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return "";
        try
        {
            return string.Join(" | ", JsonSerializer.Deserialize<List<string>>(json) ?? []);
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private async Task<string> SurveyAsync(CancellationToken ct)
    {
        var responses = await surveys.LoadAsync(ct);
        return Csv.Build(SurveyHeader, responses.Select(r =>
        {
            var answers = r.Answers.ToDictionary(a => a.ItemCode, a => a.Value);
            string Answer(SurveyItem i) => answers.TryGetValue(i.Code, out var v) ? Csv.Int(v) : "";
            return (IReadOnlyList<string>)
            [
                Csv.Int(r.Id), r.ParticipantCode, Csv.Time(r.SubmittedAt), r.QuestionnaireVersion, Csv.Bool(r.ConsentGiven),
                .. SurveyQuestionnaire.SusItems.Select(Answer), Csv.Num(r.SusScore, "0.0"), SusScore.Adjective(r.SusScore),
                .. SurveyQuestionnaire.LikertItems.Select(Answer)
            ];
        }));
    }
}
