
using System.Collections;
using UnityEngine;

public class DoorController : MonoBehaviour, IInteractable
{
    [Header("Hinge Reference")]
    [Tooltip("The swinging pivot of the door. If left blank, rotates this GameObject.")]
    [SerializeField] private Transform hingeTransform;

    [Header("Door Settings")]
    [Tooltip("Angle relative to closed state.")]
    [SerializeField] private float openAngle = 90f;

    [Tooltip("Opening time at normal walking speed.")]
    [SerializeField] private float baseDuration = 0.8f;

    [Tooltip("Reference walking speed.")]
    [SerializeField] private float referenceWalkSpeed = 5f;

    [Tooltip("Maximum speed multiplier when moving quickly.")]
    [SerializeField] private float maxSpeedMultiplier = 3.5f;

    [Tooltip("Fastest possible door swing duration.")]
    [SerializeField] private float minimumDuration = 0.12f;

    [Header("Proximity Auto-Open")]
    [SerializeField] private bool autoOpenOnApproach = true;
    [SerializeField] private bool autoCloseOnExit = true;

    [Header("Audio (Optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSfx;
    [SerializeField] private AudioClip closeSfx;

    public string InteractionPrompt =>
        isOpen ? "Press E to Close" : "Press E to Open";

    private Quaternion closedRotation;
    private Quaternion openRotation;

    private bool isOpen;
    private Coroutine currentSwingCoroutine;
    private int collidersInside;

    private Transform TargetHinge =>
        hingeTransform != null ? hingeTransform : transform;

    private void Awake()
    {
        closedRotation = TargetHinge.localRotation;
    }

    public void Interact(InteractionContext context)
    {
        float speed = context.MovementSpeed;

        if (isOpen)
        {
            CloseDoor(speed);
        }
        else
        {
            Vector3 interactorPosition = context.Interactor != null
                ? context.Interactor.transform.position
                : transform.position - transform.right;

            OpenDoor(interactorPosition, speed);
        }
    }

    public void OpenDoor(
        Vector3 approacherPosition,
        float interactorSpeed = 0f)
    {
        if (isOpen)
            return;

        Vector3 directionToPlayer =
            (approacherPosition - transform.position).normalized;

        float dotProduct = Vector3.Dot(
            transform.right,
            directionToPlayer
        );

        // Swing away from the player.
        float swingAngle = dotProduct >= 0f
            ? -openAngle
            : openAngle;

        openRotation = closedRotation *
            Quaternion.Euler(0f, swingAngle, 0f);

        PlaySfx(openSfx);
        StartSwing(openRotation, interactorSpeed);

        isOpen = true;
    }

    public void CloseDoor(float interactorSpeed = 0f)
    {
        if (!isOpen)
            return;

        PlaySfx(closeSfx);
        StartSwing(closedRotation, interactorSpeed);

        isOpen = false;
    }

    private void StartSwing(
        Quaternion targetRotation,
        float interactorSpeed)
    {
        if (currentSwingCoroutine != null)
        {
            StopCoroutine(currentSwingCoroutine);
            currentSwingCoroutine = null;
        }

        float safeReferenceSpeed =
            Mathf.Max(0.1f, referenceWalkSpeed);

        float speedRatio =
            Mathf.Max(0f, interactorSpeed) / safeReferenceSpeed;

        // Normal walking speed uses the normal duration.
        // Higher speeds shorten the duration.
        float speedMultiplier = Mathf.Lerp(
            1f,
            Mathf.Max(1f, maxSpeedMultiplier),
            Mathf.Clamp01((speedRatio - 1f) / 2f)
        );

        float duration = Mathf.Max(
            minimumDuration,
            baseDuration / speedMultiplier
        );

        currentSwingCoroutine = StartCoroutine(
            SmoothSwing(targetRotation, duration)
        );
    }

    private IEnumerator SmoothSwing(
        Quaternion targetRotation,
        float duration)
    {
        Quaternion startRotation = TargetHinge.localRotation;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            // Starts quickly and eases into the final position.
            float easedT = 1f - Mathf.Pow(1f - t, 3f);

            TargetHinge.localRotation = Quaternion.Slerp(
                startRotation,
                targetRotation,
                easedT
            );

            yield return null;
        }

        TargetHinge.localRotation = targetRotation;
        currentSwingCoroutine = null;
    }

    private void PlaySfx(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!autoOpenOnApproach)
            return;

        if (IsPlayerCollider(other))
        {
            collidersInside++;

            if (!isOpen)
            {
                float speed = GetPlayerSpeed(other);
                OpenDoor(other.transform.position, speed);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!autoOpenOnApproach || !autoCloseOnExit)
            return;

        if (IsPlayerCollider(other))
        {
            collidersInside = Mathf.Max(0, collidersInside - 1);

            if (collidersInside == 0 && isOpen)
            {
                CloseDoor(GetPlayerSpeed(other));
            }
        }
    }

    private bool IsPlayerCollider(Collider other)
    {
        return other.CompareTag("Player") ||
               other.GetComponentInParent<CharacterController>() != null;
    }

    private float GetPlayerSpeed(Collider other)
    {
        CharacterController cc =
            other.GetComponentInParent<CharacterController>();

        if (cc != null)
        {
            // Horizontal speed is more relevant to door interactions.
            Vector3 velocity = cc.velocity;
            velocity.y = 0f;

            return velocity.magnitude;
        }

        return 0f;
    }
}
