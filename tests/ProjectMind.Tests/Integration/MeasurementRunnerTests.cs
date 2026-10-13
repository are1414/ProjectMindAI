using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectMind.Application;
using ProjectMind.Application.Abstractions;
using ProjectMind.Application.Ai;
using ProjectMind.Application.Demo;
using ProjectMind.Application.Evaluation;
using ProjectMind.Application.Ml;
using ProjectMind.Application.MissingWork;
using ProjectMind.Application.WhatIf;
using ProjectMind.Domain.Entities;
using ProjectMind.Domain.Enums;
using ProjectMind.Infrastructure.Persistence;

namespace ProjectMind.Tests.Integration;

/// <summary>
/// Ölçüm koşucuları (Tur 5b, D31): sağlayıcı karşılaştırması ve tohumlanmış eksik iş deneyi gerçek Application servisleriyle,
/// bellek içi SQLite ve sahte modellerle çalışır. Gerçek API çağrılmaz.
/// </summary>
public class MeasurementRunnerTests : ServiceTestBase
{
    private readonly CancellationToken _ct = CancellationToken.None;

    /// <summary>Comment JSON'unda bağlamda olmayan bir sayı (NumberGuard işaretler).</summary>
    private const string UnverifiedNumber = "98765";

    private static string ValidComment => JsonSerializer.Serialize(new
    {
        summary = $"Proje izleniyor; tahmini değer {UnverifiedNumber}.",
        keyFindings = new[] { "Takvim performansı izlenmeli." },
        recommendedActions = new[] { "Bloke işi çözün." },
        caveats = new[] { "ML modeli sentetik veriyle eğitildi." }
    });

    private const string InvalidComment = """{"summary": 5}""";

    private const string OneSuggestion =
        """{"suggestions":[{"name":"Erişilebilirlik testi","phase":"Test","skill":"Test","reason":"Engelli kullanıcılar için kontrol.","size":"S"}]}""";

    private const string NoSuggestions = """{"suggestions":[]}""";

    /// <summary>
    /// Senaryolu sağlayıcı: yorumlar (isteğe göre geçerli/geçersiz), eksik iş JSON'u ve sohbet. Sohbette sorunun beklenen aracını
    /// çağırır — what-if sorusu hariç (bilerek get_project_status çağırır: beklenen araç oranı 4/5). Eksik iş sorusunda ayrıca bir
    /// iş kartı önerir (koşucu otomatik reddetmeli).
    /// </summary>
    private sealed class ScriptedProvider(
        string name, Func<int, string> comment, string missingWork, ChatUsage? usage, bool callTools, string? failChatTask = null) : IChatModel
    {
        private int _comments;

        public bool IsConfigured => true;
        public string ProviderKey => name;

        public Task<ChatJsonResult> CompleteJsonAsync(ChatJsonRequest request, CancellationToken ct) =>
            Task.FromResult(new ChatJsonResult(
                request.SchemaName == AnalysisCommentSchema.Name ? comment(_comments++) : missingWork, $"{name}-model", usage));

        public async Task<ChatTurnResult> CompleteTurnAsync(ChatTurnRequest request, IChatToolExecutor tools, CancellationToken ct)
        {
            var task = EvaluationTaskSet.Tasks.Single(t => t.Question is not null && request.UserMessage.EndsWith(t.Question));
            if (task.Id == failChatTask)
                throw new ChatModelException(ChatModelMessages.Timeout);
            if (callTools)
            {
                var tool = task.ExpectedTool == AiTools.SimulateWhatIf ? AiTools.GetProjectStatus : task.ExpectedTool!;
                await tools.ExecuteAsync(tool, JsonSerializer.SerializeToElement(new { }), ct);
                if (task.ExpectedTool == AiTools.CheckMissingWork)
                    await tools.ExecuteAsync(AiTools.AddWorkItem, JsonSerializer.SerializeToElement(
                        new AddWorkItemPayload("Erişilebilirlik testi", WorkPhase.Test, Skill.Test, 8), AiJson.Options), ct);
            }
            return new ChatTurnResult("Durum özetlendi.", $"{name}-model", usage);
        }
    }

    private sealed class FakeCatalog(params EvaluationProvider[] providers) : IEvaluationModelCatalog
    {
        public IReadOnlyList<EvaluationProvider> GetProviders() => providers;
    }

    private sealed class SyncProgress(Action<string> onReport) : IProgress<string>
    {
        public void Report(string value) => onReport(value);
    }

    private static readonly EvaluationProvider MockProvider = new("Mock", "mock", null, "LLM değil — karşılaştırmaya alınmaz.");

    /// <summary>Uygulamanın Application kayıtları + test veritabanı + sahte ML ve sağlayıcı katalogu.</summary>
    private ServiceProvider Services(IEvaluationModelCatalog catalog, IChatModel defaultModel, EvaluationOptions? evaluation = null)
    {
        var connection = Db.Database.GetDbConnection();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(connection));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<IDelayPredictor>(Predictor);
        services.AddSingleton(new AiOptions());
        services.Configure<WhatIfOptions>(o => o.Iterations = 50);
        services.Configure<EvaluationOptions>(o =>
        {
            var e = evaluation ?? new EvaluationOptions();
            (o.Repeats, o.RecallRepeats, o.RecallSeed, o.RecallOtherItems, o.RecallTemplateItems) =
                (e.Repeats, e.RecallRepeats, e.RecallSeed, e.RecallOtherItems, e.RecallTemplateItems);
        });
        services.AddSingleton(catalog);
        // Infrastructure kaydıyla aynı kural: ölçüm bağlamının sağlayıcısı önce gelir.
        services.AddScoped<IChatModel>(sp => sp.GetRequiredService<EvaluationRunContext>().ModelOverride ?? defaultModel);
        return services.BuildServiceProvider();
    }

    private static async Task<T> InScopeAsync<T>(ServiceProvider sp, Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = sp.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    [Fact]
    public async Task Provider_comparison_tags_every_record_rejects_cards_and_reports_hand_checked_rates()
    {
        var a = new ScriptedProvider("A", _ => ValidComment, OneSuggestion, new ChatUsage(100, 20), callTools: true);
        // B: her üçüncü yorum (2., 5. çağrı) şema dışı → yorum görevlerinde 2/3 geçerli; risk sorusunda zaman aşımı.
        var b = new ScriptedProvider("B", i => i % 3 == 2 ? InvalidComment : ValidComment, NoSuggestions, null, callTools: false,
            failChatTask: "chat-risk");
        var catalog = new FakeCatalog(new("A", "a-model", a), new("B", "b-model", b), MockProvider);
        await using var sp = Services(catalog, new ScriptedJsonModel(_ => NoSuggestions, configured: false));

        // Kullanıcının daha önce tek tek uyguladığı bir kart (RQ4 verisi; koşudan etkilenmemeli).
        Db.AiActions.Add(new AiAction
        {
            ToolName = AiTools.AddPerson, PayloadJson = "{}", Summary = "Kişi ekle", Status = AiActionStatus.Applied,
            Source = AiActionSource.User
        });
        await Db.SaveChangesAsync(_ct);

        var messages = new List<string>();
        var info = await InScopeAsync(sp, s => s.GetRequiredService<ProviderComparisonService>()
            .RunAsync(3, new SyncProgress(messages.Add), _ct));
        Db.ChangeTracker.Clear();

        // Koşu kaydı
        var run = await Db.EvaluationRuns.SingleAsync(_ct);
        Assert.Equal(info.Id, run.Id);
        Assert.Equal(EvaluationRunStatus.Completed, run.Status);
        Assert.Equal(EvaluationRunKind.ProviderComparison, run.Kind);
        Assert.Equal(EvaluationTaskSet.Version, run.TaskSetVersion);
        Assert.Equal("A/a-model,B/b-model", run.Providers);
        Assert.Equal(3, run.Repeats);
        Assert.NotNull(run.FinishedAt);
        Assert.Contains("Mock", run.Notes);
        Assert.Contains(messages, m => m.Contains("(48/48)"));   // 2 sağlayıcı × 3 tekrar × 8 görev

        // Kayıtlar: A tekrar başına 3 JSON + 5 sohbet + 1 araçla tetiklenen eksik iş = 9; B 3 + 5 = 8. Hepsi koşuya etiketli.
        var logs = await Db.AiAnalysisLogs.ToListAsync(_ct);
        Assert.Equal(27 + 24, logs.Count);
        Assert.Equal(51, info.Calls);
        Assert.All(logs, l => Assert.Equal(run.Id, l.EvaluationRunId));
        Assert.All(logs, l => Assert.NotNull(EvaluationTaskSet.Find(l.EvaluationTask)));
        Assert.All(logs.Where(l => l.EvaluationProvider == "A"), l => Assert.Equal((100, 20), (l.InputTokens!.Value, l.OutputTokens!.Value)));
        Assert.All(logs.Where(l => l.EvaluationProvider == "B"), l => Assert.Null(l.InputTokens));
        Assert.Equal(3, logs.Count(l => l.EvaluationProvider == "A" && l.Kind == AiAnalysisKind.MissingWork && l.EvaluationTask == "chat-missing"));

        // Kartlar: A'nın eksik iş sorusundaki kartı her tekrarda otomatik reddedildi; hiçbiri uygulanmadı, iş eklenmedi.
        var evalCards = await Db.AiActions.Where(x => x.EvaluationRunId == run.Id).ToListAsync(_ct);
        Assert.Equal(3, evalCards.Count);
        Assert.All(evalCards, c =>
        {
            Assert.Equal(AiActionStatus.Rejected, c.Status);
            Assert.Equal(ProviderComparisonService.AutoRejectedMessage, c.ResultMessage);
            Assert.Null(c.ChatSessionId);
        });
        Assert.DoesNotContain(await Db.WorkItems.Select(w => w.Name).ToListAsync(_ct), n => n == "Erişilebilirlik testi");

        // Koşunun sohbet oturumları silindi; yalnız demonun kendi sohbeti kaldı.
        Assert.Equal(DemoScenario.ChatTitle, (await Db.ChatSessions.SingleAsync(_ct)).Title);

        // RQ4 kabul oranı ve özet ölçüm verisini içermez; RQ3 de koşunun what-if çalıştırmasını saymaz.
        var acceptance = await InScopeAsync(sp, s => s.GetRequiredService<AiActionService>().GetAcceptanceBySourceAsync(_ct));
        var user = Assert.Single(acceptance);
        Assert.Equal((AiActionSource.User, 1, 1, 0), (user.Source, user.Total, user.Applied, user.Rejected));
        var summary = await InScopeAsync(sp, s => s.GetRequiredService<EvaluationService>().GetSummaryAsync(_ct));
        Assert.Equal(1, Assert.Single(summary.Rq4.Acceptance).Total);
        Assert.Equal(0, summary.Rq4.Comments);
        Assert.Equal(0, summary.Rq3.Runs);
        Assert.Equal(WhatIfRunSource.Evaluation, (await Db.WhatIfRunLogs.SingleAsync(_ct)).Source);

        // Koşunun LLM önerisi, kullanıcı kartlarının kaynak (Llm) eşleşmesine karışmaz.
        var projectId = run.ProjectId!.Value;
        Assert.Empty(await MissingWorkService.LatestLlmSuggestionNamesAsync(Db, projectId, _ct));

        // Rapor: elle hesaplanan oranlar.
        var report = await InScopeAsync(sp, s => s.GetRequiredService<ProviderComparisonService>().GetReportAsync(run.Id, _ct));
        ProviderTaskStats Row(string provider, string task) => report.Rows.Single(r => r.Provider == provider && r.Task == task);

        Assert.Equal(2m / 3, Row("B", "comment-project").SchemaValidRate);
        Assert.Equal(2m / 3, Row("B", "comment-scenario").SchemaValidRate);
        Assert.Equal(3m / 24, Row("B", ProviderTaskStats.AllTasks).ErrorRate);        // risk sorusu 3 kez zaman aşımı
        Assert.Equal(1m, Row("B", "chat-risk").ErrorRate);
        Assert.Equal(0, Row("B", ProviderTaskStats.AllTasks).TokenCalls);
        Assert.Null(Row("B", ProviderTaskStats.AllTasks).MeanInputTokens);
        Assert.Equal(0m, Row("B", ProviderTaskStats.AllTasks).ExpectedToolRate);       // B hiç araç çağırmadı (12 sohbet)

        Assert.Equal(1m, Row("A", "comment-project").SchemaValidRate);
        Assert.Equal(1m, Row("A", "comment-project").UnverifiedShare);                 // her yorumda 98765 işaretlendi
        Assert.Equal(0.8m, Row("A", ProviderTaskStats.AllTasks).ExpectedToolRate);     // 12 / 15 (what-if sorusu yanlış araç)
        Assert.Equal(0m, Row("A", "chat-whatif").ExpectedToolRate);
        Assert.Equal(27, Row("A", ProviderTaskStats.AllTasks).TokenCalls);
        Assert.Equal(100m, Row("A", ProviderTaskStats.AllTasks).MeanInputTokens);
        Assert.Equal(0m, Row("A", ProviderTaskStats.AllTasks).ErrorRate);

        var csv = await InScopeAsync(sp, s => s.GetRequiredService<ProviderComparisonService>().ExportAsync(run.Id, _ct));
        var lines = csv.Content.TrimStart(Csv.Bom).Split(Csv.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(string.Join(',', ProviderComparisonService.CsvHeader), lines[0]);
        Assert.Equal(report.Rows.Count + 1, lines.Length);

        // AI kaydı CSV'sinde koşu kolonları var.
        var aiLog = await InScopeAsync(sp, s => s.GetRequiredService<EvaluationService>().ExportAsync(EvaluationExport.Rq4AiLog, _ct));
        Assert.Contains($",100,20,{run.Id},A,comment-project", aiLog.Content);
    }

    [Fact]
    public async Task Provider_comparison_requires_a_keyed_provider_and_creates_no_run()
    {
        await using var sp = Services(new FakeCatalog(MockProvider), new ScriptedJsonModel(_ => NoSuggestions, configured: false));
        var ex = await Assert.ThrowsAsync<ProjectMind.Application.Common.BusinessRuleException>(() =>
            InScopeAsync(sp, s => s.GetRequiredService<ProviderComparisonService>().RunAsync(null, null, _ct)));
        Assert.Contains("Mock", ex.Message);
        Assert.Empty(await Db.EvaluationRuns.ToListAsync(_ct));
        Assert.Empty(await Db.Projects.ToListAsync(_ct));   // demo da oluşturulmadı
    }

    [Fact]
    public async Task Provider_comparison_can_be_cancelled_and_records_the_partial_run()
    {
        var a = new ScriptedProvider("A", _ => ValidComment, NoSuggestions, null, callTools: true);
        await using var sp = Services(new FakeCatalog(new EvaluationProvider("A", "a-model", a)),
            new ScriptedJsonModel(_ => NoSuggestions, configured: false));
        using var cts = new CancellationTokenSource();
        var progress = new SyncProgress(m =>
        {
            if (m.Contains("(3/"))
                cts.Cancel();   // üçüncü görev başlarken iptal
        });

        var info = await InScopeAsync(sp, s => s.GetRequiredService<ProviderComparisonService>().RunAsync(1, progress, cts.Token));
        Db.ChangeTracker.Clear();

        Assert.Equal(EvaluationRunStatus.Cancelled, info.Status);
        var run = await Db.EvaluationRuns.SingleAsync(_ct);
        Assert.Equal(EvaluationRunStatus.Cancelled, run.Status);
        Assert.NotNull(run.FinishedAt);
        Assert.Equal(2, await Db.AiAnalysisLogs.CountAsync(_ct));   // yalnız ilk iki görev (iki yorum) kaydedildi
        Assert.Single(await Db.ChatSessions.ToListAsync(_ct));       // yalnız demonun sohbeti
    }

    [Fact]
    public async Task Recall_experiment_rule_mode_is_deterministic_and_hybrid_is_skipped_in_mock()
    {
        await using var sp = Services(new FakeCatalog(MockProvider), new ScriptedJsonModel(_ => NoSuggestions, configured: false));

        var first = await InScopeAsync(sp, s => s.GetRequiredService<MissingWorkRecallService>().RunAsync(null, _ct));
        var second = await InScopeAsync(sp, s => s.GetRequiredService<MissingWorkRecallService>().RunAsync(null, _ct));

        Assert.Equal(first.Removed.Select(r => r.Name), second.Removed.Select(r => r.Name));
        Assert.Equal(4, first.Removed.Count(r => r.Group == RemovedGroup.Template));
        Assert.Equal(4, first.Removed.Count(r => r.Group == RemovedGroup.Other));
        // Şablon işi bir şablonun tek karşılığı olduğundan kural onu her zaman bulur; projeye özgü işi tanımı gereği bulamaz.
        Assert.Equal(1m, first.Rule.Template.Recall);
        Assert.Equal(0m, first.Rule.Other.Recall);
        Assert.Equal(0.5m, first.Rule.All.Recall);
        Assert.Empty(first.Hybrid);
        Assert.Equal(MissingWorkRecallService.MockNote, first.Note);
        Assert.Empty(await Db.EvaluationRuns.ToListAsync(_ct));
        Assert.Equal(1, await Db.Projects.CountAsync(_ct));   // demo bir kez oluşturuldu, işler değişmedi
        Assert.Equal(DemoScenario.Tasks.Count + DemoScenario.AddedScope.Count, await Db.WorkItems.CountAsync(_ct));

        var csv = first.ToCsv();
        var lines = csv.Content.TrimStart(Csv.Bom).Split(Csv.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(string.Join(',', RecallExperimentReport.CsvHeader), lines[0]);
        Assert.Equal(2, lines.Length);
    }

    [Fact]
    public async Task Recall_experiment_hybrid_mode_recovers_project_specific_items_and_is_tagged()
    {
        // Çıkarılacak işler deterministik: önce aynı seçimle "özgü" işlerin adları bulunur, sahte LLM onları önerir.
        await using var probe = Services(new FakeCatalog(MockProvider), new ScriptedJsonModel(_ => NoSuggestions, configured: false));
        var mock = await InScopeAsync(probe, s => s.GetRequiredService<MissingWorkRecallService>().RunAsync(null, _ct));
        var others = mock.Removed.Where(r => r.Group == RemovedGroup.Other).Select(r => r.Name).ToList();
        var json = JsonSerializer.Serialize(new
        {
            suggestions = others.Select(n => new { name = n, phase = "Development", skill = "Backend", reason = "Eksik görünüyor.", size = "M" })
        });

        var llm = new ScriptedJsonModel(_ => json, providerKey: "Fake/llm");
        await using var sp = Services(new FakeCatalog(MockProvider), llm, new EvaluationOptions { RecallRepeats = 2 });
        var report = await InScopeAsync(sp, s => s.GetRequiredService<MissingWorkRecallService>().RunAsync(null, _ct));
        Db.ChangeTracker.Clear();

        Assert.Equal(2, report.Hybrid.Count);
        Assert.All(report.Hybrid, h =>
        {
            Assert.Equal(LlmLayerStatus.Success, h.Status);
            Assert.Equal(1m, h.Score.Other.Recall);
            Assert.Equal(1m, h.Score.All.Recall);
        });
        Assert.Equal(1m, report.HybridRecall(s => s.All).Mean);
        Assert.Equal(0m, report.HybridRecall(s => s.All).StdDev);
        Assert.Equal(0m, report.Rule.Other.Recall);

        var run = await Db.EvaluationRuns.SingleAsync(_ct);
        Assert.Equal((EvaluationRunKind.MissingWorkRecall, EvaluationRunStatus.Completed, "Fake/llm", 2),
            (run.Kind, run.Status, run.Providers, run.Repeats));
        var logs = await Db.AiAnalysisLogs.ToListAsync(_ct);
        Assert.Equal(2, logs.Count);
        Assert.All(logs, l => Assert.Equal((run.Id, MissingWorkRecallService.TaskId), (l.EvaluationRunId!.Value, l.EvaluationTask!)));
        // Deney kayıtları kullanıcı kartlarının kaynak eşleşmesine girmez; veritabanındaki işler değişmez.
        Assert.Empty(await MissingWorkService.LatestLlmSuggestionNamesAsync(Db, report.ProjectId, _ct));
        Assert.Equal(DemoScenario.Tasks.Count + DemoScenario.AddedScope.Count, await Db.WorkItems.CountAsync(_ct));
        // Model yalnız kısaltılmış listeyi gördü.
        Assert.All(llm.Requests, r => Assert.DoesNotContain(others[0], r.UserMessage));
    }
}
