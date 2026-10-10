using ProjectMind.Domain.Enums;

namespace ProjectMind.Domain.Entities;

/// <summary>
/// Bir what-if karşılaştırma çalıştırmasının kaydı (RQ3 kullanım kanıtı): kim tetikledi (panel / AI aracı), senaryo
/// değişiklikleri ve her senaryonun (mevcut plan dahil) P50/P80 bitişi, hedefe yetişme olasılığı ve süre.
/// Proje silinse de kalır (yabancı anahtar yok), veriyi değiştirmez.
/// </summary>
public class WhatIfRunLog : Entity
{
    public int ProjectId { get; set; }
    public WhatIfRunSource Source { get; set; }
    public int ScenarioCount { get; set; }
    public int Iterations { get; set; }
    public int Seed { get; set; }
    public long DurationMs { get; set; }

    /// <summary>Senaryo sonuçları JSON (mevcut plan ilk satır): ad, değişiklikler, P50, P80, hedef, olasılık, P80 farkı.</summary>
    public required string ResultsJson { get; set; }
}
