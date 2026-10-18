
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Puts the DisasterDifficultyManager into the open scene and wires it to the
/// three disasters. Run it from Tools &gt; Disasters, or call
/// SetupActiveScene() from another editor script.
/// </summary>
public static class DisasterSceneSetup
{
    public const string ManagerName = "DisasterDifficultyManager";

    [MenuItem("Tools/Disasters/Create or Update Difficulty Manager", priority = 20)]
    private static void CreateOrUpdateMenu()
    {
        string report = SetupActiveScene();

        Debug.Log("DisasterSceneSetup:\n" + report);

        EditorUtility.DisplayDialog("Disaster difficulty", report, "OK");
    }

    [MenuItem("Tools/Disasters/Check Disaster Wiring", priority = 21)]
    private static void CheckMenu()
    {
        EditorUtility.DisplayDialog(
            "Disaster difficulty",
            CheckWiring(),
            "OK");
    }

    [MenuItem("Tools/Disasters/Wire Disaster References", priority = 22)]
    private static void WireMenu()
    {
        string report = WireDisasters();

        Debug.Log("DisasterSceneSetup:\n" + report);

        EditorUtility.DisplayDialog("Disaster difficulty", report, "OK");
    }

    /// <summary>
    /// Builds (or refreshes) the difficulty manager in the active scene.
    /// Returns a readable report.
    /// </summary>
    public static string SetupActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
            return "ERROR: no active scene.";

        StringBuilder log = new StringBuilder();

        log.AppendLine("scene: " + scene.path);

        EarthquakeEvent earthquake =
            Object.FindFirstObjectByType<EarthquakeEvent>(
                FindObjectsInactive.Include);

        FloodEvent flood =
            Object.FindFirstObjectByType<FloodEvent>(
                FindObjectsInactive.Include);

        TornadoEvent tornado =
            Object.FindFirstObjectByType<TornadoEvent>(
                FindObjectsInactive.Include);

        log.AppendLine("earthquake: " + NameOf(earthquake));
        log.AppendLine("flood: " + NameOf(flood));
        log.AppendLine("tornado: " + NameOf(tornado));

        if (earthquake == null || flood == null || tornado == null)
        {
            log.AppendLine(
                "ERROR: a disaster script is missing from the scene. " +
                "Check for 'Missing (Mono Script)' components.");
            return log.ToString();
        }

        GameObject managerObject = GameObject.Find(ManagerName);

        if (managerObject == null)
        {
            managerObject = new GameObject(ManagerName);
            Undo.RegisterCreatedObjectUndo(
                managerObject,
                "Create Disaster Difficulty Manager");
        }

        DisasterDifficultyManager manager =
            managerObject.GetComponent<DisasterDifficultyManager>();

        if (manager == null)
            manager = managerObject.AddComponent<DisasterDifficultyManager>();

        // Wire the three disasters. Serialized fields, so this survives.
        SerializedObject serialized = new SerializedObject(manager);

        serialized.FindProperty("earthquake").objectReferenceValue =
            earthquake;
        serialized.FindProperty("flood").objectReferenceValue = flood;
        serialized.FindProperty("tornado").objectReferenceValue = tornado;

        serialized.ApplyModifiedPropertiesWithoutUndo();

        // The manager is the only thing driving the disasters.
        Undo.RecordObject(earthquake, "Take over earthquake");
        earthquake.controlledByDifficultyManager = true;

        Undo.RecordObject(flood, "Take over flood");
        flood.controlledByDifficultyManager = true;
        flood.autoStart = false;
        flood.repeatFlood = false;
        flood.manualTriggerKey = false;

        Undo.RecordObject(tornado, "Take over tornado");
        tornado.controlledByDifficultyManager = true;

        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(earthquake);
        EditorUtility.SetDirty(flood);
        EditorUtility.SetDirty(tornado);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        log.AppendLine("manager: " + managerObject.name);
        log.AppendLine("saved: " + scene.path);

        return log.ToString();
    }

    /// <summary>Reads back what the scene actually has wired up.</summary>
    public static string CheckWiring()
    {
        StringBuilder log = new StringBuilder();

        Scene scene = SceneManager.GetActiveScene();
        log.AppendLine("scene: " + scene.path);

        EarthquakeEvent earthquake =
            Object.FindFirstObjectByType<EarthquakeEvent>(
                FindObjectsInactive.Include);
        FloodEvent flood =
            Object.FindFirstObjectByType<FloodEvent>(
                FindObjectsInactive.Include);
        TornadoEvent tornado =
            Object.FindFirstObjectByType<TornadoEvent>(
                FindObjectsInactive.Include);
        DisasterDifficultyManager manager =
            Object.FindFirstObjectByType<DisasterDifficultyManager>(
                FindObjectsInactive.Include);

        log.AppendLine("earthquake component: " + NameOf(earthquake));
        log.AppendLine("flood component: " + NameOf(flood));
        log.AppendLine("tornado component: " + NameOf(tornado));
        log.AppendLine("manager component: " + NameOf(manager));

        if (earthquake != null)
        {
            log.AppendLine(
                "earthquake controlled: " +
                earthquake.controlledByDifficultyManager);

            if (earthquake.playerRoot == null)
                log.AppendLine("WARNING: earthquake has no playerRoot.");

            if (earthquake.dustParticles == null)
                log.AppendLine("WARNING: earthquake has no dust particles.");
        }

        if (flood != null)
        {
            log.AppendLine(
                "flood controlled: " + flood.controlledByDifficultyManager);
            log.AppendLine("flood autoStart: " + flood.autoStart);
            log.AppendLine("flood playerRoot: " + NameOf(flood.playerRoot));
            log.AppendLine("flood waterVolume: " + NameOf(flood.waterVolume));
            log.AppendLine("flood houseRoot: " + NameOf(flood.houseRoot));
        }

        if (tornado != null)
        {
            log.AppendLine(
                "tornado controlled: " +
                tornado.controlledByDifficultyManager);
        }

        if (manager != null)
        {
            log.AppendLine(
                "manager ramp: " + manager.timeToMaximumDifficulty + "s");
            log.AppendLine("manager gap: " + manager.gapAtStart + " -> " +
                manager.gapAtMaximum);
        }

        return log.ToString();
    }

    /// <summary>
    /// Fills in the references the disasters need from what is in the scene.
    /// Only ever fills empty slots: anything already assigned is left alone.
    /// </summary>
    public static string WireDisasters()
    {
        Scene scene = SceneManager.GetActiveScene();
        StringBuilder log = new StringBuilder();

        log.AppendLine("scene: " + scene.path);

        EarthquakeEvent earthquake =
            Object.FindFirstObjectByType<EarthquakeEvent>(
                FindObjectsInactive.Include);

        if (earthquake == null)
        {
            log.AppendLine("earthquake: NONE (skipped)");
        }
        else
        {
            Undo.RecordObject(earthquake, "Wire earthquake");

            if (earthquake.playerRoot == null)
            {
                GameObject player = GameObject.Find("PlayerRoot");

                if (player != null)
                {
                    earthquake.playerRoot = player.transform;
                    log.AppendLine("earthquake.playerRoot <- PlayerRoot");
                }
            }

            if (earthquake.playerController == null &&
                earthquake.playerRoot != null)
            {
                earthquake.playerController =
                    earthquake.playerRoot.GetComponent<CharacterController>();

                if (earthquake.playerController != null)
                    log.AppendLine("earthquake.playerController <- found");
            }

            if (earthquake.playerCamera == null)
            {
                earthquake.playerCamera = Camera.main;

                if (earthquake.playerCamera != null)
                    log.AppendLine("earthquake.playerCamera <- Main Camera");
            }

            ParticleSystem dust = FindParticles("dust");
            ParticleSystem rocks = FindParticles("debris");

            if (earthquake.dustParticles == null && dust != null)
            {
                earthquake.dustParticles = dust;
                log.AppendLine("earthquake.dustParticles <- " + dust.name);
            }

            if (earthquake.rockParticles == null && rocks != null)
            {
                earthquake.rockParticles = rocks;
                log.AppendLine("earthquake.rockParticles <- " + rocks.name);
            }

            if (earthquake.movementScriptsToDisable == null ||
                earthquake.movementScriptsToDisable.Length == 0)
            {
                PlayerInputReader reader =
                    Object.FindFirstObjectByType<PlayerInputReader>(
                        FindObjectsInactive.Include);

                FirstPersonCameraController look =
                    Object.FindFirstObjectByType<FirstPersonCameraController>(
                        FindObjectsInactive.Include);

                System.Collections.Generic.List<Behaviour> list =
                    new System.Collections.Generic.List<Behaviour>();

                if (reader != null)
                    list.Add(reader);

                if (look != null)
                    list.Add(look);

                if (list.Count > 0)
                {
                    earthquake.movementScriptsToDisable = list.ToArray();
                    log.AppendLine(
                        "earthquake.movementScriptsToDisable <- " +
                        list.Count + " script(s)");
                }
            }

            log.AppendLine("earthquake.dust particles: " +
                NameOf(earthquake.dustParticles));
            log.AppendLine("earthquake.rock particles: " +
                NameOf(earthquake.rockParticles));
            log.AppendLine("earthquake.start sound: " +
                NameOf(earthquake.earthquakeStartSound));
            log.AppendLine("earthquake.deadly sound: " +
                NameOf(earthquake.deadlyPhaseSound));

            EditorUtility.SetDirty(earthquake);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            log.AppendLine("saved: " + scene.path);
        }

        return log.ToString();
    }

    private static ParticleSystem FindParticles(string objectName)
    {
        GameObject named = GameObject.Find(objectName);

        if (named != null)
        {
            ParticleSystem found = named.GetComponent<ParticleSystem>();

            if (found != null)
                return found;

            found = named.GetComponentInChildren<ParticleSystem>();

            if (found != null)
                return found;
        }

        foreach (ParticleSystem system in
            Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include))
        {
            if (system != null &&
                system.name.IndexOf(
                    objectName,
                    System.StringComparison.OrdinalIgnoreCase) >= 0)
                return system;
        }

        return null;
    }

    private static string NameOf(Object item)
    {
        return item != null ? item.name : "NONE";
    }
}
