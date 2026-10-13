using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.Evaluation;

/// <summary>Ölçüm koşusunun bir AI kaydı (hesap için gereken alanlar).</summary>
public sealed record EvaluationLogRow(
    string Provider, string? Model, string? Task, AiAnalysisKind Kind, AiAnalysisOutcome Outcome, bool? SchemaValid, int UnverifiedCount,
    long DurationMs, int? InputTokens, int? OutputTokens, string? ToolsCalled);

/// <summary>
/// Bir sağlayıcının bir görevdeki (veya <see cref="AllTasks"/> ile tüm görevlerdeki) ölçümleri (D31):
/// <list type="bullet">
/// <item>Hata oranı = model hatası (zaman aşımı, ağ, API) / çağrı.</item>
/// <item>Şema geçerlilik oranı = şeması geçerli / şeması denetlenen (JSON görevleri; sohbet ve model hatası denetlenmez).</item>
/// <item>Doğrulanamayan sayı oranı = en az bir doğrulanamayan sayı içeren cevap / başarılı cevap; ayrıca cevap başına ortalama sayı.</item>
/// <item>Süre: tüm çağrıların ortalaması ve örneklem standart sapması (ms).</item>
/// <item>Token: yalnız sağlayıcının kullanım bildirdiği çağrıların ortalaması.</item>
/// <item>Beklenen araç oranı (sohbet): görev setindeki salt okunur aracı çağıran cevap / sohbet çağrısı (yalnız sohbet
/// kayıtları; sohbetteki araç çağrısının tetiklediği eksik iş LLM kaydı sayılmaz, hatalı çağrı sayılmaz).</item>
/// </list>
/// </summary>
public sealed record ProviderTaskStats(
    string Provider, string Task, string Models, int Calls, int Errors, int SchemaChecked, int SchemaValid, int Answered,
    int WithUnverified, int UnverifiedTotal, DescriptiveStats DurationMs, int TokenCalls, decimal? MeanInputTokens,
    decimal? MeanOutputTokens, int ExpectedToolChecks, int ExpectedToolCalled)
{
    public const string AllTasks = "all";

    public decimal? ErrorRate => Calls == 0 ? null : (decimal)Errors / Calls;
    public decimal? SchemaValidRate => SchemaChecked == 0 ? null : (decimal)SchemaValid / SchemaChecked;
    public decimal? UnverifiedShare => Answered == 0 ? null : (decimal)WithUnverified / Answered;
    public decimal? UnverifiedPerAnswer => Answered == 0 ? null : (decimal)UnverifiedTotal / Answered;
    public decimal? ExpectedToolRate => ExpectedToolChecks == 0 ? null : (decimal)ExpectedToolCalled / ExpectedToolChecks;

    /// <summary>Sağlayıcı başına önce "tümü", ardından görev setindeki sırayla görev satırları.</summary>
    public static IReadOnlyList<ProviderTaskStats> Compute(IEnumerable<EvaluationLogRow> rows)
    {
        var list = rows.ToList();
        var result = new List<ProviderTaskStats>();
        foreach (var provider in list.GroupBy(r => r.Provider).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            result.Add(Of(provider.Key, AllTasks, provider.ToList()));
            foreach (var task in provider.GroupBy(r => r.Task ?? "").OrderBy(g => TaskOrder(g.Key)).ThenBy(g => g.Key, StringComparer.Ordinal))
                result.Add(Of(provider.Key, task.Key, task.ToList()));
        }
        return result;
    }

    private static int TaskOrder(string id)
    {
        var index = EvaluationTaskSet.Tasks.ToList().FindIndex(t => t.Id == id);
        return index < 0 ? int.MaxValue : index;
    }

    public static ProviderTaskStats Of(string provider, string task, IReadOnlyList<EvaluationLogRow> rows)
    {
        var answered = rows.Where(r => r.Outcome == AiAnalysisOutcome.Success).ToList();
        var withTokens = rows.Where(r => r.InputTokens is not null || r.OutputTokens is not null).ToList();
        var chats = rows.Select(r => (Row: r, Expected: EvaluationTaskSet.Find(r.Task)?.ExpectedTool))
            .Where(x => x.Expected is not null && x.Row.Kind == AiAnalysisKind.Chat && x.Row.Outcome != AiAnalysisOutcome.ModelError)
            .ToList();
        return new ProviderTaskStats(
            provider, task,
            string.Join(" | ", rows.Select(r => r.Model).Where(m => !string.IsNullOrEmpty(m)).Distinct().Order(StringComparer.Ordinal)),
            rows.Count,
            rows.Count(r => r.Outcome == AiAnalysisOutcome.ModelError),
            rows.Count(r => r.SchemaValid is not null),
            rows.Count(r => r.SchemaValid == true),
            answered.Count,
            answered.Count(r => r.UnverifiedCount > 0),
            answered.Sum(r => r.UnverifiedCount),
            DescriptiveStats.Of(rows.Select(r => (decimal)r.DurationMs)),
            withTokens.Count,
            withTokens.Count == 0 ? null : (decimal)withTokens.Average(r => r.InputTokens ?? 0),
            withTokens.Count == 0 ? null : (decimal)withTokens.Average(r => r.OutputTokens ?? 0),
            chats.Count,
            chats.Count(x => (x.Row.ToolsCalled ?? "").Split(',', StringSplitOptions.TrimEntries).Contains(x.Expected)));
    }
}
