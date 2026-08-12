using System;
using System.Collections.Generic;

[Serializable]
public sealed class BlacksmithProgressionState
{
    [NonSerialized] private HashSet<string> knownRecipes;
    [NonSerialized] private Dictionary<string, int> blueprintFragments;
    public string[] knownRecipeIds = Array.Empty<string>();
    public SavedBlueprintFragmentData[] savedBlueprintFragments = Array.Empty<SavedBlueprintFragmentData>();

    private HashSet<string> Recipes
    {
        get
        {
            if (knownRecipes == null)
                Import(knownRecipeIds);
            return knownRecipes;
        }
    }

    private Dictionary<string, int> Fragments => blueprintFragments ??= ImportFragments(savedBlueprintFragments);

    public bool LearnRecipe(string recipeId)
    {
        string normalized = Normalize(recipeId);
        if (normalized.Length == 0 || !Recipes.Add(normalized)) return false;
        knownRecipeIds = Export();
        return true;
    }

    public int GetBlueprintFragments(string recipeId)
    {
        string normalized = Normalize(recipeId);
        return normalized.Length > 0 && Fragments.TryGetValue(normalized, out int value) ? value : 0;
    }

    public bool TryAddBlueprintFragment(string recipeId, int requiredFragments, out bool unlocked)
    {
        unlocked = false;
        string normalized = Normalize(recipeId);
        int required = Math.Max(1, requiredFragments);
        if (normalized.Length == 0 || Recipes.Contains(normalized) || GetBlueprintFragments(normalized) >= required)
            return false;

        Fragments[normalized] = Math.Min(required, GetBlueprintFragments(normalized) + 1);
        if (Fragments[normalized] >= required)
        {
            unlocked = LearnRecipe(normalized);
        }
        savedBlueprintFragments = ExportFragments();
        return true;
    }

    public void CompleteBlueprint(string recipeId, int requiredFragments)
    {
        string normalized = Normalize(recipeId);
        if (normalized.Length == 0) return;
        int required = Math.Max(1, requiredFragments);
        Fragments[normalized] = required;
        savedBlueprintFragments = ExportFragments();
        LearnRecipe(normalized);
    }

    public bool KnowsRecipe(string recipeId)
    {
        string normalized = Normalize(recipeId);
        return normalized.Length > 0 && Recipes.Contains(normalized);
    }

    public void Import(IEnumerable<string> ids, IEnumerable<SavedBlueprintFragmentData> fragments = null)
    {
        knownRecipes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (ids != null)
        {
            foreach (string id in ids)
            {
                string normalized = Normalize(id);
                if (normalized.Length > 0) knownRecipes.Add(normalized);
            }
        }
        knownRecipeIds = Export();
        blueprintFragments = ImportFragments(fragments);
        savedBlueprintFragments = ExportFragments();
    }

    public string[] Export()
    {
        if (knownRecipes == null)
            knownRecipes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] result = new string[knownRecipes.Count];
        knownRecipes.CopyTo(result);
        Array.Sort(result, StringComparer.OrdinalIgnoreCase);
        return result;
    }

    public void Clear()
    {
        knownRecipes?.Clear();
        knownRecipeIds = Array.Empty<string>();
        Fragments.Clear();
        savedBlueprintFragments = Array.Empty<SavedBlueprintFragmentData>();
    }

    public SavedBlueprintFragmentData[] ExportFragments()
    {
        if (blueprintFragments == null) blueprintFragments = ImportFragments(savedBlueprintFragments);
        var result = new List<SavedBlueprintFragmentData>();
        foreach (KeyValuePair<string, int> entry in blueprintFragments)
        {
            if (entry.Value > 0)
                result.Add(new SavedBlueprintFragmentData { recipeId = entry.Key, fragments = entry.Value });
        }
        result.Sort((a, b) => string.Compare(a.recipeId, b.recipeId, StringComparison.OrdinalIgnoreCase));
        return result.ToArray();
    }

    private static Dictionary<string, int> ImportFragments(IEnumerable<SavedBlueprintFragmentData> source)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (source == null) return result;
        foreach (SavedBlueprintFragmentData entry in source)
        {
            string id = Normalize(entry != null ? entry.recipeId : null);
            if (id.Length == 0 || entry.fragments <= 0) continue;
            result[id] = Math.Max(result.TryGetValue(id, out int current) ? current : 0, entry.fragments);
        }
        return result;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
