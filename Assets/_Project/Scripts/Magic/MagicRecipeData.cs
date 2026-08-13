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
    [Min(1)] public int blueprintFragmentsRequired = 3;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(recipeId) && !string.IsNullOrWhiteSpace(name))
            recipeId = name.Trim();
        learnCoinCost = Mathf.Max(0, learnCoinCost);
        blueprintFragmentsRequired = Mathf.Max(1, blueprintFragmentsRequired);
        learnMaterialRequirements ??= new List<MagicMaterialRequirement>();
    }
#endif
}
