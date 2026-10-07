using System.Text.Json;
using ProjectMind.Application.Ai;

namespace ProjectMind.Web.Services;

/// <summary>
/// Git'e girmeyen kişisel ayar dosyasını (appsettings.Local.json) birkaç olası konumda arar ve yükler.
/// Ne bulunduğu teşhis metni olarak saklanır; AI bağlı değilse kullanıcıya gösterilir.
/// </summary>
public static class LocalSettings
{
    public const string FileName = "appsettings.Local.json";

    public static string Load(ConfigurationManager configuration, string contentRoot)
    {
        var candidates = new[]
            {
                Path.Combine(contentRoot, FileName),                                  // src/ProjectMind.Web
                Path.GetFullPath(Path.Combine(contentRoot, "..", "..", FileName)),    // çözüm (repo) kökü
                Path.Combine(AppContext.BaseDirectory, FileName)                      // bin klasörü
            }
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var notes = new List<string>();
        foreach (var path in candidates)
        {
            if (!File.Exists(path))
            {
                if (File.Exists(path + ".txt"))
                    notes.Add($"'{path}.txt' bulundu: dosya adının sonundaki .txt silinmeli.");
                continue;
            }

            try
            {
                using var _ = JsonDocument.Parse(File.ReadAllText(path),
                    new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                configuration.AddJsonFile(path, optional: true, reloadOnChange: true);
                notes.Add($"Okundu: {path}");
            }
            catch (JsonException ex)
            {
                notes.Add($"'{path}' okunamadı, JSON hatası: {ex.Message}");
            }
        }

        if (notes.Count == 0)
            notes.Add($"{FileName} bulunamadı. Beklenen yer: {candidates[0]}");
        return string.Join("\n", notes);
    }

    /// <summary>Anahtarın kendisini göstermeden durumunu özetler (ör. "AIza…k3Fw, 39 karakter").</summary>
    public static string Describe(AiOptions o)
    {
        static string Mask(string? key) => string.IsNullOrWhiteSpace(key)
            ? "YOK"
            : $"{key[..Math.Min(4, key.Length)]}…{key[^Math.Min(4, key.Length)..]} ({key.Length} karakter)";

        return $"AI:Provider = {o.Provider} · AI:Gemini:ApiKey = {Mask(o.Gemini.ApiKey)} · AI:Claude:ApiKey = {Mask(o.Claude.ApiKey)}";
    }
}
