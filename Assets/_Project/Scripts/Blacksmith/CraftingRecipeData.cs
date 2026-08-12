using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CraftingRecipe", menuName = "Arcadia/Blacksmith/Crafting Recipe")]
public sealed class CraftingRecipeData : ScriptableObject
{
    public string recipeId;
    public WeaponItem resultWeapon;
    [Min(0)] public int startingUpgradeLevel;
    [Min(0)] public int coinCost;
    public List<UpgradeMaterialRequirement> materialRequirements = new List<UpgradeMaterialRequirement>();
    [Min(1)] public int blueprintFragmentsRequired = 3;
    public RecipeUnlockType unlockType = RecipeUnlockType.Default;
    public string storyFlagId;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(recipeId) && !string.IsNullOrWhiteSpace(name))
            recipeId = name.Trim();
        startingUpgradeLevel = Mathf.Max(0, startingUpgradeLevel);
        if (resultWeapon != null)
            startingUpgradeLevel = WeaponUpgradeRules.ClampLevel(resultWeapon, startingUpgradeLevel);
        coinCost = Mathf.Max(0, coinCost);
        blueprintFragmentsRequired = Mathf.Max(1, blueprintFragmentsRequired);
        if (materialRequirements == null)
            materialRequirements = new List<UpgradeMaterialRequirement>();
    }
#endif
}
