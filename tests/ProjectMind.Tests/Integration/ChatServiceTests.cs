using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectMind.Application.Ai;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Tests.Integration;

public class ChatServiceTests : ServiceTestBase
{
    private readonly CancellationToken _ct = CancellationToken.None;

    /// <summary>LLM yerine senaryolu sahte model: önceden belirlenen araç çağrılarını yapar.</summary>
    private sealed class ScriptedModel(params (string Tool, object Input)[] calls) : IChatModel
    {
        public ChatTurnRequest? LastRequest { get; private set; }
        public List<ToolExecutionResult> Results { get; } = [];

        public async Task<ChatTurnResult> CompleteTurnAsync(ChatTurnRequest request, IChatToolExecutor tools, CancellationToken ct)
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

    private sealed class FailingModel : IChatModel
    {
        public Task<ChatTurnResult> CompleteTurnAsync(ChatTurnRequest request, IChatToolExecutor tools, CancellationToken ct) =>
            throw new ChatModelException("AI servisine bağlanılamadı.");
    }
}
