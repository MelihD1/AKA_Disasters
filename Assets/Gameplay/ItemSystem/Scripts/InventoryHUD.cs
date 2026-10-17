using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // Complies with project New Input System contract

[RequireComponent(typeof(CanvasGroup))]
public class InventoryHUD : MonoBehaviour
{
    [Header("Source References")]
    [Tooltip("Leave empty to auto-find on the active player at runtime.")]
    [SerializeField] private PlayerInventory playerInventory;

    [Header("UI Prefab & Container")]
    [Tooltip("The PF_ItemCounterSlot prefab.")]
    [SerializeField] private ItemSlotUI slotPrefab;
    [Tooltip("Where slot instances are spawned (defaults to this transform).")]
    [SerializeField] private Transform slotsContainer;

    [Header("Display Settings")]
    [Tooltip("If true, the HUD starts hidden until 'I' is pressed.")]
    [SerializeField] private bool startHidden = true;

    private CanvasGroup _canvasGroup;
    private bool _isVisible;
    private readonly Dictionary<ItemData, ItemSlotUI> _activeSlots = new Dictionary<ItemData, ItemSlotUI>();

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();

        if (slotsContainer == null)
        {
            slotsContainer = transform;
        }

        SetVisibility(!startHidden);
    }

    private void Start()
    {
        if (playerInventory == null)
        {
            playerInventory = FindAnyObjectByType<PlayerInventory>();
        }

        if (playerInventory == null)
        {
            Debug.LogError("[InventoryHUD] Could not find PlayerInventory in scene.", this);
            return;
        }

        playerInventory.OnItemCountChanged += HandleItemCountChanged;
        playerInventory.OnItemMerged += HandleItemMerged;

        BuildHUD();
    }

    private void Update()
    {
        // Poll keyboard using the New Input System framework
        if (Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame)
        {
            ToggleVisibility();
        }
    }

    private void OnDestroy()
    {
        if (playerInventory != null)
        {
            playerInventory.OnItemCountChanged -= HandleItemCountChanged;
            playerInventory.OnItemMerged -= HandleItemMerged;
        }
    }

    public void ToggleVisibility()
    {
        SetVisibility(!_isVisible);
    }

    public void SetVisibility(bool visible)
    {
        _isVisible = visible;
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }
    }

    private void BuildHUD()
    {
        foreach (Transform child in slotsContainer)
        {
            Destroy(child.gameObject);
        }
        _activeSlots.Clear();

        MergeRecipeData recipe = playerInventory.ActiveRecipe;
        if (recipe == null) return;

        // 1. Slots for required base ingredients
        for (int i = 0; i < recipe.requiredIngredients.Count; i++)
        {
            ItemData item = recipe.requiredIngredients[i].item;
            if (item != null && !_activeSlots.ContainsKey(item))
            {
                CreateSlot(item);
            }
        }

        // 2. Slot for the merged mixed item
        if (recipe.resultItem != null && !_activeSlots.ContainsKey(recipe.resultItem))
        {
            CreateSlot(recipe.resultItem);
        }
    }

    private void CreateSlot(ItemData item)
    {
        if (slotPrefab == null)
        {
            Debug.LogError("[InventoryHUD] Slot Prefab is not assigned!", this);
            return;
        }

        ItemSlotUI slotInstance = Instantiate(slotPrefab, slotsContainer);
        int currentCount = playerInventory.GetItemCount(item);
        slotInstance.Initialize(item, currentCount);

        _activeSlots.Add(item, slotInstance);
    }

    private void HandleItemCountChanged(ItemData item, int newCount)
    {
        if (item != null && _activeSlots.TryGetValue(item, out ItemSlotUI slot))
        {
            slot.UpdateCount(newCount);
        }
    }

    private void HandleItemMerged(ItemData mergedItem, int totalMerged)
    {
        Debug.Log($"<color=cyan>[InventoryHUD] UI Merged Notification: {mergedItem.displayName} x{totalMerged}</color>");
    }
}