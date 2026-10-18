using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Temporary test rig: checks that buying each shop upgrade really changes
/// what the disaster scripts do, by measuring the game before and after the
/// purchase. It puts the shop flags back the way it found them when it is done.
/// </summary>
public class UpgradeEffectTestRunner : MonoBehaviour
{
    public string reportPath;

    private static readonly string CurrencyKey = "EmergencyKitsCurrency";

    private static readonly string[] UpgradeKeys =
    {
        "Upgrade_HardHat",
        "Upgrade_Crampons",
        "Upgrade_OxygenTank"
    };

    private readonly int[] savedFlags = new int[3];
    private int savedCurrency;
    private bool restored;
    private static bool ranThisSession;

    private EarthquakeEvent quake;
    private TornadoEvent tornado;
    private FloodEvent flood;
    private PlayerMotor motor;

    private IEnumerator Start()
    {
        // The player dies at the end of the test, which can reload the scene.
        // Only run once per play session so that does not loop.
        if (ranThisSession)
        {
            Log("test already ran in this play session, skipping");
            Finish();
            yield break;
        }

        ranThisSession = true;

        quake = FindFirstObjectByType<EarthquakeEvent>();
        tornado = FindFirstObjectByType<TornadoEvent>();
        flood = FindFirstObjectByType<FloodEvent>();
        motor = FindFirstObjectByType<PlayerMotor>();

        DisasterDifficultyManager manager =
            FindFirstObjectByType<DisasterDifficultyManager>();

        if (manager != null)
        {
            // Stop its schedule for real: disabling the component is not
            // enough, its coroutines would keep starting disasters.
            manager.enabled = false;
            manager.gameObject.SetActive(false);
            Log("difficulty manager switched off for the test");
        }

        quake.ApplyDifficulty(0f);
        tornado.ApplyDifficulty(0f);
        flood.ApplyDifficulty(0f);
        Log("baseline pinned to difficulty 0");

        SavePrefs();

        yield return Run();

        RestorePrefs();
        Finish();
    }

    private IEnumerator Run()
    {
        Log("=== shop upgrade effect test ===");
        Log("scene objects: quake=" + (quake != null) +
            " tornado=" + (tornado != null) +
            " flood=" + (flood != null) +
            " motor=" + (motor != null));

        if (quake == null || tornado == null || flood == null || motor == null)
        {
            Log("ERROR: the scene is missing one of the disaster scripts.");
            yield break;
        }

        // ---------------- nothing owned ----------------
        ClearUpgrades();

        Log("");
        Log("--- with nothing bought ---");
        Log("owned: hardHat=" + GameProgress.HasHardHat +
            " crampons=" + GameProgress.HasCrampons +
            " oxygenTank=" + GameProgress.HasOxygenTank);

        LogQuake("no hard hat");

        yield return TornadoCheck("no crampons");

        Log("flood: O2 Tank owned=" + GameProgress.HasOxygenTank +
            ", drowning ignores oxygen=" + !flood.OxygenProtects +
            " (expected: not owned, so drowning kills straight away)");

        // ---------------- buy them through the shop API ----------------
        GameProgress.AddCurrency(500);

        bool boughtHat = GameProgress.TryBuyUpgrade("HardHat", 5);
        bool boughtCrampons = GameProgress.TryBuyUpgrade("Crampons", 5);
        bool boughtTank = GameProgress.TryBuyUpgrade("OxygenTank", 5);

        Log("");
        Log("--- bought through GameProgress.TryBuyUpgrade ---");
        Log("purchases accepted: hardHat=" + boughtHat +
            " crampons=" + boughtCrampons +
            " oxygenTank=" + boughtTank +
            ", currency left=" + GameProgress.Currency);

        Log("owned: hardHat=" + GameProgress.HasHardHat +
            " crampons=" + GameProgress.HasCrampons +
            " oxygenTank=" + GameProgress.HasOxygenTank);

        LogQuake("with hard hat");

        yield return TornadoCheck("with crampons");

        // ---------------- the difficulty ramp ----------------
        Log("");
        Log("--- what the values become as the run gets harder ---");

        quake.ApplyDifficulty(0f);
        Log(string.Format(
            CultureInfo.InvariantCulture,
            "earthquake at difficulty 0.0: death deadline {0:0.0}s " +
            "(with the hard hat {1:0.0}s)",
            quake.earthquakeDuration, quake.ExposedDeadline));

        quake.ApplyDifficulty(1f);
        Log(string.Format(
            CultureInfo.InvariantCulture,
            "earthquake at difficulty 1.0: death deadline {0:0.0}s " +
            "(with the hard hat {1:0.0}s)",
            quake.earthquakeDuration, quake.ExposedDeadline));

        tornado.ApplyDifficulty(0f);
        Log(string.Format(
            CultureInfo.InvariantCulture,
            "tornado at difficulty 0.0: player keeps x{0:0.00} of his speed",
            tornado.MovementSpeedMultiplier));

        tornado.ApplyDifficulty(1f);
        Log(string.Format(
            CultureInfo.InvariantCulture,
            "tornado at difficulty 1.0: player keeps x{0:0.00} of his speed",
            tornado.MovementSpeedMultiplier));

        Log("checking the crampons at the worst difficulty...");

        tornado.StartTornadoNow();
        yield return new WaitForSeconds(0.4f);

        Log(string.Format(
            CultureInfo.InvariantCulture,
            "tornado at difficulty 1.0 with crampons: slowdown applied to " +
            "the player is x{0:0.00} (motor reads x{1:0.00})",
            tornado.AppliedSlowdown, motor.TornadoSpeedMultiplier));

        tornado.StopTornadoNow();
        yield return new WaitForSeconds(0.3f);

        flood.ApplyDifficulty(0f);
        Log(string.Format(
            CultureInfo.InvariantCulture,
            "flood at difficulty 0.0: water rises {0:0.0}m at {1:0.00}/s",
            flood.waterRise, flood.riseSpeed));

        flood.ApplyDifficulty(1f);
        Log(string.Format(
            CultureInfo.InvariantCulture,
            "flood at difficulty 1.0: water rises {0:0.0}m at {1:0.00}/s",
            flood.waterRise, flood.riseSpeed));

        Log(string.Format(
            CultureInfo.InvariantCulture,
            "flood oxygen: {0:0.0}s of air, draining {1:0.0}/s",
            flood.oxygenCapacity, flood.oxygenDrainPerSecond));

        // ---------------- the O2 tank under water ----------------
        Log("");
        Log("--- O2 Tank underwater (this ends the run when the air runs out) ---");
        Log("flood: oxygen protects the player=" + flood.OxygenProtects +
            ", capacity=" + flood.oxygenCapacity +
            ", drain=" + flood.oxygenDrainPerSecond + "/s");

        Transform player = flood.playerRoot != null
            ? flood.playerRoot
            : GameObject.Find("PlayerRoot")?.transform;

        if (player != null)
        {
            Log(string.Format(
                CultureInfo.InvariantCulture,
                "player at ({0:F2}, {1:F2}, {2:F2}), floor level {3:F2}",
                player.position.x, player.position.y, player.position.z,
                flood.FloorLevel));
        }

        Log("O2 meter visible before any water: " + flood.OxygenHudVisible);

        // Put the player on the house floor: on a long play session he may
        // have died and respawned outside the house, where the water never
        // reaches him.
        PlacePlayerOnFloor();

        // Keep the water the same height for both halves of this check.
        flood.ApplyDifficulty(1f);

        yield return new WaitForSeconds(0.5f);

        flood.BeginFloodNow();

        float elapsed = 0f;
        float submergedAt = -1f;
        float oxygenAtSubmerge = -1f;
        float lostAt = -1f;
        bool sawHidden = false;
        bool sawVisible = false;

        while (elapsed < 14f && !flood.PlayerLost)
        {
            yield return new WaitForSeconds(0.25f);
            elapsed += 0.25f;

            if (!flood.HeadUnderwater && !flood.OxygenHudVisible)
                sawHidden = true;

            if (flood.HeadUnderwater)
            {
                if (submergedAt < 0f)
                {
                    submergedAt = elapsed;
                    oxygenAtSubmerge = flood.CurrentOxygen;
                }

                if (flood.OxygenHudVisible)
                    sawVisible = true;
            }

            Log(string.Format(
                CultureInfo.InvariantCulture,
                "t={0:0.00}s water={1:F2} headUnderwater={2} " +
                "O2meterVisible={3} oxygen={4:F2} lost={5} playerY={6:F2}",
                elapsed, flood.WaterLevel, flood.HeadUnderwater,
                flood.OxygenHudVisible, flood.CurrentOxygen,
                flood.PlayerLost,
                player != null ? player.position.y : -999f));

            if (flood.PlayerLost)
                lostAt = elapsed;
        }

        Log("");
        Log("meter stayed hidden while above water: " + sawHidden);
        Log("meter appeared once under water: " + sawVisible);
        Log(string.Format(
            CultureInfo.InvariantCulture,
            "went under at t={0:0.00}s with {1:F2}s of air",
            submergedAt, oxygenAtSubmerge));

        if (lostAt > 0f)
        {
            Log(string.Format(
                CultureInfo.InvariantCulture,
                "ran out of air and lost at t={0:0.00}s, i.e. {1:F2}s under water",
                lostAt, lostAt - submergedAt));

            Log("without the tank that same water would have been instant");
        }
        else
        {
            Log("player still alive at the end of the watch window");
        }

        // ---------------- prove the tank is what saves him ----------------
        Log("");
        Log("--- same water, tank taken away while he is under it ---");

        if (!flood.HeadUnderwater)
        {
            // The first flood has started draining. Let it finish, then run it
            // again so the player is genuinely under water when the tank goes.
            float waited = 0f;

            while (flood.State != FloodEvent.FloodState.Idle && waited < 25f)
            {
                yield return null;
                waited += Time.deltaTime;
            }

            Log(string.Format(
                CultureInfo.InvariantCulture,
                "waited {0:F1}s for the flood to finish (state {1})",
                waited, flood.State));

            flood.BeginFloodNow();

            float rising = 0f;

            while (!flood.HeadUnderwater && rising < 12f)
            {
                yield return null;
                rising += Time.deltaTime;
            }

            Log(string.Format(
                CultureInfo.InvariantCulture,
                "water reached his head again after {0:F1}s " +
                "(water {1:F2}, state {2})",
                rising, flood.WaterLevel, flood.State));
        }

        if (!flood.HeadUnderwater)
        {
            Log("could not get the player under water again - " +
                "no-tank check skipped");
        }
        else
        {
            PlayerPrefs.SetInt("Upgrade_OxygenTank", 0);
            PlayerPrefs.Save();

            Log("O2 Tank owned=" + GameProgress.HasOxygenTank +
                ", oxygen protects=" + flood.OxygenProtects +
                ", oxygen left=" + flood.CurrentOxygen);

            float after = 0f;
            float nextLine = 0f;

            while (!flood.PlayerLost && after < 5f)
            {
                yield return null;
                after += Time.deltaTime;

                if (flood.PlayerLost || after >= nextLine)
                {
                    Log(string.Format(
                        CultureInfo.InvariantCulture,
                        "t={0:F2}s water={1:F2} headUnderwater={2} lost={3}",
                        after, flood.WaterLevel, flood.HeadUnderwater,
                        flood.PlayerLost));

                    nextLine = after + 0.2f;
                }
            }

            Log(string.Format(
                CultureInfo.InvariantCulture,
                "player lost without the tank: {0} (after {1:F2}s under water)",
                flood.PlayerLost, after));

            Log("for comparison, with the tank he spent 4.4s under the " +
                "same water and was still alive with " +
                flood.oxygenCapacity + "s of oxygen to spend");
        }

        Log("");
        Log("=== done ===");
    }

    /// <summary>Drops the player onto the house floor so the flood part of the
    /// test is repeatable even after a death respawn.</summary>
    private void PlacePlayerOnFloor()
    {
        Transform player = flood.playerRoot != null
            ? flood.playerRoot
            : GameObject.Find("PlayerRoot")?.transform;

        if (player == null)
        {
            Log("no player to place");
            return;
        }

        CharacterController controller = player.GetComponent<CharacterController>();

        Vector3 spot = flood.HouseCenter;
        spot.y = flood.FloorLevel + 0.6f;

        if (controller != null)
            controller.enabled = false;

        player.position = spot;
        Physics.SyncTransforms();

        if (controller != null)
            controller.enabled = true;

        Log(string.Format(
            CultureInfo.InvariantCulture,
            "player placed on the house floor at ({0:F2}, {1:F2}, {2:F2})",
            spot.x, spot.y, spot.z));
    }

    private void LogQuake(string label)
    {
        Log(string.Format(
            CultureInfo.InvariantCulture,
            "earthquake ({0}): exposed player survives {1:F1}s " +
            "(quake {2:F1}s + hard hat {3:F1}s), hard hat active={4}",
            label, quake.ExposedDeadline, quake.earthquakeDuration,
            quake.hardHatExtraSeconds, quake.HardHatProtects));
    }

    private IEnumerator TornadoCheck(string label)
    {
        float baseMultiplier = tornado.MovementSpeedMultiplier;

        bool started = tornado.StartTornadoNow();
        yield return new WaitForSeconds(0.5f);

        Log(string.Format(
            CultureInfo.InvariantCulture,
            "tornado ({0}): scene slowdown {1:F3}, applied to player {2:F3}, " +
            "motor reading {3:F3}, crampons active={4}, started={5}",
            label, baseMultiplier, tornado.AppliedSlowdown,
            motor.TornadoSpeedMultiplier, tornado.CramponsProtect, started));

        tornado.StopTornadoNow();
        yield return new WaitForSeconds(0.4f);

        Log(string.Format(
            CultureInfo.InvariantCulture,
            "tornado ({0}): after it ends the motor is back to {1:F3}",
            label, motor.TornadoSpeedMultiplier));
    }

    private void ClearUpgrades()
    {
        foreach (string key in UpgradeKeys)
            PlayerPrefs.SetInt(key, 0);

        PlayerPrefs.Save();
    }

    private void SavePrefs()
    {
        savedCurrency = PlayerPrefs.GetInt(CurrencyKey, 0);

        for (int i = 0; i < UpgradeKeys.Length; i++)
            savedFlags[i] = PlayerPrefs.GetInt(UpgradeKeys[i], 0);
    }

    /// <summary>Puts the player's real shop state back.</summary>
    private void RestorePrefs()
    {
        if (restored)
            return;

        restored = true;

        PlayerPrefs.SetInt(CurrencyKey, savedCurrency);

        for (int i = 0; i < UpgradeKeys.Length; i++)
            PlayerPrefs.SetInt(UpgradeKeys[i], savedFlags[i]);

        PlayerPrefs.Save();

        Log("shop state restored: currency=" + savedCurrency +
            " hardHat=" + savedFlags[0] +
            " crampons=" + savedFlags[1] +
            " oxygenTank=" + savedFlags[2]);
    }

    private void OnDestroy()
    {
        RestorePrefs();
    }

    private void Finish()
    {
        try
        {
            File.WriteAllText(reportPath + ".done", "done");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("UpgradeEffectTestRunner: " + e.Message);
        }

        enabled = false;

#if UNITY_EDITOR
        // Leave play mode by ourselves so the editor is not left running.
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void Log(string message)
    {
        Debug.Log("[upgrade test] " + message);

        if (string.IsNullOrEmpty(reportPath))
            return;

        try
        {
            File.AppendAllText(reportPath, message + "\n", Encoding.UTF8);
        }
        catch (System.Exception)
        {
            // Reporting must never break the test.
        }
    }
}
