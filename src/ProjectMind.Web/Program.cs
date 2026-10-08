using ProjectMind.Application;
using ProjectMind.Application.Ai;
using Microsoft.EntityFrameworkCore;
using ProjectMind.Infrastructure;
using ProjectMind.Infrastructure.Persistence;
using ProjectMind.Web.Components;
using ProjectMind.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Kişisel ayarlar (API anahtarları) için git'e girmeyen dosya. Örnek: appsettings.Local.example.json
var contentRoot = builder.Environment.ContentRootPath;
LocalSettings.Register(builder.Configuration, contentRoot);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default tanımlı değil.");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.SectionName));
builder.Services.Configure<ProjectMind.Application.Analytics.HealthOptions>(
    builder.Configuration.GetSection(ProjectMind.Application.Analytics.HealthOptions.SectionName));
builder.Services.Configure<ProjectMind.Application.WhatIf.WhatIfOptions>(builder.Configuration.GetSection(ProjectMind.Application.WhatIf.WhatIfOptions.Section));
builder.Services.Configure<ProjectMind.Application.MissingWork.MissingWorkOptions>(
    builder.Configuration.GetSection(ProjectMind.Application.MissingWork.MissingWorkOptions.Section));
builder.Services.Configure<ProjectMind.Application.Demo.DemoOptions>(builder.Configuration.GetSection(ProjectMind.Application.Demo.DemoOptions.Section));
builder.Services.Configure<ProjectMind.Application.Ml.MlOptions>(builder.Configuration.GetSection(ProjectMind.Application.Ml.MlOptions.Section));
// Model klasörü göreli verilirse uygulama klasörüne göre çözülür (App_Data/models, git'e girmez).
builder.Services.PostConfigure<ProjectMind.Application.Ml.MlOptions>(o =>
    o.ModelDirectory = Path.Combine(builder.Environment.ContentRootPath, o.ModelDirectory));
builder.Services.PostConfigure<AiOptions>(o =>
{
    var legacyKey = string.IsNullOrWhiteSpace(o.ApiKey) ? null : o.ApiKey.Trim();
    var useGemini = string.Equals(o.Provider, AiOptions.GeminiProvider, StringComparison.OrdinalIgnoreCase);
    if (string.IsNullOrWhiteSpace(o.Gemini.ApiKey))
        o.Gemini.ApiKey = (useGemini ? legacyKey : null) ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
    if (string.IsNullOrWhiteSpace(o.Claude.ApiKey))
        o.Claude.ApiKey = (useGemini ? null : legacyKey) ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
    o.Gemini.ApiKey = o.Gemini.ApiKey?.Trim();
    o.Claude.ApiKey = o.Claude.ApiKey?.Trim();
    o.Diagnostics = $"{LocalSettings.Inspect(contentRoot)}\n{LocalSettings.Describe(o)}";
});

builder.Services.AddScoped<AppScope>();
builder.Services.AddScoped<AppEvents>();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();

var ai = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>().Value;
app.Logger.LogInformation("AI sağlayıcısı: {Ai}\n{Diagnostics}", ai.ActiveModelLabel(), ai.Diagnostics);

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // Geliştirmede bekleyen migration'lar açılışta uygulanır ("Invalid object name" hatası yaşanmasın).
    if (app.Environment.IsDevelopment())
        await db.Database.MigrateAsync();
    // Isınma: EF modeli ve ilk SQL bağlantısı açılışta hazırlanır; ilk sayfa yüklemesi beklemez.
    try
    {
        await db.ChatSessions.AsNoTracking().AnyAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Veritabanı ısınma sorgusu başarısız");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
