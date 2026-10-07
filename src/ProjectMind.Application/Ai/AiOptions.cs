namespace ProjectMind.Application.Ai;

/// <summary>
/// appsettings "AI" bölümü. API anahtarları repoya yazılmaz: git'e girmeyen appsettings.Local.json,
/// user-secrets veya ortam değişkeni (GEMINI_API_KEY / ANTHROPIC_API_KEY) kullanılır.
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "AI";
    public const string GeminiProvider = "Gemini";
    public const string ClaudeProvider = "Claude";

    /// <summary>"Gemini", "Claude" veya "Mock". Seçili sağlayıcının anahtarı yoksa Mock'a düşülür.</summary>
    public string Provider { get; set; } = GeminiProvider;
    public int MaxTokens { get; set; } = 8000;
    public int MaxToolRounds { get; set; } = 8;
    public int MaxHistoryMessages { get; set; } = 20;

    public ProviderOptions Gemini { get; set; } = new() { Model = "gemini-2.5-flash" };
    public ProviderOptions Claude { get; set; } = new() { Model = "claude-opus-5-5" };

    /// <summary>Claude'a özgü: düşünme derinliği (low/medium/high/max).</summary>
    public string ClaudeEffort { get; set; } = "low";
}

public sealed class ProviderOptions
{
    public string Model { get; set; } = "";
    public string? ApiKey { get; set; }
}
