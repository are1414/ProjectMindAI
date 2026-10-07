using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ProjectMind.Application.Ai;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Infrastructure.Ai;

/// <summary>
/// Google Gemini (generateContent REST API) ile bir sohbet turu yürütür. Model functionCall döndükçe araçlar
/// executor ile çalıştırılır, sonuç functionResponse olarak geri gönderilir; model metinle bitirene kadar döner.
/// Modelin içeriği (thoughtSignature dahil) olduğu gibi geri eklenir.
/// </summary>
public sealed class GeminiChatModel(HttpClient http, AiOptions options, ILogger<GeminiChatModel> logger) : IChatModel
{
    public const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/";
    private const string PreferredFamily = "flash";

    // Ayarlanan model kapatılmışsa bulunan yedek model; uygulama yeniden başlayana kadar tekrar aranmaz.
    private static string? _resolvedFallbackModel;

    public async Task<ChatTurnResult> CompleteTurnAsync(ChatTurnRequest request, IChatToolExecutor tools, CancellationToken ct)
    {
        var o = options;
        var model = _resolvedFallbackModel ?? o.Gemini.Model;

        var contents = new JsonArray();
        foreach (var h in request.History)
            contents.Add(TextContent(h.Role == ChatRole.User ? "user" : "model", h.Content));
        contents.Add(TextContent("user", request.UserMessage));

        var functionDeclarations = new JsonArray(request.Tools.Select(ToDeclaration).ToArray<JsonNode?>());
        var reply = new StringBuilder();

        for (var round = 0; round < o.MaxToolRounds; round++)
        {
            var body = new JsonObject
            {
                ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = request.SystemPrompt }) },
                ["contents"] = contents.DeepClone(),
                ["tools"] = new JsonArray(new JsonObject { ["functionDeclarations"] = functionDeclarations.DeepClone() }),
                ["generationConfig"] = new JsonObject { ["maxOutputTokens"] = o.MaxTokens }
            };

            JsonNode response;
            try
            {
                response = await SendAsync(model, o.Gemini.ApiKey, body, ct);
            }
            catch (ModelNotFoundException)
            {
                var fallback = await FindAvailableModelAsync(o.Gemini.ApiKey, ct)
                    ?? throw new ChatModelException(
                        $"Gemini modeli bulunamadı: '{model}' ve uygun bir yedek model de bulunamadı. " +
                        "appsettings 'AI:Gemini:Model' değerini AI Studio'daki güncel bir model adıyla değiştirin.");
                logger.LogWarning("Gemini modeli '{Model}' kullanılamıyor; '{Fallback}' modeline geçildi", model, fallback);
                _resolvedFallbackModel = model = fallback;
                response = await SendAsync(model, o.Gemini.ApiKey, body, ct);
            }
            var candidate = response["candidates"]?.AsArray().FirstOrDefault();
            var content = candidate?["content"];
            if (content?["parts"] is not JsonArray parts)
            {
                var reason = candidate?["finishReason"]?.GetValue<string>() ?? response["promptFeedback"]?["blockReason"]?.GetValue<string>();
                logger.LogWarning("Gemini boş cevap döndü: {Reason}", reason);
                return new ChatTurnResult("Bu isteği işleyemedim. Lütfen farklı şekilde ifade edin.", model);
            }

            contents.Add(content.DeepClone());

            var functionResponses = new JsonArray();
            foreach (var part in parts.OfType<JsonObject>())
            {
                if (part["functionCall"] is JsonObject call)
                {
                    var name = call["name"]?.GetValue<string>() ?? "";
                    var args = call["args"] is JsonNode a ? JsonSerializer.SerializeToElement(a) : JsonSerializer.SerializeToElement(new { });
                    var result = await tools.ExecuteAsync(name, args, ct);

                    var functionResponse = new JsonObject
                    {
                        ["name"] = name,
                        ["response"] = new JsonObject { [result.IsError ? "error" : "result"] = result.Content }
                    };
                    if (call["id"] is JsonNode id)
                        functionResponse["id"] = id.DeepClone();
                    functionResponses.Add(new JsonObject { ["functionResponse"] = functionResponse });
                }
                else if (part["text"] is JsonNode text && part["thought"]?.GetValue<bool>() != true)
                {
                    reply.AppendLine(text.GetValue<string>());
                }
            }

            if (functionResponses.Count == 0)
                return new ChatTurnResult(reply.ToString(), model);

            contents.Add(new JsonObject { ["role"] = "user", ["parts"] = functionResponses });
        }

        logger.LogWarning("Araç turu sınırına ({Max}) ulaşıldı", o.MaxToolRounds);
        reply.AppendLine("(İşlem çok uzun sürdü; şu ana kadarki önerileri kontrol edin.)");
        return new ChatTurnResult(reply.ToString(), model);
    }

    private async Task<JsonNode> SendAsync(string model, string? apiKey, JsonObject body, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}models/{model}:generateContent")
        {
            Content = JsonContent.Create(body)
        };
        message.Headers.Add("x-goog-api-key", apiKey);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(message, ct);
        }
        catch (HttpRequestException ex)
        {
            throw Fail(ex, "AI servisine bağlanılamadı. İnternet bağlantısını kontrol edin.");
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogWarning("Gemini modeli bulunamadı ({Model}): {Body}", model, text);
                throw new ModelNotFoundException();
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Gemini hata {Status}: {Body}", (int)response.StatusCode, text);
                throw new ChatModelException(response.StatusCode switch
                {
                    HttpStatusCode.BadRequest when text.Contains("API_KEY_INVALID") =>
                        "Gemini API anahtarı geçersiz. ⚙ Ayarlar sayfasından anahtarı kontrol edip yeniden kaydedin.",
                    HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized =>
                        "Gemini API anahtarının bu modele erişimi yok.",
                    HttpStatusCode.TooManyRequests =>
                        "Gemini kullanım limiti doldu veya çok fazla istek gönderildi. Biraz sonra tekrar deneyin.",
                    _ => "AI servisinden beklenmeyen bir hata döndü. Ayrıntı loglarda."
                });
            }

            logger.LogInformation("Gemini turu: model {Model}, kullanım {Usage}", model,
                JsonNode.Parse(text)?["usageMetadata"]?.ToJsonString());
            return JsonNode.Parse(text) ?? throw new ChatModelException("AI servisinden boş cevap geldi.");
        }
    }

    /// <summary>
    /// ListModels ile generateContent destekleyen, sohbete uygun en yeni "flash" modelini bulur
    /// (lite / görüntü / ses / canlı / önizleme sürümleri tercih edilmez).
    /// </summary>
    private async Task<string?> FindAvailableModelAsync(string? apiKey, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}models?pageSize=1000");
        message.Headers.Add("x-goog-api-key", apiKey);
        using var response = await http.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var models = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct))?["models"]?.AsArray() ?? [];
        string[] excluded = ["lite", "image", "tts", "audio", "live", "embedding", "preview", "exp"];
        return models
            .Where(m => m?["supportedGenerationMethods"]?.AsArray()
                .Any(x => x?.GetValue<string>() == "generateContent") == true)
            .Select(m => m!["name"]!.GetValue<string>().Replace("models/", ""))
            .Where(n => n.StartsWith("gemini-") && n.Contains(PreferredFamily) && !excluded.Any(n.Contains))
            .OrderByDescending(n => n, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public static void ResetModelCache() => _resolvedFallbackModel = null;

    private sealed class ModelNotFoundException : Exception;

    private ChatModelException Fail(Exception ex, string message)
    {
        logger.LogError(ex, "Gemini çağrısı başarısız");
        return new ChatModelException(message, ex);
    }

    private static JsonObject TextContent(string role, string text) => new()
    {
        ["role"] = role,
        ["parts"] = new JsonArray(new JsonObject { ["text"] = text })
    };

    private static JsonObject ToDeclaration(ChatToolDefinition d)
    {
        var parameters = JsonNode.Parse(d.InputSchema.GetRawText())!.AsObject();
        // Gemini boş "required" dizisini kabul etmeyebiliyor; boşsa kaldır.
        if (parameters["required"] is JsonArray { Count: 0 })
            parameters.Remove("required");
        return new JsonObject { ["name"] = d.Name, ["description"] = d.Description, ["parameters"] = parameters };
    }
}
