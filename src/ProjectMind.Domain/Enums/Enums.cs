using System.ComponentModel.DataAnnotations;

namespace ProjectMind.Domain.Enums;

public enum ProjectType
{
    [Display(Name = "Web uygulaması")] WebApplication,
    [Display(Name = "Mobil uygulama")] MobileApplication,
    [Display(Name = "Masaüstü uygulama")] DesktopApplication,
    [Display(Name = "Entegrasyon")] Integration,
    [Display(Name = "Veri / Analitik")] DataAnalytics,
    [Display(Name = "Diğer")] Other
}

public enum ProjectStatus
{
    [Display(Name = "Planlama")] Planning,
    [Display(Name = "Aktif")] Active,
    [Display(Name = "Beklemede")] OnHold,
    [Display(Name = "Tamamlandı")] Completed,
    [Display(Name = "İptal")] Cancelled
}

/// <summary>Bir kişinin sahip olabileceği beceriler. Kişi birden fazla beceriye sahip olabilir.</summary>
[Flags]
public enum Skill
{
    [Display(Name = "Yok")] None = 0,
    [Display(Name = "Analiz")] Analysis = 1,
    [Display(Name = "Tasarım")] Design = 2,
    [Display(Name = "Backend")] Backend = 4,
    [Display(Name = "Frontend")] Frontend = 8,
    [Display(Name = "Veritabanı")] Database = 16,
    [Display(Name = "DevOps / Sunucu")] DevOps = 32,
    [Display(Name = "Test")] Test = 64,
    [Display(Name = "Dokümantasyon")] Documentation = 128,
    [Display(Name = "Proje yönetimi")] ProjectManagement = 256
}

/// <summary>Proje yaşam döngüsü fazı; eksik iş tespiti (Faz 3) bu alanı kullanır.</summary>
public enum WorkPhase
{
    [Display(Name = "Analiz")] Analysis,
    [Display(Name = "Tasarım")] Design,
    [Display(Name = "Altyapı")] Infrastructure,
    [Display(Name = "Geliştirme")] Development,
    [Display(Name = "Test")] Test,
    [Display(Name = "Kapanış")] Closing
}

public enum Priority
{
    [Display(Name = "Düşük")] Low,
    [Display(Name = "Orta")] Medium,
    [Display(Name = "Yüksek")] High,
    [Display(Name = "Kritik")] Critical
}

public enum WorkItemStatus
{
    [Display(Name = "Başlamadı")] NotStarted,
    [Display(Name = "Devam ediyor")] InProgress,
    [Display(Name = "Bloke")] Blocked,
    [Display(Name = "Bitti")] Done,
    [Display(Name = "İptal")] Cancelled
}

public enum ChatRole
{
    User,
    Assistant
}

public enum AiActionStatus
{
    [Display(Name = "Onay bekliyor")] Pending,
    [Display(Name = "Uygulandı")] Applied,
    [Display(Name = "Reddedildi")] Rejected,
    [Display(Name = "Hata")] Failed
}

/// <summary>Önerinin içeriği nereden geldi (RQ4: kabul oranı kaynağa göre). Bu alandan önceki kayıtlar Unknown.</summary>
public enum AiActionSource
{
    [Display(Name = "Bilinmiyor")] Unknown,
    [Display(Name = "Kural / şablon")] Rule,
    [Display(Name = "LLM")] Llm,
    [Display(Name = "Kullanıcı")] User
}

/// <summary>Denetim kaydı tutulan AI analiz cevabı türü.</summary>
public enum AiAnalysisKind
{
    Chat,
    ProjectComment,
    ScenarioComment,

    /// <summary>Hibrit eksik iş önerisinin LLM katmanı (şablonların kapsamadığı ek işler).</summary>
    MissingWork
}

/// <summary>AI analiz çağrısının sonucu.</summary>
public enum AiAnalysisOutcome
{
    Success,
    InvalidSchema,
    ModelError
}

/// <summary>What-if karşılaştırmasını kim çalıştırdı (RQ3).</summary>
public enum WhatIfRunSource
{
    [Display(Name = "What-if paneli")] Panel,
    [Display(Name = "AI aracı (simulate_what_if)")] AiTool
}
