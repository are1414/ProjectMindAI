using ProjectMind.Application;
using ProjectMind.Application.Ai;
using ProjectMind.Infrastructure;
using ProjectMind.Web.Components;
using ProjectMind.Web.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default tanımlı değil.");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.SectionName));
builder.Services.PostConfigure<AiOptions>(o =>
    o.ApiKey = string.IsNullOrWhiteSpace(o.ApiKey) ? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") : o.ApiKey);

builder.Services.AddScoped<AppScope>();
builder.Services.AddScoped<AppEvents>();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();

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
