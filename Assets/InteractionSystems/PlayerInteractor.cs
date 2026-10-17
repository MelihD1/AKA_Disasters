using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Detection Settings")]
    [Tooltip("Maximum distance to interact with objects.")]
    [SerializeField] private float reachDistance = 3f;
    [Tooltip("Layer mask filtering interactable objects.")]
    [SerializeField] private LayerMask interactableLayer;

    [Header("Input (Optional Reference)")]
    [Tooltip("Optional Input Action Reference for Interact. If empty, falls back to Keyboard 'E'.")]
    [SerializeField] private InputActionReference interactAction;

    [Header("UI / Debug Prompt")]
    [SerializeField] private bool showDebugRay = true;

    private Camera cam;
    private CharacterController characterController;
    private IInteractable currentInteractable;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            cam = Camera.main;
        }

        // Cache CharacterController from the player root to read real-time movement speed
        characterController = GetComponentInParent<CharacterController>();
    }

    private void OnEnable()
    {
        if (interactAction != null && interactAction.action != null)
        {
            interactAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (interactAction != null && interactAction.action != null)
        {
            interactAction.action.Disable();
        }
    }

    private void Update()
    {
        DetectInteractable();
        HandleInput();
    }

    private void DetectInteractable()
    {
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        RaycastHit hit;

        if (showDebugRay)
        {
            Debug.DrawRay(ray.origin, ray.direction * reachDistance, Color.cyan);
        }

        if (Physics.Raycast(ray, out hit, reachDistance, interactableLayer))
        {
            // Check the hit object or its parent for an IInteractable component
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable == null)
            {
                interactable = hit.collider.GetComponent<IInteractable>();
            }

            currentInteractable = interactable;
        }
        else
        {
            currentInteractable = null;
        }
    }

    private void HandleInput()
    {
        bool wasTriggered = false;

        // Check assigned Input Action first
        if (interactAction != null && interactAction.action != null)
        {
            wasTriggered = interactAction.action.WasPressedThisFrame();
        }
        // Fallback to direct 'E' key press to keep it failproof
        else if (Keyboard.current != null)
        {
            wasTriggered = Keyboard.current.eKey.wasPressedThisFrame;
        }

        if (wasTriggered && currentInteractable != null)
        {
            float speed = 0f;
            if (characterController != null)
            {
                speed = characterController.velocity.magnitude;
            }

            GameObject interactorObj = characterController != null ? characterController.gameObject : gameObject;
            InteractionContext context = new InteractionContext(interactorObj, speed);

            currentInteractable.Interact(context);
        }
    }
}