namespace ProjectMind.Domain.Entities;

/// <summary>
/// Onaylanmış planın dondurulmuş kopyası. Sonradan iş değişse/silinse de değişmez;
/// EVM'de planlanan değer (PV) bu kayıttan hesaplanır.
/// </summary>
public class Baseline : Entity
{
    public int ProjectId { get; set; }
    public DateOnly PlannedStart { get; set; }
    public DateOnly PlannedFinish { get; set; }
    public decimal TotalHours { get; set; }
    public decimal PlannedCost { get; set; }

    public List<BaselineItem> Items { get; set; } = [];
}

public class BaselineItem
{
    public int Id { get; set; }
    public int BaselineId { get; set; }

    /// <summary>İş sonradan silinebileceği için yabancı anahtar değil; ad da saklanır.</summary>
    public int WorkItemId { get; set; }
    public required string Name { get; set; }
    public DateOnly PlannedStart { get; set; }
    public DateOnly PlannedEnd { get; set; }
    public decimal Hours { get; set; }
    public decimal HourlyCost { get; set; }
    public int? AssigneeId { get; set; }
}
