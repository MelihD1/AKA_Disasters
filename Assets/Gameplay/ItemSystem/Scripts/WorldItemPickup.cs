using UnityEngine;

public enum PickupMode
{
    ProximityOnly,
    ManualPressE,
    Both
}

public class WorldItemPickup : MonoBehaviour, IInteractable
{
    [Header("Item Configuration")]
    [Tooltip("The data asset defining which item this world object gives.")]
    [SerializeField] private ItemData itemData;
    [SerializeField, Min(1)] private int amount = 1;

    [Header("Interaction Settings")]
    [Tooltip("ProximityOnly automatically collects when entering the trigger. ManualPressE requires looking at the item and pressing E.")]
    [SerializeField] private PickupMode pickupMode = PickupMode.ProximityOnly;

    [Tooltip("Prompt displayed if manual interaction is enabled.")]
    [SerializeField] private string promptText = "Pick up";

    [Header("Feedback (Optional)")]
    [SerializeField] private AudioClip pickupSound;

    // Guard against multiple simultaneous triggers in the same frame
    private bool _isCollected = false;

    public ItemData Item => itemData;

    #region IInteractable Implementation

    public string InteractionPrompt => itemData != null ? $"{promptText} {itemData.displayName}" : promptText;

    public void Interact(InteractionContext context)
    {
        if (_isCollected) return;
        if (pickupMode == PickupMode.ProximityOnly) return;

        // Context passes the interactor GameObject (typically PlayerRoot)
        if (context.Interactor != null)
        {
            PlayerInventory inventory = context.Interactor.GetComponentInParent<PlayerInventory>();
            if (inventory == null)
            {
                inventory = context.Interactor.GetComponentInChildren<PlayerInventory>();
            }

            if (inventory != null)
            {
                ExecutePickup(inventory);
            }
        }
    }

    #endregion

    #region Proximity Trigger Handling

    private void OnTriggerEnter(Collider other)
    {
        if (_isCollected) return;
        if (pickupMode == PickupMode.ManualPressE) return;

        // Look for PlayerInventory on the entering body or its root
        PlayerInventory inventory = other.GetComponentInParent<PlayerInventory>();
        if (inventory == null)
        {
            inventory = other.GetComponentInChildren<PlayerInventory>();
        }

        if (inventory != null)
        {
            ExecutePickup(inventory);
        }
    }

    #endregion

    private void ExecutePickup(PlayerInventory inventory)
    {
        if (itemData == null)
        {
            Debug.LogWarning($"[WorldItemPickup] ItemData is not assigned on '{gameObject.name}'.", this);
            return;
        }

        _isCollected = true;

        if (inventory.AddItem(itemData, amount))
        {
            if (pickupSound != null)
            {
                AudioSource.PlayClipAtPoint(pickupSound, transform.position);
            }

            Destroy(gameObject);
        }
        else
        {
            // Reset if addition failed for any custom gameplay reason
            _isCollected = false;
        }
    }
}