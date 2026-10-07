using ProjectMind.Application;
using ProjectMind.Application.Ai;
using Microsoft.EntityFrameworkCore;
using ProjectMind.Infrastructure;
using ProjectMind.Infrastructure.Persistence;
using ProjectMind.Web.Components;
using ProjectMind.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Kişisel ayarlar (API anahtarları) için git'e girmeyen dosya. Örnek: appsettings.Local.example.json
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default tanımlı değil.");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.SectionName));
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
});

builder.Services.AddScoped<AppScope>();
builder.Services.AddScoped<AppEvents>();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();

var aiLabel = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>().Value.ActiveModelLabel();
app.Logger.LogInformation("AI sağlayıcısı: {Ai}", aiLabel);

// Geliştirme ortamında bekleyen migration'lar açılışta uygulanır ("Invalid object name" hatası yaşanmasın).
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
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
