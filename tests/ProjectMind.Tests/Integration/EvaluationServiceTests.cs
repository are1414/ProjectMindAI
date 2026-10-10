using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectMind.Application.Ai;
using ProjectMind.Application.Common;
using ProjectMind.Application.Evaluation;
using ProjectMind.Application.Ml;
using ProjectMind.Application.People;
using ProjectMind.Application.Projects;
using ProjectMind.Application.WhatIf;
using ProjectMind.Application.WorkItems;
using ProjectMind.Domain.Entities;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Tests.Integration;

/// <summary>Faz 9 değerlendirme altyapısı: anket kaydı, what-if çalıştırma kaydı, özet ve RQ CSV'leri (D30).</summary>
public class EvaluationServiceTests : ServiceTestBase
{
    private readonly CancellationToken _ct = CancellationToken.None;
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private SurveyService NewSurveyService() => new(Db, new FixedClock(Now));

    private EvaluationService NewEvaluationService() => new(Db, Predictor, NewSurveyService(), new FixedClock(Now));

    private static Dictionary<string, int?> Answers(int[] sus, int rq3a = 4, int rq3b = 5, int rq4a = 3, int rq4b = 4)
    {
        var answers = SurveyQuestionnaire.SusItems.Select((item, i) => (item.Code, Value: (int?)sus[i]))
            .ToDictionary(x => x.Code, x => x.Value);
        answers["RQ3_1"] = rq3a;
        answers["RQ3_2"] = rq3b;
        answers["RQ4_1"] = rq4a;
        answers["RQ4_2"] = rq4b;
        return answers;
    }

    /// <summary>WhatIfServiceTests ile aynı: tek backendci, üç bağımsız 40 saatlik iş, hedef 20 Kasım.</summary>
    private async Task<int> SeedProjectAsync()
    {
        var project = await new ProjectService(Db).CreateAsync(new ProjectRequest
        {
            Name = "Değerlendirme, deneme", Type = ProjectType.WebApplication, StartDate = new DateOnly(2026, 11, 2),
            TargetEndDate = new DateOnly(2026, 11, 20), Currency = "TRY", HoursPerDay = 8
        }, _ct);
        await new PersonService(Db).CreateAsync(project.Id,
            new PersonRequest { Name = "Ayşe", Skills = [Skill.Backend], WeeklyCapacityHours = 40, HourlyCost = 100 }, _ct);
        var items = new WorkItemService(Db);
        foreach (var name in new[] { "Ödeme", "Kampanya", "Raporlama" })
            await items.CreateAsync(project.Id, new WorkItemRequest { Name = name, RequiredSkill = Skill.Backend, EstimatedHours = 40 }, _ct);
        Db.ChangeTracker.Clear();
        return project.Id;
    }

    private static List<string> Lines(CsvFile file)
    {
        Assert.Equal(Csv.Bom, file.Content[0]);
        Assert.EndsWith(".csv", file.FileName);
        return file.Content[1..].Split(Csv.NewLine).Where(l => l.Length > 0).ToList();
    }

    // ---------- Anket ----------

    [Fact]
    public async Task Survey_is_stored_with_code_consent_and_computed_sus_score()
    {
        var row = await NewSurveyService().SubmitAsync(new SurveySubmission(" p01 ", true, Answers([4, 2, 5, 1, 4, 2, 4, 2, 3, 3])), _ct);
        Db.ChangeTracker.Clear();

        Assert.Equal("P01", row.ParticipantCode);
        Assert.Equal(75m, row.SusScore);
        Assert.Equal("İyi", row.Adjective);
        var saved = await Db.SurveyResponses.Include(r => r.Answers).SingleAsync(_ct);
        Assert.True(saved.ConsentGiven);
        Assert.Equal(Now.UtcDateTime, saved.SubmittedAt);
        Assert.Equal(SurveyQuestionnaire.Version, saved.QuestionnaireVersion);
        Assert.Equal(14, saved.Answers.Count);
        Assert.Equal(5, saved.Answers.Single(a => a.ItemCode == "RQ3_2").Value);
    }

    [Fact]
    public async Task Survey_rejects_missing_items_missing_consent_bad_code_and_duplicates()
    {
        var service = NewSurveyService();
        var incomplete = Answers([3, 3, 3, 3, 3, 3, 3, 3, 3, 3]);
        incomplete["SUS4"] = null;
        incomplete.Remove("RQ4_2");
        var missing = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SubmitAsync(new SurveySubmission("P01", true, incomplete), _ct));
        Assert.Equal("Şu maddeler cevaplanmadı: SUS4, RQ4_2.", missing.Message);

        var outOfRange = Answers([3, 3, 3, 3, 3, 3, 3, 3, 3, 3]);
        outOfRange["RQ3_1"] = 6;
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SubmitAsync(new SurveySubmission("P01", true, outOfRange), _ct));

        var consent = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SubmitAsync(new SurveySubmission("P01", false, Answers([3, 3, 3, 3, 3, 3, 3, 3, 3, 3])), _ct));
        Assert.Contains("onam", consent.Message);

        var name = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SubmitAsync(new SurveySubmission("Ayşe Yılmaz", true, Answers([3, 3, 3, 3, 3, 3, 3, 3, 3, 3])), _ct));
        Assert.Contains("Katılımcı kodu", name.Message);

        var unknown = Answers([3, 3, 3, 3, 3, 3, 3, 3, 3, 3]);
        unknown["SUS11"] = 3;
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SubmitAsync(new SurveySubmission("P01", true, unknown), _ct));

        await service.SubmitAsync(new SurveySubmission("P01", true, Answers([3, 3, 3, 3, 3, 3, 3, 3, 3, 3])), _ct);
        var duplicate = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SubmitAsync(new SurveySubmission("p01", true, Answers([5, 1, 5, 1, 5, 1, 5, 1, 5, 1])), _ct));
        Assert.Contains("zaten kayıtlı", duplicate.Message);
        Assert.Equal(1, await Db.SurveyResponses.CountAsync(_ct));
    }

    [Fact]
    public async Task Survey_summary_gives_sus_stats_adjective_and_likert_per_rq()
    {
        var service = NewSurveyService();
        await service.SubmitAsync(new SurveySubmission("P01", true, Answers([4, 2, 5, 1, 4, 2, 4, 2, 3, 3], rq3a: 5, rq4a: 2)), _ct); // 75
        await service.SubmitAsync(new SurveySubmission("P02", true, Answers([3, 4, 2, 3, 3, 4, 2, 3, 3, 4], rq3a: 4, rq4a: 4)), _ct); // 37,5
        await service.SubmitAsync(new SurveySubmission("P03", true, Answers([3, 3, 3, 3, 3, 3, 3, 3, 3, 3], rq3a: 2, rq4a: 5)), _ct); // 50

        var summary = await service.GetSummaryAsync(_ct);

        // (75 + 37,5 + 50) / 3 = 54,1667 → "Fena değil (OK)"; SS: sapmalar 20,833 / −16,667 / −4,167 → √(729,1667/2) ≈ 19,0941.
        Assert.Equal(3, summary.Sus.N);
        Assert.Equal(54.1667m, Math.Round(summary.Sus.Mean!.Value, 4));
        Assert.Equal(19.0941m, Math.Round(summary.Sus.StdDev!.Value, 4));
        Assert.Equal(37.5m, summary.Sus.Min);
        Assert.Equal(75m, summary.Sus.Max);
        Assert.Equal("Fena değil (OK)", summary.MeanAdjective);

        var rq3 = summary.LikertFor(SurveyItemGroup.Rq3);
        Assert.Equal(["RQ3_1", "RQ3_2"], rq3.Select(l => l.ItemCode));
        Assert.Equal(11m / 3, rq3[0].Stats.Mean);                 // 5, 4, 2
        Assert.Equal([0, 1, 0, 1, 1], rq3[0].Distribution);
        Assert.Equal(2m / 3, rq3[0].AgreeShare);
        Assert.Equal(["RQ4_1", "RQ4_2"], summary.LikertFor(SurveyItemGroup.Rq4).Select(l => l.ItemCode));

        await service.DeleteAsync(summary.Responses[0].Id, _ct);
        Assert.Equal(2, (await service.GetSummaryAsync(_ct)).Sus.N);
        Assert.Equal(0, await Db.SurveyAnswers.CountAsync(a => a.SurveyResponseId == summary.Responses[0].Id, _ct));
    }

    // ---------- What-if çalıştırma kaydı ----------

    [Fact]
    public async Task What_if_comparison_writes_one_run_log_matching_the_returned_results()
    {
        var projectId = await SeedProjectAsync();
        var raporlama = Db.WorkItems.Single(w => w.Name == "Raporlama").Id;

        var result = await NewWhatIfService().CompareAsync(projectId,
        [
            new WhatIfScenario("Kapsam azalt", [new ScenarioChange(ScenarioChangeKind.RemoveWorkItem, WorkItemId: raporlama)]),
            new WhatIfScenario("1 kişi ekle", [new ScenarioChange(ScenarioChangeKind.AddPerson, Skills: [Skill.Backend], WeeklyHours: 40, Count: 1)])
        ], _ct);
        Db.ChangeTracker.Clear();

        var log = await Db.WhatIfRunLogs.SingleAsync(_ct);
        Assert.Equal(projectId, log.ProjectId);
        Assert.Equal(WhatIfRunSource.Panel, log.Source);
        Assert.Equal(2, log.ScenarioCount);
        Assert.Equal(100, log.Iterations);
        Assert.True(log.DurationMs >= 0);

        var rows = JsonSerializer.Deserialize<List<WhatIfRunScenario>>(log.ResultsJson, AiJson.Options)!;
        Assert.Equal(3, rows.Count);
        Assert.True(rows[0].IsCurrent);
        Assert.Equal(WhatIfService.CurrentPlanName, rows[0].Name);
        var all = new[] { result.Current }.Concat(result.Scenarios).ToList();
        for (var i = 0; i < all.Count; i++)
        {
            Assert.Equal(all[i].Name, rows[i].Name);
            Assert.Equal(all[i].MonteCarlo.P50Finish, rows[i].P50Finish);
            Assert.Equal(all[i].MonteCarlo.P80Finish, rows[i].P80Finish);
            Assert.Equal(all[i].MonteCarlo.OnTimeProbability, rows[i].OnTimeProbability);
            Assert.Equal(all[i].P80DeltaWorkdays, rows[i].P80DeltaWorkdays);
        }
        Assert.Equal([$"RemoveWorkItem workItemId={raporlama}"], rows[1].Changes);
        Assert.Equal(["AddPerson count=1 skills=Backend weeklyHours=40"], rows[2].Changes);
    }

    [Fact]
    public async Task Ai_tool_what_if_run_is_logged_with_its_source()
    {
        var projectId = await SeedProjectAsync();
        var session = new ChatSession { Title = "t", ProjectId = projectId };
        Db.ChatSessions.Add(session);
        await Db.SaveChangesAsync(_ct);

        var handler = new ReadOnlyToolHandler(Db, null!, NewScheduleService(), NewStatusService(), NewWhatIfService());
        var input = JsonSerializer.SerializeToElement(new { scenarioName = "Süre uzat", newDeadline = "2026-12-31" });
        var result = await handler.ExecuteAsync(session.Id, AiTools.SimulateWhatIf, input, _ct);

        Assert.False(result.IsError);
        var log = await Db.WhatIfRunLogs.SingleAsync(_ct);
        Assert.Equal(WhatIfRunSource.AiTool, log.Source);
        Assert.Equal(1, log.ScenarioCount);
    }

    [Fact]
    public async Task Failed_what_if_is_not_logged()
    {
        var projectId = await SeedProjectAsync();
        var tooMany = Enumerable.Range(1, new WhatIfOptions().MaxScenarios + 1)
            .Select(i => new WhatIfScenario($"S{i}", [])).ToList();
        await Assert.ThrowsAsync<BusinessRuleException>(() => NewWhatIfService().CompareAsync(projectId, tooMany, _ct));
        Assert.Equal(0, await Db.WhatIfRunLogs.CountAsync(_ct));
    }

    // ---------- Özet ve CSV'ler (küçük, elle kurulmuş veritabanı) ----------

    /// <summary>
    /// 1 proje: 3 snapshot (2'si ML tahminli), 1 what-if çalıştırması (mevcut + 1 senaryo), 6 öneri kartı,
    /// 4 AI kaydı (2 başarılı yorum, 1 şema dışı yorum, 1 sohbet), 2 anket cevabı.
    /// </summary>
    private async Task<int> SeedEvaluationDbAsync()
    {
        var projectId = await SeedProjectAsync();
        Db.ProjectSnapshots.AddRange(
            new ProjectSnapshot { ProjectId = projectId, Date = new DateOnly(2026, 11, 13), Spi = 0.9m, SpiTime = 0.875m, Cpi = 1m, PercentComplete = 30m },
            new ProjectSnapshot { ProjectId = projectId, Date = new DateOnly(2026, 11, 6), Spi = 1m, SpiTime = 1m, PercentComplete = 10m },
            new ProjectSnapshot
            {
                ProjectId = projectId, Date = new DateOnly(2026, 11, 20), Spi = 0.85m, SpiTime = 0.8m, PercentComplete = 60m,
                ForecastFinish = new DateOnly(2026, 11, 27), DelayProbability = 0.72m, MlForecastFinish = new DateOnly(2026, 12, 1)
            });
        Db.ProjectSnapshots.Local.First(s => s.Date == new DateOnly(2026, 11, 13)).DelayProbability = 0.4m;

        Db.AiActions.AddRange(
            Action(AiActionSource.Rule, AiTools.AddWorkItem, AiActionStatus.Applied),
            Action(AiActionSource.Rule, AiTools.AddWorkItem, AiActionStatus.Applied),
            Action(AiActionSource.Rule, AiTools.AddWorkItem, AiActionStatus.Rejected),
            Action(AiActionSource.Llm, AiTools.AddWorkItem, AiActionStatus.Applied, inBulk: true),
            Action(AiActionSource.Llm, AiTools.AddWorkItem, AiActionStatus.Pending),
            Action(AiActionSource.User, AiTools.AddDependency, AiActionStatus.Applied));

        Db.AiAnalysisLogs.AddRange(
            Log(AiAnalysisKind.ProjectComment, AiAnalysisOutcome.Success, unverified: 0, helpful: true, clarity: 5),
            Log(AiAnalysisKind.ScenarioComment, AiAnalysisOutcome.Success, unverified: 2, helpful: false, clarity: 2),
            Log(AiAnalysisKind.ProjectComment, AiAnalysisOutcome.InvalidSchema, unverified: 0),
            Log(AiAnalysisKind.Chat, AiAnalysisOutcome.Success, unverified: 0));
        await Db.SaveChangesAsync(_ct);

        await NewWhatIfService().CompareAsync(projectId,
            [new WhatIfScenario("Süre uzat", [new ScenarioChange(ScenarioChangeKind.ChangeDeadline, Date: new DateOnly(2026, 12, 31))])], _ct);

        var surveys = NewSurveyService();
        await surveys.SubmitAsync(new SurveySubmission("P01", true, Answers([4, 2, 5, 1, 4, 2, 4, 2, 3, 3])), _ct);
        await surveys.SubmitAsync(new SurveySubmission("P02", true, Answers([3, 3, 3, 3, 3, 3, 3, 3, 3, 3])), _ct);
        Db.ChangeTracker.Clear();
        return projectId;

        static AiAction Action(AiActionSource source, string tool, AiActionStatus status, bool inBulk = false) => new()
        {
            ToolName = tool, PayloadJson = "{}", Summary = "s", Source = source, Status = status, DecidedInBulk = inBulk
        };

        AiAnalysisLog Log(AiAnalysisKind kind, AiAnalysisOutcome outcome, int unverified, bool? helpful = null, int? clarity = null) => new()
        {
            Kind = kind, ProjectId = projectId, PromptVersion = "comment-v1", Model = "gemini-test", ContextLength = 1234,
            Outcome = outcome, SchemaValid = outcome == AiAnalysisOutcome.Success, UnverifiedNumberCount = unverified,
            UnverifiedNumbers = unverified == 0 ? null : """["137","1,5"]""", DurationMs = 850, Helpful = helpful,
            ClarityRating = clarity, ResultJson = """{"summary":"gizli yorum metni"}""", ErrorMessage = "hata ayrıntısı",
            InputHash = "ABC"
        };
    }

    [Fact]
    public async Task Summary_aggregates_each_research_question()
    {
        await SeedEvaluationDbAsync();

        var s = await NewEvaluationService().GetSummaryAsync(_ct);

        var rq1 = Assert.Single(s.Rq1);
        Assert.Equal(3, rq1.Snapshots);
        Assert.Equal(2, rq1.WithMlPrediction);
        Assert.Equal(new DateOnly(2026, 11, 6), rq1.FirstDate);
        Assert.Equal(new DateOnly(2026, 11, 20), rq1.LastDate);
        Assert.Equal(0.8m, rq1.LastSpiTime);
        Assert.Equal(0.72m, rq1.LastDelayProbability);
        Assert.Null(s.Rq2);

        Assert.Equal(1, s.Rq3.Runs);
        Assert.Equal(1, s.Rq3.PanelRuns);
        Assert.Equal(0, s.Rq3.AiToolRuns);
        Assert.Equal(1, s.Rq3.Scenarios);

        // Kural: 2 uygulandı + 1 red → 2/3; LLM: 1 toplu uygulandı + 1 bekliyor → 1/1, tek tek oranı yok.
        var rule = s.Rq4.Acceptance.Single(a => a.Source == AiActionSource.Rule);
        Assert.Equal(2.0 / 3, rule.AcceptanceRate!.Value, 6);
        var llm = s.Rq4.Acceptance.Single(a => a.Source == AiActionSource.Llm);
        Assert.Equal(1.0, llm.AcceptanceRate);
        Assert.Null(llm.IndividualAcceptanceRate);
        // Yorumlar: 3 çağrı (sohbet hariç), 2 geçerli, 1'i doğrulanamayan sayılı; 2 puan, 1 Evet; anlaşılırlık (5 + 2) / 2.
        Assert.Equal(3, s.Rq4.Comments);
        Assert.Equal(2, s.Rq4.ValidComments);
        Assert.Equal(1, s.Rq4.CommentsWithUnverified);
        Assert.Equal(0.5m, s.Rq4.UnverifiedShare);
        Assert.Equal(2, s.Rq4.Rated);
        Assert.Equal(0.5m, s.Rq4.HelpfulShare);
        Assert.Equal(3.5m, s.Rq4.Clarity.Mean);

        // SUS: 75 ve 50 → 62,5.
        Assert.Equal(2, s.Survey.Sus.N);
        Assert.Equal(62.5m, s.Survey.Sus.Mean);
    }

    [Fact]
    public async Task Csv_exports_have_fixed_headers_and_one_row_per_record_in_invariant_culture()
    {
        var projectId = await SeedEvaluationDbAsync();
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");   // ondalık virgül kültüründe bile nokta yazılmalı
        try
        {
            var service = NewEvaluationService();

            var rq1 = Lines(await service.ExportAsync(EvaluationExport.Rq1Timeline, _ct));
            Assert.Equal(string.Join(',', EvaluationService.Rq1Header), rq1[0]);
            Assert.Equal(1 + await Db.ProjectSnapshots.CountAsync(_ct), rq1.Count);
            Assert.StartsWith($"{projectId},\"Değerlendirme, deneme\",2026-11-06,", rq1[1]);   // tarih sırasıyla, virgüllü ad tırnakta
            Assert.Equal($"{projectId},\"Değerlendirme, deneme\",2026-11-20,,60,0.85,0.8,,,2026-11-27,0.72,2026-12-01", rq1[3]);

            var rq3 = Lines(await service.ExportAsync(EvaluationExport.Rq3WhatIfRuns, _ct));
            Assert.Equal(string.Join(',', EvaluationService.Rq3Header), rq3[0]);
            Assert.Equal(3, rq3.Count);                                       // mevcut plan + 1 senaryo
            Assert.Contains(",Panel,100,7,", rq3[1]);
            Assert.Contains(",Mevcut plan,1,0,,", rq3[1]);
            Assert.Contains(",Süre uzat,0,1,ChangeDeadline date=2026-12-31,", rq3[2]);
            Assert.EndsWith(",2026-12-31,1,0", rq3[2]);                      // hedef, olasılık 1, P80 farkı 0

            var acceptance = Lines(await service.ExportAsync(EvaluationExport.Rq4Acceptance, _ct));
            Assert.Equal("source,tool,status,decided_in_bulk,count", acceptance[0]);
            Assert.Equal(
            [
                "Rule,add_work_item,Applied,0,2", "Rule,add_work_item,Rejected,0,1",
                "Llm,add_work_item,Pending,0,1", "Llm,add_work_item,Applied,1,1", "User,add_dependency,Applied,0,1"
            ], acceptance.Skip(1));                                           // kaynak, araç, durum (enum sırası), toplu

            var log = Lines(await service.ExportAsync(EvaluationExport.Rq4AiLog, _ct));
            Assert.Equal(string.Join(',', EvaluationService.Rq4AiLogHeader), log[0]);
            Assert.Equal(1 + await Db.AiAnalysisLogs.CountAsync(_ct), log.Count);
            Assert.Contains(",ScenarioComment,", log[2]);
            Assert.EndsWith(",2,\"137 | 1,5\",850,0,2", log[2]);
            Assert.DoesNotContain(log, l => l.Contains("gizli yorum metni") || l.Contains("hata ayrıntısı") || l.Contains("ABC"));
            Assert.DoesNotContain(EvaluationService.Rq4AiLogHeader,
                h => h.Contains("key", StringComparison.OrdinalIgnoreCase) || h.Contains("result") || h == "context" || h.Contains("hash"));

            var survey = Lines(await service.ExportAsync(EvaluationExport.Survey, _ct));
            Assert.Equal(string.Join(',', EvaluationService.SurveyHeader), survey[0]);
            Assert.StartsWith("response_id,participant,submitted_at,questionnaire_version,consent,sus1,", survey[0]);
            Assert.Equal(3, survey.Count);
            Assert.EndsWith(",4,2,5,1,4,2,4,2,3,3,75.0,İyi,4,5,3,4", survey[1]);
            Assert.Contains(",P01,2026-10-10T12:00:00Z,survey-v1,1,", survey[1]);
            Assert.EndsWith(",50.0,Zayıf,4,5,3,4", survey[2]);   // 50 < 50,9 (OK eşiği)

            // RQ2: deney raporu yoksa Türkçe hata; varsa özellik başına bir satır.
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.ExportAsync(EvaluationExport.Rq2Importance, _ct));
            Predictor.LastReport = new DelayExperimentReport(new SyntheticOptions(Projects: 400, Seed: 42), 280, 120, 1000, 400, 0.3, [],
                "FastTree", [new FeatureImportance("SpiTime", 0.1234), new FeatureImportance("ScopeGrowth", 0.05)], Now);
            var rq2 = Lines(await service.ExportAsync(EvaluationExport.Rq2Importance, _ct));
            Assert.Equal(["feature,auc_drop,classifier,dataset_projects,dataset_seed,created_at",
                "SpiTime,0.123400,FastTree,400,42,2026-10-10T12:00:00Z", "ScopeGrowth,0.050000,FastTree,400,42,2026-10-10T12:00:00Z"], rq2);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public async Task Export_file_name_has_date_stamp()
    {
        var file = await NewEvaluationService().ExportAsync(EvaluationExport.Survey, _ct);
        Assert.Equal("anket-sus-likert-20261010-1200.csv", file.FileName);
        Assert.Single(Lines(file));   // yalnız başlık
    }
}
