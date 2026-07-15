using UnityEngine;

/// <summary>
/// OYUNUN BAŞLANGIÇ NOKTASI.
/// Sahneyi tamamen koddan kurar: kamera, arka plan, pota, atıcı, top, figür ve arayüz.
/// Sahneye elle hiçbir şey eklemene gerek yok — Play'e basman yeterli.
/// (Boş bir sahnede bile otomatik olarak kendini oluşturur.)
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    static bool built = false;

    Camera cam;
    CameraShake camShake;
    BallShooter shooter;
    Hoop hoop;
    GameObject playerRoot;
    SpriteRenderer hoopGlowSr; // pota halesi (ALEV MODU'nda renk değiştirir, sayıda flaş yapar)
    SpriteRenderer bgSr;       // arka plan (ekran oranı değişince yeniden ölçeklenir)
    float lastAspect;          // oran değişimini (telefon döndürme) yakalamak için
    float glowFlashT;          // sayı sonrası beyaz-sıcak flaş süresi
    static readonly Color GlowNormal = new Color(1f, 0.93f, 0.7f, 0.55f);
    static readonly Color GlowFire = new Color(1f, 0.42f, 0.12f, 0.72f);
    static readonly Color GlowFlash = new Color(1f, 1f, 0.88f, 0.95f);

    // Sahnede GameBootstrap yoksa otomatik oluştur (Play'e basınca çalışır).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoStart()
    {
        if (Object.FindAnyObjectByType<GameBootstrap>() == null)
        {
            var go = new GameObject("Game (Auto)");
            go.AddComponent<GameBootstrap>();
        }
    }

    void Awake()
    {
        if (built) { Destroy(gameObject); return; }
        built = true;
    }

    void OnDestroy()
    {
        // Play modundan çıkınca tekrar kurulabilsin.
        built = false;
    }

    void Start()
    {
        BuildWorld();
    }

    void BuildWorld()
    {
        // 1) Oyun yöneticisi
        if (GameManager.Instance == null)
            new GameObject("GameManager").AddComponent<GameManager>();

        // 1b) Online skor tablosu istemcisi (kalıcı; sahneler arası yaşar)
        if (Leaderboard.Instance == null)
            new GameObject("Leaderboard").AddComponent<Leaderboard>();

        // 2) Kamera
        cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
        }
        cam.orthographic = true;
        ApplyCameraSize(); // ekran oranına göre boyut (dar ekranda otomatik uzaklaşır)
        cam.backgroundColor = GameConfig.SkyColor;
        cam.clearFlags = CameraClearFlags.SolidColor;
        // Dikey kurgu: oyuncu altta olduğundan kamera merkezi aşağı kaydırılır.
        cam.transform.position = new Vector3(0f, GameConfig.CameraY, -10f);
        camShake = cam.GetComponent<CameraShake>();
        if (camShake == null) camShake = cam.gameObject.AddComponent<CameraShake>();

        // 3) Arka plan (saha)
        // Assets/Resources/Sprites/court_bg.png eklersen otomatik onu kullanır;
        // yoksa koddan gece sahası (yıldızlı gökyüzü + ay + ahşap zemin) üretilir.
        var bg = new GameObject("Background").AddComponent<SpriteRenderer>();
        bg.sprite = Resources.Load<Sprite>("Sprites/court_bg");
        if (bg.sprite == null) bg.sprite = MakeCourtSprite(cam.aspect);
        bg.sortingOrder = 0;
        bgSr = bg;
        FitBackground(bg);

        // 4) Zemin (görünmez çarpışma) - top yere düşsün diye
        var ground = new GameObject("Ground");
        ground.transform.position = new Vector3(0f, GameConfig.PlayerBase.y - 0.4f, 0f);
        var gc = ground.AddComponent<BoxCollider2D>();
        gc.size = new Vector2(40f, 1f);
        gc.sharedMaterial = new PhysicsMaterial2D("ground") { bounciness = 0.2f, friction = 0.6f };

        // 5) Pota
        var hoopGo = new GameObject("Hoop");
        var hoopSr = hoopGo.AddComponent<SpriteRenderer>();
        hoopSr.sprite = Resources.Load<Sprite>("Sprites/hoop");
        hoopSr.sortingOrder = 3;
        hoopGo.transform.position = GameConfig.HoopPos;
        float hoopScale = GameUtil.FitToHeight(hoopSr, GameConfig.HoopHeight);

        // Gece göğü ile pota rengi (lacivert) birbirine çok yakın olduğu için
        // pota arkasına parlak bir "spot ışığı" halesi koyuyoruz ki her zaman net seçilsin.
        // Hale, potanın ÇOCUĞU: pota hareket edince (zorluk) hale de onunla gider.
        if (hoopSr.sprite != null)
        {
            var glowGo = new GameObject("HoopGlow");
            var glowSr = glowGo.AddComponent<SpriteRenderer>();
            glowSr.sprite = MakeGlowSprite();
            glowSr.color = GlowNormal;
            glowSr.sortingOrder = 2; // pota(3)'nın hemen altı, arka plan(0)'ın üstü
            hoopGlowSr = glowSr;
            glowGo.transform.SetParent(hoopGo.transform, false);
            // Ebeveyn (pota) ölçekli olduğundan istenen dünya boyutu için ölçeğine bölüyoruz.
            float glowSize = GameConfig.HoopHeight * 2.1f / hoopScale;
            glowGo.transform.localScale = new Vector3(glowSize, glowSize, 1f);

            // File ÖN katmanı: pota dokusunun sadece file bölgesi (tahtanın altındaki ipler)
            // ayrı bir sprite olarak topun ÖNÜNE çizilir. Sayı olan top böylece iplerin
            // arkasından geçer ama opak tahtanın arkasında kaybolmaz.
            // Oranlar hoop.png'ye göre: alt %18.5'lik şerit, yatayda file genişliği kadar.
            var spr = hoopSr.sprite;
            var mainRect = spr.rect;
            var netRect = new Rect(mainRect.x + mainRect.width * 0.24f,
                                   mainRect.y,
                                   mainRect.width * 0.47f,
                                   mainRect.height * 0.185f);
            var netGo = new GameObject("NetFront");
            netGo.transform.SetParent(hoopGo.transform, false);
            Vector2 netCenterPx = new Vector2(netRect.x - mainRect.x + netRect.width * 0.5f,
                                              netRect.y - mainRect.y + netRect.height * 0.5f);
            netGo.transform.localPosition = (netCenterPx - spr.pivot) / spr.pixelsPerUnit;
            var netSr = netGo.AddComponent<SpriteRenderer>();
            netSr.sprite = Sprite.Create(spr.texture, netRect, new Vector2(0.5f, 0.5f), spr.pixelsPerUnit);
            netSr.sortingOrder = 7; // top(6) fileden geçerken ipler önde kalır
        }

        hoop = hoopGo.AddComponent<Hoop>();
        hoop.Init();

        // 6) Atıcı + top
        shooter = new GameObject("Shooter").AddComponent<BallShooter>();
        shooter.Init(cam, Resources.Load<Sprite>("Sprites/ball"));

        // 7) Arayüz
        var ui = new GameObject("UI").AddComponent<UIController>();
        ui.Init(this);

        // 8) Skor efektleri (temiz/SWISH atışlar daha gösterişli kutlanır)
        GameManager.Instance.OnScored += (pts, pos, clean) =>
        {
            Sfx.Play(clean ? Sfx.Id.Swish : Sfx.Id.Score);
            Sfx.Play(Sfx.Id.Cheer); // kalabalık coşkusu (skor melodisinin üstüne biner)
            BurstFlash.Spawn(pos + Vector3.up * 0.25f, clean ? 2.6f : 2.0f); // yazının arkasında patlama
            // SWISH ise yeşil kelime-görseli + "+N"; normalde sadece "+N" (glyph rakamlar).
            SpritePopup.Spawn(pos + Vector3.up * 0.3f, clean ? "swish" : null, "+" + pts);
            // Zaman modu: kazanılan ek süre görünür olsun (sol yana, camgöbeği).
            if (GameManager.Instance.CurrentMode == GameManager.Mode.Timed)
                ScorePopup.Spawn("+" + Mathf.RoundToInt(GameConfig.TimedScoreBonus) + "sn",
                    pos + new Vector3(-1.1f, 0.9f, 0f), new Color(0.45f, 0.9f, 1f));
            Confetti.Burst(pos, clean ? 24 : 14);
            if (camShake != null) camShake.Shake(clean ? 0.22f : 0.18f, clean ? 0.16f : 0.12f);
            if (playerRoot != null)
            {
                var anim = playerRoot.GetComponent<BobbleAnimator>();
                if (anim != null) anim.Pop();
            }
            // ALEV MODU duyurusu: kombo tam 5'e ulaştığı an bir kez.
            if (GameManager.Instance.Combo == 5)
                SpritePopup.Spawn(pos + Vector3.up * 1.6f, "alev", null);
            glowFlashT = 0.22f; // pota ışık şovu: haleye beyaz-sıcak flaş
        };

        // 9) Arena ambiyansı: alçak kalabalık uğultusu (sahneye canlılık katar).
        Sfx.StartAmbience();
    }

    /// <summary>Kamerayı ekran oranına uydurur: dar (dikey) ekranda sahne sığana kadar uzaklaşır.</summary>
    void ApplyCameraSize()
    {
        if (cam == null) return;
        lastAspect = cam.aspect;
        cam.orthographicSize = Mathf.Max(GameConfig.CameraSize, GameConfig.MinHalfWidth / cam.aspect);
    }

    void Update()
    {
        // Telefon döndürüldü / pencere boyutlandı: kamerayı ve arka planı yeniden uydur.
        if (cam != null && Mathf.Abs(cam.aspect - lastAspect) > 0.01f)
        {
            ApplyCameraSize();
            if (bgSr != null) FitBackground(bgSr);
        }

        // Pota halesi durumu: taban rengi (normal / ALEV MODU) + sayı sonrası beyaz flaş sönümü.
        if (hoopGlowSr == null || GameManager.Instance == null) return;
        Color baseCol = GameManager.Instance.Combo >= 5 ? GlowFire : GlowNormal;
        if (glowFlashT > 0f)
        {
            glowFlashT -= Time.deltaTime;
            float k = Mathf.Clamp01(glowFlashT / 0.22f);
            hoopGlowSr.color = Color.Lerp(baseCol, GlowFlash, k);
        }
        else
        {
            hoopGlowSr.color = baseCol;
        }
    }

    static Sprite glowSprite;

    /// <summary>Kodla üretilen yumuşak, dairesel bir parlaklık dokusu (dosyaya bağlı değil, her zaman çalışır).
    /// Pota halesi ve altın top parlaması gibi farklı yerlerde ortak kullanılır.</summary>
    public static Sprite MakeGlowSprite()
    {
        if (glowSprite != null) return glowSprite;
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Vector2 c = new Vector2(size / 2f, size / 2f);
        float maxR = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / maxR;
                float a = Mathf.Clamp01(1f - d);
                a *= a; // yumuşak (quadratic) düşüş
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return glowSprite;
    }

    /// <summary>
    /// Kodla üretilen gece basketbol sahası arka planı: yıldızlı gökyüzü, ay ve ahşap zemin.
    /// (Resources'ta court_bg görseli yoksa devreye girer; ekran oranına göre üretilir.)
    /// </summary>
    static Sprite MakeCourtSprite(float aspect)
    {
        const int H = 540;
        int W = Mathf.Max(H, Mathf.RoundToInt(H * aspect));
        float worldH = GameConfig.CameraSize * 2f;
        // Zemin çizgisi, görünmez Ground collider'ının üst kenarıyla aynı hizada dursun.
        float floorY = GameConfig.PlayerBase.y + 0.1f;
        int floorPx = Mathf.RoundToInt((floorY + GameConfig.CameraSize) / worldH * H);

        var px = new Color[W * H];
        var skyTop = new Color(0.03f, 0.05f, 0.11f);
        var skyHorizon = new Color(0.17f, 0.16f, 0.32f);
        var floorLight = new Color(0.46f, 0.28f, 0.16f);
        var floorDark = new Color(0.27f, 0.15f, 0.09f);

        for (int y = 0; y < H; y++)
        {
            Color c;
            if (y >= floorPx)
            {
                float t = Mathf.InverseLerp(H - 1, floorPx, y);
                c = Color.Lerp(skyTop, skyHorizon, t * t); // ufka yaklaştıkça aydınlanan gece göğü
            }
            else
            {
                float t = Mathf.InverseLerp(floorPx, 0, y);
                c = Color.Lerp(floorLight, floorDark, t); // öne doğru koyulaşan ahşap zemin
            }
            int row = y * W;
            for (int x = 0; x < W; x++) px[row + x] = c;
        }

        void Blend(int x, int y, Color c, float a)
        {
            if (x < 0 || x >= W || y < 0 || y >= H) return;
            int i = y * W + x;
            px[i] = Color.Lerp(px[i], c, a);
        }

        // Ahşap plaka araları (zeminde ince dikey koyu çizgiler)
        for (int x = 36; x < W; x += 72)
            for (int y = 0; y < floorPx; y++)
                Blend(x, y, Color.black, 0.16f);

        // Saha çizgisi (zeminin üst kenarı boyunca)
        var lineCol = new Color(0.95f, 0.92f, 0.82f);
        for (int x = 0; x < W; x++)
        {
            Blend(x, floorPx - 1, lineCol, 0.9f);
            Blend(x, floorPx - 2, lineCol, 0.9f);
            Blend(x, floorPx - 3, lineCol, 0.35f); // alt kenarda hafif yumuşatma
        }

        // Yıldızlar (sabit tohum: her açılışta aynı gökyüzü)
        var rnd = new System.Random(1907);
        var starCol = new Color(0.92f, 0.95f, 1f);
        for (int i = 0; i < 160; i++)
        {
            int sx = rnd.Next(0, W);
            int sy = rnd.Next(floorPx + 12, H - 4);
            float b = 0.3f + (float)rnd.NextDouble() * 0.7f;
            Blend(sx, sy, starCol, b);
            if (b > 0.75f) // parlak yıldızlara küçük bir parıltı
            {
                Blend(sx + 1, sy, starCol, b * 0.45f);
                Blend(sx - 1, sy, starCol, b * 0.45f);
                Blend(sx, sy + 1, starCol, b * 0.45f);
                Blend(sx, sy - 1, starCol, b * 0.45f);
            }
        }

        // Ay + halesi (skor/kombo yazılarıyla çakışmayan bir noktada)
        var moonCol = new Color(0.98f, 0.95f, 0.80f);
        int mx = Mathf.RoundToInt(W * 0.30f), my = Mathf.RoundToInt(H * 0.86f);
        float mr = H * 0.040f;
        int reach = Mathf.CeilToInt(mr * 3.2f);
        for (int dy = -reach; dy <= reach; dy++)
        {
            for (int dx = -reach; dx <= reach; dx++)
            {
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d <= mr) Blend(mx + dx, my + dy, moonCol, 1f);
                else
                {
                    float a = 1f - (d - mr) / (mr * 2.2f);
                    if (a > 0f) Blend(mx + dx, my + dy, moonCol, a * a * 0.30f);
                }
            }
        }

        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels(px);
        tex.Apply();
        // PPU = H / dünyaYüksekliği -> sprite tam kamera yüksekliğini kaplar.
        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), H / worldH);
    }

    void FitBackground(SpriteRenderer sr)
    {
        if (sr.sprite == null) return;
        // Görselin ZEMİN ÇİZGİSİ (piksel ölçümü: alttan %29) dünyadaki zemine oturmalı;
        // yoksa figür zeminin üstünde/altında havada duruyormuş gibi görünür. Ölçek,
        // zemin sabitken görüntünün hem üstte hem altta ekranı kaplamasını garanti eder.
        const float FloorFrac = 0.29f;
        float floorWorldY = GameConfig.PlayerBase.y + 0.1f;
        float camY = cam.transform.position.y;
        float ortho = cam.orthographicSize;
        float worldW = ortho * 2f * cam.aspect;
        var size = sr.sprite.bounds.size;
        float needTop = (camY + ortho) - floorWorldY;   // zeminden yukarı kaplanacak mesafe
        float needBot = floorWorldY - (camY - ortho);   // zeminden aşağı kaplanacak mesafe
        float s = Mathf.Max(worldW / size.x,
                            needTop / ((1f - FloorFrac) * size.y),
                            Mathf.Max(0.01f, needBot) / (FloorFrac * size.y)) * 1.02f;
        sr.transform.localScale = new Vector3(s, s, 1f);
        float centerY = floorWorldY + (0.5f - FloorFrac) * size.y * s;
        sr.transform.position = new Vector3(0f, centerY, 5f);
    }

    // ---- Figür oluşturma ----
    void SpawnPlayer(int idx)
    {
        if (playerRoot != null) Destroy(playerRoot);
        playerRoot = new GameObject("Player");
        playerRoot.transform.position = GameConfig.PlayerBase;

        var figGo = new GameObject("Fig");
        figGo.transform.SetParent(playerRoot.transform, false);
        var sr = figGo.AddComponent<SpriteRenderer>();
        sr.sprite = Resources.Load<Sprite>($"Figures/fig_{(idx + 1):00}");
        sr.sortingOrder = 4;
        float s = GameUtil.FitToHeight(sr, GameConfig.PlayerHeight);
        float worldH = (sr.sprite != null ? sr.sprite.bounds.size.y : 1f) * s;
        figGo.transform.localPosition = new Vector3(0f, worldH * 0.5f, 0f); // taban kökte olsun

        playerRoot.AddComponent<BobbleAnimator>();
    }

    // ---- UI'den çağrılan akış kontrolleri ----
    public void PlayWithFigure(int i)
    {
        PlayerData.SelectedFigure = i;
        SpawnPlayer(i);
        GameManager.Instance.StartGame();
        shooter.PrepareForPlay();
    }

    public void Retry()
    {
        if (playerRoot == null) SpawnPlayer(PlayerData.SelectedFigure);
        GameManager.Instance.StartGame();
        shooter.PrepareForPlay();
    }

    public void BackToSelect()
    {
        GameManager.Instance.GoSelect();
    }
}
