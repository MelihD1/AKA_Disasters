
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor tool that puts the flood rig into the open scene:
/// a translucent water box plus a FloodEvent manager wired up to the player
/// and the house. Run it from Tools > Flood, or call
/// SetupActiveScene() from another editor script.
/// </summary>
public static class FloodSceneSetup
{
    public const string WaterName = "FloodWater";
    public const string ManagerName = "FloodManager";

    /// <summary>Thin preview slab so the water is visible in the editor.</summary>
    private const float PreviewThickness = 0.25f;

    [MenuItem("Tools/Flood/Create or Update Flood System", priority = 10)]
    private static void CreateOrUpdateMenu()
    {
        string report = SetupActiveScene();

        Debug.Log("FloodSceneSetup:\n" + report);

        EditorUtility.DisplayDialog(
            "Flood system",
            report,
            "OK");
    }

    [MenuItem("Tools/Flood/Recalculate Water Peak Height", priority = 11)]
    private static void RecalculateMenu()
    {
        FloodEvent flood = Object.FindFirstObjectByType<FloodEvent>();

        if (flood == null)
        {
            EditorUtility.DisplayDialog(
                "Flood system",
                "No FloodEvent found in the open scene.",
                "OK");
            return;
        }

        Undo.RecordObject(flood, "Recalculate Flood Peak");

        float peak = flood.RecalculatePeakHeight();

        EditorUtility.SetDirty(flood);
        EditorSceneManager.MarkSceneDirty(flood.gameObject.scene);

        EditorUtility.DisplayDialog(
            "Flood system",
            string.Format(
                CultureInfo.InvariantCulture,
                "Floor: {0:F2}\nFurniture top: {1:F2}\nEye height: {2:F2}\n" +
                "Peak water level: {3:F2}",
                flood.FloorLevel,
                flood.StandableFurnitureTop,
                flood.EyeHeight,
                peak),
            "OK");
    }

    [MenuItem("Tools/Flood/Remove Flood System", priority = 12)]
    private static void RemoveMenu()
    {
        Scene scene = SceneManager.GetActiveScene();

        List<string> removed = new List<string>();

        FloodEvent flood = Object.FindFirstObjectByType<FloodEvent>();

        if (flood != null)
        {
            Undo.DestroyObjectImmediate(flood.gameObject);
            removed.Add(ManagerName);
        }

        GameObject water = GameObject.Find(WaterName);

        if (water != null)
        {
            Undo.DestroyObjectImmediate(water);
            removed.Add(WaterName);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        EditorUtility.DisplayDialog(
            "Flood system",
            removed.Count == 0
                ? "Nothing to remove."
                : "Removed: " + string.Join(", ", removed),
            "OK");
    }

    /// <summary>
    /// Builds (or refreshes) the flood rig in the active scene.
    /// Returns a human readable report of what it measured.
    /// </summary>
    public static string SetupActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
            return "ERROR: no active scene.";

        StringBuilder log = new StringBuilder();

        log.AppendLine("scene: " + scene.path);

        // --- Player --------------------------------------------------------
        EarthquakeEvent earthquake =
            Object.FindFirstObjectByType<EarthquakeEvent>();

        Transform playerRoot = null;

        if (earthquake != null && earthquake.playerRoot != null)
            playerRoot = earthquake.playerRoot;

        if (playerRoot == null)
        {
            GameObject found = GameObject.Find("PlayerRoot");

            if (found != null)
                playerRoot = found.transform;
        }

        CharacterController controller = playerRoot != null
            ? playerRoot.GetComponent<CharacterController>()
            : null;

        Camera camera = earthquake != null && earthquake.playerCamera != null
            ? earthquake.playerCamera
            : Camera.main;

        log.AppendLine("playerRoot: " +
            (playerRoot != null ? playerRoot.name : "NOT FOUND"));
        log.AppendLine("playerCamera: " +
            (camera != null ? camera.name : "NOT FOUND"));

        // --- House ---------------------------------------------------------
        Transform houseRoot = FindHouseRoot();

        log.AppendLine("houseRoot: " +
            (houseRoot != null ? houseRoot.name : "NOT FOUND"));

        if (houseRoot == null)
            return log.AppendLine(
                "ERROR: could not find the house. Nothing was changed.")
                .ToString();

        // --- Water ---------------------------------------------------------
        GameObject water = GameObject.Find(WaterName);

        if (water == null)
        {
            water = GameObject.CreatePrimitive(PrimitiveType.Cube);
            water.name = WaterName;

            Collider box = water.GetComponent<Collider>();

            if (box != null)
                Object.DestroyImmediate(box);

            Undo.RegisterCreatedObjectUndo(water, "Create Flood Water");
        }

        Renderer waterRenderer = water.GetComponent<Renderer>();

        // --- Manager -------------------------------------------------------
        GameObject manager = GameObject.Find(ManagerName);

        if (manager == null)
        {
            manager = new GameObject(ManagerName);
            Undo.RegisterCreatedObjectUndo(manager, "Create Flood Manager");
        }

        FloodEvent flood = manager.GetComponent<FloodEvent>();

        if (flood == null)
            flood = manager.AddComponent<FloodEvent>();

        flood.playerRoot = playerRoot;
        flood.playerController = controller;
        flood.playerCamera = camera;
        flood.waterVolume = water.transform;
        flood.waterRenderer = waterRenderer;
        flood.houseRoot = houseRoot;
        flood.waterMaterial = null;

        flood.MeasureScene();

        // Keep the water parked at floor level, thin, as an editor preview.
        // The preview keeps the house footprint: in play mode the rectangle
        // grows to followSize and follows the player.
        float previewHeight = PreviewThickness;

        water.transform.localScale = new Vector3(
            flood.WaterFootprint.x,
            previewHeight,
            flood.WaterFootprint.z);

        water.transform.position = new Vector3(
            flood.HouseCenter.x,
            flood.FloorLevel + previewHeight * 0.5f,
            flood.HouseCenter.z);

        if (waterRenderer != null)
        {
            Material current = waterRenderer.sharedMaterial;
            bool ours = current != null &&
                current.name.StartsWith("FloodWater");

            if (ours)
            {
                FloodEvent.ConfigureWaterMaterial(current, flood.waterColor);
            }
            else
            {
                // Never touch the built-in primitive material, it belongs to
                // the render pipeline package.
                waterRenderer.sharedMaterial =
                    FloodEvent.CreateWaterMaterial(flood.waterColor);
            }

            waterRenderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            waterRenderer.receiveShadows = false;
        }

        EditorUtility.SetDirty(flood);
        EditorUtility.SetDirty(manager);

        if (waterRenderer != null)
            EditorUtility.SetDirty(waterRenderer);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        log.AppendLine(string.Format(
            CultureInfo.InvariantCulture,
            "floorLevel: {0:F3}", flood.FloorLevel));
        log.AppendLine(string.Format(
            CultureInfo.InvariantCulture,
            "furnitureTop: {0:F3}", flood.StandableFurnitureTop));
        log.AppendLine(string.Format(
            CultureInfo.InvariantCulture,
            "eyeHeight: {0:F3}", flood.EyeHeight));
        log.AppendLine(string.Format(
            CultureInfo.InvariantCulture,
            "peakWaterLevel: {0:F3}", flood.PeakWaterLevel));
        log.AppendLine(string.Format(
            CultureInfo.InvariantCulture,
            "footprint: {0:F2} x {1:F2}",
            flood.WaterFootprint.x, flood.WaterFootprint.z));
        log.AppendLine(string.Format(
            CultureInfo.InvariantCulture,
            "houseCenter: {0:F2}, {1:F2}, {2:F2}",
            flood.HouseCenter.x, flood.HouseCenter.y, flood.HouseCenter.z));
        log.AppendLine("saved: " + scene.path);

        return log.ToString();
    }

    /// <summary>
    /// Picks the object whose colliders take up the most space: the house.
    /// </summary>
    public static Transform FindHouseRoot()
    {
        Scene scene = SceneManager.GetActiveScene();
        Transform best = null;
        float bestVolume = 0f;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == WaterName || root.name == ManagerName)
                continue;

            Collider[] colliders =
                root.GetComponentsInChildren<Collider>(true);

            if (colliders.Length == 0)
                continue;

            Bounds total = new Bounds(root.transform.position, Vector3.zero);
            bool first = true;

            foreach (Collider c in colliders)
            {
                if (c == null || c.isTrigger)
                    continue;

                if (first)
                {
                    total = c.bounds;
                    first = false;
                }
                else
                {
                    total.Encapsulate(c.bounds);
                }
            }

            if (first)
                continue;

            float volume = total.size.x * total.size.y * total.size.z;

            if (volume > bestVolume)
            {
                bestVolume = volume;
                best = root.transform;
            }
        }

        return best;
    }
}
