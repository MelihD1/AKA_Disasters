
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

    [Header("Sliding")]
    [SerializeField] private float slideStartSpeed = 8f;
    [SerializeField] private float slideFriction = 5.5f;
    [SerializeField] private float slideSteering = 8f;
    [SerializeField] private float slideHeight = 1.1f;
    [SerializeField] private float heightChangeSpeed = 8f;

    [Header("Slide Camera")]
    [SerializeField] private float slideCameraDrop = 0.45f;
    [SerializeField] private float cameraTransitionSpeed = 10f;

    private CharacterController controller;
    private Vector3 verticalVelocity;
    private Vector3 horizontalVelocity;

    private float originalHeight;
    private Vector3 originalCenter;
    private float currentHeight;

    private Vector3 cameraStandingPosition;
    private bool cameraPositionSaved;

    private bool isSliding;

    public bool IsSliding => isSliding;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (inputReader == null)
            inputReader = GetComponent<PlayerInputReader>();

        originalHeight = controller.height;
        originalCenter = controller.center;
        currentHeight = originalHeight;

        slideHeight = Mathf.Clamp(
            slideHeight,
            controller.radius * 2f,
            originalHeight
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

        UpdateSlideInput(grounded);
        ApplyMovement(grounded);
        ApplyGravityAndJump(grounded);
        UpdateControllerHeight();
        UpdateSlideCamera();
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
            // Keep momentum during a slide, with steering.
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

            float targetSpeed = inputReader.IsSprinting
                ? sprintSpeed
                : walkSpeed;

            // Preserve some sprint speed after Shift is released.
            if (!inputReader.IsSprinting &&
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

            // Quickly remove velocity in the old direction
            // when making a sharp turn or a 180-degree reversal.
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

            // Stop smoothly when no directional input is held.
            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                Vector3.zero,
                normalDeceleration * control * Time.deltaTime
            );
        }

        controller.Move(horizontalVelocity * Time.deltaTime);
    }

    private void UpdateSlideInput(bool grounded)
    {
        bool controlHeld = IsControlHeld();

        if (!isSliding &&
            controlHeld &&
            grounded &&
            horizontalVelocity.magnitude >= slideStartSpeed)
        {
            isSliding = true;
        }

        if (isSliding && (!controlHeld || !grounded))
        {
            isSliding = false;
        }
    }

    private void ApplyGravityAndJump(bool grounded)
    {
        if (grounded && verticalVelocity.y < 0f)
            verticalVelocity.y = -2f;

        if (grounded && !isSliding && inputReader.JumpTriggered)
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
            : originalHeight;

        if (!isSliding &&
            currentHeight < originalHeight &&
            !CanStandUp())
        {
            targetHeight = currentHeight;
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

        Vector3 bottom = worldCenter +
            Vector3.up * (-originalHeight * 0.5f + radius);

        Vector3 top = worldCenter +
            Vector3.up * (originalHeight * 0.5f - radius);

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

    private void UpdateSlideCamera()
    {
        if (cameraOffset == null || !cameraPositionSaved)
            return;

        Vector3 targetPosition = cameraStandingPosition;

        if (isSliding)
            targetPosition.y -= slideCameraDrop;

        cameraOffset.localPosition = Vector3.Lerp(
            cameraOffset.localPosition,
            targetPosition,
            1f - Mathf.Exp(
                -cameraTransitionSpeed * Time.deltaTime
            )
        );
    }

    private bool IsControlHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            (Keyboard.current.leftCtrlKey.isPressed ||
             Keyboard.current.rightCtrlKey.isPressed))
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.LeftControl) ||
            Input.GetKey(KeyCode.RightControl))
        {
            return true;
        }
#endif

        return false;
    }

    private void OnDisable()
    {
        isSliding = false;
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
