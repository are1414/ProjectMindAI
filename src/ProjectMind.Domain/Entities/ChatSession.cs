namespace ProjectMind.Domain.Entities;

/// <summary>Bir AI sohbeti. Proje sohbetten oluşturulabildiği için ProjectId başta boş olabilir.</summary>
public class ChatSession : Entity
{
    public int? ProjectId { get; set; }
    public required string Title { get; set; }

    public Project? Project { get; set; }
    public List<ChatMessage> Messages { get; set; } = [];
}
