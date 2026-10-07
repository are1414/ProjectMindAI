using System.Text.Json;
using System.Text.Json.Nodes;
using ProjectMind.Application.Ai;

namespace ProjectMind.Web.Services;

/// <summary>
/// Git'e girmeyen kişisel ayar dosyası (appsettings.Local.json): okuma, teşhis ve Ayarlar sayfasından yazma.
/// Asıl konum proje klasörüdür; dosya sonradan oluşturulsa da değişiklik yeniden başlatmadan okunur.
/// </summary>
public static class LocalSettings
{
    public const string FileName = "appsettings.Local.json";

    private static readonly JsonDocumentOptions ParseOptions =
        new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static string PrimaryPath(string contentRoot) => Path.Combine(contentRoot, FileName);

    private static IEnumerable<string> Candidates(string contentRoot) => new[]
        {
            PrimaryPath(contentRoot),                                              // src/ProjectMind.Web
            Path.GetFullPath(Path.Combine(contentRoot, "..", "..", FileName)),     // çözüm (repo) kökü
            Path.Combine(AppContext.BaseDirectory, FileName)                       // bin klasörü
        }
        .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ayar dosyalarını yapılandırmaya ekler. Proje klasöründeki dosya yoksa da izlenir (sonradan oluşturulabilir).</summary>
    public static void Register(ConfigurationManager configuration, string contentRoot)
    {
        var primary = PrimaryPath(contentRoot);
        foreach (var path in Candidates(contentRoot))
        {
            var exists = File.Exists(path);
            if ((exists && IsValidJson(path, out _)) || (!exists && path == primary))
                configuration.AddJsonFile(path, optional: true, reloadOnChange: true);
        }
    }

    /// <summary>Hangi dosyanın bulunduğunu/okunduğunu anlatan teşhis metni.</summary>
    public static string Inspect(string contentRoot)
    {
        var notes = new List<string>();
        foreach (var path in Candidates(contentRoot))
        {
            if (File.Exists(path))
                notes.Add(IsValidJson(path, out var error) ? $"Okundu: {path}" : $"'{path}' okunamadı, JSON hatası: {error}");
            else if (File.Exists(path + ".txt"))
                notes.Add($"'{path}.txt' bulundu: dosya adının sonundaki .txt silinmeli.");
        }

        if (notes.Count == 0)
            notes.Add($"{FileName} bulunamadı. Beklenen yer: {PrimaryPath(contentRoot)}");
        return string.Join("\n", notes);
    }

    /// <summary>Sağlayıcıyı ve anahtarı proje klasöründeki dosyaya yazar; dosyadaki diğer ayarlar korunur.</summary>
    public static string Save(string contentRoot, string provider, string apiKey, string? model)
    {
        var path = PrimaryPath(contentRoot);
        var root = File.Exists(path) && IsValidJson(path, out _)
            ? JsonNode.Parse(File.ReadAllText(path), documentOptions: ParseOptions)?.AsObject() ?? new JsonObject()
            : new JsonObject();

        var ai = root["AI"] as JsonObject ?? new JsonObject();
        root["AI"] = ai;
        ai["Provider"] = provider;

        var section = ai[provider] as JsonObject ?? new JsonObject();
        ai[provider] = section;
        section["ApiKey"] = apiKey.Trim();
        if (!string.IsNullOrWhiteSpace(model))
            section["Model"] = model.Trim();

        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    /// <summary>Anahtarın kendisini göstermeden durumunu özetler (ör. "AIza…k3Fw (39 karakter)").</summary>
    public static string Describe(AiOptions o)
    {
        static string Mask(string? key) => string.IsNullOrWhiteSpace(key)
            ? "YOK"
            : $"{key[..Math.Min(4, key.Length)]}…{key[^Math.Min(4, key.Length)..]} ({key.Length} karakter)";

        return $"AI:Provider = {o.Provider} · AI:Gemini:ApiKey = {Mask(o.Gemini.ApiKey)} · AI:Claude:ApiKey = {Mask(o.Claude.ApiKey)}";
    }

    private static bool IsValidJson(string path, out string? error)
    {
        try
        {
            using var _ = JsonDocument.Parse(File.ReadAllText(path), ParseOptions);
            error = null;
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
