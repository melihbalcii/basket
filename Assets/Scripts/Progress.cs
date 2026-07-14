using UnityEngine;

/// <summary>
/// Kalıcı ilerleme: coin bakiyesi ve açılmış karakterler.
/// Coin, her oyun sonunda skora göre kazanılır; kilitli karakterleri açmak için harcanır.
/// Tümü PlayerPrefs'te saklanır (cihazda kalıcı).
/// </summary>
public static class Progress
{
    public static int Coins { get; private set; }

    static bool[] unlocked;
    static bool loaded;

    static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        Coins = PlayerPrefs.GetInt("vl_coins", 0);
        unlocked = new bool[PlayerData.FigureCount];
        for (int i = 0; i < unlocked.Length; i++)
            unlocked[i] = CostOf(i) == 0 || PlayerPrefs.GetInt("vl_unlock_" + i, 0) == 1;
    }

    /// <summary>Karakterin açılma bedeli (0 = baştan açık/bedava).</summary>
    public static int CostOf(int figIndex)
    {
        var costs = GameConfig.UnlockCosts;
        return (figIndex >= 0 && figIndex < costs.Length) ? costs[figIndex] : 0;
    }

    public static bool IsUnlocked(int figIndex)
    {
        EnsureLoaded();
        return figIndex >= 0 && figIndex < unlocked.Length && unlocked[figIndex];
    }

    public static void AddCoins(int amount)
    {
        EnsureLoaded();
        if (amount <= 0) return;
        Coins += amount;
        PlayerPrefs.SetInt("vl_coins", Coins);
        PlayerPrefs.Save();
    }

    /// <summary>Yeterli coin varsa karakteri açar ve coin düşer. Başarılıysa true.</summary>
    public static bool TryUnlock(int figIndex)
    {
        EnsureLoaded();
        if (IsUnlocked(figIndex)) return true;
        int cost = CostOf(figIndex);
        if (Coins < cost) return false;
        Coins -= cost;
        unlocked[figIndex] = true;
        PlayerPrefs.SetInt("vl_coins", Coins);
        PlayerPrefs.SetInt("vl_unlock_" + figIndex, 1);
        PlayerPrefs.Save();
        return true;
    }
}
