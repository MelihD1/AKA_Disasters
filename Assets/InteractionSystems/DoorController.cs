
using System.Collections.Generic;
using UnityEngine;

public class DoorController : MonoBehaviour, IInteractable
{
    [Header("Hinge Reference")]
    [SerializeField] private Transform hingeTransform;

    [Header("Door Settings")]
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float baseDuration = 0.8f;
    [SerializeField] private float referenceWalkSpeed = 5f;
    [SerializeField] private float maxSpeedMultiplier = 3.5f;
    [SerializeField] private float minimumDuration = 0.12f;

    [Header("Player Tracking")]
    [SerializeField] private float approachDistance = 2.5f;
    [SerializeField] private float stopDistance = 1.2f;
    [SerializeField] private float movementThreshold = 0.08f;
    [SerializeField] private float rotationSmoothness = 12f;

    [Header("Proximity Auto-Open")]
    [SerializeField] private bool autoOpenOnApproach = true;
    [SerializeField] private bool autoCloseOnExit = true;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSfx;
    [SerializeField] private AudioClip closeSfx;

    public string InteractionPrompt =>
        isOpen ? "Press E to Close" : "Press E to Open";

    private Quaternion closedRotation;
    private Quaternion openRotation;

    private bool isOpen;
    private bool manuallyControlled;

    private Transform trackedPlayer;
    private CharacterController trackedController;

    private Vector3 previousPlayerPosition;
    private float currentSwingAngle;
    private float targetSwingAngle;
    private float selectedSwingDirection = 1f;

    private readonly HashSet<Collider> playerColliders =
        new HashSet<Collider>();

    private Transform TargetHinge =>
        hingeTransform != null ? hingeTransform : transform;

    private void Awake()
    {
        closedRotation = TargetHinge.localRotation;
        currentSwingAngle = 0f;
        targetSwingAngle = 0f;
    }

    private void Update()
    {
        CleanPlayerColliders();

        if (trackedPlayer == null || !isOpen || manuallyControlled)
            return;

        float distance = GetPlayerDistance();

        Vector3 playerMovement =
            trackedPlayer.position - previousPlayerPosition;

        playerMovement.y = 0f;

        float frameSpeed = playerMovement.magnitude /
                           Mathf.Max(Time.deltaTime, 0.0001f);

        previousPlayerPosition = trackedPlayer.position;

        // Stop the door from rotating if the player stops approaching.
        if (frameSpeed < movementThreshold)
            return;

        // Do not keep pushing the door if the player is too far away.
        if (distance > approachDistance)
            return;

        // Use player speed to determine how quickly the door opens.
        float speedRatio = frameSpeed /
                           Mathf.Max(0.1f, referenceWalkSpeed);

        float speedMultiplier = Mathf.Lerp(
            0.5f,
            Mathf.Max(1f, maxSpeedMultiplier),
            Mathf.Clamp01(speedRatio)
        );

        // The closer the player is, the more urgently the door moves.
        float distanceFactor = Mathf.InverseLerp(
            approachDistance,
            stopDistance,
            distance
        );

        distanceFactor = Mathf.Clamp01(distanceFactor);

        float angularSpeed = (openAngle / Mathf.Max(0.01f, baseDuration))
                             * speedMultiplier
                             * Mathf.Lerp(0.5f, 1.5f, distanceFactor);

        currentSwingAngle = Mathf.MoveTowards(
            currentSwingAngle,
            targetSwingAngle,
            angularSpeed * Time.deltaTime
        );

        ApplyDoorRotation();
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
            Vector3 playerPosition = context.Interactor != null
                ? context.Interactor.transform.position
                : transform.position - transform.right;

            OpenDoor(playerPosition, speed);

            if (context.Interactor != null)
            {
                SetTrackedPlayer(context.Interactor.transform);
                manuallyControlled = true;
            }
        }
    }


    public void OpenDoor(
        Vector3 approacherPosition,
        float interactorSpeed = 0f)
    {
        // Never recalculate the direction if the door is already open.
        if (isOpen)
            return;

        Vector3 directionToPlayer =
            approacherPosition - TargetHinge.position;

        directionToPlayer.y = 0f;

        if (directionToPlayer.sqrMagnitude > 0.001f)
            directionToPlayer.Normalize();

        float dotProduct = Vector3.Dot(
            TargetHinge.right,
            directionToPlayer
        );

        // Choose the swing direction once, when opening begins.
        selectedSwingDirection = dotProduct >= 0f ? -1f : 1f;

        openRotation = closedRotation *
            Quaternion.Euler(
                0f,
                selectedSwingDirection * openAngle,
                0f
            );

        isOpen = true;
        manuallyControlled = false;
        targetSwingAngle = openAngle;

        if (currentSwingAngle < 0.01f)
            currentSwingAngle = 0f;

        PlaySfx(openSfx);
    }


    public void CloseDoor(float interactorSpeed = 0f)
    {
        if (!isOpen)
            return;

        isOpen = false;
        manuallyControlled = false;

        targetSwingAngle = 0f;
        trackedPlayer = null;
        trackedController = null;

        PlaySfx(closeSfx);
    }

    private void SetTrackedPlayer(Transform player)
    {
        trackedPlayer = player;
        trackedController =
            player.GetComponentInParent<CharacterController>();

        previousPlayerPosition = player.position;
    }

    private float GetPlayerDistance()
    {
        if (trackedPlayer == null)
            return float.MaxValue;

        Vector3 playerPosition = trackedPlayer.position;
        Vector3 hingePosition = TargetHinge.position;

        playerPosition.y = 0f;
        hingePosition.y = 0f;

        return Vector3.Distance(playerPosition, hingePosition);
    }

    private void ApplyDoorRotation()
    {
        Quaternion targetRotation = closedRotation *
            Quaternion.Euler(
                0f,
                selectedSwingDirection * currentSwingAngle,
                0f
            );

        TargetHinge.localRotation = Quaternion.Slerp(
            TargetHinge.localRotation,
            targetRotation,
            1f - Mathf.Exp(-rotationSmoothness * Time.deltaTime)
        );
    }

    private void LateUpdate()
    {
        if (isOpen && !manuallyControlled)
            ApplyDoorRotation();
        else if (!isOpen)
        {
            currentSwingAngle = Mathf.MoveTowards(
                currentSwingAngle,
                0f,
                (openAngle / Mathf.Max(0.01f, baseDuration))
                * Time.deltaTime
            );

            ApplyDoorRotation();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        playerColliders.Add(other);

        CharacterController controller =
            other.GetComponentInParent<CharacterController>();

        Transform player = controller != null
            ? controller.transform
            : other.transform;

        SetTrackedPlayer(player);

        if (autoOpenOnApproach && !isOpen)
        {
            OpenDoor(
                player.position,
                GetPlayerSpeed(other)
            );
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        playerColliders.Remove(other);
        CleanPlayerColliders();

        if (playerColliders.Count == 0)
        {
            trackedPlayer = null;
            trackedController = null;

            if (autoOpenOnApproach && autoCloseOnExit && isOpen)
                CloseDoor();
        }
    }

    private void CleanPlayerColliders()
    {
        playerColliders.RemoveWhere(
            c => c == null ||
                 !c.enabled ||
                 !c.gameObject.activeInHierarchy
        );
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

        if (cc == null)
            return 0f;

        Vector3 velocity = cc.velocity;
        velocity.y = 0f;

        return velocity.magnitude;
    }

    private void PlaySfx(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}
