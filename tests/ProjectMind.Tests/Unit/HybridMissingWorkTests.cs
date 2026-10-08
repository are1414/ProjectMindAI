using System.Text.Json;
using ProjectMind.Application.Ai;
using ProjectMind.Application.MissingWork;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Tests.Unit;

/// <summary>Hibrit eksik işin deterministik parçaları (D26): tekrar eleme, büyüklük → saat, şema, kaynak, kabul oranı.</summary>
public class HybridMissingWorkTests
{
    [Theory]
    [InlineData("Veritabanı kurulumu", "veritabani KURULUMU", true)]          // Türkçe sadeleştirme + büyük/küçük harf
    [InlineData("Gereksinim analizi", "Detaylı gereksinim analizi", true)]    // kısa adın tüm kelimeleri uzun adda
    [InlineData("Birim test", "Birim testleri", true)]                       // ortak kök: test ~ testleri (ek ≤ 4 harf)
    [InlineData("Güvenlik test", "Güvenlik testi", true)]                    // ortak kök: test ~ testi
    [InlineData("Test", "test", true)]                                       // tek kelime: tam eşitlik
    [InlineData("Test", "Güvenlik testi", false)]                            // Tur 4a H5: tek kelimelik ad yalnız tam eşleşmede eler
    [InlineData("Veri", "Veritabanı yedekleme", false)]
    [InlineData("API", "API dokümantasyonu", false)]
    [InlineData("Uygulama", "Uygulama içi bildirim izinleri", false)]
    [InlineData("Veri modeli", "Veritabanı modeli", false)]                  // "veri" → "veritabani": ek 6 harf, ayrı kelime
    [InlineData("Backend API", "Backend API geliştirmesi", true)]
    [InlineData("Ödeme ve iade akışı", "Ödeme iade akışı", true)]            // bağlaç (ve) yok sayılır
    [InlineData("Yük testi", "Birim testleri", false)]                       // "yük" uzun adda yok
    [InlineData("KVKK uyum incelemesi", "Backend API", false)]
    [InlineData("UI", "UX", false)]                                          // kısa kelimede kök eşleşmesi yok
    public void Same_work_is_detected_by_normalized_name_similarity(string a, string b, bool expected)
    {
        Assert.Equal(expected, HybridMissingWork.IsSameWork(a, b));
        Assert.Equal(expected, HybridMissingWork.IsSameWork(b, a));   // simetrik
    }

    [Fact]
    public void Size_class_maps_to_named_hours_from_options()
    {
        var defaults = new MissingWorkOptions();
        Assert.Equal(8m, defaults.HoursFor(WorkSize.S));
        Assert.Equal(24m, defaults.HoursFor(WorkSize.M));
        Assert.Equal(80m, defaults.HoursFor(WorkSize.L));

        var custom = new MissingWorkOptions { SizeHours = new SizeHoursOptions { S = 4, M = 16, L = 40 } };
        Assert.Equal(16m, custom.HoursFor(WorkSize.M));
    }

    [Fact]
    public void Filter_drops_existing_rule_and_self_duplicates_assigns_hours_and_caps_count()
    {
        LlmMissingWorkSuggestion S(string name, WorkSize size) => new(name, WorkPhase.Analysis, Skill.Analysis, "gerekçe", size);
        var suggestions = new[]
        {
            S("Backend API geliştirmesi", WorkSize.L),   // mevcut iş "Backend API" ile aynı
            S("veritabanı KURULUMU", WorkSize.S),        // kural önerisi ile aynı
            S("KVKK uyum incelemesi", WorkSize.M),       // kalır → 24 saat
            S("KVKK uyum incelemesi yapılması", WorkSize.S), // az önce kalan öneriyle aynı
            S("Yük testi", WorkSize.L),                  // kalır → 80 saat
            S("Ödeme sağlayıcı sözleşmesi", WorkSize.S)  // tekrar değil ama üst sınır (2) doldu → gösterilmez, kayda "üst sınır"
        };

        var (kept, dropped) = HybridMissingWork.Filter(suggestions, ["Backend API"], ["Veritabanı kurulumu"],
            new MissingWorkOptions { MaxLlmSuggestions = 2 });

        Assert.Equal(["KVKK uyum incelemesi", "Yük testi"], kept.Select(k => k.Name));
        Assert.Equal([24m, 80m], kept.Select(k => k.Hours));
        Assert.Equal(
            [("Backend API geliştirmesi", "Backend API"), ("veritabanı KURULUMU", "Veritabanı kurulumu"),
             ("KVKK uyum incelemesi yapılması", "KVKK uyum incelemesi"), ("Ödeme sağlayıcı sözleşmesi", (string?)null)],
            dropped.Select(d => (d.Name, d.DuplicateOf)));
        Assert.Equal([false, false, false, true], dropped.Select(d => d.OverLimit));
    }

    [Fact]
    public void Card_source_is_decided_by_name_not_by_model_claim()
    {
        string[] rule = ["Veritabanı kurulumu", "Kullanıcı eğitimi"];
        string[] llm = ["KVKK uyum incelemesi", "Kullanıcı eğitimi"];

        Assert.Equal(AiActionSource.Rule, HybridMissingWork.ResolveSource("veritabani kurulumu", rule, llm));
        Assert.Equal(AiActionSource.Rule, HybridMissingWork.ResolveSource("Kullanıcı eğitimi", rule, llm));   // ikisinde de: kural önce
        Assert.Equal(AiActionSource.Llm, HybridMissingWork.ResolveSource("KVKK Uyum İncelemesi", rule, llm));
        Assert.Equal(AiActionSource.User, HybridMissingWork.ResolveSource("Ödeme entegrasyonu", rule, llm));
        Assert.Equal(AiActionSource.User, HybridMissingWork.ResolveSource("KVKK uyum incelemesi ve raporu", rule, llm)); // benzer ama aynı değil
    }

    private const string ValidItem =
        """{"name":"KVKK uyum incelemesi","phase":"Analysis","skill":"Analysis","reason":"Kişisel veri işleniyor.","size":"M"}""";

    [Fact]
    public void Schema_accepts_valid_answer()
    {
        var result = LlmMissingWorkSchema.TryParse($$"""{"suggestions":[{{ValidItem}}]}""", out var error);

        Assert.Null(error);
        var s = Assert.Single(result!.Suggestions);
        Assert.Equal(new LlmMissingWorkSuggestion("KVKK uyum incelemesi", WorkPhase.Analysis, Skill.Analysis, "Kişisel veri işleniyor.", WorkSize.M), s);
        Assert.Equal(0, result.DroppedInvalid);
        Assert.NotNull(LlmMissingWorkSchema.TryParse("""{"suggestions":[]}""", out _));   // boş liste geçerli
    }

    [Theory]
    [InlineData("")]
    [InlineData("öneri yok")]
    [InlineData("[]")]
    [InlineData("""{"items":[]}""")]
    [InlineData("""{"suggestions":[],"note":"x"}""")]
    [InlineData("""{"suggestions":"KVKK"}""")]
    public void Schema_rejects_invalid_top_level(string json)
    {
        Assert.Null(LlmMissingWorkSchema.TryParse(json, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Schema_truncates_too_many_items_instead_of_rejecting()
    {
        // Tur 4a H5 (TEST_PAZAR Q8): 12 madde → ilk 10 okunur, 2 kesilir; cevap geçersiz sayılmaz.
        var items = string.Join(",", Enumerable.Repeat(ValidItem, LlmMissingWorkSchema.MaxItems + 2));
        var result = LlmMissingWorkSchema.TryParse($$"""{"suggestions":[{{items}}]}""", out var error);

        Assert.Null(error);
        Assert.Equal(LlmMissingWorkSchema.MaxItems, result!.Suggestions.Count);
        Assert.Equal(2, result.Truncated);
        Assert.Equal(0, result.DroppedInvalid);
    }

    [Fact]
    public void Reply_lists_duplicates_and_suggestions_hidden_by_limit()
    {
        var hybrid = new HybridMissingWorkResult(
            new MissingWorkResult([], new Dictionary<string, IReadOnlyList<string>>()),
            [new LlmMissingWorkItem("KVKK uyum incelemesi", WorkPhase.Analysis, Skill.Analysis, "Kişisel veri.", WorkSize.M, 24)],
            LlmLayerStatus.Success, null,
            [new DroppedSuggestion("Backend API geliştirmesi", "Backend API"), new DroppedSuggestion("Yük testi", null)],
            [], null, "model", LlmTruncated: 2);

        var reply = ProjectMind.Application.Chat.ChatService.MissingWorkReply(hybrid, "TRY");

        Assert.Contains("Tekrar sayılıp elenen AI önerileri (1): Backend API geliştirmesi (≈ Backend API).", reply);
        Assert.Contains("Üst sınır nedeniyle gösterilmeyen AI önerisi: 3 (Yük testi).", reply);   // 1 üst sınır + 2 kesilen
    }

    [Theory]
    [InlineData("""{"name":"Yük testi","phase":"Test","skill":"Test","reason":"r","size":"L","hours":40}""")]   // sayı alanı: şema dışı
    [InlineData("""{"name":"Yük testi","phase":"Coding","skill":"Test","reason":"r","size":"L"}""")]            // bilinmeyen faz
    [InlineData("""{"name":"Yük testi","phase":"Test","skill":"None","reason":"r","size":"L"}""")]              // beceri yok
    [InlineData("""{"name":"Yük testi","phase":"Test","skill":"Backend, Test","reason":"r","size":"L"}""")]     // bileşik beceri
    [InlineData("""{"name":"Yük testi","phase":"Test","skill":"Test","reason":"r","size":"XL"}""")]             // bilinmeyen büyüklük
    [InlineData("""{"name":"Yük testi","phase":"Test","skill":"Test","reason":"r","size":2}""")]                // sayısal büyüklük
    [InlineData("""{"name":"Yük testi","phase":"Test","skill":"Test","reason":"r"}""")]                          // büyüklük (saat kaynağı) yok
    [InlineData("""{"name":"  ","phase":"Test","skill":"Test","reason":"r","size":"S"}""")]                      // boş ad
    [InlineData("\"Yük testi\"")]                                                                                 // nesne değil
    public void Invalid_item_is_dropped_but_valid_items_remain(string badItem)
    {
        var result = LlmMissingWorkSchema.TryParse($$"""{"suggestions":[{{badItem}},{{ValidItem}}]}""", out var error);

        Assert.Null(error);
        Assert.Equal("KVKK uyum incelemesi", Assert.Single(result!.Suggestions).Name);
        Assert.Equal(1, result.DroppedInvalid);
    }

    [Fact]
    public void Schema_sent_to_provider_restricts_enums_and_extra_fields()
    {
        var item = LlmMissingWorkSchema.Schema.GetProperty("properties").GetProperty("suggestions").GetProperty("items");

        Assert.False(item.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(["S", "M", "L"], item.GetProperty("properties").GetProperty("size").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()));
        Assert.DoesNotContain("None", item.GetProperty("properties").GetProperty("skill").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()));
        Assert.DoesNotContain(item.GetProperty("properties").EnumerateObject(), p => p.Value.GetProperty("type").GetString() is "number" or "integer");
        Assert.Equal(JsonValueKind.Array, item.GetProperty("required").ValueKind);
    }

    [Fact]
    public void Acceptance_rate_by_source_is_applied_over_decided()
    {
        var actions = new List<(AiActionSource, AiActionStatus)>();
        void Add(AiActionSource s, AiActionStatus st, int n) => actions.AddRange(Enumerable.Repeat((s, st), n));
        Add(AiActionSource.User, AiActionStatus.Pending, 2);
        Add(AiActionSource.Rule, AiActionStatus.Applied, 3);
        Add(AiActionSource.Rule, AiActionStatus.Rejected, 1);
        Add(AiActionSource.Rule, AiActionStatus.Pending, 1);
        Add(AiActionSource.Llm, AiActionStatus.Applied, 1);
        Add(AiActionSource.Llm, AiActionStatus.Rejected, 1);
        Add(AiActionSource.Llm, AiActionStatus.Failed, 1);

        var rows = SourceAcceptance.Compute(actions);

        Assert.Equal([AiActionSource.Rule, AiActionSource.Llm, AiActionSource.User], rows.Select(r => r.Source));
        Assert.Equal(new SourceAcceptance(AiActionSource.Rule, 5, 3, 1, 0, 1), rows[0]);
        Assert.Equal(0.75, rows[0].AcceptanceRate);                 // 3 / (3 + 1)
        Assert.Equal(0.5, rows[1].AcceptanceRate);                  // 1 / (1 + 1); hata veren orana girmez
        Assert.Equal(1, rows[1].Failed);
        Assert.Null(rows[2].AcceptanceRate);                        // karar yok
    }

    [Fact]
    public void Bulk_applied_cards_are_reported_separately()
    {
        // Tur 4a H4 (ANALIZ madde 2 kabul örneği): 2 tek tek uygulandı + 1 reddedildi + 3 toplu uygulandı →
        // tümü 5 / 6, yalnız tek tek kararlar 2 / 3.
        var actions = new (AiActionSource, AiActionStatus, bool)[]
        {
            (AiActionSource.Llm, AiActionStatus.Applied, false), (AiActionSource.Llm, AiActionStatus.Applied, false),
            (AiActionSource.Llm, AiActionStatus.Rejected, false),
            (AiActionSource.Llm, AiActionStatus.Applied, true), (AiActionSource.Llm, AiActionStatus.Applied, true),
            (AiActionSource.Llm, AiActionStatus.Applied, true),
            (AiActionSource.Llm, AiActionStatus.Failed, true)                 // toplu ama hata: orana girmez
        };

        var row = Assert.Single(SourceAcceptance.Compute(actions));

        Assert.Equal(new SourceAcceptance(AiActionSource.Llm, 7, 5, 1, 1, 0, 3), row);
        Assert.Equal(5d / 6, row.AcceptanceRate);
        Assert.Equal(2d / 3, row.IndividualAcceptanceRate);
    }
}
