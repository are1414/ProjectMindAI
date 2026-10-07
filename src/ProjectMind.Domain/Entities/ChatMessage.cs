using ProjectMind.Domain.Enums;

namespace ProjectMind.Domain.Entities;

public class ChatMessage : Entity
{
    public int ChatSessionId { get; set; }
    public ChatRole Role { get; set; }
    public required string Content { get; set; }

    /// <summary>AI cevaplarında denetim için: kullanılan model ve prompt sürümü.</summary>
    public string? Model { get; set; }
    public string? PromptVersion { get; set; }

    public ChatSession? ChatSession { get; set; }
}
