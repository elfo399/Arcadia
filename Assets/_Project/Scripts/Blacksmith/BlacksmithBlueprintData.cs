using UnityEngine;

[CreateAssetMenu(fileName = "BlacksmithBlueprint", menuName = "Arcadia/Blacksmith/Blueprint")]
public sealed class BlacksmithBlueprintData : ScriptableObject
{
    public CraftingRecipeData recipe;

    public bool Learn(PlayerStats stats)
    {
        return stats != null && recipe != null && stats.CompleteBlacksmithBlueprint(recipe.recipeId, recipe.blueprintFragmentsRequired);
    }
}
