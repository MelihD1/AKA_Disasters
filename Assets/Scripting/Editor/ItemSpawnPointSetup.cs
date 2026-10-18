
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Replaces Scan's "REPLACEWITHITEM" marker cubes in Melih's room prefabs with
/// item spawn points. Each marker becomes an ItemSpawnPoint holding every item
/// prefab from the emergency kit recipe, so the room drops a random one of them
/// wherever a cube used to sit.
/// </summary>
public static class ItemSpawnPointSetup
{
    private const string RoomsFolder = "Assets/Prefabs Melihs/Rooms";
    private const string ItemVariantsFolder =
        "Assets/Gameplay/ItemSystem/Prefabs/Variants";
    private const string MarkerPrefix = "REPLACEWITHITEM";

    [MenuItem("Tools/Items/Replace Marker Cubes With Item Spawners",
        priority = 45)]
    private static void SetupMenu()
    {
        string report = ReplaceMarkers();

        Debug.Log("ItemSpawnPointSetup:\n" + report);
        EditorUtility.DisplayDialog("Item spawn points", report, "OK");
    }

    [MenuItem("Tools/Items/Check Item Spawn Points", priority = 46)]
    private static void CheckMenu()
    {
        string report = CheckRooms();

        Debug.Log("ItemSpawnPointSetup:\n" + report);
        EditorUtility.DisplayDialog("Item spawn points", report, "OK");
    }

    /// <summary>Swaps every marker cube for a spawn point. Safe to re-run.</summary>
    public static string ReplaceMarkers()
    {
        StringBuilder log = new StringBuilder();

        List<GameObject> items = LoadItemPrefabs(log);

        if (items.Count == 0)
        {
            log.AppendLine("ERROR: no item prefabs found in " +
                ItemVariantsFolder);
            return log.ToString();
        }

        log.AppendLine("item prefabs available: " + items.Count);

        if (!AssetDatabase.IsValidFolder(RoomsFolder))
        {
            log.AppendLine("ERROR: missing folder " + RoomsFolder);
            return log.ToString();
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab",
            new[] { RoomsFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);

            GameObject root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                List<Transform> markers = new List<Transform>();

                foreach (Transform candidate in
                    root.GetComponentsInChildren<Transform>(true))
                {
                    if (candidate == root.transform)
                        continue;

                    if (candidate.name.Trim().ToUpperInvariant()
                        .StartsWith(MarkerPrefix))
                    {
                        markers.Add(candidate);
                    }
                }

                if (markers.Count == 0)
                {
                    log.AppendLine("   " + name + ": no markers");
                    continue;
                }

                foreach (Transform marker in markers)
                {
                    GameObject spawner = new GameObject("ItemSpawn");
                    Transform parent = marker.parent;

                    spawner.transform.SetParent(parent, false);
                    spawner.transform.localPosition = marker.localPosition;
                    spawner.transform.localRotation = marker.localRotation;
                    spawner.transform.localScale = Vector3.one;

                    ItemSpawnPoint point =
                        spawner.AddComponent<ItemSpawnPoint>();

                    point.itemPrefabs = new List<GameObject>(items);

                    Object.DestroyImmediate(marker.gameObject);
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);

                log.AppendLine("   " + name + ": replaced " +
                    markers.Count + " marker(s)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return log.ToString();
    }

    /// <summary>Reports what is in the room prefabs right now.</summary>
    public static string CheckRooms()
    {
        StringBuilder log = new StringBuilder();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab",
            new[] { RoomsFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);

            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (root == null)
                continue;

            int markers = 0;
            ItemSpawnPoint[] points =
                root.GetComponentsInChildren<ItemSpawnPoint>(true);

            foreach (Transform candidate in
                root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name.Trim().ToUpperInvariant()
                    .StartsWith(MarkerPrefix))
                {
                    markers++;
                }
            }

            log.AppendLine(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0}: {1} spawn point(s), {2} marker cube(s) left, " +
                "pool size {3}",
                name, points.Length, markers,
                points.Length > 0 ? points[0].itemPrefabs.Count : 0));
        }

        return log.ToString();
    }

    private static List<GameObject> LoadItemPrefabs(StringBuilder log)
    {
        List<GameObject> items = new List<GameObject>();

        if (!AssetDatabase.IsValidFolder(ItemVariantsFolder))
        {
            log.AppendLine("MISSING FOLDER: " + ItemVariantsFolder);
            return items;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab",
            new[] { ItemVariantsFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
                continue;

            if (prefab.GetComponent<WorldItemPickup>() == null)
            {
                log.AppendLine("   skipped (not a pickup): " +
                    Path.GetFileNameWithoutExtension(path));
                continue;
            }

            items.Add(prefab);
        }

        return items;
    }
}
