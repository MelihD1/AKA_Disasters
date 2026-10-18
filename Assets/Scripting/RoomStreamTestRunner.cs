using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Temporary rig: walks the player down the streaming chain door by door and
/// reports how the pieces line up, how far the chain reaches, and when the
/// oldest pieces get removed.
/// </summary>
public class RoomStreamTestRunner : MonoBehaviour
{
    public string reportPath;
    public int steps = 8;

    private IEnumerator Start()
    {
        RoomStreamer streamer = FindFirstObjectByType<RoomStreamer>();
        FloodEvent flood = FindFirstObjectByType<FloodEvent>();

        Log("=== room streaming test ===");

        if (streamer == null)
        {
            Log("ERROR: no RoomStreamer in the scene.");
            Finish();
            yield break;
        }

        Log("pools: rooms=" + streamer.roomPool.Count +
            " hallways=" + streamer.hallwayPool.Count +
            " starting=" + (streamer.startingRoom != null
                ? streamer.startingRoom.name
                : "NONE"));

        Log("settings: keepBehind=" + streamer.keepBehind +
            " buildAhead=" + streamer.buildAhead +
            " buildDistance=" + streamer.buildDistance +
            " hallwayBetweenRooms=" + streamer.hallwayBetweenRooms);

        Transform player = flood != null && flood.playerRoot != null
            ? flood.playerRoot
            : GameObject.Find("PlayerRoot")?.transform;

        if (player == null)
        {
            Log("ERROR: no player in the scene.");
            Finish();
            yield break;
        }

        CharacterController controller = player.GetComponent<CharacterController>();

        DisasterDifficultyManager manager =
            FindFirstObjectByType<DisasterDifficultyManager>();

        if (manager != null)
        {
            manager.enabled = false;
            manager.gameObject.SetActive(false);
            Log("difficulty manager switched off for the test");
        }

        yield return null;
        yield return null;

        LogState("before walking anywhere", streamer);

        for (int step = 1; step <= steps; step++)
        {
            IReadOnlyList<Room> chain = streamer.Chain;

            if (chain.Count == 0)
            {
                Log("the chain is empty, stopping.");
                break;
            }

            int next = Mathf.Min(
                streamer.CurrentIndex + 1, chain.Count - 1);

            Room target = chain[next];

            if (target == null)
            {
                Log("next piece is missing, stopping.");
                break;
            }

            // Stand just inside the piece's entrance, then walk on to its exit,
            // which is the same as going through its door.
            Vector3 entrance = target.entrancePoint != null
                ? target.entrancePoint.position
                : target.transform.position;

            Vector3 exit = target.exitPoint != null
                ? target.exitPoint.position
                : target.transform.position;

            Teleport(controller, player,
                new Vector3(entrance.x, entrance.y + 0.7f, entrance.z));

            yield return new WaitForSeconds(0.2f);

            Teleport(controller, player,
                new Vector3(exit.x, exit.y + 0.7f, exit.z));

            yield return new WaitForSeconds(0.3f);

            Log(string.Format(
                CultureInfo.InvariantCulture,
                "walked into '{0}' (player y={1:F2}, chain has {2})",
                target.name, player.position.y, streamer.PiecesInChain));

            LogState("step " + step, streamer);
        }

        // ---- items the rooms placed, and whether the sounds start ----
        Log("");
        Log("--- items placed by the rooms ---");

        WorldItemPickup[] pickups = FindObjectsByType<WorldItemPickup>(
            FindObjectsInactive.Include);

        Log("collectible items in the scene: " + pickups.Length);

        foreach (WorldItemPickup pickup in pickups)
        {
            if (pickup == null)
                continue;

            Log(string.Format(
                CultureInfo.InvariantCulture,
                "   '{0}' at ({1:F1}, {2:F1}, {3:F1}) item={4}",
                pickup.name,
                pickup.transform.position.x,
                pickup.transform.position.y,
                pickup.transform.position.z,
                pickup.Item != null ? pickup.Item.displayName : "NULL"));
        }

        ItemSpawnPoint[] points = FindObjectsByType<ItemSpawnPoint>(
            FindObjectsInactive.Include);

        int filled = 0;

        foreach (ItemSpawnPoint point in points)
        {
            if (point != null && point.Spawned != null)
                filled++;
        }

        Log("spawn points: " + points.Length + ", filled: " + filled);

        // Does the glow actually breathe?
        ItemGlow glow = FindFirstObjectByType<ItemGlow>();

        if (glow == null)
        {
            Log("no ItemGlow found on any spawned item");
        }
        else
        {
            Log("watching the glow on '" + glow.name + "' for two seconds:");

            for (int i = 0; i < 8; i++)
            {
                yield return new WaitForSeconds(0.25f);

                Light light = glow.GetComponent<Light>();

                Log(string.Format(
                    CultureInfo.InvariantCulture,
                    "   t={0:F2}s glow={1:F2} light={2:F2}",
                    (i + 1) * 0.25f, glow.CurrentGlow,
                    light != null ? light.intensity : -1f));
            }
        }

        Log("");
        Log("--- do the disaster sounds start? ---");

        EarthquakeEvent quake = FindFirstObjectByType<EarthquakeEvent>();
        TornadoEvent tornado = FindFirstObjectByType<TornadoEvent>();
        FloodEvent water = FindFirstObjectByType<FloodEvent>();

        LogAudio("earthquake idle", quake);
        LogAudio("tornado idle", tornado);
        LogAudio("flood idle", water);

        if (tornado != null)
        {
            tornado.StartTornadoNow();
            yield return new WaitForSeconds(1f);
            LogAudio("tornado running", tornado);
            tornado.StopTornadoNow();
        }

        if (quake != null)
        {
            quake.StartEarthquakeNow();
            yield return new WaitForSeconds(1f);
            LogAudio("earthquake running", quake);
            quake.StopEarthquakeNow();
        }

        if (water != null)
        {
            water.BeginFloodNow();
            yield return new WaitForSeconds(1f);
            LogAudio("flood running", water);
        }

        // ---- hiding should skip the quiet part of a quake ----
        Log("");
        Log("--- hiding during an earthquake ---");

        if (water != null)
        {
            // Stop the water from the sound check so it cannot drown him here.
            water.CancelFlood();
            yield return new WaitForSeconds(1f);
        }

        if (quake != null)
        {
            yield return new WaitForSeconds(0.5f);

            quake.StartEarthquakeNow();

            float deadline = quake.ExposedDeadline;

            Log(string.Format(
                CultureInfo.InvariantCulture,
                "quake started: exposed deadline {0:F1}s, quake active={1}",
                deadline, quake.EarthquakeActive));

            HidingSpot spot = FindFirstObjectByType<HidingSpot>();

            if (spot == null)
            {
                Log("no HidingSpot in the scene, cannot test hiding.");
            }
            else
            {
                // Hide straight away, while the safe part is still running.
                yield return new WaitForSeconds(0.3f);

                bool hidden = quake.TryEnterHide(spot);

                Log("hid immediately, TryEnterHide returned " + hidden +
                    " (hiding=" + quake.IsHiding + ")");

                float waited = 0f;

                while (quake.EarthquakeActive && waited < 30f)
                {
                    yield return null;
                    waited += Time.deltaTime;
                }

                Log(string.Format(
                    CultureInfo.InvariantCulture,
                    "quake ended after {0:F1}s of hiding (it would have been " +
                    "~{1:F1}s if the safe period had to play out), lost={2}",
                    waited, deadline + 5f, quake.PlayerLost));
            }
        }

        Log("");
        Log("=== done ===");
        Finish();
    }

    /// <summary>Names every AudioSource on a disaster object and says if it is running.</summary>
    private void LogAudio(string label, MonoBehaviour host)
    {
        if (host == null)
        {
            Log("  " + label + ": not in the scene");
            return;
        }

        AudioSource[] sources = host.GetComponents<AudioSource>();

        if (sources.Length == 0)
        {
            Log("  " + label + ": no AudioSource on '" + host.name + "'");
            return;
        }

        foreach (AudioSource source in sources)
        {
            Log(string.Format(
                CultureInfo.InvariantCulture,
                "  {0}: clip={1} playing={2} loop={3} volume={4:F2}",
                label,
                source.clip != null ? source.clip.name : "NONE",
                source.isPlaying,
                source.loop,
                source.volume));
        }
    }

    private void LogState(string label, RoomStreamer streamer)
    {
        Log("");
        Log("--- " + label + " ---");
        Log(string.Format(
            CultureInfo.InvariantCulture,
            "chain={0} built={1} removed={2} doorsPassed={3} current='{4}' " +
            "linkError={5:F4}m angleError={6:F2}deg",
            streamer.PiecesInChain, streamer.PiecesBuilt,
            streamer.PiecesRemoved, streamer.DoorsPassed,
            streamer.CurrentPieceName, streamer.LastLinkError,
            streamer.LastLinkAngleError));

        StringBuilder names = new StringBuilder();

        foreach (Room piece in streamer.Chain)
            names.Append(piece != null ? piece.name : "NULL").Append(" | ");

        Log("order: " + names);

        Room[] all = FindObjectsByType<Room>(FindObjectsInactive.Include);
        Log("Room components alive in the scene: " + all.Length);
    }

    private void Teleport(
        CharacterController controller, Transform player, Vector3 position)
    {
        if (controller != null)
            controller.enabled = false;

        player.position = position;
        Physics.SyncTransforms();

        if (controller != null)
            controller.enabled = true;
    }

    private void Finish()
    {
        try
        {
            File.WriteAllText(reportPath + ".done", "done");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("RoomStreamTestRunner: " + e.Message);
        }

        enabled = false;
    }

    private void Log(string message)
    {
        Debug.Log("[room test] " + message);

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
