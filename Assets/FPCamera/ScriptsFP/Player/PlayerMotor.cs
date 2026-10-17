using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to the input reader. Auto-assigned if left blank.")]
    [SerializeField] private PlayerInputReader inputReader;

    [Header("Movement Speeds")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8.5f;

    [Header("Physics & Gravity")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float jumpHeight = 1.2f;

    private CharacterController controller;
    private Vector3 verticalVelocity = Vector3.zero;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (inputReader == null)
        {
            inputReader = GetComponent<PlayerInputReader>();
        }
    }

    void Update()
    {
        if (inputReader == null || controller == null) return;

        ApplyMovement();
        ApplyGravityAndJump();
    }

    private void ApplyMovement()
    {
        Vector2 input = inputReader.MoveInput;

        // Convert the 2D input into horizontal world movement relative to player facing direction
        Vector3 moveDirection = transform.right * input.x + transform.forward * input.y;

        // Pick current speed based on whether sprint is held
        float currentSpeed = inputReader.IsSprinting ? sprintSpeed : walkSpeed;

        // Move the CharacterController across the horizontal plane
        controller.Move(moveDirection * currentSpeed * Time.deltaTime);
    }

    private void ApplyGravityAndJump()
    {
        bool isGrounded = controller.isGrounded;

        // Keep the player grounded on slight bumps/slopes
        if (isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = -2f;
        }

        // Jump when jump input is triggered and the player is currently grounded
        if (isGrounded && inputReader.JumpTriggered)
        {
            verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Accumulate gravity downwards over time
        verticalVelocity.y += gravity * Time.deltaTime;

        // Apply vertical movement
        controller.Move(verticalVelocity * Time.deltaTime);
    }
}