using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct IngredientRequirement
{
    public ItemData item;
    [Min(1)]
    public int requiredAmount;
}

[CreateAssetMenu(fileName = "NewMergeRecipe", menuName = "Item System/Merge Recipe")]
public class MergeRecipeData : ScriptableObject
{
    [Header("Ingredients")]
    [Tooltip("All base items required to trigger the automatic merge.")]
    public List<IngredientRequirement> requiredIngredients = new List<IngredientRequirement>();

    [Header("Result")]
    [Tooltip("The resulting mixed item produced when ingredients are consumed.")]
    public ItemData resultItem;
}