using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using ProjectMind.Application.Ai;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Infrastructure.Ai;

/// <summary>
/// Claude (Anthropic Messages API) ile bir sohbet turu yürütür: model araç çağırdıkça executor'a iletir,
/// sonucu geri gönderir ve model metin cevabıyla bitirene kadar döner.
/// </summary>
public sealed class ClaudeChatModel(AiOptions options, ILogger<ClaudeChatModel> logger) : IChatModel
{
    public async Task<ChatTurnResult> CompleteTurnAsync(ChatTurnRequest request, IChatToolExecutor tools, CancellationToken ct)
    {
        var o = options;
        AnthropicClient client = new() { ApiKey = o.Claude.ApiKey };

        List<MessageParam> messages = request.History
            .Select(h => new MessageParam { Role = h.Role == ChatRole.User ? Role.User : Role.Assistant, Content = h.Content })
            .ToList();
        messages.Add(new MessageParam { Role = Role.User, Content = request.UserMessage });

        List<ToolUnion> toolDefinitions = request.Tools.Select(ToTool).ToList();
        var reply = new StringBuilder();

        for (var round = 0; round < o.MaxToolRounds; round++)
        {
            Message response;
            try
            {
                response = await client.Messages.Create(new MessageCreateParams
                {
                    Model = o.Claude.Model,
                    MaxTokens = o.MaxTokens,
                    System = request.SystemPrompt,
                    OutputConfig = new OutputConfig { Effort = ParseEffort(o.ClaudeEffort) },
                    Tools = toolDefinitions,
                    Messages = messages
                }, ct);
            }
            catch (AnthropicUnauthorizedException ex)
            {
                throw Fail(ex, "API anahtarı geçersiz. 'AI:Claude:ApiKey' ayarını kontrol edin.");
            }
            catch (AnthropicRateLimitException ex)
            {
                throw Fail(ex, "AI servisi şu an yoğun veya kullanım limiti doldu. Biraz sonra tekrar deneyin.");
            }
            catch (AnthropicBadRequestException ex)
            {
                throw Fail(ex, "AI isteği reddedildi (kredi bitmiş veya ayar hatalı olabilir). Ayrıntı loglarda.");
            }
            catch (AnthropicIOException ex)
            {
                throw Fail(ex, ChatModelMessages.Unreachable);
            }
            catch (AnthropicException ex)
            {
                throw Fail(ex, "AI servisinden beklenmeyen bir hata döndü. Ayrıntı loglarda.");
            }
            catch (HttpRequestException ex)
            {
                throw Fail(ex, ChatModelMessages.Unreachable);
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
            {
                throw Fail(ex, ChatModelMessages.Timeout);
            }

            logger.LogInformation("Claude turu: model {Model}, giriş {In} / çıkış {Out} token",
                o.Claude.Model, response.Usage.InputTokens, response.Usage.OutputTokens);

            if (response.StopReason == StopReason.Refusal)
                return new ChatTurnResult("Bu isteği işleyemiyorum. Lütfen farklı şekilde ifade edin.", o.Claude.Model);

            List<ContentBlockParam> assistantContent = [];
            List<ContentBlockParam> toolResults = [];
            foreach (var block in response.Content)
            {
                if (block.TryPickText(out TextBlock? text))
                {
                    assistantContent.Add(new TextBlockParam { Text = text.Text });
                    reply.AppendLine(text.Text);
                }
                else if (block.TryPickThinking(out ThinkingBlock? thinking))
                {
                    assistantContent.Add(new ThinkingBlockParam { Thinking = thinking.Thinking, Signature = thinking.Signature });
                }
                else if (block.TryPickRedactedThinking(out RedactedThinkingBlock? redacted))
                {
                    assistantContent.Add(new RedactedThinkingBlockParam { Data = redacted.Data });
                }
                else if (block.TryPickToolUse(out ToolUseBlock? toolUse))
                {
                    assistantContent.Add(new ToolUseBlockParam { ID = toolUse.ID, Name = toolUse.Name, Input = toolUse.Input });
                    var input = JsonSerializer.SerializeToElement(toolUse.Input);
                    var result = await tools.ExecuteAsync(toolUse.Name, input, ct);
                    toolResults.Add(new ToolResultBlockParam { ToolUseID = toolUse.ID, Content = result.Content, IsError = result.IsError });
                }
            }

            if (toolResults.Count == 0)
                return new ChatTurnResult(reply.ToString(), o.Claude.Model);

            messages.Add(new MessageParam { Role = Role.Assistant, Content = assistantContent });
            messages.Add(new MessageParam { Role = Role.User, Content = toolResults });
        }

        logger.LogWarning("Araç turu sınırına ({Max}) ulaşıldı", o.MaxToolRounds);
        reply.AppendLine("(İşlem çok uzun sürdü; şu ana kadarki önerileri kontrol edin.)");
        return new ChatTurnResult(reply.ToString(), o.Claude.Model);
    }

    private ChatModelException Fail(Exception ex, string message)
    {
        logger.LogError(ex, "Claude API çağrısı başarısız");
        return new ChatModelException(message, ex);
    }

    private static ToolUnion ToTool(ChatToolDefinition d) => new Tool
    {
        Name = d.Name,
        Description = d.Description,
        InputSchema = InputSchema.FromRawUnchecked(
            d.InputSchema.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone()))
    };

    private static Effort ParseEffort(string value) => value.ToLowerInvariant() switch
    {
        "medium" => Effort.Medium,
        "high" => Effort.High,
        "max" => Effort.Max,
        _ => Effort.Low
    };
}
