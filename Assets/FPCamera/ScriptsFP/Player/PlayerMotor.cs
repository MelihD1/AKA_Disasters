
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private Transform cameraOffset;

    [Header("Movement Speeds")]
    [SerializeField] private float walkSpeed = 6f;
    [SerializeField] private float sprintSpeed = 10f;

    [Header("Movement Feel")]
    [SerializeField] private float acceleration = 30f;
    [SerializeField] private float normalDeceleration = 18f;
    [SerializeField] private float sprintReleaseDeceleration = 5f;
    [SerializeField] private float airControl = 0.4f;
    [SerializeField] private float turnResponsiveness = 5f;

    [Header("Jumping & Gravity")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float jumpHeight = 1.4f;

    [Header("Crouching")]
    [SerializeField] private float crouchSpeed = 3f;
    [SerializeField] private float crouchHeight = 1.2f;
    [SerializeField] private float crouchCameraDrop = 0.35f;
    [SerializeField] private float crouchTransitionSpeed = 10f;

    [Header("Sliding")]
    [SerializeField] private float slideStartSpeed = 8f;
    [SerializeField] private float slideToCrouchSpeed = 3f;
    [SerializeField] private float slideFriction = 5.5f;
    [SerializeField] private float slideSteering = 8f;
    [SerializeField] private float slideHeight = 1.1f;
    [SerializeField] private float slideCameraDrop = 0.45f;
    [SerializeField] private float heightChangeSpeed = 8f;
    [SerializeField] private float slideCameraTransitionSpeed = 10f;

    private CharacterController controller;
    private Vector3 verticalVelocity;
    private Vector3 horizontalVelocity;

    private float originalHeight;
    private Vector3 originalCenter;
    private float currentHeight;

    private Vector3 cameraStandingPosition;
    private bool cameraPositionSaved;

    private bool isSliding;
    private bool isCrouching;

    public bool IsSliding => isSliding;
    public bool IsCrouching => isCrouching;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (inputReader == null)
            inputReader = GetComponent<PlayerInputReader>();

        originalHeight = controller.height;
        originalCenter = controller.center;
        currentHeight = originalHeight;

        crouchHeight = Mathf.Clamp(
            crouchHeight,
            controller.radius * 2f,
            originalHeight
        );

        slideHeight = Mathf.Clamp(
            slideHeight,
            controller.radius * 2f,
            crouchHeight
        );

        if (cameraOffset != null)
        {
            cameraStandingPosition = cameraOffset.localPosition;
            cameraPositionSaved = true;
        }
    }

    private void Update()
    {
        if (inputReader == null || controller == null)
            return;

        bool grounded = controller.isGrounded;

        UpdateCrouchAndSlideInput(grounded);
        ApplyMovement(grounded);
        ApplyGravityAndJump(grounded);
        UpdateControllerHeight();
        UpdateCameraPosition();
    }

    private void UpdateCrouchAndSlideInput(bool grounded)
    {
        bool controlHeld = IsControlHeld();

        Vector3 actualVelocity = controller.velocity;
        actualVelocity.y = 0f;

        float actualSpeed = Mathf.Max(
            horizontalVelocity.magnitude,
            actualVelocity.magnitude
        );

        if (!controlHeld)
        {
            isSliding = false;

            // Stay crouched if there isn't room to stand.
            isCrouching = currentHeight < originalHeight - 0.01f
                && !CanStandUp();
        }
        else if (isSliding)
        {
            // Transition from sliding to crouching when slow
            // or when leaving the ground.
            if (!grounded || actualSpeed <= slideToCrouchSpeed)
            {
                isSliding = false;
                isCrouching = true;
            }
        }
        else if (grounded && actualSpeed >= slideStartSpeed)
        {
            // Start sliding when moving fast enough.
            isSliding = true;
            isCrouching = false;
        }
        else
        {
            // Holding Ctrl at a lower speed means crouching.
            isSliding = false;
            isCrouching = true;
        }
    }

    private void ApplyMovement(bool grounded)
    {
        Vector2 input = Vector2.ClampMagnitude(
            inputReader.MoveInput, 1f
        );

        Vector3 direction =
            transform.right * input.x +
            transform.forward * input.y;

        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
            direction.Normalize();

        if (isSliding)
        {
            if (direction.sqrMagnitude > 0.001f)
            {
                Vector3 steeringTarget =
                    direction * horizontalVelocity.magnitude;

                horizontalVelocity = Vector3.MoveTowards(
                    horizontalVelocity,
                    steeringTarget,
                    slideSteering * Time.deltaTime
                );
            }

            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                Vector3.zero,
                slideFriction * Time.deltaTime
            );
        }
        else if (direction.sqrMagnitude > 0.001f)
        {
            float currentSpeed = horizontalVelocity.magnitude;

            float targetSpeed = isCrouching
                ? crouchSpeed
                : inputReader.IsSprinting
                    ? sprintSpeed
                    : walkSpeed;

            if (!isCrouching &&
                !inputReader.IsSprinting &&
                currentSpeed > walkSpeed)
            {
                targetSpeed = Mathf.MoveTowards(
                    currentSpeed,
                    walkSpeed,
                    sprintReleaseDeceleration * Time.deltaTime
                );
            }

            Vector3 targetVelocity = direction * targetSpeed;
            float control = grounded ? 1f : airControl;

            float directionDot = currentSpeed > 0.1f
                ? Vector3.Dot(
                    horizontalVelocity.normalized,
                    direction
                )
                : 1f;

            float turnRate = directionDot < 0.5f
                ? acceleration * turnResponsiveness
                : acceleration;

            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                targetVelocity,
                turnRate * control * Time.deltaTime
            );
        }
        else
        {
            float control = grounded ? 1f : airControl;

            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                Vector3.zero,
                normalDeceleration * control * Time.deltaTime
            );
        }

        controller.Move(horizontalVelocity * Time.deltaTime);
    }

    private void ApplyGravityAndJump(bool grounded)
    {
        if (grounded && verticalVelocity.y < 0f)
            verticalVelocity.y = -2f;

        if (grounded &&
            !isSliding &&
            !isCrouching &&
            inputReader.JumpTriggered)
        {
            verticalVelocity.y = Mathf.Sqrt(
                jumpHeight * -2f * gravity
            );
        }

        verticalVelocity.y += gravity * Time.deltaTime;
        controller.Move(verticalVelocity * Time.deltaTime);
    }

    private void UpdateControllerHeight()
    {
        float targetHeight = isSliding
            ? slideHeight
            : isCrouching
                ? crouchHeight
                : originalHeight;

        // Only stand up when there is enough room.
        if (targetHeight > currentHeight && !CanStandUp())
        {
            targetHeight = currentHeight;
            isCrouching = true;
        }

        currentHeight = Mathf.MoveTowards(
            currentHeight,
            targetHeight,
            heightChangeSpeed * Time.deltaTime
        );

        float heightDifference = originalHeight - currentHeight;

        controller.height = currentHeight;
        controller.center = originalCenter -
            Vector3.up * (heightDifference * 0.5f);
    }

    private bool CanStandUp()
    {
        float radius = controller.radius * 0.95f;

        Vector3 worldCenter =
            transform.TransformPoint(originalCenter);

        float halfSegment = Mathf.Max(
            0f,
            originalHeight * 0.5f - radius
        );

        Vector3 bottom = worldCenter -
            Vector3.up * halfSegment;

        Vector3 top = worldCenter +
            Vector3.up * halfSegment;

        Collider[] hits = Physics.OverlapCapsule(
            bottom,
            top,
            radius,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider hit in hits)
        {
            if (hit == controller ||
                hit.transform.IsChildOf(transform))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private void UpdateCameraPosition()
    {
        if (cameraOffset == null || !cameraPositionSaved)
            return;

        float drop = isSliding
            ? slideCameraDrop
            : isCrouching
                ? crouchCameraDrop
                : 0f;

        float speed = isSliding
            ? slideCameraTransitionSpeed
            : crouchTransitionSpeed;

        Vector3 targetPosition = cameraStandingPosition;
        targetPosition.y -= drop;

        cameraOffset.localPosition = Vector3.Lerp(
            cameraOffset.localPosition,
            targetPosition,
            1f - Mathf.Exp(-speed * Time.deltaTime)
        );
    }

    private bool IsControlHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.leftCtrlKey.isPressed ||
                   Keyboard.current.rightCtrlKey.isPressed;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.LeftControl) ||
               Input.GetKey(KeyCode.RightControl);
#else
        return false;
#endif
    }

    private void OnDisable()
    {
        isSliding = false;
        isCrouching = false;
        horizontalVelocity = Vector3.zero;
        verticalVelocity = Vector3.zero;

        if (controller != null)
        {
            controller.height = originalHeight;
            controller.center = originalCenter;
            currentHeight = originalHeight;
        }

        if (cameraOffset != null && cameraPositionSaved)
            cameraOffset.localPosition = cameraStandingPosition;
    }
}
