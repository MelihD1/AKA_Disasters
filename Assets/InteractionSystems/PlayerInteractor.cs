using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Target optical camera to raycast from (Main Camera). Factored for head bob & recoil.")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private CharacterController characterController;

    [Header("Detection Settings")]
    [SerializeField] private float reachDistance = 3f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private bool showDebugRay = true;

    private IInteractable currentInteractable;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        if (inputReader == null)
            inputReader = GetComponent<PlayerInputReader>();

        if (characterController == null)
            characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        DetectInteractable();
        HandleInput();
    }

    private void DetectInteractable()
    {
        if (playerCamera == null) return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hit;

        if (showDebugRay)
        {
            Debug.DrawRay(ray.origin, ray.direction * reachDistance, Color.cyan);
        }

        if (Physics.Raycast(ray, out hit, reachDistance, interactableLayer))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>() 
                                      ?? hit.collider.GetComponent<IInteractable>();
            currentInteractable = interactable;
        }
        else
        {
            currentInteractable = null;
        }
    }

    private void HandleInput()
    {
        // 1. Guard: Ignore interaction if cursor is unlocked (menus/UI)
        if (Cursor.lockState != CursorLockMode.Locked) return;

        // 2. Guard: Respect disabled input during cutscenes/dialogue
        if (inputReader != null && !inputReader.enabled) return;

        // 3. Fallback/Direct check for 'E'
        bool interactPressed = UnityEngine.InputSystem.Keyboard.current != null 
                               && UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame;

        if (interactPressed && currentInteractable != null)
        {
            // Planar horizontal speed only (ignores jump/gravity inflation)
            float speed = 0f;
            if (characterController != null)
            {
                Vector3 flatVel = new Vector3(characterController.velocity.x, 0f, characterController.velocity.z);
                speed = flatVel.magnitude;
            }

            InteractionContext context = new InteractionContext(gameObject, speed);
            currentInteractable.Interact(context);
        }
    }
}