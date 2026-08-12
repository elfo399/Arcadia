using System;
using System.Collections.Generic;

/// <summary>
/// Permanent, aggregate storage for crafting materials only. It deliberately
/// has no instance identity, upgrade data, or physical item semantics.
/// </summary>
[Serializable]
public sealed class MaterialStorageState
{
    [NonSerialized] private Dictionary<ItemData, int> quantities;

    private Dictionary<ItemData, int> Quantities => quantities ??= new Dictionary<ItemData, int>();

    public int GetAmount(ItemData item)
    {
        return item != null && item.category == ItemCategory.Material && Quantities.TryGetValue(item, out int amount)
            ? amount
            : 0;
    }

    public bool CanAdd(ItemData item, int amount)
    {
        if (item == null || item.category != ItemCategory.Material || amount <= 0)
            return false;

        long updated = (long)GetAmount(item) + amount;
        return updated <= int.MaxValue;
    }

    public bool TryAdd(ItemData item, int amount)
    {
        if (!CanAdd(item, amount))
            return false;

        Quantities[item] = GetAmount(item) + amount;
        return true;
    }

    public bool TryRemove(ItemData item, int amount)
    {
        if (item == null || item.category != ItemCategory.Material || amount <= 0)
            return false;

        int current = GetAmount(item);
        if (current < amount)
            return false;

        int remaining = current - amount;
        if (remaining == 0)
            Quantities.Remove(item);
        else
            Quantities[item] = remaining;
        return true;
    }

    public SavedMaterialStorageData Export(Func<ItemData, string> assetNameResolver)
    {
        var result = new List<SavedMaterialStackData>();
        foreach (KeyValuePair<ItemData, int> entry in Quantities)
        {
            if (entry.Key == null || entry.Key.category != ItemCategory.Material || entry.Value <= 0)
                continue;

            string assetName = assetNameResolver != null ? assetNameResolver(entry.Key) : entry.Key.name;
            if (string.IsNullOrWhiteSpace(assetName))
                continue;

            result.Add(new SavedMaterialStackData { assetName = assetName, amount = entry.Value });
        }

        result.Sort((a, b) => string.Compare(a.assetName, b.assetName, StringComparison.OrdinalIgnoreCase));
        return new SavedMaterialStorageData { materials = result.ToArray() };
    }

    public void Import(SavedMaterialStorageData saved, Func<string, ItemData> resolver)
    {
        Quantities.Clear();
        if (saved == null || saved.materials == null || resolver == null)
            return;

        for (int i = 0; i < saved.materials.Length; i++)
        {
            SavedMaterialStackData entry = saved.materials[i];
            if (entry == null || entry.amount <= 0)
                continue;

            ItemData item = resolver(entry.assetName);
            if (item == null || item.category != ItemCategory.Material || !CanAdd(item, entry.amount))
                continue;

            TryAdd(item, entry.amount);
        }
    }

    public void Clear()
    {
        Quantities.Clear();
    }
}
