
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sets up the endless room streamer in the open scene: it collects the room
/// prefabs from Melih's Rooms folder (the ones ending in "+ Scan's edits") and
/// the hallways from his Halls folder, finds the room the player starts in,
/// and puts a RoomStreamer next to it. Run it from Tools &gt; Rooms.
/// </summary>
public static class RoomStreamerSetup
{
    private const string RoomsFolder = "Assets/Prefabs Melihs/Rooms";
    private const string HallsFolder = "Assets/Prefabs Melihs/Halls";
    private const string StreamerName = "RoomStreamer";

    [MenuItem("Tools/Rooms/Set Up Room Streamer", priority = 40)]
    private static void SetupMenu()
    {
        string report = SetupActiveScene();

        Debug.Log("RoomStreamerSetup:\n" + report);
        EditorUtility.DisplayDialog("Room streamer", report, "OK");
    }

    [MenuItem("Tools/Rooms/Check Room Streamer", priority = 41)]
    private static void CheckMenu()
    {
        string report = ReportStreamer();

        Debug.Log("RoomStreamerSetup:\n" + report);
        EditorUtility.DisplayDialog("Room streamer", report, "OK");
    }

    /// <summary>Builds the streamer object and fills its pools. Safe to re-run.</summary>
    public static string SetupActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        StringBuilder log = new StringBuilder();

        if (!scene.IsValid() || !scene.isLoaded)
            return "ERROR: no active scene.";

        log.AppendLine("scene: " + scene.path);

        List<Room> rooms = LoadPieces(RoomsFolder, true, log);
        List<Room> halls = LoadPieces(HallsFolder, false, log);

        Room starting = FindStartingRoom(log);

        if (starting == null)
        {
            log.AppendLine("ERROR: no Room component found in the scene to " +
                "use as the starting room.");
            return log.ToString();
        }

        GameObject host = GameObject.Find(StreamerName);

        if (host == null)
        {
            host = new GameObject(StreamerName);
            Undo.RegisterCreatedObjectUndo(host, "Create room streamer");
        }

        RoomStreamer streamer = host.GetComponent<RoomStreamer>();

        if (streamer == null)
            streamer = host.AddComponent<RoomStreamer>();

        streamer.roomPool = rooms;
        streamer.hallwayPool = halls;
        streamer.startingRoom = starting;

        EditorUtility.SetDirty(streamer);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        log.AppendLine("rooms: " + rooms.Count + ", hallways: " + halls.Count);
        log.AppendLine("starting room: " + starting.name);
        log.AppendLine("saved: " + scene.path);

        return log.ToString();
    }

    /// <summary>Reports the pools and, if playing, what the chain is doing.</summary>
    public static string ReportStreamer()
    {
        StringBuilder log = new StringBuilder();
        Scene scene = SceneManager.GetActiveScene();

        log.AppendLine("scene: " + scene.path);

        RoomStreamer streamer = Object.FindFirstObjectByType<RoomStreamer>();

        if (streamer == null)
        {
            log.AppendLine("streamer: NOT IN SCENE (run Tools > Rooms > Set Up Room Streamer)");
            return log.ToString();
        }

        log.AppendLine("streamer pools: rooms=" + streamer.roomPool.Count +
            " hallways=" + streamer.hallwayPool.Count +
            " starting=" + (streamer.startingRoom != null
                ? streamer.startingRoom.name
                : "NONE"));

        log.AppendLine("settings: keepBehind=" + streamer.keepBehind +
            " buildAhead=" + streamer.buildAhead +
            " buildDistance=" + streamer.buildDistance +
            " hallwayBetweenRooms=" + streamer.hallwayBetweenRooms);

        if (!Application.isPlaying)
        {
            log.AppendLine("(not playing: chain stats appear when playing)");
            return log.ToString();
        }

        log.AppendLine("chain: pieces=" + streamer.PiecesInChain +
            " built=" + streamer.PiecesBuilt +
            " removed=" + streamer.PiecesRemoved +
            " doorsPassed=" + streamer.DoorsPassed +
            " current='" + streamer.CurrentPieceName + "'");

        log.AppendLine("last link: error=" +
            streamer.LastLinkError.ToString("F4") + "m angle=" +
            streamer.LastLinkAngleError.ToString("F2") + " deg");

        foreach (Room piece in Object.FindObjectsByType<Room>(
            FindObjectsInactive.Include))
        {
            string name = piece.name;

            Vector3 centre = piece.entrancePoint != null &&
                piece.exitPoint != null
                ? (piece.entrancePoint.position + piece.exitPoint.position) * 0.5f
                : piece.transform.position;

            log.AppendLine(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "   '{0}' at ({1:F1}, {2:F1}, {3:F1})",
                name, centre.x, centre.y, centre.z));
        }

        return log.ToString();
    }

    private static List<Room> LoadPieces(
        string folder, bool onlyScanEdits, StringBuilder log)
    {
        List<Room> pieces = new List<Room>();

        if (!AssetDatabase.IsValidFolder(folder))
        {
            log.AppendLine("MISSING FOLDER: " + folder);
            return pieces;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab",
            new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path).Trim();

            if (onlyScanEdits && !IsScanEdit(name))
            {
                log.AppendLine("   skipped (not a Scan edit): " + name);
                continue;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
                continue;

            Room room = prefab.GetComponent<Room>();

            if (room == null)
            {
                log.AppendLine("   skipped (no Room component): " + name);
                continue;
            }

            if (room.entrancePoint == null || room.exitPoint == null)
            {
                log.AppendLine("   skipped (entrance/exit not wired): " + name);
                continue;
            }

            pieces.Add(room);
        }

        return pieces;
    }

    /// <summary>Matches names like "Room1.1.3 Done + Scan's edits".</summary>
    private static bool IsScanEdit(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        string lower = name.ToLowerInvariant();

        return lower.Contains("scan") && lower.TrimEnd().EndsWith("edits");
    }

    private static Room FindStartingRoom(StringBuilder log)
    {
        Room found = null;

        foreach (Room room in Object.FindObjectsByType<Room>(
            FindObjectsInactive.Include))
        {
            log.AppendLine("   room in scene: " + room.name +
                (room.entrancePoint != null ? " (entrance ok)" : " (no entrance)") +
                (room.exitPoint != null ? " (exit ok)" : " (no exit)"));

            if (room.entrancePoint == null || room.exitPoint == null)
                continue;

            // Prefer the biggest piece in the scene - that is the house the
            // player starts in rather than a stray prop.
            if (found == null)
            {
                found = room;
                continue;
            }

            if (RoomSize(room) > RoomSize(found))
                found = room;
        }

        return found;
    }

    private static float RoomSize(Room room)
    {
        if (room.entrancePoint == null || room.exitPoint == null)
            return 0f;

        return Vector3.Distance(
            room.entrancePoint.position, room.exitPoint.position);
    }
}
