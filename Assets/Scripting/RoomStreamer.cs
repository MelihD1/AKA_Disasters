using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Streams an endless chain of rooms and hallways: room, hallway, room,
/// hallway, and so on. The next piece is built a little before the player
/// reaches the door, and pieces more than a few doors back are thrown away, so
/// the level can carry on forever without the scene filling up.
///
/// It uses the same convention as Melih's LevelGenerator: every piece is a
/// prefab with a Room component whose entrancePoint and exitPoint are set, and
/// a new piece is rotated so that its entrance sits exactly on the previous
/// piece's exit, facing the same way.
/// </summary>
[DisallowMultipleComponent]
public class RoomStreamer : MonoBehaviour
{
    [Header("Pieces")]
    [Tooltip("Room prefabs the streamer picks from.")]
    public List<Room> roomPool = new List<Room>();

    [Tooltip("Hallway prefabs the streamer picks from. One is placed between every two rooms.")]
    public List<Room> hallwayPool = new List<Room>();

    [Tooltip("The piece the player starts in. The chain grows out of its exit.")]
    public Room startingRoom;

    [Tooltip("Build a hallway first, so the chain reads room-hallway-room-hallway.")]
    public bool hallwayBetweenRooms = true;

    [Header("Streaming")]
    [Tooltip("How many pieces to keep behind the player before the oldest is deleted. 3 = three doors back.")]
    [Min(0)] public int keepBehind = 3;

    [Tooltip("How many pieces to keep built ahead of the player.")]
    [Min(1)] public int buildAhead = 1;

    [Tooltip("How close the player gets to a piece's exit before the next piece is built.")]
    public float buildDistance = 9f;

    [Tooltip("Never place the same room twice in a row.")]
    public bool avoidRepeats = true;

    [Header("Debug")]
    public bool showDebugMessages = true;

    // Chain bookkeeping ------------------------------------------------------

    private readonly List<Room> chain = new List<Room>();
    private int nextIsHallway = -1;

    /// <summary>Pieces currently alive in the chain.</summary>
    public int PiecesInChain => chain.Count;

    /// <summary>Total pieces built since the scene started.</summary>
    public int PiecesBuilt { get; private set; }

    /// <summary>Total pieces removed behind the player.</summary>
    public int PiecesRemoved { get; private set; }

    /// <summary>How many doors the player has walked past.</summary>
    public int DoorsPassed { get; private set; }

    /// <summary>Name of the piece the player is closest to.</summary>
    public string CurrentPieceName { get; private set; } = "?";

    /// <summary>The pieces currently alive, oldest first.</summary>
    public IReadOnlyList<Room> Chain => chain;

    /// <summary>Index of the piece the player is standing in.</summary>
    public int CurrentIndex => currentIndex;

    /// <summary>The last piece built.</summary>
    public Room LastPiece =>
        chain.Count > 0 ? chain[chain.Count - 1] : null;

    /// <summary>Distance error of the last link, in metres. 0 is a perfect join.</summary>
    public float LastLinkError { get; private set; }

    /// <summary>Facing error of the last link, in degrees.</summary>
    public float LastLinkAngleError { get; private set; }

    private Transform player;
    private int currentIndex;

    private void Start()
    {
        ResolvePlayer();

        if (startingRoom == null)
        {
            Debug.LogWarning("RoomStreamer: no starting room set.", this);
            return;
        }

        if (!chain.Contains(startingRoom))
            chain.Add(startingRoom);

        currentIndex = 0;
        nextIsHallway = hallwayBetweenRooms ? 1 : 0;

        // A couple of pieces so there is somewhere to walk to right away.
        for (int i = 0; i < buildAhead + 1; i++)
            BuildNext();

        if (showDebugMessages)
        {
            Debug.Log("RoomStreamer: chain started with " + chain.Count +
                " pieces, " + PiecesBuilt + " built.", this);
        }
    }

    private void Update()
    {
        if (chain.Count == 0)
            return;

        if (player == null)
        {
            ResolvePlayer();

            if (player == null)
                return;
        }

        UpdateCurrentPiece();
        BuildAheadIfNeeded();
        RemoveOldPieces();
    }

    private void ResolvePlayer()
    {
        FloodEvent flood = FindFirstObjectByType<FloodEvent>();

        if (flood != null && flood.playerRoot != null)
        {
            player = flood.playerRoot;
            return;
        }

        GameObject named = GameObject.Find("PlayerRoot");

        if (named != null)
            player = named.transform;
    }

    /// <summary>Finds the piece the player is standing in.</summary>
    private void UpdateCurrentPiece()
    {
        int closest = 0;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < chain.Count; i++)
        {
            Vector3 centre = CentreOf(chain[i]);
            float distance = Vector3.Distance(player.position, centre);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = i;
            }
        }

        if (closest > currentIndex)
            DoorsPassed += closest - currentIndex;

        currentIndex = closest;
        CurrentPieceName = chain[currentIndex] != null
            ? chain[currentIndex].name
            : "?";
    }

    private void BuildAheadIfNeeded()
    {
        // Always keep a minimum number of pieces built ahead of the player...
        while (chain.Count - 1 < currentIndex + buildAhead)
            BuildNext();

        if (chain.Count == 0)
            return;

        // ...and start the piece after that as soon as he walks up to the door
        // at the far end of the chain.
        Room last = chain[chain.Count - 1];

        if (last == null || last.exitPoint == null)
            return;

        if (Vector3.Distance(player.position, last.exitPoint.position) <=
            buildDistance)
        {
            BuildNext();
        }
    }

    private void RemoveOldPieces()
    {
        while (currentIndex - keepBehind > 0 && chain.Count > 0)
        {
            Room oldest = chain[0];

            chain.RemoveAt(0);
            currentIndex--;
            PiecesRemoved++;

            if (oldest != null && oldest != startingRoom)
            {
                if (showDebugMessages)
                {
                    Debug.Log("RoomStreamer: removed '" + oldest.name +
                        "' (" + PiecesRemoved + " removed so far).", this);
                }

                Destroy(oldest.gameObject);
            }
            else if (oldest == startingRoom)
            {
                // Never delete the room the player started in; just stop
                // tracking it.
                if (showDebugMessages)
                    Debug.Log("RoomStreamer: the starting room is now behind the cutoff and stays put.", this);
            }
        }
    }

    /// <summary>Builds the next piece of the chain at the end of the last one.</summary>
    private void BuildNext()
    {
        Room previous = chain.Count > 0 ? chain[chain.Count - 1] : startingRoom;

        if (previous == null)
            return;

        bool wantHallway = nextIsHallway == 1;

        if (nextIsHallway < 0)
            wantHallway = hallwayBetweenRooms;

        List<Room> pool = wantHallway ? hallwayPool : roomPool;

        if (pool == null || pool.Count == 0)
        {
            pool = wantHallway ? roomPool : hallwayPool;
            wantHallway = !wantHallway;
        }

        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning("RoomStreamer: no prefabs to build from.", this);
            return;
        }

        Room prefab = Pick(pool);

        if (prefab == null)
            return;

        Room piece = Instantiate(prefab, transform);
        piece.name = prefab.name + " [streamed]";

        if (Link(previous, piece))
        {
            chain.Add(piece);
            PiecesBuilt++;
            nextIsHallway = wantHallway ? 0 : 1;
        }
        else
        {
            Debug.LogWarning("RoomStreamer: could not link '" + prefab.name +
                "' - it needs an entrance and an exit point.", this);

            Destroy(piece.gameObject);
        }
    }

    /// <summary>
    /// Puts the new piece's entrance exactly on the previous piece's exit,
    /// facing the same way.
    /// </summary>
    private bool Link(Room previous, Room piece)
    {
        if (previous == null || piece == null)
            return false;

        Transform exit = previous.exitPoint;
        Transform entrance = piece.entrancePoint;

        if (exit == null || entrance == null)
            return false;

        // Rotate first, with the piece at identity so its entrance rotation is
        // its local rotation.
        piece.transform.rotation = Quaternion.identity;

        piece.transform.rotation =
            exit.rotation * Quaternion.Inverse(entrance.rotation);

        // Then slide it so the entrance lands on the exit.
        piece.transform.position += exit.position - entrance.position;

        LastLinkError = Vector3.Distance(exit.position, entrance.position);
        LastLinkAngleError = Quaternion.Angle(exit.rotation, entrance.rotation);

        return true;
    }

    private Room Pick(List<Room> pool)
    {
        if (pool.Count == 1)
            return pool[0];

        // Compare against the last piece of the same kind: the piece right
        // before this one is always the other kind (a hallway between rooms).
        Room lastSameKind = null;

        for (int i = chain.Count - 1; i >= 0; i--)
        {
            Room piece = chain[i];

            if (piece == null)
                continue;

            string pieceName = piece.name.Replace(" [streamed]", "");

            foreach (Room candidate in pool)
            {
                if (candidate != null && candidate.name == pieceName)
                {
                    lastSameKind = piece;
                    break;
                }
            }

            if (lastSameKind != null)
                break;
        }

        string lastName = lastSameKind != null
            ? lastSameKind.name.Replace(" [streamed]", "")
            : "";

        for (int attempt = 0; attempt < 8; attempt++)
        {
            Room candidate = pool[Random.Range(0, pool.Count)];

            if (candidate == null)
                continue;

            if (avoidRepeats && candidate.name == lastName)
                continue;

            return candidate;
        }

        return pool[Random.Range(0, pool.Count)];
    }

    /// <summary>Midpoint between a piece's entrance and exit.</summary>
    private Vector3 CentreOf(Room piece)
    {
        if (piece == null)
            return Vector3.zero;

        if (piece.entrancePoint == null || piece.exitPoint == null)
            return piece.transform.position;

        return (piece.entrancePoint.position + piece.exitPoint.position) * 0.5f;
    }

    private void OnDrawGizmosSelected()
    {
        foreach (Room piece in chain)
        {
            if (piece == null)
                continue;

            Gizmos.color = piece == startingRoom
                ? Color.yellow
                : (piece.entrancePoint != null && hallwayPool.Contains(piece)
                    ? Color.cyan
                    : Color.green);

            Vector3 a = piece.entrancePoint != null
                ? piece.entrancePoint.position
                : piece.transform.position;

            Vector3 b = piece.exitPoint != null
                ? piece.exitPoint.position
                : piece.transform.position;

            Gizmos.DrawLine(a, b);
            Gizmos.DrawSphere(a, 0.4f);
            Gizmos.DrawSphere(b, 0.4f);
        }
    }
}
