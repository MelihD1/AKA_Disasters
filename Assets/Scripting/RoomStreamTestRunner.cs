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

        Log("");
        Log("=== done ===");
        Finish();
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
