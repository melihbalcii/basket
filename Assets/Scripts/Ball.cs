using System;
using UnityEngine;

/// <summary>
/// Basketbol topu. İki durumu var:
///  - Hold: elde, hareketsiz bekler (kinematik).
///  - Launch: atılır, fizik devreye girer.
/// Ekrandan çıkınca veya durunca kendini "sonuçlandırır" ve atıcıya haber verir.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class Ball : MonoBehaviour
{
    public event Action<Ball, bool> OnResolved; // (top, sayıOldu)

    Rigidbody2D rb;
    SpriteRenderer sr;
    bool live, resolved, scored;
    bool netSlow; // sayı sonrası "file içinde süzülme" modu aktif mi
    Vector3 holdPos;
    Vector3 baseScale;
    float squashT;   // çarpışma sonrası "squash & stretch" esneme süresi
    float squashAmp; // esneme şiddeti (çarpma hızına göre; gerçek top gibi en fazla ~%6)
    float bobT;
    float liveT; // atış başladığından beri geçen süre (takılma güvenlik ağı için)

    public bool IsLive => live;
    /// <summary>Nişan alınırken true: dribble zıplaması durur, sapan merkezi ve yörünge titremez.</summary>
    public bool BobPaused { get; set; }
    /// <summary>Atış boyunca bir engele (çember, tahta veya yer) değdi mi (SWISH bonusu için).</summary>
    public bool TouchedObstacle { get; private set; }
    /// <summary>Bu atışta sayı yazıldı mı (Hoop, aynı atışa iki kez sayı yazmamak için bakar).</summary>
    public bool IsScored => scored;
    /// <summary>Bu atışın puan çarpanı (1 = normal; altın top / son top 2x). Atıcı her atış öncesi belirler.</summary>
    public int BonusMult { get; private set; } = 1;
    /// <summary>Bonuslu topun parlaklık rengi (altın=sarı, son top=kırmızı-sıcak).</summary>
    public Color BonusGlow { get; private set; }

    public void SetBonus(int mult, Color glow) { BonusMult = mult; BonusGlow = glow; }

    public void Init()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale; // FitToHeight sonrası ölçek (esneme buna göre)
    }

    public void Hold(Vector3 pos)
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        resolved = false; scored = false; live = false;
        netSlow = false;
        TouchedObstacle = false;
        BobPaused = false;
        holdPos = pos;
        bobT = UnityEngine.Random.value * 10f;
        transform.position = pos;
        transform.rotation = Quaternion.identity;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        // Sonuçlanırken şeffaflaşan top ele dönünce tekrar tam görünür olur.
        if (sr != null) { var c = sr.color; c.a = 1f; sr.color = c; }
    }

    public void Launch(Vector2 velocity)
    {
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = GameConfig.BallGravity;
        rb.linearVelocity = velocity;
        rb.angularVelocity = -velocity.x * 30f; // dönme efekti
        liveT = 0f;
        live = true;
    }

    public void MarkScored()
    {
        scored = true;
        netSlow = true;
        // "File hissi": top yavaşlar ve düşüşü kısa süre yumuşar; görsel olarak file
        // iplerinin arkasından geçmesini potadaki "NetFront" katmanı sağlar.
        // Fileden çıkınca (Update) yerçekimi normale döner ve top doğal şekilde yere düşer.
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.25f, Mathf.Clamp(rb.linearVelocity.y, -3.5f, -1.5f));
        rb.angularVelocity *= 0.3f;
        rb.gravityScale = 0.5f; // filede süzülme (Launch her atışta normale döndürür)
    }

    void Update()
    {
        // Squash & stretch: çarpınca hafifçe yassılaşıp hızla toparlanır. Gerçek basketbol
        // topu gibi: şiddet çarpma hızıyla orantılı (yavaş temas ~hiç, en sert çarpma %6).
        if (squashT > 0f)
        {
            squashT -= Time.deltaTime;
            float k = Mathf.Sin(Mathf.Clamp01(squashT / 0.09f) * Mathf.PI) * squashAmp;
            transform.localScale = new Vector3(baseScale.x * (1f + k), baseScale.y * (1f - k), 1f);
            if (squashT <= 0f) transform.localScale = baseScale;
        }

        if (!live)
        {
            // Elde beklerken hafif bir "dribble" zıplaması - sahne cansız durmasın.
            if (!resolved)
            {
                if (BobPaused)
                {
                    // Nişan alınırken top sabit: hedefleme deterministik olsun.
                    transform.position = holdPos;
                }
                else
                {
                    bobT += Time.deltaTime;
                    float bob = Mathf.Abs(Mathf.Sin(bobT * 2.2f)) * 0.06f;
                    transform.position = holdPos + new Vector3(0f, bob, 0f);
                }
            }
            else if (sr != null)
            {
                // Sonuçlanan top ele ışınlanmadan önce yumuşakça kaybolur;
                // yoksa havada/yerdeyken bir anda ele zıplamış gibi görünüyor.
                var c = sr.color;
                c.a = Mathf.MoveTowards(c.a, 0f, Time.deltaTime * 4f);
                sr.color = c;
            }
            return;
        }

        // Güvenlik ağı: top nadiren çemberin/tahtanın üstünde dengede takılı kalabilir;
        // hiçbir sonuçlanma koşulu gerçekleşmez ve yeni top asla gelmez. Süre dolunca zorla sonuçlandır.
        liveT += Time.deltaTime;
        if (liveT > GameConfig.ShotTimeout)
        {
            Resolve();
            return;
        }

        Vector3 p = transform.position;
        float rimY = GameConfig.HoopPos.y + GameConfig.RimYOffset;

        // Sayı olan top fileyi geçti -> "file süzülmesi" biter, yerçekimi normale döner,
        // top doğal bir hızla yere doğru düşmeye devam eder (buharlaşma potada olmaz).
        if (scored && netSlow && p.y < rimY - GameConfig.NetDepth)
        {
            netSlow = false;
            rb.gravityScale = GameConfig.BallGravity;
        }

        // ekranın altından/yanından çıktı
        if (p.y < GameConfig.KillBottom || Mathf.Abs(p.x) > GameConfig.KillSide)
        {
            Resolve();
        }
        // sayı yaptıktan sonra potadan iyice aşağı indi (görünür düşüş tamamlandı)
        else if (scored && rb.linearVelocity.y < 0f && p.y < rimY - GameConfig.ScoreDropResolve)
        {
            Resolve();
        }
        // yere oturup durdu
        else if (rb.linearVelocity.sqrMagnitude < 0.05f && p.y < GameConfig.PlayerBase.y + 1.2f)
        {
            Resolve();
        }
    }

    void Resolve()
    {
        if (resolved) return;
        resolved = true;
        live = false;
        OnResolved?.Invoke(this, scored);
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (!live) return;

        // DİKKAT: col.gameObject, Rigidbody'li bileşik collider'larda ÇOCUĞUN değil kök objenin
        // ("Hoop") adını verir; parçanın gerçek adı için col.collider.gameObject gerekir.
        string n = col.collider.gameObject.name;
        if (n == "RimLeft" || n == "RimRight" || n == "Board" || n == "Ground")
            TouchedObstacle = true;

        // Esneme şiddeti çarpma hızından: hafif temasta belli belirsiz, sertte en çok %6.
        float impact = Mathf.Clamp01(col.relativeVelocity.magnitude / GameConfig.MaxShotSpeed);
        squashAmp = Mathf.Lerp(0.01f, 0.06f, impact);
        squashT = 0.09f; // gerçek top gibi çabuk toparlanır
        Sfx.Play(Sfx.Id.Bounce);

        // Yere değen top artık sayı olamaz: seke seke bekletmeden atışı hemen sonuçlandır.
        if (n == "Ground") Resolve();
    }
}
