using UnityEngine;

/// <summary>
/// Figürü hafifçe sallar (bobblehead hissi) VE gerçek bir atış hareketi oynatır.
/// Bu script figürün KÖK objesindedir; görsel ise kökün biraz yukarısında bir çocuktur,
/// böylece sallanma/eğilme tabandan (ayaktan) olur.
///
/// Atış akışı (BallShooter bunları tetikler):
///   - Nişan alırken  -> SetLoad(0..1): figür çömelir ve potanın TERSİNE hafif yaslanır (yük toplar).
///   - Bırakınca      -> Shoot(): patlayıcı yukarı itiş + potaya doğru eğilme + hop (şutu çıkarır).
///   - Sayı olunca    -> Pop(): kısa zıplama/şişme (kutlama).
/// Hepsi bobblehead sallanmasının ÜZERİNE binerek tek transformda birleşir.
/// </summary>
public class BobbleAnimator : MonoBehaviour
{
    public float swayAmp = 4f;     // sallanma açısı (derece)
    public float swayFreq = 1.8f;  // sallanma hızı

    /// <summary>Sahnedeki aktif figür (aynı anda tek figür olur). BallShooter buradan erişir.</summary>
    public static BobbleAnimator Current;

    float seed, popT;
    Vector3 baseScale, basePos;

    float loadAmt;   // hedef yük (0..1) - nişan gücü
    float loadCur;   // yumuşatılmış yük
    float shootT;    // atış animasyonunun kalan süresi (>0 iken aktif)
    const float ShootDur = 0.55f;

    // Potanın yatay yönü: figür atışta oraya doğru eğilir. +x sağ olduğu için sağa (potaya)
    // eğilmek Z ekseninde negatif dönüştür (saat yönü). Pota her zaman figürün sağında.
    float faceSign = -1f;

    void Awake()
    {
        seed = Random.value * 10f;
        baseScale = transform.localScale;
        basePos = transform.localPosition;
        faceSign = (GameConfig.HoopPos.x >= GameConfig.PlayerBase.x) ? -1f : 1f;
        Current = this;
    }

    void OnDestroy() { if (Current == this) Current = null; }

    /// <summary>Nişan gücü (0..1): figür orantılı çömelir/yaslanır.</summary>
    public void SetLoad(float a) { loadAmt = Mathf.Clamp01(a); }

    /// <summary>Atışı çıkar: yukarı itiş + potaya eğilme + hop.</summary>
    public void Shoot() { shootT = ShootDur; loadAmt = 0f; loadCur = Mathf.Min(loadCur, 0.6f); }

    public void Pop() { popT = 0.3f; }

    void Update()
    {
        // --- bobblehead sallanması (taban) ---
        float sway = Mathf.Sin(Time.time * swayFreq + seed) * swayAmp;

        // --- yük (nişan) yumuşatma ---
        loadCur = Mathf.MoveTowards(loadCur, loadAmt, Time.deltaTime * 8f);

        // --- atış itişi profili (0 -> 1 -> 0): çabuk yüksel, sonra otur ---
        float push = 0f;
        if (shootT > 0f)
        {
            shootT -= Time.deltaTime;
            float p = Mathf.Clamp01(1f - shootT / ShootDur);
            // Öne yüklenen yay: başta hızlı zirve, sonra yumuşak iniş (gerçek şut ritmi).
            push = Mathf.Sin(Mathf.Pow(p, 0.65f) * Mathf.PI);
        }

        // --- pop (sayı kutlaması) ---
        float pop = 0f;
        if (popT > 0f)
        {
            popT -= Time.deltaTime;
            pop = Mathf.Sin((1f - popT / 0.3f) * Mathf.PI) * 0.18f;
        }

        // --- pozları birleştir ---
        // Dikey: nişanda çömel (aşağı), atışta hopla (yukarı).
        float yOff = -0.18f * loadCur + 0.42f * push;

        // Ölçek: nişanda squash (basılma), atışta stretch (uzama).
        float sx = (1f + 0.05f * loadCur) * (1f - 0.06f * push);
        float sy = (1f - 0.09f * loadCur) * (1f + 0.14f * push);

        // Eğim: nişanda potanın tersine yaslan (yük), atışta potaya doğru sertçe eğil.
        float leanLoad = 7f * loadCur * -faceSign;   // yük: potadan uzağa
        float leanShot = 20f * push * faceSign;      // atış: potaya doğru
        float z = sway + leanLoad + leanShot;

        transform.localRotation = Quaternion.Euler(0f, 0f, z);
        transform.localScale = new Vector3(baseScale.x * sx, baseScale.y * sy, baseScale.z) * (1f + pop);
        transform.localPosition = basePos + new Vector3(0f, yOff, 0f);
    }
}
