using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.WebEncoders;
using ProjectMind.Application;
using ProjectMind.Infrastructure;
using ProjectMind.Web.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default tanımlı değil.");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);
builder.Services.AddControllersWithViews(o => o.Filters.Add<NotFoundExceptionFilter>());

// Türkçe karakterler (ş, ğ, ı…) HTML'de &#x..; yerine olduğu gibi yazılsın.
builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

// Form sayıları (ör. 12.5) tarayıcıdan nokta ile gelir; bağlama kültürden bağımsız olmalı.
// Türkçe gösterim biçimlendirmesi Display yardımcısında yapılır.
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture),
    SupportedCultures = [CultureInfo.InvariantCulture],
    SupportedUICultures = [CultureInfo.InvariantCulture]
});

app.UseHttpsRedirection();
app.UseRouting();

app.MapStaticAssets();
app.MapControllers().WithStaticAssets();

app.Run();

public partial class Program;
