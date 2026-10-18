
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Wires the disaster sounds in the open scene. The three clips in
/// Assets/Sound effects are used where they fit: the earth tremor and the
/// crack/shatter for the earthquake, the wind for the tornado. Nothing is
/// invented - if a sound is missing it says so instead of guessing.
/// </summary>
public static class DisasterAudioSetup
{
    private const string TremorClip =
        "Assets/Sound effects/332454__paulw2k__earth-tremor-1.wav";
    private const string CrackClip =
        "Assets/Sound effects/446133__justinvoke__crack-shatter.wav";
    private const string WindClip =
        "Assets/Sound effects/420323__tonik-95__wind9.wav";

    [MenuItem("Tools/Disasters/Wire Up Disaster Sounds", priority = 50)]
    private static void SetupMenu()
    {
        string report = SetupActiveScene();

        Debug.Log("DisasterAudioSetup:\n" + report);
        EditorUtility.DisplayDialog("Disaster sounds", report, "OK");
    }

    public static string SetupActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        StringBuilder log = new StringBuilder();

        if (!scene.IsValid() || !scene.isLoaded)
            return "ERROR: no active scene.";

        EarthquakeEvent quake = Object.FindFirstObjectByType<EarthquakeEvent>();
        TornadoEvent tornado = Object.FindFirstObjectByType<TornadoEvent>();
        FloodEvent flood = Object.FindFirstObjectByType<FloodEvent>();

        AudioClip tremor = Load(TremorClip, log);
        AudioClip crack = Load(CrackClip, log);
        AudioClip wind = Load(WindClip, log);

        if (quake != null)
        {
            SerializedObject target = new SerializedObject(quake);

            EnsureSource(target, "earthquakeAudioSource", quake.gameObject,
                log, "earthquake start", tremor);
            EnsureSource(target, "deadlyPhaseAudioSource", quake.gameObject,
                log, "earthquake deadly phase", crack);

            AssignClip(target, "earthquakeStartSound", tremor, log,
                "earthquake start clip");
            AssignClip(target, "deadlyPhaseSound", crack, log,
                "earthquake deadly clip");

            target.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(quake);
        }
        else
        {
            log.AppendLine("earthquake: not in the scene");
        }

        if (tornado != null)
        {
            SerializedObject target = new SerializedObject(tornado);

            EnsureSource(target, "tornadoAudioSource", tornado.gameObject,
                log, "tornado loop", wind);

            AssignClip(target, "tornadoLoopSound", wind, log,
                "tornado loop clip");

            target.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tornado);
        }
        else
        {
            log.AppendLine("tornado: not in the scene");
        }

        if (flood != null)
        {
            SerializedObject target = new SerializedObject(flood);

            EnsureSource(target, "floodAudioSource", flood.gameObject,
                log, "flood loop", null);

            AssignClip(target, "floodLoopSound", null, log,
                "flood loop clip");

            target.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(flood);
        }
        else
        {
            log.AppendLine("flood: not in the scene");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        log.AppendLine("saved: " + scene.path);

        return log.ToString();
    }

    private static AudioClip Load(string path, StringBuilder log)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);

        if (clip == null)
            log.AppendLine("MISSING CLIP: " + path);

        return clip;
    }

    private static void EnsureSource(
        SerializedObject target,
        string field,
        GameObject host,
        StringBuilder log,
        string label,
        AudioClip clip)
    {
        SerializedProperty property = target.FindProperty(field);

        if (property == null)
        {
            log.AppendLine(label + ": field '" + field + "' not found");
            return;
        }

        AudioSource source = property.objectReferenceValue as AudioSource;

        if (source == null)
        {
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 1f;
            property.objectReferenceValue = source;

            log.AppendLine(label + ": added an AudioSource on '" +
                host.name + "'");
        }
        else
        {
            log.AppendLine(label + ": already had an AudioSource");
        }

        if (clip != null && source.clip == null)
            source.clip = clip;
    }

    private static void AssignClip(
        SerializedObject target,
        string field,
        AudioClip clip,
        StringBuilder log,
        string label)
    {
        SerializedProperty property = target.FindProperty(field);

        if (property == null)
        {
            log.AppendLine(label + ": field '" + field + "' not found");
            return;
        }

        if (clip == null)
        {
            log.AppendLine(label + ": NO CLIP AVAILABLE - the project has no " +
                "sound that fits, drop one in and assign it (the code path is " +
                "ready and waiting)");
            return;
        }

        property.objectReferenceValue = clip;
        log.AppendLine(label + ": " + clip.name);
    }
}
