using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using ProjectMind.Application.Ai;
using ProjectMind.Web.Services;

namespace ProjectMind.Tests.Unit;

public class LocalSettingsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("pm-local-").FullName;

    private string LocalFile => Path.Combine(_root, LocalSettings.FileName);

    [Fact]
    public void Reads_key_from_content_root_file()
    {
        File.WriteAllText(LocalFile, """{ "AI": { "Provider": "Gemini", "Gemini": { "ApiKey": "AIzaTEST1234" } } }""");
        var config = new ConfigurationManager();

        LocalSettings.Register(config, _root);

        Assert.Equal("AIzaTEST1234", config["AI:Gemini:ApiKey"]);
        Assert.Contains("Okundu", LocalSettings.Inspect(_root));
    }

    [Fact]
    public void Saved_file_is_valid_keeps_other_settings_and_is_read_back()
    {
        File.WriteAllText(LocalFile, """{ "Other": 1, "AI": { "Gemini": { "Model": "x-model" } } }""");

        LocalSettings.Save(_root, AiOptions.GeminiProvider, "  AIzaNEWKEY  ", model: null);

        var json = JsonNode.Parse(File.ReadAllText(LocalFile))!;
        Assert.Equal(1, json["Other"]!.GetValue<int>());
        Assert.Equal("Gemini", json["AI"]!["Provider"]!.GetValue<string>());
        Assert.Equal("AIzaNEWKEY", json["AI"]!["Gemini"]!["ApiKey"]!.GetValue<string>());
        Assert.Equal("x-model", json["AI"]!["Gemini"]!["Model"]!.GetValue<string>());

        var config = new ConfigurationManager();
        LocalSettings.Register(config, _root);
        Assert.Equal("AIzaNEWKEY", config["AI:Gemini:ApiKey"]);
    }

    [Fact]
    public void Save_creates_file_when_missing()
    {
        LocalSettings.Save(_root, AiOptions.GeminiProvider, "AIzaFIRST", "gemini-test");

        Assert.Contains("gemini-test", File.ReadAllText(LocalFile));
    }

    [Fact]
    public void Reports_hidden_txt_extension_and_missing_file()
    {
        Assert.Contains("bulunamadı", LocalSettings.Inspect(_root));

        File.WriteAllText(LocalFile + ".txt", "{}");
        Assert.Contains(".txt silinmeli", LocalSettings.Inspect(_root));
    }

    [Fact]
    public void Reports_invalid_json_instead_of_crashing()
    {
        File.WriteAllText(LocalFile, """{ "AI": { "Gemini": { "ApiKey": "x" }  """);

        LocalSettings.Register(new ConfigurationManager(), _root);
        Assert.Contains("JSON hatası", LocalSettings.Inspect(_root));
    }

    [Fact]
    public void Describe_masks_the_key()
    {
        var text = LocalSettings.Describe(new AiOptions { Gemini = new() { ApiKey = "AIzaSECRETVALUE9876" } });

        Assert.Contains("AIza…9876", text);
        Assert.DoesNotContain("SECRET", text);
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }
}
