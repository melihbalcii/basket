using UnityEngine;

/// <summary>
/// Sayı anında popup yazısının arkasında patlayan görsel efekt (Sprites/burst.png).
/// Hızla büyür, hafif döner ve sönerek kaybolur. Görsel yoksa sessizce hiçbir şey yapmaz.
/// </summary>
public class BurstFlash : MonoBehaviour
{
    static Sprite sprite;
    static bool loaded;

    SpriteRenderer sr;
    Vector3 baseScale;
    float life, dur;

    public static void Spawn(Vector3 pos, float worldSize = 2f)
    {
        if (!loaded) { sprite = Resources.Load<Sprite>("Sprites/burst"); loaded = true; }
        if (sprite == null) return;

        var go = new GameObject("BurstFlash");
        go.transform.position = pos;
        var b = go.AddComponent<BurstFlash>();
        b.sr = go.AddComponent<SpriteRenderer>();
        b.sr.sprite = sprite;
        b.sr.sortingOrder = 48; // skor yazısının (50) hemen altı, sahnenin üstü

        float natural = sprite.bounds.size.y;
        b.baseScale = Vector3.one * (worldSize / Mathf.Max(natural, 0.001f));
        go.transform.localScale = b.baseScale * 0.45f;
        b.dur = 0.38f;
        b.life = b.dur;
    }

    void Update()
    {
        life -= Time.deltaTime;
        float p = 1f - Mathf.Clamp01(life / dur);
        // Hızlı açılır (ease-out), sona doğru hafif büyümeye devam eder.
        transform.localScale = baseScale * Mathf.Lerp(0.45f, 1.15f, Mathf.Sin(p * Mathf.PI * 0.5f));
        transform.Rotate(0f, 0f, 40f * Time.deltaTime);
        var c = sr.color;
        c.a = 1f - p * p; // sona doğru hızlanan sönme
        sr.color = c;
        if (life <= 0f) Destroy(gameObject);
    }
}
