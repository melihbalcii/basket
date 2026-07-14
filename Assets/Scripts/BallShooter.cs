using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Atış kontrolü: fareyle (veya parmakla) sürükle-bırak.
/// Sürükledikçe yörünge çizgisi gösterilir; bırakınca top fırlatılır.
/// Tek bir top objesini tekrar tekrar kullanır.
/// </summary>
public class BallShooter : MonoBehaviour
{
    Camera cam;
    Sprite ballSprite;
    Ball ball;
    SpriteRenderer ballSr;
    SpriteRenderer goldGlowSr; // altın topun arkasındaki parlaklık halkası
    SpriteRenderer shadowSr;   // topun zemindeki gölgesi (uçuşa derinlik hissi verir)
    static readonly Color GoldGlowColor = new Color(1f, 0.82f, 0.2f);
    TrailRenderer trail;
    LineRenderer traj;               // yedek nişan çizgisi (dot görseli yoksa)
    Transform aimRoot;               // noktalı nişan yayının kökü
    SpriteRenderer[] aimDots;        // yay boyunca dizilen parlak noktalar
    SpriteRenderer aimArrow;         // yayın ucundaki yön oku
    Vector2 ballHome;
    bool aiming;
    Vector2 dragStartWorld; // parmağın/farenin İLK bastığı nokta (göreli sürükleme referansı)
    Vector2 smoothPull;     // yumuşatılmış çekiş vektörü (titreme filtresi)
    int aimStartFrame; // nişan hangi karede başladı (aynı karede basma+bırakma = kazara atışı engeller)

    public void Init(Camera c, Sprite ballSpr)
    {
        cam = c;
        ballSprite = ballSpr;
        ballHome = GameConfig.PlayerBase + GameConfig.BallOffsetFromBase;
        traj = CreateTrajectory();
        CreateAimDots(); // görsel varsa çizgi yerine noktalı yay kullanılır
        SpawnBall();
        // Çizgi, topun sprite materyalini paylaşır: Shader.Find'a göre daha güvenli,
        // çünkü o shader build'e dahil edilmeyip çizgiyi pembe/görünmez bırakabilir.
        traj.material = ballSr.sharedMaterial;
        PrepareForPlay();
    }

    void SpawnBall()
    {
        var go = new GameObject("Ball");
        ballSr = go.AddComponent<SpriteRenderer>();
        ballSr.sprite = ballSprite;
        ballSr.sortingOrder = 6;
        GameUtil.FitToHeight(ballSr, GameConfig.BallDiameter);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        var col = go.AddComponent<CircleCollider2D>();
        // ölçek sonrası dünyada doğru yarıçap; görsel yoksa (henüz eklenmediyse) yapılandırılan çapa düş.
        col.radius = (ballSprite != null) ? ballSprite.bounds.extents.y : GameConfig.BallDiameter * 0.5f;
        col.sharedMaterial = new PhysicsMaterial2D("ball") { bounciness = GameConfig.BallBounce, friction = 0.4f };

        // "Alev serisi" izi: kombo yükselince atışta yanar (FireBall karar verir).
        trail = go.AddComponent<TrailRenderer>();
        trail.time = 0.22f;
        trail.startWidth = GameConfig.BallDiameter * 0.55f;
        trail.endWidth = 0.04f;
        trail.material = ballSr.sharedMaterial;
        trail.sortingOrder = 5; // topun (6) hemen arkası
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.62f, 0.12f), 0f), new GradientColorKey(new Color(1f, 0.25f, 0f), 1f) },
            new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;
        trail.emitting = false;

        // Altın top parlaması: topun çocuğu, sadece altın atışlarda görünür.
        var glowGo = new GameObject("GoldGlow");
        glowGo.transform.SetParent(go.transform, false);
        goldGlowSr = glowGo.AddComponent<SpriteRenderer>();
        goldGlowSr.sprite = GameBootstrap.MakeGlowSprite();
        goldGlowSr.color = GoldGlowColor;
        goldGlowSr.sortingOrder = 5; // topun (6) hemen arkası
        // Ebeveyn (top) ölçekli; dünyada topun ~1.9 katı çapında dursun diye hem parlaklık
        // sprite'ının doğal boyutuna hem de ebeveyn ölçeğine böl.
        float glowWorld = GameConfig.BallDiameter * 1.9f;
        float glowNatural = goldGlowSr.sprite.bounds.size.y;
        float parentScale = go.transform.localScale.x > 0.0001f ? go.transform.localScale.x : 1f;
        glowGo.transform.localScale = Vector3.one * (glowWorld / glowNatural / parentScale);
        goldGlowSr.enabled = false;

        // Zemin gölgesi: topu takip eden, yükseklikle küçülüp soluklaşan yassı leke.
        var shGo = new GameObject("BallShadow");
        shadowSr = shGo.AddComponent<SpriteRenderer>();
        shadowSr.sprite = GameBootstrap.MakeGlowSprite();
        shadowSr.color = new Color(0f, 0f, 0f, 0.30f);
        shadowSr.sortingOrder = 1; // arka planın üstü, her şeyin altı
        shadowSr.enabled = false;

        ball = go.AddComponent<Ball>();
        ball.Init();
        ball.OnResolved += OnBallResolved;
        ball.Hold(ballHome);
    }

    /// <summary>Gölgeyi topun altına yerleştirir: top yükseldikçe küçülür ve soluklaşır.</summary>
    void UpdateShadow(bool playing)
    {
        if (shadowSr == null || ball == null) return;
        bool show = playing && ballSr != null && ballSr.enabled && ballSr.color.a > 0.3f;
        shadowSr.enabled = show;
        if (!show) return;

        float groundY = GameConfig.PlayerBase.y + 0.1f; // görünmez zeminin üst kenarı
        float h = Mathf.Max(0f, ball.transform.position.y - groundY);
        float t = Mathf.Clamp01(h / 8f); // 0=yerde, 1=çok yüksekte
        float w = Mathf.Lerp(1.0f, 0.45f, t) * GameConfig.BallDiameter;
        float natural = shadowSr.sprite.bounds.size.y;
        shadowSr.transform.position = new Vector3(ball.transform.position.x, groundY + 0.02f, 0f);
        shadowSr.transform.localScale = new Vector3(w / natural, w / natural * 0.32f, 1f);
        var c = shadowSr.color;
        c.a = Mathf.Lerp(0.30f, 0.08f, t) * ballSr.color.a;
        shadowSr.color = c;
    }

    /// <summary>Yeni oyun/yeniden başlatma için topu ele koyar.</summary>
    public void PrepareForPlay()
    {
        CancelInvoke();
        // Seçili top görünümünü uygula (mağazadan değiştirilmiş olabilir).
        if (ballSr != null)
        {
            var skin = BallSkins.CurrentSprite();
            if (skin != null && ballSr.sprite != skin) ballSr.sprite = skin;
        }
        if (ball != null) ball.Hold(ballHome);
        // İz temizlenmeli: Hold topu ışınlar, yoksa ekranda uzun bir çizgi kalır.
        if (trail != null) { trail.Clear(); trail.emitting = false; }
        aiming = false;
        SetAimVisible(false);
        if (BobbleAnimator.Current != null) BobbleAnimator.Current.SetLoad(0f); // yeni top: figür dinlensin

        // Bonus toplar: oyuncu topu görünce o atışa daha çok özenir - ritmi bozmayan heyecan.
        //  - SON TOP: oyunun son atışı kırmızı-sıcak parlar, 2x puan (final draması).
        //  - ALTIN TOP: her GoldenEvery. atış altın parlar, 2x puan.
        var gm = GameManager.Instance;
        int mult = 1; Color glow = Color.clear; string word = null, num = null;
        if (gm != null && gm.CurrentState == GameManager.State.Playing && gm.BallsLeft > 0)
        {
            if (gm.BallsLeft == 1)
            {
                mult = GameConfig.LastBallMult; glow = new Color(1f, 0.35f, 0.15f);
                word = "sontop"; num = "x" + mult;
            }
            else
            {
                // Şanslı özelliği: altın top daha sık gelir (3 atışta bir; normalde 4).
                int every = PlayerData.HasTrait(PlayerData.Trait.Lucky)
                    ? GameConfig.LuckyGoldenEvery : GameConfig.GoldenEvery;
                // ALTIN topu özelliği: altın top 3 atışta bir (Şanslı ile aynı; birlikte de 3).
                if (BallSkins.CurrentPerk == BallSkins.Perk.GoldenOften)
                    every = Mathf.Min(every, GameConfig.LuckyGoldenEvery);
                if ((gm.ShotsTaken + 1) % every == 0)
                {
                    mult = GameConfig.GoldenMult; glow = new Color(1f, 0.8f, 0.15f);
                    word = "altintop"; // altın top duyurusu (sayı yok)
                }
            }
        }
        if (ball != null) ball.SetBonus(mult, glow);
        if (word != null)
            SpritePopup.Spawn((Vector3)ballHome + Vector3.up * 1.3f, word, num);
    }

    void Update()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Paused) return; // duraklatmada nişan/atış girişi alınmaz
        bool playing = GameManager.Instance.CurrentState == GameManager.State.Playing;

        // Top sadece oynanış sırasında görünür.
        if (ballSr != null) ballSr.enabled = playing;
        UpdateShadow(playing);
        // Bonus parlaması: yalnızca bonuslu (altın/son) topta görünür, topun rengiyle parlar;
        // topun kaybolma animasyonuna (alpha) eşlik eder.
        if (goldGlowSr != null)
        {
            bool show = playing && ball != null && ball.BonusMult > 1;
            goldGlowSr.enabled = show;
            if (show)
            {
                var c = ball.BonusGlow;
                c.a = 0.6f * (ballSr != null ? ballSr.color.a : 1f);
                goldGlowSr.color = c;
            }
        }

        if (!playing || ball == null || ball.IsLive)
        {
            SetAimVisible(false);
            return;
        }

        // UI üzerindeki tıklamalar (figür seçimi, butonlar) nişan başlatmasın -
        // yoksa seçim tıklaması bir atış hakkı yiyip topu rastgele fırlatıyordu.
        if (Input.GetMouseButtonDown(0)
            && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
        {
            aiming = true;
            aimStartFrame = Time.frameCount;
            // GÖRELİ SÜRÜKLEME: referans, parmağın ilk dokunduğu nokta - ekranın
            // neresinden başlarsan başla kontrol aynı hisseder (mobil için kritik).
            Vector3 w = cam.ScreenToWorldPoint(Input.mousePosition);
            dragStartWorld = new Vector2(w.x, w.y);
            smoothPull = Vector2.zero;
            ball.BobPaused = true; // nişan alırken top zıplamayı bıraksın (sapan merkezi sabit kalsın)
            if (BobbleAnimator.Current != null) BobbleAnimator.Current.SetLoad(0f);
        }

        if (aiming && Input.GetMouseButton(0))
        {
            // Parmak titremesi filtresi: çekiş vektörü yumuşatılarak takip edilir.
            Vector2 raw = ComputeRawPull();
            float k = 1f - Mathf.Exp(-GameConfig.AimSmoothing * Time.deltaTime);
            smoothPull = Vector2.Lerp(smoothPull, raw, k);
            Vector2 v = PullToVelocity(smoothPull, out bool valid);
            // Figür atış hareketi: çekiş gücü kadar "yüklenir" (çömel + geriye yaslan).
            if (BobbleAnimator.Current != null)
                BobbleAnimator.Current.SetLoad(smoothPull.magnitude / GameConfig.MaxDrag);
            if (valid) { SetAimVisible(true); DrawTrajectory(ball.transform.position, v); }
            else SetAimVisible(false);
        }

        if (aiming && Input.GetMouseButtonUp(0) && Time.frameCount > aimStartFrame)
        {
            aiming = false;
            SetAimVisible(false);
            ball.BobPaused = false; // atış iptal olsa da zıplama devam etsin
            Vector2 v = PullToVelocity(smoothPull, out bool valid);
            if (valid) FireBall(v);
            else if (BobbleAnimator.Current != null) BobbleAnimator.Current.SetLoad(0f); // iptal -> yükü bırak
        }
    }

    /// <summary>Ham çekiş: ilk dokunuş noktasından şu anki parmak konumuna (sapan mantığı, göreli).</summary>
    Vector2 ComputeRawPull()
    {
        Vector3 m = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 pull = dragStartWorld - new Vector2(m.x, m.y); // geri çek -> ileri fırlar
        if (pull.magnitude > GameConfig.MaxDrag) pull = pull.normalized * GameConfig.MaxDrag;
        return pull;
    }

    /// <summary>Çekiş vektörünü atış hızına çevirir (Güçlü Kol özelliği dahil).</summary>
    Vector2 PullToVelocity(Vector2 pull, out bool valid)
    {
        // Güçlü Kol özelliği: aynı sürüklemeyle daha hızlı atış (üst sınır da aynı oranda artar).
        // (Asiller tüm yeteneklere sahip - HasTrait bunu kapsar.)
        float traitMult = PlayerData.HasTrait(PlayerData.Trait.Power) ? GameConfig.PowerTraitBoost : 1f;
        // MOR topu özelliği: atış gücü +%5 (karakter özelliğiyle birleşebilir).
        if (BallSkins.CurrentPerk == BallSkins.Perk.PowerBoost) traitMult *= 1.05f;
        float maxSpeed = GameConfig.MaxShotSpeed * traitMult;

        valid = pull.magnitude >= GameConfig.MinDrag;
        Vector2 v = pull * (maxSpeed / GameConfig.MaxDrag) * GameConfig.ShotPower;
        if (v.magnitude > maxSpeed) v = v.normalized * maxSpeed;
        return v;
    }

    void FireBall(Vector2 v)
    {
        if (trail != null) trail.emitting = GameManager.Instance.Combo >= GameConfig.FireCombo;
        Sfx.Play(Sfx.Id.Shot);
        if (BobbleAnimator.Current != null) BobbleAnimator.Current.Shoot(); // figür şutu çıkarsın
        ball.Launch(v);
        GameManager.Instance.RegisterShot();
    }

    void OnBallResolved(Ball b, bool scored)
    {
        if (!scored) GameManager.Instance.Miss();
        GameManager.Instance.OnShotResolved(); // gerekirse oyunu bitirir

        if (GameManager.Instance.CurrentState == GameManager.State.Playing)
            Invoke(nameof(PrepareForPlay), 0.4f); // kısa bekleme sonrası yeni top
        else if (trail != null)
            trail.emitting = false; // oyun bittiyse yerde yatan topun izi kalmasın
    }

    // --- Noktalı nişan yayı (Sprites/dot + arrow varsa) ---
    void CreateAimDots()
    {
        var dotSpr = Resources.Load<Sprite>("Sprites/dot");
        if (dotSpr == null) return; // görsel yok -> LineRenderer yedeği kullanılır

        aimRoot = new GameObject("AimDots").transform;
        const int n = 12;
        aimDots = new SpriteRenderer[n];
        for (int i = 0; i < n; i++)
        {
            var go = new GameObject("Dot" + i);
            go.transform.SetParent(aimRoot, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = dotSpr;
            sr.sortingOrder = 7;
            aimDots[i] = sr;
        }
        var arrowSpr = Resources.Load<Sprite>("Sprites/arrow");
        if (arrowSpr != null)
        {
            var ago = new GameObject("AimArrow");
            ago.transform.SetParent(aimRoot, false);
            aimArrow = ago.AddComponent<SpriteRenderer>();
            aimArrow.sprite = arrowSpr;
            aimArrow.sortingOrder = 7;
        }
        aimRoot.gameObject.SetActive(false);
    }

    /// <summary>Nişan göstergesini aç/kapat (noktalı yay varsa onu, yoksa çizgiyi yönetir).</summary>
    void SetAimVisible(bool on)
    {
        if (aimRoot != null)
        {
            aimRoot.gameObject.SetActive(on);
            if (traj != null) traj.enabled = false;
        }
        else if (traj != null) traj.enabled = on;
    }

    // --- Yörünge çizgisi ---
    LineRenderer CreateTrajectory()
    {
        var go = new GameObject("Trajectory");
        var lr = go.AddComponent<LineRenderer>();
        lr.widthMultiplier = 0.08f;
        lr.numCornerVertices = 2;
        lr.numCapVertices = 4; // çizgi uçları yuvarlak (keskin kesik yerine yumuşak bitiş)
        lr.startColor = new Color(1f, 1f, 1f, 0.9f);
        lr.endColor = new Color(1f, 1f, 1f, 0.1f);
        lr.sortingOrder = 7;
        lr.useWorldSpace = true;
        lr.enabled = false;
        return lr;
    }

    void DrawTrajectory(Vector2 start, Vector2 v)
    {
        // Güç geri bildirimi: renk atış gücüyle beyaz -> altın -> kırmızı kayar.
        // Oyuncu "ne kadar sert" attığını fare mesafesine bakmadan yaydan okur.
        float power = Mathf.Clamp01(v.magnitude / GameConfig.MaxShotSpeed);
        Color pc = power < 0.6f
            ? Color.Lerp(Color.white, new Color(1f, 0.85f, 0.25f), power / 0.6f)
            : Color.Lerp(new Color(1f, 0.85f, 0.25f), new Color(1f, 0.30f, 0.15f), (power - 0.6f) / 0.4f);

        float g = Physics2D.gravity.y * GameConfig.BallGravity;

        // Yay, uçuşun tamamına yakınını göstermeli: kısaltınca oyuncu yayın BİTTİĞİ yeri
        // düşüş noktası sanıyor ve her atışı aşırtıyor (video kaydında görüldü).
        if (aimDots != null)
        {
            // Noktalı yay: küçülen ve solan parlak toplar + uçta yön oku.
            // Keskin Nişancı özelliği: yay daha uzağı gösterir (noktalar seyrekleşir, menzil uzar).
            int n = aimDots.Length;
            float dt = PlayerData.HasTrait(PlayerData.Trait.Sniper)
                ? 0.085f * GameConfig.SniperAimStretch : 0.085f;
            // PEMBE topu özelliği: nişan yayı %15 daha uzağı gösterir (Nişancı ile birleşebilir).
            if (BallSkins.CurrentPerk == BallSkins.Perk.AimStretch) dt *= 1.15f;
            float dotBase = 0.22f / aimDots[0].sprite.bounds.size.y;
            for (int i = 0; i < n; i++)
            {
                float t = (i + 1) * dt;
                float k = (float)i / (n - 1);
                aimDots[i].transform.position = new Vector3(
                    start.x + v.x * t,
                    start.y + v.y * t + 0.5f * g * t * t, 0f);
                aimDots[i].transform.localScale = Vector3.one * (dotBase * Mathf.Lerp(1.05f, 0.55f, k));
                var c = pc; c.a = Mathf.Lerp(0.95f, 0.30f, k);
                aimDots[i].color = c;
            }
            if (aimArrow != null)
            {
                float t = (n + 1.3f) * dt;
                Vector2 dir = new Vector2(v.x, v.y + g * t); // o andaki uçuş yönü
                aimArrow.transform.position = new Vector3(
                    start.x + v.x * t,
                    start.y + v.y * t + 0.5f * g * t * t, 0f);
                aimArrow.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                aimArrow.transform.localScale = Vector3.one * (0.45f / aimArrow.sprite.bounds.size.y);
                var ac = pc; ac.a = 0.95f;
                aimArrow.color = ac;
            }
            return;
        }

        // --- Yedek: düz çizgi (dot görseli yoksa) ---
        traj.startColor = new Color(pc.r, pc.g, pc.b, 0.9f);
        traj.endColor = new Color(pc.r, pc.g, pc.b, 0.12f);
        int nn = 26;
        traj.positionCount = nn;
        float dtl = 0.05f;
        for (int i = 0; i < nn; i++)
        {
            float t = i * dtl;
            float x = start.x + v.x * t;
            float y = start.y + v.y * t + 0.5f * g * t * t;
            traj.SetPosition(i, new Vector3(x, y, 0f));
        }
    }
}
