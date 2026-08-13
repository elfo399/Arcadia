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

    [SerializeField, Tooltip("Catalogo condiviso delle recipe: risolve le magie PREPARED anche nel GameScene.")]
    private List<MagicRecipeData> magicRecipes = new();
    public IReadOnlyList<MagicRecipeData> MagicRecipes => magicRecipes;

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

    public static string GetDefinitionId(ScriptableObject definition)
    {
        string id = definition switch
        {
            WeaponItem weapon => weapon.definitionId,
            MagicItemData magic => magic.definitionId,
            ArmorItemData armor => armor.definitionId,
            UsableItemData usable => usable.definitionId,
            ItemData item => item.definitionId,
            _ => null
        };
        return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
    }

    public bool TryGetWeapon(string definitionId, out WeaponItem result)
        => TryResolve(BuildFlatWeaponList(), definitionId, null, out result, "weapon");

    public bool TryGetMagic(string definitionId, out MagicItemData result)
        => TryResolve(magics, definitionId, null, out result, "magic");

    public bool TryGetArmor(string definitionId, out ArmorItemData result)
        => TryResolve(armors, definitionId, null, out result, "armor");

    public bool TryGetUsable(string definitionId, out UsableItemData result)
        => TryResolve(usables, definitionId, null, out result, "usable");

    public bool TryGetItem(string definitionId, out ItemData result)
        => TryResolve(items, definitionId, null, out result, "item");

    public bool TryResolveWeapon(string definitionId, string legacyAssetName, out WeaponItem result)
        => TryResolve(BuildFlatWeaponList(), definitionId, legacyAssetName, out result, "weapon");

    public bool TryResolveMagic(string definitionId, string legacyAssetName, out MagicItemData result)
        => TryResolve(magics, definitionId, legacyAssetName, out result, "magic");

    public bool TryResolveArmor(string definitionId, string legacyAssetName, out ArmorItemData result)
        => TryResolve(armors, definitionId, legacyAssetName, out result, "armor");

    public bool TryResolveUsable(string definitionId, string legacyAssetName, out UsableItemData result)
        => TryResolve(usables, definitionId, legacyAssetName, out result, "usable");

    public bool TryResolveItem(string definitionId, string legacyAssetName, out ItemData result)
        => TryResolve(items, definitionId, legacyAssetName, out result, "item");

    private bool TryResolve<T>(IReadOnlyList<T> source, string definitionId, string legacyAssetName, out T result, string label)
        where T : ScriptableObject
    {
        result = null;
        string normalizedId = string.IsNullOrWhiteSpace(definitionId) ? string.Empty : definitionId.Trim();
        if (normalizedId.Length > 0 && source != null)
        {
            for (int i = 0; i < source.Count; i++)
            {
                T candidate = source[i];
                if (candidate == null || !string.Equals(
                        GetDefinitionId(candidate), normalizedId, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                if (result == null)
                    result = candidate;
                else
                    Debug.LogError($"[ItemDatabase] Duplicate {label} definitionId '{normalizedId}'.", this);
            }

            if (result != null)
                return true;
        }

        string normalizedAssetName = string.IsNullOrWhiteSpace(legacyAssetName) ? string.Empty : legacyAssetName.Trim();
        if (normalizedAssetName.Length == 0 || source == null)
            return false;

        for (int i = 0; i < source.Count; i++)
        {
            T candidate = source[i];
            if (candidate == null || !string.Equals(
                    candidate.name, normalizedAssetName, System.StringComparison.OrdinalIgnoreCase))
                continue;

            if (result == null)
                result = candidate;
            else
                Debug.LogWarning($"[ItemDatabase] Legacy {label} assetName ambiguous: '{normalizedAssetName}'.", this);
        }

        return result != null;
    }

    public bool TryGetMagicRecipe(string recipeId, out MagicRecipeData recipe)
    {
        recipe = null;
        if (string.IsNullOrWhiteSpace(recipeId) || magicRecipes == null)
            return false;

        string normalized = recipeId.Trim();
        int matchingEntries = 0;
        for (int i = 0; i < magicRecipes.Count; i++)
        {
            MagicRecipeData candidate = magicRecipes[i];
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.recipeId)
                || !string.Equals(candidate.recipeId.Trim(), normalized, System.StringComparison.OrdinalIgnoreCase))
                continue;

            matchingEntries++;
            if (matchingEntries > 1)
                Debug.LogWarning($"[ItemDatabase] Magic recipe ID duplicato: '{normalized}'. Uso la prima entry valida.", this);
            if (candidate.resultMagic == null)
            {
                Debug.LogWarning($"[ItemDatabase] Magic recipe '{normalized}' ignorata: resultMagic mancante.", this);
                continue;
            }

            if (recipe != null)
                continue;
            recipe = candidate;
        }

        if (recipe == null && matchingEntries > 0)
            Debug.LogWarning($"[ItemDatabase] Magic recipe '{normalized}' configurata ma non valida.", this);
        return recipe != null;
    }
}
