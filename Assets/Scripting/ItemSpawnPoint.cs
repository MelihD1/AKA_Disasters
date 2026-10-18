using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sits where one of Scan's "REPLACEWITHITEM" marker cubes used to be and drops
/// one random item from the emergency kit recipe there when the room appears.
/// </summary>
[DisallowMultipleComponent]
public class ItemSpawnPoint : MonoBehaviour
{
    [Tooltip("Item prefabs to choose from. One is picked at random per room.")]
    public List<GameObject> itemPrefabs = new List<GameObject>();

    [Tooltip("Lift the item off the floor a little so it does not sink in.")]
    public float heightOffset = 0.15f;

    [Tooltip("Spin the item randomly so rooms do not all look identical.")]
    public bool randomRotation = true;

    [Tooltip("Keep the item parented here, so it disappears with the room.")]
    public bool keepAsChild = true;

    private GameObject spawned;

    /// <summary>The item this point produced, if any.</summary>
    public GameObject Spawned => spawned;

    private void Start()
    {
        Spawn();
    }

    /// <summary>Places one random item. Does nothing if it already has.</summary>
    public GameObject Spawn()
    {
        if (spawned != null)
            return spawned;

        if (itemPrefabs == null || itemPrefabs.Count == 0)
        {
            Debug.LogWarning(
                "ItemSpawnPoint '" + name + "' has no item prefabs set.", this);
            return null;
        }

        GameObject prefab = itemPrefabs[Random.Range(0, itemPrefabs.Count)];

        if (prefab == null)
            return null;

        Quaternion rotation = randomRotation
            ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
            : transform.rotation;

        Vector3 position = transform.position + Vector3.up * heightOffset;

        spawned = Instantiate(prefab, position, rotation);
        spawned.name = prefab.name + " (spawned)";

        // Glow so the player can spot it across the room. Added here rather
        // than in the prefab so the item prefabs stay untouched.
        if (spawned.GetComponent<ItemGlow>() == null)
            spawned.AddComponent<ItemGlow>();

        if (keepAsChild)
            spawned.transform.SetParent(transform, true);

        return spawned;
    }
}
