using ProjectMind.Domain.Enums;

namespace ProjectMind.Domain.Entities;

public class Person : Entity
{
    public int ProjectId { get; set; }
    public required string Name { get; set; }
    public Skill Skills { get; set; }
    public decimal WeeklyCapacityHours { get; set; }
    public decimal HourlyCost { get; set; }

    public Project? Project { get; set; }
}
