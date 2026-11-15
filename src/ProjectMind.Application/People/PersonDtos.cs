using System.ComponentModel.DataAnnotations;
using ProjectMind.Domain.Entities;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.People;

public sealed class PersonRequest
{
    [Display(Name = "Ad soyad"), Required(ErrorMessage = "Ad zorunludur."),
     StringLength(200, ErrorMessage = "Ad en fazla 200 karakter olabilir.")]
    public string Name { get; set; } = "";

    /// <summary>Formda çoklu seçim olarak gelir; servis tek bir flags değerine birleştirir.</summary>
    [Display(Name = "Beceriler")]
    public List<Skill> Skills { get; set; } = [];

    [Display(Name = "Haftalık kapasite (saat)"), Range(0, 168, ErrorMessage = "Haftalık kapasite 0 ile 168 saat arasında olmalı.")]
    public decimal WeeklyCapacityHours { get; set; } = 40;

    [Display(Name = "Saatlik maliyet"), Range(0, double.MaxValue, ErrorMessage = "Saatlik maliyet negatif olamaz.")]
    public decimal HourlyCost { get; set; }

    public Skill CombinedSkills => Skills.Aggregate(Skill.None, (all, s) => all | s);

    public static PersonRequest From(PersonResponse p) => new()
    {
        Name = p.Name,
        Skills = Enum.GetValues<Skill>().Where(s => s != Skill.None && p.Skills.HasFlag(s)).ToList(),
        WeeklyCapacityHours = p.WeeklyCapacityHours,
        HourlyCost = p.HourlyCost
    };
}

public sealed record PersonResponse(
    int Id,
    int ProjectId,
    string Name,
    Skill Skills,
    decimal WeeklyCapacityHours,
    decimal HourlyCost)
{
    public static PersonResponse From(Person p) =>
        new(p.Id, p.ProjectId, p.Name, p.Skills, p.WeeklyCapacityHours, p.HourlyCost);
}
