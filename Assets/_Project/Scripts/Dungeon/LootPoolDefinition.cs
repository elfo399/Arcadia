using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LootPool", menuName = "Dungeon/Loot Pool")]
public sealed class LootPoolDefinition : ScriptableObject
{
    [Serializable] public sealed class Entry
    {
        [Tooltip("Inventory definition only: Weapon, Magic, Armor, Usable, or generic Item.")]
        public ScriptableObject item;
        [Min(1)] public int weight = 1;
        [Min(1)] public int amount = 1;
        public bool IsValidInventoryDefinition => item is WeaponItem || item is MagicItemData || item is ArmorItemData || item is UsableItemData || item is ItemData;
    }
    [SerializeField] private List<Entry> entries = new List<Entry>();
    public Entry Pick(System.Random random)
    {
        int total=0; foreach(var entry in entries) if(entry != null && entry.IsValidInventoryDefinition) total += Mathf.Max(1,entry.weight);
        if(total==0) return null; int roll=random.Next(total);
        foreach(var entry in entries) if(entry != null && entry.IsValidInventoryDefinition) { roll-=Mathf.Max(1,entry.weight); if(roll<0)return entry; } return null;
    }
    private void OnValidate() { foreach(var entry in entries) if(entry != null && !entry.IsValidInventoryDefinition) Debug.LogWarning($"[LootPool] '{name}' contains an empty or unsupported inventory definition.",this); }
}
