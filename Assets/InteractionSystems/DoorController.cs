using System.Collections;
using UnityEngine;

public class DoorController : MonoBehaviour, IInteractable
{
    [Header("Door Settings")]
    [Tooltip("Angle relative to closed state (e.g. 90 degrees).")]
    [SerializeField] private float openAngle = 90f;
    [Tooltip("Base duration (in seconds) to fully swing open at normal walk speed.")]
    [SerializeField] private float baseDuration = 0.8f;
    [Tooltip("Reference walk speed to scale rotation timing from.")]
    [SerializeField] private float referenceWalkSpeed = 5f;

    [Header("Proximity Auto-Open")]
    [Tooltip("If true, the door swings open automatically when entering a trigger collider.")]
    [SerializeField] private bool autoOpenOnApproach = true;
    [Tooltip("Automatically closes when player leaves the trigger zone.")]
    [SerializeField] private bool autoCloseOnExit = true;

    [Header("Audio (Optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSfx;
    [SerializeField] private AudioClip closeSfx;

    public string InteractionPrompt => isOpen ? "Press E to Close" : "Press E to Open";

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private bool isOpen = false;
    private Coroutine currentSwingCoroutine;

    private void Awake()
    {
        closedRotation = transform.localRotation;
    }

    public void Interact(InteractionContext context)
    {
        if (isOpen)
        {
            CloseDoor(context.MovementSpeed);
        }
        else
        {
            OpenDoor(context.Interactor != null ? context.Interactor.transform.position : transform.position - transform.forward, context.MovementSpeed);
        }
    }

    public void OpenDoor(Vector3 approacherPosition, float interactorSpeed = 0f)
    {
        if (isOpen) return;

        // Calculate direction to player relative to the door's forward vector.
        // If approaching from front (dot > 0), swing backward (-openAngle). Otherwise swing forward (+openAngle).
        Vector3 directionToPlayer = (approacherPosition - transform.position).normalized;
        float dotProduct = Vector3.Dot(transform.forward, directionToPlayer);
        float swingAngle = dotProduct >= 0f ? -openAngle : openAngle;

        openRotation = closedRotation * Quaternion.Euler(0f, swingAngle, 0f);

        PlaySfx(openSfx);
        StartSwing(openRotation, interactorSpeed);
        isOpen = true;
    }

    public void CloseDoor(float interactorSpeed = 0f)
    {
        if (!isOpen) return;

        PlaySfx(closeSfx);
        StartSwing(closedRotation, interactorSpeed);
        isOpen = false;
    }

    private void StartSwing(Quaternion targetRotation, float interactorSpeed)
    {
        if (currentSwingCoroutine != null)
        {
            StopCoroutine(currentSwingCoroutine);
        }

        // Scale duration based on player speed: faster speed = shorter swing time
        float duration = baseDuration;
        if (interactorSpeed > referenceWalkSpeed && referenceWalkSpeed > 0f)
        {
            float speedMultiplier = interactorSpeed / referenceWalkSpeed;
            duration = Mathf.Max(0.2f, baseDuration / speedMultiplier);
        }

        currentSwingCoroutine = StartCoroutine(SmoothSwing(targetRotation, duration));
    }

    private IEnumerator SmoothSwing(Quaternion targetRotation, float duration)
    {
        Quaternion startRotation = transform.localRotation;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // SmoothStep curve for natural mechanical acceleration and deceleration
            t = t * t * (3f - 2f * t);

            transform.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        transform.localRotation = targetRotation;
        currentSwingCoroutine = null;
    }

    private void PlaySfx(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // Auto-Open Proximity Triggers
    private void OnTriggerEnter(Collider other)
    {
        if (!autoOpenOnApproach || isOpen) return;

        if (other.CompareTag("Player") || other.GetComponentInParent<CharacterController>() != null)
        {
            float speed = 0f;
            var cc = other.GetComponentInParent<CharacterController>();
            if (cc != null) speed = cc.velocity.magnitude;

            OpenDoor(other.transform.position, speed);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!autoOpenOnApproach || !autoCloseOnExit || !isOpen) return;

        if (other.CompareTag("Player") || other.GetComponentInParent<CharacterController>() != null)
        {
            float speed = 0f;
            var cc = other.GetComponentInParent<CharacterController>();
            if (cc != null) speed = cc.velocity.magnitude;

            CloseDoor(speed);
        }
    }
}