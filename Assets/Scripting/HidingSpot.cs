
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class HidingSpot : MonoBehaviour
{
    [Header("Interaction")]
    public float interactionDistance = 3f;

    [Header("Glow")]
    public Renderer[] glowRenderers;
    public Color glowColor = new Color(1f, 0.8f, 0.25f);
    public float glowStrength = 2f;

    private EarthquakeEvent earthquake;
    private Camera playerCamera;

    private bool isLookingAt;
    private bool playerIsInside;
    private bool isGlowing;

    private Material[][] materials;
    private Color[][] originalEmissionColors;
    private bool[][] originalEmissionStates;

    private void Start()
    {
        earthquake = FindObjectOfType<EarthquakeEvent>();

        if (earthquake != null)
            playerCamera = earthquake.playerCamera;

        if (playerCamera == null)
            playerCamera = Camera.main;

        if (glowRenderers == null || glowRenderers.Length == 0)
            glowRenderers = GetComponentsInChildren<Renderer>();

        materials = new Material[glowRenderers.Length][];
        originalEmissionColors = new Color[glowRenderers.Length][];
        originalEmissionStates = new bool[glowRenderers.Length][];

        for (int i = 0; i < glowRenderers.Length; i++)
        {
            if (glowRenderers[i] == null)
                continue;

            materials[i] = glowRenderers[i].materials;
            originalEmissionColors[i] = new Color[materials[i].Length];
            originalEmissionStates[i] = new bool[materials[i].Length];

            for (int j = 0; j < materials[i].Length; j++)
            {
                Material mat = materials[i][j];

                originalEmissionStates[i][j] =
                    mat.IsKeywordEnabled("_EMISSION");

                if (mat.HasProperty("_EmissionColor"))
                    originalEmissionColors[i][j] =
                        mat.GetColor("_EmissionColor");
            }
        }

        if (earthquake == null)
            Debug.LogError("HidingSpot: EarthquakeManager was not found.");

        if (playerCamera == null)
            Debug.LogError("HidingSpot: Assign Player Camera in EarthquakeManager.");
    }

    private void Update()
    {
        if (earthquake == null || playerCamera == null)
            return;

        if (earthquake.PlayerLost)
        {
            SetGlow(false);
            return;
        }

        isLookingAt = false;

        if (earthquake.IsHiding && playerIsInside)
        {
            if (PressedE())
            {
                earthquake.ExitHide(this);
                playerIsInside = false;
            }

            SetGlow(false);
            return;
        }

        if (earthquake.IsHiding)
        {
            SetGlow(false);
            return;
        }

        // Find the hiding object the camera is pointing at.
        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            interactionDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        // Check the nearest hit first, ignoring objects that
        // are not this hiding spot.
        System.Array.Sort(hits, (a, b) =>
            a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            HidingSpot hitSpot =
                hit.collider.GetComponentInParent<HidingSpot>();

            if (hitSpot != null)
            {
                isLookingAt = hitSpot == this;
                break;
            }
        }

        SetGlow(isLookingAt);

        if (isLookingAt && PressedE())
        {
            if (earthquake.TryEnterHide(this))
                playerIsInside = true;
        }
    }

    private bool PressedE()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
            return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.E))
            return true;
#endif

        return false;
    }

    private void SetGlow(bool enable)
    {
        if (isGlowing == enable)
            return;

        isGlowing = enable;

        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == null)
                continue;

            for (int j = 0; j < materials[i].Length; j++)
            {
                Material mat = materials[i][j];

                if (mat == null ||
                    !mat.HasProperty("_EmissionColor"))
                    continue;

                if (enable)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor(
                        "_EmissionColor",
                        glowColor * glowStrength
                    );
                }
                else
                {
                    mat.SetColor(
                        "_EmissionColor",
                        originalEmissionColors[i][j]
                    );

                    if (!originalEmissionStates[i][j])
                        mat.DisableKeyword("_EMISSION");
                }
            }
        }
    }

    private void OnGUI()
    {
        if (earthquake == null || earthquake.PlayerLost)
            return;

        string message = "";

        if (earthquake.IsHiding && playerIsInside)
            message = "Press E to Leave Hiding Spot";
        else if (isLookingAt && !earthquake.IsHiding)
            message = "Press E to Hide";

        if (string.IsNullOrEmpty(message))
            return;

        GUIStyle style = new GUIStyle();
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 24;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.white;

        GUI.Box(
            new Rect(Screen.width / 2f - 175f,
                     Screen.height - 110f, 350f, 50f),
            GUIContent.none
        );

        GUI.Label(
            new Rect(Screen.width / 2f - 175f,
                     Screen.height - 110f, 350f, 50f),
            message,
            style
        );
    }

    private void OnDisable()
    {
        if (materials != null && isGlowing)
            SetGlow(false);
    }
}
