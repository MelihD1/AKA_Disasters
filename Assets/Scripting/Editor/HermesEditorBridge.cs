
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tiny file based command channel so an external tool (Hermes) can drive the
/// open editor: write &lt;project&gt;/Library/hermes_bridge_request.txt, the
/// bridge runs the command and writes Library/hermes_bridge_response.txt.
///
/// It does nothing until a request file shows up, and it is editor only, so it
/// can be deleted at any time without touching the game.
/// </summary>
[InitializeOnLoad]
public static class HermesEditorBridge
{
    private const string RequestFile = "hermes_bridge_request.txt";
    private const string ResponseFile = "hermes_bridge_response.txt";
    private const string TestConfigFile = "hermes_flood_test_config.txt";
    private const string TestReportFile = "hermes_flood_test_report.txt";
    private const string TestShotFile = "hermes_flood_shot.png";

    private static double nextPoll;

    static HermesEditorBridge()
    {
        EditorApplication.update += Poll;
    }

    private static string ProjectRoot
    {
        get
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }
    }

    private static string LibraryPath(string file)
    {
        return Path.Combine(
            Path.Combine(ProjectRoot, "Library"),
            file);
    }

    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll)
            return;

        nextPoll = EditorApplication.timeSinceStartup + 0.5;

        string requestPath = LibraryPath(RequestFile);

        if (!File.Exists(requestPath))
            return;

        string request = "";

        try
        {
            request = File.ReadAllText(requestPath);
            File.Delete(requestPath);
        }
        catch (System.Exception)
        {
            return;
        }

        string response;

        try
        {
            response = Handle(request.Trim());
        }
        catch (System.Exception e)
        {
            response = "ERROR: " + e.GetType().Name + ": " + e.Message;
        }

        try
        {
            File.WriteAllText(LibraryPath(ResponseFile), response);
        }
        catch (System.Exception e)
        {
            Debug.LogError("HermesEditorBridge: " + e.Message);
        }
    }

    private static string Handle(string request)
    {
        string[] parts = request.Split(' ');
        string command = parts.Length > 0 ? parts[0].ToLowerInvariant() : "";

        switch (command)
        {
            case "ping":
                return "OK ping";

            case "report":
                return "OK report\n" + SceneReport();

            case "setup":
                return "OK setup\n" + FloodSceneSetup.SetupActiveScene();

            case "recalc":
                return Recalc();

            case "play":
                EditorApplication.isPlaying = true;
                return "OK play: entering play mode";

            case "stop":
                EditorApplication.isPlaying = false;
                return "OK stop: leaving play mode";

            case "test":
                return StartTest();

            case "cleanup":
                return Cleanup();

            case "disasters":
                return "OK disasters\n" +
                    DisasterSceneSetup.SetupActiveScene();

            case "wire":
                return "OK wire\n" + DisasterSceneSetup.WireDisasters();

            case "wiring":
                return "OK wiring\n" + DisasterSceneSetup.CheckWiring();

            case "disastertest":
                return StartDisasterTest();

            case "itemsetup":
                return "OK itemsetup\n" + ItemSceneSetup.SetupActiveScene();

            case "itemcheck":
                return "OK itemcheck\n" + ItemSceneSetup.CheckWiring();

            case "itemtest":
                return StartItemTest();

            case "probe":
                return "OK probe\n" + Probe();

            case "pause":
                EditorApplication.isPaused = !EditorApplication.isPaused;
                return "OK pause: " + EditorApplication.isPaused;

            default:
                return "ERROR: unknown command '" + command + "'";
        }
    }

    private static string SceneReport()
    {
        StringBuilder text = new StringBuilder();

        Scene scene = SceneManager.GetActiveScene();

        text.AppendLine("scene: " + scene.path);
        text.AppendLine("isPlaying: " + EditorApplication.isPlaying);
        text.AppendLine("isPlayingOrWillChange: " +
            EditorApplication.isPlayingOrWillChangePlaymode);
        text.AppendLine("sceneIsDirty: " + scene.isDirty);
        text.AppendLine("timeScale: " +
            Time.timeScale.ToString("F2", CultureInfo.InvariantCulture));

        FloodEvent flood = Object.FindFirstObjectByType<FloodEvent>();

        if (flood == null)
        {
            text.AppendLine("flood: none");
            return text.ToString();
        }

        text.AppendLine(string.Format(
            CultureInfo.InvariantCulture,
            "flood: state={0} lost={1} water={2:F3} peak={3:F3} " +
            "floor={4:F3} furniture={5:F3} eye={6:F3} floods={7} " +
            "hold={8:F1}s rise={9:F2} speed={10:F2}",
            flood.State,
            flood.PlayerLost,
            flood.WaterLevel,
            flood.PeakWaterLevel,
            flood.FloorLevel,
            flood.StandableFurnitureTop,
            flood.EyeHeight,
            flood.FloodsCompleted,
            flood.peakHoldDuration,
            flood.waterRise,
            flood.riseSpeed));

        text.AppendLine("objects: waterVolume=" +
            (flood.waterVolume != null ? flood.waterVolume.name : "null") +
            " houseRoot=" +
            (flood.houseRoot != null ? flood.houseRoot.name : "null") +
            " playerRoot=" +
            (flood.playerRoot != null ? flood.playerRoot.name : "null"));

        if (flood.waterRenderer != null)
        {
            Bounds b = flood.waterRenderer.bounds;

            text.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "waterBounds: min=({0:F2}, {1:F2}, {2:F2}) " +
                "max=({3:F2}, {4:F2}, {5:F2}) enabled={6}",
                b.min.x, b.min.y, b.min.z,
                b.max.x, b.max.y, b.max.z,
                flood.waterRenderer.enabled));
        }

        if (flood.playerRoot != null)
        {
            Vector3 p = flood.playerRoot.position;

            text.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "player: ({0:F2}, {1:F2}, {2:F2})",
                p.x, p.y, p.z));
        }

        if (flood.playerCamera != null)
        {
            Vector3 p = flood.playerCamera.transform.position;

            text.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "camera: ({0:F2}, {1:F2}, {2:F2}) submersion={3:F3}",
                p.x, p.y, p.z,
                flood.SubmersionDepth));
        }

        DisasterDifficultyManager manager =
            Object.FindFirstObjectByType<DisasterDifficultyManager>();

        if (manager != null)
        {
            text.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "difficulty: {0:F3}  now={1}  next={2:F1}s  started={3}  " +
                "running={4}  playerLost={5}",
                manager.Difficulty,
                manager.CurrentDisaster,
                manager.SecondsUntilNext,
                manager.DisastersStarted,
                manager.Running,
                manager.PlayerLost));
        }
        else
        {
            text.AppendLine("difficulty: no manager in the scene");
        }

        PlayerInventory inventory =
            Object.FindFirstObjectByType<PlayerInventory>();

        if (inventory == null)
        {
            text.AppendLine("inventory: none");
        }
        else
        {
            text.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "inventory: merged={0} recipe={1}",
                inventory.MergedItemCount,
                inventory.ActiveRecipe != null
                    ? inventory.ActiveRecipe.name
                    : "NONE"));

            foreach (ItemData item in LoadAllItems())
            {
                text.AppendLine("   " + item.displayName + " (" +
                    item.itemID + "): " + inventory.GetItemCount(item));
            }
        }

        InventoryHUD hud = Object.FindFirstObjectByType<InventoryHUD>();

        if (hud == null)
        {
            text.AppendLine("hud: none");
        }
        else
        {
            CanvasGroup group = hud.GetComponent<CanvasGroup>();

            text.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "hud: {0} alpha={1:F2} blocksRaycasts={2}",
                hud.gameObject.name,
                group != null ? group.alpha : -1f,
                group != null ? group.blocksRaycasts : false));
        }

        return text.ToString();
    }

    private static string Recalc()
    {
        FloodEvent flood = Object.FindFirstObjectByType<FloodEvent>();

        if (flood == null)
            return "ERROR: no FloodEvent in the open scene";

        float peak = flood.RecalculatePeakHeight();

        EditorUtility.SetDirty(flood);
        EditorSceneManager.MarkSceneDirty(flood.gameObject.scene);

        return string.Format(
            CultureInfo.InvariantCulture,
            "OK recalc\nfloor: {0:F3}\nfurnitureTop: {1:F3}\n" +
            "eyeHeight: {2:F3}\npeakWaterLevel: {3:F3}",
            flood.FloorLevel,
            flood.StandableFurnitureTop,
            flood.EyeHeight,
            peak);
    }

    private static string StartTest()
    {
        Scene scene = SceneManager.GetActiveScene();

        FloodEvent flood = Object.FindFirstObjectByType<FloodEvent>();

        if (flood == null)
            return "ERROR: no FloodEvent in the open scene";

        if (!scene.isLoaded)
            return "ERROR: no loaded scene";

        GameObject runner = GameObject.Find("FloodTestRunner");

        if (runner == null)
            runner = new GameObject("FloodTestRunner");

        FloodPlayTestRunner component =
            runner.GetComponent<FloodPlayTestRunner>();

        if (component == null)
            component = runner.AddComponent<FloodPlayTestRunner>();

        string reportPath = LibraryPath(TestReportFile);

        // Start from a clean report.
        if (File.Exists(reportPath))
            File.Delete(reportPath);

        if (File.Exists(reportPath + ".done"))
            File.Delete(reportPath + ".done");

        component.configPath = LibraryPath(TestConfigFile);
        component.reportPath = reportPath;
        component.shotPath = LibraryPath("hermes_flood_shot_waist.png");
        component.shotPeakPath = LibraryPath("hermes_flood_shot_peak.png");
        component.shotUnderPath = LibraryPath("hermes_flood_shot_under.png");

        // Drop stale screenshots so an old one is never mistaken for new.
        foreach (string shot in new string[]
        {
            component.shotPath,
            component.shotPeakPath,
            component.shotUnderPath
        })
        {
            if (File.Exists(shot))
                File.Delete(shot);
        }

        File.WriteAllText(
            component.configPath,
            "mode=run\nreport=" + reportPath + "\n");

        EditorUtility.SetDirty(component);

        EditorApplication.isPlaying = true;

        return "OK test: runner created, entering play mode\nreport: " +
            reportPath;
    }

    private static string Cleanup()
    {
        GameObject runner = GameObject.Find("FloodTestRunner");

        if (runner != null)
            Object.DestroyImmediate(runner);

        GameObject disasterRunner = GameObject.Find("DisasterTestRunner");

        if (disasterRunner != null)
            Object.DestroyImmediate(disasterRunner);

        GameObject itemRunner = GameObject.Find("ItemTestRunner");

        if (itemRunner != null)
            Object.DestroyImmediate(itemRunner);

        return "OK cleanup";
    }

    private static string StartDisasterTest()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.isLoaded)
            return "ERROR: no loaded scene";

        DisasterDifficultyManager manager =
            Object.FindFirstObjectByType<DisasterDifficultyManager>();

        if (manager == null)
            return "ERROR: no DisasterDifficultyManager in the open scene " +
                "(run the 'disasters' command first)";

        GameObject runner = GameObject.Find("DisasterTestRunner");

        if (runner == null)
            runner = new GameObject("DisasterTestRunner");

        DisasterPlayTestRunner component =
            runner.GetComponent<DisasterPlayTestRunner>();

        if (component == null)
            component = runner.AddComponent<DisasterPlayTestRunner>();

        string reportPath = LibraryPath("hermes_disaster_test_report.txt");

        if (File.Exists(reportPath))
            File.Delete(reportPath);

        if (File.Exists(reportPath + ".done"))
            File.Delete(reportPath + ".done");

        component.reportPath = reportPath;
        component.testSeconds = 70f;

        EditorUtility.SetDirty(component);

        EditorApplication.isPlaying = true;

        return "OK disastertest: runner created, entering play mode\n" +
            "report: " + reportPath;
    }

    private static string StartItemTest()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.isLoaded)
            return "ERROR: no loaded scene";

        if (Object.FindFirstObjectByType<PlayerInventory>() == null)
            return "ERROR: no PlayerInventory in the open scene";

        GameObject runner = GameObject.Find("ItemTestRunner");

        if (runner == null)
            runner = new GameObject("ItemTestRunner");

        ItemPlayTestRunner component =
            runner.GetComponent<ItemPlayTestRunner>();

        if (component == null)
            component = runner.AddComponent<ItemPlayTestRunner>();

        string reportPath = LibraryPath("hermes_item_test_report.txt");

        if (File.Exists(reportPath))
            File.Delete(reportPath);

        if (File.Exists(reportPath + ".done"))
            File.Delete(reportPath + ".done");

        component.reportPath = reportPath;

        EditorUtility.SetDirty(component);

        EditorApplication.isPlaying = true;

        return "OK itemtest: runner created, entering play mode\n" +
            "report: " + reportPath;
    }

    /// <summary>Every ItemData asset in the project.</summary>
    private static List<ItemData> LoadAllItems()
    {
        List<ItemData> items = new List<ItemData>();

        foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);

            if (item != null)
                items.Add(item);
        }

        return items;
    }

    /// <summary>
    /// Dumps what the flood system can see in the house: every collider with
    /// its world bounds, largest surfaces first, so the measured floor and
    /// furniture can be checked by hand.
    /// </summary>
    private static string Probe()
    {
        FloodEvent flood = Object.FindFirstObjectByType<FloodEvent>();

        if (flood == null || flood.houseRoot == null)
            return "ERROR: no FloodEvent / houseRoot";

        Collider[] colliders =
            flood.houseRoot.GetComponentsInChildren<Collider>(true);

        System.Collections.Generic.List<Collider> list =
            new System.Collections.Generic.List<Collider>();

        foreach (Collider c in colliders)
        {
            if (c != null && !c.isTrigger && c.bounds.size != Vector3.zero)
                list.Add(c);
        }

        StringBuilder text = new StringBuilder();

        text.AppendLine("houseRoot: " + flood.houseRoot.name);
        text.AppendLine("colliders: " + list.Count);
        text.AppendLine(string.Format(
            CultureInfo.InvariantCulture,
            "measured: floor={0:F3} ({1}) furniture={2:F3} ({3}) " +
            "peak={4:F3}",
            flood.FloorLevel,
            flood.FloorColliderName,
            flood.StandableFurnitureTop,
            flood.TallestStandableName,
            flood.PeakWaterLevel));

        text.AppendLine("--- biggest top surfaces (floor candidates) ---");

        list.Sort((a, b) =>
            (b.bounds.size.x * b.bounds.size.z).CompareTo(
                a.bounds.size.x * a.bounds.size.z));

        for (int i = 0; i < list.Count && i < 8; i++)
        {
            Bounds b = list[i].bounds;

            text.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0,-28} area={1,7:F2} top={2,7:F3} min=({3:F2},{4:F2},{5:F2})",
                list[i].name,
                b.size.x * b.size.z,
                b.max.y,
                b.min.x, b.min.y, b.min.z));
        }

        text.AppendLine("--- highest colliders ---");

        list.Sort((a, b) => b.bounds.max.y.CompareTo(a.bounds.max.y));

        for (int i = 0; i < list.Count && i < 12; i++)
        {
            Bounds b = list[i].bounds;

            text.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0,-28} top={1,7:F3} bottom={2,7:F3} aboveFloor={3,6:F3}",
                list[i].name,
                b.max.y,
                b.min.y,
                b.max.y - flood.FloorLevel));
        }

        text.AppendLine("--- standable surfaces, highest first ---");

        List<Collider> standable = new List<Collider>();

        foreach (Collider c in list)
        {
            float height = c.bounds.max.y - flood.FloorLevel;

            if (height >= flood.standableMinHeight &&
                height <= flood.standableMaxHeight)
                standable.Add(c);
        }

        standable.Sort((a, b) => b.bounds.max.y.CompareTo(a.bounds.max.y));

        for (int i = 0; i < standable.Count && i < 14; i++)
        {
            Bounds b = standable[i].bounds;

            text.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0,-28} top={1,7:F3} height={2,6:F3} " +
                "centerXZ=({3:F2}, {4:F2})",
                standable[i].name,
                b.max.y,
                b.max.y - flood.FloorLevel,
                b.center.x,
                b.center.z));
        }

        text.AppendLine("standable count: " + standable.Count);

        return text.ToString();
    }

    private static string FloatingText(string name, Collider c)
    {
        Bounds b = c.bounds;

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}: min=({1:F2}, {2:F2}, {3:F2}) max=({4:F2}, {5:F2}, {6:F2})",
            name, b.min.x, b.min.y, b.min.z, b.max.x, b.max.y, b.max.z);
    }
}

