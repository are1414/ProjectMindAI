using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectMind.Application.Ai;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Tests.Integration;

public class ChatServiceTests : ServiceTestBase
{
    private readonly CancellationToken _ct = CancellationToken.None;

    /// <summary>LLM yerine senaryolu sahte model: önceden belirlenen araç çağrılarını yapar.</summary>
    private sealed class ScriptedModel(params (string Tool, object Input)[] calls) : ChatOnlyModel
    {
        public ChatTurnRequest? LastRequest { get; private set; }
        public List<ToolExecutionResult> Results { get; } = [];

        public override async Task<ChatTurnResult> CompleteTurnAsync(ChatTurnRequest request, IChatToolExecutor tools, CancellationToken ct)
        {
            LastRequest = request;
            foreach (var (tool, input) in calls)
                Results.Add(await tools.ExecuteAsync(tool, JsonSerializer.SerializeToElement(input), ct));
            return new ChatTurnResult("Önerilerimi hazırladım.", "scripted");
        }
    }

    [Fact]
    public async Task Chat_turn_is_saved_and_tool_calls_become_pending_actions_linked_to_reply()
    {
        var model = new ScriptedModel(
            (AiTools.CreateProject, new { name = "E-ticaret", startDate = "2026-12-01", targetEndDate = "2027-03-31" }),
            (AiTools.AddWorkItem, new { name = "Ödeme entegrasyonu", phase = "Development", requiredSkill = "Backend", estimatedHours = 60 }));
        var chat = NewChatService(model);
        var session = await chat.CreateSessionAsync(_ct);

        var reply = await chat.SendAsync(session.Id, "E-ticaret projesi açalım", _ct);

        var messages = await chat.ListMessagesAsync(session.Id, _ct);
        Assert.Equal([ChatRole.User, ChatRole.Assistant], messages.Select(m => m.Role));
        Assert.Equal("Önerilerimi hazırladım.", reply.Content);

        var actions = await NewActionService().ListAsync(session.Id, _ct);
        Assert.Equal(2, actions.Count);
        Assert.All(actions, a => Assert.Equal(reply.Id, a.ChatMessageId));
        Assert.All(model.Results, r => Assert.False(r.IsError));

        var saved = await Db.ChatMessages.AsNoTracking().SingleAsync(m => m.Id == reply.Id);
        Assert.Equal("scripted", saved.Model);
        Assert.Equal(ChatPrompts.Version, saved.PromptVersion);
    }

    [Fact]
    public async Task Context_is_sent_with_message_and_invalid_tool_call_is_reported_back_to_model()
    {
        var model = new ScriptedModel(
            (AiTools.AddWorkItem, new { name = "Projesiz iş", phase = "Development", requiredSkill = "Backend", estimatedHours = 8 }));
        var chat = NewChatService(model);
        var session = await chat.CreateSessionAsync(_ct);

        await chat.SendAsync(session.Id, "Bir iş ekle", _ct);

        Assert.Contains("<proje_durumu>", model.LastRequest!.UserMessage);
        Assert.Contains("\"project\":null", model.LastRequest.UserMessage);
        Assert.True(model.Results.Single().IsError);
        Assert.Empty(await NewActionService().ListAsync(session.Id, _ct));
    }

    [Fact]
    public async Task Model_error_is_saved_as_friendly_assistant_message()
    {
        var chat = NewChatService(new FailingModel());
        var session = await chat.CreateSessionAsync(_ct);

        var reply = await chat.SendAsync(session.Id, "Merhaba", _ct);

        Assert.Equal("AI servisine bağlanılamadı.", reply.Content);
    }

    [Fact]
    public async Task Clear_history_keeps_session_and_project_but_removes_messages_and_actions()
    {
        var chat = NewChatService(new ScriptedModel(
            (AiTools.CreateProject, new { name = "P", startDate = "2026-12-01", targetEndDate = "2027-03-31" })));
        var session = await chat.CreateSessionAsync(_ct);
        await chat.SendAsync(session.Id, "Proje aç", _ct);
        await NewActionService().ApplyAllPendingAsync(session.Id, _ct);

        await chat.ClearHistoryAsync(session.Id, _ct);

        Assert.Empty(await chat.ListMessagesAsync(session.Id, _ct));
        Assert.Empty(await NewActionService().ListAsync(session.Id, _ct));
        Assert.NotNull((await chat.GetSessionAsync(session.Id, _ct)).ProjectId);
        Assert.Single(Db.Projects);
    }

    [Fact]
    public async Task Project_chat_cannot_be_deleted_without_its_project_but_can_with_it()
    {
        var chat = NewChatService(new ScriptedModel(
            (AiTools.CreateProject, new { name = "P", startDate = "2026-12-01", targetEndDate = "2027-03-31" }),
            (AiTools.AddWorkItem, new { name = "İş", phase = "Development", requiredSkill = "Backend", estimatedHours = 8 })));
        var session = await chat.CreateSessionAsync(_ct);
        await chat.SendAsync(session.Id, "Proje aç", _ct);
        await NewActionService().ApplyAllPendingAsync(session.Id, _ct);

        await Assert.ThrowsAsync<ProjectMind.Application.Common.BusinessRuleException>(() =>
            chat.DeleteSessionAsync(session.Id, deleteProject: false, _ct));

        await chat.DeleteSessionAsync(session.Id, deleteProject: true, _ct);

        Assert.Empty(Db.ChatSessions);
        Assert.Empty(Db.ChatMessages);
        Assert.Empty(Db.AiActions);
        Assert.Empty(Db.Projects);
        Assert.Empty(Db.WorkItems);
    }

    [Fact]
    public async Task Draft_chat_can_be_deleted()
    {
        var chat = NewChatService(new ScriptedModel());
        var session = await chat.CreateSessionAsync(_ct);
        await chat.SendAsync(session.Id, "Merhaba", _ct);

        await chat.DeleteSessionAsync(session.Id, deleteProject: false, _ct);

        Assert.Empty(Db.ChatSessions);
        Assert.Empty(Db.ChatMessages);
    }

    [Fact]
    public async Task Missing_work_check_returns_rule_based_result_without_creating_cards()
    {
        var chat = NewChatService(new ScriptedModel(
            (AiTools.CreateProject, new { name = "Web", type = "WebApplication", startDate = "2026-12-01", targetEndDate = "2027-03-31" }),
            (AiTools.AddWorkItem, new { name = "Backend API", phase = "Development", requiredSkill = "Backend", estimatedHours = 40 })));
        var session = await chat.CreateSessionAsync(_ct);
        await chat.SendAsync(session.Id, "Proje aç", _ct);
        await NewActionService().ApplyAllPendingAsync(session.Id, _ct);

        var checker = new ScriptedModel((AiTools.CheckMissingWork, new { }));
        await NewChatService(checker).SendAsync(session.Id, "Eksik iş var mı?", _ct);

        var result = checker.Results.Single();
        Assert.False(result.IsError);
        Assert.Contains("Veritabanı kurulumu", result.Content);
        Assert.Contains("Backend API", result.Content);        // mustFinishBefore önerisi
        Assert.DoesNotContain("Mobil uygulama", result.Content);
        Assert.All(await NewActionService().ListAsync(session.Id, _ct), a => Assert.NotEqual(AiActionStatus.Pending, a.Status));
    }

    [Fact]
    public async Task Number_flagged_in_previous_answer_is_still_flagged_when_repeated()
    {
        var chat = NewChatService(new TextModel("Kalan efor 137 saat."));
        var session = await chat.CreateSessionAsync(_ct);

        var first = await chat.SendAsync(session.Id, "Kalan efor ne kadar?", _ct);
        var second = await chat.SendAsync(session.Id, "Emin misin?", _ct);

        Assert.Contains("Doğrulanamayan sayılar: 137", first.Content);
        Assert.Contains("Doğrulanamayan sayılar: 137", second.Content);   // önceki asistan cevabı kanıt değil
    }

    [Fact]
    public async Task Number_given_by_user_in_previous_message_is_accepted()
    {
        var chat = NewChatService(new TextModel("Hedef 137 saat olarak not edildi."));
        var session = await chat.CreateSessionAsync(_ct);

        await chat.SendAsync(session.Id, "Hedef efor 137 saat olsun.", _ct);
        var second = await chat.SendAsync(session.Id, "Tekrar eder misin?", _ct);

        Assert.DoesNotContain("Doğrulanamayan", second.Content);
    }

    [Fact]
    public async Task Unconverted_timeout_is_saved_as_friendly_assistant_message()
    {
        var chat = NewChatService(new TimingOutModel());
        var session = await chat.CreateSessionAsync(_ct);

        var reply = await chat.SendAsync(session.Id, "Merhaba", _ct);

        Assert.Equal(ChatModelMessages.Timeout, reply.Content);
        Assert.Equal([ChatRole.User, ChatRole.Assistant], (await chat.ListMessagesAsync(session.Id, _ct)).Select(m => m.Role));
    }

    private sealed class TimingOutModel : ChatOnlyModel
    {
        public override Task<ChatTurnResult> CompleteTurnAsync(ChatTurnRequest request, IChatToolExecutor tools, CancellationToken ct) =>
            throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout");
    }

    private sealed class TextModel(string text) : ChatOnlyModel
    {
        public override Task<ChatTurnResult> CompleteTurnAsync(ChatTurnRequest request, IChatToolExecutor tools, CancellationToken ct) =>
            Task.FromResult(new ChatTurnResult(text, "text"));
    }

    private sealed class FailingModel : ChatOnlyModel
    {
        public override Task<ChatTurnResult> CompleteTurnAsync(ChatTurnRequest request, IChatToolExecutor tools, CancellationToken ct) =>
            throw new ChatModelException("AI servisine bağlanılamadı.");
    }

    [Fact]
    public async Task Every_chat_turn_is_audited_with_tools_and_unverified_numbers()
    {
        var chat = NewChatService(new ScriptedModel(
            (AiTools.CreateProject, new { name = "E-ticaret", startDate = "2026-12-01", targetEndDate = "2027-03-31" }),
            (AiTools.AddWorkItem, new { name = "Ödeme", phase = "Development", requiredSkill = "Backend", estimatedHours = 60 })));
        var session = await chat.CreateSessionAsync(_ct);
        var first = await chat.SendAsync(session.Id, "Proje aç", _ct);

        var flagged = await NewChatService(new TextModel("Kalan efor 137 saat, 245 saat değil.")).SendAsync(session.Id, "Kalan?", _ct);
        var failed = await NewChatService(new FailingModel()).SendAsync(session.Id, "Merhaba", _ct);

        var logs = await Db.AiAnalysisLogs.AsNoTracking().OrderBy(l => l.Id).ToListAsync(_ct);
        Assert.Equal(3, logs.Count);
        Assert.All(logs, l =>
        {
            Assert.Equal(AiAnalysisKind.Chat, l.Kind);
            Assert.Equal(ChatPrompts.Version, l.PromptVersion);
            Assert.Equal(session.Id, l.ChatSessionId);
            Assert.True(l.ContextLength > 0);
            Assert.Null(l.SchemaValid);   // sohbet cevabı serbest metin
            Assert.Null(l.ResultJson);
        });

        Assert.Equal(first.Id, logs[0].ChatMessageId);
        Assert.Equal("create_project,add_work_item", logs[0].ToolsCalled);
        Assert.Equal("scripted", logs[0].Model);
        Assert.Equal(0, logs[0].UnverifiedNumberCount);

        Assert.Equal(flagged.Id, logs[1].ChatMessageId);
        Assert.Null(logs[1].ToolsCalled);
        Assert.Equal(2, logs[1].UnverifiedNumberCount);                     // NumberGuard sonucu ile aynı
        Assert.Contains("Doğrulanamayan sayılar: 137, 245", flagged.Content);
        Assert.Equal("""["137","245"]""", logs[1].UnverifiedNumbers);
        Assert.Equal(AiAnalysisOutcome.Success, logs[1].Outcome);

        Assert.Equal(failed.Id, logs[2].ChatMessageId);
        Assert.Equal(AiAnalysisOutcome.ModelError, logs[2].Outcome);
        Assert.Equal("AI servisine bağlanılamadı.", logs[2].ErrorMessage);
    }
}
