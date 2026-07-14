using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>
/// Tüm ekran arayüzünü (menü + skor tablosu + oyun sonu) kod ile kurar.
/// Eski (legacy) Unity UI kullanır, böylece ekstra kurulum gerekmez.
/// </summary>
public class UIController : MonoBehaviour
{
    GameBootstrap boot;
    Font font;      // gövde/etiket: Fredoka SemiBold
    Font fontBold;  // vurgu/başlık/sayı: Fredoka Bold

    GameObject selectPanel, gameOverPanel, hud, pausePanel, traitsPanel, unlockPanel;
    Transform gridTransform;   // karakter kartlarının kapsayıcısı (kilit açınca yeniden kurulur)
    Text coinText, gameOverCoinText;
    Image unlockFigImg;
    Text unlockNameText, unlockCostText, unlockBalanceText, unlockConfirmLabel;
    Button unlockConfirmBtn;
    int pendingUnlock = -1;
    Text soundBtnLabel; // duraklatma panelindeki ses düğmesinin yazısı
    Text modeBtnLabel;  // seçim ekranındaki oyun modu düğmesinin yazısı
    Image vignetteImg;  // ALEV MODU ekran kenarı parlaması (HUD'un altında)
    float fireGlow;     // vinyetin yumuşak açılıp kapanma değeri (0..1)
    RectTransform fxLayer; // en üstte çizilen efekt katmanı (rekor konfetisi)
    Image trophyImg;    // yeni rekorda görünen kupa (Sprites/trophy varsa)
    Text scoreText, ballsText, comboText, finalText, highText, nameText;
    Image hudTraitIcon, hudBallIcon; // oyun-içi özellik çipi ikonu + top sayaç ikonu
    int hudTraitFig = -1;            // ikon yalnız seçili oyuncu değişince güncellensin
    Sprite pillSprite;              // 9-slice yuvarlak koyu HUD arka planı (kodla çizilir)
    Text streakText, careerText; // günlük seri + kariyer istatistikleri (seçim ekranı)
    Image[] starImgs; // oyun sonu yıldız değerlendirmesi (0-3)
    Coroutine starsRoutine; // yıldızların sırayla belirme animasyonu
    Sprite sprButton, sprCard; // görsel varsa butonlar/kartlar bunları kullanır (yoksa düz renk)
    float shownScore;   // HUD'da o an GÖRÜNEN skor (gerçek skora doğru sayarak akar)
    float scorePunch;   // puan gelince skor yazısının "zıplama" animasyon süresi
    int lastScoreSeen;  // artışı yakalamak için son bilinen skor

    // --- Online skor tablosu (arkadaş rekabeti) ---
    GameObject leaderboardPanel;
    Transform lbListTransform;      // sıralama satırlarının kapsayıcısı
    Text lbTitleText, gameOverRankText, lbEmptyText;
    Button lbClassicBtn, lbTimedBtn;
    InputField nameField;
    string lbMode = "classic";     // tabloda gösterilen mod

    // Zorunlu isim kapısı (isim girilmeden oyuna geçilemez)
    GameObject namePrompt;
    InputField namePromptInput;
    Text namePromptHint;

    // Günlük görevler
    GameObject missionsPanel;
    Transform missionsList;
    Text missionsBonusText, missionToast;
    Coroutine toastRoutine;

    // Top görünümü mağazası
    GameObject ballShopPanel;
    Transform ballShopGrid;
    Text ballShopCoinText;

    public void Init(GameBootstrap b)
    {
        boot = b;
        // Premium tipografi: Fredoka (yuvarlak, tombul, tam Türkçe). Tek satır değişse de
        // TÜM arayüz yazıları buradan beslendiği için hepsi bir anda premium görünür.
        font = Resources.Load<Font>("Fonts/Fredoka-SemiBold");
        fontBold = Resources.Load<Font>("Fonts/Fredoka-Bold");
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // güvenlik ağı
        if (fontBold == null) fontBold = font;
        sprButton = Resources.Load<Sprite>("Sprites/button");
        sprCard = Resources.Load<Sprite>("Sprites/card");

        EnsureEventSystem();
        var canvas = CreateCanvas();
        BuildVignette(canvas.transform);   // ilk çocuk: HUD/panellerin ALTINDA kalır
        BuildHUD(canvas.transform);
        BuildSelectPanel(canvas.transform);
        BuildGameOverPanel(canvas.transform);
        BuildPausePanel(canvas.transform);
        BuildTraitsPanel(canvas.transform);
        BuildUnlockPanel(canvas.transform);
        BuildLeaderboardPanel(canvas.transform);
        BuildMissionsPanel(canvas.transform);
        BuildBallShopPanel(canvas.transform);

        // Efekt katmanı: son çocuk = her şeyin ÜSTÜNDE (rekor konfetisi burada yağar).
        var fx = new GameObject("FxLayer", typeof(RectTransform));
        fx.transform.SetParent(canvas.transform, false);
        fxLayer = (RectTransform)fx.transform;
        fxLayer.anchorMin = Vector2.zero; fxLayer.anchorMax = Vector2.one;
        fxLayer.offsetMin = Vector2.zero; fxLayer.offsetMax = Vector2.zero;

        // Günlük görev kutlaması: fx katmanında (her şeyin üstünde) kısa "GÖREV TAMAM" bildirimi.
        missionToast = MakeText(fxLayer, "", 40, TextAnchor.MiddleCenter, new Color(0.5f, 0.95f, 0.7f),
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -240), new Vector2(1300, 60));
        missionToast.font = fontBold;
        Missions.OnCompleted += (i, def, reward) => ShowMissionToast("GÖREV TAMAM: " + def.Text + "  +" + reward + " COİN");
        Missions.OnAllDone += bonus => ShowMissionToast("TÜM GÖREVLER BİTTİ! BONUS +" + bonus + " COİN");

        // Zorunlu isim kapısı: her şeyin ÜSTÜNDE (en son çocuk) - isim girilmeden geçilmez.
        BuildNamePrompt(canvas.transform);

        GameManager.Instance.OnChanged += Refresh;
        GameManager.Instance.OnStateChanged += OnStateChanged;
        GameManager.Instance.OnComboShielded += () => ShowMissionToast("GECE TOPU komboyu korudu! 🛡");
        OnStateChanged();
        Refresh();
        MaybeShowNamePrompt(); // isim yoksa açılışta zorunlu kapıyı göster
    }

    void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    Canvas CreateCanvas()
    {
        var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    // ---------------- Alev vinyeti ----------------
    void BuildVignette(Transform parent)
    {
        var go = new GameObject("FireVignette", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        vignetteImg = go.AddComponent<Image>();
        // Görsel (Sprites/vignette) varsa onu kullan; yoksa kodla üretilen kenar parlaması.
        var spr = Resources.Load<Sprite>("Sprites/vignette");
        vignetteImg.sprite = spr != null ? spr : MakeVignetteSprite();
        vignetteImg.raycastTarget = false;
        vignetteImg.color = new Color(1f, 1f, 1f, 0f); // kapalı başlar
        var rt = vignetteImg.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static Sprite vignetteSprite;

    /// <summary>Kodla üretilen turuncu kenar parlaması (ekran ortası tamamen şeffaf).</summary>
    static Sprite MakeVignetteSprite()
    {
        if (vignetteSprite != null) return vignetteSprite;
        const int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var fire = new Color(1f, 0.42f, 0.08f);
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                // Kenara uzaklık (dikdörtgen): 0 = merkez, 1 = kenar/köşe.
                float dx = Mathf.Abs(x - S / 2f) / (S / 2f);
                float dy = Mathf.Abs(y - S / 2f) / (S / 2f);
                float d = Mathf.Max(dx, dy);
                float a = Mathf.Pow(Mathf.Clamp01((d - 0.55f) / 0.45f), 1.6f);
                tex.SetPixel(x, y, new Color(fire.r, fire.g, fire.b, a));
            }
        }
        tex.Apply();
        vignetteSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return vignetteSprite;
    }

    // ---------------- HUD ----------------
    void BuildHUD(Transform parent)
    {
        hud = NewPanel(parent, "HUD", new Color(0, 0, 0, 0));
        var pillDark = new Color(0.05f, 0.07f, 0.14f, 0.55f);

        // --- SKOR KARTI (sol üst): yuvarlak koyu pill + "SKOR" başlığı + büyük sayı ---
        var scorePill = MakePill(hud.transform, new Vector2(0, 1), new Vector2(28, -28), new Vector2(300, 138), pillDark);
        var scoreCap = MakeText(scorePill.transform, "SKOR", 26, TextAnchor.UpperLeft, new Color(1f, 0.82f, 0.35f),
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -18), new Vector2(240, 34));
        scoreCap.font = fontBold;
        scoreText = MakeText(scorePill.transform, "0", 82, TextAnchor.UpperLeft, Color.white,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -42), new Vector2(280, 96));
        scoreText.font = fontBold;

        // --- OYUNCU / ÖZELLİK ÇİPİ (skorun altında): ikon + oyuncu adı (özellik renginde) ---
        var chip = MakePill(hud.transform, new Vector2(0, 1), new Vector2(28, -178), new Vector2(360, 66),
            new Color(0.10f, 0.12f, 0.20f, 0.62f));
        var iconGo = new GameObject("HudTraitIcon", typeof(RectTransform));
        iconGo.transform.SetParent(chip.transform, false);
        hudTraitIcon = iconGo.AddComponent<Image>();
        hudTraitIcon.preserveAspect = true; hudTraitIcon.raycastTarget = false;
        var irt = hudTraitIcon.rectTransform;
        irt.anchorMin = irt.anchorMax = new Vector2(0, 0.5f); irt.pivot = new Vector2(0, 0.5f);
        irt.anchoredPosition = new Vector2(16, 0); irt.sizeDelta = new Vector2(46, 46);
        nameText = MakeText(chip.transform, "", 32, TextAnchor.MiddleLeft, Color.white,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(74, 1), new Vector2(280, 50));
        nameText.font = fontBold;

        // --- TOP / SÜRE PILL (sağ üst): basketbol ikonu + sayı ---
        var ballPill = MakePill(hud.transform, new Vector2(1, 1), new Vector2(-28, -28), new Vector2(224, 86), pillDark);
        var ballIconGo = new GameObject("HudBallIcon", typeof(RectTransform));
        ballIconGo.transform.SetParent(ballPill.transform, false);
        hudBallIcon = ballIconGo.AddComponent<Image>();
        hudBallIcon.sprite = Resources.Load<Sprite>("Sprites/ball");
        hudBallIcon.preserveAspect = true; hudBallIcon.raycastTarget = false;
        var brt = hudBallIcon.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(0, 0.5f); brt.pivot = new Vector2(0, 0.5f);
        brt.anchoredPosition = new Vector2(16, 0); brt.sizeDelta = new Vector2(54, 54);
        ballsText = MakeText(ballPill.transform, "", 46, TextAnchor.MiddleRight, new Color(1f, 0.85f, 0.3f),
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-22, 1), new Vector2(170, 62));
        ballsText.font = fontBold;

        // --- KOMBO (üst orta) ---
        comboText = MakeText(hud.transform, "", 48, TextAnchor.UpperCenter, new Color(1f, 0.55f, 0.2f),
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(700, 70));
        comboText.font = fontBold;

        // Duraklat düğmesi (sağ üst, top pill'inin altında). ESC de aynı işi yapar.
        MakeButton(hud.transform, "I I", new Color(0.25f, 0.30f, 0.45f),
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -132), new Vector2(96, 68),
            TogglePause);
    }

    /// <summary>Kodla çizilen 9-slice yuvarlak koyu HUD arka planı (pill). Kenarlar
    /// köşe yarıçapını korur; genişletince ovalleşmez. Renk image.color ile verilir.</summary>
    GameObject MakePill(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject("Pill", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = PillSprite();
        img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return go;
    }

    Sprite PillSprite()
    {
        if (pillSprite != null) return pillSprite;
        const int S = 64, r = 22;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float cx = Mathf.Clamp(x, r, S - 1 - r);
                float cy = Mathf.Clamp(y, r, S - 1 - r);
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                float a = Mathf.Clamp01(r - d + 0.5f); // yumuşak kenar (antialias)
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        pillSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        return pillSprite;
    }

    // ---------------- Duraklatma ----------------
    void BuildPausePanel(Transform parent)
    {
        pausePanel = NewPanel(parent, "PausePanel", new Color(0.03f, 0.04f, 0.10f, 0.90f));

        MakeText(pausePanel.transform, "DURAKLATILDI", 72, TextAnchor.UpperCenter, Color.white,
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -200), new Vector2(1200, 100));

        MakeButton(pausePanel.transform, "DEVAM ET", new Color(0.9f, 0.45f, 0.15f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(440, 96),
            () => SetPaused(false));
        var sndBtn = MakeButton(pausePanel.transform, "SES: AÇIK", new Color(0.25f, 0.30f, 0.45f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(440, 96),
            ToggleSound);
        soundBtnLabel = sndBtn.GetComponentInChildren<Text>();
        MakeButton(pausePanel.transform, "MENÜYE DÖN", new Color(0.25f, 0.30f, 0.45f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -180), new Vector2(440, 96),
            () => { SetPaused(false); boot.BackToSelect(); });

        RefreshSoundLabel();
        pausePanel.SetActive(false);
    }

    // ---------------- Özellik rehberi ----------------
    void BuildTraitsPanel(Transform parent)
    {
        traitsPanel = NewPanel(parent, "TraitsPanel", new Color(0.03f, 0.04f, 0.10f, 0.94f));

        MakeText(traitsPanel.transform, "OYUNCU ÖZELLİKLERİ", 64, TextAnchor.UpperCenter, Color.white,
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(1200, 90));

        var traits = new[] { PlayerData.Trait.Sniper, PlayerData.Trait.Power, PlayerData.Trait.Lucky };
        var descs = new[]
        {
            "Nişan yayı daha uzağı gösterir; topun düşeceği yeri önceden görürsün.",
            "Kolları daha güçlüdür; aynı çekişle top daha hızlı ve daha uzağa gider.",
            "Altın top ona daha sık gelir (4 yerine 3 atışta bir) - daha çok 2x puan fırsatı.",
        };

        for (int i = 0; i < traits.Length; i++)
        {
            var t = traits[i];
            float y = 160f - i * 180f;

            // İkon (görsel varsa) veya özellik renginde yıldız çipi
            var iconGo = new GameObject("TraitIcon", typeof(RectTransform));
            iconGo.transform.SetParent(traitsPanel.transform, false);
            var icon = iconGo.AddComponent<Image>();
            var tspr = TraitSprite(t);
            icon.sprite = tspr != null ? tspr : MakeStarSprite();
            icon.color = tspr != null ? Color.white : PlayerData.TraitColor(t);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var irt = icon.rectTransform;
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.pivot = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = new Vector2(-430, y + 10);
            irt.sizeDelta = new Vector2(96, 96);

            // Ad (özellik renginde, kalın)
            var nameT = MakeText(traitsPanel.transform, PlayerData.TraitName(t), 42, TextAnchor.MiddleLeft, PlayerData.TraitColor(t),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-350, y + 42), new Vector2(800, 52));
            nameT.font = fontBold;

            // Açıklama
            MakeText(traitsPanel.transform, descs[i], 30, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.88f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-350, y - 4), new Vector2(880, 60));

            // Bu özellikteki oyuncular (isim listesi dinamik kurulur)
            var sb = new System.Text.StringBuilder("Oyuncular: ");
            bool first = true;
            for (int f = 0; f < PlayerData.FigureCount; f++)
            {
                // Asiller TÜM yeteneklere sahip: her özelliğin listesinde görünürler.
                if (PlayerData.TraitOf(f) != t && !PlayerData.IsRoyal(f)) continue;
                if (!first) sb.Append(", ");
                sb.Append(PlayerData.NameOf(f));
                first = false;
            }
            MakeText(traitsPanel.transform, sb.ToString(), 26, TextAnchor.UpperLeft, new Color(1f, 0.85f, 0.4f, 0.9f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-350, y - 48), new Vector2(880, 40));
        }

        MakeButton(traitsPanel.transform, "KAPAT", new Color(0.9f, 0.45f, 0.15f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 48), new Vector2(360, 88),
            () => traitsPanel.SetActive(false));

        traitsPanel.SetActive(false);
    }

    void TogglePause() => SetPaused(!GameManager.Paused);

    void SetPaused(bool p)
    {
        if (GameManager.Instance == null) return;
        if (p && GameManager.Instance.CurrentState != GameManager.State.Playing) return; // sadece oyunda
        if (p == GameManager.Paused) { pausePanel.SetActive(p); return; }

        GameManager.Paused = p;
        if (p) SlowMo.Cancel(); // aktif ağır çekim zamanı geri açmasın
        Time.timeScale = p ? 0f : 1f;
        Time.fixedDeltaTime = 0.02f;
        pausePanel.SetActive(p);
    }

    void ToggleSound()
    {
        Sfx.SetMuted(!Sfx.Muted);
        RefreshSoundLabel();
    }

    void ToggleMode()
    {
        GameManager.NextMode = GameManager.NextMode == GameManager.Mode.Classic
            ? GameManager.Mode.Timed : GameManager.Mode.Classic;
        RefreshModeLabel();
    }

    void RefreshModeLabel()
    {
        if (modeBtnLabel != null)
            modeBtnLabel.text = GameManager.NextMode == GameManager.Mode.Timed
                ? "MOD: ZAMANA KARŞI (60sn)"
                : "MOD: KLASİK (10 TOP)";
    }

    void RefreshSoundLabel()
    {
        if (soundBtnLabel != null)
            soundBtnLabel.text = Sfx.Muted ? "SES: KAPALI" : "SES: AÇIK";
    }

    // ---------------- Figür seçim ----------------
    void BuildSelectPanel(Transform parent)
    {
        selectPanel = NewPanel(parent, "SelectPanel", new Color(0.05f, 0.06f, 0.12f, 0.92f));

        // Başlık: logo görseli varsa onu kullan (Sprites/logo.png), yoksa yazıya düş.
        var logoSpr = Resources.Load<Sprite>("Sprites/logo");
        float subtitleY = -180f;
        if (logoSpr != null)
        {
            var lgo = new GameObject("Logo", typeof(RectTransform));
            lgo.transform.SetParent(selectPanel.transform, false);
            var li = lgo.AddComponent<Image>();
            li.sprite = logoSpr;
            li.preserveAspect = true;
            li.raycastTarget = false;
            var lrt = li.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 1f);
            lrt.pivot = new Vector2(0.5f, 1f);
            lrt.anchoredPosition = new Vector2(0, -20);
            lrt.sizeDelta = new Vector2(580, 263); // logo oranına (626x284) yakın
            subtitleY = -290f; // alt yazı logonun altına insin
        }
        else
        {
            MakeText(selectPanel.transform, "BOBBLE HOOPS", 80, TextAnchor.UpperCenter, Color.white,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(1200, 110));
        }
        MakeText(selectPanel.transform, "Oyuncunu seç", 40, TextAnchor.UpperCenter, new Color(1f, 0.85f, 0.4f),
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, subtitleY), new Vector2(1000, 60));

        // Coin bakiyesi (sol üst): kilit açmada harcanır, oyuncuya "biriktirme" hedefi verir.
        coinText = MakeText(selectPanel.transform, "", 44, TextAnchor.UpperLeft, new Color(1f, 0.82f, 0.2f),
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -34), new Vector2(700, 56));
        coinText.font = fontBold;

        // Günlük seri rozeti (coin'in altında): her gün oynamaya teşvik eder.
        streakText = MakeText(selectPanel.transform, "", 30, TextAnchor.UpperLeft, new Color(1f, 0.6f, 0.2f),
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -96), new Vector2(700, 44));

        // Oyun modu seçici (sağ üst): KLASİK (10 top) <-> ZAMANA KARŞI (60sn).
        var modeBtn = MakeButton(selectPanel.transform, "", new Color(0.25f, 0.30f, 0.45f),
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -36), new Vector2(470, 76),
            ToggleMode);
        modeBtnLabel = modeBtn.GetComponentInChildren<Text>();
        modeBtnLabel.fontSize = 28;
        RefreshModeLabel();

        // Skor tablosu düğmesi (sağ üst, mod düğmesinin altında): arkadaş rekabeti.
        MakeButton(selectPanel.transform, "SIRALAMA", new Color(0.18f, 0.42f, 0.30f),
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -124), new Vector2(470, 72),
            () => OpenLeaderboard(lbMode));

        // Top mağazası düğmesi (sıralamanın altında): coin harcama hedefi.
        MakeButton(selectPanel.transform, "TOPLAR", new Color(0.45f, 0.28f, 0.55f),
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -212), new Vector2(470, 72),
            OpenBallShop);

        // Oyuncu adı (sol üst, serinin altında): skor tablosunda bu isimle görünürsün.
        nameField = MakeInputField(selectPanel.transform, "ADINI GİR (sıralama için)",
            new Vector2(0, 1), new Vector2(40, -150), new Vector2(430, 64));

        // Günlük görevler düğmesi (sol üst, isim alanının altında): her gün 3 görev = coin.
        MakeButton(selectPanel.transform, "GÖREVLER", new Color(0.55f, 0.35f, 0.12f),
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -232), new Vector2(300, 70),
            OpenMissions);

        // Özellik rehberi düğmesi (sağ alt): rozetlerin ne anlama geldiğini açıklar.
        MakeButton(selectPanel.transform, "ÖZELLİKLER", new Color(0.25f, 0.30f, 0.45f),
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 40), new Vector2(300, 74),
            () => traitsPanel.SetActive(true));
        // Kariyer istatistikleri (alt şerit): uzun vadeli ilerleme hissi.
        careerText = MakeText(selectPanel.transform, "", 26, TextAnchor.LowerCenter, new Color(1f, 1f, 1f, 0.55f),
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(1700, 40));

        var grid = new GameObject("Grid", typeof(RectTransform));
        grid.transform.SetParent(selectPanel.transform, false);
        var grt = grid.GetComponent<RectTransform>();
        grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 0.5f);
        grt.pivot = new Vector2(0.5f, 0.5f);
        grt.anchoredPosition = new Vector2(0, -100);
        grt.sizeDelta = new Vector2(1280, 564);
        gridTransform = grid.transform;
        PopulateGrid();
    }

    /// <summary>Karakter kartlarını (kilit durumlarıyla) kurar. Kilit açıldığında yeniden çağrılır.</summary>
    void PopulateGrid()
    {
        if (gridTransform == null) return;
        // Eski kartları temizle (kilit durumu değişince taze kur).
        for (int i = gridTransform.childCount - 1; i >= 0; i--)
        {
            var c = gridTransform.GetChild(i).gameObject;
            c.SetActive(false);
            Destroy(c);
        }

        // Kart görseli dikey (855x1170) olduğu için hücreler de dikey oranlı.
        // 9 figür: üst sıra 5, alt sıra 4 kart - her sıra kendi içinde ORTALANIR.
        // (GridLayoutGroup eksik son sırayı sola yasladığı için yerleşim elle hesaplanır.)
        const float cellW = 200f, cellH = 274f, gap = 16f;
        int total = PlayerData.FigureCount;
        int topCount = (total + 1) / 2; // üst sırada bir fazla (10 -> 5+5)

        // Gösterim sırası: ucuzdan pahalıya; asiller (Kral/Kraliçe) HER ZAMAN en sonda.
        var order = new System.Collections.Generic.List<int>();
        for (int i = 0; i < total; i++) order.Add(i);
        order.Sort((a, b) =>
        {
            bool ra = PlayerData.IsRoyal(a), rb = PlayerData.IsRoyal(b);
            if (ra != rb) return ra ? 1 : -1;              // asiller sona
            int ca = Progress.CostOf(a), cb = Progress.CostOf(b);
            if (ca != cb) return ca.CompareTo(cb);          // ucuz -> pahalı
            return a.CompareTo(b);                          // eşitlikte index sırası
        });

        for (int pos = 0; pos < total; pos++)
        {
            int idx = order[pos];
            int row = pos < topCount ? 0 : 1;
            int col = row == 0 ? pos : pos - topCount;
            int rowCount = row == 0 ? topCount : total - topCount;
            float x = (col - (rowCount - 1) * 0.5f) * (cellW + gap);
            float y = row == 0 ? (cellH + gap) * 0.5f : -(cellH + gap) * 0.5f;

            int captured = idx;
            var sprite = Resources.Load<Sprite>($"Figures/fig_{(idx + 1):00}");
            var card = MakeFigureButton(gridTransform, sprite, PlayerData.NameOf(idx), idx, () => boot.PlayWithFigure(captured));
            var crt = (RectTransform)card.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(cellW, cellH);
            crt.anchoredPosition = new Vector2(x, y);
        }
    }

    GameObject MakeFigureButton(Transform parent, Sprite figSprite, string name, int figIndex, UnityAction onClick)
    {
        var go = new GameObject("FigBtn", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var bg = go.AddComponent<Image>();
        bool hasCard = sprCard != null;
        if (hasCard) { bg.sprite = sprCard; bg.color = Color.white; }
        else bg.color = new Color(0.16f, 0.20f, 0.32f, 1f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;
        bool locked = !Progress.IsUnlocked(figIndex);
        btn.onClick.AddListener(() => Sfx.Play(Sfx.Id.Click));
        // Kilitliyse tıklama açma onayını açar; açıksa oyunu başlatır.
        btn.onClick.AddListener(() => { if (locked) OpenUnlock(figIndex); else onClick(); });

        if (figSprite != null)
        {
            var imgGo = new GameObject("Fig", typeof(RectTransform));
            imgGo.transform.SetParent(go.transform, false);
            var img = imgGo.AddComponent<Image>();
            img.sprite = figSprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            if (locked) img.color = new Color(0.42f, 0.45f, 0.55f, 0.85f); // kilitli: silikleştir
            var rt = img.rectTransform;
            if (hasCard)
            {
                // Kart görselinin İÇ PENCERESİ (piksel ölçümüyle): x 0.10..0.89, y 0.20..0.96.
                // Figür pencerenin içine, kenarlardan az payla oturur.
                rt.anchorMin = new Vector2(0.13f, 0.23f); rt.anchorMax = new Vector2(0.87f, 0.93f);
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            }
            else
            {
                rt.anchorMin = new Vector2(0, 0.20f); rt.anchorMax = new Vector2(1, 1);
                rt.offsetMin = new Vector2(10, 4); rt.offsetMax = new Vector2(-10, -8);
            }
        }

        // Kilit örtüsü: figür penceresini karartır, ortada kilit ikonu + altında fiyat.
        if (locked)
        {
            var ovGo = new GameObject("LockOverlay", typeof(RectTransform));
            ovGo.transform.SetParent(go.transform, false);
            var ov = ovGo.AddComponent<Image>();
            ov.color = new Color(0.02f, 0.03f, 0.08f, 0.60f);
            ov.raycastTarget = false;
            var ort = ov.rectTransform;
            ort.anchorMin = new Vector2(0.13f, 0.23f); ort.anchorMax = new Vector2(0.87f, 0.93f);
            ort.offsetMin = Vector2.zero; ort.offsetMax = Vector2.zero;

            var lkGo = new GameObject("LockIcon", typeof(RectTransform));
            lkGo.transform.SetParent(go.transform, false);
            var lk = lkGo.AddComponent<Image>();
            lk.sprite = MakeLockSprite();
            lk.color = new Color(1f, 0.85f, 0.35f);
            lk.raycastTarget = false;
            var lrt = lk.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.62f);
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = new Vector2(58, 58);

            var costLbl = MakeText(go.transform, Progress.CostOf(figIndex).ToString(), 34, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f),
                new Vector2(0.5f, 0.34f), new Vector2(0.5f, 0.34f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180, 44));
            costLbl.font = fontBold;
        }

        // İsim etiketi: kart varsa alttaki turuncu banner'a (ölçüm: y 0.095..0.187) oturur.
        // Küçülen kart hücresine uygun yazı boyutu (22).
        var nameLabel = hasCard
            ? MakeText(go.transform, name, 22, TextAnchor.MiddleCenter, new Color(0.10f, 0.12f, 0.30f),
                new Vector2(0.16f, 0.09f), new Vector2(0.84f, 0.19f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero)
            : MakeText(go.transform, name, 32, TextAnchor.MiddleCenter, Color.white,
                new Vector2(0, 0), new Vector2(1, 0.20f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        nameLabel.font = fontBold;
        nameLabel.rectTransform.offsetMin = new Vector2(4, 2);
        nameLabel.rectTransform.offsetMax = new Vector2(-4, -2);

        // Özellik rozeti (kartın sağ üst köşesi): ikon görseli (Sprites/trait_*) varsa onu,
        // yoksa özellik renginde küçük bir yıldız çipi kullanır. Yazı YOK - temiz görünüm.
        var trait = PlayerData.TraitOf(figIndex);
        var badgeGo = new GameObject("TraitBadge", typeof(RectTransform));
        badgeGo.transform.SetParent(go.transform, false);
        var badge = badgeGo.AddComponent<Image>();
        var tspr = TraitSprite(trait);
        badge.sprite = tspr != null ? tspr : MakeStarSprite();
        badge.color = tspr != null ? Color.white : PlayerData.TraitColor(trait);
        badge.preserveAspect = true;
        badge.raycastTarget = false;
        var brt = badge.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.80f, 0.855f);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.anchoredPosition = Vector2.zero;
        brt.sizeDelta = new Vector2(42, 42);
        return go;
    }

    /// <summary>Özellik ikonu görselini yükler (yoksa null; çağıran yıldız çipine düşer).</summary>
    static Sprite TraitSprite(PlayerData.Trait t)
    {
        string n = t switch
        {
            PlayerData.Trait.Sniper => "trait_sniper",
            PlayerData.Trait.Power => "trait_power",
            _ => "trait_lucky",
        };
        return Resources.Load<Sprite>("Sprites/" + n);
    }

    // ---------------- Kilit açma onayı ----------------
    void BuildUnlockPanel(Transform parent)
    {
        unlockPanel = NewPanel(parent, "UnlockPanel", new Color(0.03f, 0.04f, 0.10f, 0.93f));

        MakeText(unlockPanel.transform, "OYUNCUYU AÇ", 60, TextAnchor.UpperCenter, Color.white,
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(1200, 84));

        var figGo = new GameObject("UnlockFig", typeof(RectTransform));
        figGo.transform.SetParent(unlockPanel.transform, false);
        unlockFigImg = figGo.AddComponent<Image>();
        unlockFigImg.preserveAspect = true;
        unlockFigImg.raycastTarget = false;
        var frt = unlockFigImg.rectTransform;
        frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
        frt.pivot = new Vector2(0.5f, 0.5f);
        frt.anchoredPosition = new Vector2(0, 150);
        frt.sizeDelta = new Vector2(300, 400);

        unlockNameText = MakeText(unlockPanel.transform, "", 44, TextAnchor.MiddleCenter, Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -110), new Vector2(1100, 56));
        unlockNameText.font = fontBold;
        unlockCostText = MakeText(unlockPanel.transform, "", 40, TextAnchor.MiddleCenter, new Color(1f, 0.82f, 0.2f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -172), new Vector2(1100, 52));
        unlockCostText.font = fontBold;
        unlockBalanceText = MakeText(unlockPanel.transform, "", 30, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.7f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -224), new Vector2(1100, 44));

        unlockConfirmBtn = MakeButton(unlockPanel.transform, "AÇ", new Color(0.2f, 0.7f, 0.3f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-130, 150), new Vector2(360, 96),
            DoUnlock);
        unlockConfirmLabel = unlockConfirmBtn.GetComponentInChildren<Text>();
        MakeButton(unlockPanel.transform, "VAZGEÇ", new Color(0.25f, 0.30f, 0.45f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(130, 150), new Vector2(360, 96),
            () => { unlockPanel.SetActive(false); pendingUnlock = -1; });

        unlockPanel.SetActive(false);
    }

    void OpenUnlock(int figIndex)
    {
        pendingUnlock = figIndex;
        unlockFigImg.sprite = Resources.Load<Sprite>($"Figures/fig_{(figIndex + 1):00}");
        unlockFigImg.color = Color.white;
        unlockNameText.text = PlayerData.NameOf(figIndex) + "  •  " + (PlayerData.IsRoyal(figIndex)
            ? "TÜM YETENEKLER (NİŞANCI + GÜÇLÜ KOL + ŞANSLI)"
            : PlayerData.TraitName(PlayerData.TraitOf(figIndex)));
        int cost = Progress.CostOf(figIndex);
        unlockCostText.text = "FİYAT: " + cost + " COIN";
        bool can = Progress.Coins >= cost;
        unlockBalanceText.text = "Bakiyen: " + Progress.Coins + (can ? "" : "   (yetersiz)");
        unlockBalanceText.color = can ? new Color(1f, 1f, 1f, 0.7f) : new Color(1f, 0.4f, 0.3f, 0.9f);
        if (unlockConfirmLabel != null) unlockConfirmLabel.text = can ? "AÇ" : "YETERSİZ";
        unlockConfirmBtn.interactable = can;
        unlockPanel.SetActive(true);
    }

    void DoUnlock()
    {
        if (pendingUnlock < 0) return;
        if (Progress.TryUnlock(pendingUnlock)) // güvenlik: yeterli coin yoksa açmaz
        {
            Sfx.Play(Sfx.Id.Swish); // ödül sesi
            PopulateGrid();          // kart artık açık görünsün
            Refresh();               // coin bakiyesi güncellensin
            unlockPanel.SetActive(false);
            pendingUnlock = -1;
        }
    }

    // ---------------- Oyun sonu ----------------
    void BuildGameOverPanel(Transform parent)
    {
        gameOverPanel = NewPanel(parent, "GameOverPanel", new Color(0.05f, 0.06f, 0.12f, 0.92f));

        MakeText(gameOverPanel.transform, "OYUN BİTTİ", 80, TextAnchor.UpperCenter, Color.white,
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(1200, 110));

        // Rekor kupası (Sprites/trophy varsa): sadece yeni rekor kırıldığında görünür.
        var trophySpr = Resources.Load<Sprite>("Sprites/trophy");
        if (trophySpr != null)
        {
            var tGo = new GameObject("Trophy", typeof(RectTransform));
            tGo.transform.SetParent(gameOverPanel.transform, false);
            trophyImg = tGo.AddComponent<Image>();
            trophyImg.sprite = trophySpr;
            trophyImg.preserveAspect = true;
            trophyImg.raycastTarget = false;
            var trt = trophyImg.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0, -16);
            trt.sizeDelta = new Vector2(118, 118);
            tGo.SetActive(false);
        }
        finalText = MakeText(gameOverPanel.transform, "Skor: 0", 56, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.4f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(900, 80));
        highText = MakeText(gameOverPanel.transform, "Rekor: 0", 40, TextAnchor.MiddleCenter, Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(900, 60));

        // Kazanılan coin (kilit açmaya gider): oyuncuya ilerleme hissi verir.
        gameOverCoinText = MakeText(gameOverPanel.transform, "", 40, TextAnchor.MiddleCenter, new Color(1f, 0.82f, 0.2f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -35), new Vector2(900, 52));
        gameOverCoinText.font = fontBold;

        // Yıldız değerlendirmesi (skor eşiklerine göre 0-3 yıldız): oyuncuya "bir dahakine
        // 3 yıldız" hedefi verir - tekrar oynatmanın en basit ve etkili yolu.
        starImgs = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            var sGo = new GameObject("Star" + i, typeof(RectTransform));
            sGo.transform.SetParent(gameOverPanel.transform, false);
            var img = sGo.AddComponent<Image>();
            img.sprite = MakeStarSprite();
            img.raycastTarget = false;
            var srt = img.rectTransform;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = new Vector2((i - 1) * 100, 175);
            srt.sizeDelta = new Vector2(80, 80);
            starImgs[i] = img;
        }

        // Online sıra göstergesi (skor gönderildiğinde dolar): "Sıralamada #3'sün!".
        gameOverRankText = MakeText(gameOverPanel.transform, "", 34, TextAnchor.MiddleCenter, new Color(0.5f, 0.95f, 0.7f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -72), new Vector2(900, 46));
        gameOverRankText.font = fontBold;

        // İki buton da aynı boyut ve tam merkez hizasında (görsel gövde gölgesiz kırpıldığı
        // için yazı, butonun görsel merkezine tam oturur).
        MakeButton(gameOverPanel.transform, "TEKRAR OYNA", new Color(0.9f, 0.45f, 0.15f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -128), new Vector2(440, 92),
            () => boot.Retry());
        MakeButton(gameOverPanel.transform, "SIRALAMA", new Color(0.18f, 0.42f, 0.30f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -232), new Vector2(440, 92),
            () => OpenLeaderboard(Leaderboard.ModeStr(GameManager.Instance.CurrentMode)));
        MakeButton(gameOverPanel.transform, "OYUNCU DEĞİŞTİR", new Color(0.25f, 0.30f, 0.45f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -336), new Vector2(440, 92),
            () => boot.BackToSelect());
    }

    // ---------------- Online skor tablosu ----------------
    void SubmitScoreToLeaderboard()
    {
        if (gameOverRankText != null) gameOverRankText.text = "";
        var gm = GameManager.Instance;
        if (gm.Score <= 0 || Leaderboard.Instance == null || gameOverRankText == null) return;

        if (!Leaderboard.HasName)
        {
            gameOverRankText.color = new Color(1f, 0.8f, 0.4f);
            gameOverRankText.text = "Sıralamaya girmek için menüde adını gir";
            return;
        }
        gameOverRankText.color = new Color(0.5f, 0.95f, 0.7f);
        gameOverRankText.text = "Sıralamaya gönderiliyor…";
        Leaderboard.Instance.Submit(Leaderboard.ModeStr(gm.CurrentMode), gm.Score, r =>
        {
            if (gameOverRankText == null) return;
            gameOverRankText.text = (r != null && r.rank > 0)
                ? "Sıralamada #" + r.rank + "'sin!"
                : "Sıralama şu an güncellenemedi";
        });
    }

    void BuildLeaderboardPanel(Transform parent)
    {
        leaderboardPanel = NewPanel(parent, "LeaderboardPanel", new Color(0.03f, 0.05f, 0.11f, 0.96f));

        lbTitleText = MakeText(leaderboardPanel.transform, "SIRALAMA", 62, TextAnchor.UpperCenter, Color.white,
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(1200, 84));
        lbTitleText.font = fontBold;

        lbClassicBtn = MakeButton(leaderboardPanel.transform, "KLASİK", new Color(0.9f, 0.45f, 0.15f),
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-165, -164), new Vector2(310, 70),
            () => OpenLeaderboard("classic"));
        lbTimedBtn = MakeButton(leaderboardPanel.transform, "ZAMANA KARŞI", new Color(0.25f, 0.30f, 0.45f),
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(165, -164), new Vector2(310, 70),
            () => OpenLeaderboard("timed"));

        var list = new GameObject("LbList", typeof(RectTransform));
        list.transform.SetParent(leaderboardPanel.transform, false);
        var lrt = (RectTransform)list.transform;
        lrt.anchorMin = new Vector2(0.5f, 1); lrt.anchorMax = new Vector2(0.5f, 1); lrt.pivot = new Vector2(0.5f, 1);
        lrt.anchoredPosition = new Vector2(0, -226); lrt.sizeDelta = new Vector2(920, 720);
        lbListTransform = list.transform;

        lbEmptyText = MakeText(leaderboardPanel.transform, "", 34, TextAnchor.UpperCenter, new Color(1f, 1f, 1f, 0.6f),
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -360), new Vector2(900, 80));

        MakeButton(leaderboardPanel.transform, "YENİLE", new Color(0.25f, 0.30f, 0.45f),
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-150, 48), new Vector2(280, 84),
            () => OpenLeaderboard(lbMode));
        MakeButton(leaderboardPanel.transform, "KAPAT", new Color(0.9f, 0.45f, 0.15f),
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(150, 48), new Vector2(280, 84),
            () => leaderboardPanel.SetActive(false));

        leaderboardPanel.SetActive(false);
    }

    void OpenLeaderboard(string mode)
    {
        lbMode = mode;
        leaderboardPanel.SetActive(true);
        HighlightModeTabs();
        lbTitleText.text = mode == "timed" ? "SIRALAMA — ZAMANA KARŞI" : "SIRALAMA — KLASİK";
        lbEmptyText.text = "Yükleniyor…";
        ClearLbList();
        if (Leaderboard.Instance != null)
            Leaderboard.Instance.Fetch(mode, PopulateLeaderboard);
        else
            lbEmptyText.text = "Skor tablosu şu an kullanılamıyor";
    }

    void HighlightModeTabs()
    {
        void Tint(Button b, bool on)
        {
            if (b == null) return;
            var img = b.GetComponent<Image>();
            if (img != null)
                img.color = sprButton != null
                    ? (on ? Color.white : new Color(0.6f, 0.64f, 0.72f, 1f))
                    : (on ? new Color(0.9f, 0.45f, 0.15f) : new Color(0.25f, 0.30f, 0.45f));
            var lbl = b.GetComponentInChildren<Text>();
            if (lbl != null) lbl.color = on ? Color.white : new Color(1f, 1f, 1f, 0.6f);
        }
        Tint(lbClassicBtn, lbMode == "classic");
        Tint(lbTimedBtn, lbMode == "timed");
    }

    void ClearLbList()
    {
        if (lbListTransform == null) return;
        for (int i = lbListTransform.childCount - 1; i >= 0; i--)
            Destroy(lbListTransform.GetChild(i).gameObject);
    }

    void PopulateLeaderboard(Leaderboard.Resp r)
    {
        if (leaderboardPanel == null || !leaderboardPanel.activeSelf) return;
        ClearLbList();
        if (r == null || r.entries == null) { lbEmptyText.text = "Bağlanılamadı (internet/deploy gerekli)."; return; }
        if (r.entries.Length == 0) { lbEmptyText.text = "Henüz skor yok — ilk sen ol!"; return; }
        lbEmptyText.text = "";

        string myId = Leaderboard.DeviceId;
        int n = Mathf.Min(r.entries.Length, 15);
        const float rowH = 62f, gap = 6f;
        for (int i = 0; i < n; i++)
        {
            var e = r.entries[i];
            bool me = e.id == myId;
            var row = new GameObject("Row" + i, typeof(RectTransform));
            row.transform.SetParent(lbListTransform, false);
            var bg = row.AddComponent<Image>();
            bg.sprite = PillSprite(); bg.type = Image.Type.Sliced;
            bg.color = me ? new Color(0.24f, 0.55f, 0.38f, 0.95f) : new Color(0.12f, 0.15f, 0.24f, 0.85f);
            var rrt = bg.rectTransform;
            rrt.anchorMin = new Vector2(0.5f, 1); rrt.anchorMax = new Vector2(0.5f, 1); rrt.pivot = new Vector2(0.5f, 1);
            rrt.sizeDelta = new Vector2(900, rowH);
            rrt.anchoredPosition = new Vector2(0, -i * (rowH + gap));

            var rank = MakeText(row.transform, "#" + (i + 1), 34, TextAnchor.MiddleLeft,
                i < 3 ? new Color(1f, 0.82f, 0.3f) : Color.white,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(130, 50));
            rank.font = fontBold;
            var nm = MakeText(row.transform, me ? e.name + "  (sen)" : e.name, 32, TextAnchor.MiddleLeft, Color.white,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(160, 0), new Vector2(520, 50));
            nm.font = fontBold;
            var sc = MakeText(row.transform, e.score.ToString(), 36, TextAnchor.MiddleRight, new Color(1f, 0.85f, 0.35f),
                new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(200, 50));
            sc.font = fontBold;
        }
    }

    InputField MakeInputField(Transform parent, string placeholder, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("NameField", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var bg = go.AddComponent<Image>();
        bg.sprite = PillSprite(); bg.type = Image.Type.Sliced;
        bg.color = new Color(0.10f, 0.12f, 0.20f, 0.9f);
        var rt = bg.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = size;

        var input = go.AddComponent<InputField>();
        input.characterLimit = 16;

        var txtGo = new GameObject("Text", typeof(RectTransform));
        txtGo.transform.SetParent(go.transform, false);
        var txt = txtGo.AddComponent<Text>();
        txt.font = fontBold; txt.fontSize = 30; txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleLeft; txt.supportRichText = false;
        var trt = txt.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(20, 4); trt.offsetMax = new Vector2(-20, -4);

        var phGo = new GameObject("Placeholder", typeof(RectTransform));
        phGo.transform.SetParent(go.transform, false);
        var ph = phGo.AddComponent<Text>();
        ph.font = font; ph.fontSize = 26; ph.color = new Color(1, 1, 1, 0.45f);
        ph.alignment = TextAnchor.MiddleLeft; ph.text = placeholder;
        var prt = ph.rectTransform; prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
        prt.offsetMin = new Vector2(20, 4); prt.offsetMax = new Vector2(-20, -4);

        input.textComponent = txt;
        input.placeholder = ph;
        input.text = Leaderboard.PlayerName;
        input.onEndEdit.AddListener(v => Leaderboard.PlayerName = (v ?? "").Trim());
        return input;
    }

    // ---------------- Günlük görevler ----------------
    void BuildMissionsPanel(Transform parent)
    {
        missionsPanel = NewPanel(parent, "MissionsPanel", new Color(0.03f, 0.05f, 0.11f, 0.96f));

        var t = MakeText(missionsPanel.transform, "GÜNLÜK GÖREVLER", 62, TextAnchor.UpperCenter, Color.white,
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(1200, 84));
        t.font = fontBold;
        MakeText(missionsPanel.transform, "Her gün yenilenir • Ödüller anında coin olarak eklenir", 28,
            TextAnchor.UpperCenter, new Color(1f, 0.85f, 0.5f),
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -148), new Vector2(1200, 40));

        var list = new GameObject("MisList", typeof(RectTransform));
        list.transform.SetParent(missionsPanel.transform, false);
        var lrt = (RectTransform)list.transform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 1); lrt.pivot = new Vector2(0.5f, 1);
        lrt.anchoredPosition = new Vector2(0, -210); lrt.sizeDelta = new Vector2(1060, 420);
        missionsList = list.transform;

        missionsBonusText = MakeText(missionsPanel.transform, "", 32, TextAnchor.MiddleCenter, new Color(1f, 0.82f, 0.3f),
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -560), new Vector2(1000, 50));
        missionsBonusText.font = fontBold;

        MakeButton(missionsPanel.transform, "KAPAT", new Color(0.9f, 0.45f, 0.15f),
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 48), new Vector2(320, 88),
            () => missionsPanel.SetActive(false));

        missionsPanel.SetActive(false);
    }

    void OpenMissions()
    {
        missionsPanel.SetActive(true);
        RefreshMissions();
    }

    void RefreshMissions()
    {
        if (missionsList == null) return;
        for (int i = missionsList.childCount - 1; i >= 0; i--)
            Destroy(missionsList.GetChild(i).gameObject);

        const float rowH = 110f, gap = 18f;
        for (int i = 0; i < 3; i++)
        {
            var def = Missions.Get(i);
            bool dn = Missions.IsDone(i);
            var row = new GameObject("Mis" + i, typeof(RectTransform));
            row.transform.SetParent(missionsList, false);
            var bg = row.AddComponent<Image>();
            bg.sprite = PillSprite(); bg.type = Image.Type.Sliced;
            bg.color = dn ? new Color(0.16f, 0.42f, 0.28f, 0.95f) : new Color(0.12f, 0.15f, 0.24f, 0.9f);
            var rrt = bg.rectTransform;
            rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 1); rrt.pivot = new Vector2(0.5f, 1);
            rrt.sizeDelta = new Vector2(1060, rowH);
            rrt.anchoredPosition = new Vector2(0, -i * (rowH + gap));

            var desc = MakeText(row.transform, def.Text, 32, TextAnchor.MiddleLeft, Color.white,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(34, 16), new Vector2(760, 44));
            desc.font = fontBold;
            // İlerleme çubuğu yerine net sayı: "12/20" (bitti ise ✓ TAMAMLANDI).
            MakeText(row.transform, dn ? "TAMAMLANDI" : (Missions.ProgressOf(i) + " / " + def.target), 26,
                TextAnchor.MiddleLeft, dn ? new Color(0.6f, 1f, 0.75f) : new Color(1f, 1f, 1f, 0.65f),
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(34, -22), new Vector2(500, 36));

            var rw = MakeText(row.transform, "+" + def.reward, 40, TextAnchor.MiddleRight,
                dn ? new Color(0.6f, 1f, 0.75f) : new Color(1f, 0.82f, 0.3f),
                new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-36, 0), new Vector2(220, 54));
            rw.font = fontBold;
        }

        missionsBonusText.text = Missions.AllDone
            ? "3/3 TAMAM! Bonus +" + Missions.AllDoneBonus + " coin verildi ✓"
            : "3 görevi de bitir → ekstra +" + Missions.AllDoneBonus + " coin";
    }

    void ShowMissionToast(string msg)
    {
        if (missionToast == null) return;
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(ToastCo(msg));
        // Panel açıksa satırları da tazele (TAMAMLANDI'ya dönsün).
        if (missionsPanel != null && missionsPanel.activeSelf) RefreshMissions();
    }

    System.Collections.IEnumerator ToastCo(string msg)
    {
        missionToast.text = msg;
        var c = missionToast.color; c.a = 1f; missionToast.color = c;
        Sfx.Play(Sfx.Id.Score);
        yield return new WaitForSeconds(2.6f);
        for (float f = 1f; f > 0f; f -= Time.deltaTime * 2.5f)
        {
            c.a = f; missionToast.color = c;
            yield return null;
        }
        missionToast.text = "";
    }

    // ---------------- Top mağazası ----------------
    void BuildBallShopPanel(Transform parent)
    {
        ballShopPanel = NewPanel(parent, "BallShopPanel", new Color(0.03f, 0.05f, 0.11f, 0.96f));

        var t = MakeText(ballShopPanel.transform, "TOP KOLEKSİYONU", 62, TextAnchor.UpperCenter, Color.white,
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(1200, 84));
        t.font = fontBold;
        ballShopCoinText = MakeText(ballShopPanel.transform, "", 40, TextAnchor.UpperCenter, new Color(1f, 0.82f, 0.2f),
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(800, 54));
        ballShopCoinText.font = fontBold;

        var grid = new GameObject("BallGrid", typeof(RectTransform));
        grid.transform.SetParent(ballShopPanel.transform, false);
        var grt = (RectTransform)grid.transform;
        grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 0.5f); grt.pivot = new Vector2(0.5f, 0.5f);
        grt.anchoredPosition = new Vector2(0, -20); grt.sizeDelta = new Vector2(1100, 560);
        ballShopGrid = grid.transform;

        MakeButton(ballShopPanel.transform, "KAPAT", new Color(0.9f, 0.45f, 0.15f),
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 48), new Vector2(320, 88),
            () => ballShopPanel.SetActive(false));

        ballShopPanel.SetActive(false);
    }

    void OpenBallShop()
    {
        ballShopPanel.SetActive(true);
        RefreshBallShop();
    }

    void RefreshBallShop()
    {
        if (ballShopGrid == null) return;
        for (int i = ballShopGrid.childCount - 1; i >= 0; i--)
            Destroy(ballShopGrid.GetChild(i).gameObject);
        ballShopCoinText.text = "Bakiye: " + Progress.Coins + " coin";

        const float cw = 250f, ch = 300f, gap = 16f;
        int cols = 4;
        for (int i = 0; i < BallSkins.All.Length; i++)
        {
            var skin = BallSkins.All[i];
            bool unlocked = BallSkins.IsUnlocked(skin);
            bool selected = BallSkins.SelectedId == skin.id;

            int r = i / cols, c = i % cols;
            int rowCount = Mathf.Min(cols, BallSkins.All.Length - r * cols);
            float x = (c - (rowCount - 1) * 0.5f) * (cw + gap);
            float y = 150f - r * (ch + gap);

            var card = new GameObject("Skin_" + skin.id, typeof(RectTransform));
            card.transform.SetParent(ballShopGrid, false);
            var bg = card.AddComponent<Image>();
            bg.sprite = PillSprite(); bg.type = Image.Type.Sliced;
            bg.color = selected ? new Color(0.20f, 0.48f, 0.32f, 0.95f)
                     : unlocked ? new Color(0.14f, 0.18f, 0.30f, 0.92f)
                                : new Color(0.10f, 0.12f, 0.20f, 0.92f);
            var crt = bg.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f); crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(cw, ch); crt.anchoredPosition = new Vector2(x, y);

            var btn = card.AddComponent<Button>();
            btn.targetGraphic = bg;
            var captured = skin;
            btn.onClick.AddListener(() => Sfx.Play(Sfx.Id.Click));
            btn.onClick.AddListener(() => OnSkinClicked(captured));

            var imgGo = new GameObject("Ball", typeof(RectTransform));
            imgGo.transform.SetParent(card.transform, false);
            var img = imgGo.AddComponent<Image>();
            img.sprite = BallSkins.SpriteOf(skin);
            img.preserveAspect = true; img.raycastTarget = false;
            if (!unlocked) img.color = new Color(0.55f, 0.55f, 0.6f, 1f); // kilitli: soluk
            var irt = img.rectTransform;
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 1f); irt.pivot = new Vector2(0.5f, 1f);
            irt.anchoredPosition = new Vector2(0, -18); irt.sizeDelta = new Vector2(140, 140);

            var nm = MakeText(card.transform, skin.name, 30, TextAnchor.MiddleCenter, Color.white,
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 104), new Vector2(230, 40));
            nm.font = fontBold;

            // Topun özelliği: satın alma kararının asıl sebebi - her kartta net görünsün.
            MakeText(card.transform, skin.perkDesc, 20, TextAnchor.MiddleCenter, new Color(0.65f, 0.9f, 1f),
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(236, 36));

            string stateTxt = selected ? "SEÇİLİ ✓" : unlocked ? "SEÇ" : skin.cost + " coin";
            var st = MakeText(card.transform, stateTxt, 26, TextAnchor.MiddleCenter,
                selected ? new Color(0.6f, 1f, 0.75f) : unlocked ? new Color(1f, 1f, 1f, 0.85f) : new Color(1f, 0.82f, 0.3f),
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 32), new Vector2(230, 36));
            st.font = fontBold;
        }
    }

    void OnSkinClicked(BallSkins.Skin skin)
    {
        if (BallSkins.IsUnlocked(skin))
        {
            BallSkins.Select(skin);
        }
        else if (BallSkins.TryBuy(skin))
        {
            ShowMissionToast(skin.name + " topu alındı!"); // mevcut kutlama bildirimini kullan
        }
        else
        {
            ShowMissionToast("Yetersiz coin — " + skin.cost + " gerekli");
        }
        RefreshBallShop();
        Refresh(); // seçim ekranındaki coin yazısı güncellensin
    }

    // ---------------- Zorunlu isim kapısı ----------------
    void BuildNamePrompt(Transform parent)
    {
        namePrompt = NewPanel(parent, "NamePrompt", new Color(0.03f, 0.05f, 0.11f, 0.99f));

        var logoSpr = Resources.Load<Sprite>("Sprites/logo");
        if (logoSpr != null)
        {
            var lgo = new GameObject("Logo", typeof(RectTransform));
            lgo.transform.SetParent(namePrompt.transform, false);
            var li = lgo.AddComponent<Image>();
            li.sprite = logoSpr; li.preserveAspect = true; li.raycastTarget = false;
            var lrt = li.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f); lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.anchoredPosition = new Vector2(0, 250); lrt.sizeDelta = new Vector2(460, 209);
        }

        var t = MakeText(namePrompt.transform, "ADINI GİR", 72, TextAnchor.MiddleCenter, Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 88), new Vector2(1000, 100));
        t.font = fontBold;
        MakeText(namePrompt.transform, "Skor tablosunda bu isimle yarışacaksın", 34, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 22), new Vector2(1100, 60));

        namePromptInput = MakeInputField(namePrompt.transform, "Adın (en az 2 harf)",
            new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(620, 92));
        namePromptInput.onEndEdit.RemoveAllListeners(); // sadece BAŞLA ile kaydet (doğrulamalı)
        namePromptInput.text = "";

        namePromptHint = MakeText(namePrompt.transform, "", 28, TextAnchor.MiddleCenter, new Color(1f, 0.5f, 0.4f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -122), new Vector2(900, 40));

        MakeButton(namePrompt.transform, "BAŞLA", new Color(0.9f, 0.45f, 0.15f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -216), new Vector2(420, 100),
            ConfirmName);

        namePrompt.SetActive(false);
    }

    void ConfirmName()
    {
        string v = (namePromptInput != null ? namePromptInput.text : "").Trim();
        if (v.Length < 2)
        {
            if (namePromptHint != null) namePromptHint.text = "Lütfen en az 2 harflik bir isim gir";
            return;
        }
        Leaderboard.PlayerName = v;
        if (nameField != null) nameField.text = v;     // seçim ekranındaki alanı da eşitle
        if (namePromptHint != null) namePromptHint.text = "";
        namePrompt.SetActive(false);
    }

    void MaybeShowNamePrompt()
    {
        if (namePrompt == null) return;
        bool need = !Leaderboard.HasName;
        if (namePromptInput != null) namePromptInput.text = "";
        if (namePromptHint != null) namePromptHint.text = "";
        namePrompt.SetActive(need);
    }

    // ---------------- Durum / yenileme ----------------
    void OnStateChanged()
    {
        var s = GameManager.Instance.CurrentState;
        selectPanel.SetActive(s == GameManager.State.Select);
        gameOverPanel.SetActive(s == GameManager.State.GameOver);
        hud.SetActive(s == GameManager.State.Playing);
        if (leaderboardPanel != null && s == GameManager.State.Playing) leaderboardPanel.SetActive(false);
        if (missionsPanel != null && s == GameManager.State.Playing) missionsPanel.SetActive(false);
        if (ballShopPanel != null && s == GameManager.State.Playing) ballShopPanel.SetActive(false);
        // Durum ne olursa olsun duraklatma kalıntısı kalmasın (ör. oyun biterken).
        if (s != GameManager.State.Playing && pausePanel != null && pausePanel.activeSelf)
        {
            GameManager.Paused = false;
            Time.timeScale = 1f;
            pausePanel.SetActive(false);
        }
        if (s == GameManager.State.GameOver)
        {
            Sfx.Play(Sfx.Id.GameOver);
            SubmitScoreToLeaderboard(); // online tabloya gönder + sırayı göster
            // Yıldızlar tek tek "düşerek" belirir (tören hissi).
            if (starsRoutine != null) StopCoroutine(starsRoutine);
            starsRoutine = StartCoroutine(AnimateStars());
            // REKOR GECESİ: yeni rekor kırıldıysa altın konfeti yağmuru + coşku.
            if (GameManager.Instance.NewRecord)
            {
                Sfx.Play(Sfx.Id.Cheer);
                StartCoroutine(GoldRain());
            }
        }
    }

    /// <summary>REKOR GECESİ: ekranın tepesinden ~2.5 saniye altın konfeti yağar (panelin üstünde).</summary>
    System.Collections.IEnumerator GoldRain()
    {
        float t = 0f;
        while (t < 2.4f)
        {
            for (int i = 0; i < 4; i++) StartCoroutine(GoldPiece());
            t += 0.08f;
            yield return new WaitForSecondsRealtime(0.08f);
        }
    }

    System.Collections.IEnumerator GoldPiece()
    {
        var go = new GameObject("Gold", typeof(RectTransform));
        go.transform.SetParent(fxLayer, false);
        var img = go.AddComponent<Image>();
        img.raycastTarget = false;
        // Altın tonları arasında rastgele seçim
        float tone = Random.value;
        img.color = Color.Lerp(new Color(1f, 0.7f, 0.1f), new Color(1f, 0.95f, 0.5f), tone);
        var rt = img.rectTransform;
        rt.sizeDelta = new Vector2(Random.Range(10f, 18f), Random.Range(16f, 26f));
        float x = Random.Range(-940f, 940f);
        float y = 580f;
        float speed = Random.Range(380f, 640f);
        float sway = Random.Range(1.5f, 3.5f);
        float phase = Random.value * 6f;
        float spin = Random.Range(-240f, 240f);
        while (y > -580f && go != null)
        {
            float dt = Time.unscaledDeltaTime;
            y -= speed * dt;
            rt.anchoredPosition = new Vector2(x + Mathf.Sin(Time.unscaledTime * sway + phase) * 40f, y);
            go.transform.Rotate(0f, 0f, spin * dt);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    /// <summary>Kazanılan yıldızları sırayla, büyüyüp yerine oturarak ve ses eşliğinde gösterir.</summary>
    System.Collections.IEnumerator AnimateStars()
    {
        var gm = GameManager.Instance;
        int stars = 0;
        var ths = gm.CurrentMode == GameManager.Mode.Timed ? GameConfig.StarThresholdsTimed : GameConfig.StarThresholds;
        foreach (int th in ths) if (gm.Score >= th) stars++;
        for (int i = 0; i < starImgs.Length; i++)
        {
            starImgs[i].rectTransform.localScale = Vector3.one;
            starImgs[i].color = i < stars
                ? new Color(1f, 0.8f, 0.15f, 0f)            // kazanılan: önce görünmez, animasyonla gelecek
                : new Color(1f, 1f, 1f, 0.13f);              // kazanılmayan: soluk sabit
        }
        yield return new WaitForSecondsRealtime(0.45f);
        for (int i = 0; i < stars; i++)
        {
            var img = starImgs[i];
            Sfx.Play(Sfx.Id.Score);
            float t = 0f; const float d = 0.24f;
            while (t < d)
            {
                t += Time.unscaledDeltaTime; // ağır çekimden etkilenmesin
                float p = Mathf.Clamp01(t / d);
                img.rectTransform.localScale = Vector3.one * Mathf.Lerp(2.3f, 1f, p * p);
                img.color = new Color(1f, 0.8f, 0.15f, p);
                yield return null;
            }
            img.rectTransform.localScale = Vector3.one;
            yield return new WaitForSecondsRealtime(0.14f);
        }
        starsRoutine = null;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        // ESC = duraklat/devam (masaüstünde standart beklenti).
        if (Input.GetKeyDown(KeyCode.Escape) && gm.CurrentState == GameManager.State.Playing)
            TogglePause();

        // Zaman modu: süre göstergesi her kare güncellenir (son 10 saniyede kırmızı yanar).
        if (gm.CurrentState == GameManager.State.Playing && gm.CurrentMode == GameManager.Mode.Timed && ballsText != null)
        {
            int s = Mathf.CeilToInt(gm.TimeLeft);
            ballsText.text = "SÜRE: " + s;
            ballsText.color = s <= 10 ? new Color(1f, 0.35f, 0.25f) : new Color(1f, 0.85f, 0.3f);
        }

        // ALEV MODU vinyeti: kombo 5+ iken ekran kenarları nabız gibi yanar, seri bitince söner.
        if (vignetteImg != null)
        {
            bool fire = gm.CurrentState == GameManager.State.Playing && gm.Combo >= 5 && !GameManager.Paused;
            fireGlow = Mathf.MoveTowards(fireGlow, fire ? 1f : 0f, Time.unscaledDeltaTime * 2.5f);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);
            var vc = vignetteImg.color;
            vc.a = 0.55f * fireGlow * pulse;
            vignetteImg.color = vc;
        }

        // Skor sayacı animasyonu: görünen değer gerçeğe doğru hızla sayar; artışta yazı zıplar.
        if (scoreText == null) return;
        if (Mathf.RoundToInt(shownScore) != gm.Score)
        {
            float speed = Mathf.Max(12f, Mathf.Abs(gm.Score - shownScore) * 7f);
            shownScore = Mathf.MoveTowards(shownScore, gm.Score, speed * Time.deltaTime);
            scoreText.text = Mathf.RoundToInt(shownScore).ToString();
        }
        if (scorePunch > 0f)
        {
            scorePunch -= Time.deltaTime;
            float k = 1f + Mathf.Sin(Mathf.Clamp01(1f - scorePunch / 0.28f) * Mathf.PI) * 0.22f;
            scoreText.rectTransform.localScale = Vector3.one * Mathf.Max(1f, k);
            if (scorePunch <= 0f) scoreText.rectTransform.localScale = Vector3.one;
        }
    }

    void Refresh()
    {
        var gm = GameManager.Instance;
        if (scoreText != null)
        {
            if (gm.Score < Mathf.RoundToInt(shownScore)) // yeni oyun: sayaç anında sıfırlanır
            {
                shownScore = gm.Score;
                scoreText.text = gm.Score.ToString();
            }
            if (gm.Score > lastScoreSeen) scorePunch = 0.28f; // artış -> zıplama
            lastScoreSeen = gm.Score;
        }
        // Oyuncu/özellik çipi: ad özellik renginde; ikon yalnız seçim değişince güncellenir.
        if (nameText != null)
        {
            var trait = PlayerData.CurrentTrait;
            bool royal = PlayerData.IsRoyal(PlayerData.SelectedFigure);
            nameText.text = PlayerData.NameOf(PlayerData.SelectedFigure) + (royal ? "  •  TÜM YETENEKLER" : "");
            nameText.color = royal ? new Color(1f, 0.82f, 0.25f) : PlayerData.TraitColor(trait);
            if (hudTraitIcon != null && hudTraitFig != PlayerData.SelectedFigure)
            {
                hudTraitFig = PlayerData.SelectedFigure;
                // Asil: altın yıldız (tüm yetenekler); diğerleri: kendi özellik rozeti.
                var ts = royal ? null : TraitSprite(trait);
                hudTraitIcon.sprite = ts != null ? ts : MakeStarSprite();
                hudTraitIcon.color = ts != null ? Color.white
                    : royal ? new Color(1f, 0.82f, 0.25f) : PlayerData.TraitColor(trait);
            }
        }
        // Süre modunda top ikonu gizlenir (yerine "SÜRE: N" yazar); klasikte görünür.
        if (hudBallIcon != null) hudBallIcon.enabled = gm.CurrentMode == GameManager.Mode.Classic;
        if (ballsText != null && gm.CurrentMode == GameManager.Mode.Classic) // zaman modunda Update() yazar
        {
            ballsText.text = gm.BallsLeft.ToString(); // ikon zaten "top" olduğunu anlatıyor
            // Son toplarda uyarı rengi: oyuncu hakkının azaldığını fark etsin.
            ballsText.color = gm.BallsLeft <= 3 ? new Color(1f, 0.4f, 0.25f) : new Color(1f, 0.85f, 0.3f);
        }
        // Kombo göstergesi artık PUAN çarpanını da söylüyor - oyuncu serinin değerini görsün.
        if (comboText != null)
        {
            float cm = Mathf.Min(1f + (gm.Combo - 1) * GameConfig.ComboStepMult, GameConfig.ComboMaxMult);
            comboText.text = gm.Combo >= 2 ? ("KOMBO x" + gm.Combo + "  •  PUAN ×" + cm.ToString("0.#")) : "";
        }
        if (finalText != null) finalText.text = "Skor: " + gm.Score;
        if (starImgs != null && starsRoutine == null) // animasyon oynarken renkleri ezme
        {
            int stars = 0;
            var ths = gm.CurrentMode == GameManager.Mode.Timed ? GameConfig.StarThresholdsTimed : GameConfig.StarThresholds;
            foreach (int th in ths) if (gm.Score >= th) stars++;
            for (int i = 0; i < starImgs.Length; i++)
                starImgs[i].color = i < stars ? new Color(1f, 0.8f, 0.15f) : new Color(1f, 1f, 1f, 0.13f);
        }
        if (highText != null)
        {
            highText.text = gm.NewRecord ? ("YENİ REKOR! " + gm.High) : ("Rekor: " + gm.High);
            highText.color = gm.NewRecord ? new Color(1f, 0.85f, 0.3f) : Color.white;
        }
        if (trophyImg != null) trophyImg.gameObject.SetActive(gm.NewRecord); // kupa sadece rekor gecesi
        if (streakText != null)
            streakText.text = gm.DayStreak >= 1 ? ("GÜNLÜK SERİ: " + gm.DayStreak + ". GÜN") : "";
        if (careerText != null)
            careerText.text = gm.CareerGames > 0
                ? $"KARİYER   •   Maç: {gm.CareerGames}   •   Toplam Sayı: {gm.CareerScore}   •   SWISH: {gm.CareerSwish}   •   En İyi Kombo: x{gm.BestCombo}"
                : "";
        if (coinText != null) coinText.text = "COIN  " + Progress.Coins;
        if (gameOverCoinText != null) gameOverCoinText.text = "+ " + gm.LastCoinsEarned + " COIN";
    }

    // ---------------- Yardımcılar ----------------
    static Sprite starSprite;

    /// <summary>Kodla çizilen 5 kollu yıldız (font glifine güvenmek yerine garantili görsel).
    /// 2x2 süper örnekleme ile kenarları yumuşatılır; beyaz üretilir, Image.color ile boyanır.</summary>
    static Sprite MakeStarSprite()
    {
        if (starSprite != null) return starSprite;
        const int S = 96;
        var c = new Vector2(S / 2f, S / 2f);
        float outer = S * 0.48f, inner = outer * 0.5f;
        var pts = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float ang = Mathf.PI / 2f + i * Mathf.PI / 5f; // tepe yukarı bakar
            float r = (i % 2 == 0) ? outer : inner;
            pts[i] = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
        }
        bool Inside(Vector2 p)
        {
            bool ins = false;
            for (int i = 0, j = 9; i < 10; j = i++)
                if ((pts[i].y > p.y) != (pts[j].y > p.y) &&
                    p.x < (pts[j].x - pts[i].x) * (p.y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x)
                    ins = !ins;
            return ins;
        }
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                int hit = 0; // 2x2 süper örnekleme -> kenar yumuşatma
                if (Inside(new Vector2(x + 0.25f, y + 0.25f))) hit++;
                if (Inside(new Vector2(x + 0.75f, y + 0.25f))) hit++;
                if (Inside(new Vector2(x + 0.25f, y + 0.75f))) hit++;
                if (Inside(new Vector2(x + 0.75f, y + 0.75f))) hit++;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, hit / 4f));
            }
        }
        tex.Apply();
        starSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return starSprite;
    }

    static Sprite lockSprite;

    /// <summary>Kodla çizilen asma kilit (gövde + kemer + anahtar deliği), 2x2 kenar yumuşatmalı.</summary>
    static Sprite MakeLockSprite()
    {
        if (lockSprite != null) return lockSprite;
        const int S = 96;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        bool Inside(float fx, float fy)
        {
            bool body = fx > 0.26f && fx < 0.74f && fy > 0.12f && fy < 0.56f;   // gövde (alt)
            float dx = fx - 0.5f, dy = fy - 0.60f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            bool shackle = fy > 0.52f && d > 0.14f && d < 0.225f;               // kemer (üst kavis)
            float kx = fx - 0.5f, ky = fy - 0.33f;
            bool hole = Mathf.Sqrt(kx * kx + ky * ky) < 0.05f;                  // anahtar deliği (boşluk)
            return (body || shackle) && !hole;
        }
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int hit = 0;
                if (Inside((x + 0.25f) / S, (y + 0.25f) / S)) hit++;
                if (Inside((x + 0.75f) / S, (y + 0.25f) / S)) hit++;
                if (Inside((x + 0.25f) / S, (y + 0.75f) / S)) hit++;
                if (Inside((x + 0.75f) / S, (y + 0.75f) / S)) hit++;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, hit / 4f));
            }
        tex.Apply();
        lockSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return lockSprite;
    }

    GameObject NewPanel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = color.a > 0.01f;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return go;
    }

    Text MakeText(Transform parent, string txt, int size, TextAnchor anchor, Color col,
        Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 sd)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = font; t.text = txt; t.fontSize = size; t.alignment = anchor; t.color = col;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = sd;
        return t;
    }

    Button MakeButton(Transform parent, string txt, Color bg,
        Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size, UnityAction onClick)
    {
        var go = new GameObject("Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        if (sprButton != null) { img.sprite = sprButton; img.color = Color.white; } // görselli buton
        else img.color = bg;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => Sfx.Play(Sfx.Id.Click));
        btn.onClick.AddListener(onClick);
        var rt = img.rectTransform;
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;

        var label = MakeText(go.transform, txt, 36, TextAnchor.MiddleCenter, Color.white,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        label.font = fontBold;
        // Parlak buton gövdesinin optik merkezi geometrik merkezden bir tık yukarıda:
        // yazıyı 2px kaldırmak gözün "tam ortada" algısını yakalar (tipografi inceliği).
        label.rectTransform.offsetMin = new Vector2(0, 2);
        label.rectTransform.offsetMax = new Vector2(0, 2);
        return btn;
    }
}
