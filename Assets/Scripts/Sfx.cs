using UnityEngine;

/// <summary>
/// Kodla üretilen basit ses efektleri (ses dosyası gerektirmez).
/// Kullanım: Sfx.Play(Sfx.Id.Score). İlk çağrıda kendini kurar.
/// </summary>
public static class Sfx
{
    public enum Id { Shot, Bounce, Score, Swish, GameOver, Click, Cheer }

    static AudioSource src;
    static AudioSource amb; // sürekli kalabalık uğultusu (loop)
    static AudioClip[] clips;
    static float[] lastPlay;

    /// <summary>Sesler kapalı mı (ayar kalıcıdır, PlayerPrefs'te saklanır).</summary>
    public static bool Muted { get; private set; }

    public static void SetMuted(bool m)
    {
        EnsureInit();
        Muted = m;
        if (amb != null) amb.mute = m;
        PlayerPrefs.SetInt("bobble_muted", m ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void Play(Id id)
    {
        EnsureInit();
        if (Muted) return;
        int i = (int)id;
        // Aynı ses art arda çok sık tetiklenmesin (ör. top hızlı sekerken).
        if (Time.unscaledTime - lastPlay[i] < 0.06f) return;
        lastPlay[i] = Time.unscaledTime;
        src.PlayOneShot(clips[i]);
    }

    static void EnsureInit()
    {
        if (src != null) return;
        Muted = PlayerPrefs.GetInt("bobble_muted", 0) == 1; // kayıtlı ses tercihi
        var go = new GameObject("Sfx");
        src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.volume = 0.7f;
        // Sahnede ses alıcısı yoksa (kamera da koddan kurulduysa) ekle.
        if (Object.FindAnyObjectByType<AudioListener>() == null)
            go.AddComponent<AudioListener>();

        clips = new AudioClip[7];
        clips[(int)Id.Shot]     = Tone("sfx_shot",   new[] { 520f, 300f },        0.12f, 0.30f, 0.45f);
        clips[(int)Id.Bounce]   = Tone("sfx_bounce", new[] { 130f },              0.10f, 0.50f, 0.25f);
        clips[(int)Id.Score]    = Tone("sfx_score",  new[] { 659f, 880f },        0.22f, 0.45f);
        clips[(int)Id.Swish]    = Tone("sfx_swish",  new[] { 659f, 880f, 1319f }, 0.32f, 0.45f);
        clips[(int)Id.GameOver] = Tone("sfx_over",   new[] { 392f, 330f, 262f },  0.55f, 0.40f);
        clips[(int)Id.Click]    = Tone("sfx_click",  new[] { 990f },              0.06f, 0.30f);
        clips[(int)Id.Cheer]    = CrowdCheer("sfx_cheer", 0.9f, 0.5f);
        lastPlay = new float[clips.Length];
    }

    /// <summary>Arka plan kalabalık uğultusunu (loop) başlatır; bir kez kurulur. Arena hissi verir.</summary>
    public static void StartAmbience()
    {
        EnsureInit();
        if (amb != null) return;
        var go = new GameObject("SfxAmbience");
        amb = go.AddComponent<AudioSource>();
        amb.clip = CrowdLoop("sfx_amb", 4f, 0.06f);
        amb.loop = true;
        amb.playOnAwake = false;
        amb.mute = Muted;
        amb.Play();
    }

    /// <summary>Tepe genliği hedefe eşitle (filtre zinciri sesi zayıflattığı için üretim sonrası normalize edilir).</summary>
    static void Normalize(float[] d, float peak)
    {
        float m = 0f;
        for (int i = 0; i < d.Length; i++) m = Mathf.Max(m, Mathf.Abs(d[i]));
        if (m < 1e-5f) return;
        float g = peak / m;
        for (int i = 0; i < d.Length; i++) d[i] *= g;
    }

    /// <summary>
    /// Alçak, sürekli kalabalık uğultusu. "TV karlanması" gibi duyulmaması için iki şey kritik:
    ///  1) AĞIR alçak geçiren filtre (3 kademe, ~250 Hz): cızırtı yapan tizlerin tamamı kesilir,
    ///     uzaktan gelen boğuk salon uğultusu kalır.
    ///  2) Yavaş "nefes" dalgaları: gerçek kalabalık sabit değildir, kabarır ve iner. Dalga
    ///     frekansları döngü süresini tam böldüğü için loop dikişsizdir.
    /// </summary>
    static AudioClip CrowdLoop(string name, float dur, float vol)
    {
        const int rate = 44100;
        int n = (int)(rate * dur);
        var data = new float[n];
        var rnd = new System.Random(name.GetHashCode());
        float lp1 = 0f, lp2 = 0f, lp3 = 0f;
        const float k = 0.035f; // tek kutuplu filtre katsayısı (~250 Hz kesim)
        for (int i = 0; i < n; i++)
        {
            float w = (float)rnd.NextDouble() * 2f - 1f;
            lp1 += k * (w - lp1); lp2 += k * (lp1 - lp2); lp3 += k * (lp2 - lp3);
            float t = (float)i / n; // 0..1 döngü ilerlemesi
            float swell = 0.72f + 0.20f * Mathf.Sin(2f * Mathf.PI * t + 1.3f)
                                + 0.08f * Mathf.Sin(6f * Mathf.PI * t);
            data[i] = lp3 * swell;
        }
        for (int i = 0; i < 4000; i++) // filtre durumu dikişi: son ~90ms başlangıca karışır
        {
            float t = i / 4000f;
            data[n - 4000 + i] = Mathf.Lerp(data[n - 4000 + i], data[i], t);
        }
        Normalize(data, vol);
        var clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Sayı sonrası kısa kalabalık coşkusu: filtrelenmiş (boğuk) gürültü + yükselip sönen zarf.</summary>
    static AudioClip CrowdCheer(string name, float dur, float vol)
    {
        const int rate = 44100;
        int n = (int)(rate * dur);
        var data = new float[n];
        var rnd = new System.Random(name.GetHashCode());
        float lp1 = 0f, lp2 = 0f;
        const float k = 0.08f; // ~600 Hz: coşku uğultudan biraz daha parlak ama yine cızırtısız
        for (int i = 0; i < n; i++)
        {
            float w = (float)rnd.NextDouble() * 2f - 1f;
            lp1 += k * (w - lp1); lp2 += k * (lp1 - lp2);
            float p = (float)i / n;
            float env = Mathf.Sin(Mathf.Clamp01(p / 0.22f) * Mathf.PI * 0.5f) * Mathf.Exp(-2.6f * Mathf.Max(0f, p - 0.22f));
            data[i] = lp2 * env;
        }
        Normalize(data, vol);
        var clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Verilen nota dizisinden kısa bir "çip" sesi üretir (sinüs + nota başına sönüm zarfı).</summary>
    static AudioClip Tone(string name, float[] freqs, float dur, float vol, float noise = 0f)
    {
        const int rate = 44100;
        int n = Mathf.Max(1, (int)(rate * dur));
        var data = new float[n];
        var rnd = new System.Random(name.GetHashCode());
        float phase = 0f;
        int segs = freqs.Length;
        for (int i = 0; i < n; i++)
        {
            float p = (float)i / n;
            int si = Mathf.Min((int)(p * segs), segs - 1);
            float segP = p * segs - si; // 0..1 nota içi ilerleme
            // Frekans değişimlerinde çıtlama olmasın diye faz kesintisiz ilerletilir.
            phase += 2f * Mathf.PI * freqs[si] / rate;
            float env = Mathf.Clamp01(i / (rate * 0.005f)) * Mathf.Exp(-5f * segP);
            float s = Mathf.Sin(phase);
            if (noise > 0f) s = Mathf.Lerp(s, (float)(rnd.NextDouble() * 2.0 - 1.0), noise);
            data[i] = s * env * vol;
        }
        var clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
