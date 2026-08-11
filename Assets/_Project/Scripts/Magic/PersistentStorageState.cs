using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PersistentStorageState
{
    private readonly List<InventoryItem> items = new List<InventoryItem>();
    private readonly List<InventoryItem> magicItems = new List<InventoryItem>();

    public IReadOnlyList<InventoryItem> Items => items;
    public IReadOnlyList<InventoryItem> MagicItems => magicItems;

    public bool CanAdd(InventoryItem item)
    {
        return item != null && item.amount > 0 && !string.IsNullOrWhiteSpace(item.instanceId)
               && !ContainsInstance(item.instanceId);
    }

    public bool TryAddItem(InventoryItem item)
    {
        if (item == null || item.magicData != null || !CanAdd(item)) return false;
        items.Add(item);
        return true;
    }

    public bool TryAddMagic(InventoryItem magic)
    {
        if (magic == null || magic.magicData == null || !CanAdd(magic)) return false;
        magicItems.Add(magic);
        return true;
    }

    public bool TryRemoveItem(string instanceId, out InventoryItem item)
    {
        return TryRemove(items, instanceId, out item);
    }

    public bool TryRemoveMagic(string instanceId, out InventoryItem magic)
    {
        return TryRemove(magicItems, instanceId, out magic);
    }

    public bool ContainsInstance(string instanceId)
    {
        return Find(instanceId) != null;
    }

    public InventoryItem Find(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return null;
        for (int i = 0; i < items.Count; i++)
            if (items[i] != null && string.Equals(items[i].instanceId, instanceId, StringComparison.Ordinal)) return items[i];
        for (int i = 0; i < magicItems.Count; i++)
            if (magicItems[i] != null && string.Equals(magicItems[i].instanceId, instanceId, StringComparison.Ordinal)) return magicItems[i];
        return null;
    }

    public SavedStorageData Export(Func<InventoryItem, SavedInventoryItemData> serializer)
    {
        var result = new SavedStorageData
        {
            items = ExportList(items, serializer),
            magicItems = ExportList(magicItems, serializer)
        };
        return result;
    }

    public void Import(
        SavedStorageData saved,
        Func<SavedInventoryItemData, InventoryItem> resolver)
    {
        items.Clear();
        magicItems.Clear();
        if (saved == null || resolver == null) return;

        ImportList(saved.items, false, resolver);
        ImportList(saved.magicItems, true, resolver);
    }

    public void Clear()
    {
        items.Clear();
        magicItems.Clear();
    }

    private void ImportList(SavedInventoryItemData[] source, bool magic, Func<SavedInventoryItemData, InventoryItem> resolver)
    {
        if (source == null) return;
        for (int i = 0; i < source.Length; i++)
        {
            SavedInventoryItemData saved = source[i];
            InventoryItem item = resolver(saved);
            if (item == null || item.amount <= 0 || string.IsNullOrWhiteSpace(item.instanceId))
            {
                if (saved != null)
                    Debug.LogWarning($"[PersistentStorageState] Entry storage ignorata: asset '{saved.assetName}' non risolto.");
                continue;
            }

            bool added = magic ? item.magicData != null && TryAddMagic(item) : item.magicData == null && TryAddItem(item);
            if (!added)
                Debug.LogWarning($"[PersistentStorageState] Entry storage duplicata o non valida: '{saved.instanceId}'.");
        }
    }

    private static bool TryRemove(List<InventoryItem> source, string instanceId, out InventoryItem item)
    {
        item = null;
        if (string.IsNullOrWhiteSpace(instanceId)) return false;
        for (int i = 0; i < source.Count; i++)
        {
            InventoryItem candidate = source[i];
            if (candidate == null || !string.Equals(candidate.instanceId, instanceId, StringComparison.Ordinal)) continue;
            item = candidate;
            source.RemoveAt(i);
            return true;
        }
        return false;
    }

    private static SavedInventoryItemData[] ExportList(List<InventoryItem> source, Func<InventoryItem, SavedInventoryItemData> serializer)
    {
        if (source == null || source.Count == 0 || serializer == null) return Array.Empty<SavedInventoryItemData>();
        var result = new List<SavedInventoryItemData>(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            SavedInventoryItemData saved = serializer(source[i]);
            if (saved != null) result.Add(saved);
        }
        return result.ToArray();
    }
}
