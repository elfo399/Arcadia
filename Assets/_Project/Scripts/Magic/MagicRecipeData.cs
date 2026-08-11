using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MagicRecipe", menuName = "Arcadia/Magic/Magic Recipe")]
public sealed class MagicRecipeData : ScriptableObject
{
    public string recipeId;
    public MagicItemData resultMagic;
    public MagicRecipeUnlockType unlockType = MagicRecipeUnlockType.Default;

    [Header("Learn Cost")]
    [Min(0)] public int learnCoinCost;
    public List<MagicMaterialRequirement> learnMaterialRequirements = new List<MagicMaterialRequirement>();

    [Header("Create Cost")]
    [Min(0)] public int createCoinCost;
    public List<MagicMaterialRequirement> createMaterialRequirements = new List<MagicMaterialRequirement>();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(recipeId) && !string.IsNullOrWhiteSpace(name))
            recipeId = name.Trim();
        learnCoinCost = Mathf.Max(0, learnCoinCost);
        createCoinCost = Mathf.Max(0, createCoinCost);
        learnMaterialRequirements ??= new List<MagicMaterialRequirement>();
        createMaterialRequirements ??= new List<MagicMaterialRequirement>();
    }
#endif
}
