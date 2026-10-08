using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectMind.Application.Ai;
using ProjectMind.Application.MissingWork;
using ProjectMind.Application.Projects;
using ProjectMind.Application.WorkItems;
using ProjectMind.Domain.Entities;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Tests.Integration;

/// <summary>Hibrit eksik iş (D26): kural + sahte LLM katmanı, kart kaynağı, Mock modu, hata yolları, denetim kaydı.</summary>
public class HybridMissingWorkServiceTests : ServiceTestBase
{
    private readonly CancellationToken _ct = CancellationToken.None;

    /// <summary>
    /// LLM cevabı: 1) KVKK (M, kalır), 2) kural önerisinin tekrarı, 3) mevcut işin tekrarı, 4) saat alanlı şema dışı madde,
    /// 5) Ödeme sağlayıcı sözleşmesi (S, kalır).
    /// </summary>
    private const string LlmAnswer = """
        {"suggestions":[
          {"name":"KVKK uyum incelemesi","phase":"Analysis","skill":"Analysis","reason":"Kişisel veri işleniyor.","size":"M"},
          {"name":"Veritabanı kurulumu","phase":"Infrastructure","skill":"Database","reason":"Ortam gerekli.","size":"S"},
          {"name":"Backend API geliştirmesi","phase":"Development","skill":"Backend","reason":"Servisler.","size":"L"},
          {"name":"Yük testi","phase":"Test","skill":"Test","reason":"Trafik artar.","size":"L","hours":40},
          {"name":"Ödeme sağlayıcı sözleşmesi","phase":"Analysis","skill":"ProjectManagement","reason":"Ödeme altyapısı için gerekli.","size":"S"}
        ]}
        """;

    private async Task<(int ProjectId, int SessionId)> SeedAsync()
    {
        var project = await new ProjectService(Db).CreateAsync(new ProjectRequest
        {
            Name = "E-ticaret", Description = "Online mağaza", Type = ProjectType.WebApplication,
            StartDate = new DateOnly(2026, 11, 2), TargetEndDate = new DateOnly(2027, 3, 31), Currency = "TRY", HoursPerDay = 8
        }, _ct);
        await new WorkItemService(Db).CreateAsync(project.Id, new WorkItemRequest
        {
            Name = "Backend API", Phase = WorkPhase.Development, RequiredSkill = Skill.Backend, EstimatedHours = 40
        }, _ct);
        var session = new ChatSession { Title = "E-ticaret", ProjectId = project.Id };
        Db.ChatSessions.Add(session);
        await Db.SaveChangesAsync(_ct);
        Db.ChangeTracker.Clear();
        return (project.Id, session.Id);
    }

    private async Task<MissingWorkResult> RuleResultAsync(int projectId) =>
        MissingWorkDetector.Detect(ProjectType.WebApplication, await new WorkItemService(Db).ListAsync(projectId, _ct));

    [Fact]
    public async Task Shortcut_creates_rule_cards_then_llm_cards_with_source_and_hours_from_size()
    {
        var (projectId, sessionId) = await SeedAsync();
        var rule = await RuleResultAsync(projectId);
        var model = new ScriptedJsonModel(_ => LlmAnswer);

        var reply = await NewChatService(model).CheckMissingWorkAsync(sessionId, _ct);

        var actions = await Db.AiActions.AsNoTracking().Where(a => a.ChatSessionId == sessionId).OrderBy(a => a.Id).ToListAsync(_ct);
        var ruleWork = actions.Where(a => a.Source == AiActionSource.Rule && a.ToolName == AiTools.AddWorkItem).ToList();
        var ruleDeps = actions.Where(a => a.Source == AiActionSource.Rule && a.ToolName == AiTools.AddDependency).ToList();
        var llm = actions.Where(a => a.Source == AiActionSource.Llm).ToList();

        Assert.Equal(rule.Missing.Count, ruleWork.Count);
        Assert.Equal(rule.Missing.Sum(m => m.SuggestedSuccessors.Count), ruleDeps.Count);
        Assert.Contains(ruleDeps, d => d.Summary == "Bağımlılık ekle: Veritabanı kurulumu → Backend API");
        Assert.Equal(actions.Count, ruleWork.Count + ruleDeps.Count + llm.Count);   // User/Unknown kart yok
        Assert.True(actions.FindIndex(a => a.Source == AiActionSource.Llm) > actions.FindLastIndex(a => a.Source == AiActionSource.Rule));

        // Yalnız KVKK (M → 24 s) ve Ödeme sözleşmesi (S → 8 s) kalır; tekrarlar ve saat alanlı madde düşer.
        Assert.Equal(
            ["İş ekle: KVKK uyum incelemesi · Analiz · Analiz · 24 saat",
             "İş ekle: Ödeme sağlayıcı sözleşmesi · Analiz · Proje yönetimi · 8 saat"],
            llm.Select(a => a.Summary));
        Assert.All(actions, a => Assert.Equal(reply.Id, a.ChatMessageId));
        Assert.All(actions, a => Assert.Equal(AiActionStatus.Pending, a.Status));   // onaysız hiçbir şey eklenmez
        Assert.Equal(1, await Db.WorkItems.CountAsync(_ct));

        Assert.Contains("KVKK uyum incelemesi (M): Kişisel veri işleniyor.", reply.Content);
        Assert.DoesNotContain("Doğrulanamayan", reply.Content);
        // Tur 4a H5: elenen tekrarlar cevapta ve denetim kaydında görünür.
        Assert.Contains("Tekrar sayılıp elenen AI önerileri (2): Veritabanı kurulumu (≈ Veritabanı kurulumu), " +
                        "Backend API geliştirmesi (≈ Backend API).", reply.Content);

        // LLM'e veritabanı değil özet gider: mevcut işler ve kural önerileri; şema adı ve prompt.
        var request = Assert.Single(model.Requests);
        Assert.Equal(LlmMissingWorkSchema.Name, request.SchemaName);
        Assert.Equal(ChatPrompts.MissingWorkSystem, request.SystemPrompt);
        Assert.Contains("\"Backend API\"", request.UserMessage);
        Assert.Contains("\"Veritabanı kurulumu\"", request.UserMessage);

        var log = await Db.AiAnalysisLogs.AsNoTracking().SingleAsync(_ct);
        Assert.Equal(AiAnalysisKind.MissingWork, log.Kind);
        Assert.Equal(AiAnalysisOutcome.Success, log.Outcome);
        Assert.False(log.SchemaValid);                     // bir madde şema dışıydı
        Assert.Equal(ChatPrompts.MissingWorkVersion, log.PromptVersion);
        Assert.Equal("scripted-json", log.Model);
        Assert.Equal(projectId, log.ProjectId);
        Assert.Equal(sessionId, log.ChatSessionId);
        Assert.Contains("KVKK uyum incelemesi", log.ResultJson);
        Assert.Contains("\"duplicateOf\":\"Backend API\"", log.ResultJson);
    }

    [Fact]
    public async Task Mock_mode_runs_only_rule_layer_and_says_ai_needs_a_key()
    {
        var (projectId, sessionId) = await SeedAsync();
        var rule = await RuleResultAsync(projectId);
        var model = new ScriptedJsonModel(_ => LlmAnswer, configured: false);

        var reply = await NewChatService(model).CheckMissingWorkAsync(sessionId, _ct);

        Assert.Empty(model.Requests);
        Assert.Contains(MissingWorkService.NotConfiguredMessage, reply.Content);
        var actions = await NewActionService().ListAsync(sessionId, _ct);
        Assert.All(actions, a => Assert.Equal(AiActionSource.Rule, a.Source));
        Assert.Equal(rule.Missing.Count, actions.Count(a => a.ToolName == AiTools.AddWorkItem));
        Assert.Empty(Db.AiAnalysisLogs);                  // LLM çağrısı yok → kayıt yok
    }

    [Fact]
    public async Task Model_error_still_returns_rule_result_and_is_logged()
    {
        var (projectId, _) = await SeedAsync();
        var rule = await RuleResultAsync(projectId);

        var result = await NewMissingWorkService(new ScriptedJsonModel(_ => throw new ChatModelException("AI servisine bağlanılamadı.")))
            .CheckHybridAsync(projectId, null, _ct);

        Assert.Equal(LlmLayerStatus.ModelError, result.LlmStatus);
        Assert.Equal(rule.Missing.Select(m => m.Name), result.Rule.Missing.Select(m => m.Name));
        Assert.Empty(result.Llm);
        Assert.NotNull(result.Impact);
        Assert.Equal(rule.TotalDefaultHours, result.Impact.ExtraHours);
        Assert.Contains("yalnız kural", result.LlmMessage);
        var log = await Db.AiAnalysisLogs.AsNoTracking().SingleAsync(_ct);
        Assert.Equal(AiAnalysisOutcome.ModelError, log.Outcome);
        Assert.Null(log.ResultJson);
    }

    [Fact]
    public async Task Invalid_schema_answer_gives_no_llm_suggestions()
    {
        var (projectId, _) = await SeedAsync();

        var result = await NewMissingWorkService(new ScriptedJsonModel(_ => """{"suggestions":[],"hours":120}"""))
            .CheckHybridAsync(projectId, null, _ct);

        Assert.Equal(LlmLayerStatus.InvalidSchema, result.LlmStatus);
        Assert.Empty(result.Llm);
        Assert.NotEmpty(result.Rule.Missing);
        var log = await Db.AiAnalysisLogs.AsNoTracking().SingleAsync(_ct);
        Assert.Equal(AiAnalysisOutcome.InvalidSchema, log.Outcome);
        Assert.False(log.SchemaValid);
    }

    [Fact]
    public async Task Impact_includes_llm_hours_from_size()
    {
        var (projectId, _) = await SeedAsync();
        var rule = await RuleResultAsync(projectId);

        var result = await NewMissingWorkService(new ScriptedJsonModel(_ => LlmAnswer)).CheckHybridAsync(projectId, null, _ct);

        Assert.Equal(rule.TotalDefaultHours + 24 + 8, result.Impact!.ExtraHours);
    }

    /// <summary>Sohbet modeli: önce check_missing_work, sonra verilen kartları önerir; JSON çağrısında LLM katmanı cevabı.</summary>
    private sealed class HybridChatModel(params (string Tool, object Input)[] calls) : IChatModel
    {
        public List<ToolExecutionResult> Results { get; } = [];
        public bool IsConfigured => true;
        public string ProviderKey => "hybrid-chat";

        public async Task<ChatTurnResult> CompleteTurnAsync(ChatTurnRequest request, IChatToolExecutor tools, CancellationToken ct)
        {
            Results.Add(await tools.ExecuteAsync(AiTools.CheckMissingWork, JsonSerializer.SerializeToElement(new { }), ct));
            foreach (var (tool, input) in calls)
                Results.Add(await tools.ExecuteAsync(tool, JsonSerializer.SerializeToElement(input), ct));
            return new ChatTurnResult("Eksik işleri önerdim.", "hybrid-chat");
        }

        public Task<ChatJsonResult> CompleteJsonAsync(ChatJsonRequest request, CancellationToken ct) =>
            Task.FromResult(new ChatJsonResult(LlmAnswer, "hybrid-json"));
    }

    [Fact]
    public async Task Chat_tool_tags_sources_and_cards_get_source_from_csharp_matching()
    {
        var (_, sessionId) = await SeedAsync();
        var model = new HybridChatModel(
            (AiTools.AddWorkItem, new { name = "veritabanı kurulumu", phase = "Infrastructure", requiredSkill = "Database", estimatedHours = 8 }),
            (AiTools.AddDependency, new { predecessorName = "Veritabanı kurulumu", successorName = "Backend API" }),
            (AiTools.AddWorkItem, new { name = "KVKK uyum incelemesi", phase = "Analysis", requiredSkill = "Analysis", estimatedHours = 24 }),
            // Model kaynağı "llm" diye iddia etse de LLM katmanı bunu önermedi → User.
            (AiTools.AddWorkItem, new { name = "Kampanya modülü", phase = "Development", requiredSkill = "Backend", estimatedHours = 30, description = "source: llm" }),
            (AiTools.AddPerson, new { name = "Ayşe", skills = new[] { "Backend" }, weeklyCapacityHours = 40 }));

        await NewChatService(model).SendAsync(sessionId, "Eksik iş var mı?", _ct);

        var tool = model.Results[0];
        Assert.False(tool.IsError);
        using (var doc = JsonDocument.Parse(tool.Content))
        {
            var root = doc.RootElement;
            Assert.All(root.GetProperty("missing").EnumerateArray(), m => Assert.Equal("rule", m.GetProperty("source").GetString()));
            var ai = root.GetProperty("aiSuggestions").EnumerateArray().ToList();
            Assert.Equal(["KVKK uyum incelemesi", "Ödeme sağlayıcı sözleşmesi"], ai.Select(a => a.GetProperty("name").GetString()));
            Assert.Equal([24m, 8m], ai.Select(a => a.GetProperty("suggestedHours").GetDecimal()));
            Assert.Equal("Success", root.GetProperty("aiLayer").GetProperty("status").GetString());
        }

        var sources = (await NewActionService().ListAsync(sessionId, _ct)).Select(a => a.Source).ToList();
        Assert.Equal([AiActionSource.Rule, AiActionSource.Rule, AiActionSource.Llm, AiActionSource.User, AiActionSource.User], sources);
    }

    [Fact]
    public async Task Acceptance_by_source_counts_decisions()
    {
        var (_, sessionId) = await SeedAsync();
        await NewChatService(new ScriptedJsonModel(_ => LlmAnswer)).CheckMissingWorkAsync(sessionId, _ct);
        var actions = NewActionService();
        var cards = await actions.ListAsync(sessionId, _ct);
        var llm = cards.Where(c => c.Source == AiActionSource.Llm).ToList();
        await actions.ApplyAsync(llm[0].Id, _ct);
        await actions.RejectAsync(llm[1].Id, _ct);
        await actions.RejectAsync(cards.First(c => c.Source == AiActionSource.Rule && c.ToolName == AiTools.AddWorkItem).Id, _ct);

        var rows = await actions.GetAcceptanceBySourceAsync(_ct);

        var llmRow = rows.Single(r => r.Source == AiActionSource.Llm);
        Assert.Equal((2, 1, 1, 0, 0), (llmRow.Total, llmRow.Applied, llmRow.Rejected, llmRow.Failed, llmRow.Pending));
        Assert.Equal(0.5, llmRow.AcceptanceRate);
        var ruleRow = rows.Single(r => r.Source == AiActionSource.Rule);
        Assert.Equal(0d, ruleRow.AcceptanceRate);
        Assert.Equal(ruleRow.Total - 1, ruleRow.Pending);
        Assert.True(await Db.WorkItems.AnyAsync(w => w.Name == "KVKK uyum incelemesi" && w.EstimatedHours == 24, _ct));
    }
}
