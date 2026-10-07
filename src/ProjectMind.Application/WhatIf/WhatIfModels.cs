using ProjectMind.Application.Planning;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.WhatIf;

public enum ScenarioChangeKind { AddPerson, RemovePerson, ChangeCapacity, RemoveWorkItem, ChangeDeadline }

/// <summary>
/// Bir senaryo değişikliği. Kullanılan alanlar türe göre:
/// AddPerson: Name, Skills, WeeklyHours, HourlyCost, Date (katılım), Count · RemovePerson: PersonId ·
/// ChangeCapacity: PersonId, WeeklyHours · RemoveWorkItem: WorkItemId (alt işleriyle) · ChangeDeadline: Date.
/// </summary>
public sealed record ScenarioChange(
    ScenarioChangeKind Kind,
    int? PersonId = null,
    int? WorkItemId = null,
    string? Name = null,
    IReadOnlyList<Skill>? Skills = null,
    decimal? WeeklyHours = null,
    decimal? HourlyCost = null,
    DateOnly? Date = null,
    int Count = 1);

public sealed record WhatIfScenario(string Name, IReadOnlyList<ScenarioChange> Changes);

/// <summary>Monte Carlo bitiş dağılımında bir nokta: bu tarihte veya önce bitme olasılığı.</summary>
public sealed record FinishProbability(DateOnly Date, double Cumulative);

public sealed record MonteCarloResult(
    int Iterations,
    DateOnly P50Finish,
    DateOnly P80Finish,
    DateOnly P90Finish,
    decimal P50Cost,
    decimal P80Cost,
    double OnTimeProbability,
    IReadOnlyList<FinishProbability> Distribution);

public sealed record ScenarioResult(
    string Name,
    IReadOnlyList<ScenarioChange> Changes,
    IReadOnlyList<string> Notes,
    DateOnly TargetDate,
    PlanResult Plan,
    MonteCarloResult MonteCarlo,
    int P80DeltaWorkdays);

public sealed record WhatIfComparison(ScenarioResult Current, IReadOnlyList<ScenarioResult> Scenarios, WhatIfOptions Assumptions);

/// <summary>What-if varsayımları (ayar bölümü "WhatIf").</summary>
public sealed class WhatIfOptions
{
    public const string Section = "WhatIf";

    public int Iterations { get; set; } = 500;
    public int Seed { get; set; } = 7;

    /// <summary>Efor belirsizliği: üçgen dağılım çarpanı (en iyi, en olası, en kötü). Sağa çarpık: yazılımda aşım daha olası.</summary>
    public double EffortMin { get; set; } = 0.85;
    public double EffortMode { get; set; } = 1.0;
    public double EffortMax { get; set; } = 1.5;

    /// <summary>Brooks etkisi: yeni kişi ısınma süresince bu verimle çalışır.</summary>
    public int RampUpWeeks { get; set; } = 4;
    public decimal RampUpProductivity { get; set; } = 0.5m;

    /// <summary>Mentorluk yükü: yeni kişinin kapasitesinin bu oranı kadar saat, ısınma süresince mevcut ekipten eşit düşülür.</summary>
    public decimal MentoringShare { get; set; } = 0.25m;

    /// <summary>Mentorluk yüzünden bir kişinin kapasitesi en fazla bu orana düşer.</summary>
    public decimal MinRemainingCapacity { get; set; } = 0.5m;

    public decimal DefaultWeeklyHours { get; set; } = 40;
    public int MaxAddedPeople { get; set; } = 10;
    public int MaxScenarios { get; set; } = 4;
}
