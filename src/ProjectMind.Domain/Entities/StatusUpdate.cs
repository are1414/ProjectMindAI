using ProjectMind.Domain.Enums;

namespace ProjectMind.Domain.Entities;

/// <summary>Bir işin ilerleme geçmişi: durum / % / harcanan saat her değiştiğinde bir kayıt.</summary>
public class StatusUpdate : Entity
{
    public int WorkItemId { get; set; }
    public int ProjectId { get; set; }
    public DateOnly Date { get; set; }
    public WorkItemStatus Status { get; set; }
    public int PercentComplete { get; set; }
    public decimal ActualHours { get; set; }
}
