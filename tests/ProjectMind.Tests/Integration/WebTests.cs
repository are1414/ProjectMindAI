using System.Net;
using System.Text.RegularExpressions;

namespace ProjectMind.Tests.Integration;

/// <summary>Sayfaların açıldığını ve formların gerçekten kaydettiğini uçtan uca doğrular.</summary>
public partial class WebTests(WebFactory factory) : IClassFixture<WebFactory>
{
    private readonly HttpClient _client = factory.CreateClient(
        new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Root_redirects_to_projects()
    {
        var response = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/projects", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Create_project_person_and_work_item_through_forms()
    {
        var projectUrl = await PostFormAsync("/projects/new", new()
        {
            ["Name"] = "Form Test Projesi",
            ["Type"] = "0",
            ["Status"] = "0",
            ["StartDate"] = "2026-11-01",
            ["TargetEndDate"] = "2027-03-31",
            ["Budget"] = "1250000.50",
            ["Currency"] = "TRY",
            ["HoursPerDay"] = "7.5"
        });
        Assert.Matches(@"^/projects/\d+$", projectUrl);

        await PostFormAsync($"{projectUrl}/people/new", new()
        {
            ["Name"] = "Zeynep",
            ["Skills"] = new[] { "Backend", "Database" },
            ["WeeklyCapacityHours"] = "37.5",
            ["HourlyCost"] = "650"
        });

        await PostFormAsync($"{projectUrl}/work-items/new", new()
        {
            ["Name"] = "Veritabanı kurulumu",
            ["Phase"] = "2",
            ["RequiredSkill"] = "Database",
            ["Priority"] = "2",
            ["EstimatedHours"] = "12.5",
            ["Status"] = "0",
            ["PercentComplete"] = "0",
            ["ActualHours"] = "0"
        });

        var html = await _client.GetStringAsync(projectUrl);
        Assert.Contains("Form Test Projesi", html);
        Assert.Contains("01.11.2026", html);
        Assert.Contains("1.250.001 TRY", html);       // Türkçe sayı biçimi
        Assert.Contains("Zeynep", html);
        Assert.Contains("Backend, Veritabanı", html);
        Assert.Contains("Veritabanı kurulumu", html);
        Assert.Contains("12.5", html);
    }

    [Fact]
    public async Task Invalid_project_form_shows_error_instead_of_saving()
    {
        var form = await GetFormAsync("/projects/new");
        var response = await _client.PostAsync("/projects/new", Encode(form.Token, new()
        {
            ["Name"] = "Tarih hatalı",
            ["StartDate"] = "2027-01-01",
            ["TargetEndDate"] = "2026-01-01",
            ["Currency"] = "TRY",
            ["HoursPerDay"] = "8"
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Hedef bitiş tarihi başlangıç tarihinden önce olamaz", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unknown_project_returns_404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/projects/99999")).StatusCode);

    private async Task<string> PostFormAsync(string url, Dictionary<string, StringOrList> fields)
    {
        var form = await GetFormAsync(url);
        var response = await _client.PostAsync(url, Encode(form.Token, fields));
        Assert.True(response.StatusCode == HttpStatusCode.Redirect,
            $"{url} kaydedilmedi: {(int)response.StatusCode}\n{await response.Content.ReadAsStringAsync()}");
        return response.Headers.Location!.OriginalString.Split('#')[0];
    }

    private async Task<(string Token, string Html)> GetFormAsync(string url)
    {
        var html = await _client.GetStringAsync(url);
        var token = TokenRegex().Match(html).Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token), "Antiforgery token bulunamadı");
        return (token, html);
    }

    private static FormUrlEncodedContent Encode(string token, Dictionary<string, StringOrList> fields)
    {
        var pairs = fields.SelectMany(f => f.Value.Values.Select(v => new KeyValuePair<string, string>(f.Key, v))).ToList();
        pairs.Add(new("__RequestVerificationToken", token));
        return new FormUrlEncodedContent(pairs);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();

    /// <summary>Form alanı: tek değer veya aynı isimle birden fazla değer (ör. çoklu checkbox).</summary>
    public sealed class StringOrList
    {
        private StringOrList(string[] values) => Values = values;
        public string[] Values { get; }
        public static implicit operator StringOrList(string value) => new([value]);
        public static implicit operator StringOrList(string[] values) => new(values);
    }
}
