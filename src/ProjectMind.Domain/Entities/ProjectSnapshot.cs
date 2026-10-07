namespace ProjectMind.Domain.Entities;

/// <summary>
/// Projenin bir gündeki ölçümleri (günde bir kayıt, gün içinde güncellenir).
/// S-eğrisi ve ML veri seti (Faz 6) bu tablodan beslenir.
/// </summary>
public class ProjectSnapshot : Entity
{
    public int ProjectId { get; set; }
    public DateOnly Date { get; set; }
    public int? BaselineId { get; set; }

    public decimal PlannedValue { get; set; }
    public decimal EarnedValue { get; set; }
    public decimal ActualCost { get; set; }
    public decimal BudgetAtCompletion { get; set; }
    public decimal? Spi { get; set; }
    public decimal? Cpi { get; set; }
    public decimal? SpiTime { get; set; }
    public decimal? EstimateAtCompletion { get; set; }
    public DateOnly? ForecastFinish { get; set; }
    public decimal PercentComplete { get; set; }
    public decimal? HealthScore { get; set; }

    public decimal ScopeHours { get; set; }
    public int BlockedItems { get; set; }
    public int TeamSize { get; set; }
}
