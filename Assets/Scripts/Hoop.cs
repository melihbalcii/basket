using UnityEngine;

/// <summary>
/// Pota. Dört parçadan oluşur (hepsi kodla kurulur):
///  - Skor bölgesi: çemberin ortasındaki görünmez tetik. Top yukarıdan aşağı geçerse sayı.
///  - İki çember ucu (sol/sağ): topun sekebileceği katı engeller.
///  - Tahta (backboard): topun banka atış için sekebileceği katı düzlem.
/// Skor arttıkça pota yatay hareket etmeye başlar (zorluk).
/// </summary>
public class Hoop : MonoBehaviour
{
    public bool moving = false;
    public float moveAmp = 1.6f;
    public float moveSpeed = 1.2f;
    float baseX;
    Rigidbody2D rb;
    float popT;         // sayı sonrası "file esnedi" nabzının kalan süresi
    Vector3 baseScale;

    public void Init()
    {
        baseX = GameConfig.HoopPos.x;
        baseScale = transform.localScale;

        // KRİTİK ÖLÇEK TELAFİSİ: Pota kökü, görseli istenen boya getirmek için ölçeklendi
        // (FitToHeight, ~0.73). Collider boyut/konumları LOCAL uzayda olduğundan ebeveyn
        // ölçeğiyle çarpılıp küçülür; config'deki değerler ise DÜNYA birimidir. Telafi
        // edilmezse görsel çember 1.70 birimken fiziksel giriş penceresi topun çapının
        // ~%27'sine düşüyordu (oyun bu yüzden "girmiyor" hissi veriyordu). Hepsini ölçeğe böl.
        float k = (baseScale.x > 0.0001f) ? 1f / baseScale.x : 1f;

        // Kinematik gövde: pota hareket ederken tetik/çarpışma güvenli çalışsın.
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        // Skor bölgesi (tetik) - bu objenin üzerinde.
        var sz = GetComponent<BoxCollider2D>();
        if (sz == null) sz = gameObject.AddComponent<BoxCollider2D>();
        sz.isTrigger = true;
        sz.size = new Vector2(GameConfig.RimGap * 0.7f, GameConfig.ScoreTriggerHeight) * k;
        sz.offset = new Vector2(0f, (GameConfig.RimYOffset - 0.05f) * k); // skor çizgisini görseldeki çembere indir

        // Çember uçları (katı çarpışma) - çocuk objeler. (Yeni pota görselinin çemberi zaten görünür.)
        MakeRim("RimLeft", -GameConfig.RimGap * 0.5f * k, k);
        MakeRim("RimRight", GameConfig.RimGap * 0.5f * k, k);

        // TAHTA/TAMPON COLLIDER'I YOK - BİLİNÇLİ OLARAK.
        // Pota görseli önden bakışlı bir dekor; üzerinde görünmez herhangi bir katı yüzey
        // (duvar, tampon, kutu...) oyuncuya "görünmez engel" olarak yansıyor ve top orada
        // takılı/asılı görünüyordu (3 kez şikayet edildi). Artık çarpılabilir TEK şey
        // görünen iki çember ucu: gördüğün neyse fizik de o. Uzun atışlar potanın
        // arkasından/önünden serbestçe geçip düşer - doğal ıska.

        // Skor değişince zorluğu güncelle.
        if (GameManager.Instance != null)
            GameManager.Instance.OnChanged += () => SetDifficulty(GameManager.Instance.Score);
    }

    void MakeRim(string n, float dx, float k)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(dx, GameConfig.RimYOffset * k, 0f);
        // DAİRE collider (kutu değil): kutunun düz üst rafında top dengede kalabiliyordu.
        // Dairede duramaz; ayrıca kenardan sıyıran toplar gerçek çember gibi teğet seker.
        var c = go.AddComponent<CircleCollider2D>();
        c.radius = GameConfig.RimThickness * 0.5f * k;
        c.sharedMaterial = new PhysicsMaterial2D("rim") { bounciness = 0.28f, friction = 0.1f };
    }

    void Update()
    {
        // Sayı sonrası küçük ölçek nabzı: file topu karşılamış gibi esner (görsel geri bildirim).
        if (popT > 0f)
        {
            popT -= Time.deltaTime;
            float k = 1f + Mathf.Sin(Mathf.Clamp01(1f - popT / 0.25f) * Mathf.PI) * 0.06f;
            transform.localScale = baseScale * k;
            if (popT <= 0f) transform.localScale = baseScale;
        }
    }

    // Fizikle etkileşen kinematik gövde transform yerine MovePosition ile ve FixedUpdate'te
    // taşınmalı; yoksa collider'lar "ışınlanır" ve topla temaslar tutarsız olabilir.
    void FixedUpdate()
    {
        if (rb == null) return;
        float targetX = moving
            ? baseX + Mathf.Sin(Time.time * moveSpeed) * moveAmp
            // Hareket kapaliyken merkeze geri süzül - yeni oyun kayık potayla başlamasın.
            : Mathf.MoveTowards(rb.position.x, baseX, 8f * Time.fixedDeltaTime);
        if (Mathf.Approximately(targetX, rb.position.x)) return;
        rb.MovePosition(new Vector2(targetX, rb.position.y));
    }

    // NEDEN OnTriggerStay (OnTriggerEnter DEĞİL): Enter, topun ALT KENARI tetiğe değer değmez
    // ateşlenir - top daha çemberin yarım top boyu ÜSTÜNDEYKEN sayı yazılıyor, kutlama top
    // girmeden patlıyor ve "basket olduğu görünmüyor" hissi doğuyordu. Stay + merkez kontrolü
    // ile sayı, topun MERKEZİ çember düzleminin altına indiğinde (top gözle görülür şekilde
    // içinden geçmişken) yazılır.
    void OnTriggerStay2D(Collider2D other)
    {
        var ball = other.GetComponent<Ball>();
        if (ball == null || !ball.IsLive) return;
        if (ball.IsScored) return; // aynı atışa ikinci kez sayı yazma

        var ballRb = other.attachedRigidbody;
        if (ballRb == null || ballRb.linearVelocity.y >= 0f) return; // sadece aşağı inerken

        // Topun merkezi hem çember düzleminin altında hem de açıklığın İÇİNDE olmalı
        // (kenardan sıyırıp geçen top tetiğin köşesine değse bile sayı olmasın).
        float rimWorldY = transform.position.y + GameConfig.RimYOffset;
        if (ballRb.position.y > rimWorldY - GameConfig.ScorePlaneDepth) return;
        if (Mathf.Abs(ballRb.position.x - transform.position.x) > GameConfig.RimGap * 0.5f - 0.1f) return;

        ball.MarkScored();
        bool clean = !ball.TouchedObstacle; // hiçbir yere değmeden temiz girdi mi -> SWISH bonusu
        int basePoints = clean ? GameConfig.SwishPoints : GameConfig.NormalPoints;
        basePoints *= ball.BonusMult; // altın top / son top çarpanı
        if (GameManager.Instance != null)
            GameManager.Instance.AddScore(basePoints, transform.position + Vector3.up * GameConfig.RimYOffset, clean);
        // NOT: Çarpan (×2) zaten yukarıdaki "+N" puanının İÇİNDE. Burada ayrıca "×2" göstermek
        // oyuncuya "N'yi bir daha 2 ile çarp" izlenimi verip puanı eksik sanmasına yol açıyordu.
        // Bu yüzden sadece "BONUS" kelimesini gösteriyoruz (sayı yok) - gösterilen = kazanılan.
        if (ball.BonusMult > 1)
            SpritePopup.Spawn(transform.position + Vector3.up * (GameConfig.RimYOffset + 1.2f), "bonus", null);
        // Sinematik an: temiz (SWISH) veya bonuslu sayılarda kısa ağır çekim.
        if (clean || ball.BonusMult > 1)
            SlowMo.Trigger();
        popT = 0.25f; // file nabzını başlat
    }

    public void SetDifficulty(int score)
    {
        moving = score >= 6;
        // Dikey kurguda ekran dar: salınım küçük tutulur ki pota kenardan taşmasın
        // (MinHalfWidth 4.3; pota merkez 1.55 + amp 0.9 + sprite yarısı 1.82 = 4.27).
        moveAmp = Mathf.Min(0.5f + score * 0.03f, 0.9f);
        moveSpeed = Mathf.Min(0.9f + score * 0.03f, 2.0f);
        // BUZ topu özelliği: pota %12 daha yavaş ve daha dar salınır.
        if (BallSkins.CurrentPerk == BallSkins.Perk.SlowHoop)
        {
            moveAmp *= 0.88f;
            moveSpeed *= 0.88f;
        }
    }
}
