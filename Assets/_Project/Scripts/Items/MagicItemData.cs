using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "RogueLike/Magic Item")]
public class MagicItemData : ScriptableObject
{
    public enum MagicCategory
    {
        Attack,
        Boost,
        Healing
    }

    public enum MagicEffectType
    {
        Damage,
        HealHealth,
        RestoreMana,
        BoostAttribute
    }

    public enum BoostAttribute
    {
        None,
        Vigor,
        Mind,
        Endurance,
        Strength,
        Dexterity,
        Intelligence,
        Faith
    }

    [Header("Info")]
    public string magicName;
    [Min(0)] public int baseValue = 1;
    [TextArea] public string description;
    public Sprite icon;
    public MagicCategory category = MagicCategory.Attack;
    public MagicEffectType effectType = MagicEffectType.Damage;

    public bool IsVisualCategory(MagicCategory visualCategory)
    {
        return category == visualCategory;
    }

    public static string FormatCompact(float value)
    {
        return value.ToString("0.##");
    }

    public static string FormatSignedAmount(int amount)
    {
        return amount > 0 ? $"+{amount}" : amount.ToString();
    }

    public static string FormatBoostAttribute(BoostAttribute attribute)
    {
        switch (attribute)
        {
            case BoostAttribute.Vigor: return "Vigor";
            case BoostAttribute.Mind: return "Mind";
            case BoostAttribute.Endurance: return "Endurance";
            case BoostAttribute.Strength: return "Strength";
            case BoostAttribute.Dexterity: return "Dexterity";
            case BoostAttribute.Intelligence: return "Intelligence";
            case BoostAttribute.Faith: return "Faith";
            default: return string.Empty;
        }
    }

    public static string FormatDuration(float seconds)
    {
        return seconds > 0f ? $"{FormatCompact(seconds)}s" : string.Empty;
    }

    public static string FormatHealingType(MagicEffectType effectType)
    {
        switch (effectType)
        {
            case MagicEffectType.HealHealth: return "Health";
            case MagicEffectType.RestoreMana: return "Mana";
            default: return string.Empty;
        }
    }

    [Header("Stats")]
    public int magicDamage = 10;
    public int healAmount = 0;
    public BoostAttribute boostAttribute = BoostAttribute.None;
    public int boostAmount = 0;
    [Min(0f)] public float boostDurationSeconds = 0f;
    public float criticalHit = 1f;
    public string scaling = "INT C";
    public string requirements = "INT 10+";
    public List<MagicStatRequirement> statRequirements = new List<MagicStatRequirement>();

    public IReadOnlyList<MagicStatRequirement> StatRequirements => statRequirements;

    public bool MeetsStatRequirements(PlayerStats stats)
    {
        if (stats == null) return false;
        if (statRequirements == null) return true;
        for (int i = 0; i < statRequirements.Count; i++)
        {
            MagicStatRequirement requirement = statRequirements[i];
            if (requirement == null || GetStatValue(stats, requirement.attribute) < Mathf.Max(1, requirement.requiredValue))
                return false;
        }
        return true;
    }

    public string GetRequirementsLabel()
    {
        if (statRequirements == null || statRequirements.Count == 0)
            return string.Empty;

        var parts = new List<string>();
        for (int i = 0; i < statRequirements.Count; i++)
        {
            MagicStatRequirement requirement = statRequirements[i];
            if (requirement == null) continue;
            parts.Add(GetAbbreviation(requirement.attribute) + " " + Mathf.Max(1, requirement.requiredValue));
        }
        return string.Join(" / ", parts);
    }

    public static int GetStatValue(PlayerStats stats, MagicStatAttribute attribute)
    {
        if (stats == null) return 0;
        switch (attribute)
        {
            case MagicStatAttribute.Vigor: return stats.vigor;
            case MagicStatAttribute.Mind: return stats.mind;
            case MagicStatAttribute.Endurance: return stats.endurance;
            case MagicStatAttribute.Strength: return stats.strength;
            case MagicStatAttribute.Dexterity: return stats.dexterity;
            case MagicStatAttribute.Intelligence: return stats.intelligence;
            case MagicStatAttribute.Faith: return stats.faith;
            default: return 0;
        }
    }

    public static string GetAbbreviation(MagicStatAttribute attribute)
    {
        switch (attribute)
        {
            case MagicStatAttribute.Vigor: return "VIG";
            case MagicStatAttribute.Mind: return "MND";
            case MagicStatAttribute.Endurance: return "END";
            case MagicStatAttribute.Strength: return "STR";
            case MagicStatAttribute.Dexterity: return "DEX";
            case MagicStatAttribute.Intelligence: return "INT";
            case MagicStatAttribute.Faith: return "FAI";
            default: return string.Empty;
        }
    }

    [Header("Cast")]
    [Min(0f)] public float manaCost = 12f;
    [Min(0f)] public float castTime = 0.35f;
    [Min(0f)] public float castCooldown = 0.45f;

    [Header("Projectile")]
    public GameObject projectilePrefab;
    [Min(0.1f)] public float projectileSpeed = 18f;
    [Min(0.1f)] public float projectileLifetime = 4f;
    public Vector3 spawnOffset = new Vector3(0f, 1.2f, 0.7f);
    public LayerMask hitMask = ~0;
}
