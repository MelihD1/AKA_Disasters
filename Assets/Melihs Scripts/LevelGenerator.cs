using System.Collections.Generic;
using UnityEngine;

public class LevelGenerator : MonoBehaviour
{
    [Header("Room Pool")]
    [Tooltip("Drag your Room prefabs from the Project folder here")]
    public List<Room> roomPool = new List<Room>();

    [Header("Starting Setup")]
    [Tooltip("Drag the first room that is already sitting in your scene here")]
    public Room startingRoom;

    [Header("Level Settings")]
    public int currentLevel = 1;

    private List<Room> spawnedRooms = new List<Room>();

    void Start()
    {
        GenerateLevel();
    }

    public void GenerateLevel()
    {
        // Level 1 = 3 rooms, Level 2 = 5 rooms, Level 3 = 7 rooms...
        int roomsToSpawn = 1 + (currentLevel * 2);

        Transform nextSpawnPoint = startingRoom.exitPoint;

        for (int i = 0; i < roomsToSpawn; i++)
        {
            // 1. Pick a random room prefab from the pool
            int randomIndex = Random.Range(0, roomPool.Count);
            Room selectedRoomPrefab = roomPool[randomIndex];

            // 2. Spawn the room
            Room newRoom = Instantiate(selectedRoomPrefab, transform);

            // 3. Align new room's EntrancePoint rotation and position with previous room's ExitPoint
            if (newRoom.entrancePoint != null)
            {
                Quaternion targetRotation = nextSpawnPoint.rotation * Quaternion.Inverse(newRoom.entrancePoint.localRotation);
                newRoom.transform.rotation = targetRotation;

                Vector3 positionOffset = newRoom.transform.position - newRoom.entrancePoint.position;
                newRoom.transform.position = nextSpawnPoint.position + positionOffset;
            }
            else
            {
                newRoom.transform.position = nextSpawnPoint.position;
                newRoom.transform.rotation = nextSpawnPoint.rotation;
            }

            spawnedRooms.Add(newRoom);

            // 4. Move the target spawn point to the newly spawned room's ExitPoint
            nextSpawnPoint = newRoom.exitPoint;
        }
    }
}