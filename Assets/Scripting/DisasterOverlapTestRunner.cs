using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Temporary rig: runs an earthquake and a flood at the same time, both ways
/// round, to prove neither one cancels the other and that both sounds keep
/// playing until the event that owns them ends.
/// </summary>
public class DisasterOverlapTestRunner : MonoBehaviour
{
    public string reportPath;

    private EarthquakeEvent quake;
    private FloodEvent flood;
    private TornadoEvent tornado;

    private IEnumerator Start()
    {
        quake = FindFirstObjectByType<EarthquakeEvent>();
        flood = FindFirstObjectByType<FloodEvent>();
        tornado = FindFirstObjectByType<TornadoEvent>();

        Log("=== disaster overlap test ===");

        if (quake == null || flood == null)
        {
            Log("ERROR: need both an earthquake and a flood in the scene.");
            Finish();
            yield break;
        }

        DisasterDifficultyManager manager =
            FindFirstObjectByType<DisasterDifficultyManager>();

        if (manager != null)
        {
            manager.enabled = false;
            manager.gameObject.SetActive(false);
            Log("difficulty manager switched off so it cannot start anything");
        }

        // Keep the water from ending the run, so both events can be watched.
        flood.drownPlayer = false;
        Log("flood drowning disabled just for this test");

        Log("");
        Log("--- earthquake first, flood started on top of it ---");

        quake.StartEarthquakeNow();
        yield return new WaitForSeconds(0.5f);
        Report("quake alone");

        bool started = flood.BeginFloodNow();
        Log("flood start accepted while the quake runs: " + started);

        yield return new WaitForSeconds(1f);
        Report("1s later");

        yield return new WaitForSeconds(2f);
        Report("3s later");

        Log("quake still running through all of that: " +
            quake.EarthquakeActive);

        Log("");
        Log("--- stopping the quake while the flood keeps going ---");

        quake.StopEarthquakeNow();
        yield return new WaitForSeconds(0.2f);
        Report("0.2s after the quake stopped (sound should be fading)");

        yield return new WaitForSeconds(0.6f);
        Report("0.8s after (fade should be done)");

        Log("");
        Log("--- flood first, then an earthquake on top of it ---");

        quake.StartEarthquakeNow();
        yield return new WaitForSeconds(1f);
        Report("quake started during the flood");

        Log("flood state through that: " + flood.State +
            " (still running: " + flood.FloodActive + ")");

        Log("");
        Log("=== done ===");
        Finish();
    }

    private void Report(string label)
    {
        Log("[" + label + "]");
        Log("   quake active=" + quake.EarthquakeActive +
            " lost=" + quake.PlayerLost +
            " | flood state=" + flood.State +
            " water=" + flood.WaterLevel.ToString("F2", CultureInfo.InvariantCulture));

        LogSources("   quake", quake);
        LogSources("   flood", flood);
        LogSources("   tornado", tornado);
    }

    private void LogSources(string label, MonoBehaviour host)
    {
        if (host == null)
            return;

        foreach (AudioSource source in host.GetComponents<AudioSource>())
        {
            if (source.clip == null && !source.isPlaying)
                continue;

            Log(string.Format(
                CultureInfo.InvariantCulture,
                "{0} audio: clip={1} playing={2} volume={3:F2}",
                label,
                source.clip != null ? source.clip.name : "NONE",
                source.isPlaying,
                source.volume));
        }
    }

    private void Finish()
    {
        try
        {
            File.WriteAllText(reportPath + ".done", "done");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("DisasterOverlapTestRunner: " + e.Message);
        }

        enabled = false;
    }

    private void Log(string message)
    {
        Debug.Log("[overlap test] " + message);

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
