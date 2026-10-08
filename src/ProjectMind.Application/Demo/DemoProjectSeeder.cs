using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectMind.Application.Abstractions;
using ProjectMind.Application.Analytics;
using ProjectMind.Application.Common;
using ProjectMind.Application.Dependencies;
using ProjectMind.Application.Ml;
using ProjectMind.Application.Overview;
using ProjectMind.Application.People;
using ProjectMind.Application.Planning;
using ProjectMind.Application.Projects;
using ProjectMind.Application.WorkItems;
using ProjectMind.Domain.Entities;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.Demo;

/// <summary>Demo üretim ayarları (ayar bölümü "Demo").</summary>
public sealed class DemoOptions
{
    public const string Section = "Demo";

    /// <summary>Proje bu haftanın pazartesisinden kaç hafta önce başlar (her hafta bir durum girişi + snapshot).</summary>
    public int WeeksBack { get; set; } = 9;

    /// <summary>İş bazında hız/maliyet sapmalarının tohumu (aynı tohum = aynı demo).</summary>
    public int Seed { get; set; } = 2026;

    /// <summary>Ekibin plana göre ortalama hızı (1 = plana uygun). SPI(t) bu değerin biraz altında çıkar (bağımlılık + bloke iş).</summary>
    public double Velocity { get; set; } = 0.9;

    /// <summary>İş bazında hız sapması (±, oransal).</summary>
    public double VelocityJitter { get; set; } = 0.05;

    /// <summary>Harcanan saatin kazanılan saate oranı aralığı (1,05–1,20 → CPI ≈ 0,87).</summary>
    public double MinCostFactor { get; set; } = 1.05;
    public double MaxCostFactor { get; set; } = 1.20;
}

public sealed record DemoSeedResult(int ProjectId, int ChatSessionId, string ProjectName, int Snapshots);

/// <summary>Demo için zamanı geri alınabilen saat: verilen yerel zamanı döner, yerel saat dilimi gerçek saatinkidir.</summary>
public sealed class DemoClock(TimeProvider inner) : TimeProvider
{
    private DateTimeOffset? _now;

    public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;
    public override DateTimeOffset GetUtcNow() => _now ?? inner.GetUtcNow();

    public void SetLocal(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time);
        _now = new DateTimeOffset(local, LocalTimeZone.GetUtcOffset(local)).ToUniversalTime();
    }

    /// <summary>Gerçek saate döner.</summary>
    public void Reset() => _now = null;
}

/// <summary>Demo ilerlemesinin saf hesapları (elle doğrulanabilir, birim testli).</summary>
public static class DemoProgress
{
    private const int FullPercent = 100;

    /// <summary>
    /// İşin, baseline'daki [start, end] iş günü penceresine göre % tamamlanması. Ekip <paramref name="velocity"/> hızında
    /// çalışır: durum günü <paramref name="statusIndex"/> (0 = planın ilk iş günü, dahil) itibarıyla geçen efektif süre
    /// (statusIndex + 1) × hız iş günüdür. Pencerenin bu kadarı geçtiyse o oranda tamamlanmıştır (aşağı yuvarlanır).
    /// Örn. pencere 0–9, 5. gün sonu (index 4), hız 0,8 → efektif 4 gün → %40.
    /// </summary>
    public static int PercentAt(int startIndex, int endIndex, int statusIndex, double velocity)
    {
        var span = Math.Max(endIndex - startIndex + 1, 1);
        var effective = (statusIndex + 1) * velocity;
        var fraction = (effective - startIndex) / span;
        return fraction >= 1 ? FullPercent : fraction <= 0 ? 0 : (int)Math.Floor(fraction * FullPercent);
    }

    /// <summary>Harcanan saat = tahmini efor × %tamamlanma × maliyet çarpanı, tam saate yuvarlanır.</summary>
    public static decimal ActualHours(decimal estimatedHours, int percent, double costFactor) =>
        Math.Round(estimatedHours * percent / FullPercent * (decimal)costFactor, 0, MidpointRounding.AwayFromZero);
}

/// <summary>
/// "Mobile Banking Modernization" demo projesini, proje yöneticisi uygulamayı haftalardır kullanıyormuş gibi üretir.
/// Gerçek Application servisleri (proje, kişi, iş, bağımlılık, plan + baseline, durum) geriye alınmış bir saatle
/// yeniden oynatılır: proje <see cref="DemoOptions.WeeksBack"/> hafta önce başlar ve baseline'ı alınır; her cuma
/// ilerleme girilir ve durum hesaplanır (StatusUpdate + günlük snapshot, ML tahmini dahil); son olarak bugün.
/// LLM çağrılmaz; tüm sayılar sabit senaryo + tohumlu sapmalardan C#'ta üretilir. Aynı ad varsa ikinci kopya açılmaz.
/// </summary>
public sealed class DemoProjectSeeder(
    IAppDbContext db, TimeProvider clock, IOptions<HealthOptions> healthOptions, IDelayPredictor predictor,
    IOptions<DemoOptions> options)
{
    private const int FullPercent = 100;
    private static readonly TimeOnly MorningTime = new(9, 0);
    private static readonly TimeOnly EveningTime = new(18, 0);
    private const int DaysToFriday = 4;

    public const string AlreadyExistsMessage =
        $"\"{DemoScenario.ProjectName}\" demo projesi zaten var. Sol menüden açabilirsiniz; yeniden oluşturmak için önce " +
        "o sohbeti projeyle birlikte silin.";

    /// <summary>Demo projesi zaten varsa Id'si.</summary>
    public async Task<int?> FindExistingAsync(CancellationToken ct) =>
        await db.Projects.AsNoTracking().Where(p => p.Name == DemoScenario.ProjectName)
            .OrderBy(p => p.Id).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);

    public async Task<DemoSeedResult> CreateAsync(CancellationToken ct)
    {
        if (await FindExistingAsync(ct) is not null)
            throw new BusinessRuleException(AlreadyExistsMessage);

        var o = options.Value;
        var demoClock = new DemoClock(clock);
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var thisMonday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var start = thisMonday.AddDays(-7 * o.WeeksBack);

        var projects = new ProjectService(db);
        var people = new PersonService(db);
        var workItems = new WorkItemService(db, demoClock);
        var dependencies = new DependencyService(db);
        var schedules = new ScheduleService(db, new ProjectOverviewService(projects, people, workItems, dependencies), demoClock);
        var status = new ProjectStatusService(db, schedules, demoClock, healthOptions, predictor);

        // 1) Hafta 0, pazartesi sabahı: proje, ekip, işler, bağımlılıklar; plan uygulanır ve baseline alınır.
        demoClock.SetLocal(start, MorningTime);
        var project = await projects.CreateAsync(new ProjectRequest
        {
            Name = DemoScenario.ProjectName,
            Description = DemoScenario.Description,
            Type = DemoScenario.Type,
            Status = ProjectStatus.Active,
            StartDate = start,
            TargetEndDate = start.AddDays(7 * (DemoScenario.TargetWeeks - 1) + DaysToFriday),
            Budget = DemoScenario.Budget,
            Currency = DemoScenario.Currency,
            HoursPerDay = DemoScenario.HoursPerDay
        }, ct);
        var projectId = project.Id;

        var personIds = new Dictionary<string, int>();
        foreach (var person in DemoScenario.People)
        {
            var created = await people.CreateAsync(projectId, new PersonRequest
            {
                Name = person.Name, Skills = [.. person.Skills],
                WeeklyCapacityHours = person.WeeklyCapacityHours, HourlyCost = person.HourlyCost
            }, ct);
            personIds[person.Name] = created.Id;
        }

        var ids = new Dictionary<string, int>();
        await CreateTasksAsync(workItems, dependencies, projectId, DemoScenario.Tasks, DemoScenario.Tasks, ids, personIds, ct);
        await schedules.ApplyAsync(projectId, ct);

        // 2) Her cuma akşamı (bugünden önce) ilerleme + durum; en son bugün.
        var random = new Random(o.Seed);
        var allTasks = DemoScenario.Tasks.Concat(DemoScenario.AddedScope).ToList();
        var leafKeys = LeafKeys(allTasks);
        var velocity = allTasks.ToDictionary(t => t.Key, _ => o.Velocity * (1 + (random.NextDouble() * 2 - 1) * o.VelocityJitter));
        var costFactor = allTasks.ToDictionary(t => t.Key,
            _ => o.MinCostFactor + random.NextDouble() * (o.MaxCostFactor - o.MinCostFactor));
        var predecessors = LeafPredecessors(allTasks, leafKeys);

        var planned = (await workItems.ListAsync(projectId, ct)).ToDictionary(w => w.Id);
        var firstDay = new WorkCalendar(start).FirstDay;
        var window = ids.Where(kv => leafKeys.Contains(kv.Key) && planned[kv.Value].PlannedStart is not null)
            .ToDictionary(kv => kv.Key, kv => (
                Start: WorkCalendar.WorkdaysBetween(firstDay, planned[kv.Value].PlannedStart!.Value),
                End: WorkCalendar.WorkdaysBetween(firstDay, planned[kv.Value].PlannedEnd!.Value)));

        var statusDays = Enumerable.Range(0, o.WeeksBack + 1)
            .Select(w => (Week: w, Day: start.AddDays(7 * w + DaysToFriday)))
            .Where(x => x.Day < today)
            .Append((Week: o.WeeksBack, Day: today))
            .ToList();
        var addedWeek = (int?)null;

        foreach (var (week, day) in statusDays)
        {
            if (day == today)
                demoClock.Reset();
            else
                demoClock.SetLocal(day, EveningTime);

            if (week >= DemoScenario.AddedScopeWeek && addedWeek is null)
            {
                await CreateTasksAsync(workItems, dependencies, projectId, DemoScenario.AddedScope, allTasks, ids, personIds, ct);
                addedWeek = week;
            }
            if (week >= DemoScenario.CancelledWeek)
                await SetStatusAsync(workItems, projectId, ids[DemoScenario.CancelledTaskKey], WorkItemStatus.Cancelled, ct);

            var statusIndex = WorkCalendar.WorkdaysBetween(firstDay, day);
            var current = (await workItems.ListAsync(projectId, ct)).ToDictionary(w => w.Id);
            var done = current.Values.Where(w => w.Status == WorkItemStatus.Done).Select(w => w.Id).ToHashSet();

            // Plan sırasıyla: bir iş, öncülleri bitmeden ilerlemez (FS bağımlılığı).
            var order = leafKeys.Where(ids.ContainsKey)
                .OrderBy(k => window.TryGetValue(k, out var w) ? w.Start : int.MaxValue).ThenBy(k => ids[k]);
            foreach (var key in order)
            {
                var item = current[ids[key]];
                if (item.Status is WorkItemStatus.Done or WorkItemStatus.Cancelled or WorkItemStatus.Blocked)
                    continue;
                if (key == DemoScenario.BlockedTaskKey && week >= DemoScenario.BlockedFromWeek)
                {
                    // Tedarikçi erişimi gelmedi: iş bloke, ilerlemesi olduğu yerde kalır.
                    await SetStatusAsync(workItems, projectId, item.Id, WorkItemStatus.Blocked, ct);
                    continue;
                }
                if (!predecessors[key].Where(ids.ContainsKey).All(p => done.Contains(ids[p])))
                    continue;

                int percent;
                if (window.TryGetValue(key, out var w))
                    percent = DemoProgress.PercentAt(w.Start, w.End, statusIndex, velocity[key]);
                else   // baseline sonrası eklenen iş: eklendiği haftadan sonra sabit haftalık ilerleme
                    percent = Math.Min(FullPercent, (week - (addedWeek ?? week)) * DemoScenario.AddedScopeWeeklyPercent);
                percent = Math.Max(percent, item.PercentComplete);

                var newStatus = percent >= FullPercent ? WorkItemStatus.Done
                    : percent > 0 ? WorkItemStatus.InProgress : WorkItemStatus.NotStarted;
                if (newStatus == item.Status && percent == item.PercentComplete)
                    continue;

                var request = WorkItemRequest.From(item);
                request.Status = newStatus;
                request.PercentComplete = percent;
                request.ActualHours = Math.Max(item.ActualHours,
                    DemoProgress.ActualHours(item.EstimatedHours, percent, costFactor[key]));
                await workItems.UpdateAsync(projectId, item.Id, request, ct);
                if (newStatus == WorkItemStatus.Done)
                    done.Add(item.Id);
            }

            await status.GetAsync(projectId, ct);
        }

        demoClock.Reset();
        var session = new ChatSession { ProjectId = projectId, Title = DemoScenario.ChatTitle };
        db.ChatSessions.Add(session);
        await db.SaveChangesAsync(ct);

        var snapshots = await db.ProjectSnapshots.CountAsync(s => s.ProjectId == projectId, ct);
        return new DemoSeedResult(projectId, session.Id, DemoScenario.ProjectName, snapshots);
    }

    private static async Task CreateTasksAsync(WorkItemService workItems, DependencyService dependencies, int projectId,
        IReadOnlyList<DemoTask> tasks, IReadOnlyList<DemoTask> all, Dictionary<string, int> ids,
        IReadOnlyDictionary<string, int> personIds, CancellationToken ct)
    {
        var leafKeys = LeafKeys(all);
        foreach (var t in tasks)
        {
            var created = await workItems.CreateAsync(projectId, new WorkItemRequest
            {
                Name = t.Name,
                Description = t.Description,
                Phase = t.Phase,
                RequiredSkill = t.Skill,
                Priority = t.Priority,
                EstimatedHours = leafKeys.Contains(t.Key) ? t.Hours : LeafHours(all, leafKeys, t.Key),
                ParentId = t.ParentKey is null ? null : ids[t.ParentKey],
                AssigneeId = t.Assignee is null ? null : personIds[t.Assignee]
            }, ct);
            ids[t.Key] = created.Id;
        }
        foreach (var t in tasks)
            foreach (var before in t.After ?? [])
                await dependencies.CreateAsync(projectId,
                    new DependencyRequest { PredecessorId = ids[before], SuccessorId = ids[t.Key] }, ct);
    }

    private static async Task SetStatusAsync(WorkItemService workItems, int projectId, int id, WorkItemStatus status,
        CancellationToken ct)
    {
        var item = await workItems.GetAsync(projectId, id, ct);
        if (item.Status == status)
            return;
        var request = WorkItemRequest.From(item);
        request.Status = status;
        await workItems.UpdateAsync(projectId, id, request, ct);
    }

    private static HashSet<string> LeafKeys(IReadOnlyList<DemoTask> tasks) =>
        tasks.Where(t => tasks.All(c => c.ParentKey != t.Key)).Select(t => t.Key).ToHashSet();

    private static IEnumerable<string> LeavesOf(IReadOnlyList<DemoTask> tasks, HashSet<string> leafKeys, string key) =>
        leafKeys.Contains(key) ? [key] : tasks.Where(t => t.ParentKey == key).SelectMany(c => LeavesOf(tasks, leafKeys, c.Key));

    private static decimal LeafHours(IReadOnlyList<DemoTask> tasks, HashSet<string> leafKeys, string key) =>
        LeavesOf(tasks, leafKeys, key).Sum(k => tasks.First(t => t.Key == k).Hours);

    /// <summary>Yaprak iş → öncül yaprak işler (üst işe verilen bağımlılık alt işlerine açılır; ScheduleService ile aynı).</summary>
    private static Dictionary<string, HashSet<string>> LeafPredecessors(IReadOnlyList<DemoTask> tasks, HashSet<string> leafKeys)
    {
        var result = leafKeys.ToDictionary(k => k, _ => new HashSet<string>());
        foreach (var t in tasks)
            foreach (var before in t.After ?? [])
                foreach (var s in LeavesOf(tasks, leafKeys, t.Key))
                    result[s].UnionWith(LeavesOf(tasks, leafKeys, before));
        return result;
    }
}
