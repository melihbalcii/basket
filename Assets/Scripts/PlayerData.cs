using UnityEngine;

/// <summary>Seçilen figürü, figür isimlerini ve karakter özelliklerini oyun boyunca hatırlar.</summary>
public static class PlayerData
{
    public static int SelectedFigure = 0; // 0..FigureCount-1
    public const int FigureCount = 10;

    // Her figürün ekranda görünecek adı (fig_01 -> Names[0], fig_02 -> Names[1] ...).
    // Gerçek isimleri buraya yaz; sırası Figures klasöründeki fig_01..fig_10 ile aynı.
    public static readonly string[] Names =
    {
        "OYUNCU 1",  // fig_01
        "OYUNCU 2",  // fig_02
        "KRALİÇE",   // fig_03  (asil)
        "KRAL",      // fig_04  (asil)
        "OYUNCU 5",  // fig_05
        "OYUNCU 6",  // fig_06
        "OYUNCU 7",  // fig_07
        "OYUNCU 8",  // fig_08
        "OYUNCU 9",  // fig_09
        "OYUNCU 10", // fig_10
    };

    public static string NameOf(int i)
        => (i >= 0 && i < Names.Length) ? Names[i] : "";

    /// <summary>Asil (Kral/Kraliçe) figürler: seçim ekranında her zaman en sonda sıralanır.</summary>
    public static bool IsRoyal(int i) => i == 2 || i == 3; // fig_03 Kraliçe, fig_04 Kral

    // ---- Karakter özellikleri: seçim anlamlı olsun, oyuncu hepsini denemek istesin ----
    public enum Trait
    {
        Sniper, // Keskin Nişancı: nişan yayı daha uzağı gösterir
        Power,  // Güçlü Kol: atışlar daha hızlı/uzağa gider
        Lucky,  // Şanslı: altın top daha sık gelir
    }

    // fig_01..fig_10 sırasına göre özellik dağılımı. Asiller (Kraliçe/Kral) ŞANSLI (altın).
    static readonly Trait[] Traits =
    {
        Trait.Sniper, Trait.Power,               // fig_01, fig_02
        Trait.Lucky,  Trait.Lucky,               // fig_03 Kraliçe, fig_04 Kral (asil -> şanslı)
        Trait.Sniper, Trait.Power, Trait.Lucky,  // fig_05, fig_06, fig_07
        Trait.Sniper, Trait.Power, Trait.Sniper, // fig_08, fig_09, fig_10
    };

    public static Trait TraitOf(int i)
        => (i >= 0 && i < Traits.Length) ? Traits[i] : Trait.Sniper;

    /// <summary>Seçili karakterin özelliği (rozet/renk gösterimi için).</summary>
    public static Trait CurrentTrait => TraitOf(SelectedFigure);

    /// <summary>Oyun içi etkiler BUNU kullanmalı: seçili karakterde bu yetenek var mı?
    /// ASİLLER (Kral/Kraliçe) TÜM yeteneklere sahiptir - en pahalı karakterlerin gücü.</summary>
    public static bool HasTrait(Trait t)
        => IsRoyal(SelectedFigure) || CurrentTrait == t;

    public static string TraitName(Trait t) => t switch
    {
        Trait.Sniper => "KESKİN NİŞANCI",
        Trait.Power => "GÜÇLÜ KOL",
        Trait.Lucky => "ŞANSLI",
        _ => "",
    };

    public static Color TraitColor(Trait t) => t switch
    {
        Trait.Sniper => new Color(0.45f, 0.85f, 1f),  // buz mavisi
        Trait.Power => new Color(1f, 0.45f, 0.25f),   // ateş turuncusu
        Trait.Lucky => new Color(1f, 0.82f, 0.2f),    // altın
        _ => Color.white,
    };
}
