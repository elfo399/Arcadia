using System;
using System.Collections.Generic;

[Serializable]
public sealed class BlacksmithProgressionState
{
    [NonSerialized] private HashSet<string> knownRecipes;
    public string[] knownRecipeIds = Array.Empty<string>();

    private HashSet<string> Recipes
    {
        get
        {
            if (knownRecipes == null)
                Import(knownRecipeIds);
            return knownRecipes;
        }
    }

    public bool LearnRecipe(string recipeId)
    {
        string normalized = Normalize(recipeId);
        if (normalized.Length == 0 || !Recipes.Add(normalized)) return false;
        knownRecipeIds = Export();
        return true;
    }

    public bool KnowsRecipe(string recipeId)
    {
        string normalized = Normalize(recipeId);
        return normalized.Length > 0 && Recipes.Contains(normalized);
    }

    public void Import(IEnumerable<string> ids)
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
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
