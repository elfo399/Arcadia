using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "RogueLike/Treasure Chest Loot Table")]
public class TreasureChestLootTable : ScriptableObject
{
    public enum RewardType
    {
        Item,
        Usable,
        Magic,
        Armor,
        Weapon
    }

    [Serializable]
    public class LootEntry
    {
        [Range(0f, 100f)] public float dropChance = 100f;
        public ItemData item;
        public UsableItemData usable;
        public MagicItemData magic;
        public ArmorItemData armor;
        public WeaponItem weapon;

        public UnityEngine.Object GetAssignedAsset()
        {
            if (weapon != null) return weapon;
            if (armor != null) return armor;
            if (magic != null) return magic;
            if (usable != null) return usable;
            if (item != null) return item;
            return null;
        }

        public RewardType GetRewardType()
        {
            if (weapon != null) return RewardType.Weapon;
            if (armor != null) return RewardType.Armor;
            if (magic != null) return RewardType.Magic;
            if (usable != null) return RewardType.Usable;
            return RewardType.Item;
        }
    }

    [Serializable]
    public struct LootResult
    {
        public RewardType rewardType;
        public int amount;
        public ItemData item;
        public UsableItemData usable;
        public MagicItemData magic;
        public ArmorItemData armor;
        public WeaponItem weapon;
        public string label;
    }

    [SerializeField] private List<LootEntry> entries = new();

    public IReadOnlyList<LootEntry> Entries => entries;

    public List<LootResult> RollLoot()
    {
        List<LootResult> results = new();
        LootEntry selected = ChooseWeightedEntry(GetValidEntries());
        if (selected != null)
            results.Add(CreateResult(selected));
        return results;
    }

    private List<LootEntry> GetValidEntries()
    {
        List<LootEntry> valid = new();
        for (int i = 0; i < entries.Count; i++)
        {
            LootEntry entry = entries[i];
            if (entry == null || entry.GetAssignedAsset() == null || entry.dropChance <= 0f)
                continue;
            valid.Add(entry);
        }

        return valid;
    }

    private LootEntry ChooseWeightedEntry(List<LootEntry> source)
    {
        if (source == null || source.Count == 0)
            return null;

        float totalWeight = 0f;
        for (int i = 0; i < source.Count; i++)
            totalWeight += source[i].dropChance;

        if (totalWeight <= 0f)
            return null;

        float roll = UnityEngine.Random.value * totalWeight;
        float accumulated = 0f;
        for (int i = 0; i < source.Count; i++)
        {
            LootEntry entry = source[i];
            accumulated += entry.dropChance;
            if (roll <= accumulated)
                return entry;
        }

        return source[source.Count - 1];
    }

    private LootResult CreateResult(LootEntry entry)
    {
        return new LootResult
        {
            rewardType = entry.GetRewardType(),
            amount = 1,
            label = ResolveDefaultLabel(entry),
            item = entry.item,
            usable = entry.usable,
            magic = entry.magic,
            armor = entry.armor,
            weapon = entry.weapon
        };
    }

    private static string ResolveDefaultLabel(LootEntry entry)
    {
        if (entry == null)
            return string.Empty;

        switch (entry.GetRewardType())
        {
            case RewardType.Item: return entry.item != null ? entry.item.itemName : string.Empty;
            case RewardType.Usable: return entry.usable != null ? entry.usable.itemName : string.Empty;
            case RewardType.Magic: return entry.magic != null ? entry.magic.magicName : string.Empty;
            case RewardType.Armor: return entry.armor != null ? entry.armor.itemName : string.Empty;
            case RewardType.Weapon: return entry.weapon != null ? entry.weapon.weaponName : string.Empty;
            default: return string.Empty;
        }
    }
}
