using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Online skor tablosu istemcisi. Aynı origin'deki serverless fonksiyona (/api/leaderboard)
/// UnityWebRequest ile bağlanır. Oyuncu adı + cihaz kimliği PlayerPrefs'te tutulur.
/// Ağ yoksa/yavaşsa sessizce başarısız olur (oyun akışını bozmaz).
/// </summary>
public class Leaderboard : MonoBehaviour
{
    public static Leaderboard Instance { get; private set; }

    const string Path = "/api/leaderboard";
    // Editörde test için canlı sunucuya git; WebGL'de kendi origin'imizden servis edilir.
    const string EditorBase = "https://vinyl-league.netlify.app";

    [Serializable] public class Entry { public string id; public string name; public int score; public long ts; }
    [Serializable] public class Resp { public string mode; public int rank; public Entry[] entries; }

    // Son çekilen tablo (UI anında göstersin diye önbellek) + oyuncunun son sırası.
    public Resp Cached;
    public int MyLastRank = -1;

    public static string PlayerName
    {
        get => PlayerPrefs.GetString("vl_name", "");
        set { PlayerPrefs.SetString("vl_name", value ?? ""); PlayerPrefs.Save(); }
    }
    public static bool HasName => !string.IsNullOrWhiteSpace(PlayerName);

    public static string DeviceId
    {
        get
        {
            var id = PlayerPrefs.GetString("vl_id", "");
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString("N").Substring(0, 16);
                PlayerPrefs.SetString("vl_id", id); PlayerPrefs.Save();
            }
            return id;
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    static string Url()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        string a = Application.absoluteURL; // ör. https://site/index.html?...
        int p = a.IndexOf("://");
        int slash = p >= 0 ? a.IndexOf('/', p + 3) : -1;
        string origin = slash > 0 ? a.Substring(0, slash) : a;
        return origin + Path;
#else
        return EditorBase + Path;
#endif
    }

    public static string ModeStr(GameManager.Mode m) => m == GameManager.Mode.Timed ? "timed" : "classic";

    /// <summary>Skoru gönderir (ad ayarlıysa). Dönüşte sıra + tablo Cached'e yazılır.</summary>
    public void Submit(string mode, int score, Action<Resp> onDone = null)
    {
        if (!HasName || score <= 0) { onDone?.Invoke(null); return; }
        StartCoroutine(SubmitCo(mode, score, onDone));
    }

    [Serializable] class Body { public string id; public string name; public int score; public string mode; }

    IEnumerator SubmitCo(string mode, int score, Action<Resp> onDone)
    {
        string payload = JsonUtility.ToJson(new Body { id = DeviceId, name = PlayerName, score = score, mode = mode });
        using var req = new UnityWebRequest(Url(), "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 12;
        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success)
        {
            var r = JsonUtility.FromJson<Resp>(req.downloadHandler.text);
            Cached = r; MyLastRank = r != null ? r.rank : -1;
            onDone?.Invoke(r);
        }
        else onDone?.Invoke(null);
    }

    /// <summary>Tabloyu çeker (ilk 50). Sonuç Cached'e yazılır.</summary>
    public void Fetch(string mode, Action<Resp> onDone = null)
    {
        StartCoroutine(FetchCo(mode, onDone));
    }

    IEnumerator FetchCo(string mode, Action<Resp> onDone)
    {
        using var req = UnityWebRequest.Get(Url() + "?mode=" + mode);
        req.timeout = 12;
        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success)
        {
            var r = JsonUtility.FromJson<Resp>(req.downloadHandler.text);
            Cached = r;
            onDone?.Invoke(r);
        }
        else onDone?.Invoke(null);
    }
}
