using UnityEngine;

/// <summary>Sayı olunca patlayan küçük renkli konfeti kareleri (oyun hissini zenginleştirir).</summary>
public class Confetti : MonoBehaviour
{
    static readonly Color[] Palette =
    {
        new Color(1f, 0.85f, 0.2f),
        new Color(1f, 0.45f, 0.15f),
        new Color(0.3f, 0.7f, 1f),
        new Color(0.9f, 0.3f, 0.5f),
        new Color(0.6f, 0.9f, 0.3f),
    };

    static Sprite dot;
    static Sprite Dot()
    {
        if (dot == null)
        {
            var tex = Texture2D.whiteTexture;
            dot = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
        return dot;
    }

    Vector2 vel;
    float life = 0.7f;
    float spin;
    SpriteRenderer sr;

    public static void Burst(Vector3 pos, int count = 14)
    {
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Confetti");
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * Random.Range(0.08f, 0.16f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = Dot();
            r.color = Palette[Random.Range(0, Palette.Length)];
            r.sortingOrder = 55;

            var c = go.AddComponent<Confetti>();
            float ang = Random.Range(20f, 160f) * Mathf.Deg2Rad;
            float speed = Random.Range(2f, 5f);
            c.vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed;
            c.spin = Random.Range(-540f, 540f);
            c.sr = r;
        }
    }

    void Update()
    {
        life -= Time.deltaTime;
        vel += Vector2.down * 9f * Time.deltaTime;
        transform.position += (Vector3)(vel * Time.deltaTime);
        transform.Rotate(0f, 0f, spin * Time.deltaTime);
        if (sr != null)
        {
            var c = sr.color;
            c.a = Mathf.Clamp01(life / 0.7f);
            sr.color = c;
        }
        if (life <= 0f) Destroy(gameObject);
    }
}
