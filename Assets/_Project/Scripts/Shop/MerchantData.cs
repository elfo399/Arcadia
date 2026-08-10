using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Arcadia/Shop/Merchant Data")]
public sealed class MerchantData : ScriptableObject
{
    [Serializable]
    public sealed class StockEntry
    {
        public string entryId;
        public ScriptableObject item;
        [Min(0)] public int quantity = 1;
        public bool infiniteStock = true;
    }

    public string merchantId = "merchant";
    public List<StockEntry> stock = new List<StockEntry>();
    [Min(0f)] public float buyMultiplier = 1f;
    [Min(0f)] public float sellMultiplier = 0.5f;
}
