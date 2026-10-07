using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectMind.Application.Ai;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Infrastructure.Ai;

/// <summary>
/// Google Gemini (generateContent REST API) ile bir sohbet turu yürütür. Model functionCall döndükçe araçlar
/// executor ile çalıştırılır, sonuç functionResponse olarak geri gönderilir; model metinle bitirene kadar döner.
/// Modelin içeriği (thoughtSignature dahil) olduğu gibi geri eklenir.
/// </summary>
public sealed class GeminiChatModel(HttpClient http, IOptions<AiOptions> options, ILogger<GeminiChatModel> logger) : IChatModel
{
    public const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/";

    public async Task<ChatTurnResult> CompleteTurnAsync(ChatTurnRequest request, IChatToolExecutor tools, CancellationToken ct)
    {
        var o = options.Value;
        var model = o.Gemini.Model;

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

            var response = await SendAsync(model, o.Gemini.ApiKey, body, ct);
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
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Gemini hata {Status}: {Body}", (int)response.StatusCode, text);
                throw new ChatModelException(response.StatusCode switch
                {
                    HttpStatusCode.BadRequest when text.Contains("API_KEY_INVALID") =>
                        "Gemini API anahtarı geçersiz. appsettings.Local.json içindeki 'AI:Gemini:ApiKey' değerini kontrol edin.",
                    HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized =>
                        "Gemini API anahtarının bu modele erişimi yok.",
                    HttpStatusCode.NotFound =>
                        $"Gemini modeli bulunamadı: '{model}'. appsettings 'AI:Gemini:Model' değerini kontrol edin.",
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
