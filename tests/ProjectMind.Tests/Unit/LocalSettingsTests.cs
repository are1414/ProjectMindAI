using Microsoft.Extensions.Configuration;
using ProjectMind.Application.Ai;
using ProjectMind.Web.Services;

namespace ProjectMind.Tests.Unit;

public class LocalSettingsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("pm-local-").FullName;

    [Fact]
    public void Reads_key_from_content_root_file()
    {
        File.WriteAllText(Path.Combine(_root, LocalSettings.FileName),
            """{ "AI": { "Provider": "Gemini", "Gemini": { "ApiKey": "AIzaTEST1234" } } }""");
        var config = new ConfigurationManager();

        var report = LocalSettings.Load(config, _root);

        Assert.Contains("Okundu", report);
        Assert.Equal("AIzaTEST1234", config["AI:Gemini:ApiKey"]);
    }

    [Fact]
    public void Reports_hidden_txt_extension_and_missing_file()
    {
        File.WriteAllText(Path.Combine(_root, LocalSettings.FileName + ".txt"), "{}");

        var report = LocalSettings.Load(new ConfigurationManager(), _root);

        Assert.Contains(".txt silinmeli", report);
    }

    [Fact]
    public void Reports_invalid_json_instead_of_crashing()
    {
        File.WriteAllText(Path.Combine(_root, LocalSettings.FileName), """{ "AI": { "Gemini": { "ApiKey": "x" }  """);

        var report = LocalSettings.Load(new ConfigurationManager(), _root);

        Assert.Contains("JSON hatası", report);
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
