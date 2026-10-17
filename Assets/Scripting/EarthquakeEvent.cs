
using System.Collections;
using UnityEngine;

public class EarthquakeEvent : MonoBehaviour
{
    [Header("Objects")]
    public Transform playerRoot;
    public CharacterController playerController;
    public Camera playerCamera;

    [Header("Existing Movement Scripts")]
    public Behaviour[] movementScriptsToDisable;

    [Header("Event Timing")]
    public float delayBeforeEarthquake = 10f;
    public float earthquakeDuration = 10f;
    public float extraSurvivalTime = 5f;
    public float pauseBetweenEarthquakes = 3f;

    [Header("Camera Shake")]
    public float cameraShakeAmount = 0.08f;
    public float shakeSpeed = 20f;

    [Header("Player Drift")]
    public float maxDriftSpeed = 1.2f;
    public float directionChangeInterval = 1.2f;
    public float driftSmoothness = 0.8f;

    [Header("Intensity")]
    [Range(0f, 1f)]
    public float startingIntensity = 0.15f;

    [Range(0f, 1f)]
    public float endingIntensity = 1f;

    public bool IsHiding { get; private set; }
    public bool EarthquakeActive { get; private set; }
    public bool PlayerLost { get; private set; }

    private HidingSpot currentHidingSpot;
    private Vector3 hidingPosition;
    private Quaternion hidingRotation;

    private float elapsed;
    private float nextDirectionChange;

    private Vector3 driftVelocity;
    private Vector3 targetDriftVelocity;
    private Vector3 driftVelocityChange;

    private Vector3 cameraOriginalPosition;
    private Vector3 lastCameraShake;

    private bool[] originalMovementStates;

    private void Start()
    {
        if (playerController == null && playerRoot != null)
            playerController = playerRoot.GetComponent<CharacterController>();

        if (playerRoot == null && playerController != null)
            playerRoot = playerController.transform;

        if (playerCamera == null)
            playerCamera = Camera.main;

        if (playerCamera != null)
            cameraOriginalPosition = playerCamera.transform.localPosition;

        originalMovementStates =
            new bool[movementScriptsToDisable.Length];

        for (int i = 0; i < movementScriptsToDisable.Length; i++)
        {
            if (movementScriptsToDisable[i] != null)
                originalMovementStates[i] =
                    movementScriptsToDisable[i].enabled;
        }

        StartCoroutine(EarthquakeLoop());
    }

    private IEnumerator EarthquakeLoop()
    {
        // Initial delay, followed by repeating earthquakes.
        yield return new WaitForSeconds(delayBeforeEarthquake);

        while (!PlayerLost)
        {
            EarthquakeActive = true;
            elapsed = 0f;
            nextDirectionChange = 0f;

            driftVelocity = Vector3.zero;
            targetDriftVelocity = Vector3.zero;
            driftVelocityChange = Vector3.zero;

            // Wait until the earthquake finishes or the player loses.
            while (EarthquakeActive && !PlayerLost)
                yield return null;

            if (PlayerLost)
                yield break;

            // The player successfully survived this earthquake.
            // No victory message: just a short quiet interval.
            RestoreCameraShake();

            yield return new WaitForSeconds(pauseBetweenEarthquakes);

            if (PlayerLost)
                yield break;
        }
    }

    private void Update()
    {
        if (!EarthquakeActive)
            return;

        elapsed += Time.deltaTime;

        // Exposed players lose at the original deadline.
        if (elapsed >= earthquakeDuration && !IsHiding)
        {
            EndEventAsLoss();
            return;
        }

        // A player who stays hidden survives the extended event.
        if (elapsed >= earthquakeDuration + extraSurvivalTime)
        {
            if (IsHiding)
                FinishEarthquakeCycle();
            else
                EndEventAsLoss();
        }
    }

    private void LateUpdate()
    {
        if (!EarthquakeActive)
            return;

        float totalDuration =
            earthquakeDuration + extraSurvivalTime;

        float progress = Mathf.Clamp01(elapsed / totalDuration);

        // Smoothly ramp intensity from weak to strong.
        float easedProgress =
            progress * progress * (3f - 2f * progress);

        float intensity = Mathf.Lerp(
            startingIntensity,
            endingIntensity,
            easedProgress
        );

        // Camera shake continues even while the player is hidden.
        if (playerCamera != null)
        {
            float t = Time.time * shakeSpeed;

            Vector3 newShake = new Vector3(
                (Mathf.PerlinNoise(t, 10f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(20f, t) - 0.5f) * 2f,
                0f
            ) * cameraShakeAmount * intensity;

            RestoreCameraShake();

            playerCamera.transform.localPosition += newShake;
            lastCameraShake = newShake;
        }

        // Keep the hidden player completely stationary.
        if (IsHiding)
        {
            if (playerRoot != null)
            {
                playerRoot.position = hidingPosition;
                playerRoot.rotation = hidingRotation;
            }

            return;
        }

        if (Time.time >= nextDirectionChange)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;

            if (direction.sqrMagnitude < 0.01f)
                direction = Vector2.right;

            targetDriftVelocity = new Vector3(
                direction.x, 0f, direction.y
            ) * maxDriftSpeed * intensity;

            nextDirectionChange =
                Time.time + directionChangeInterval;
        }

        driftVelocity = Vector3.SmoothDamp(
            driftVelocity,
            targetDriftVelocity,
            ref driftVelocityChange,
            driftSmoothness
        );

        if (playerController != null)
        {
            playerController.Move(driftVelocity * Time.deltaTime);
        }
    }

    public bool TryEnterHide(HidingSpot spot)
    {
        if (spot == null || PlayerLost || IsHiding)
            return false;

        currentHidingSpot = spot;
        IsHiding = true;

        if (playerRoot != null)
        {
            hidingPosition = playerRoot.position;
            hidingRotation = playerRoot.rotation;
        }

        for (int i = 0; i < movementScriptsToDisable.Length; i++)
        {
            if (movementScriptsToDisable[i] != null)
                movementScriptsToDisable[i].enabled = false;
        }

        driftVelocity = Vector3.zero;
        targetDriftVelocity = Vector3.zero;
        driftVelocityChange = Vector3.zero;

        return true;
    }

    public void ExitHide(HidingSpot spot)
    {
        if (!IsHiding || currentHidingSpot != spot)
            return;

        IsHiding = false;
        currentHidingSpot = null;

        RestoreMovementScripts();

        // Leaving after the original death deadline causes a loss.
        if (EarthquakeActive && elapsed >= earthquakeDuration)
            EndEventAsLoss();
    }

    private void FinishEarthquakeCycle()
    {
        EarthquakeActive = false;

        // Let the player move and leave the hiding spot again.
        if (IsHiding)
        {
            IsHiding = false;
            currentHidingSpot = null;
            RestoreMovementScripts();
        }

        driftVelocity = Vector3.zero;
        targetDriftVelocity = Vector3.zero;
        driftVelocityChange = Vector3.zero;

        RestoreCameraShake();
    }

    private void EndEventAsLoss()
    {
        EarthquakeActive = false;
        PlayerLost = true;

        RestoreCameraShake();

        // Stop the player remaining frozen after losing.
        if (IsHiding)
        {
            IsHiding = false;
            currentHidingSpot = null;
            RestoreMovementScripts();
        }
    }

    private void RestoreMovementScripts()
    {
        for (int i = 0; i < movementScriptsToDisable.Length; i++)
        {
            if (movementScriptsToDisable[i] != null)
                movementScriptsToDisable[i].enabled =
                    originalMovementStates[i];
        }
    }

    private void RestoreCameraShake()
    {
        if (playerCamera != null &&
            lastCameraShake != Vector3.zero)
        {
            playerCamera.transform.localPosition -= lastCameraShake;
            lastCameraShake = Vector3.zero;
        }
    }

    private void OnGUI()
    {
        if (!PlayerLost)
            return;

        GUIStyle style = new GUIStyle();
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 48;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.red;

        GUI.Label(
            new Rect(0, 0, Screen.width, Screen.height),
            "PLAYER LOST",
            style
        );
    }
}
