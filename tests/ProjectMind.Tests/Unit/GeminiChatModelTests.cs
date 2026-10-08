using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectMind.Application.Ai;
using ProjectMind.Domain.Enums;
using ProjectMind.Infrastructure.Ai;

namespace ProjectMind.Tests.Unit;

/// <summary>Gemini araç döngüsü, gerçek ağa çıkmadan sahte HTTP cevaplarıyla test edilir.</summary>
public class GeminiChatModelTests
{
    private sealed class FakeHandler(params (HttpStatusCode Status, string Body)[] responses) : HttpMessageHandler
    {
        private int _index;
        public List<(string Url, string? ApiKey, JsonNode Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.RequestUri!.ToString(),
                request.Headers.TryGetValues("x-goog-api-key", out var k) ? k.Single() : null,
                request.Content is null ? new JsonObject() : JsonNode.Parse(await request.Content.ReadAsStringAsync(ct))!));
            var (status, body) = responses[_index++];
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class RecordingExecutor : IChatToolExecutor
    {
        public List<(string Name, JsonElement Input)> Calls { get; } = [];

        public Task<ToolExecutionResult> ExecuteAsync(string toolName, JsonElement input, CancellationToken ct)
        {
            Calls.Add((toolName, input));
            return Task.FromResult(new ToolExecutionResult("Öneri #1 oluşturuldu", false));
        }
    }

    public GeminiChatModelTests() => GeminiChatModel.ResetModelCache();

    private static GeminiChatModel Model(FakeHandler handler) => new(
        new HttpClient(handler),
        new AiOptions { Gemini = new() { Model = "gemini-test", ApiKey = "KEY" }, RetryDelayMs = 0 },
        NullLogger<GeminiChatModel>.Instance);

    private static ChatTurnRequest Request() => new(
        "sistem", [new ChatHistoryItem(ChatRole.User, "önceki"), new ChatHistoryItem(ChatRole.Assistant, "cevap")],
        "proje aç", AiTools.All);

    private const string FunctionCallResponse = """
        {"candidates":[{"content":{"role":"model","parts":[
          {"functionCall":{"name":"create_project","args":{"name":"X","startDate":"2026-11-01","targetEndDate":"2027-01-01"}},
           "thoughtSignature":"SIG"}]}}]}
        """;

    private const string TextResponse = """
        {"candidates":[{"content":{"role":"model","parts":[
          {"text":"düşünce","thought":true},{"text":"Projeyi önerdim."}]}}]}
        """;

    [Fact]
    public async Task Tool_call_is_executed_and_result_sent_back_with_model_content_preserved()
    {
        var handler = new FakeHandler((HttpStatusCode.OK, FunctionCallResponse), (HttpStatusCode.OK, TextResponse));
        var executor = new RecordingExecutor();

        var result = await Model(handler).CompleteTurnAsync(Request(), executor, CancellationToken.None);

        Assert.Equal("Projeyi önerdim.", result.Text.Trim());   // "thought" parçası cevaba girmez
        Assert.Equal("gemini-test", result.Model);
        Assert.Equal("create_project", executor.Calls.Single().Name);
        Assert.Equal("X", executor.Calls.Single().Input.GetProperty("name").GetString());

        var first = handler.Requests[0];
        Assert.EndsWith("models/gemini-test:generateContent", first.Url);
        Assert.Equal("KEY", first.ApiKey);
        Assert.Equal("sistem", first.Body["systemInstruction"]!["parts"]![0]!["text"]!.GetValue<string>());
        Assert.Equal(["user", "model", "user"], first.Body["contents"]!.AsArray().Select(c => c!["role"]!.GetValue<string>()));
        Assert.Equal(AiTools.All.Count, first.Body["tools"]![0]!["functionDeclarations"]!.AsArray().Count);

        var second = handler.Requests[1].Body["contents"]!.AsArray();
        Assert.Equal("SIG", second[3]!["parts"]![0]!["thoughtSignature"]!.GetValue<string>());
        var functionResponse = second[4]!["parts"]![0]!["functionResponse"]!;
        Assert.Equal("create_project", functionResponse["name"]!.GetValue<string>());
        Assert.Equal("Öneri #1 oluşturuldu", functionResponse["response"]!["result"]!.GetValue<string>());
    }

    [Fact]
    public async Task Empty_required_arrays_are_removed_from_declarations()
    {
        var handler = new FakeHandler((HttpStatusCode.OK, TextResponse));
        await Model(handler).CompleteTurnAsync(Request(), new RecordingExecutor(), CancellationToken.None);

        var updateProject = handler.Requests[0].Body["tools"]![0]!["functionDeclarations"]!.AsArray()
            .Single(d => d!["name"]!.GetValue<string>() == AiTools.UpdateProject)!;
        Assert.Null(updateProject["parameters"]!["required"]);

        var checkMissing = handler.Requests[0].Body["tools"]![0]!["functionDeclarations"]!.AsArray()
            .Single(d => d!["name"]!.GetValue<string>() == AiTools.CheckMissingWork)!;
        Assert.Null(checkMissing["parameters"]);   // parametresiz araçta şema gönderilmez
    }

    [Fact]
    public async Task Retired_model_falls_back_to_newest_available_flash_model()
    {
        const string models = """
            {"models":[
              {"name":"models/gemini-3.5-flash","supportedGenerationMethods":["generateContent"]},
              {"name":"models/gemini-4.0-flash-lite","supportedGenerationMethods":["generateContent"]},
              {"name":"models/gemini-4.0-flash-preview","supportedGenerationMethods":["generateContent"]},
              {"name":"models/gemini-4.0-flash","supportedGenerationMethods":["generateContent","countTokens"]},
              {"name":"models/text-embedding-004","supportedGenerationMethods":["embedContent"]}]}
            """;
        var handler = new FakeHandler(
            (HttpStatusCode.NotFound, """{"error":{"code":404,"message":"no longer available"}}"""),
            (HttpStatusCode.OK, models),
            (HttpStatusCode.OK, TextResponse));

        var result = await Model(handler).CompleteTurnAsync(Request(), new RecordingExecutor(), CancellationToken.None);

        Assert.Equal("gemini-4.0-flash", result.Model);
        Assert.Contains("/models?", handler.Requests[1].Url);
        Assert.EndsWith("models/gemini-4.0-flash:generateContent", handler.Requests[2].Url);
    }

    /// <summary>Hiç cevap vermeyen sunucu: istek ancak iptal/zaman aşımı ile biter.</summary>
    private sealed class HangingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("ulaşılmaz");
        }
    }

    private static GeminiChatModel HangingModel() => new(
        new HttpClient(new HangingHandler()) { Timeout = TimeSpan.FromMilliseconds(50) },
        new AiOptions { Gemini = new() { Model = "gemini-test", ApiKey = "KEY" }, RetryDelayMs = 0 },
        NullLogger<GeminiChatModel>.Instance);

    [Fact]
    public async Task Http_timeout_becomes_friendly_chat_model_exception()
    {
        var ex = await Assert.ThrowsAsync<ChatModelException>(() =>
            HangingModel().CompleteTurnAsync(Request(), new RecordingExecutor(), CancellationToken.None));

        Assert.Equal(ChatModelMessages.Timeout, ex.Message);
    }

    [Fact]
    public async Task User_cancellation_is_not_reported_as_timeout()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(10));
        var model = new GeminiChatModel(new HttpClient(new HangingHandler()),
            new AiOptions { Gemini = new() { Model = "gemini-test", ApiKey = "KEY" } }, NullLogger<GeminiChatModel>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            model.CompleteTurnAsync(Request(), new RecordingExecutor(), cts.Token));
    }

    private const string Overloaded = """{"error":{"code":503,"message":"high demand","status":"UNAVAILABLE"}}""";

    [Fact]
    public async Task Temporary_overload_is_retried_and_succeeds()
    {
        var handler = new FakeHandler((HttpStatusCode.ServiceUnavailable, Overloaded), (HttpStatusCode.OK, TextResponse));

        var result = await Model(handler).CompleteTurnAsync(Request(), new RecordingExecutor(), CancellationToken.None);

        Assert.Equal("Projeyi önerdim.", result.Text.Trim());
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.EndsWith("models/gemini-test:generateContent", r.Url));
    }

    [Fact]
    public async Task Persistent_overload_switches_to_another_model_for_this_turn()
    {
        const string models = """
            {"models":[
              {"name":"models/gemini-test","supportedGenerationMethods":["generateContent"]},
              {"name":"models/gemini-9-flash-lite","supportedGenerationMethods":["generateContent"]}]}
            """;
        var handler = new FakeHandler(
            (HttpStatusCode.ServiceUnavailable, Overloaded), (HttpStatusCode.ServiceUnavailable, Overloaded),
            (HttpStatusCode.ServiceUnavailable, Overloaded),          // 1 deneme + 2 tekrar
            (HttpStatusCode.OK, models),
            (HttpStatusCode.OK, TextResponse));

        var result = await Model(handler).CompleteTurnAsync(Request(), new RecordingExecutor(), CancellationToken.None);

        Assert.Equal("gemini-9-flash-lite", result.Model);
        Assert.EndsWith("models/gemini-9-flash-lite:generateContent", handler.Requests[4].Url);
    }

    [Fact]
    public async Task Overload_without_alternative_gives_friendly_message()
    {
        var handler = new FakeHandler(
            (HttpStatusCode.ServiceUnavailable, Overloaded), (HttpStatusCode.ServiceUnavailable, Overloaded),
            (HttpStatusCode.ServiceUnavailable, Overloaded), (HttpStatusCode.OK, """{"models":[]}"""));

        var ex = await Assert.ThrowsAsync<ChatModelException>(() =>
            Model(handler).CompleteTurnAsync(Request(), new RecordingExecutor(), CancellationToken.None));
        Assert.Contains("çok yoğun", ex.Message);
    }

    [Fact]
    public async Task Invalid_key_becomes_friendly_error()
    {
        var handler = new FakeHandler((HttpStatusCode.BadRequest, """{"error":{"status":"INVALID_ARGUMENT","details":[{"reason":"API_KEY_INVALID"}]}}"""));

        var ex = await Assert.ThrowsAsync<ChatModelException>(() =>
            Model(handler).CompleteTurnAsync(Request(), new RecordingExecutor(), CancellationToken.None));
        Assert.Contains("anahtarı geçersiz", ex.Message);
    }
}
