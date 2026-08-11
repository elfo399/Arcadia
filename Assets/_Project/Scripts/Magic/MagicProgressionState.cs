using System;
using System.Collections.Generic;

[Serializable]
public sealed class MagicProgressionState
{
    [NonSerialized] private HashSet<string> unlockedRecipes;
    [NonSerialized] private HashSet<string> learnedRecipes;

    public string[] unlockedRecipeIds = Array.Empty<string>();
    public string[] learnedRecipeIds = Array.Empty<string>();

    private HashSet<string> Unlocked => unlockedRecipes ??= CreateSet(unlockedRecipeIds);
    private HashSet<string> Learned => learnedRecipes ??= CreateSet(learnedRecipeIds);

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

    public bool KnowsRecipe(string recipeId)
    {
        string normalized = Normalize(recipeId);
        return normalized.Length > 0 && Learned.Contains(normalized);
    }

    public void Import(IEnumerable<string> unlocked, IEnumerable<string> learned)
    {
        unlockedRecipes = CreateSet(unlocked);
        learnedRecipes = CreateSet(learned);
        unlockedRecipeIds = ExportUnlocked();
        learnedRecipeIds = ExportLearned();
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
