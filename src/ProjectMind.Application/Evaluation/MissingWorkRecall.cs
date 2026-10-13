using ProjectMind.Application.MissingWork;
using ProjectMind.Application.WorkItems;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.Evaluation;

public enum RemovedGroup
{
    /// <summary>Şablonun kapsadığı iş: bir şablonun projedeki tek karşılığı (çıkarılınca o şablon "eksik" olur).</summary>
    Template,

    /// <summary>Hiçbir şablona uymayan, projeye özgü iş (kural katmanı tanımı gereği bulamaz).</summary>
    Other
}

/// <summary>Bellekte listeden çıkarılan iş ve (şablon grubunda) tek karşılığı olduğu şablonlar.</summary>
public sealed record RemovedItem(string Name, RemovedGroup Group, IReadOnlyList<string> TemplateKeys);

public sealed record RecallSetup(IReadOnlyList<RemovedItem> Removed, IReadOnlyList<WorkItemResponse> Remaining);

/// <summary>Bir grup (veya tümü) için bulma oranı.</summary>
public sealed record GroupRecall(int Removed, int Recovered)
{
    public decimal? Recall => Removed == 0 ? null : (decimal)Recovered / Removed;
}

/// <summary>
/// Bir modun (kural-yalnız / hibrit) sonucu: grup başına recall, değerlendirilen öneri sayısı, çıkarılan bir işe karşılık
/// gelen öneri sayısı ve precision. Precision alt sınırdır: çıkarılmamış ama gerçekten yararlı LLM önerileri de "yanlış"
/// sayılır (ilgililik insan değerlendirmesi ister).
/// </summary>
public sealed record RecallScore(
    GroupRecall Template, GroupRecall Other, GroupRecall All, int Suggestions, int MatchedSuggestions, int PreExisting,
    IReadOnlyList<string> RecoveredNames)
{
    public decimal? Precision => Suggestions == 0 ? null : (decimal)MatchedSuggestions / Suggestions;
}

/// <summary>
/// RQ4 "tohumlanmış eksik" deneyinin deterministik parçaları (D31): çıkarılacak işlerin seçimi, kural ve LLM önerilerinin
/// çıkarılan işlerle eşleştirilmesi ve recall / precision hesabı. Veritabanı değişmez; liste bellekte kısaltılır.
/// <list type="bullet">
/// <item>Uygun iş: alt işi olmayan, iptal edilmemiş iş. Şablon grubu: en az bir şablonun projedeki <b>tek</b> karşılığı olan iş.
/// Diğer grup: hiçbir şablona uymayan iş. Seçim, Id sırasındaki listenin tohumlu karıştırılmasıyla yapılır.</item>
/// <item>Kural önerisi (şablon t) çıkarılan r'ye karşılık gelir ⇔ tam listede t'yi kapsayan işler arasında r var.
/// Tam listede de eksik olan şablonlar ("önceden eksik") precision paydasına girmez.</item>
/// <item>LLM önerisi s, r'ye karşılık gelir ⇔ <see cref="HybridMissingWork.IsSameWork"/>(s, r) ya da r şablon grubundaysa
/// s'nin adı r'nin şablonunun anahtar kelimelerine uyar.</item>
/// <item>Recall = karşılığı bulunan çıkarılmış iş / çıkarılan iş; precision = en az bir çıkarılmış işe karşılık gelen öneri /
/// değerlendirilen öneri.</item>
/// </list>
/// </summary>
public static class MissingWorkRecall
{
    public const string Version = "recall-v1";

    public static RecallSetup SelectRemovals(
        ProjectType type, IReadOnlyList<WorkItemResponse> items, int templateCount, int otherCount, int seed)
    {
        var full = MissingWorkDetector.Detect(type, items);
        var parents = items.Where(i => i.ParentId is not null).Select(i => i.ParentId!.Value).ToHashSet();
        var eligible = items.Where(i => !parents.Contains(i.Id) && i.Status != WorkItemStatus.Cancelled)
            .OrderBy(i => i.Id).ToList();

        // Şablonun tek karşılığı olan işler (çıkarılınca kural katmanı o şablonu eksik bulabilir).
        var soleCover = full.Covered.Where(c => c.Value.Count == 1)
            .GroupBy(c => c.Value[0]).ToDictionary(g => g.Key, g => g.Select(c => c.Key).Order(StringComparer.Ordinal).ToList());
        var anyCover = full.Covered.SelectMany(c => c.Value).ToHashSet();

        var random = new Random(seed);
        var templateItems = Shuffle(eligible.Where(i => soleCover.ContainsKey(i.Name)).ToList(), random).Take(templateCount)
            .Select(i => new RemovedItem(i.Name, RemovedGroup.Template, soleCover[i.Name]));
        var otherItems = Shuffle(eligible.Where(i => !anyCover.Contains(i.Name)).ToList(), random).Take(otherCount)
            .Select(i => new RemovedItem(i.Name, RemovedGroup.Other, []));
        var removed = templateItems.Concat(otherItems).ToList();

        var removedNames = removed.Select(r => r.Name).ToHashSet();
        return new RecallSetup(removed, items.Where(i => !removedNames.Contains(i.Name)).ToList());
    }

    /// <summary>Fisher–Yates (tohumlu, deterministik).</summary>
    private static List<T> Shuffle<T>(List<T> list, Random random)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }

    /// <param name="full">Tam listede kural sonucu.</param>
    /// <param name="reduced">Kısaltılmış listede kural sonucu.</param>
    /// <param name="llmSuggestions">LLM katmanının (tekrar eleme sonrası) kalan öneri adları; kural-yalnız modda boş.</param>
    public static RecallScore Score(
        IReadOnlyList<RemovedItem> removed, MissingWorkResult full, MissingWorkResult reduced, IReadOnlyList<string> llmSuggestions)
    {
        var preExisting = full.Missing.Select(m => m.TemplateKey).ToHashSet();
        var recovered = new HashSet<string>();
        var suggestions = 0;
        var matched = 0;

        foreach (var m in reduced.Missing.Where(m => !preExisting.Contains(m.TemplateKey)))
        {
            suggestions++;
            var covers = full.Covered.TryGetValue(m.TemplateKey, out var names) ? names : [];
            var hits = removed.Where(r => covers.Contains(r.Name)).ToList();
            if (hits.Count > 0)
                matched++;
            recovered.UnionWith(hits.Select(r => r.Name));
        }

        var templates = WorkTemplateCatalog.All.ToDictionary(t => t.Key);
        foreach (var s in llmSuggestions)
        {
            suggestions++;
            var normalized = MissingWorkDetector.Normalize(s);
            var hits = removed.Where(r => HybridMissingWork.IsSameWork(s, r.Name)
                                          || r.TemplateKeys.Any(k => templates.TryGetValue(k, out var t)
                                                                     && MissingWorkDetector.Matches(normalized, t.Keywords)))
                .ToList();
            if (hits.Count > 0)
                matched++;
            recovered.UnionWith(hits.Select(r => r.Name));
        }

        GroupRecall Of(IEnumerable<RemovedItem> group)
        {
            var g = group.ToList();
            return new GroupRecall(g.Count, g.Count(r => recovered.Contains(r.Name)));
        }

        return new RecallScore(Of(removed.Where(r => r.Group == RemovedGroup.Template)),
            Of(removed.Where(r => r.Group == RemovedGroup.Other)), Of(removed), suggestions, matched,
            reduced.Missing.Count(m => preExisting.Contains(m.TemplateKey)),
            removed.Where(r => recovered.Contains(r.Name)).Select(r => r.Name).ToList());
    }
}
