using UnityEngine;

/// <summary>
/// Top görünümleri (skin) kataloğu ve kalıcılığı. Görseller Resources/Balls/ball_XX.png
/// (varsayılan: Resources/Sprites/ball). Satın alma coin'le; seçim PlayerPrefs'te saklanır.
/// HER TOPUN BİR ÖZELLİĞİ VAR ve fiyat arttıkça avantaj büyür (KLASİK bedava/özelliksiz).
/// </summary>
public static class BallSkins
{
    /// <summary>Topların oyun içi avantajları (fiyatla orantılı güç sırasında).</summary>
    public enum Perk
    {
        None,          // KLASİK: başlangıç topu, özelliksiz
        SlowHoop,      // BUZ: pota %12 daha yavaş hareket eder
        CoinPerBasket, // ZEHİR: her basket +1 coin
        AimStretch,    // PEMBE: nişan yayı %15 daha uzağı gösterir
        PowerBoost,    // MOR: atış gücü +%5
        ComboShield,   // GECE: oyun başına 1 ıska komboyu bozmaz
        GoldenOften,   // ALTIN: altın top 4 yerine 3 atışta bir gelir
        ExtraBall,     // GALAKSİ: klasik modda +1 ekstra top
    }

    public class Skin
    {
        public string id; public string name; public int cost;
        public Perk perk; public string perkDesc;
    }

    public static readonly Skin[] All =
    {
        new Skin { id = "ball",    name = "KLASİK",  cost = 0,    perk = Perk.None,          perkDesc = "Başlangıç topu" },
        new Skin { id = "ball_02", name = "BUZ",     cost = 150,  perk = Perk.SlowHoop,      perkDesc = "Potayı yavaşlatır (%12)" },
        new Skin { id = "ball_03", name = "ZEHİR",   cost = 250,  perk = Perk.CoinPerBasket, perkDesc = "Her basket +1 coin" },
        new Skin { id = "ball_04", name = "PEMBE",   cost = 300,  perk = Perk.AimStretch,    perkDesc = "Nişan yayı %15 uzun" },
        new Skin { id = "ball_05", name = "MOR",     cost = 450,  perk = Perk.PowerBoost,    perkDesc = "Atış gücü +%5" },
        new Skin { id = "ball_06", name = "GECE",    cost = 600,  perk = Perk.ComboShield,   perkDesc = "1 ıska komboyu bozmaz" },
        new Skin { id = "ball_07", name = "ALTIN",   cost = 900,  perk = Perk.GoldenOften,   perkDesc = "Altın top daha sık (3 atışta bir)" },
        new Skin { id = "ball_08", name = "GALAKSİ", cost = 1500, perk = Perk.ExtraBall,     perkDesc = "Klasikte +1 ekstra top" },
    };

    public static string SelectedId
    {
        get => PlayerPrefs.GetString("vl_ball", "ball");
        private set { PlayerPrefs.SetString("vl_ball", value); PlayerPrefs.Save(); }
    }

    /// <summary>Seçili topun özelliği (oyun içi etkiler buradan okur).</summary>
    public static Perk CurrentPerk
    {
        get
        {
            foreach (var s in All)
                if (s.id == SelectedId) return s.perk;
            return Perk.None;
        }
    }

    public static bool IsUnlocked(Skin s)
        => s.cost == 0 || PlayerPrefs.GetInt("vl_ballskin_" + s.id, 0) == 1;

    /// <summary>Yeterli coin varsa satın alır (ve seçer). Başarılıysa true.</summary>
    public static bool TryBuy(Skin s)
    {
        if (IsUnlocked(s)) { Select(s); return true; }
        if (!Progress.TrySpend(s.cost)) return false;
        PlayerPrefs.SetInt("vl_ballskin_" + s.id, 1);
        Select(s);
        return true;
    }

    public static void Select(Skin s)
    {
        if (IsUnlocked(s)) SelectedId = s.id;
    }

    public static Sprite SpriteOf(Skin s)
        => s.id == "ball" ? Resources.Load<Sprite>("Sprites/ball") : Resources.Load<Sprite>("Balls/" + s.id);

    /// <summary>Seçili görünümün sprite'ı (dosya yoksa klasik topa düşer - oyun asla topsuz kalmaz).</summary>
    public static Sprite CurrentSprite()
    {
        foreach (var s in All)
            if (s.id == SelectedId)
            {
                var spr = SpriteOf(s);
                if (spr != null) return spr;
                break;
            }
        return Resources.Load<Sprite>("Sprites/ball");
    }
}
