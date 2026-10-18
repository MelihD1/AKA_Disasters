
using System.Collections;
using UnityEngine;

/// <summary>
/// The one thing in charge of the disasters.
///
/// It decides *when* a disaster happens (a countdown that gets shorter as the
/// run goes on) and *how strong it is*: difficulty climbs from 0 to 1 over
/// timeToMaximumDifficulty, and every value the disasters use is scaled with
/// it - earthquake length, shake and drift, how fast and how high the water
/// rises, how long the tornado lasts and how much it slows you down.
///
/// While this component exists the disaster scripts stop running their own
/// schedules (they are given controlledByDifficultyManager = true), so there is
/// only ever one thing firing events.
///
/// Left alone it runs by itself. Everything can also be driven from code:
/// ForceEarthquake(), ForceFlood(), ForceTornado(), StopEverything(),
/// SetFixedDifficulty(), UseRamp().
/// </summary>
[AddComponentMenu("Events/Disaster Difficulty Manager")]
public class DisasterDifficultyManager : MonoBehaviour
{
    public static DisasterDifficultyManager Instance { get; private set; }

    private static EarthquakeEvent registeredEarthquake;
    private static FloodEvent registeredFlood;
    private static TornadoEvent registeredTornado;

    [Header("Disaster References")]
    [SerializeField] private EarthquakeEvent earthquake;
    [SerializeField] private FloodEvent flood;
    [SerializeField] private TornadoEvent tornado;

    [Header("Difficulty Ramp")]
    [Tooltip("Seconds of play until the difficulty reaches its maximum.")]
    public float timeToMaximumDifficulty = 300f;

    [Tooltip("Difficulty when the scene starts. 0 = easy, 1 = hardest.")]
    [Range(0f, 1f)] public float startingDifficulty = 0f;

    [Tooltip("Hold one fixed difficulty instead of ramping. For testing.")]
    public bool useFixedDifficulty = false;

    [Range(0f, 1f)] public float fixedDifficulty = 0f;

    [Header("Pace")]
    [Tooltip("Seconds before the very first disaster.")]
    public float firstDisasterDelay = 20f;

    [Tooltip("Seconds between disasters at difficulty 0.")]
    public float gapAtStart = 30f;

    [Tooltip("Seconds between disasters at difficulty 1.")]
    public float gapAtMaximum = 10f;

    [Tooltip("Random extra seconds added to every gap.")]
    public float gapJitter = 6f;

    [Header("Disaster Mix")]
    public bool useEarthquakes = true;
    public bool useFloods = true;
    public bool useTornadoes = true;

    [Tooltip("Let a tornado run while an earthquake or a flood is also going.")]
    public bool tornadoDuringMajor = false;

    [Header("Diagnostics")]
    public bool showDebugMessages = true;
    public bool showHud = false;

    // Read-only state -------------------------------------------------------

    /// <summary>0 = easy, 1 = hardest.</summary>
    public float Difficulty { get; private set; }

    /// <summary>Seconds of play so far.</summary>
    public float Elapsed { get; private set; }

    /// <summary>"earthquake", "flood", "tornado" or "none".</summary>
    public string CurrentDisaster { get; private set; } = "none";

    /// <summary>Seconds until the next major disaster starts.</summary>
    public float SecondsUntilNext { get; private set; }

    public int DisastersStarted { get; private set; }
    public bool Running { get; private set; } = true;

    public bool PlayerLost
    {
        get
        {
            return (earthquake != null && earthquake.PlayerLost) ||
                (flood != null && flood.PlayerLost);
        }
    }

    // Registration (called by the disaster scripts in OnEnable) ------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistrations()
    {
        Instance = null;
        registeredEarthquake = null;
        registeredFlood = null;
        registeredTornado = null;
    }

    public static void Register(EarthquakeEvent item)
    {
        if (item == null)
            return;

        registeredEarthquake = item;

        if (Instance != null)
            Instance.earthquake = item;
    }

    public static void Register(FloodEvent item)
    {
        if (item == null)
            return;

        registeredFlood = item;

        if (Instance != null)
            Instance.flood = item;
    }

    public static void Register(TornadoEvent item)
    {
        if (item == null)
            return;

        registeredTornado = item;

        if (Instance != null)
            Instance.tornado = item;
    }

    // Unity messages --------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "[Disasters] More than one DisasterDifficultyManager exists.",
                this);
            enabled = false;
            return;
        }

        Instance = this;

        ResolveReferences();
        TakeOverDisasters();

        Difficulty = useFixedDifficulty
            ? Mathf.Clamp01(fixedDifficulty)
            : Mathf.Clamp01(startingDifficulty);
    }

    private void Start()
    {
        // The disasters may only register after our Awake ran.
        ResolveReferences();
        TakeOverDisasters();

        LogReferences();

        StartCoroutine(MajorLoop());

        if (useTornadoes)
            StartCoroutine(TornadoLoop());
    }

    private void Update()
    {
        if (earthquake == null || flood == null || tornado == null)
            ResolveReferences();

        Elapsed += Time.deltaTime;

        Difficulty = useFixedDifficulty
            ? Mathf.Clamp01(fixedDifficulty)
            : Mathf.Clamp01(
                startingDifficulty +
                Elapsed / Mathf.Max(1f, timeToMaximumDifficulty));
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Wiring ----------------------------------------------------------------

    private void ResolveReferences()
    {
        if (earthquake == null)
            earthquake = registeredEarthquake;

        if (flood == null)
            flood = registeredFlood;

        if (tornado == null)
            tornado = registeredTornado;

        // Fallback for event components already sitting in the loaded scene.
        if (earthquake == null)
            earthquake = FindFirstObjectByType<EarthquakeEvent>(
                FindObjectsInactive.Include);

        if (flood == null)
            flood = FindFirstObjectByType<FloodEvent>(
                FindObjectsInactive.Include);

        if (tornado == null)
            tornado = FindFirstObjectByType<TornadoEvent>(
                FindObjectsInactive.Include);
    }

    /// <summary>
    /// Stops the disasters running their own schedules so this manager is the
    /// only thing starting events. Called from Awake, before any Start runs.
    /// </summary>
    public void TakeOverDisasters()
    {
        if (earthquake != null)
            earthquake.controlledByDifficultyManager = true;

        if (tornado != null)
            tornado.controlledByDifficultyManager = true;

        if (flood != null)
        {
            flood.controlledByDifficultyManager = true;

            // The flood is only ever started by this manager.
            flood.autoStart = false;
            flood.repeatFlood = false;
            flood.manualTriggerKey = false;
        }
    }

    private void LogReferences()
    {
        Debug.Log(
            "[Disasters] Manager connected - " +
            "Earthquake: " + NameOf(earthquake) + ", " +
            "Flood: " + NameOf(flood) + ", " +
            "Tornado: " + NameOf(tornado),
            this);
    }

    private static string NameOf(Object item)
    {
        return item != null ? item.name : "NOT REGISTERED";
    }

    // Public API ------------------------------------------------------------

    /// <summary>Starts an earthquake now, scaled to the current difficulty.</summary>
    public bool ForceEarthquake()
    {
        if (earthquake == null || earthquake.PlayerLost ||
            earthquake.EarthquakeActive)
            return false;

        earthquake.ApplyDifficulty(Difficulty);
        earthquake.StartEarthquakeNow();

        CurrentDisaster = "earthquake";
        DisastersStarted++;

        LogEvent(
            "earthquake",
            string.Format(
                "length {0:F1}s, shake {1:F2}, drift {2:F1}, " +
                "hide survival {3:F1}s",
                earthquake.earthquakeDuration,
                earthquake.cameraShakeAmount,
                earthquake.maxDriftSpeed,
                earthquake.extraSurvivalTime));

        return true;
    }

    /// <summary>Starts a flood now, scaled to the current difficulty.</summary>
    public bool ForceFlood()
    {
        if (flood == null || flood.PlayerLost)
            return false;

        flood.ApplyDifficulty(Difficulty);

        // BeginFloodNow starts it in this frame, so the scheduler below can
        // tell "running" from "not started yet".
        if (!flood.BeginFloodNow())
            return false;

        CurrentDisaster = "flood";
        DisastersStarted++;

        LogEvent(
            "flood",
            string.Format(
                "rise {0:F2}m, water speed {1:F2}, holds {2:F1}s",
                flood.waterRise,
                flood.riseSpeed,
                flood.peakHoldDuration));

        return true;
    }

    /// <summary>Starts a tornado now, scaled to the current difficulty.</summary>
    public bool ForceTornado()
    {
        if (tornado == null || tornado.TornadoActive)
            return false;

        tornado.ApplyDifficulty(Difficulty);

        float seconds = tornado.TornadoDuration;

        if (!tornado.StartTornadoNow())
            return false;

        CurrentDisaster = "tornado";
        DisastersStarted++;

        LogEvent(
            "tornado",
            string.Format(
                "lasts {0:F1}s, movement x{1:F2}",
                seconds,
                tornado.MovementSpeedMultiplier));

        StartCoroutine(StopTornadoAfter(seconds));
        return true;
    }

    /// <summary>Ends whatever is running and holds off the next disaster.</summary>
    public void StopEverything()
    {
        Running = false;

        if (tornado != null && tornado.TornadoActive)
            tornado.StopTornadoNow();

        if (earthquake != null && earthquake.EarthquakeActive)
            earthquake.StopEarthquakeNow();

        if (flood != null)
            flood.CancelFlood();

        CurrentDisaster = "none";
    }

    /// <summary>Lets the schedule run again after StopEverything().</summary>
    public void ResumeScheduling()
    {
        Running = true;
    }

    /// <summary>Holds one difficulty instead of ramping (tuning / testing).</summary>
    public void SetFixedDifficulty(float value)
    {
        useFixedDifficulty = true;
        fixedDifficulty = Mathf.Clamp01(value);
        Difficulty = fixedDifficulty;
    }

    /// <summary>Goes back to ramping with time.</summary>
    public void UseRamp()
    {
        useFixedDifficulty = false;
    }

    // Scheduling ------------------------------------------------------------

    private bool MajorActive()
    {
        return (earthquake != null && earthquake.EarthquakeActive) ||
            (flood != null && flood.State != FloodEvent.FloodState.Idle);
    }

    private float NextGap()
    {
        float gap = Mathf.Lerp(gapAtStart, gapAtMaximum, Difficulty);
        return Mathf.Max(0.5f, gap + Random.Range(0f, Mathf.Max(0f, gapJitter)));
    }

    private IEnumerator MajorLoop()
    {
        float wait = Mathf.Max(0f, firstDisasterDelay);

        while (true)
        {
            if (!Running || PlayerLost)
            {
                SecondsUntilNext = 0f;
                yield return null;
                continue;
            }

            while (wait > 0f)
            {
                if (!Running)
                    break;

                wait -= Time.deltaTime;
                SecondsUntilNext = Mathf.Max(0f, wait);
                yield return null;
            }

            if (!Running || PlayerLost)
                continue;

            yield return StartMajorAndWait();

            wait = NextGap();
        }
    }

    private IEnumerator StartMajorAndWait()
    {
        // One disaster at a time unless the designer asked otherwise.
        if (!tornadoDuringMajor)
        {
            while (tornado != null && tornado.TornadoActive && !PlayerLost)
                yield return null;
        }

        bool earthquakeReady = useEarthquakes &&
            earthquake != null &&
            !earthquake.PlayerLost &&
            !earthquake.EarthquakeActive;

        bool floodReady = useFloods &&
            flood != null &&
            !flood.PlayerLost &&
            flood.State == FloodEvent.FloodState.Idle;

        if (!earthquakeReady && !floodReady)
            yield break;

        bool pickEarthquake = earthquakeReady &&
            (!floodReady || Random.value < 0.5f);

        if (pickEarthquake)
        {
            if (!ForceEarthquake())
                yield break;

            while (earthquake.EarthquakeActive && !PlayerLost)
                yield return null;
        }
        else
        {
            if (!ForceFlood())
                yield break;

            // Safety net: never treat a flood as finished before it started.
            float startTimeout = 0f;

            while (flood.State == FloodEvent.FloodState.Idle &&
                startTimeout < 2f &&
                !PlayerLost)
            {
                startTimeout += Time.deltaTime;
                yield return null;
            }

            while (flood.State != FloodEvent.FloodState.Idle && !PlayerLost)
                yield return null;
        }

        CurrentDisaster = "none";
    }

    private IEnumerator TornadoLoop()
    {
        float wait = Mathf.Max(0f, firstDisasterDelay * 0.6f);

        while (true)
        {
            if (!Running || PlayerLost || tornado == null)
            {
                yield return null;
                continue;
            }

            while (wait > 0f)
            {
                if (!Running)
                    break;

                wait -= Time.deltaTime;
                yield return null;
            }

            if (!Running || PlayerLost)
                continue;

            // The tornado normally waits for a break in the major disasters.
            if (!tornadoDuringMajor)
            {
                while (MajorActive() && !PlayerLost)
                    yield return null;
            }

            if (PlayerLost)
                continue;

            wait = NextGap();

            if (!ForceTornado())
                continue;

            while (tornado.TornadoActive && !PlayerLost)
                yield return null;

            if (CurrentDisaster == "tornado")
                CurrentDisaster = "none";
        }
    }

    private IEnumerator StopTornadoAfter(float seconds)
    {
        float timer = 0f;

        while (timer < seconds && tornado != null && tornado.TornadoActive)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (tornado != null && tornado.TornadoActive)
            tornado.StopTornadoNow();
    }

    private void LogEvent(string name, string details)
    {
        if (!showDebugMessages)
            return;

        Debug.Log(
            "[Disasters] " + name + " started at difficulty " +
            Difficulty.ToString("F2") + " (" + details + ")",
            this);
    }

    // HUD ------------------------------------------------------------------

    private void OnGUI()
    {
        if (!showHud)
            return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 16;
        style.normal.textColor = Color.white;

        float y = Screen.height - 92f;

        GUI.Label(
            new Rect(12f, y, 520f, 20f),
            string.Format(
                "difficulty {0:F2}   {1}   disasters started: {2}",
                Difficulty,
                Running ? "running" : "paused",
                DisastersStarted),
            style);

        GUI.Label(
            new Rect(12f, y + 20f, 520f, 20f),
            string.Format(
                "now: {0}   next in {1:F1}s   ramp {2:F0}s",
                CurrentDisaster,
                SecondsUntilNext,
                timeToMaximumDifficulty),
            style);

        Rect bar = new Rect(12f, y + 44f, 240f, 12f);

        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(bar, Texture2D.whiteTexture);

        GUI.color = Color.Lerp(Color.green, Color.red, Difficulty);
        GUI.DrawTexture(
            new Rect(
                bar.x + 1f,
                bar.y + 1f,
                (bar.width - 2f) * Difficulty,
                bar.height - 2f),
            Texture2D.whiteTexture);

        GUI.color = Color.white;
    }
}
