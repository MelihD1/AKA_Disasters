using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Dev-only keys for checking the shop upgrades and forcing disasters.
///
/// Editor only - outside the editor this component does nothing at all, so it
/// is harmless if it ends up in a build. Delete the DevTestKeys object in the
/// scene when the jam is over if you would rather not have it around.
///
/// F5 = hand out kits           F6 = own all three upgrades
/// F7 = drop all upgrades       F8 = print the shop state
/// 1  = force an earthquake     2  = force a flood     3  = force a tornado
/// </summary>
[DisallowMultipleComponent]
public class DevTestKeys : MonoBehaviour
{
    [Tooltip("Kits handed out per F5 press, so upgrades can be bought in the shop.")]
    public int kitsPerPress = 30;

    private static readonly string[] UpgradeKeys =
    {
        "Upgrade_HardHat",
        "Upgrade_Crampons",
        "Upgrade_OxygenTank"
    };

#if UNITY_EDITOR
    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (keyboard.f5Key.wasPressedThisFrame)
        {
            GameProgress.AddCurrency(kitsPerPress);
            Report("handed out " + kitsPerPress + " kits");
        }

        if (keyboard.f6Key.wasPressedThisFrame)
            GrantUpgrades();

        if (keyboard.f7Key.wasPressedThisFrame)
            ClearUpgrades();

        if (keyboard.f8Key.wasPressedThisFrame)
            Report("shop state");

        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            EarthquakeEvent quake = FindFirstObjectByType<EarthquakeEvent>();

            if (quake != null)
            {
                quake.StartEarthquakeNow();
                Report("forced an earthquake");
            }
        }

        if (keyboard.digit2Key.wasPressedThisFrame)
        {
            FloodEvent flood = FindFirstObjectByType<FloodEvent>();

            if (flood != null)
            {
                flood.BeginFloodNow();
                Report("forced a flood");
            }
        }

        if (keyboard.digit3Key.wasPressedThisFrame)
        {
            TornadoEvent tornado = FindFirstObjectByType<TornadoEvent>();

            if (tornado != null)
            {
                tornado.StartTornadoNow();
                Report("forced a tornado");
            }
        }
    }

    private void GrantUpgrades()
    {
        GameProgress.AddCurrency(100);

        GameProgress.TryBuyUpgrade("HardHat", 5);
        GameProgress.TryBuyUpgrade("Crampons", 5);
        GameProgress.TryBuyUpgrade("OxygenTank", 5);

        Report("all three upgrades owned now");
    }

    private void ClearUpgrades()
    {
        foreach (string key in UpgradeKeys)
            PlayerPrefs.SetInt(key, 0);

        PlayerPrefs.Save();

        Report("upgrades cleared");
    }

    private void Report(string label)
    {
        Debug.Log("[dev keys] " + label +
            " | currency=" + GameProgress.Currency +
            " hardHat=" + GameProgress.HasHardHat +
            " crampons=" + GameProgress.HasCrampons +
            " oxygenTank=" + GameProgress.HasOxygenTank);
    }
#endif
}
