namespace ProjectMind.Application.Ai;

/// <summary>appsettings "AI" bölümü. API anahtarı repoya yazılmaz: user-secrets veya ANTHROPIC_API_KEY.</summary>
public sealed class AiOptions
{
    public const string SectionName = "AI";

    /// <summary>"Claude" veya "Mock". Anahtar yoksa Claude seçili olsa da Mock'a düşülür.</summary>
    public string Provider { get; set; } = "Claude";
    public string Model { get; set; } = "claude-opus-5-5";
    public string Effort { get; set; } = "low";
    public int MaxTokens { get; set; } = 16000;
    public int MaxToolRounds { get; set; } = 8;
    public int MaxHistoryMessages { get; set; } = 20;
    public string? ApiKey { get; set; }
}
