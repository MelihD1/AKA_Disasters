using System.Collections;
using UnityEngine;

public class TornadoEvent : MonoBehaviour
{
    [HideInInspector]
    public bool controlledByDifficultyManager;
    private void OnEnable()
    {
        DisasterDifficultyManager.Register(this);
    }

    [Header("Tornado Schedule")]
    [Tooltip("Delay in seconds before the first tornado starts.")]
    [SerializeField] private float firstStartDelay = 30f;

    [Tooltip("Minimum time between the end of one tornado and the next.")]
    [SerializeField] private float minimumTimeBetweenEvents = 60f;

    [Tooltip("Maximum time between the end of one tornado and the next.")]
    [SerializeField] private float maximumTimeBetweenEvents = 120f;

    [Header("Tornado Duration")]
    [Tooltip("Minimum duration of a tornado in seconds.")]
    [SerializeField] private float minimumDuration = 10f;

    [Tooltip("Maximum duration of a tornado in seconds.")]
    [SerializeField] private float maximumDuration = 20f;

    [Header("Player Slowdown")]
    [Tooltip("Movement speed multiplier during the tornado. 0.5 = half speed.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float movementSpeedMultiplier = 0.5f;

    [Tooltip("How much of the tornado's slowdown the permanent Crampons upgrade cancels. 0.6 = the player keeps 60% of the slowdown removed.")]
    [Range(0f, 1f)]
    [SerializeField] private float cramponSlowdownReduction = 0.6f;

    [Header("Tornado Audio")]
    [Tooltip("Audio Source used to play the looping tornado sound.")]
    [SerializeField] private AudioSource tornadoAudioSource;

    [Tooltip("Seconds the looped tornado sound takes to fade out when the tornado ends.")]
    public float audioFadeOut = 0.5f;

    [Tooltip("Sound that loops for the entire time the tornado is active.")]
    [SerializeField] private AudioClip tornadoLoopSound;

    [Header("Difficulty Scaling")]
    [Tooltip("Tornado length at difficulty 0 / 1.")]
    [SerializeField] private float durationAtDifficulty0 = 10f;
    [SerializeField] private float durationAtDifficulty1 = 22f;

    [Tooltip("Movement speed multiplier at difficulty 0 / 1. Lower slows the player more.")]
    [Range(0.05f, 1f)] [SerializeField] private float slowdownAtDifficulty0 = 0.75f;
    [Range(0.05f, 1f)] [SerializeField] private float slowdownAtDifficulty1 = 0.4f;

    [Header("Debug")]
    [SerializeField] private bool showDebugMessages = true;

    private PlayerMotor playerMotor;
    private bool tornadoActive;

    public bool TornadoActive => tornadoActive;

    /// <summary>Movement multiplier the tornado is currently applying.</summary>
    public float MovementSpeedMultiplier => movementSpeedMultiplier;

    /// <summary>
    /// The slowdown actually handed to the player. Lower than
    /// MovementSpeedMultiplier once the Crampons upgrade is owned; 1 = not
    /// slowed at all.
    /// </summary>
    public float AppliedSlowdown { get; private set; } = 1f;

    /// <summary>True while the Crampons upgrade is weakening the tornado.</summary>
    public bool CramponsProtect => GameProgress.HasCrampons;

    private void Start()
    {
        FindPlayer();
        if (!controlledByDifficultyManager)
            StartCoroutine(TornadoRoutine());
    }

    private IEnumerator TornadoRoutine()
    {
        // Wait before the first tornado.
        yield return new WaitForSeconds(
            Mathf.Max(0f, firstStartDelay)
        );

        while (true)
        {
            FindPlayer();

            float duration = Random.Range(
                Mathf.Min(minimumDuration, maximumDuration),
                Mathf.Max(minimumDuration, maximumDuration)
            );

            StartTornado();

            yield return new WaitForSeconds(duration);

            EndTornado();

            float nextDelay = Random.Range(
                Mathf.Min(
                    minimumTimeBetweenEvents,
                    maximumTimeBetweenEvents
                ),
                Mathf.Max(
                    minimumTimeBetweenEvents,
                    maximumTimeBetweenEvents
                )
            );

            yield return new WaitForSeconds(
                Mathf.Max(0f, nextDelay)
            );
        }
    }


    public bool StartTornadoNow()
    {
        if (tornadoActive)
            return false;

        FindPlayer();
        StartTornado();
        return true;
    }

    public void StopTornadoNow()
    {
        if (tornadoActive)
            EndTornado();
    }

    /// <summary>How long a tornado lasts at the current settings.</summary>
    public float TornadoDuration =>
        Mathf.Max(1f, (minimumDuration + maximumDuration) * 0.5f);

    /// <summary>
    /// Sets the tornado's values from one 0..1 difficulty. 0 is the gentler
    /// end of each pair, 1 the harsher end. The difficulty manager calls this
    /// right before it starts a tornado.
    /// </summary>
    public void ApplyDifficulty(float difficulty)
    {
        float t = Mathf.Clamp01(difficulty);

        float seconds = Mathf.Lerp(
            durationAtDifficulty0,
            durationAtDifficulty1,
            t);

        // The owner may drive the exact length itself; keep both ends of the
        // random range on the scaled value so either way it agrees.
        minimumDuration = seconds;
        maximumDuration = seconds;

        movementSpeedMultiplier = Mathf.Lerp(
            slowdownAtDifficulty0,
            slowdownAtDifficulty1,
            t);
    }

    private void FindPlayer()
    {
        if (playerMotor != null)
            return;

        playerMotor = FindFirstObjectByType<PlayerMotor>();
    }

    private void StartTornado()
    {
        tornadoActive = true;

        if (playerMotor != null)
        {
            // Crampons permanently reduce the tornado's slowdown.
            float effectiveMultiplier = movementSpeedMultiplier;
            if (GameProgress.HasCrampons)
                effectiveMultiplier = Mathf.Lerp(movementSpeedMultiplier, 1f, Mathf.Clamp01(cramponSlowdownReduction));

            playerMotor.SetTornadoSlowdown(effectiveMultiplier);
            AppliedSlowdown = effectiveMultiplier;
        }
        else
        {
            AppliedSlowdown = 1f;
        }

        if (tornadoAudioSource != null && tornadoLoopSound != null)
        {
            tornadoAudioSource.Stop();
            tornadoAudioSource.clip = tornadoLoopSound;
            tornadoAudioSource.loop = true;
            tornadoAudioSource.Play();
        }

        if (showDebugMessages)
            Debug.Log("Tornado started!");
    }

    private void EndTornado()
    {
        tornadoActive = false;

        if (playerMotor != null)
            playerMotor.SetTornadoSlowdown(1f);

        AppliedSlowdown = 1f;

        StopTornadoAudio();

        if (showDebugMessages)
            Debug.Log("Tornado ended.");
    }

    private void StopTornadoAudio()
    {
        if (tornadoAudioSource == null)
            return;

        // Fade instead of cutting dead.
        if (tornadoAudioSource.isPlaying && isActiveAndEnabled)
        {
            StartCoroutine(DisasterAudioFade.FadeOutAndStop(
                tornadoAudioSource, audioFadeOut));
            return;
        }

        DisasterAudioFade.StopNow(tornadoAudioSource);
    }

    private void OnDisable()
    {
        if (playerMotor != null)
            playerMotor.SetTornadoSlowdown(1f);

        StopTornadoAudio();
        tornadoActive = false;
    }
}
