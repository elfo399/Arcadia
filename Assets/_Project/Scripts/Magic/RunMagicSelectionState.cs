using System;

/// <summary>
/// Run loadout selection for learned magic knowledge. It is separate from
/// physical magic loot and from the three combat equip slots.
/// </summary>
[Serializable]
public sealed class RunMagicSelectionState
{
    private string[] selectedRecipeIds;
    public int Capacity { get; private set; }

    public RunMagicSelectionState(int capacity)
    {
        Capacity = Math.Max(1, capacity);
        selectedRecipeIds = new string[Capacity];
    }

    public string[] Export()
    {
        string[] result = new string[selectedRecipeIds.Length];
        Array.Copy(selectedRecipeIds, result, selectedRecipeIds.Length);
        return result;
    }

    public bool SetAtSlot(int slot, string recipeId, Func<string, bool> learnedResolver)
    {
        if (slot < 0 || slot >= selectedRecipeIds.Length || string.IsNullOrWhiteSpace(recipeId)
            || learnedResolver == null || !learnedResolver(recipeId))
            return false;

        string normalized = recipeId.Trim();
        for (int i = 0; i < selectedRecipeIds.Length; i++)
            if (i != slot && string.Equals(selectedRecipeIds[i], normalized, StringComparison.OrdinalIgnoreCase))
                return false;

        selectedRecipeIds[slot] = normalized;
        return true;
    }

    public bool RemoveAtSlot(int slot)
    {
        if (slot < 0 || slot >= selectedRecipeIds.Length || string.IsNullOrWhiteSpace(selectedRecipeIds[slot]))
            return false;
        selectedRecipeIds[slot] = null;
        return true;
    }

    public void Clear()
    {
        Array.Clear(selectedRecipeIds, 0, selectedRecipeIds.Length);
    }
}
