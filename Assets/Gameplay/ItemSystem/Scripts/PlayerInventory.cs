using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    [Header("Recipe Configuration")]
    [Tooltip("The recipe defining what items merge into the final mixed item.")]
    [SerializeField] private MergeRecipeData activeRecipe;

    // Runtime state: tracks counts of base ingredients
    private readonly Dictionary<ItemData, int> _itemCounts = new Dictionary<ItemData, int>();

    // Runtime state: count of completed merged items (which cannot be mixed further)
    private int _mergedItemCount = 0;

    // Events for UI and gameplay audio/feedback
    public event Action<ItemData, int> OnItemCountChanged;
    public event Action<ItemData, int> OnItemMerged; // Passes result item and new merged total

    public int MergedItemCount => _mergedItemCount;
    public MergeRecipeData ActiveRecipe => activeRecipe;

    /// <summary>
    /// Returns the current quantity held for a specific item.
    /// </summary>
    public int GetItemCount(ItemData item)
    {
        if (item == null) return 0;

        if (item.itemType == ItemType.MergedResult)
        {
            return _mergedItemCount;
        }

        return _itemCounts.TryGetValue(item, out int count) ? count : 0;
    }

    /// <summary>
    /// Adds an item to the inventory and checks if an auto-merge can occur.
    /// </summary>
    public bool AddItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;

        // If picking up an already merged item directly
        if (item.itemType == ItemType.MergedResult)
        {
            _mergedItemCount += amount;
            OnItemCountChanged?.Invoke(item, _mergedItemCount);
            return true;
        }

        // Add to base ingredient count
        if (_itemCounts.ContainsKey(item))
        {
            _itemCounts[item] += amount;
        }
        else
        {
            _itemCounts[item] = amount;
        }

        OnItemCountChanged?.Invoke(item, _itemCounts[item]);

        // Evaluate whether we can auto-merge
        CheckAndExecuteMerge();

        return true;
    }

    /// <summary>
    /// Verifies if all ingredients in the recipe are present in sufficient amounts.
    /// Deducts ingredients and increments the merged item counter if true.
    /// </summary>
    private void CheckAndExecuteMerge()
    {
        if (activeRecipe == null || activeRecipe.resultItem == null) return;
        if (activeRecipe.requiredIngredients == null || activeRecipe.requiredIngredients.Count == 0) return;

        // Loop handles edge case where player picks up a batch that satisfies multiple merges at once
        while (CanCraftRecipe(activeRecipe))
        {
            // Consume required ingredients
            for (int i = 0; i < activeRecipe.requiredIngredients.Count; i++)
            {
                IngredientRequirement req = activeRecipe.requiredIngredients[i];
                _itemCounts[req.item] -= req.requiredAmount;
                OnItemCountChanged?.Invoke(req.item, _itemCounts[req.item]);
            }

            // Grant the merged result
            _mergedItemCount++;
            OnItemCountChanged?.Invoke(activeRecipe.resultItem, _mergedItemCount);
            OnItemMerged?.Invoke(activeRecipe.resultItem, _mergedItemCount);

            Debug.Log($"[PlayerInventory] Auto-Merged! Total '{activeRecipe.resultItem.displayName}' count: {_mergedItemCount}");
        }
    }

    private bool CanCraftRecipe(MergeRecipeData recipe)
    {
        for (int i = 0; i < recipe.requiredIngredients.Count; i++)
        {
            IngredientRequirement req = recipe.requiredIngredients[i];
            if (req.item == null) return false;

            int currentAmount = _itemCounts.TryGetValue(req.item, out int count) ? count : 0;
            if (currentAmount < req.requiredAmount)
            {
                return false;
            }
        }
        return true;
    }
}