using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class MerchantStockState
{
    private readonly Dictionary<string, int> remaining = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public void Import(SavedMerchantStockData[] saved)
    {
        remaining.Clear();
        if (saved == null) return;
        for (int i = 0; i < saved.Length; i++)
        {
            SavedMerchantStockData entry = saved[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.entryId)) continue;
            remaining[Key(CanonicalMerchantId(entry.merchantId), entry.entryId)] = Mathf.Max(0, entry.remainingQuantity);
        }
    }

    public void RegisterMerchant(MerchantData merchant)
    {
        if (merchant == null || merchant.stock == null) return;
        string merchantId = CanonicalMerchantId(merchant.merchantId);
        for (int i = 0; i < merchant.stock.Count; i++)
        {
            MerchantData.StockEntry entry = merchant.stock[i];
            if (entry == null || entry.infiniteStock) continue;
            string entryId = StableEntryId(entry, i);
            string key = Key(merchantId, entryId);
            if (remaining.ContainsKey(key)) continue;

            // Legacy saves used entry_N. Consume that value once, then keep the
            // semantic id for every subsequent snapshot.
            string legacyKey = Key(merchantId, "entry_" + i);
            if (remaining.TryGetValue(legacyKey, out int legacyQuantity))
            {
                remaining.Remove(legacyKey);
                remaining[key] = legacyQuantity;
            }
            else remaining[key] = Mathf.Max(0, entry.quantity);
        }
    }

    public int GetRemaining(MerchantData merchant, MerchantData.StockEntry entry, int index)
    {
        RegisterMerchant(merchant);
        string key = Key(CanonicalMerchantId(merchant != null ? merchant.merchantId : string.Empty), StableEntryId(entry, index));
        return remaining.TryGetValue(key, out int value) ? Mathf.Max(0, value) : Mathf.Max(0, entry != null ? entry.quantity : 0);
    }

    public void Decrement(MerchantData merchant, MerchantData.StockEntry entry, int index, int amount = 1)
    {
        if (merchant == null || entry == null || entry.infiniteStock) return;
        RegisterMerchant(merchant);
        string key = Key(CanonicalMerchantId(merchant.merchantId), StableEntryId(entry, index));
        remaining[key] = Mathf.Max(0, GetRemaining(merchant, entry, index) - Mathf.Max(0, amount));
    }

    public SavedMerchantStockData[] Export()
    {
        List<SavedMerchantStockData> result = new List<SavedMerchantStockData>();
        foreach (KeyValuePair<string, int> pair in remaining)
        {
            int separator = pair.Key.IndexOf('|');
            if (separator <= 0 || separator >= pair.Key.Length - 1) continue;
            result.Add(new SavedMerchantStockData
            {
                merchantId = pair.Key.Substring(0, separator),
                entryId = pair.Key.Substring(separator + 1),
                remainingQuantity = Mathf.Max(0, pair.Value)
            });
        }
        return result.ToArray();
    }

    public static string CanonicalMerchantId(string merchantId)
    {
        return string.Equals(merchantId, "merchant_tony", StringComparison.OrdinalIgnoreCase) ? "merchant" : (merchantId ?? string.Empty).Trim();
    }

    public static string StableEntryId(MerchantData.StockEntry entry, int index)
    {
        return entry != null && !string.IsNullOrWhiteSpace(entry.entryId) ? entry.entryId.Trim() : "entry_" + index;
    }

    private static string Key(string merchantId, string entryId) { return (merchantId ?? string.Empty) + "|" + (entryId ?? string.Empty); }
}
