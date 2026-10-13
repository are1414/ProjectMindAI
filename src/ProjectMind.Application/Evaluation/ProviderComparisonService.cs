using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProjectMind.Application.Abstractions;
using ProjectMind.Application.Ai;
using ProjectMind.Application.Analytics;
using ProjectMind.Application.Chat;
using ProjectMind.Application.Common;
using ProjectMind.Application.Demo;
using ProjectMind.Application.MissingWork;
using ProjectMind.Application.WhatIf;
using ProjectMind.Domain.Entities;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.Evaluation;

/// <summary>Bir ölçüm koşusunun özeti (liste ve rapor başlığı).</summary>
public sealed record EvaluationRunInfo(
    int Id, EvaluationRunKind Kind, string TaskSetVersion, string Providers, int Repeats, int? ProjectId, DateTime StartedAt,
    DateTime? FinishedAt, EvaluationRunStatus Status, string? Notes, int Calls)
{
    public static EvaluationRunInfo From(EvaluationRun r, int calls) =>
        new(r.Id, r.Kind, r.TaskSetVersion, r.Providers, r.Repeats, r.ProjectId, r.StartedAt, r.FinishedAt, r.Status, r.Notes, calls);
}

public sealed record ProviderComparisonReport(EvaluationRunInfo Run, IReadOnlyList<ProviderTaskStats> Rows);

/// <summary>
/// LLM sağlayıcı karşılaştırma koşucusu (Faz 9, D31). Sabit görev seti (<see cref="EvaluationTaskSet"/>, eval-v1) demo
/// projesinde (yoksa demo tohumlayıcısıyla oluşturulur), anahtarı olan her sağlayıcıyla n kez çalıştırılır. Her görev kendi
/// DI kapsamında, <see cref="EvaluationRunContext"/> ile o sağlayıcıya yönlendirilmiş gerçek servislerle (yorum, hibrit eksik
/// iş, sohbet) çalışır; böylece ölçülen yol kullanıcının yoluyla aynıdır. Kayıtlar <see cref="AiAnalysisLog.EvaluationRunId"/>
/// ile koşuya bağlanır. Koşucu hiçbir kartı uygulamaz: sohbette oluşan kartlar otomatik reddedilir ve koşuya etiketlenir
/// (RQ4 kabul oranına girmez), koşunun sohbet oturumları sonunda silinir. İptal edilebilir; yarım koşu "İptal edildi" kalır.
/// </summary>
public sealed class ProviderComparisonService(
    IServiceScopeFactory scopes,
    IAppDbContext db,
    IEvaluationModelCatalog catalog,
    IOptions<EvaluationOptions> options,
    TimeProvider clock)
{
    public const string AutoRejectedMessage = "Ölçüm koşusu: otomatik reddedildi (veri değişmedi).";
    public const string NoProviderMessage =
        "Karşılaştırma için API anahtarı ayarlı en az bir sağlayıcı gerekli (Mock atlanır). ⚙ Ayarlar'dan anahtar ekleyin.";

    private const int MaxNotesLength = 2000;

    public static readonly IReadOnlyList<string> CsvHeader =
    [
        "run_id", "task_set", "provider", "models", "task", "calls", "errors", "error_rate", "schema_checked", "schema_valid",
        "schema_valid_rate", "answered", "with_unverified", "unverified_share", "unverified_per_answer", "mean_duration_ms",
        "sd_duration_ms", "token_calls", "mean_input_tokens", "mean_output_tokens", "expected_tool_checks", "expected_tool_rate"
    ];

    /// <summary>Karşılaştırmaya girecek ve atlanacak sağlayıcılar (butonun yanındaki bilgi için; model çağrılmaz).</summary>
    public IReadOnlyList<EvaluationProvider> Providers() => catalog.GetProviders();

    public async Task<EvaluationRunInfo> RunAsync(int? repeats, IProgress<string>? progress, CancellationToken ct)
    {
        var o = options.Value;
        var n = repeats ?? o.Repeats;
        if (n < 1 || n > o.MaxRepeats)
            throw new BusinessRuleException($"Tekrar sayısı 1–{o.MaxRepeats} arasında olmalı.");

        var candidates = catalog.GetProviders();
        var providers = candidates.Where(p => p.IsAvailable).ToList();
        var skipped = candidates.Where(p => !p.IsAvailable)
            .Select(p => $"{p.Name}: {p.SkipReason ?? "yapılandırılmamış — atlandı."}").ToList();
        if (providers.Count == 0)
            throw new BusinessRuleException($"{NoProviderMessage} ({string.Join(" ", skipped)})");

        progress?.Report("Demo projesi hazırlanıyor…");
        var projectId = await EnsureDemoProjectAsync(ct);

        var run = new EvaluationRun
        {
            Kind = EvaluationRunKind.ProviderComparison,
            TaskSetVersion = EvaluationTaskSet.Version,
            Providers = string.Join(",", providers.Select(p => $"{p.Name}/{p.ModelName}")),
            Repeats = n,
            ProjectId = projectId,
            StartedAt = clock.GetUtcNow().UtcDateTime,
            Notes = skipped.Count == 0 ? null : Truncate(string.Join(" ", skipped))
        };
        db.EvaluationRuns.Add(run);
        await db.SaveChangesAsync(ct);

        var notes = new List<string>(skipped);
        try
        {
            // Girdiler koşu başında bir kez hesaplanır: her sağlayıcı ve tekrar aynı durum raporunu ve karşılaştırmayı yorumlar.
            progress?.Report("Proje durumu ve senaryolar hesaplanıyor…");
            var (status, comparison) = await PrepareInputsAsync(run.Id, projectId, ct);

            var total = providers.Count * n * EvaluationTaskSet.Tasks.Count;
            var done = 0;
            foreach (var provider in providers)
            {
                for (var repeat = 1; repeat <= n; repeat++)
                {
                    foreach (var task in EvaluationTaskSet.Tasks)
                    {
                        ct.ThrowIfCancellationRequested();
                        progress?.Report($"{provider.Name} · tekrar {repeat}/{n} · {task.Id} ({++done}/{total})");
                        var error = await RunTaskAsync(run.Id, projectId, provider, task, status, comparison, ct);
                        if (error is not null)
                            notes.Add($"{provider.Name}/{task.Id}/{repeat}: {error}");
                    }
                }
            }

            run.Status = EvaluationRunStatus.Completed;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            run.Status = EvaluationRunStatus.Cancelled;
        }
        catch (Exception ex) when (ex is BusinessRuleException or NotFoundException)
        {
            run.Status = EvaluationRunStatus.Failed;
            notes.Add(ex.Message);
        }

        run.FinishedAt = clock.GetUtcNow().UtcDateTime;
        run.Notes = notes.Count == 0 ? null : Truncate(string.Join(" ", notes));
        await db.SaveChangesAsync(CancellationToken.None);
        progress?.Report(run.Status == EvaluationRunStatus.Cancelled ? "Koşu iptal edildi." : "Koşu tamamlandı.");
        return EvaluationRunInfo.From(run, await db.AiAnalysisLogs.CountAsync(l => l.EvaluationRunId == run.Id, CancellationToken.None));
    }

    private async Task<int> EnsureDemoProjectAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoProjectSeeder>();
        return await seeder.FindExistingAsync(ct) ?? (await seeder.CreateAsync(ct)).ProjectId;
    }

    private async Task<(ProjectStatusReport Status, WhatIfComparison Comparison)> PrepareInputsAsync(
        int runId, int projectId, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<EvaluationRunContext>().Begin(runId, null, "inputs");
        var status = await scope.ServiceProvider.GetRequiredService<ProjectStatusService>().GetAsync(projectId, ct);
        var comparison = await scope.ServiceProvider.GetRequiredService<WhatIfService>()
            .CompareAsync(projectId, EvaluationTaskSet.Scenarios, WhatIfRunSource.Evaluation, ct);
        return (status, comparison);
    }

    /// <summary>Bir görevi bir sağlayıcıyla çalıştırır. Model hataları servislerde kayda yazılır; dönen metin yalnız koşu notudur.</summary>
    private async Task<string?> RunTaskAsync(int runId, int projectId, EvaluationProvider provider, EvaluationTask task,
        ProjectStatusReport status, WhatIfComparison comparison, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<EvaluationRunContext>().Begin(runId, provider.Name, task.Id, provider.Model);
        try
        {
            switch (task.Kind)
            {
                case EvaluationTaskKind.ProjectComment:
                    await sp.GetRequiredService<ProjectCommentService>().GenerateProjectCommentAsync(status, ct);
                    break;
                case EvaluationTaskKind.ScenarioComment:
                    await sp.GetRequiredService<ProjectCommentService>().GenerateScenarioCommentAsync(projectId, comparison, ct);
                    break;
                case EvaluationTaskKind.MissingWork:
                    await sp.GetRequiredService<MissingWorkService>().CheckHybridAsync(projectId, null, ct);
                    break;
                case EvaluationTaskKind.Chat:
                    await RunChatAsync(sp, projectId, task, ct);
                    break;
            }
            return null;
        }
        catch (Exception ex) when (ex is BusinessRuleException or NotFoundException)
        {
            return ex.Message;
        }
    }

    private static async Task RunChatAsync(IServiceProvider sp, int projectId, EvaluationTask task, CancellationToken ct)
    {
        var taskDb = sp.GetRequiredService<IAppDbContext>();
        var session = new ChatSession { ProjectId = projectId, Title = $"{EvaluationTaskSet.ChatTitlePrefix} {task.Id}" };
        taskDb.ChatSessions.Add(session);
        await taskDb.SaveChangesAsync(ct);
        try
        {
            await sp.GetRequiredService<ChatService>().SendAsync(session.Id, task.Question!, ct);
        }
        finally
        {
            await CleanUpSessionAsync(taskDb, session.Id);
        }
    }

    /// <summary>
    /// Koşunun sohbet oturumunu kaldırır: bekleyen kartlar otomatik reddedilir (uygulanmaz) ve oturumsuz kalır; mesajlar ve
    /// oturum silinir. Kartlar ve AI kaydı koşuya etiketli olarak kalır.
    /// </summary>
    private static async Task CleanUpSessionAsync(IAppDbContext taskDb, int sessionId)
    {
        var ct = CancellationToken.None;
        var now = DateTime.UtcNow;
        await taskDb.AiActions.Where(a => a.ChatSessionId == sessionId && a.Status == AiActionStatus.Pending)
            .ExecuteUpdateAsync(u => u
                .SetProperty(a => a.Status, AiActionStatus.Rejected)
                .SetProperty(a => a.DecidedAt, now)
                .SetProperty(a => a.ResultMessage, AutoRejectedMessage), ct);
        await taskDb.AiActions.Where(a => a.ChatSessionId == sessionId)
            .ExecuteUpdateAsync(u => u
                .SetProperty(a => a.ChatMessageId, (int?)null)
                .SetProperty(a => a.ChatSessionId, (int?)null), ct);
        await taskDb.ChatMessages.Where(m => m.ChatSessionId == sessionId).ExecuteDeleteAsync(ct);
        await taskDb.ChatSessions.Where(s => s.Id == sessionId).ExecuteDeleteAsync(ct);
    }

    /// <summary>Koşular (en yeni önce) ve her koşunun AI kaydı sayısı.</summary>
    public async Task<IReadOnlyList<EvaluationRunInfo>> ListRunsAsync(EvaluationRunKind kind, CancellationToken ct)
    {
        var runs = await db.EvaluationRuns.AsNoTracking().Where(r => r.Kind == kind).OrderByDescending(r => r.Id).ToListAsync(ct);
        var ids = runs.Select(r => (int?)r.Id).ToList();
        var counts = await db.AiAnalysisLogs.AsNoTracking().Where(l => ids.Contains(l.EvaluationRunId))
            .GroupBy(l => l.EvaluationRunId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        return runs.Select(r => EvaluationRunInfo.From(r, counts.FirstOrDefault(c => c.Key == r.Id)?.Count ?? 0)).ToList();
    }

    public async Task<ProviderComparisonReport> GetReportAsync(int runId, CancellationToken ct)
    {
        var run = await db.EvaluationRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, ct)
                  ?? throw new NotFoundException("Ölçüm koşusu", runId);
        var rows = await db.AiAnalysisLogs.AsNoTracking().Where(l => l.EvaluationRunId == runId)
            .Select(l => new EvaluationLogRow(l.EvaluationProvider ?? "?", l.Model, l.EvaluationTask, l.Kind, l.Outcome, l.SchemaValid,
                l.UnverifiedNumberCount, l.DurationMs, l.InputTokens, l.OutputTokens, l.ToolsCalled))
            .ToListAsync(ct);
        return new ProviderComparisonReport(EvaluationRunInfo.From(run, rows.Count), ProviderTaskStats.Compute(rows));
    }

    /// <summary>Sağlayıcı × görev özet tablosu (CSV, D30 biçimi). Ham çağrılar "AI kayıtları" CSV'sindedir (koşu kolonlarıyla).</summary>
    public async Task<CsvFile> ExportAsync(int runId, CancellationToken ct)
    {
        var report = await GetReportAsync(runId, ct);
        var csv = Csv.Build(CsvHeader, report.Rows.Select(r => (IReadOnlyList<string>)
        [
            Csv.Int(runId), report.Run.TaskSetVersion, r.Provider, r.Models, r.Task, Csv.Int(r.Calls), Csv.Int(r.Errors),
            Csv.Num(r.ErrorRate), Csv.Int(r.SchemaChecked), Csv.Int(r.SchemaValid), Csv.Num(r.SchemaValidRate), Csv.Int(r.Answered),
            Csv.Int(r.WithUnverified), Csv.Num(r.UnverifiedShare), Csv.Num(r.UnverifiedPerAnswer), Csv.Num(r.DurationMs.Mean, "0.#"),
            Csv.Num(r.DurationMs.StdDev, "0.#"), Csv.Int(r.TokenCalls), Csv.Num(r.MeanInputTokens, "0.#"),
            Csv.Num(r.MeanOutputTokens, "0.#"), Csv.Int(r.ExpectedToolChecks), Csv.Num(r.ExpectedToolRate)
        ]));
        return new CsvFile($"saglayici-karsilastirma-{runId}-{clock.GetUtcNow():yyyyMMdd-HHmm}.csv", csv);
    }

    private static string Truncate(string text) => text.Length > MaxNotesLength ? text[..MaxNotesLength] : text;
}
