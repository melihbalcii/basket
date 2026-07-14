using UnityEngine;

/// <summary>
/// Kısa sinematik ağır çekim: SWISH ve bonus (altın/son top) sayılarında an'ı büyütür.
/// Zamanı aniden yavaşlatır, süre biterken yumuşakça normale döndürür.
/// </summary>
public class SlowMo : MonoBehaviour
{
    static SlowMo active;
    float t, dur, scale;

    public static void Trigger(float duration = 0.45f, float timeScale = 0.40f)
    {
        if (active != null) { active.t = duration; active.dur = duration; return; } // uzat
        var go = new GameObject("SlowMo");
        active = go.AddComponent<SlowMo>();
        active.dur = duration; active.t = duration; active.scale = timeScale;
        Time.timeScale = timeScale;
        Time.fixedDeltaTime = 0.02f * timeScale; // fizik de yavaşlasın (takılma olmasın)
    }

    /// <summary>Aktif ağır çekimi anında iptal eder (duraklatma öncesi çağrılır).</summary>
    public static void Cancel()
    {
        if (active != null) Destroy(active.gameObject);
    }

    void Update()
    {
        if (GameManager.Paused) return; // duraklatmayla yarışma: zaman kontrolü UIController'da
        t -= Time.unscaledDeltaTime;
        if (t <= 0f) { Destroy(gameObject); return; }
        // Sürenin son %35'inde yumuşak geri dönüş (ani hız değişimi hissedilmesin).
        float back = Mathf.Clamp01(t / (dur * 0.35f));
        float s = Mathf.Lerp(1f, scale, back);
        Time.timeScale = s;
        Time.fixedDeltaTime = 0.02f * s;
    }

    void OnDestroy()
    {
        // Zaman normale dönsün; oyun duraklatıldıysa duraklatma korunur.
        Time.timeScale = GameManager.Paused ? 0f : 1f;
        Time.fixedDeltaTime = 0.02f;
        if (active == this) active = null;
    }
}
