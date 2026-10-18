using System.Collections;
using UnityEngine;

public class TornadoEvent : MonoBehaviour
{
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

    [Header("Tornado Audio")]
    [Tooltip("Audio Source used to play the looping tornado sound.")]
    [SerializeField] private AudioSource tornadoAudioSource;

    [Tooltip("Sound that loops for the entire time the tornado is active.")]
    [SerializeField] private AudioClip tornadoLoopSound;

    [Header("Debug")]
    [SerializeField] private bool showDebugMessages = true;

    private PlayerMotor playerMotor;
    private bool tornadoActive;

    public bool TornadoActive => tornadoActive;

    private void Start()
    {
        FindPlayer();
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
            playerMotor.SetTornadoSlowdown(movementSpeedMultiplier);

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

        StopTornadoAudio();

        if (showDebugMessages)
            Debug.Log("Tornado ended.");
    }

    private void StopTornadoAudio()
    {
        if (tornadoAudioSource == null)
            return;

        tornadoAudioSource.Stop();
        tornadoAudioSource.loop = false;
        tornadoAudioSource.clip = null;
    }

    private void OnDisable()
    {
        if (playerMotor != null)
            playerMotor.SetTornadoSlowdown(1f);

        StopTornadoAudio();
        tornadoActive = false;
    }
}
