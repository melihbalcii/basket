using System;
using UnityEngine;

/// <summary>
/// Günlük görevler: her gün tarihten türetilen 3 görev (herkese aynı gün aynı görevler).
/// Tamamlanınca coin ödülü ANINDA verilir; 3'ü de bitince ek bonus. PlayerPrefs'te saklanır,
/// gün değişince otomatik sıfırlanır. Backend gerektirmez.
/// </summary>
public static class Missions
{
    public enum Kind
    {
        TotalPoints, // bugün toplam N puan topla (birikimli)
        Baskets,     // bugün N basket at (birikimli)
        Swish,       // bugün N deliksiz at (birikimli)
        GameScore,   // tek oyunda N puan yap (en iyi değer)
        Combo,       // tek oyunda N kombo yap (en iyi değer)
        Games,       // N oyun bitir (birikimli)
    }

    public class Def
    {
        public Kind kind;
        public int target;
        public int reward;
        public string Text => kind switch
        {
            Kind.TotalPoints => $"Bugün toplam {target} puan topla",
            Kind.Baskets => $"Bugün {target} basket at",
            Kind.Swish => $"Bugün {target} deliksiz (SWISH) at",
            Kind.GameScore => $"Tek oyunda {target} puan yap",
            Kind.Combo => $"Tek oyunda {target} kombo yap",
            _ => $"{target} oyun bitir",
        };
    }

    public const int AllDoneBonus = 100; // 3/3 tamamlama bonusu

    // Havuz: {kind, kolay/orta/zor hedefler, hedefe göre ödüller}
    static readonly (Kind k, int[] targets, int[] rewards)[] Pool =
    {
        (Kind.TotalPoints, new[] { 40, 70, 100 }, new[] { 30, 45, 60 }),
        (Kind.Baskets,     new[] { 12, 18, 25 },  new[] { 30, 45, 60 }),
        (Kind.Swish,       new[] { 4, 7, 10 },    new[] { 35, 50, 65 }),
        (Kind.GameScore,   new[] { 40, 60, 80 },  new[] { 35, 50, 70 }),
        (Kind.Combo,       new[] { 5, 7, 9 },     new[] { 30, 45, 65 }),
        (Kind.Games,       new[] { 3, 5, 8 },     new[] { 25, 40, 55 }),
    };

    /// <summary>Görev tamamlanınca ateşlenir (index, görev, ödül). UI kutlama gösterir.</summary>
    public static event Action<int, Def, int> OnCompleted;
    /// <summary>3/3 bonusu verilince ateşlenir.</summary>
    public static event Action<int> OnAllDone;

    static Def[] today;
    static int[] prog;
    static bool[] done;
    static bool bonusGiven;
    static string loadedDate;

    static string Today() => DateTime.Now.ToString("yyyyMMdd");

    /// <summary>Bugünün görevlerini yükler/üretir (gün değiştiyse sıfırlar).</summary>
    public static void EnsureToday()
    {
        string d = Today();
        if (loadedDate == d && today != null) return;
        loadedDate = d;

        // Tarih tohumlu deterministik seçim: herkes aynı gün aynı 3 görevi görür.
        var rnd = new System.Random(int.Parse(d));
        var idx = new System.Collections.Generic.List<int>();
        while (idx.Count < 3)
        {
            int i = rnd.Next(Pool.Length);
            if (!idx.Contains(i)) idx.Add(i);
        }
        today = new Def[3]; prog = new int[3]; done = new bool[3];
        for (int i = 0; i < 3; i++)
        {
            var p = Pool[idx[i]];
            int tier = rnd.Next(p.targets.Length);
            today[i] = new Def { kind = p.k, target = p.targets[tier], reward = p.rewards[tier] };
        }

        // Kayıtlı ilerleme bugüne aitse yükle, değilse temiz başla.
        if (PlayerPrefs.GetString("vl_mis_date", "") == d)
        {
            for (int i = 0; i < 3; i++)
            {
                prog[i] = PlayerPrefs.GetInt("vl_mis_prog_" + i, 0);
                done[i] = PlayerPrefs.GetInt("vl_mis_done_" + i, 0) == 1;
            }
            bonusGiven = PlayerPrefs.GetInt("vl_mis_bonus", 0) == 1;
        }
        else
        {
            bonusGiven = false;
            Save();
        }
    }

    static void Save()
    {
        PlayerPrefs.SetString("vl_mis_date", loadedDate);
        for (int i = 0; i < 3; i++)
        {
            PlayerPrefs.SetInt("vl_mis_prog_" + i, prog[i]);
            PlayerPrefs.SetInt("vl_mis_done_" + i, done[i] ? 1 : 0);
        }
        PlayerPrefs.SetInt("vl_mis_bonus", bonusGiven ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static Def Get(int i) { EnsureToday(); return today[i]; }
    public static int ProgressOf(int i) { EnsureToday(); return Mathf.Min(prog[i], today[i].target); }
    public static bool IsDone(int i) { EnsureToday(); return done[i]; }
    public static bool AllDone { get { EnsureToday(); return done[0] && done[1] && done[2]; } }

    /// <summary>Her basket sonrası çağrılır (GameManager.AddScore).</summary>
    public static void NotifyBasket(int pts, bool clean, int combo, int gameScore)
    {
        EnsureToday();
        for (int i = 0; i < 3; i++)
        {
            if (done[i]) continue;
            switch (today[i].kind)
            {
                case Kind.TotalPoints: prog[i] += pts; break;
                case Kind.Baskets: prog[i] += 1; break;
                case Kind.Swish: if (clean) prog[i] += 1; break;
                case Kind.GameScore: prog[i] = Mathf.Max(prog[i], gameScore); break;
                case Kind.Combo: prog[i] = Mathf.Max(prog[i], combo); break;
            }
            CheckDone(i);
        }
        Save();
    }

    /// <summary>Her oyun bitişinde çağrılır (GameManager.EndGame).</summary>
    public static void NotifyGameEnd()
    {
        EnsureToday();
        for (int i = 0; i < 3; i++)
        {
            if (done[i] || today[i].kind != Kind.Games) continue;
            prog[i] += 1;
            CheckDone(i);
        }
        Save();
    }

    static void CheckDone(int i)
    {
        if (done[i] || prog[i] < today[i].target) return;
        done[i] = true;
        Progress.AddCoins(today[i].reward); // ödül anında cebe
        OnCompleted?.Invoke(i, today[i], today[i].reward);
        if (AllDone && !bonusGiven)
        {
            bonusGiven = true;
            Progress.AddCoins(AllDoneBonus);
            OnAllDone?.Invoke(AllDoneBonus);
        }
    }
}
