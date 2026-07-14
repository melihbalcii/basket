using System;
using UnityEngine;

/// <summary>
/// Oyunun beyni: skor, atış hakkı, kombo ve oyun durumu (seçim / oynanış / bitiş).
/// Diğer scriptler buraya GameManager.Instance ile ulaşır.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    /// <summary>Oyun duraklatıldı mı (UIController yönetir; BallShooter girişleri, SlowMo zamanı buna bakar).</summary>
    public static bool Paused;

    public enum State { Select, Playing, GameOver }
    public State CurrentState { get; private set; } = State.Select;

    public enum Mode { Classic, Timed }
    /// <summary>Seçim ekranındaki mod tercihi (UI yazar; StartGame bunu okur).</summary>
    public static Mode NextMode = Mode.Classic;
    public Mode CurrentMode { get; private set; } = Mode.Classic;
    /// <summary>Zaman modunda kalan süre (saniye).</summary>
    public float TimeLeft { get; private set; }

    public int Score { get; private set; }
    public int Combo { get; private set; }
    int highClassic, highTimed;
    /// <summary>Aktif modun rekoru.</summary>
    public int High => CurrentMode == Mode.Timed ? highTimed : highClassic;
    public int ShotsTaken { get; private set; }
    public int BallsTotal { get; private set; } = GameConfig.StartingBalls;
    // Zaman modunda top sınırsız: "son top" gibi hak bazlı mantıklar hiç tetiklenmez.
    public int BallsLeft => CurrentMode == Mode.Timed ? 99 : Mathf.Max(0, BallsTotal - ShotsTaken);
    /// <summary>Bu oyunda rekor kırıldı mı (oyun sonu ekranı kutlama için bakar).</summary>
    public bool NewRecord { get; private set; }

    // --- Kariyer istatistikleri (kalıcı; oyuncuya uzun vadeli ilerleme hissi verir) ---
    public int CareerScore { get; private set; }  // tüm zamanların toplam puanı
    public int CareerGames { get; private set; }  // oynanan toplam oyun
    public int CareerSwish { get; private set; }  // toplam temiz (swish) sayı
    public int BestCombo { get; private set; }    // tüm zamanların en iyi kombosu
    /// <summary>Üst üste kaç gündür oynuyor (günlük seri; her günün ilk oyununda güncellenir).</summary>
    public int DayStreak { get; private set; }
    /// <summary>Bu oyunda kazanılan coin (oyun sonu ekranı gösterir).</summary>
    public int LastCoinsEarned { get; private set; }
    /// <summary>Bu oyundaki basket sayısı (ZEHİR topunun +1 coin/basket özelliği için).</summary>
    public int BasketsThisGame { get; private set; }
    bool comboShieldUsed; // GECE topunun kalkanı bu oyunda kullanıldı mı

    // Olaylar (UI ve efektler bunları dinler)
    public event Action OnChanged;             // skor/hak/kombo değişti -> HUD yenile
    public event Action<int, Vector3, bool> OnScored; // (puan, dünya konumu, temiz/swish mi) -> efektler
    public event Action OnStateChanged;        // durum değişti -> panelleri değiştir
    public event Action OnComboShielded;       // GECE kalkanı bir ıskayı affetti -> bildirim göster

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Paused = false;
        Time.timeScale = 1f; // önceki oturumdan kalmış duraklatma/ağır çekim temizliği
        highClassic = PlayerPrefs.GetInt("bobble_high", 0);
        highTimed = PlayerPrefs.GetInt("bobble_high_timed", 0);
        CareerScore = PlayerPrefs.GetInt("bobble_cscore", 0);
        CareerGames = PlayerPrefs.GetInt("bobble_cgames", 0);
        CareerSwish = PlayerPrefs.GetInt("bobble_cswish", 0);
        BestCombo = PlayerPrefs.GetInt("bobble_bcombo", 0);
        DayStreak = PlayerPrefs.GetInt("bobble_streak", 0);
        // Seri koptuysa (son oyun dünden eskiyse) ekranda eski seriyi gösterme.
        int today = TodayStamp(0), yest = TodayStamp(-1);
        int last = PlayerPrefs.GetInt("bobble_lastday", 0);
        if (last != today && last != yest) DayStreak = 0;
    }

    static int TodayStamp(int addDays)
        => int.Parse(DateTime.Now.AddDays(addDays).ToString("yyyyMMdd"));

    void Update()
    {
        // Zaman modu geri sayımı (duraklatmada timeScale=0 olduğundan kendiliğinden durur).
        if (CurrentState == State.Playing && CurrentMode == Mode.Timed)
        {
            TimeLeft -= Time.deltaTime;
            if (TimeLeft <= 0f)
            {
                TimeLeft = 0f;
                EndGame();
            }
        }
    }

    public void StartGame()
    {
        Score = 0; Combo = 0; ShotsTaken = 0;
        NewRecord = false;
        BasketsThisGame = 0;
        comboShieldUsed = false;
        CurrentMode = NextMode;
        TimeLeft = GameConfig.TimedDuration;
        // GALAKSİ topu özelliği: klasik modda +1 ekstra top.
        BallsTotal = GameConfig.StartingBalls
            + (BallSkins.CurrentPerk == BallSkins.Perk.ExtraBall ? 1 : 0);

        // Günlük seri: günün İLK oyununda güncellenir (dün oynadıysa +1, yoksa 1'den başlar).
        int today = TodayStamp(0);
        int last = PlayerPrefs.GetInt("bobble_lastday", 0);
        if (last != today)
        {
            DayStreak = (last == TodayStamp(-1)) ? DayStreak + 1 : 1;
            PlayerPrefs.SetInt("bobble_lastday", today);
            PlayerPrefs.SetInt("bobble_streak", DayStreak);
            PlayerPrefs.Save();
        }

        SetState(State.Playing);
        OnChanged?.Invoke();
    }

    /// <summary>Atış yapıldığında çağrılır (hak düşer).</summary>
    public void RegisterShot()
    {
        ShotsTaken++;
        OnChanged?.Invoke();
    }

    /// <summary>Sayı olduğunda çağrılır.</summary>
    public void AddScore(int basePoints, Vector3 worldPos, bool clean = false)
    {
        Combo++;
        // Kombo ÇARPANI: seri uzadıkça her sayı katlanarak değerlenir (x1 -> x3).
        // Çarpan basePoints'in üstüne biner; altın top / son top çarpanı basePoints'in içinde.
        float comboMult = Mathf.Min(1f + (Combo - 1) * GameConfig.ComboStepMult, GameConfig.ComboMaxMult);
        int pts = Mathf.RoundToInt(basePoints * comboMult);
        Score += pts;
        CareerScore += pts;
        if (clean) CareerSwish++;
        if (Combo > BestCombo) BestCombo = Combo;
        if (CurrentMode == Mode.Timed) TimeLeft += GameConfig.TimedScoreBonus; // sayı = ek süre
        BasketsThisGame++;
        Missions.NotifyBasket(pts, clean, Combo, Score); // günlük görev ilerlemesi
        OnScored?.Invoke(pts, worldPos, clean);
        OnChanged?.Invoke();
    }

    /// <summary>Atış ıskalandığında.</summary>
    public void Miss()
    {
        // GECE topu özelliği: oyun başına 1 ıska komboyu bozmaz (kalkan).
        if (Combo > 0 && !comboShieldUsed && BallSkins.CurrentPerk == BallSkins.Perk.ComboShield)
        {
            comboShieldUsed = true;
            OnComboShielded?.Invoke();
            OnChanged?.Invoke();
            return;
        }
        Combo = 0;
        OnChanged?.Invoke();
    }

    /// <summary>Top sonuçlandığında (sayı veya ıska) çağrılır; klasik modda haklar bittiyse oyunu bitirir.</summary>
    public void OnShotResolved()
    {
        if (CurrentMode == Mode.Classic && ShotsTaken >= BallsTotal)
            EndGame();
    }

    public void EndGame()
    {
        if (CurrentState != State.Playing) return; // çifte bitiş koruması (süre + son top aynı anda)
        NewRecord = Score > High && Score > 0;
        if (CurrentMode == Mode.Timed)
        {
            if (Score > highTimed) { highTimed = Score; PlayerPrefs.SetInt("bobble_high_timed", highTimed); }
        }
        else
        {
            if (Score > highClassic) { highClassic = Score; PlayerPrefs.SetInt("bobble_high", highClassic); }
        }
        // Coin ödülü: bu oyunun skoru kadar (kilit açmada harcanır).
        // ZEHİR topu özelliği: her basket +1 coin ekler.
        LastCoinsEarned = Score * GameConfig.CoinsPerPoint
            + (BallSkins.CurrentPerk == BallSkins.Perk.CoinPerBasket ? BasketsThisGame : 0);
        Progress.AddCoins(LastCoinsEarned);
        Missions.NotifyGameEnd(); // "N oyun bitir" görevleri

        // Kariyer istatistiklerini kalıcılaştır (oyun başına bir kez, toplu yaz).
        CareerGames++;
        PlayerPrefs.SetInt("bobble_cscore", CareerScore);
        PlayerPrefs.SetInt("bobble_cgames", CareerGames);
        PlayerPrefs.SetInt("bobble_cswish", CareerSwish);
        PlayerPrefs.SetInt("bobble_bcombo", BestCombo);
        PlayerPrefs.Save();
        SetState(State.GameOver);
        OnChanged?.Invoke();
    }

    public void GoSelect()
    {
        SetState(State.Select);
        OnChanged?.Invoke();
    }

    void SetState(State s)
    {
        CurrentState = s;
        OnStateChanged?.Invoke();
    }
}
