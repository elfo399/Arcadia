using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "RogueLike/Item Database")]
public class ItemDatabase : ScriptableObject
{
    [System.Serializable]
    public class WeaponCategoryBucket
    {
        public WeaponCategory category;
        public List<WeaponItem> weapons = new();
    }

    [Header("Weapons")]
    [Tooltip("Armi divise per categoria.")]
    public List<WeaponCategoryBucket> weaponsByCategory = new();

    [Header("Magics")]
    public List<MagicItemData> magics = new();

    [Tooltip("Catalogo condiviso delle recipe: risolve le magie PREPARED anche nel GameScene.")]
    public List<MagicRecipeData> magicRecipes = new();

    [Header("Armors")]
    public List<ArmorItemData> armors = new();

    [Header("Usables")]
    public List<UsableItemData> usables = new();

    [Header("Items")]
    public List<ItemData> items = new();

    [Header("Registries")]
    public List<ItemRegistry> registries = new();

    public List<WeaponItem> BuildFlatWeaponList()
    {
        var result = new List<WeaponItem>();

        if (weaponsByCategory != null)
        {
            for (int i = 0; i < weaponsByCategory.Count; i++)
            {
                var bucket = weaponsByCategory[i];
                if (bucket == null || bucket.weapons == null) continue;

                for (int j = 0; j < bucket.weapons.Count; j++)
                {
                    var w = bucket.weapons[j];
                    if (w != null && !result.Contains(w))
                        result.Add(w);
                }
            }
        }

        return result;
    }

    public bool TryGetMagicRecipe(string recipeId, out MagicRecipeData recipe)
    {
        recipe = null;
        if (string.IsNullOrWhiteSpace(recipeId) || magicRecipes == null)
            return false;

        string normalized = recipeId.Trim();
        for (int i = 0; i < magicRecipes.Count; i++)
        {
            MagicRecipeData candidate = magicRecipes[i];
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.recipeId)
                || !string.Equals(candidate.recipeId.Trim(), normalized, System.StringComparison.OrdinalIgnoreCase))
                continue;

            if (recipe != null)
            {
                Debug.LogWarning($"[ItemDatabase] Magic recipe ID duplicato: '{normalized}'. Uso la prima entry.", this);
                continue;
            }
            recipe = candidate;
        }

        return recipe != null;
    }
}
