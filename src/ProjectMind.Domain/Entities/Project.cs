using ProjectMind.Domain.Enums;

namespace ProjectMind.Domain.Entities;

public class Project : Entity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public ProjectType Type { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Planning;
    public DateOnly StartDate { get; set; }
    public DateOnly TargetEndDate { get; set; }
    public decimal Budget { get; set; }
    public string Currency { get; set; } = "TRY";

    /// <summary>Bir iş gününde çalışılan saat; takvim hesaplarında kullanılır.</summary>
    public decimal HoursPerDay { get; set; } = 8;

    public List<Person> People { get; set; } = [];
    public List<WorkItem> WorkItems { get; set; } = [];
}
