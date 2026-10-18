
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

            case "openscene":
                return OpenScene(parts.Length > 1 ? parts[1] : "");

            case "tune":
                return Tune();

            case "lights":
                return Lights();

            case "prefs":
                return Prefs(parts);

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

            case "upgradetest":
                return StartUpgradeTest();

            case "deathtest":
                return StartDeathTest();

            case "roomsetup":
                return "OK roomsetup\n" + RoomStreamerSetup.SetupActiveScene();

            case "rooms":
                return "OK rooms\n" + RoomStreamerSetup.ReportStreamer();

            case "roomtest":
                return StartRoomTest();

            case "prefabcheck":
                return "OK prefabcheck\n" + PrefabScaleFixer.Measure(
                    JoinArgs(parts, HallPrefabPath));

            case "prefabfix":
                return "OK prefabfix\n" + PrefabScaleFixer.BakeRootScale(
                    JoinArgs(parts, HallPrefabPath));

            case "spawnsetup":
                return "OK spawnsetup\n" + ItemSpawnPointSetup.ReplaceMarkers();

            case "spawncheck":
                return "OK spawncheck\n" + ItemSpawnPointSetup.CheckRooms();

            case "audiosetup":
                return "OK audiosetup\n" + DisasterAudioSetup.SetupActiveScene();

            case "overlaptest":
                return StartOverlapTest();

            case "uidump":
                return UiDump();

            case "tutorialsetup":
                return "OK tutorialsetup\n" + TutorialSetup.SetupActiveScene();

            case "tutorialcheck":
                return "OK tutorialcheck\n" + TutorialSetup.Check();

            case "tutorialtest":
                return StartTutorialTest();

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
            "hold={8:F1}s rise={9:F2} speed={10:F2} " +
            "o2={11:F1}s drain={12:F1}/s",
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
            flood.riseSpeed,
            flood.oxygenCapacity,
            flood.oxygenDrainPerSecond));

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
        Scene scene = SceneManager.GetActiveScene();
        StringBuilder log = new StringBuilder();

        // Sweep out every Hermes test rig, including any that got saved into
        // the scene by accident (a rig left in the scene runs on play and
        // starts disasters on its own).
        foreach (GameObject candidate in SceneManager.GetActiveScene()
            .GetRootGameObjects())
        {
            if (candidate == null || !candidate.name.EndsWith("TestRunner"))
                continue;

            log.AppendLine("removed " + candidate.name);
            Object.DestroyImmediate(candidate);
        }

        if (log.Length == 0)
            log.AppendLine("no test rigs in the scene");

        // The hand-placed test items from wiring up the item system: the rooms
        // now spawn their own, so these would just double up.
        GameObject itemRoot = GameObject.Find("ItemPickups");

        if (itemRoot != null)
        {
            log.AppendLine("removed ItemPickups (the rooms spawn items now)");
            Object.DestroyImmediate(itemRoot);
        }

        if (!scene.IsValid())
            return "OK cleanup";

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

        log.AppendLine("saved: " + scene.path);

        return "OK cleanup\n" + log.ToString();
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

    /// <summary>
    /// Shop state helper: reports it, snapshots it, restores it, or sets it.
    /// Used to put a player's real unlocks back after a test run.
    /// </summary>
    private const string PrefCurrencyKey = "EmergencyKitsCurrency";

    private static readonly string[] PrefUpgradeKeys =
    {
        "Upgrade_HardHat",
        "Upgrade_Crampons",
        "Upgrade_OxygenTank"
    };

    private const string PrefSnapshotFile = "hermes_pref_snapshot.txt";

    private static string Prefs(string[] parts)
    {
        string action = parts.Length > 1 ? parts[1].ToLowerInvariant() : "report";
        StringBuilder log = new StringBuilder();

        string path = LibraryPath(PrefSnapshotFile);

        if (action == "snapshot")
        {
            File.WriteAllText(path, SnapshotText());
            log.AppendLine("snapshot written to " + path);
        }
        else if (action == "restore")
        {
            if (!File.Exists(path))
                return "ERROR: no snapshot at " + path;

            string[] values = File.ReadAllText(path)
                .Trim()
                .Split(' ');

            if (values.Length < 1 + PrefUpgradeKeys.Length)
                return "ERROR: bad snapshot: " + File.ReadAllText(path);

            SetPrefs(values);
            log.AppendLine("shop state restored from " + path);
        }
        else if (action == "set")
        {
            if (parts.Length < 2 + 1 + PrefUpgradeKeys.Length)
                return "ERROR: prefs set <currency> <hardHat> <crampons> <oxygenTank>";

            SetPrefs(parts, 2);
            log.AppendLine("shop state set");
        }

        log.AppendLine("currency=" + GameProgress.Currency +
            " hardHat=" + GameProgress.HasHardHat +
            " crampons=" + GameProgress.HasCrampons +
            " oxygenTank=" + GameProgress.HasOxygenTank);

        return log.ToString();
    }

    private static string SnapshotText()
    {
        StringBuilder text = new StringBuilder();

        text.Append(PlayerPrefs.GetInt(PrefCurrencyKey, 0));

        foreach (string key in PrefUpgradeKeys)
            text.Append(" ").Append(PlayerPrefs.GetInt(key, 0));

        return text.ToString();
    }

    private static void SetPrefs(string[] values, int offset = 0)
    {
        PlayerPrefs.SetInt(
            PrefCurrencyKey,
            int.Parse(values[offset], CultureInfo.InvariantCulture));

        for (int i = 0; i < PrefUpgradeKeys.Length; i++)
        {
            PlayerPrefs.SetInt(
                PrefUpgradeKeys[i],
                int.Parse(values[offset + 1 + i], CultureInfo.InvariantCulture));
        }

        PlayerPrefs.Save();
    }

    /// <summary>
    /// Reports the lights in the open scene plus the shadow settings that
    /// decide whether soft shadows can actually be rendered.
    /// </summary>
    private static string Lights()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
            return "ERROR: no active scene";

        StringBuilder log = new StringBuilder();

        Light[] lights = Object.FindObjectsByType<Light>(
            FindObjectsInactive.Include);

        log.AppendLine("scene: " + scene.path);
        log.AppendLine("lights in scene: " + lights.Length);

        foreach (Light light in lights)
        {
            if (light == null)
                continue;

            Vector3 angles = light.transform.eulerAngles;

            log.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "  '{0}' type={1} enabled={2} intensity={3:F2} " +
                "color=({4:F2},{5:F2},{6:F2}) shadows={7} " +
                "strength={8:F2} bake={9} angles=({10:F1},{11:F1},{12:F1})",
                light.gameObject.name, light.type, light.enabled,
                light.intensity, light.color.r, light.color.g, light.color.b,
                light.shadows, light.shadowStrength, light.lightmapBakeType,
                angles.x, angles.y, angles.z));
        }

        int level = QualitySettings.GetQualityLevel();

        log.AppendLine(string.Format(
            CultureInfo.InvariantCulture,
            "quality level: {0} = '{1}' (of {2} levels)",
            level, QualitySettings.names[level],
            QualitySettings.names.Length - 1));

        UniversalRenderPipelineAsset pipeline =
            QualitySettings.renderPipeline as UniversalRenderPipelineAsset;

        if (pipeline == null)
        {
            pipeline = GraphicsSettings.defaultRenderPipeline
                as UniversalRenderPipelineAsset;
        }

        if (pipeline == null)
        {
            log.AppendLine("render pipeline: not URP");
            return log.ToString();
        }

        log.AppendLine("URP asset: " + pipeline.name);
        log.AppendLine("  main light shadows supported: " +
            pipeline.supportsMainLightShadows);
        log.AppendLine("  soft shadows supported: " +
            pipeline.supportsSoftShadows);
        log.AppendLine("  shadow distance: " +
            pipeline.shadowDistance + "m");
        log.AppendLine("  cascade count: " +
            pipeline.shadowCascadeCount);
        log.AppendLine("  main light shadowmap: " +
            pipeline.mainLightShadowmapResolution);
        log.AppendLine("  additional light shadows: " +
            pipeline.supportsAdditionalLightShadows);

        return log.ToString();
    }

    /// <summary>
    /// Pushes the current balance numbers into the open scene. A scene stores
    /// its own copy of every serialized field, so changing a script default
    /// alone does not change a scene that has already been built.
    /// </summary>
    private static string Tune()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
            return "ERROR: no active scene";

        StringBuilder log = new StringBuilder();

        FloodEvent flood = Object.FindFirstObjectByType<FloodEvent>();
        EarthquakeEvent quake = Object.FindFirstObjectByType<EarthquakeEvent>();
        TornadoEvent tornado = Object.FindFirstObjectByType<TornadoEvent>();

        if (flood != null)
        {
            log.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "flood.oxygenCapacity: {0:0.0}s -> 3.0s",
                flood.oxygenCapacity));

            flood.oxygenCapacity = 3f;
            EditorUtility.SetDirty(flood);
        }

        if (quake != null)
        {
            SerializedObject quakeObject = new SerializedObject(quake);

            log.AppendLine(SetFloat(quakeObject,
                "durationAtDifficulty0", 10f, "earthquake.deathDeadline@0"));
            log.AppendLine(SetFloat(quakeObject,
                "durationAtDifficulty1", 5f, "earthquake.deathDeadline@1"));

            quakeObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(quake);
        }

        if (tornado != null)
        {
            SerializedObject tornadoObject = new SerializedObject(tornado);

            log.AppendLine(SetFloat(tornadoObject,
                "slowdownAtDifficulty0", 0.75f, "tornado.playerSpeed@0"));
            log.AppendLine(SetFloat(tornadoObject,
                "slowdownAtDifficulty1", 0.4f, "tornado.playerSpeed@1"));

            tornadoObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tornado);
        }

        log.AppendLine("--- read back from the components ---");

        if (flood != null)
        {
            log.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "flood: oxygen {0:0.0}s, drain {1:0.0}/s",
                flood.oxygenCapacity, flood.oxygenDrainPerSecond));
        }

        if (quake != null)
        {
            log.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "earthquake: death deadline {0:0.0}s at difficulty 0, " +
                "{1:0.0}s at difficulty 1 (hard hat adds {2:0.0}s)",
                quake.durationAtDifficulty0, quake.durationAtDifficulty1,
                quake.hardHatExtraSeconds));
        }

        if (tornado != null)
        {
            SerializedObject tornadoObject = new SerializedObject(tornado);

            SerializedProperty speed0 =
                tornadoObject.FindProperty("slowdownAtDifficulty0");
            SerializedProperty speed1 =
                tornadoObject.FindProperty("slowdownAtDifficulty1");
            SerializedProperty crampons =
                tornadoObject.FindProperty("cramponSlowdownReduction");

            log.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "tornado: player keeps {0:0.00} of his speed at difficulty 0, " +
                "{1:0.00} at difficulty 1 (crampons cancel {2:0}% of the loss)",
                speed0 != null ? speed0.floatValue : -1f,
                speed1 != null ? speed1.floatValue : -1f,
                (crampons != null ? crampons.floatValue : 0f) * 100f));
        }

        InventoryHUD hud = Object.FindFirstObjectByType<InventoryHUD>();

        if (hud != null)
        {
            SerializedObject hudObject = new SerializedObject(hud);
            SerializedProperty hidden = hudObject.FindProperty("startHidden");

            if (hidden != null)
            {
                log.AppendLine("hud.startHidden: " + hidden.boolValue +
                    " -> False (always on screen)");

                hidden.boolValue = false;
                hudObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(hud);
            }
            else
            {
                log.AppendLine("hud.startHidden: property not found");
            }

            CanvasGroup group = hud.GetComponent<CanvasGroup>();

            if (group != null)
            {
                if (group.alpha <= 0f)
                {
                    group.alpha = 1f;
                    EditorUtility.SetDirty(group);
                }

                log.AppendLine("hud canvas alpha in the scene: " + group.alpha);
            }
        }
        else
        {
            log.AppendLine("hud: not in this scene");
        }

        // The water volume is built at run time. Keep the saved scene tidy so
        // it never looks like a flood is already running.
        if (flood != null && flood.waterVolume != null)
        {
            Renderer water = flood.waterVolume.GetComponent<Renderer>();

            if (water != null && water.enabled)
            {
                water.enabled = false;
                EditorUtility.SetDirty(water);
            }

            Vector3 scale = flood.waterVolume.localScale;
            flood.waterVolume.localScale = new Vector3(scale.x, 0f, scale.z);
            EditorUtility.SetDirty(flood.waterVolume);

            log.AppendLine("water volume hidden in the saved scene");
        }

        // Dev-only keys for testing the shop upgrades.
        if (Object.FindFirstObjectByType<DevTestKeys>(FindObjectsInactive.Include) == null)
        {
            GameObject devKeys = new GameObject("DevTestKeys");
            devKeys.AddComponent<DevTestKeys>();

            log.AppendLine("added DevTestKeys: F5 kits, F6 upgrades on, " +
                "F7 upgrades off, F8 shop state, 1/2/3 force disasters");
        }
        else
        {
            log.AppendLine("DevTestKeys already in the scene");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("saved: " + scene.path);

        return log.ToString();
    }

    private static string SetFloat(
        SerializedObject target, string name, float value, string label)
    {
        SerializedProperty property = target.FindProperty(name);

        if (property == null)
            return label + ": PROPERTY NOT FOUND";

        float before = property.floatValue;
        property.floatValue = value;

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}: {1:0.00} -> {2:0.00}",
            label, before, value);
    }

    /// <summary>
    /// Opens a scene for the tests. Refuses when the open scene has unsaved
    /// changes so nobody's work is thrown away.
    /// </summary>
    private static string OpenScene(string path)
    {
        if (string.IsNullOrEmpty(path))
            path = "Assets/Scenes/Gameplay.unity";

        if (!path.EndsWith(".unity"))
            path += ".unity";

        if (EditorApplication.isPlaying)
            return "REFUSED: in play mode, stop first";

        Scene current = SceneManager.GetActiveScene();

        if (current.path == path)
            return "OK openscene: already open " + path;

        if (current.isDirty)
            return "REFUSED: " + current.path +
                " has unsaved changes - save it first";

        Scene opened = EditorSceneManager.OpenScene(
            path, OpenSceneMode.Single);

        return "OK openscene: " + opened.path + " (was " + current.path + ")";
    }

    private static string StartUpgradeTest()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.isLoaded)
            return "ERROR: no loaded scene";

        if (Object.FindFirstObjectByType<EarthquakeEvent>() == null ||
            Object.FindFirstObjectByType<TornadoEvent>() == null ||
            Object.FindFirstObjectByType<FloodEvent>() == null)
        {
            return "ERROR: the open scene is missing a disaster script";
        }

        GameObject runner = GameObject.Find("UpgradeTestRunner");

        if (runner == null)
            runner = new GameObject("UpgradeTestRunner");

        UpgradeEffectTestRunner component =
            runner.GetComponent<UpgradeEffectTestRunner>();

        if (component == null)
            component = runner.AddComponent<UpgradeEffectTestRunner>();

        string reportPath = LibraryPath("hermes_upgrade_test_report.txt");

        if (File.Exists(reportPath))
            File.Delete(reportPath);

        if (File.Exists(reportPath + ".done"))
            File.Delete(reportPath + ".done");

        // Write down the player's real shop state first, in case the test
        // ends with a death and never gets to put it back itself.
        File.WriteAllText(LibraryPath(PrefSnapshotFile), SnapshotText());

        component.reportPath = reportPath;

        EditorUtility.SetDirty(component);

        EditorApplication.isPlaying = true;

        return "OK upgradetest: runner created, entering play mode\n" +
            "report: " + reportPath;
    }

    private static string StartDeathTest()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.isLoaded)
            return "ERROR: no loaded scene";

        if (Object.FindFirstObjectByType<GameOverManager>() == null ||
            Object.FindFirstObjectByType<PlayerInventory>() == null ||
            Object.FindFirstObjectByType<FloodEvent>() == null)
        {
            return "ERROR: the open scene is missing a script the test needs";
        }

        GameObject runner = GameObject.Find("DeathTestRunner");

        if (runner == null)
            runner = new GameObject("DeathTestRunner");

        DeathSummaryTestRunner component =
            runner.GetComponent<DeathSummaryTestRunner>();

        if (component == null)
            component = runner.AddComponent<DeathSummaryTestRunner>();

        string reportPath = LibraryPath("hermes_death_test_report.txt");

        if (File.Exists(reportPath))
            File.Delete(reportPath);

        if (File.Exists(reportPath + ".done"))
            File.Delete(reportPath + ".done");

        // The death banks a kit, so write down the real shop state first.
        File.WriteAllText(LibraryPath(PrefSnapshotFile), SnapshotText());

        component.reportPath = reportPath;

        EditorUtility.SetDirty(component);

        EditorApplication.isPlaying = true;

        return "OK deathtest: runner created, entering play mode\n" +
            "report: " + reportPath;
    }

    private const string HallPrefabPath =
        "Assets/Prefabs Melihs/Halls/Hall 1.prefab";

    /// <summary>Joins the rest of a command line, so paths with spaces work.</summary>
    private static string JoinArgs(string[] parts, string fallback)
    {
        if (parts.Length < 2)
            return fallback;

        string joined = string.Join(" ", parts, 1, parts.Length - 1).Trim();

        return string.IsNullOrEmpty(joined) ? fallback : joined;
    }

    private static string StartTutorialTest()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.isLoaded)
            return "ERROR: no loaded scene";

        if (Object.FindFirstObjectByType<TutorialMenu>(
            FindObjectsInactive.Include) == null)
        {
            return "ERROR: no TutorialMenu in the open scene";
        }

        GameObject runner = GameObject.Find("TutorialTestRunner");

        if (runner == null)
            runner = new GameObject("TutorialTestRunner");

        TutorialTestRunner component =
            runner.GetComponent<TutorialTestRunner>();

        if (component == null)
            component = runner.AddComponent<TutorialTestRunner>();

        string reportPath = LibraryPath("hermes_tutorial_test_report.txt");

        if (File.Exists(reportPath))
            File.Delete(reportPath);

        if (File.Exists(reportPath + ".done"))
            File.Delete(reportPath + ".done");

        component.reportPath = reportPath;

        EditorUtility.SetDirty(component);

        EditorApplication.isPlaying = true;

        return "OK tutorialtest: runner created, entering play mode\n" +
            "report: " + reportPath;
    }

    /// <summary>
    /// Lists the UI in the open scene: canvases, every button with what it
    /// calls, every text, and whether the tutorial scripts are present. Used to
    /// wire menus up without guessing.
    /// </summary>
    private static string UiDump()
    {
        Scene scene = SceneManager.GetActiveScene();
        StringBuilder log = new StringBuilder();

        log.AppendLine("scene: " + scene.path);

        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(
            FindObjectsInactive.Include))
        {
            log.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "canvas '{0}' renderMode={1} sortingOrder={2} active={3} scale={4}",
                canvas.name, canvas.renderMode, canvas.sortingOrder,
                canvas.gameObject.activeInHierarchy,
                canvas.transform.localScale));
        }

        log.AppendLine("--- buttons ---");

        foreach (Button button in Object.FindObjectsByType<Button>(
            FindObjectsInactive.Include))
        {
            StringBuilder calls = new StringBuilder();

            int count = button.onClick.GetPersistentEventCount();

            for (int i = 0; i < count; i++)
            {
                Object target = button.onClick.GetPersistentTarget(i);

                calls.Append(target != null ? target.GetType().Name : "null")
                    .Append('.')
                    .Append(button.onClick.GetPersistentMethodName(i))
                    .Append(' ');
            }

            log.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "  '{0}' active={1} parent='{2}' onClick=[{3}]",
                button.name, button.gameObject.activeInHierarchy,
                button.transform.parent != null
                    ? button.transform.parent.name
                    : "-",
                calls.ToString().Trim()));
        }

        log.AppendLine("--- texts ---");

        foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>(
            FindObjectsInactive.Include))
        {
            string content = text.text != null && text.text.Length > 50
                ? text.text.Substring(0, 50) + "..."
                : text.text;

            log.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "  '{0}' active={1} text=\"{2}\"",
                text.name, text.gameObject.activeInHierarchy,
                content != null ? content.Replace("\n", " / ") : ""));
        }

        log.AppendLine("--- scripts ---");

        TutorialMenu tutorial = Object.FindFirstObjectByType<TutorialMenu>(
            FindObjectsInactive.Include);

        log.AppendLine("TutorialMenu: " + (tutorial != null
            ? "on '" + tutorial.gameObject.name + "'"
            : "NOT IN THIS SCENE"));

        MainMenuManager menu = Object.FindFirstObjectByType<MainMenuManager>(
            FindObjectsInactive.Include);

        if (menu == null)
        {
            log.AppendLine("MainMenuManager: not in this scene");
            return log.ToString();
        }

        SerializedObject menuObject = new SerializedObject(menu);

        foreach (string field in new[]
        {
            "settingsPanel", "volumeSlider", "volumeValue"
        })
        {
            SerializedProperty property = menuObject.FindProperty(field);

            log.AppendLine("MainMenuManager." + field + " = " +
                (property != null && property.objectReferenceValue != null
                    ? property.objectReferenceValue.name
                    : "NONE"));
        }

        return log.ToString();
    }

    private static string StartOverlapTest()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.isLoaded)
            return "ERROR: no loaded scene";

        if (Object.FindFirstObjectByType<EarthquakeEvent>() == null ||
            Object.FindFirstObjectByType<FloodEvent>() == null)
        {
            return "ERROR: the open scene needs both an earthquake and a flood";
        }

        GameObject runner = GameObject.Find("OverlapTestRunner");

        if (runner == null)
            runner = new GameObject("OverlapTestRunner");

        DisasterOverlapTestRunner component =
            runner.GetComponent<DisasterOverlapTestRunner>();

        if (component == null)
            component = runner.AddComponent<DisasterOverlapTestRunner>();

        string reportPath = LibraryPath("hermes_overlap_test_report.txt");

        if (File.Exists(reportPath))
            File.Delete(reportPath);

        if (File.Exists(reportPath + ".done"))
            File.Delete(reportPath + ".done");

        component.reportPath = reportPath;

        EditorUtility.SetDirty(component);

        EditorApplication.isPlaying = true;

        return "OK overlaptest: runner created, entering play mode\n" +
            "report: " + reportPath;
    }

    private static string StartRoomTest()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.isLoaded)
            return "ERROR: no loaded scene";

        if (Object.FindFirstObjectByType<RoomStreamer>() == null)
        {
            return "ERROR: no RoomStreamer in the open scene " +
                "(run the 'roomsetup' command first)";
        }

        GameObject runner = GameObject.Find("RoomTestRunner");

        if (runner == null)
            runner = new GameObject("RoomTestRunner");

        RoomStreamTestRunner component =
            runner.GetComponent<RoomStreamTestRunner>();

        if (component == null)
            component = runner.AddComponent<RoomStreamTestRunner>();

        string reportPath = LibraryPath("hermes_room_test_report.txt");

        if (File.Exists(reportPath))
            File.Delete(reportPath);

        if (File.Exists(reportPath + ".done"))
            File.Delete(reportPath + ".done");

        component.reportPath = reportPath;

        EditorUtility.SetDirty(component);

        EditorApplication.isPlaying = true;

        return "OK roomtest: runner created, entering play mode\n" +
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

