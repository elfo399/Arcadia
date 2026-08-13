using System;
using System.Collections.Generic;

[Serializable]
public sealed class MagicProgressionState
{
    [NonSerialized] private HashSet<string> unlockedRecipes;
    [NonSerialized] private HashSet<string> learnedRecipes;
    [NonSerialized] private Dictionary<string, int> blueprintFragments;

    public string[] unlockedRecipeIds = Array.Empty<string>();
    public string[] learnedRecipeIds = Array.Empty<string>();
    public SavedBlueprintFragmentData[] savedBlueprintFragments = Array.Empty<SavedBlueprintFragmentData>();

    private HashSet<string> Unlocked => unlockedRecipes ??= CreateSet(unlockedRecipeIds);
    private HashSet<string> Learned => learnedRecipes ??= CreateSet(learnedRecipeIds);
    private Dictionary<string, int> Fragments => blueprintFragments ??= CreateFragments(savedBlueprintFragments);

    public bool UnlockRecipe(string recipeId)
    {
        string normalized = Normalize(recipeId);
        if (normalized.Length == 0 || !Unlocked.Add(normalized)) return false;
        unlockedRecipeIds = ExportUnlocked();
        return true;
    }

    public bool IsRecipeUnlocked(string recipeId)
    {
        string normalized = Normalize(recipeId);
        return normalized.Length > 0 && Unlocked.Contains(normalized);
    }

    public bool LearnRecipe(string recipeId)
    {
        string normalized = Normalize(recipeId);
        if (normalized.Length == 0 || !Learned.Add(normalized)) return false;
        learnedRecipeIds = ExportLearned();
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
        if (normalized.Length == 0 || Unlocked.Contains(normalized) || Learned.Contains(normalized)
            || GetBlueprintFragments(normalized) >= required)
            return false;

        Fragments[normalized] = Math.Min(required, GetBlueprintFragments(normalized) + 1);
        if (Fragments[normalized] >= required)
            unlocked = UnlockRecipe(normalized);
        savedBlueprintFragments = ExportFragments();
        return true;
    }

    public void CompleteBlueprint(string recipeId, int requiredFragments)
    {
        string normalized = Normalize(recipeId);
        if (normalized.Length == 0) return;
        int required = Math.Max(1, requiredFragments);
        Fragments[normalized] = Math.Max(GetBlueprintFragments(normalized), required);
        savedBlueprintFragments = ExportFragments();
        UnlockRecipe(normalized);
    }

    public bool KnowsRecipe(string recipeId)
    {
        string normalized = Normalize(recipeId);
        return normalized.Length > 0 && Learned.Contains(normalized);
    }

    public void Import(IEnumerable<string> unlocked, IEnumerable<string> learned, IEnumerable<SavedBlueprintFragmentData> fragments = null)
    {
        unlockedRecipes = CreateSet(unlocked);
        learnedRecipes = CreateSet(learned);
        unlockedRecipeIds = ExportUnlocked();
        learnedRecipeIds = ExportLearned();
        blueprintFragments = CreateFragments(fragments);
        savedBlueprintFragments = ExportFragments();
    }

    public string[] ExportUnlocked()
    {
        if (unlockedRecipes == null) unlockedRecipes = CreateSet(unlockedRecipeIds);
        return Export(unlockedRecipes);
    }

    public string[] ExportLearned()
    {
        if (learnedRecipes == null) learnedRecipes = CreateSet(learnedRecipeIds);
        return Export(learnedRecipes);
    }

    public void Clear()
    {
        Unlocked.Clear();
        Learned.Clear();
        unlockedRecipeIds = Array.Empty<string>();
        learnedRecipeIds = Array.Empty<string>();
        Fragments.Clear();
        savedBlueprintFragments = Array.Empty<SavedBlueprintFragmentData>();
    }

    public SavedBlueprintFragmentData[] ExportFragments()
    {
        if (blueprintFragments == null) blueprintFragments = CreateFragments(savedBlueprintFragments);
        var result = new List<SavedBlueprintFragmentData>();
        foreach (KeyValuePair<string, int> entry in blueprintFragments)
        {
            if (entry.Value > 0)
                result.Add(new SavedBlueprintFragmentData { recipeId = entry.Key, fragments = entry.Value });
        }
        result.Sort((a, b) => string.Compare(a.recipeId, b.recipeId, StringComparison.OrdinalIgnoreCase));
        return result.ToArray();
    }

    private static Dictionary<string, int> CreateFragments(IEnumerable<SavedBlueprintFragmentData> source)
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

    private static HashSet<string> CreateSet(IEnumerable<string> ids)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (ids == null) return result;
        foreach (string id in ids)
        {
            string normalized = Normalize(id);
            if (normalized.Length > 0) result.Add(normalized);
        }
        return result;
    }

    private static string[] Export(HashSet<string> source)
    {
        string[] result = new string[source.Count];
        source.CopyTo(result);
        Array.Sort(result, StringComparer.OrdinalIgnoreCase);
        return result;
    }

    private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
