using UnityEngine;

public enum MagicInventorySlotSource
{
    Empty,
    Prepared,
    Found
}

/// <summary>Runtime-authoritative entry of the shared six-slot magic inventory.</summary>
[System.Serializable]
public sealed class MagicInventorySlotState
{
    [SerializeField] private MagicInventorySlotSource source;
    [SerializeField] private string recipeId;
    [SerializeField] private string instanceId;

    public MagicInventorySlotSource Source => source;
    public string RecipeId => recipeId;
    public string InstanceId => instanceId;

    public void Clear()
    {
        source = MagicInventorySlotSource.Empty;
        recipeId = null;
        instanceId = null;
    }

    public void SetPrepared(string value)
    {
        source = MagicInventorySlotSource.Prepared;
        recipeId = value;
        instanceId = null;
    }

    public void SetFound(string value)
    {
        source = MagicInventorySlotSource.Found;
        instanceId = value;
        recipeId = null;
    }
}

/// <summary>Resolved read-only projection of a magic inventory slot for UI and combat binding.</summary>
public readonly struct MagicInventorySlotView
{
    public readonly MagicInventorySlotSource Source;
    public readonly MagicItemData Magic;
    public readonly string RecipeId;
    public readonly string InstanceId;

    public MagicInventorySlotView(MagicInventorySlotSource source, MagicItemData magic, string recipeId, string instanceId)
    {
        Source = source;
        Magic = magic;
        RecipeId = recipeId;
        InstanceId = instanceId;
    }
}
