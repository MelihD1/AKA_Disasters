using UnityEngine;
using UnityEngine.InputSystem;

public class FirstPersonCameraController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to the input reader component on the player root.")]
    [SerializeField] private PlayerInputReader inputReader;

    [Tooltip("The root transform of the player to rotate horizontally (Yaw).")]
    [SerializeField] private Transform playerBody;

    [Header("Sensitivity")]
    [Range(0.01f, 1f)]
    [SerializeField] private float mouseSensitivity = 0.15f;

    [Header("Pitch Clamping")]
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    [Header("Options")]
    [SerializeField] private bool invertY = false;

    private float pitchAngle = 0f;

    void Awake()
    {
        if (playerBody == null && transform.parent != null)
        {
            playerBody = transform.root;
        }

        if (inputReader == null)
        {
            inputReader = GetComponentInParent<PlayerInputReader>();
        }
    }

    void Start()
    {
        SetCursorLocked(true);
    }

    void Update()
    {
        HandleCursorState();

        if (Cursor.lockState != CursorLockMode.Locked) return;

        RotateView();
    }

    private void RotateView()
    {
        if (inputReader == null || playerBody == null) return;

        Vector2 lookDelta = inputReader.LookInput;

        float mouseX = lookDelta.x * mouseSensitivity;
        float mouseY = lookDelta.y * mouseSensitivity * (invertY ? 1f : -1f);

        // 1. Vertical Rotation (Pitch) on Camera_Rig
        pitchAngle += mouseY;
        pitchAngle = Mathf.Clamp(pitchAngle, minPitch, maxPitch);
        transform.localRotation = Quaternion.Euler(pitchAngle, 0f, 0f);

        // 2. Horizontal Rotation (Yaw) on PlayerRoot
        playerBody.Rotate(Vector3.up * mouseX);
    }

    private void HandleCursorState()
    {
        // Using the new Input System API instead of legacy UnityEngine.Input
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SetCursorLocked(false);
        }
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        {
            SetCursorLocked(true);
        }
    }

    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}