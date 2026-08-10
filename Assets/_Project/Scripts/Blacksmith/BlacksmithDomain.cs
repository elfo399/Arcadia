using System;
using System.Collections.Generic;
using UnityEngine;

public enum BlacksmithMode
{
    Upgrade,
    Craft
}

public enum WeaponRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

public enum RecipeUnlockType
{
    Default,
    Blueprint,
    Story,
    BlueprintAndStory
}

[Serializable]
public class UpgradeMaterialRequirement
{
    public ItemData item;
    [Min(0)] public int amount = 1;
}

[Serializable]
public class UpgradeCostStage
{
    [Min(1)] public int minimumTargetLevel = 1;
    [Min(0)] public int coinCost;
    public List<UpgradeMaterialRequirement> materialRequirements = new List<UpgradeMaterialRequirement>();
}

public static class WeaponUpgradeRules
{
    public static int GetMaxLevel(WeaponRarity rarity)
    {
        switch (rarity)
        {
            case WeaponRarity.Uncommon: return 8;
            case WeaponRarity.Rare: return 7;
            case WeaponRarity.Epic: return 6;
            case WeaponRarity.Legendary: return 5;
            default: return 10;
        }
    }

    public static int ClampLevel(WeaponItem weapon, int level)
    {
        return Mathf.Clamp(level, 0, weapon == null ? 0 : GetMaxLevel(weapon.rarity));
    }
}

public struct EffectiveWeaponStats
{
    public int PhysicalDamage { get; private set; }
    public int MagicDamage { get; private set; }
    public float CriticalHit { get; private set; }
    public float CriticalChance { get; private set; }
    public WeaponItem.ScalingRank StrengthScalingRank { get; private set; }
    public WeaponItem.ScalingRank DexterityScalingRank { get; private set; }
    public WeaponItem.ScalingRank IntelligenceScalingRank { get; private set; }
    public WeaponItem.ScalingRank FaithScalingRank { get; private set; }
    public float PhysicalBlockPercent { get; private set; }
    public float MagicBlockPercent { get; private set; }
    public float Stability { get; private set; }
    public string DisplayName { get; private set; }
    public int EffectiveValue { get; private set; }

    public float StrengthScalingFactor => WeaponUpgradeCalculator.GetScalingFactor(StrengthScalingRank);
    public float DexterityScalingFactor => WeaponUpgradeCalculator.GetScalingFactor(DexterityScalingRank);
    public float IntelligenceScalingFactor => WeaponUpgradeCalculator.GetScalingFactor(IntelligenceScalingRank);
    public float FaithScalingFactor => WeaponUpgradeCalculator.GetScalingFactor(FaithScalingRank);

    public EffectiveWeaponStats(WeaponItem weapon, int upgradeLevel)
    {
        int level = WeaponUpgradeRules.ClampLevel(weapon, upgradeLevel);
        float damageMultiplier = 1f + Mathf.Max(0f, weapon != null ? weapon.physicalDamageGrowth : 0f) * level;
        float magicMultiplier = 1f + Mathf.Max(0f, weapon != null ? weapon.magicDamageGrowth : 0f) * level;

        PhysicalDamage = weapon == null ? 0 : Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, weapon.physicalDamage) * damageMultiplier));
        MagicDamage = weapon == null ? 0 : Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, weapon.magicDamage) * magicMultiplier));
        CriticalHit = weapon == null ? 1f : Mathf.Max(1f, weapon.criticalHit + Mathf.Max(0f, weapon.criticalHitGrowth) * level);
        CriticalChance = weapon == null ? 0f : Mathf.Clamp01(weapon.criticalChance + Mathf.Max(0f, weapon.criticalChanceGrowth) * level);
        StrengthScalingRank = weapon == null ? WeaponItem.ScalingRank.None : IncreaseRank(weapon.strengthScalingRank, weapon.strengthScalingRankGrowth * level);
        DexterityScalingRank = weapon == null ? WeaponItem.ScalingRank.None : IncreaseRank(weapon.dexterityScalingRank, weapon.dexterityScalingRankGrowth * level);
        IntelligenceScalingRank = weapon == null ? WeaponItem.ScalingRank.None : IncreaseRank(weapon.intelligenceScalingRank, weapon.intelligenceScalingRankGrowth * level);
        FaithScalingRank = weapon == null ? WeaponItem.ScalingRank.None : IncreaseRank(weapon.faithScalingRank, weapon.faithScalingRankGrowth * level);
        PhysicalBlockPercent = weapon == null ? 0f : Mathf.Clamp01(weapon.physicalBlockPercent + Mathf.Max(0f, weapon.physicalBlockGrowth) * level);
        MagicBlockPercent = weapon == null ? 0f : Mathf.Clamp01(weapon.magicBlockPercent + Mathf.Max(0f, weapon.magicBlockGrowth) * level);
        Stability = weapon == null ? 0f : Mathf.Max(0f, weapon.stability + Mathf.Max(0f, weapon.stabilityGrowth) * level);
        DisplayName = WeaponUpgradeCalculator.GetDisplayName(weapon, level);
        EffectiveValue = WeaponUpgradeCalculator.CalculateValue(weapon, level);
    }

    private static WeaponItem.ScalingRank IncreaseRank(WeaponItem.ScalingRank rank, int amount)
    {
        int value = Mathf.Clamp((int)rank + Mathf.Max(0, amount), 0, (int)WeaponItem.ScalingRank.S);
        return (WeaponItem.ScalingRank)value;
    }
}

public static class WeaponUpgradeCalculator
{
    public static EffectiveWeaponStats GetStats(InventoryItem item)
    {
        return new EffectiveWeaponStats(item != null ? item.weaponData : null, item != null ? item.upgradeLevel : 0);
    }

    public static EffectiveWeaponStats GetStats(WeaponItem weapon, int upgradeLevel = 0)
    {
        return new EffectiveWeaponStats(weapon, upgradeLevel);
    }

    public static string GetDisplayName(InventoryItem item)
    {
        return GetDisplayName(item != null ? item.weaponData : null, item != null ? item.upgradeLevel : 0);
    }

    public static string GetDisplayName(WeaponItem weapon, int upgradeLevel)
    {
        if (weapon == null) return string.Empty;
        string baseName = string.IsNullOrWhiteSpace(weapon.weaponName) ? weapon.name : weapon.weaponName;
        int level = WeaponUpgradeRules.ClampLevel(weapon, upgradeLevel);
        return level > 0 ? baseName + " +" + level : baseName;
    }

    public static int GetEffectiveValue(InventoryItem item)
    {
        return CalculateValue(item != null ? item.weaponData : null, item != null ? item.upgradeLevel : 0);
    }

    public static int CalculateValue(WeaponItem weapon, int upgradeLevel)
    {
        if (weapon == null) return 0;
        int level = WeaponUpgradeRules.ClampLevel(weapon, upgradeLevel);
        // L'investimento aumenta il valore del 5% per livello: il prezzo di
        // vendita resta inferiore a un costo di upgrade configurato normalmente.
        float multiplier = 1f + level * 0.05f;
        return Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, weapon.baseValue) * multiplier));
    }

    public static int GetUpgradeCoinCost(WeaponItem weapon, int targetLevel)
    {
        if (weapon == null) return 0;
        UpgradeCostStage stage = GetCostStage(weapon, targetLevel);
        return stage != null ? Mathf.Max(0, stage.coinCost) : Mathf.Max(0, weapon.upgradeCoinCost);
    }

    public static List<UpgradeMaterialRequirement> GetUpgradeMaterialRequirements(WeaponItem weapon, int targetLevel)
    {
        var result = new List<UpgradeMaterialRequirement>();
        if (weapon == null) return result;

        UpgradeCostStage stage = GetCostStage(weapon, targetLevel);
        IList<UpgradeMaterialRequirement> source = stage != null ? stage.materialRequirements : weapon.upgradeMaterialRequirements;
        if (source == null) return result;
        for (int i = 0; i < source.Count; i++)
        {
            UpgradeMaterialRequirement requirement = source[i];
            if (requirement == null || requirement.item == null || requirement.amount <= 0) continue;
            result.Add(new UpgradeMaterialRequirement { item = requirement.item, amount = requirement.amount });
        }
        return result;
    }

    private static UpgradeCostStage GetCostStage(WeaponItem weapon, int targetLevel)
    {
        UpgradeCostStage selected = null;
        if (weapon == null || weapon.upgradeCostStages == null) return null;
        for (int i = 0; i < weapon.upgradeCostStages.Count; i++)
        {
            UpgradeCostStage candidate = weapon.upgradeCostStages[i];
            if (candidate == null || candidate.minimumTargetLevel > targetLevel) continue;
            if (selected == null || candidate.minimumTargetLevel > selected.minimumTargetLevel)
                selected = candidate;
        }
        return selected;
    }

    public static float GetScalingFactor(WeaponItem.ScalingRank rank)
    {
        switch (rank)
        {
            case WeaponItem.ScalingRank.S: return 0.75f;
            case WeaponItem.ScalingRank.A: return 0.50f;
            case WeaponItem.ScalingRank.B: return 0.375f;
            case WeaponItem.ScalingRank.C: return 0.25f;
            case WeaponItem.ScalingRank.D: return 0.125f;
            case WeaponItem.ScalingRank.E: return 0.06f;
            default: return 0f;
        }
    }
}

[Serializable]
public class BlacksmithRequirementStatus
{
    public ItemData item;
    public int required;
    public int owned;
    public int missing;
    public bool met;
}

public class BlacksmithUpgradeCheck
{
    public bool IsValid;
    public bool IsMaxLevel;
    public string FailureReason;
    public int CurrentLevel;
    public int TargetLevel;
    public int MaxLevel;
    public int CoinCost;
    public List<BlacksmithRequirementStatus> Materials = new List<BlacksmithRequirementStatus>();
}

public class BlacksmithCraftCheck
{
    public bool IsValid;
    public string FailureReason;
    public int CoinCost;
    public List<BlacksmithRequirementStatus> Materials = new List<BlacksmithRequirementStatus>();
}
