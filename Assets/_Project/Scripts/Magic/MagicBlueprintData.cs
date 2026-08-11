using UnityEngine;

[CreateAssetMenu(fileName = "MagicBlueprint", menuName = "Arcadia/Magic/Blueprint")]
public sealed class MagicBlueprintData : ScriptableObject
{
    public MagicRecipeData recipe;

    public bool Unlock(PlayerStats stats)
    {
        return stats != null && recipe != null && stats.UnlockMagicRecipe(recipe.recipeId);
    }
}
