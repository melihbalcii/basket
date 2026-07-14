using UnityEngine;

/// <summary>
/// Oyunun tüm ayarları tek yerde. Değer değiştirip oyunun hissini ayarlayabilirsin.
/// (Bu dosyada kod mantığı yok, sadece sayılar.)
/// </summary>
public static class GameConfig
{
    // --- Kamera ---
    public const float CameraSize = 5f;                       // ortografik kamera boyutu
    // Dar ekranlarda (telefon dik tutulunca) oyuncu ve pota ekran dışında kalmasın:
    // kamera, yarı-genişlik en az bu kadar olacak şekilde otomatik uzaklaşır.
    public const float MinHalfWidth = 7.0f;
    public static readonly Color SkyColor = new Color(0.10f, 0.12f, 0.20f);

    // --- Oyuncu (figür) ---
    public static readonly Vector2 PlayerBase = new Vector2(-5.6f, -4.0f); // figürün ayağının yere bastığı nokta
    public const float PlayerHeight = 4.8f;                   // figür dünya birimi cinsinden boy

    // --- Top ---
    public static readonly Vector2 BallOffsetFromBase = new Vector2(1.5f, 1.95f); // topun figüre göre konumu (sağ el)
    public const float BallDiameter = 0.85f;
    public const float BallGravity = 1.45f;                   // yerçekimi çarpanı (büyük = daha hızlı düşer) - hafif floaty arc için düşürüldü
    public const float BallBounce = 0.3f;                     // 0.4 -> 0.3: çemberde pinball gibi sekmesin, düşüp girsin

    // --- Pota (tam tahta+çember+file görseli; sprite merkezi = tahtanın ortası, çember biraz aşağıda) ---
    // NOT: RimGap, RimYOffset, Board* değerleri hoop.png (496x465) üzerinde piksel renk analiziyle
    // (turuncu tahta vs. mavi çember geçiş noktaları) TAM ÖLÇÜLEREK hesaplandı - tahmini değil.
    // Görsel değişirse yeniden ölçülmeli.
    public static readonly Vector2 HoopPos = new Vector2(4.2f, 2.6f); // pota (tahta) MERKEZİ
    public const float HoopHeight = 3.4f;                     // pota dünya YÜKSEKLİĞİ (büyütüldü: 2.8 -> 3.4)
    public const float RimGap = 1.70f;                        // çember açıklığı (top buradan geçer) - ölçülen orana göre büyütüldü
    public const float RimThickness = 0.22f;
    public const float RimYOffset = -0.40f;                   // çemberin tahta merkezine göre dikey konumu - ölçülen orana göre büyütüldü

    // --- Skor tetiği ---
    public const float ScoreTriggerHeight = 0.30f;            // skor tetiğinin dikey kalınlığı (pota büyüklüğüyle orantılı)
    public const int NormalPoints = 2;                        // çembere/tahtaya değerek giren sayı
    public const int SwishPoints = 4;                          // hiçbir yere değmeden temiz giren sayı (tam 2x - deliksiz ödülü)
    // Kombo artık TOPLAMA değil ÇARPAN: her ardışık sayı çarpanı +0.5 büyütür (üst sınır x3).
    // Kombo 1: x1 -> 2/4 puan | Kombo 3: x2 -> 4/8 | Kombo 5+: x3 -> 6/12 (swish+altınla 24'e kadar).
    public const float ComboStepMult = 0.5f;                  // kombo başına çarpan artışı
    public const float ComboMaxMult = 3f;                     // kombo çarpanı üst sınırı (kombo 5+)
    public const float ScorePlaneDepth = 0.15f;               // sayının yazılması için top merkezinin çember düzleminin ne kadar ALTINA inmesi gerektiği

    // --- Sayı sonrası top akışı (file -> serbest düşüş -> sonuçlanma) ---
    public const float NetDepth = 1.30f;                      // file derinliği: top bu kadar indikten sonra fileden çıkar (yerçekimi normale döner)
    public const float ScoreDropResolve = 2.2f;               // sayı olan top çemberin bu kadar altına düşünce atış sonuçlanır (buharlaşma potadan uzakta olur)

    // --- Altın Top (orijinal özellik: belirli aralıkla gelen parlak top 2 kat puan) ---
    public const int GoldenEvery = 4;                         // her kaç atışta bir altın top gelsin
    public const int GoldenMult = 2;                          // altın topla atılan sayının puan çarpanı

    // --- Son Top draması: oyunun SON topu kırmızı-sıcak parlar ve 2x puan verir ---
    public const int LastBallMult = 2;

    // --- Yıldız değerlendirmesi (oyun sonu 0-3 yıldız; oyuncuya hedef verir) ---
    public static readonly int[] StarThresholds = { 15, 30, 50 }; // yeni kombo-çarpanlı skora göre dengelendi

    // --- İlerleme / Kilit açma (coin ekonomisi) ---
    // Her oyun sonunda skor kadar coin kazanılır; kilitli karakterler coinle açılır.
    // İlk 2 karakter bedava (0); asiller (Kraliçe/Kral) en pahalı; kalanlar artan bedelli.
    // Dizi FigureCount (10) ile aynı uzunlukta olmalı; sıra fig_01..fig_10 ile eşleşir.
    public const int CoinsPerPoint = 1;                       // 1 skor puanı = 1 coin
    //                                          f01 f02  f03(Kraliçe) f04(Kral) f05 f06  f07  f08  f09  f10
    public static readonly int[] UnlockCosts = { 0,  0,   800,        950,      90, 160, 250, 360, 500, 680 };

    // --- Tahta (backboard) çarpışması - GERÇEK banka atış için ---
    // KRİTİK GEOMETRİ: Bu ÖNDEN görünüm; çember, tahtanın tam altında. Tahtayı çemberin ÜSTÜNE
    // geniş bir kutu olarak koyarsak yukarıdan inen top çembere ulaşamadan tahtaya çarpar (top asla
    // giremez). Gerçekte pota, tahtadan atıcıya (SOLA) doğru uzanır; yani tahta çemberin SAĞINDA/
    // arkasında İNCE DİKEY bir duvardır. Böyle koyunca ortadan inen normal atışlar engellenmez;
    // sağa kaçan/sert atışlar tahtaya çarpıp sola-aşağı seker = gerçek banka atış.
    public const float BoardX = 0.92f;                        // duvarın çember merkezine göre yatay konumu (sağ/arka)
    public const float BoardThickness = 0.16f;               // dikey duvar kalınlığı
    public const float BoardTop = 0.50f;                     // duvarın üst ucu - SADECE çemberin hemen üstü (1.70 iken
                                                             // top panyanın ortasında "görünmez duvara" çarpıyordu; önden
                                                             // görünümde yalnız çember dibindeki sekme doğal görünür)
    public const float BoardBottom = -0.25f;                 // duvarın alt ucu (çember hizasının biraz üstü)
    public const float BoardBounce = 0.05f;                  // tahta ÖLÜ olmalı: 0.42 topu sola fırlatıp atışı bozuyordu;
                                                             // ölü tahtada top yüzeyden aşağı kayıp FİLEYE düşer = gerçek banka atış

    // --- Atış mekaniği ---
    // NOT: MaxShotSpeed, topun çembere ulaşması için gereken min hızdan (ölçüldü ~14) belirgin
    // yüksek olmalı ki "tatlı nokta" ~%69 güçte, rahat olsun. MaxDrag ise top elde alçakta
    // durduğu için ekran içinde ulaşılabilir kalmalı (4.5 ekran dışına taşıyordu -> 3.0).
    public const float ShotPower = 1.0f;                      // genel güç çarpanı (ince ayar)
    public const float MaxShotSpeed = 20f;                    // en yüksek atış hızı (15 -> 20: potaya rahat ulaşsın)
    // Sürükleme artık parmağın İLK DOKUNDUĞU noktadan ölçülür (göreli); ekran sınırı derdi
    // olmadığından mesafe uzun tutulur = aynı güç için daha çok parmak yolu = daha ince kontrol.
    public const float MaxDrag = 3.6f;                        // tam güç için gereken sürükleme mesafesi
    public const float MinDrag = 0.35f;                       // bundan kısa sürüklemede atış olmaz
    public const float AimSmoothing = 22f;                    // nişan yumuşatma hızı (parmak titremesini filtreler; büyük = daha az gecikme)

    // --- Oyun kuralları ---
    public const int StartingBalls = 10;                      // klasik modda atış hakkı
    public const int FireCombo = 3;                           // bu kombodan itibaren top "alev izi" bırakır

    // --- Karakter özellikleri (PlayerData.Trait etkileri) ---
    public const float PowerTraitBoost = 1.08f;               // Güçlü Kol: atış hızı çarpanı (%8)
    public const int LuckyGoldenEvery = 3;                    // Şanslı: altın top her 3 atışta bir (normalde 4)
    public const float SniperAimStretch = 1.25f;              // Keskin Nişancı: nişan yayı %25 daha uzağı gösterir

    // --- Zaman Modu (60 saniyede sınırsız top; her sayı ek süre verir) ---
    public const float TimedDuration = 60f;                   // başlangıç süresi (saniye)
    public const float TimedScoreBonus = 2f;                  // her sayının kazandırdığı ek saniye
    public static readonly int[] StarThresholdsTimed = { 30, 55, 85 }; // zaman modu yıldız eşikleri (yeni skora göre dengelendi)

    // --- Sınırlar (top buraya ulaşınca atış sonuçlanır) ---
    public const float KillBottom = -7f;
    public const float KillSide = 11f;
    public const float ShotTimeout = 5f;                      // top bu süreden uzun sonuçlanmazsa (bir yere takıldıysa) atış otomatik biter
}

/// <summary>Küçük yardımcı fonksiyonlar.</summary>
public static class GameUtil
{
    /// <summary>Bir SpriteRenderer'ı istenen dünya yüksekliğine ölçekler, ölçek katsayısını döndürür.</summary>
    public static float FitToHeight(SpriteRenderer sr, float targetWorldHeight)
    {
        if (sr.sprite == null) return 1f;
        float h = sr.sprite.bounds.size.y;
        float s = (h <= 0f) ? 1f : targetWorldHeight / h;
        sr.transform.localScale = new Vector3(s, s, 1f);
        return s;
    }

    /// <summary>Bir SpriteRenderer'ı istenen dünya genişliğine ölçekler, ölçek katsayısını döndürür.</summary>
    public static float FitToWidth(SpriteRenderer sr, float targetWorldWidth)
    {
        if (sr.sprite == null) return 1f;
        float w = sr.sprite.bounds.size.x;
        float s = (w <= 0f) ? 1f : targetWorldWidth / w;
        sr.transform.localScale = new Vector3(s, s, 1f);
        return s;
    }
}
