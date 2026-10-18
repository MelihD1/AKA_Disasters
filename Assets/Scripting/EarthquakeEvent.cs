
using System.Collections;
using UnityEngine;

public class EarthquakeEvent : MonoBehaviour
{
    [HideInInspector] 
    public bool controlledByDifficultyManager;
    private void OnEnable()
    {
        DisasterDifficultyManager.Register(this);
    }
    public void StartEarthquakeNow()
    {
        if (PlayerLost || EarthquakeActive)
            return;

        EarthquakeActive = true;
        elapsed = 0f;
        nextDirectionChange = 0f;

        driftVelocity = Vector3.zero;
        targetDriftVelocity = Vector3.zero;
        driftVelocityChange = Vector3.zero;

        if (dustParticles != null)
            dustParticles.Play();

        if (earthquakeAudioSource != null && earthquakeStartSound != null)
            earthquakeAudioSource.PlayOneShot(earthquakeStartSound);

        if (deadlyPhaseAudioSource != null)
        {
            deadlyPhaseAudioSource.Stop();
            deadlyPhaseAudioSource.loop = false;
            deadlyPhaseAudioSource.clip = null;
        }
    }

    /// <summary>
    /// Ends the earthquake right now. Used by the difficulty manager when it
    /// needs to clear the field (StopEverything).
    /// </summary>
    public void StopEarthquakeNow()
    {
        if (!EarthquakeActive)
            return;

        FinishEarthquakeCycle();
    }

    /// <summary>
    /// Sets every value this event uses from one 0..1 difficulty: 0 is the
    /// gentler end of each pair below, 1 the harsher end. The difficulty
    /// manager calls this right before it starts an earthquake.
    /// </summary>
    public void ApplyDifficulty(float difficulty)
    {
        float t = Mathf.Clamp01(difficulty);

        earthquakeDuration = Mathf.Lerp(
            durationAtDifficulty0,
            durationAtDifficulty1,
            t);

        extraSurvivalTime = Mathf.Lerp(
            survivalTimeAtDifficulty0,
            survivalTimeAtDifficulty1,
            t);

        maxDriftSpeed = Mathf.Lerp(
            driftAtDifficulty0,
            driftAtDifficulty1,
            t);

        cameraShakeAmount = Mathf.Lerp(
            shakeAtDifficulty0,
            shakeAtDifficulty1,
            t);
    }

    [Header("Objects")]
    public Transform playerRoot;
    public CharacterController playerController;
    public Camera playerCamera;

    [Header("Earthquake Particles")]
    public ParticleSystem dustParticles;
    public ParticleSystem rockParticles;

    [Header("Earthquake Audio")]
    [Tooltip("Plays once when the earthquake begins.")]
    public AudioSource earthquakeAudioSource;
    public AudioClip earthquakeStartSound;

    [Tooltip("Separate source so this sound can overlap the start sound.")]
    public AudioSource deadlyPhaseAudioSource;
    public AudioClip deadlyPhaseSound;

    [Header("Existing Movement Scripts")]
    public Behaviour[] movementScriptsToDisable;

    [Header("Event Timing")]
    public float delayBeforeEarthquake = 10f;
    public float earthquakeDuration = 10f;
    public float extraSurvivalTime = 5f;
    [Tooltip("Extra seconds before the exposed-player deadline when the permanent Hard Hat upgrade is owned.")]
    public float hardHatExtraSeconds = 5f;
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

    [Header("Difficulty Scaling")]
    [Tooltip("How long the earthquake lasts at difficulty 0 / 1.")]
    public float durationAtDifficulty0 = 6f;
    public float durationAtDifficulty1 = 12f;

    [Tooltip("Seconds a hidden player survives past the deadline, at difficulty 0 / 1.")]
    public float survivalTimeAtDifficulty0 = 8f;
    public float survivalTimeAtDifficulty1 = 3f;

    [Tooltip("Drift push on the player at difficulty 0 / 1.")]
    public float driftAtDifficulty0 = 6f;
    public float driftAtDifficulty1 = 14f;

    [Tooltip("Camera shake at difficulty 0 / 1.")]
    public float shakeAtDifficulty0 = 0.05f;
    public float shakeAtDifficulty1 = 0.18f;

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

    private Vector3 lastCameraShake;

    private Vector3 cameraOriginalPosition;

    private bool[] originalMovementStates;

    private void Start()
    {
        if (movementScriptsToDisable == null)
            movementScriptsToDisable = new Behaviour[0];

        if (playerController == null && playerRoot != null)
            playerController =
                playerRoot.GetComponent<CharacterController>();

        if (playerRoot == null && playerController != null)
            playerRoot = playerController.transform;

        if (playerCamera == null)
            playerCamera = Camera.main;

        if (playerCamera != null)
            cameraOriginalPosition =
                playerCamera.transform.localPosition;

        originalMovementStates =
            new bool[movementScriptsToDisable.Length];

        for (int i = 0; i < movementScriptsToDisable.Length; i++)
        {
            if (movementScriptsToDisable[i] != null)
            {
                originalMovementStates[i] =
                    movementScriptsToDisable[i].enabled;
            }
        }

        StopEarthquakeParticles();

        if (!controlledByDifficultyManager)
            StartCoroutine(EarthquakeLoop());
    }

    private IEnumerator EarthquakeLoop()
    {
        if (!controlledByDifficultyManager)
            yield return new WaitForSeconds(delayBeforeEarthquake);

        while (!PlayerLost)
        {
            if (!controlledByDifficultyManager)
                BeginEarthquakeCycle();

            while (EarthquakeActive && !PlayerLost)
                yield return null;

            if (PlayerLost)
                yield break;

            if (controlledByDifficultyManager)
            {
                // Wait for the difficulty manager to start the next event.
                while (!EarthquakeActive && !PlayerLost)
                    yield return null;
            }
            else
            {
                RestoreCameraShake();
                yield return new WaitForSeconds(pauseBetweenEarthquakes);
            }
        }
    }

    private void BeginEarthquakeCycle()
    {
        if (PlayerLost || EarthquakeActive)
            return;

        EarthquakeActive = true;
        elapsed = 0f;
        nextDirectionChange = 0f;
        driftVelocity = Vector3.zero;
        targetDriftVelocity = Vector3.zero;
        driftVelocityChange = Vector3.zero;

        if (dustParticles != null)
            dustParticles.Play();

        if (earthquakeAudioSource != null && earthquakeStartSound != null)
            earthquakeAudioSource.PlayOneShot(earthquakeStartSound);

        if (deadlyPhaseAudioSource != null)
        {
            deadlyPhaseAudioSource.Stop();
            deadlyPhaseAudioSource.loop = false;
            deadlyPhaseAudioSource.clip = null;
        }
    }

    
    private void Update()
    {
        if (!EarthquakeActive)
            return;

        elapsed += Time.deltaTime;

        float exposedDeadline = earthquakeDuration + (GameProgress.HasHardHat ? hardHatExtraSeconds : 0f);

        // Start rocks and the deadly-phase sound at the deadly phase.
        if (elapsed >= exposedDeadline)
        {
            if (rockParticles != null && !rockParticles.isPlaying)
                rockParticles.Play();

            // Use a separate AudioSource so this overlaps the first sound.
            if (deadlyPhaseAudioSource != null &&
                deadlyPhaseSound != null &&
                !deadlyPhaseAudioSource.isPlaying)
            {
                deadlyPhaseAudioSource.clip = deadlyPhaseSound;
                deadlyPhaseAudioSource.loop = true;
                deadlyPhaseAudioSource.Play();
            }
        }

        // Exposed players lose at the original deadline.
        if (elapsed >= exposedDeadline && !IsHiding)
        {
            EndEventAsLoss();
            return;
        }

        // Hidden players survive the extended earthquake.
        if (elapsed >= exposedDeadline + extraSurvivalTime)
        {
            if (IsHiding)
            {
                FinishEarthquakeCycle();
            }
            else
            {
                EndEventAsLoss();
            }
        }
    }

    private void LateUpdate()
    {
        if (!EarthquakeActive)
            return;

        float totalDuration =
            earthquakeDuration + (GameProgress.HasHardHat ? hardHatExtraSeconds : 0f) + extraSurvivalTime;

        float progress = Mathf.Clamp01(
            elapsed / totalDuration
        );

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

        // Hidden players do not receive earthquake drift.
        if (IsHiding)
        {
            if (playerRoot != null)
            {
                playerRoot.position = hidingPosition;
                playerRoot.rotation = hidingRotation;
            }

            return;
        }

        // Choose a new horizontal drift direction.
        if (Time.time >= nextDirectionChange)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;

            if (direction.sqrMagnitude < 0.01f)
                direction = Vector2.right;

            targetDriftVelocity = new Vector3(
                direction.x,
                0f,
                direction.y
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
            playerController.Move(
                driftVelocity * Time.deltaTime
            );
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

        // Leaving after the original deadline means losing.
        float exposedDeadline = earthquakeDuration + (GameProgress.HasHardHat ? hardHatExtraSeconds : 0f);
        if (EarthquakeActive && elapsed >= exposedDeadline)
            EndEventAsLoss();
    }

    private void FinishEarthquakeCycle()
    {
        EarthquakeActive = false;

        StopEarthquakeAudio();
        StopEarthquakeParticles();
        RestoreCameraShake();

        // Do NOT automatically release a hidden player when the earthquake ends.
        // Keep the hiding state and movement scripts disabled until the player
        // presses E at the hiding spot to leave. This prevents the player from
        // being released/clipped into the hiding object at the end of the event.

        driftVelocity = Vector3.zero;
        targetDriftVelocity = Vector3.zero;
        driftVelocityChange = Vector3.zero;
    }

    private void EndEventAsLoss()
    {
        EarthquakeActive = false;
        PlayerLost = true;

        StopEarthquakeAudio();
        StopEarthquakeParticles();
        RestoreCameraShake();

        if (IsHiding)
        {
            IsHiding = false;
            currentHidingSpot = null;
            RestoreMovementScripts();
        }
    }

    private void StopEarthquakeAudio()
    {
        if (earthquakeAudioSource != null)
            earthquakeAudioSource.Stop();

        if (deadlyPhaseAudioSource != null)
        {
            deadlyPhaseAudioSource.Stop();
            deadlyPhaseAudioSource.loop = false;
            deadlyPhaseAudioSource.clip = null;
        }
    }

    private void StopEarthquakeParticles()
    {
        if (dustParticles != null)
        {
            dustParticles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }

        if (rockParticles != null)
        {
            rockParticles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }
    }

    private void RestoreMovementScripts()
    {
        for (int i = 0; i < movementScriptsToDisable.Length; i++)
        {
            if (movementScriptsToDisable[i] != null)
            {
                movementScriptsToDisable[i].enabled =
                    originalMovementStates[i];
            }
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
        // GameOverManager owns the death screen UI.
    }
}
