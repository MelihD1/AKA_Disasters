using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputReader : MonoBehaviour
{
    // Public properties that other scripts can read, but only this script can modify
    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool JumpTriggered { get; private set; }
    public bool IsSprinting { get; private set; }

    void Update()
    {
        ReadLookInput();
        ReadMovementInput();
    }

    private void ReadLookInput()
    {
        // Read mouse delta (how far the mouse moved this frame in X and Y)
        if (Mouse.current != null)
        {
            LookInput = Mouse.current.delta.ReadValue();
        }
        else
        {
            LookInput = Vector2.zero;
        }
    }

    private void ReadMovementInput()
    {
        if (Keyboard.current == null)
        {
            MoveInput = Vector2.zero;
            JumpTriggered = false;
            IsSprinting = false;
            return;
        }

        // Calculate horizontal (X) and vertical (Y) direction from WASD / Arrow keys
        float x = 0f;
        float y = 0f;

        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) y += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) y -= 1f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;

        // Vector2.ClampMagnitude prevents diagonal walking from being faster than straight walking
        MoveInput = Vector2.ClampMagnitude(new Vector2(x, y), 1f);

        // Action flags
        JumpTriggered = Keyboard.current.spaceKey.wasPressedThisFrame;
        IsSprinting = Keyboard.current.leftShiftKey.isPressed;
    }
}