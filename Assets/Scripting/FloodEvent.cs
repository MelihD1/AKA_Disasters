
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Flood event, built as a companion to EarthquakeEvent.
///
/// A long translucent blue rectangle (the water volume) rises out of the
/// floor, holds at its peak for a while and then drains away again. It is
/// centred on the player and follows him around.
///
/// How high it goes is yours to set: "water rise" is simply how far above
/// the floor the water climbs. If you would rather have the script work the
/// height out for you, turn on auto calculating and it will stop just below
/// the player's head when he stands on the tallest surface he can climb.
///
/// If the water catches up with the player's head he drowns.
/// </summary>
[AddComponentMenu("Events/Flood Event")]
public class FloodEvent : MonoBehaviour
{

    private void StartFloodAudio()
    {
        if (floodAudioSource == null || floodLoopSound == null)
            return;

        floodAudioSource.Stop();
        floodAudioSource.clip = floodLoopSound;
        floodAudioSource.loop = true;
        floodAudioSource.Play();
    }

    private void StopFloodAudio()
    {
        if (floodAudioSource == null)
            return;

        floodAudioSource.Stop();
        floodAudioSource.loop = false;
        floodAudioSource.clip = null;
    }
    public enum FloodState
    {
        Idle,
        Rising,
        Holding,
        Draining
    }

    [Header("Objects")]
    [Tooltip("Player transform (feet are derived from the CharacterController).")]
    public Transform playerRoot;
    public CharacterController playerController;
    public Camera playerCamera;

    [Tooltip("The translucent rectangle that rises. Its Y scale is driven by the script.")]
    public Transform waterVolume;
    public Renderer waterRenderer;

    [Tooltip("Root of the house. Colliders under it are measured to find the floor and the furniture.")]
    public Transform houseRoot;

    [Header("Flood Audio")]
    [SerializeField] private AudioSource floodAudioSource;
    [SerializeField] private AudioClip floodLoopSound;


    [Header("Activation")]
    [Tooltip("Start a flood automatically when the scene starts. Off by default: trigger it with F, or call StartFlood() from your own game mode.")]

    
    public bool autoStart = false;
    public float delayBeforeFlood = 10f;

    [Tooltip("Press F at runtime to trigger a flood by hand.")]
    public bool manualTriggerKey = true;

    [Tooltip("Keep flooding after the water drains away.")]
    public bool repeatFlood = true;

    [Tooltip("0 = endless.")]
    public int maxFloodCount = 0;
    public float pauseBetweenFloods = 5f;

    [Header("Water Speed")]
    [Tooltip("How fast the water rises, in units per second.")]
    public float riseSpeed = 0.5f;

    [Tooltip("Extra speed gained every second while rising. 0 = constant speed.")]
    public float riseAcceleration = 0.15f;

    [Tooltip("How long the water stays at its peak before draining.")]
    public float peakHoldDuration = 8f;

    [Tooltip("How fast the water drains, in units per second.")]
    public float drainSpeed = 1f;

    [Header("Water Height")]
    [Tooltip("How far above the floor the water rises. This is your height knob.")]
    public float waterRise = 2.5f;

    [Tooltip("Off: the water simply rises waterRise above the floor. On: the script works the height out from the furniture instead, so it stops just below the player's head when he stands on the tallest surface.")]
    public bool autoCalculateHeight = false;

    [Tooltip("Only used while auto calculating: how far below the player's head the water stops.")]
    public float headClearance = 0.25f;

    [Tooltip("Surfaces lower than this above the floor (rugs, steps) count as floor.")]
    public float standableMinHeight = 0.35f;

    [Tooltip("Surfaces higher than this above the floor are not treated as furniture you can stand on (walls, ceiling).")]
    public float standableMaxHeight = 2.6f;

    [Tooltip("Anything whose name contains one of these is ignored when looking for furniture: lamps, plants and clutter are not something to climb.")]
    public string[] standableIgnoreNames =
    {
        "lamp",
        "window",
        "plant",
        "mug",
        "cup",
        "bowl",
        "apple",
        "pineapple",
        "book"
    };

    [Tooltip("Eye height used when the player is not standing on the floor while measuring.")]
    public float fallbackEyeHeight = 1.7f;

    [Tooltip("Clear head room a surface needs before it counts as somewhere to stand (a ledge under the ceiling does not qualify).")]
    public float standableHeadroom = 1.85f;

    [Tooltip("Shifts the whole water body up or down without changing the measured peak.")]
    public float waterLevelOffset = 0f;

    [Header("Water Shape & Look")]
    [Tooltip("Keep the water rectangle centred on the player, so the flood follows him around instead of sitting on the house.")]
    public bool followPlayer = true;

    [Tooltip("Size of the water rectangle when it follows the player (X = width, Y = length).")]
    public Vector2 followSize = new Vector2(60f, 60f);

    public Color waterColor = new Color(0.16f, 0.5f, 0.85f, 0.55f);

    [Tooltip("Extra space around the house footprint that the water covers.")]
    public float footprintMargin = 1f;

    [Tooltip("Optional. Leave empty to build the translucent material at runtime.")]
    public Material waterMaterial;

    [Header("Drowning")]
    public bool drownPlayer = true;

    [Tooltip("How far above the camera the top of the head is.")]
    public float headHeightAboveCamera = 0.12f;

    public bool underwaterScreenTint = true;
    public Color underwaterTint = new Color(0.06f, 0.3f, 0.55f, 1f);
    public float underwaterTintMaxAlpha = 0.6f;

    public bool underwaterFog = true;
    public float underwaterFogDensity = 0.08f;

    [Header("Diagnostics")]
    public bool showDebugHud = true;

    // Read-only state -------------------------------------------------------

    public FloodState State { get; private set; } = FloodState.Idle;
    public bool FloodActive =>
        State == FloodState.Rising || State == FloodState.Holding;
    public bool PlayerLost { get; private set; }
    public float WaterLevel { get; private set; }
    public float FloorLevel { get; private set; }
    public float PeakWaterLevel { get; private set; }
    public float StandableFurnitureTop { get; private set; }
    public float EyeHeight { get; private set; }
    public string FloorColliderName { get; private set; } = "?";
    public string TallestStandableName { get; private set; } = "?";
    public int FloodsCompleted { get; private set; }

    public Vector3 WaterFootprint { get; private set; } = new Vector3(12f, 0f, 12f);
    public Vector3 HouseCenter { get; private set; }

    /// <summary>How deep the camera currently is under the surface (0 = dry).</summary>
    public float SubmersionDepth
    {
        get
        {
            float eyeY = playerCamera != null
                ? playerCamera.transform.position.y
                : 0f;

            return WaterLevel - eyeY;
        }
    }

    private bool manualRequest;
    private bool cancelRequested;

    private bool fogSaved;
    private bool fogApplied;
    private bool fogWasEnabled;
    private Color fogColorBefore;
    private FogMode fogModeBefore;
    private float fogDensityBefore;

    private Canvas overlayCanvas;
    private Image overlayImage;
    private float overlayAlpha;

    private Material runtimeMaterial;

    // Unity messages --------------------------------------------------------

    private void Awake()
    {
        ResolveReferences();
        SaveFogSettings();
        BuildWaterMaterial();
        MeasureScene();
        WaterLevel = FloorLevel + waterLevelOffset;
        ApplyWater();
    }

    private void Start()
    {
        if (Application.isPlaying)
            StartCoroutine(FloodLoop());
    }

    private void Update()
    {
        if (manualTriggerKey && FloodKeyPressed())
            StartFlood();

        CheckDrowning();
        ApplyWater();
        UpdateUnderwaterEffects();
    }

    private void OnDisable()
    {
        RestoreFog();
        fogApplied = false;
        SetOverlayAlpha(0f);
    }

    private void OnDestroy()
    {
        RestoreFog();

        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }

    // Public API ------------------------------------------------------------

    /// <summary>Starts a flood right now (ignored while one is already running).</summary>
    public bool StartFlood()
    {
        if (PlayerLost || State != FloodState.Idle)
            return false;

        manualRequest = true;
        return true;
    }

    /// <summary>Ends the current flood and puts the water back.</summary>
    public void CancelFlood()
    {
        cancelRequested = true;

        if (State == FloodState.Idle)
        {
            WaterLevel = FloorLevel + waterLevelOffset;
            ApplyWater();
            UpdateUnderwaterEffects();
        }
    }

    /// <summary>Re-measures the peak height. Returns the resulting peak level.</summary>
    public float RecalculatePeakHeight()
    {
        MeasureScene();
        return PeakWaterLevel;
    }

    // Setup ----------------------------------------------------------------

    private void ResolveReferences()
    {
        if (playerController == null && playerRoot != null)
            playerController = playerRoot.GetComponent<CharacterController>();

        if (playerRoot == null && playerController != null)
            playerRoot = playerController.transform;

        if (playerCamera == null)
            playerCamera = Camera.main;

        if (waterVolume != null && waterRenderer == null)
            waterRenderer = waterVolume.GetComponent<Renderer>();

        if (houseRoot == null)
        {
            // Fall back to the biggest thing in the scene that is not us.
            Collider[] all = FindObjectsByType<Collider>(
                FindObjectsInactive.Exclude);

            Collider biggest = null;

            foreach (Collider c in all)
            {
                if (c == null || c.isTrigger || c.bounds.size.magnitude < 4f)
                    continue;

                if (c.transform.IsChildOf(transform))
                    continue;

                if (biggest == null ||
                    c.bounds.size.magnitude > biggest.bounds.size.magnitude)
                    biggest = c;
            }

            if (biggest != null)
            {
                houseRoot = biggest.transform.root;

                Debug.LogWarning(
                    "FloodEvent: no houseRoot assigned, measuring '" +
                    houseRoot.name + "' instead.");
            }
        }
    }

    private void SaveFogSettings()
    {
        if (fogSaved)
            return;

        fogWasEnabled = RenderSettings.fog;
        fogColorBefore = RenderSettings.fogColor;
        fogModeBefore = RenderSettings.fogMode;
        fogDensityBefore = RenderSettings.fogDensity;
        fogSaved = true;
    }

    private void RestoreFog()
    {
        if (!fogSaved)
            return;

        RenderSettings.fog = fogWasEnabled;
        RenderSettings.fogColor = fogColorBefore;
        RenderSettings.fogMode = fogModeBefore;
        RenderSettings.fogDensity = fogDensityBefore;
        fogSaved = false;
    }

    /// <summary>
    /// Looks at the house and works out the floor, the tallest thing the
    /// player can climb on, and how high the water is allowed to go.
    /// </summary>
    public void MeasureScene()
    {
        float eye = ResolveEyeHeight();

        Bounds bounds;
        float floor;
        float furniture;

        if (!TryMeasureHouse(out bounds, out floor, out furniture))
        {
            FloorLevel = playerRoot != null
                ? playerRoot.position.y - (playerController != null
                    ? playerController.height * 0.5f
                    : 1f)
                : 0f;

            furniture = FloorLevel;
            bounds = new Bounds(
                new Vector3(
                    playerRoot != null ? playerRoot.position.x : 0f,
                    FloorLevel,
                    playerRoot != null ? playerRoot.position.z : 0f),
                new Vector3(12f, 1f, 12f));
        }

        FloorLevel = floor;
        StandableFurnitureTop = Mathf.Max(furniture, floor);
        EyeHeight = eye;

        HouseCenter = new Vector3(bounds.center.x, floor, bounds.center.z);
        WaterFootprint = new Vector3(
            Mathf.Max(1f, bounds.size.x + footprintMargin * 2f),
            0f,
            Mathf.Max(1f, bounds.size.z + footprintMargin * 2f));

        if (autoCalculateHeight)
        {
            float computedPeak =
                StandableFurnitureTop + EyeHeight - headClearance;

            // The floor has to stay deadly, otherwise the flood is harmless.
            float deadlyFloorLevel = FloorLevel + EyeHeight + 0.15f;

            if (computedPeak < deadlyFloorLevel)
            {
                Debug.LogWarning(
                    "FloodEvent: the tallest surface (" +
                    StandableFurnitureTop.ToString("F2") +
                    ") is too low for the water to threaten the player on " +
                    "the floor. Raising the peak to " +
                    deadlyFloorLevel.ToString("F2") + ".");

                computedPeak = deadlyFloorLevel;
            }

            PeakWaterLevel = computedPeak;
        }
        else
        {
            // The height is the designer's call: floor + waterRise.
            PeakWaterLevel = FloorLevel + waterRise;
        }
    }

    private bool TryMeasureHouse(
        out Bounds bounds,
        out float floor,
        out float furniture)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        floor = 0f;
        furniture = 0f;

        if (houseRoot == null)
            return false;

        Collider[] colliders =
            houseRoot.GetComponentsInChildren<Collider>(true);

        if (colliders == null || colliders.Length == 0)
            return false;

        List<Collider> valid = new List<Collider>();

        bool hasAny = false;
        float largestArea = 0f;
        Bounds total = new Bounds(houseRoot.position, Vector3.zero);

        foreach (Collider c in colliders)
        {
            if (c == null || c.isTrigger || c.bounds.size == Vector3.zero)
                continue;

            valid.Add(c);

            Bounds b = c.bounds;

            if (!hasAny)
            {
                total = b;
                hasAny = true;
            }
            else
            {
                total.Encapsulate(b);
            }

            float area = b.size.x * b.size.z;

            if (area > largestArea)
                largestArea = area;
        }

        if (!hasAny)
            return false;

        // The floor of the room the player walks in: the lowest of the wide
        // horizontal slabs. Floor tiles are much bigger than walls or
        // furniture, and a house can have several of them stacked.
        float floorTop = float.MaxValue;
        string floorName = "?";

        foreach (Collider c in valid)
        {
            Bounds b = c.bounds;
            float area = b.size.x * b.size.z;

            if (area < largestArea * 0.5f)
                continue;

            if (b.max.y < floorTop)
            {
                floorTop = b.max.y;
                floorName = c.name;
            }
        }

        if (floorTop == float.MaxValue)
            floorTop = total.min.y;

        FloorColliderName = floorName;

        // Furniture: anything sitting on the floor within climbing reach.
        float highestFurniture = float.MinValue;
        string furnitureName = "?";

        foreach (Collider c in valid)
        {
            float top = c.bounds.max.y;
            float height = top - floorTop;

            if (height < standableMinHeight || height > standableMaxHeight)
                continue;

            if (IsIgnoredFurniture(c.name))
                continue;

            // A surface is only useful if the player fits on it.
            if (!HasHeadroom(c.bounds))
                continue;

            if (top > highestFurniture)
            {
                highestFurniture = top;
                furnitureName = c.name;
            }
        }

        TallestStandableName = furnitureName;

        bounds = total;
        floor = floorTop;
        furniture = highestFurniture > float.MinValue
            ? highestFurniture
            : floorTop;

        return true;
    }

    private bool IsIgnoredFurniture(string objectName)
    {
        if (standableIgnoreNames == null || string.IsNullOrEmpty(objectName))
            return false;

        foreach (string ignore in standableIgnoreNames)
        {
            if (string.IsNullOrEmpty(ignore))
                continue;

            if (objectName.IndexOf(
                    ignore,
                    System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// True when there is room to stand on the surface: no ceiling or shelf
    /// right above it.
    /// </summary>
    private bool HasHeadroom(Bounds surface)
    {
        float required = Mathf.Max(0.5f, standableHeadroom);

        if (HasClearance(surface.center.x, surface.center.z, surface.max.y,
                required))
            return true;

        float insetX = Mathf.Max(0f, surface.extents.x - 0.3f);
        float insetZ = Mathf.Max(0f, surface.extents.z - 0.3f);

        return HasClearance(
                surface.center.x + insetX, surface.center.z,
                surface.max.y, required) ||
            HasClearance(
                surface.center.x - insetX, surface.center.z,
                surface.max.y, required) ||
            HasClearance(
                surface.center.x, surface.center.z + insetZ,
                surface.max.y, required) ||
            HasClearance(
                surface.center.x, surface.center.z - insetZ,
                surface.max.y, required);
    }

    private bool HasClearance(float x, float z, float top, float required)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            new Vector3(x, top + 0.05f, z),
            Vector3.up,
            required,
            ~0,
            QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            // The player's own capsule is not an obstacle.
            if (playerRoot != null &&
                hit.collider.transform.IsChildOf(playerRoot))
                continue;

            return false;
        }

        return true;
    }

    private float ResolveEyeHeight()
    {
        if (playerCamera == null || playerRoot == null)
            return fallbackEyeHeight;

        CharacterController cc = playerController;

        if (cc == null)
            cc = playerRoot.GetComponent<CharacterController>();

        if (cc == null)
            return fallbackEyeHeight;

        float feetY = playerRoot.position.y - cc.height * 0.5f;
        float measured = playerCamera.transform.position.y - feetY;

        if (measured <= 0.1f || measured > 4f)
            return fallbackEyeHeight;

        // Crouching or sliding must not lower the measured peak.
        return Mathf.Max(measured, fallbackEyeHeight);
    }

    // Water ----------------------------------------------------------------

    private void BuildWaterMaterial()
    {
        if (waterRenderer == null || waterMaterial != null)
            return;

        Material mat = CreateWaterMaterial(waterColor);

        if (mat == null)
        {
            Debug.LogWarning(
                "FloodEvent: no unlit shader found, keeping the current " +
                "water material.");
            return;
        }

        runtimeMaterial = mat;

        waterRenderer.material = mat;
        waterRenderer.shadowCastingMode = ShadowCastingMode.Off;
        waterRenderer.receiveShadows = false;
    }

    /// <summary>
    /// Builds the translucent, double sided, unlit water material.
    /// Used both by the scene setup tool and at runtime.
    /// </summary>
    public static Material CreateWaterMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null)
            return null;

        Material mat = new Material(shader);
        mat.name = "FloodWater";

        ConfigureWaterMaterial(mat, color);

        return mat;
    }

    /// <summary>Applies the translucent water settings to a material.</summary>
    public static void ConfigureWaterMaterial(Material mat, Color color)
    {
        if (mat == null)
            return;

        // Transparent, unlit, and visible from the inside.
        if (mat.HasProperty("_Surface"))
            mat.SetFloat("_Surface", 1f);          // transparent

        if (mat.HasProperty("_Blend"))
            mat.SetFloat("_Blend", 0f);            // alpha blend

        if (mat.HasProperty("_Cull"))
            mat.SetFloat("_Cull", 2f);             // front faces only: one
                                                   // layer, so it reads as
                                                   // translucent water

        if (mat.HasProperty("_ZWrite"))
            mat.SetFloat("_ZWrite", 0f);

        if (mat.HasProperty("_SrcBlend"))
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);

        if (mat.HasProperty("_DstBlend"))
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)RenderQueue.Transparent;
    }

    private void ApplyWater()
    {
        if (waterVolume == null)
            return;

        float baseLevel = FloorLevel + waterLevelOffset;
        float height = WaterLevel - baseLevel;

        if (height < 0.01f)
        {
            if (waterRenderer != null && waterRenderer.enabled)
                waterRenderer.enabled = false;

            return;
        }

        if (waterRenderer != null && !waterRenderer.enabled)
            waterRenderer.enabled = true;

        // The rectangle either sits on the house or rides along with the
        // player so the flood is always around him.
        float centreX = HouseCenter.x;
        float centreZ = HouseCenter.z;
        float sizeX = WaterFootprint.x;
        float sizeZ = WaterFootprint.z;

        if (followPlayer && playerRoot != null)
        {
            centreX = playerRoot.position.x;
            centreZ = playerRoot.position.z;
            sizeX = Mathf.Max(1f, followSize.x);
            sizeZ = Mathf.Max(1f, followSize.y);
        }

        waterVolume.localScale = new Vector3(sizeX, height, sizeZ);

        waterVolume.position = new Vector3(
            centreX,
            baseLevel + height * 0.5f,
            centreZ);
    }

    // Flood cycle ----------------------------------------------------------

    private IEnumerator FloodLoop()
    {
        // Waiting for the first flood, but a manual trigger cuts it short.
        float wait = autoStart ? Mathf.Max(0f, delayBeforeFlood) : 0f;

        while (wait > 0f && !manualRequest && !PlayerLost)
        {
            wait -= Time.deltaTime;
            yield return null;
        }

        while (!PlayerLost)
        {
            if (!manualRequest && !autoStart)
            {
                yield return null;
                continue;
            }

            manualRequest = false;

            yield return RunFlood();

            if (PlayerLost)
                yield break;

            FloodsCompleted++;

            bool keepGoing = repeatFlood &&
                (maxFloodCount <= 0 || FloodsCompleted < maxFloodCount);

            if (!keepGoing)
            {
                if (autoStart && !manualRequest)
                    yield break;

                if (!repeatFlood && !autoStart)
                {
                    yield return null;
                    continue;
                }
            }

            float pause = Mathf.Max(0f, pauseBetweenFloods);

            while (pause > 0f && !manualRequest && !PlayerLost)
            {
                pause -= Time.deltaTime;
                yield return null;
            }
        }
    }

    private IEnumerator RunFlood()
    {
        ResolveReferences();
        MeasureScene();

        cancelRequested = false;

        float baseLevel = FloorLevel + waterLevelOffset;
        float peak = PeakWaterLevel + waterLevelOffset;

        WaterLevel = baseLevel;
        ApplyWater();

        State = FloodState.Rising;

        StartFloodAudio();

        float speed = Mathf.Max(0.01f, riseSpeed);

        while (WaterLevel < peak && !PlayerLost && !cancelRequested)
        {
            speed += riseAcceleration * Time.deltaTime;

            WaterLevel = Mathf.Min(
                peak,
                WaterLevel + speed * Time.deltaTime);

            yield return null;
        }

        if (PlayerLost)
            yield break;

        WaterLevel = peak;
        State = FloodState.Holding;

        float held = 0f;

        while (held < peakHoldDuration && !PlayerLost && !cancelRequested)
        {
            held += Time.deltaTime;
            yield return null;
        }

        if (PlayerLost)
            yield break;

        State = FloodState.Draining;

        while (WaterLevel > baseLevel && !PlayerLost)
        {
            WaterLevel = Mathf.Max(
                baseLevel,
                WaterLevel - Mathf.Max(0.01f, drainSpeed) * Time.deltaTime);

            yield return null;
        }

        WaterLevel = baseLevel;
        cancelRequested = false;
        State = FloodState.Idle;
        ApplyWater();
        StopFloodAudio();
    }

    public bool FloodKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            Keyboard.current.fKey.wasPressedThisFrame)
            return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.F))
            return true;
#endif

        return false;
    }

    // Drowning -------------------------------------------------------------

    private void CheckDrowning()
    {
        if (!drownPlayer || PlayerLost || playerCamera == null)
            return;

        float baseLevel = FloorLevel + waterLevelOffset;

        if (WaterLevel <= baseLevel + 0.05f)
            return;

        float headTop = playerCamera.transform.position.y +
            headHeightAboveCamera;

        if (WaterLevel >= headTop)
            EndEventAsLoss();
    }

    private void EndEventAsLoss()
    {
        PlayerLost = true;
        State = FloodState.Idle;

        StopAllCoroutines();

        StopFloodAudio();

        Debug.Log("FloodEvent: the player drowned.");
    }

    // Underwater feel ------------------------------------------------------

    private void UpdateUnderwaterEffects()
    {
        float depth = SubmersionDepth;
        float target = depth > 0f
            ? Mathf.Clamp01(depth / 0.6f) * underwaterTintMaxAlpha
            : 0f;

        if (underwaterScreenTint)
        {
            overlayAlpha = Mathf.MoveTowards(
                overlayAlpha,
                target,
                Time.deltaTime * 2.5f);

            SetOverlayAlpha(overlayAlpha);
        }

        if (!underwaterFog)
            return;

        if (depth > 0f)
        {
            if (!fogApplied)
            {
                SaveFogSettings();
                fogApplied = true;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = underwaterTint;
            RenderSettings.fogDensity = underwaterFogDensity;
        }
        else if (fogApplied)
        {
            RestoreFog();
            fogApplied = false;
        }
    }

    private void SetOverlayAlpha(float alpha)
    {
        if (!underwaterScreenTint)
            return;

        if (overlayImage == null)
        {
            if (alpha <= 0f)
                return;

            CreateOverlay();
        }

        if (overlayImage == null)
            return;

        Color c = underwaterTint;
        c.a = alpha;
        overlayImage.color = c;

        if (overlayCanvas != null)
            overlayCanvas.enabled = alpha > 0.001f;
    }

    private void CreateOverlay()
    {
        GameObject canvasObject = new GameObject("FloodUnderwaterOverlay");

        overlayCanvas = canvasObject.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 5000;

        canvasObject.AddComponent<CanvasScaler>();

        GameObject imageObject = new GameObject(
            "Tint",
            typeof(RectTransform));

        imageObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        overlayImage = imageObject.AddComponent<Image>();
        overlayImage.raycastTarget = false;

        Color c = underwaterTint;
        c.a = 0f;
        overlayImage.color = c;
        overlayCanvas.enabled = false;
    }

    // HUD ------------------------------------------------------------------

    private void OnGUI()
    {
        if (PlayerLost)
        {
            GUIStyle style = new GUIStyle();
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = 48;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = new Color(0.35f, 0.75f, 1f);

            GUI.Label(
                new Rect(0, 0, Screen.width, Screen.height),
                "YOU DROWNED",
                style);
        }

        if (!showDebugHud || PlayerLost)
            return;

        GUIStyle hud = new GUIStyle();
        hud.fontSize = 16;
        hud.normal.textColor = Color.white;

        string text = string.Format(
            "flood: {0}   level {1:F2}   peak {2:F2}   rise {3:F2}   " +
            "floor {4:F2}",
            State,
            WaterLevel,
            PeakWaterLevel + waterLevelOffset,
            waterRise,
            FloorLevel);

        GUI.Label(new Rect(12f, 12f, Screen.width - 24f, 22f), text, hud);
    }
}
