using UnityEngine;

/// <summary>Persistent currency and permanent shop unlocks.</summary>
public static class GameProgress
{
    private const string CurrencyKey = "EmergencyKitsCurrency";
    private const string HardHatKey = "Upgrade_HardHat";
    private const string CramponsKey = "Upgrade_Crampons";
    private const string OxygenTankKey = "Upgrade_OxygenTank";

    public static int Currency => PlayerPrefs.GetInt(CurrencyKey, 0);
    public static bool HasHardHat => PlayerPrefs.GetInt(HardHatKey, 0) == 1;
    public static bool HasCrampons => PlayerPrefs.GetInt(CramponsKey, 0) == 1;
    public static bool HasOxygenTank => PlayerPrefs.GetInt(OxygenTankKey, 0) == 1;

    public static void AddCurrency(int amount)
    {
        if (amount <= 0) return;
        PlayerPrefs.SetInt(CurrencyKey, Currency + amount);
        PlayerPrefs.Save();
    }

    public static bool TryBuyUpgrade(string upgrade, int price)
    {
        if (IsOwned(upgrade) || price < 0 || Currency < price) return false;
        PlayerPrefs.SetInt(CurrencyKey, Currency - price);
        PlayerPrefs.SetInt(GetUpgradeKey(upgrade), 1);
        PlayerPrefs.Save();
        return true;
    }

    public static bool IsOwned(string upgrade)
    {
        switch (upgrade)
        {
            case "HardHat": return HasHardHat;
            case "Crampons": return HasCrampons;
            case "OxygenTank": return HasOxygenTank;
            default: return false;
        }
    }

    private static string GetUpgradeKey(string upgrade)
    {
        switch (upgrade)
        {
            case "HardHat": return HardHatKey;
            case "Crampons": return CramponsKey;
            case "OxygenTank": return OxygenTankKey;
            default: return string.Empty;
        }
    }
}
