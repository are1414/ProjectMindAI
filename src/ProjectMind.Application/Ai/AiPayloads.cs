using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.Ai;

// AI araçlarının girdileri. Alan adları araç şemalarıyla (AiTools) birebir aynıdır.

public sealed record CreateProjectPayload(
    string Name, DateOnly StartDate, DateOnly TargetEndDate, string? Description = null, ProjectType? Type = null,
    decimal? Budget = null, string? Currency = null, decimal? HoursPerDay = null);

public sealed record UpdateProjectPayload(
    string? Name = null, string? Description = null, ProjectType? Type = null, ProjectStatus? Status = null, DateOnly? StartDate = null,
    DateOnly? TargetEndDate = null, decimal? Budget = null, string? Currency = null, decimal? HoursPerDay = null);

public sealed record AddPersonPayload(string Name, List<Skill> Skills, decimal WeeklyCapacityHours, decimal? HourlyCost = null);

public sealed record UpdatePersonPayload(
    int PersonId, string? Name = null, List<Skill>? Skills = null, decimal? WeeklyCapacityHours = null, decimal? HourlyCost = null);

/// <param name="AssigneeNames">Atanan kişiler (birlikte çalışırlar, D32).</param>
/// <param name="AssigneeName">Eski tek kişilik alan (araç şemasında yok); D32'den önce oluşmuş bekleyen kartlar için okunur.</param>
public sealed record AddWorkItemPayload(
    string Name, WorkPhase Phase, Skill RequiredSkill, decimal EstimatedHours, string? Description = null,
    Priority? Priority = null, List<string>? AssigneeNames = null, string? ParentName = null, string? AssigneeName = null);

/// <param name="AssigneeNames">Atananların tam listesi (mevcut atamanın yerine geçer; boş liste = atama kaldırılır, D32).</param>
/// <param name="AssigneeName">Eski tek kişilik alan (araç şemasında yok); D32'den önce oluşmuş bekleyen kartlar için okunur.</param>
public sealed record UpdateWorkItemPayload(
    int WorkItemId, string? Name = null, string? Description = null, WorkPhase? Phase = null, Skill? RequiredSkill = null, Priority? Priority = null,
    decimal? EstimatedHours = null, List<string>? AssigneeNames = null, WorkItemStatus? Status = null, int? PercentComplete = null,
    decimal? ActualHours = null, DateOnly? PlannedStart = null, DateOnly? PlannedEnd = null, string? ParentName = null,
    string? AssigneeName = null);

public static class AssigneeRequest
{
    /// <summary>
    /// İstenen atananlar: assigneeNames (kırpılmış, boşlar ve tekrarlar atılmış) ya da eski kartın assigneeName alanı
    /// (boş = atamayı kaldır). İkisi de yoksa null (atama değişmez).
    /// </summary>
    public static List<string>? Names(List<string>? names, string? legacyName) =>
        names is not null
            ? names.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).Distinct().ToList()
            : legacyName is null ? null
            : string.IsNullOrWhiteSpace(legacyName) ? [] : [legacyName.Trim()];
}

public sealed record RemoveWorkItemPayload(int WorkItemId);

public sealed record AddDependencyPayload(string PredecessorName, string SuccessorName);

/// <summary>simulate_what_if: tek senaryo, düz alanlar (modeller iç içe şemada daha çok hata yapıyor).</summary>
public sealed record SimulateWhatIfPayload(
    string? ScenarioName = null, int? AddPeopleCount = null, List<Skill>? AddPeopleSkills = null, decimal? AddPeopleWeeklyHours = null,
    DateOnly? AddPeopleJoinDate = null, string? RemovePersonName = null, string? CapacityPersonName = null, decimal? NewWeeklyHours = null,
    string? RemoveWorkItemName = null, DateOnly? NewDeadline = null);

public static class AiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
        RespectRequiredConstructorParameters = true,
        // Türkçe karakterler \u0131 gibi kaçışlanmasın: model için okunaklı ve daha az token.
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        RespectNullableAnnotations = true
    };
}
