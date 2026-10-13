using ProjectMind.Application.Ai;
using ProjectMind.Application.WhatIf;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.Evaluation;

public enum EvaluationTaskKind
{
    /// <summary>Durum sekmesindeki AI proje yorumu (JSON şemalı).</summary>
    ProjectComment,

    /// <summary>What-if sekmesindeki senaryo yorumu (JSON şemalı, sabit iki senaryo).</summary>
    ScenarioComment,

    /// <summary>Hibrit eksik işin LLM katmanı (JSON şemalı).</summary>
    MissingWork,

    /// <summary>Sabit sohbet sorusu; cevap salt okunur bir araçtan beslenmeli.</summary>
    Chat
}

/// <summary>Görev setindeki bir görev. Sohbet görevinde <see cref="ExpectedTool"/> modelin çağırması beklenen salt okunur araçtır.</summary>
public sealed record EvaluationTask(string Id, EvaluationTaskKind Kind, string? Question = null, string? ExpectedTool = null);

/// <summary>
/// Sağlayıcı karşılaştırmasının sabit, sürümlü görev seti (D31). Görevler demo projesi ("Mobile Banking Modernization")
/// üzerinde çalışır. Metin veya senaryo değişirse <see cref="Version"/> artırılır; eski koşular eski sürümle karşılaştırılmaz.
/// </summary>
public static class EvaluationTaskSet
{
    public const string Version = "eval-v1";

    /// <summary>Koşunun sohbet oturumlarının başlığı (koşu sonunda silinir).</summary>
    public const string ChatTitlePrefix = "[eval-v1]";

    /// <summary>Senaryo yorumu görevinin iki sabit senaryosu (kişi ekleme; kimlik gerektirmez, her demoda aynı).</summary>
    public static readonly IReadOnlyList<WhatIfScenario> Scenarios =
    [
        new("+1 Backend geliştirici", [new ScenarioChange(ScenarioChangeKind.AddPerson, Name: "Yeni backend geliştirici",
            Skills: [Skill.Backend], WeeklyHours: 40, Count: 1)]),
        new("+1 Frontend geliştirici", [new ScenarioChange(ScenarioChangeKind.AddPerson, Name: "Yeni mobil geliştirici",
            Skills: [Skill.Frontend], WeeklyHours: 40, Count: 1)])
    ];

    public static readonly IReadOnlyList<EvaluationTask> Tasks =
    [
        new("comment-project", EvaluationTaskKind.ProjectComment),
        new("comment-scenario", EvaluationTaskKind.ScenarioComment),
        new("missing-work", EvaluationTaskKind.MissingWork),
        new("chat-status", EvaluationTaskKind.Chat,
            "Projenin takvim ve maliyet performansı nasıl? SPI(t), CPI ve tahmini bitiş tarihini söyler misin?",
            AiTools.GetProjectStatus),
        new("chat-plan", EvaluationTaskKind.Chat,
            "Kritik yoldaki işler hangileri ve mevcut ekiple planlanan bitiş tarihi ne?", AiTools.PreviewSchedule),
        new("chat-missing", EvaluationTaskKind.Chat, "Bu projede eksik iş var mı?", AiTools.CheckMissingWork),
        new("chat-whatif", EvaluationTaskKind.Chat,
            "Ekibe haftada 40 saat çalışan bir Backend geliştirici eklersek P80 bitiş tarihi nasıl değişir?", AiTools.SimulateWhatIf),
        new("chat-risk", EvaluationTaskKind.Chat, "Projenin en büyük riskleri neler? Bloke iş var mı?", AiTools.GetProjectStatus)
    ];

    public static EvaluationTask? Find(string? id) => Tasks.FirstOrDefault(t => t.Id == id);
}
