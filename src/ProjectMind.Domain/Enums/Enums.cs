namespace ProjectMind.Domain.Enums;

public enum ProjectType
{
    WebApplication,
    MobileApplication,
    DesktopApplication,
    Integration,
    DataAnalytics,
    Other
}

public enum ProjectStatus
{
    Planning,
    Active,
    OnHold,
    Completed,
    Cancelled
}

/// <summary>Bir kişinin sahip olabileceği beceriler. Kişi birden fazla beceriye sahip olabilir.</summary>
[Flags]
public enum Skill
{
    None = 0,
    Analysis = 1,
    Design = 2,
    Backend = 4,
    Frontend = 8,
    Database = 16,
    DevOps = 32,
    Test = 64,
    Documentation = 128,
    ProjectManagement = 256
}

/// <summary>Proje yaşam döngüsü fazı; eksik iş tespiti (Faz 3) bu alanı kullanır.</summary>
public enum WorkPhase
{
    Analysis,
    Design,
    Infrastructure,
    Development,
    Test,
    Closing
}

public enum Priority
{
    Low,
    Medium,
    High,
    Critical
}

public enum WorkItemStatus
{
    NotStarted,
    InProgress,
    Blocked,
    Done,
    Cancelled
}
