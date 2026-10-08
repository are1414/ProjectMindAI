using ProjectMind.Application.Ai;

namespace ProjectMind.Tests.Integration;

/// <summary>
/// Yalnız sohbet turu senaryolayan sahte modellerin tabanı. JSON çağrısı (ör. eksik iş LLM katmanı) model hatası gibi
/// davranır; böylece hibrit katmanın "LLM hatasında kural sonucu yine döner" yolu çalışır.
/// </summary>
public abstract class ChatOnlyModel : IChatModel
{
    public const string JsonUnsupportedMessage = "Bu sahte model yalnız sohbet turu içindir.";

    public bool IsConfigured => true;

    public abstract Task<ChatTurnResult> CompleteTurnAsync(ChatTurnRequest request, IChatToolExecutor tools, CancellationToken ct);

    public Task<ChatJsonResult> CompleteJsonAsync(ChatJsonRequest request, CancellationToken ct) =>
        throw new ChatModelException(JsonUnsupportedMessage);
}

/// <summary>JSON çağrısında senaryolu cevap döner; cevap isteğe (analiz verisine) bakılarak üretilebilir.</summary>
public sealed class ScriptedJsonModel(Func<ChatJsonRequest, string> respond, bool configured = true) : IChatModel
{
    public List<ChatJsonRequest> Requests { get; } = [];

    public bool IsConfigured => configured;

    public Task<ChatTurnResult> CompleteTurnAsync(ChatTurnRequest request, IChatToolExecutor tools, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<ChatJsonResult> CompleteJsonAsync(ChatJsonRequest request, CancellationToken ct)
    {
        Requests.Add(request);
        return Task.FromResult(new ChatJsonResult(respond(request), "scripted-json"));
    }
}
