using System.Collections.Generic;
using UnityEngine;

public class LevelGenerator : MonoBehaviour
{
    [Header("Pools")]
    [Tooltip("Drag your Room prefabs from the Project folder here")]
    public List<Room> roomPool = new List<Room>();

    [Tooltip("Drag your Hallway prefabs from the Project folder here")]
    public List<Room> hallwayPool = new List<Room>();

    [Header("Starting Setup")]
    [Tooltip("Drag the first room that is already sitting in your scene here")]
    public Room startingRoom;

    [Header("Level Settings")]
    public int currentLevel = 1;

    private List<Room> spawnedPieces = new List<Room>();

    void Start()
    {
        GenerateLevel();
    }

    public void GenerateLevel()
    {
        // Level 1 = 3 rooms (with a hallway before each one), Level 2 = 5 rooms, etc.
        int roomsToSpawn = 1 + (currentLevel * 2);

        Transform nextSpawnPoint = startingRoom.exitPoint;

        for (int i = 0; i < roomsToSpawn; i++)
        {
            // 1. Spawn a random Hallway first (if the hallway pool isn't empty)
            if (hallwayPool.Count > 0)
            {
                Room newHallway = SpawnPiece(hallwayPool, nextSpawnPoint);
                nextSpawnPoint = newHallway.exitPoint;
            }

            // 2. Spawn a random Room at the end of the Hallway
            if (roomPool.Count > 0)
            {
                Room newRoom = SpawnPiece(roomPool, nextSpawnPoint);
                nextSpawnPoint = newRoom.exitPoint;
            }
        }
    }

    private Room SpawnPiece(List<Room> pool, Transform spawnPoint)
    {
        // Pick a random prefab from the given pool (Room or Hallway)
        int randomIndex = Random.Range(0, pool.Count);
        Room selectedPrefab = pool[randomIndex];

        // Spawn the piece
        Room newPiece = Instantiate(selectedPrefab, transform);

        // Align its EntrancePoint rotation and position with the previous ExitPoint
        if (newPiece.entrancePoint != null)
        {
            Quaternion targetRotation = spawnPoint.rotation * Quaternion.Inverse(newPiece.entrancePoint.localRotation);
            newPiece.transform.rotation = targetRotation;

            Vector3 positionOffset = newPiece.transform.position - newPiece.entrancePoint.position;
            newPiece.transform.position = spawnPoint.position + positionOffset;
        }
        else
        {
            newPiece.transform.position = spawnPoint.position;
            newPiece.transform.rotation = spawnPoint.rotation;
        }

        spawnedPieces.Add(newPiece);
        return newPiece;
    }
}