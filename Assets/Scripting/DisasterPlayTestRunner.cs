
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Temporary test rig for the disaster difficulty system: it speeds the ramp
/// up, keeps the player alive, watches a few minutes of play and writes down
/// what actually happened. Created by the editor bridge before play starts;
/// safe to delete once the system has been verified.
/// </summary>
public class DisasterPlayTestRunner : MonoBehaviour
{
    public string reportPath;
    public float testSeconds = 70f;

    private DisasterDifficultyManager manager;
    private EarthquakeEvent earthquake;
    private FloodEvent flood;
    private TornadoEvent tornado;

    private IEnumerator Start()
    {
        manager = FindFirstObjectByType<DisasterDifficultyManager>();
        earthquake = FindFirstObjectByType<EarthquakeEvent>();
        flood = FindFirstObjectByType<FloodEvent>();
        tornado = FindFirstObjectByType<TornadoEvent>();

        if (manager == null || earthquake == null || flood == null ||
            tornado == null)
        {
            Log("ERROR: missing manager or disaster in the scene.");
            Finish();
            yield break;
        }

        yield return Run();
    }

    private IEnumerator Run()
    {
        Log("=== disaster difficulty test ===");

        // Speed everything up. Runtime only: nothing here is saved.
        manager.timeToMaximumDifficulty = 24f;
        manager.firstDisasterDelay = 2f;
        manager.gapAtStart = 5f;
        manager.gapAtMaximum = 2f;
        manager.gapJitter = 0f;
        manager.showHud = false;
        manager.useTornadoes = true;
        manager.tornadoDuringMajor = false;
        manager.TakeOverDisasters();

        // Do not let the test die: no drowning, and the player hides so the
        // earthquake cannot take him out either.
        flood.drownPlayer = false;
        flood.showDebugHud = false;

        earthquake.delayBeforeEarthquake = 9999f;

        HidingSpot spot = FindFirstObjectByType<HidingSpot>();

        if (spot == null)
        {
            // No hiding spot in this scene yet: make a throwaway one so the
            // earthquake cannot end the test early. Runtime only.
            GameObject temp = new GameObject("TestHidingSpot");
            spot = temp.AddComponent<HidingSpot>();
            Log("created a temporary hiding spot for the test");
        }

        if (spot != null)
        {
            Transform player = earthquake.playerRoot;

            if (player != null)
            {
                CharacterController controller =
                    player.GetComponent<CharacterController>();

                if (controller != null)
                    controller.enabled = false;

                player.position = spot.transform.position;

                if (controller != null)
                    controller.enabled = true;
            }

            Log("hiding the player: " + earthquake.TryEnterHide(spot));
        }
        else
        {
            Log("WARNING: no hiding spot in the scene, the earthquake may " +
                "end the test early.");
        }

        Log(string.Format(
            CultureInfo.InvariantCulture,
            "setup: ramp {0:F0}s, gap {1:F0}->{2:F0}s, tornado during major {3}",
            manager.timeToMaximumDifficulty,
            manager.gapAtStart,
            manager.gapAtMaximum,
            manager.tornadoDuringMajor));

        // Deterministic check: force a flood and prove it actually starts.
        Log("forcing a flood to check the handoff");

        bool forced = manager.ForceFlood();
        float floodCheck = 0f;

        while (flood.State == FloodEvent.FloodState.Idle && floodCheck < 2f)
        {
            floodCheck += Time.deltaTime;
            yield return null;
        }

        Log(string.Format(
            CultureInfo.InvariantCulture,
            "forced flood: accepted={0} state={1} after {2:F2}s",
            forced,
            flood.State,
            floodCheck));

        float floodEnd = 0f;

        while (flood.State != FloodEvent.FloodState.Idle && floodEnd < 60f)
        {
            floodEnd += Time.deltaTime;
            yield return null;
        }

        Log(string.Format(
            CultureInfo.InvariantCulture,
            "forced flood finished: state={0} after {1:F2}s",
            flood.State,
            floodEnd));

        float timer = 0f;
        float nextSample = 3f;
        int lastStarted = 0;
        bool overlap = false;

        float firstEarthquakeSeconds = -1f;
        float lastEarthquakeSeconds = -1f;
        float firstFloodRise = -1f;
        float lastFloodRise = -1f;
        float firstTornadoSlow = -1f;
        float lastTornadoSlow = -1f;

        while (timer < testSeconds && !manager.PlayerLost)
        {
            timer += Time.deltaTime;

            if (earthquake.EarthquakeActive && flood.State != FloodEvent.FloodState.Idle)
                overlap = true;

            if (manager.DisastersStarted != lastStarted)
            {
                lastStarted = manager.DisastersStarted;

                Log(string.Format(
                    CultureInfo.InvariantCulture,
                    "started #{0} at difficulty {1:F2}: {2}",
                    lastStarted,
                    manager.Difficulty,
                    manager.CurrentDisaster));

                if (manager.CurrentDisaster == "earthquake")
                {
                    if (firstEarthquakeSeconds < 0f)
                        firstEarthquakeSeconds = earthquake.earthquakeDuration;

                    lastEarthquakeSeconds = earthquake.earthquakeDuration;

                    Log(string.Format(
                        CultureInfo.InvariantCulture,
                        "   earthquake values: length {0:F1}s, shake {1:F2}, " +
                        "drift {2:F1}, survival {3:F1}s",
                        earthquake.earthquakeDuration,
                        earthquake.cameraShakeAmount,
                        earthquake.maxDriftSpeed,
                        earthquake.extraSurvivalTime));
                }
                else if (manager.CurrentDisaster == "flood")
                {
                    if (firstFloodRise < 0f)
                        firstFloodRise = flood.waterRise;

                    lastFloodRise = flood.waterRise;

                    Log(string.Format(
                        CultureInfo.InvariantCulture,
                        "   flood values: rise {0:F2}, speed {1:F2}, hold {2:F1}s",
                        flood.waterRise,
                        flood.riseSpeed,
                        flood.peakHoldDuration));
                }
                else if (manager.CurrentDisaster == "tornado")
                {
                    if (firstTornadoSlow < 0f)
                        firstTornadoSlow = tornado.MovementSpeedMultiplier;

                    lastTornadoSlow = tornado.MovementSpeedMultiplier;

                    Log(string.Format(
                        CultureInfo.InvariantCulture,
                        "   tornado values: lasts {0:F1}s, movement x{1:F2}",
                        tornado.TornadoDuration,
                        tornado.MovementSpeedMultiplier));
                }
            }

            if (timer >= nextSample)
            {
                nextSample += 6f;

                Log(string.Format(
                    CultureInfo.InvariantCulture,
                    "t={0:F1}s  difficulty {1:F2}  now {2}  next in {3:F1}s  " +
                    "started {4}",
                    timer,
                    manager.Difficulty,
                    manager.CurrentDisaster,
                    manager.SecondsUntilNext,
                    manager.DisastersStarted));
            }

            if (earthquake.PlayerLost || flood.PlayerLost)
            {
                Log("player lost, stopping the test.");
                break;
            }

            yield return null;
        }

        Log("");
        Log("=== summary ===");
        Log(string.Format(
            CultureInfo.InvariantCulture,
            "difficulty reached: {0:F2} after {1:F1}s",
            manager.Difficulty,
            timer));

        Log("disasters started: " + manager.DisastersStarted);
        Log("two majors at once: " + overlap);
        Log("player lost: " + manager.PlayerLost);

        Log(string.Format(
            CultureInfo.InvariantCulture,
            "earthquake length first/last: {0:F1}s / {1:F1}s",
            firstEarthquakeSeconds,
            lastEarthquakeSeconds));

        Log(string.Format(
            CultureInfo.InvariantCulture,
            "flood rise first/last: {0:F2} / {1:F2}",
            firstFloodRise,
            lastFloodRise));

        Log(string.Format(
            CultureInfo.InvariantCulture,
            "tornado slowdown first/last: x{0:F2} / x{1:F2}",
            firstTornadoSlow,
            lastTornadoSlow));

        Log("=== done ===");

        Finish();
    }

    private void Finish()
    {
        try
        {
            File.WriteAllText(reportPath + ".done", "done");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("DisasterPlayTestRunner: " + e.Message);
        }

        enabled = false;
    }

    private void Log(string message)
    {
        Debug.Log("[disaster test] " + message);

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
