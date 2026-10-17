using UnityEngine;

public enum ItemType
{
    BaseIngredient,
    MergedResult
}

[CreateAssetMenu(fileName = "NewItem", menuName = "Item System/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("Unique ID for this item (e.g., item_01, item_merged).")]
    public string itemID;
    public string displayName;

    [Header("Classification")]
    [Tooltip("MergedResult items cannot be used as ingredients in further merges.")]
    public ItemType itemType = ItemType.BaseIngredient;

    [Header("UI Visuals")]
    [Tooltip("Icon displayed in the top HUD counter.")]
    public Sprite icon;

    [Header("World Visuals (Optional Reference)")]
    [Tooltip("Prefab instantiated in the world (if spawned dynamically).")]
    public GameObject worldPrefab;
}