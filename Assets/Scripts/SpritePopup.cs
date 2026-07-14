using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dünya-uzayında beliren kutlama popup'ı: üstte kelime-görseli (SWISH!, ALTIN TOP, ...),
/// altında glyph rakamlarla sayı ("+2", "x2"). Yükselir, hafif zıplar ve solarak kaybolur.
/// Görseller Resources/Words ve Resources/Glyphs'ten yüklenir (yoksa o parça atlanır).
/// </summary>
public class SpritePopup : MonoBehaviour
{
    const float WordHeight = 1.5f;   // kelime-görseli dünya yüksekliği
    const float GlyphHeight = 0.95f; // rakam yüksekliği
    const float Tracking = 0.05f;    // rakamlar arası boşluk

    static Dictionary<char, Sprite> glyphs;
    static Dictionary<string, Sprite> words = new Dictionary<string, Sprite>();

    static Sprite Glyph(char c)
    {
        if (glyphs == null)
        {
            glyphs = new Dictionary<char, Sprite>();
            for (int i = 0; i <= 9; i++) glyphs[(char)('0' + i)] = Res("Glyphs/num_" + i);
            glyphs['+'] = Res("Glyphs/num_plus");
            glyphs['x'] = Res("Glyphs/num_x");
        }
        return glyphs.TryGetValue(c, out var s) ? s : null;
    }

    static Sprite Word(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (!words.TryGetValue(key, out var s)) { s = Res("Words/word_" + key); words[key] = s; }
        return s;
    }

    static Sprite Res(string p) => Resources.Load<Sprite>(p);

    readonly List<SpriteRenderer> parts = new List<SpriteRenderer>();
    float life = 1.0f;
    const float MaxLife = 1.0f;

    /// <summary>word: "swish"/"altintop"/"sontop"/"bonus"/"alev"/"kombo" veya null. number: "+2"/"x3" veya null.</summary>
    public static void Spawn(Vector3 pos, string word, string number)
    {
        var go = new GameObject("SpritePopup");
        go.transform.position = pos;
        var sp = go.AddComponent<SpritePopup>();

        var wordSpr = Word(word);
        bool hasWord = wordSpr != null;
        bool hasNum = !string.IsNullOrEmpty(number);
        float wordY = (hasWord && hasNum) ? 0.55f : 0f;

        if (hasWord)
        {
            var wr = sp.AddPart(go.transform, wordSpr, 60);
            float sc = WordHeight / Mathf.Max(0.01f, wordSpr.bounds.size.y);
            wr.transform.localScale = Vector3.one * sc;
            wr.transform.localPosition = new Vector3(0f, wordY, 0f);
        }
        if (hasNum)
            sp.BuildNumber(go.transform, number, new Vector3(0f, hasWord ? wordY - WordHeight * 0.72f : 0f, 0f));
    }

    SpriteRenderer AddPart(Transform parent, Sprite s, int order)
    {
        var g = new GameObject("part");
        g.transform.SetParent(parent, false);
        var r = g.AddComponent<SpriteRenderer>();
        r.sprite = s;
        r.sortingOrder = order;
        parts.Add(r);
        return r;
    }

    void BuildNumber(Transform parent, string number, Vector3 localPos)
    {
        // Önce toplam genişliği ölç (ortalamak için).
        float total = 0f;
        var scales = new List<float>();
        var sprs = new List<Sprite>();
        foreach (char c in number)
        {
            var s = Glyph(c);
            if (s == null) continue;
            float sc = GlyphHeight / Mathf.Max(0.01f, s.bounds.size.y);
            sprs.Add(s); scales.Add(sc);
            total += s.bounds.size.x * sc + Tracking;
        }
        if (sprs.Count == 0) return;
        total -= Tracking;

        float x = -total * 0.5f;
        int order = 62;
        for (int i = 0; i < sprs.Count; i++)
        {
            float w = sprs[i].bounds.size.x * scales[i];
            var r = AddPart(parent, sprs[i], order++);
            r.transform.localScale = Vector3.one * scales[i];
            r.transform.localPosition = localPos + new Vector3(x + w * 0.5f, 0f, 0f);
            x += w + Tracking;
        }
    }

    void Update()
    {
        life -= Time.deltaTime;
        transform.position += Vector3.up * Time.deltaTime * 1.1f;

        // Giriş zıplaması (ilk ~0.16s büyüyerek belirir).
        float age = MaxLife - life;
        float s = age < 0.16f ? Mathf.Lerp(0.4f, 1.08f, age / 0.16f)
                : age < 0.26f ? Mathf.Lerp(1.08f, 1f, (age - 0.16f) / 0.10f) : 1f;
        transform.localScale = Vector3.one * s;

        float a = Mathf.Clamp01(life / 0.4f); // son 0.4s'de sol
        for (int i = 0; i < parts.Count; i++)
        {
            if (parts[i] == null) continue;
            var c = parts[i].color; c.a = a; parts[i].color = c;
        }
        if (life <= 0f) Destroy(gameObject);
    }
}
