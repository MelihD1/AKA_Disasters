
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Puts the item system into the open scene: an instance of the inventory HUD
/// prefab and the six collectible items laid out inside the house.
///
/// It only reads the assets under Assets/Gameplay/ItemSystem - nothing in that
/// folder is created or edited. Run it from Tools &gt; Items.
/// </summary>
public static class ItemSceneSetup
{
    private const string HudPrefabPath =
        "Assets/Gameplay/ItemSystem/UI/PF_InventoryHUD.prefab";

    private const string ItemsRootName = "ItemPickups";

    private static readonly string[] ItemPrefabPaths =
    {
        "Assets/Gameplay/ItemSystem/Prefabs/Variants/PF_Item_01.prefab",
        "Assets/Gameplay/ItemSystem/Prefabs/Variants/PF_Item_02.prefab",
        "Assets/Gameplay/ItemSystem/Prefabs/Variants/PF_Item_03.prefab",
        "Assets/Gameplay/ItemSystem/Prefabs/Variants/PF_Item_04.prefab",
        "Assets/Gameplay/ItemSystem/Prefabs/Variants/PF_Item_05.prefab",
        "Assets/Gameplay/ItemSystem/Prefabs/Variants/PF_Item_06.prefab"
    };

    [MenuItem("Tools/Items/Set Up Item System In Scene", priority = 30)]
    private static void SetupMenu()
    {
        string report = SetupActiveScene();

        Debug.Log("ItemSceneSetup:\n" + report);
        EditorUtility.DisplayDialog("Item system", report, "OK");
    }

    [MenuItem("Tools/Items/Check Item System", priority = 31)]
    private static void CheckMenu()
    {
        string report = CheckWiring();

        Debug.Log("ItemSceneSetup:\n" + report);
        EditorUtility.DisplayDialog("Item system", report, "OK");
    }

    /// <summary>
    /// Adds the HUD and the world items if they are not in the scene yet.
    /// Safe to run more than once.
    /// </summary>
    public static string SetupActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        StringBuilder log = new StringBuilder();

        if (!scene.IsValid() || !scene.isLoaded)
            return "ERROR: no active scene.";

        log.AppendLine("scene: " + scene.path);

        // --- HUD -----------------------------------------------------------
        InventoryHUD hud =
            Object.FindFirstObjectByType<InventoryHUD>();

        if (hud == null)
        {
            GameObject hudPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);

            if (hudPrefab == null)
            {
                log.AppendLine("ERROR: could not load " + HudPrefabPath);
                return log.ToString();
            }

            GameObject instance =
                PrefabUtility.InstantiatePrefab(hudPrefab, scene)
                    as GameObject;

            Undo.RegisterCreatedObjectUndo(instance, "Add inventory HUD");

            hud = instance.GetComponentInChildren<InventoryHUD>(true);

            log.AppendLine("added HUD: " + instance.name);
        }
        else
        {
            log.AppendLine("HUD already in scene: " + hud.gameObject.name);
        }

        // Point the HUD at the player's inventory and the slot prefab so it
        // does not have to go looking for them.
        PlayerInventory inventory =
            Object.FindFirstObjectByType<PlayerInventory>();

        if (hud != null && inventory != null)
        {
            SerializedObject hudObject = new SerializedObject(hud);

            SerializedProperty source =
                hudObject.FindProperty("playerInventory");

            if (source != null && source.objectReferenceValue == null)
            {
                source.objectReferenceValue = inventory;
                hudObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(hud);
                log.AppendLine("HUD -> PlayerInventory wired");
            }
        }

        if (inventory == null)
            log.AppendLine("WARNING: no PlayerInventory in the scene.");
        else if (inventory.ActiveRecipe == null)
            log.AppendLine("WARNING: PlayerInventory has no recipe assigned.");
        else
            log.AppendLine("recipe: " + inventory.ActiveRecipe.name);

        // --- World items ---------------------------------------------------
        GameObject root = GameObject.Find(ItemsRootName);

        if (root == null)
        {
            root = new GameObject(ItemsRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create item pickups root");
        }

        List<Vector3> used = new List<Vector3>();
        int added = 0;
        int skipped = 0;

        foreach (string path in ItemPrefabPaths)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
            {
                log.AppendLine("MISSING PREFAB: " + path);
                continue;
            }

            string simpleName = System.IO.Path.GetFileNameWithoutExtension(path);

            if (FindInScene(simpleName) != null)
            {
                skipped++;
                continue;
            }

            Vector3 spot;
            string note;

            if (!FindSpot(scene, used, out spot, out note))
            {
                log.AppendLine("no free spot left for " + simpleName +
                    " (" + note + ")");
                continue;
            }

            GameObject item =
                PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;

            item.transform.SetParent(root.transform, true);
            item.transform.position = spot;
            item.transform.rotation = Quaternion.Euler(
                0f, Random.Range(0f, 360f), 0f);

            used.Add(spot);
            added++;

            log.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "placed {0} at ({1:F2}, {2:F2}, {3:F2}) {4}",
                simpleName, spot.x, spot.y, spot.z, note));
        }

        log.AppendLine("items added: " + added + ", already there: " + skipped);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("saved: " + scene.path);

        return log.ToString();
    }

    /// <summary>Reports what the scene has right now.</summary>
    public static string CheckWiring()
    {
        StringBuilder log = new StringBuilder();
        Scene scene = SceneManager.GetActiveScene();

        log.AppendLine("scene: " + scene.path);

        InventoryHUD hud = Object.FindFirstObjectByType<InventoryHUD>();
        PlayerInventory inventory =
            Object.FindFirstObjectByType<PlayerInventory>();

        log.AppendLine("HUD in scene: " +
            (hud != null ? hud.gameObject.name : "NO"));

        if (hud != null)
        {
            SerializedObject hudObject = new SerializedObject(hud);

            log.AppendLine("HUD playerInventory: " +
                NameOf(hudObject.FindProperty("playerInventory")
                    .objectReferenceValue));

            log.AppendLine("HUD slot prefab: " +
                NameOf(hudObject.FindProperty("slotPrefab")
                    .objectReferenceValue));
        }

        log.AppendLine("PlayerInventory: " +
            (inventory != null ? inventory.name : "NO"));

        if (inventory != null)
        {
            log.AppendLine("recipe: " +
                (inventory.ActiveRecipe != null
                    ? inventory.ActiveRecipe.name
                    : "NONE"));
        }

        int items = 0;

        foreach (WorldItemPickup pickup in
            Object.FindObjectsByType<WorldItemPickup>(
                FindObjectsInactive.Include))
        {
            items++;
        }

        log.AppendLine("collectible items in scene: " + items);

        foreach (string path in ItemPrefabPaths)
        {
            string simpleName =
                System.IO.Path.GetFileNameWithoutExtension(path);

            log.AppendLine("  " + simpleName + ": " +
                (FindInScene(simpleName) != null ? "placed" : "not in scene"));
        }

        return log.ToString();
    }

    private static GameObject FindInScene(string name)
    {
        foreach (GameObject root in
            SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Transform t in
                root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith(name))
                    return t.gameObject;
            }
        }

        return null;
    }

    /// <summary>
    /// Looks for a clear patch of floor inside the house to drop an item on.
    /// </summary>
    private static bool FindSpot(
        Scene scene,
        List<Vector3> used,
        out Vector3 spot,
        out string note)
    {
        spot = Vector3.zero;
        note = "";

        Bounds house;
        float floorLevel;

        if (!TryMeasureHouse(out house, out floorLevel))
        {
            note = "no house found";
            return false;
        }

        float inset = 1.5f;
        float minX = house.min.x + inset;
        float maxX = house.max.x - inset;
        float minZ = house.min.z + inset;
        float maxZ = house.max.z - inset;

        if (maxX <= minX || maxZ <= minZ)
        {
            note = "house too small";
            return false;
        }

        // Sweep the floor in a loose grid and take the first spot that is
        // clear, at ground level and far enough from the items already placed.
        // The ray starts inside the room (below the ceiling) so it finds the
        // floor rather than the roof.
        const int steps = 6;

        float scanTop = floorLevel + 1.6f;
        float scanDistance = 3.2f;

        for (int i = 0; i <= steps; i++)
        {
            for (int j = 0; j <= steps; j++)
            {
                float x = Mathf.Lerp(minX, maxX, i / (float)steps);
                float z = Mathf.Lerp(minZ, maxZ, j / (float)steps);

                RaycastHit[] hits = Physics.RaycastAll(
                    new Vector3(x, scanTop, z),
                    Vector3.down,
                    scanDistance,
                    ~0,
                    QueryTriggerInteraction.Ignore);

                if (hits.Length == 0)
                    continue;

                System.Array.Sort(
                    hits, (a, b) => a.distance.CompareTo(b.distance));

                RaycastHit hit = hits[0];
                bool found = false;

                for (int h = 0; h < hits.Length; h++)
                {
                    if (hits[h].collider == null)
                        continue;

                    if (hits[h].collider.name.StartsWith("PlayerRoot") ||
                        hits[h].collider.transform.root.name.StartsWith("PlayerRoot"))
                        continue;

                    hit = hits[h];
                    found = true;
                    break;
                }

                if (!found)
                    continue;

                // Keep it near the floor: nothing on a high shelf.
                if (hit.point.y > floorLevel + 1.2f)
                    continue;

                Vector3 candidate = new Vector3(
                    hit.point.x, hit.point.y + 0.15f, hit.point.z);

                bool tooClose = false;

                foreach (Vector3 other in used)
                {
                    if (Vector3.Distance(
                            new Vector3(other.x, 0f, other.z),
                            new Vector3(candidate.x, 0f, candidate.z)) < 2.5f)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose)
                    continue;

                spot = candidate;
                note = "(on '" + hit.collider.name + "')";
                return true;
            }
        }

        note = "no clear floor spot";
        return false;
    }

    private static bool TryMeasureHouse(out Bounds bounds, out float floorLevel)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        floorLevel = 0f;

        Transform house = FloodSceneSetup.FindHouseRoot();

        if (house == null)
            return false;

        Collider[] colliders = house.GetComponentsInChildren<Collider>(true);

        if (colliders.Length == 0)
            return false;

        bool first = true;
        float largestArea = 0f;
        float lowestWideTop = float.MaxValue;

        foreach (Collider c in colliders)
        {
            if (c == null || c.isTrigger || c.bounds.size == Vector3.zero)
                continue;

            if (first)
            {
                bounds = c.bounds;
                first = false;
            }
            else
            {
                bounds.Encapsulate(c.bounds);
            }

            float area = c.bounds.size.x * c.bounds.size.z;

            if (area > largestArea)
                largestArea = area;
        }

        if (first)
            return false;

        foreach (Collider c in colliders)
        {
            if (c == null || c.isTrigger)
                continue;

            float area = c.bounds.size.x * c.bounds.size.z;

            if (area < largestArea * 0.5f)
                continue;

            if (c.bounds.max.y < lowestWideTop)
                lowestWideTop = c.bounds.max.y;
        }

        floorLevel = lowestWideTop == float.MaxValue
            ? bounds.min.y
            : lowestWideTop;

        return true;
    }

    private static string NameOf(Object item)
    {
        return item != null ? item.name : "NONE";
    }
}
