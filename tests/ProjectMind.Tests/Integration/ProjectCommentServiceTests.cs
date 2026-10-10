using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using ProjectMind.Application.Ai;
using ProjectMind.Application.Analytics;
using ProjectMind.Application.People;
using ProjectMind.Application.Projects;
using ProjectMind.Application.WhatIf;
using ProjectMind.Application.WorkItems;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Tests.Integration;

public class ProjectCommentServiceTests : ServiceTestBase
{
    private readonly CancellationToken _ct = CancellationToken.None;
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly DateTimeOffset Nov4 = new(2026, 11, 4, 18, 0, 0, TimeSpan.Zero);

    /// <summary>ProjectStatusServiceTests ile aynı elle hesaplanan senaryo: 4 Kasım'da SPI(t) 0,92, EVM bitiş 09.11.2026.</summary>
    private async Task<ProjectStatusReport> SeedStatusAsync(DateTimeOffset at)
    {
        var project = await new ProjectService(Db).CreateAsync(new ProjectRequest
        {
            Name = "EVM", Type = ProjectType.WebApplication, Currency = "TRY", HoursPerDay = 8,
            StartDate = new DateOnly(2026, 11, 2), TargetEndDate = new DateOnly(2026, 11, 6)
        }, _ct);
        await new PersonService(Db).CreateAsync(project.Id,
            new PersonRequest { Name = "Ayşe", Skills = [Skill.Backend], WeeklyCapacityHours = 40, HourlyCost = 100 }, _ct);
        var items = new WorkItemService(Db);
        var a = await items.CreateAsync(project.Id, new WorkItemRequest { Name = "A", RequiredSkill = Skill.Backend, EstimatedHours = 16 }, _ct);
        var b = await items.CreateAsync(project.Id, new WorkItemRequest { Name = "B", RequiredSkill = Skill.Backend, EstimatedHours = 24 }, _ct);
        await new ProjectMind.Application.Dependencies.DependencyService(Db).CreateAsync(project.Id,
            new ProjectMind.Application.Dependencies.DependencyRequest { PredecessorId = a.Id, SuccessorId = b.Id }, _ct);
        Db.ChangeTracker.Clear();
        await NewScheduleService().ApplyAsync(project.Id, _ct);
        Db.ChangeTracker.Clear();

        var reqA = WorkItemRequest.From(await items.GetAsync(project.Id, a.Id, _ct));
        reqA.Status = WorkItemStatus.Done; reqA.PercentComplete = 100; reqA.ActualHours = 20;
        await items.UpdateAsync(project.Id, a.Id, reqA, _ct);
        var reqB = WorkItemRequest.From(await items.GetAsync(project.Id, b.Id, _ct));
        reqB.Status = WorkItemStatus.InProgress; reqB.PercentComplete = 25; reqB.ActualHours = 4;
        await items.UpdateAsync(project.Id, b.Id, reqB, _ct);
        Db.ChangeTracker.Clear();

        return await NewStatusService(at).GetAsync(project.Id, _ct);
    }

    private ProjectCommentService NewService(IChatModel model) => new(Db, model);

    private static JsonNode Input(ChatJsonRequest request)
    {
        const string open = "<analiz_verisi>", close = "</analiz_verisi>";
        var text = request.UserMessage;
        var start = text.IndexOf(open, StringComparison.Ordinal) + open.Length;
        return JsonNode.Parse(text[start..text.IndexOf(close, StringComparison.Ordinal)])!;
    }

    private static string Comment(string summary, string[] findings, string[] caveats) => JsonSerializer.Serialize(new
    {
        summary, keyFindings = findings, recommendedActions = new[] { "B işine destek verin." }, caveats
    });

    private static string Date(JsonNode? node) => DateOnly.Parse(node!.GetValue<string>(), CultureInfo.InvariantCulture).ToString("dd.MM.yyyy", Tr);

    /// <summary>Girdideki sayıları aynen aktaran "dürüst" model + isteğe bağlı uydurma sayı.</summary>
    private static ScriptedJsonModel StatusModel(string? fabricated = null) => new(request =>
    {
        var input = Input(request);
        var evm = input["evm"]!;
        return Comment(
            $"Takvim performansı SPI(t) {evm["spiTime"]!.GetValue<decimal>().ToString(Tr)}; tahmini bitiş {Date(evm["forecastFinish"])}, " +
            $"hedef {Date(input["targetEndDate"])}." + (fabricated is null ? "" : $" Kalan efor {fabricated} saat."),
            [$"ML gecikme olasılığı %{input["mlDelayPrediction"]!["delayProbabilityPercent"]}."],
            ["ML modeli sentetik veriyle eğitildi."]);
    });

    [Fact]
    public async Task Project_comment_uses_status_payload_and_flags_only_numbers_missing_from_it()
    {
        var status = await SeedStatusAsync(Nov4);
        var model = StatusModel(fabricated: "137");

        var result = await NewService(model).GenerateProjectCommentAsync(status, _ct);

        Assert.Equal(AnalysisCommentStatus.Ready, result.Status);
        Assert.Contains("SPI(t) 0,92", result.Comment!.Summary);
        Assert.Contains("09.11.2026", result.Comment.Summary);
        Assert.Equal(["ML gecikme olasılığı %70."], result.Comment.KeyFindings);
        Assert.Equal(["137"], result.UnverifiedNumbers);   // 0,92 · 09.11.2026 · 06.11.2026 · %70 girdide var

        // Girdi = get_project_status ile aynı deterministik JSON (tek kaynak); şema ve sürümlü prompt gönderilir.
        var request = model.Requests.Single();
        Assert.Equal(AnalysisCommentSchema.Name, request.SchemaName);
        Assert.Equal(ChatPrompts.ProjectCommentSystem, request.SystemPrompt);
        Assert.Contains(JsonSerializer.Serialize(AnalysisPayloads.ProjectStatus(status), AiJson.Options), request.UserMessage);

        var log = await Db.AiAnalysisLogs.SingleAsync(_ct);
        Assert.Equal(AiAnalysisKind.ProjectComment, log.Kind);
        Assert.Equal(AiAnalysisOutcome.Success, log.Outcome);
        Assert.True(log.SchemaValid);
        Assert.Equal(1, log.UnverifiedNumberCount);
        Assert.Equal(ChatPrompts.CommentVersion, log.PromptVersion);
        Assert.Equal("scripted-json", log.Model);
        Assert.Equal(status.Overview.Project.Id, log.ProjectId);
        Assert.True(log.ContextLength > 0);
        Assert.DoesNotContain("<analiz_verisi>", log.ResultJson);   // tam bağlam metni saklanmaz
    }

    [Fact]
    public async Task Clean_comment_has_no_unverified_numbers()
    {
        var status = await SeedStatusAsync(Nov4);

        var result = await NewService(StatusModel()).GenerateProjectCommentAsync(status, _ct);

        Assert.Empty(result.UnverifiedNumbers!);
    }

    [Fact]
    public async Task Last_comment_is_shown_again_only_while_input_is_unchanged()
    {
        var status = await SeedStatusAsync(Nov4);
        var service = NewService(StatusModel(fabricated: "137"));
        Assert.Null(await service.FindProjectCommentAsync(status, _ct));

        var generated = await service.GenerateProjectCommentAsync(status, _ct);
        var cached = await service.FindProjectCommentAsync(status, _ct);

        Assert.NotNull(cached);
        Assert.True(cached.FromCache);
        Assert.Equal(generated.Comment!.Summary, cached.Comment!.Summary);
        Assert.Equal(generated.Comment.KeyFindings, cached.Comment.KeyFindings);
        Assert.Equal(["137"], cached.UnverifiedNumbers);

        // Ertesi gün durum verisi (statusDate, EVM) değişir → eski yorum gösterilmez.
        var nextDay = await NewStatusService(Nov4.AddDays(1)).GetAsync(status.Overview.Project.Id, _ct);
        Assert.Null(await service.FindProjectCommentAsync(nextDay, _ct));
    }

    [Fact]
    public async Task Cached_comment_belongs_to_the_provider_that_generated_it()
    {
        // Tur 4a H6 (TEST_PAZAR Q10): Gemini'nin yorumu, sağlayıcı Claude'a geçince "kaydedilmiş yorum" olarak açılmaz.
        var status = await SeedStatusAsync(Nov4);
        await NewService(new ScriptedJsonModel(_ => Comment("Gemini yorumu", [], []), providerKey: "Gemini/g"))
            .GenerateProjectCommentAsync(status, _ct);

        Assert.NotNull(await NewService(new ScriptedJsonModel(_ => "{}", providerKey: "Gemini/g")).FindProjectCommentAsync(status, _ct));
        Assert.Null(await NewService(new ScriptedJsonModel(_ => "{}", providerKey: "Claude/c")).FindProjectCommentAsync(status, _ct));
        Assert.NotEqual(ProjectCommentService.InputHash(AiAnalysisKind.ProjectComment, "x", "Gemini/g"),
            ProjectCommentService.InputHash(AiAnalysisKind.ProjectComment, "x", "Claude/c"));
    }

    [Fact]
    public async Task Mock_mode_only_warns_and_does_not_call_model_or_log()
    {
        var status = await SeedStatusAsync(Nov4);
        var model = new ScriptedJsonModel(_ => throw new InvalidOperationException("çağrılmamalı"), configured: false);

        var result = await NewService(model).GenerateProjectCommentAsync(status, _ct);

        Assert.Equal(AnalysisCommentStatus.NotConfigured, result.Status);
        Assert.Null(result.Comment);
        Assert.Equal(ChatModelMessages.NotConfigured, result.Message);
        Assert.Empty(model.Requests);
        Assert.Empty(Db.AiAnalysisLogs);
    }

    [Theory]
    [InlineData("Proje iyi gidiyor, SPI 0,92.")]
    [InlineData("""{"summary":"Proje iyi gidiyor."}""")]
    public async Task Schema_invalid_answer_is_not_shown_but_logged(string answer)
    {
        var status = await SeedStatusAsync(Nov4);
        var service = NewService(new ScriptedJsonModel(_ => answer));

        var result = await service.GenerateProjectCommentAsync(status, _ct);

        Assert.Equal(AnalysisCommentStatus.Failed, result.Status);
        Assert.Null(result.Comment);
        Assert.Equal(ProjectCommentService.InvalidAnswerMessage, result.Message);
        var log = await Db.AiAnalysisLogs.SingleAsync(_ct);
        Assert.Equal(AiAnalysisOutcome.InvalidSchema, log.Outcome);
        Assert.False(log.SchemaValid);
        Assert.Null(log.ResultJson);
        Assert.Null(await service.FindProjectCommentAsync(status, _ct));
    }

    [Fact]
    public async Task Model_error_is_returned_as_message_and_logged()
    {
        var status = await SeedStatusAsync(Nov4);
        var model = new ScriptedJsonModel(_ => throw new ChatModelException(ChatModelMessages.Timeout));

        var result = await NewService(model).GenerateProjectCommentAsync(status, _ct);

        Assert.Equal(AnalysisCommentStatus.Failed, result.Status);
        Assert.Equal(ChatModelMessages.Timeout, result.Message);
        var log = await Db.AiAnalysisLogs.SingleAsync(_ct);
        Assert.Equal(AiAnalysisOutcome.ModelError, log.Outcome);
        Assert.Equal(ChatModelMessages.Timeout, log.ErrorMessage);
        Assert.Null(log.SchemaValid);
    }

    /// <summary>WhatIfServiceTests ile aynı: tek backendci, üç bağımsız 40 saatlik iş, hedef 20 Kasım.</summary>
    private async Task<(int ProjectId, WhatIfComparison Comparison)> SeedComparisonAsync()
    {
        var project = await new ProjectService(Db).CreateAsync(new ProjectRequest
        {
            Name = "What-if", Type = ProjectType.WebApplication, StartDate = new DateOnly(2026, 11, 2),
            TargetEndDate = new DateOnly(2026, 11, 20), Currency = "TRY", HoursPerDay = 8
        }, _ct);
        await new PersonService(Db).CreateAsync(project.Id,
            new PersonRequest { Name = "Ayşe", Skills = [Skill.Backend], WeeklyCapacityHours = 40, HourlyCost = 100 }, _ct);
        var items = new WorkItemService(Db);
        foreach (var name in new[] { "Ödeme", "Kampanya", "Raporlama" })
            await items.CreateAsync(project.Id, new WorkItemRequest { Name = name, RequiredSkill = Skill.Backend, EstimatedHours = 40 }, _ct);
        Db.ChangeTracker.Clear();
        var raporlama = Db.WorkItems.Single(w => w.Name == "Raporlama").Id;

        var comparison = await NewWhatIfService().CompareAsync(project.Id,
        [
            new WhatIfScenario("Kapsam azalt", [new ScenarioChange(ScenarioChangeKind.RemoveWorkItem, WorkItemId: raporlama)]),
            new WhatIfScenario("Kişi ekle", [new ScenarioChange(ScenarioChangeKind.AddPerson, Name: "Yeni", Skills: [Skill.Backend],
                WeeklyHours: 40, Date: new DateOnly(2026, 11, 2))])
        ], _ct);
        return (project.Id, comparison);
    }

    private static ScriptedJsonModel ScenarioModel(string? fabricated = null) => new(request =>
    {
        var input = Input(request);
        var findings = input["scenarios"]!.AsArray().Select(s =>
            $"{s!["name"]}: P80 bitiş {Date(s["p80Finish"])}, mevcut plana göre {s["p80DeltaWorkdaysVsCurrent"]} iş günü fark, " +
            $"hedefe yetişme olasılığı %{s["onTimeProbabilityPercent"]}.").ToList();
        if (fabricated is not null)
            findings.Add($"Maliyet {fabricated} TRY azalır.");
        return Comment(
            $"Mevcut planda hedefe yetişme olasılığı %{input["current"]!["onTimeProbabilityPercent"]}.",
            [.. findings],
            ["Yeni kişide ısınma süresi ve mentorluk yükü (Brooks etkisi) varsayılmıştır."]);
    });

    [Fact]
    public async Task Scenario_comment_quotes_comparison_values_and_flags_invented_numbers()
    {
        var (projectId, comparison) = await SeedComparisonAsync();
        var model = ScenarioModel();

        var clean = await NewService(model).GenerateScenarioCommentAsync(projectId, comparison, _ct);

        Assert.Equal(AnalysisCommentStatus.Ready, clean.Status);
        Assert.Equal(2, clean.Comment!.KeyFindings.Count);
        var scope = comparison.Scenarios[0];
        Assert.Contains($"{scope.P80DeltaWorkdays} iş günü", clean.Comment.KeyFindings[0]);
        Assert.Contains($"%{Math.Round(scope.MonteCarlo.OnTimeProbability * 100)}", clean.Comment.KeyFindings[0]);
        Assert.Empty(clean.UnverifiedNumbers!);                          // P80 farkı, olasılık, tarih girdide var
        Assert.Contains("Brooks", clean.Comment.Caveats.Single());

        var request = model.Requests.Single();
        Assert.Equal(ChatPrompts.ScenarioCommentSystem, request.SystemPrompt);
        Assert.Contains("Brooks", request.SystemPrompt);
        var input = Input(request);
        Assert.Equal("TRY", input["currency"]!.GetValue<string>());
        Assert.Equal(2, input["scenarios"]!.AsArray().Count);
        Assert.NotNull(input["assumptions"]!["newPersonRampUpWeeks"]);

        var invented = await NewService(ScenarioModel(fabricated: "98.765")).GenerateScenarioCommentAsync(projectId, comparison, _ct);
        Assert.Equal(["98.765"], invented.UnverifiedNumbers);

        Assert.Equal(2, await Db.AiAnalysisLogs.CountAsync(l => l.Kind == AiAnalysisKind.ScenarioComment, _ct));
        var cached = await NewService(model).FindScenarioCommentAsync(projectId, comparison, _ct);
        Assert.Equal(["98.765"], cached!.UnverifiedNumbers);            // aynı girdi → en son yorum
    }

    [Fact]
    public async Task Clarity_feedback_is_stored_on_the_comment_log_and_overwritten_by_a_second_rating()
    {
        // Tur 5a (RQ4): "Bu açıklama anlaşılır mıydı?" yorumu üreten AiAnalysisLog satırına yazılır.
        var status = await SeedStatusAsync(Nov4);
        var model = new ScriptedJsonModel(_ => Comment("Proje biraz geride.", [], []));
        var service = NewService(model);
        var generated = await service.GenerateProjectCommentAsync(status, _ct);
        Assert.Equal(AnalysisCommentStatus.Ready, generated.Status);
        var logId = Assert.IsType<int>(generated.LogId);
        Assert.Null(generated.Helpful);

        await service.RateAsync(logId, new CommentRating(false), _ct);
        var second = await service.RateAsync(logId, new CommentRating(true, 4), _ct);
        Assert.Equal(new CommentRating(true, 4), second);
        Db.ChangeTracker.Clear();

        var log = await Db.AiAnalysisLogs.SingleAsync(l => l.Id == logId, _ct);
        Assert.True(log.Helpful);
        Assert.Equal(4, log.ClarityRating);
        Assert.NotNull(log.RatedAt);

        // Önbellekten açılan yorum aynı kaydı ve puanı taşır.
        var cached = await service.FindProjectCommentAsync(status, _ct);
        Assert.Equal(logId, cached!.LogId);
        Assert.True(cached.Helpful);
        Assert.Equal(4, cached.ClarityRating);

        await Assert.ThrowsAsync<ProjectMind.Application.Common.BusinessRuleException>(() =>
            service.RateAsync(logId, new CommentRating(true, 6), _ct));
    }

    [Fact]
    public async Task Only_successful_comment_logs_can_be_rated()
    {
        var status = await SeedStatusAsync(Nov4);
        var invalid = await NewService(new ScriptedJsonModel(_ => "{}")).GenerateProjectCommentAsync(status, _ct);
        Assert.Equal(AnalysisCommentStatus.Failed, invalid.Status);
        Assert.Null(invalid.LogId);

        var service = NewService(new ScriptedJsonModel(_ => "{}"));
        var failedLog = await Db.AiAnalysisLogs.SingleAsync(_ct);
        await Assert.ThrowsAsync<ProjectMind.Application.Common.BusinessRuleException>(() =>
            service.RateAsync(failedLog.Id, new CommentRating(true), _ct));

        var chat = new ProjectMind.Domain.Entities.AiAnalysisLog
        {
            Kind = AiAnalysisKind.Chat, PromptVersion = "chat", Outcome = AiAnalysisOutcome.Success
        };
        Db.AiAnalysisLogs.Add(chat);
        await Db.SaveChangesAsync(_ct);
        await Assert.ThrowsAsync<ProjectMind.Application.Common.BusinessRuleException>(() =>
            service.RateAsync(chat.Id, new CommentRating(true), _ct));
        await Assert.ThrowsAsync<ProjectMind.Application.Common.NotFoundException>(() =>
            service.RateAsync(9999, new CommentRating(true), _ct));
    }

    [Fact]
    public async Task Not_configured_model_returns_no_log_id_so_rating_is_hidden()
    {
        var status = await SeedStatusAsync(Nov4);
        var result = await NewService(new ScriptedJsonModel(_ => "{}", configured: false)).GenerateProjectCommentAsync(status, _ct);
        Assert.Equal(AnalysisCommentStatus.NotConfigured, result.Status);
        Assert.Null(result.LogId);
    }
}
