using UnityEngine;
using System.Text;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "RogueLike/Weapon")]
public class WeaponItem : ScriptableObject
{
    [Header("Persistence")]
    [Tooltip("Stable save identifier. Do not change after this definition ships.")]
    public string definitionId;

    public enum ScalingRank
    {
        None,
        E,
        D,
        C,
        B,
        A,
        S
    }

    public enum DamageType
    {
        Physical,
        Magic
    }

    public enum WeaponRangeType
    {
        Melee,
        Ranged
    }

    // Display name of the weapon
    public string weaponName;
    [Min(0)] public int baseValue = 1;

    [Header("Blacksmith")]
    public WeaponRarity rarity = WeaponRarity.Common;
    public bool canUpgrade = true;
    public bool canCraft = false;
    [Min(0)] public int upgradeCoinCost = 0;
    public List<UpgradeMaterialRequirement> upgradeMaterialRequirements = new List<UpgradeMaterialRequirement>();
    public List<UpgradeCostStage> upgradeCostStages = new List<UpgradeCostStage>();
    [Min(0f)] public float physicalDamageGrowth = 0.05f;
    [Min(0f)] public float magicDamageGrowth = 0.05f;
    [Min(0f)] public float criticalHitGrowth = 0f;
    [Min(0f)] public float criticalChanceGrowth = 0f;
    [Min(0)] public int strengthScalingRankGrowth = 0;
    [Min(0)] public int dexterityScalingRankGrowth = 0;
    [Min(0)] public int intelligenceScalingRankGrowth = 0;
    [Min(0)] public int faithScalingRankGrowth = 0;
    [Min(0f)] public float physicalBlockGrowth = 0f;
    [Min(0f)] public float magicBlockGrowth = 0f;
    [Min(0f)] public float stabilityGrowth = 0f;

    [Header("Visual")]
    // Icon used in UI slots
    public Sprite icon;
    // Prefab for the weapon model
    public GameObject modelPrefab;

    [TextArea]
    [Header("Description")]
    // Descrizione testuale per il pannello dettagli
    public string description;

    [Header("Category")]
    // Weapon category classification
    public WeaponCategory category;
    public WeaponRangeType rangeType = WeaponRangeType.Melee;

    [Header("Danni")]
    public DamageType damageType = DamageType.Physical;
    // Physical damage dealt by the weapon
    public int physicalDamage = 10;
    // Magic base damage (used when damageType == Magic)
    public int magicDamage = 0;
    // Moltiplicatore o chance di colpo critico (interpretazione libera)
    public float criticalHit = 1.1f;
    [Range(0f, 1f)] public float criticalChance = 0f;
    [Min(0.1f)] public float lightDamageMultiplier = 1f;
    [Min(0.1f)] public float heavyDamageMultiplier = 1.25f;
    // Peso usato per il bilanciamento (UI / equip load)
    public float weight = 3f;

    [Header("Scaling")]
    // Nota di scaling (es. STR C / DEX B). Stringa libera per l'UI.
    public string scaling = "STR C / DEX D";
    [Header("Scaling Ranks")]
    public ScalingRank strengthScalingRank = ScalingRank.None;
    public ScalingRank dexterityScalingRank = ScalingRank.None;
    public ScalingRank intelligenceScalingRank = ScalingRank.None;
    public ScalingRank faithScalingRank = ScalingRank.None;

    [Header("Requisiti")]
    [Min(0)] public int strengthRequirement = 0;
    [Min(0)] public int dexterityRequirement = 0;
    [Min(0)] public int intelligenceRequirement = 0;
    [Min(0)] public int faithRequirement = 0;
    // Legacy/fallback: se i campi strutturati sono 0 usa questa stringa.
    public string requirements = "STR 10 / DEX 8";

    [Header("Animation Profile")]
    // Animation mappings used for attacks
    public WeaponAnimationProfile animationProfile;

    [Header("Stamina Cost")]
    // Stamina cost for light attacks
    public float lightAttackStaminaCost = 10f;
    // Stamina cost for heavy attacks
    public float heavyAttackStaminaCost = 20f;

    [Header("Shield (Block / Parry)")]
    public bool canBlock = false;
    public bool canParry = false;
    [Range(0f, 1f)] public float physicalBlockPercent = 0.75f;
    [Range(0f, 1f)] public float magicBlockPercent = 0.40f;
    [Min(0f)] public float stability = 25f;
    [Min(0f)] public float parryWindowStart = 0.05f;
    [Min(0f)] public float parryWindowDuration = 0.20f;

    [Header("Abilita (per il futuro)")]
    // Whether the weapon has a right-hand skill
    public bool hasRightSkill;
    // Whether the weapon has a left-hand skill
    public bool hasLeftSkill;

    [Header("Special Weapon")]
    // Marks the weapon as special or unique
    public bool isSpecialWeapon;

    [Header("Wand (Magic Casting)")]
    // Projectile used by light attack when category == Wand.
    public GameObject wandLightProjectilePrefab;
    [Min(0f)] public float wandLightManaCost = 6f;
    [Min(0f)] public float wandLightCooldown = 0.20f;
    [Min(0.1f)] public float wandLightProjectileSpeed = 22f;
    [Min(0.1f)] public float wandLightProjectileLifetime = 2.5f;
    public Vector3 wandLightSpawnOffset = new Vector3(0f, 1.2f, 0.75f);
    public LayerMask wandHitMask = ~0;

    [Header("Bow (Arrow Shooting)")]
    // Projectile used when category == Bow.
    public GameObject bowProjectilePrefab;
    // Ammo item required to shoot (e.g., Arrow).
    public ItemData bowAmmoItem;
    [Min(0f)] public float bowShotCooldown = 0.20f;
    [Min(0.1f)] public float bowProjectileSpeed = 26f;
    [Min(0.1f)] public float bowProjectileLifetime = 3.5f;
    public Vector3 bowSpawnOffset = new Vector3(0f, 1.2f, 0.9f);
    public LayerMask bowHitMask = ~0;

    [Header("Throw")]
    public bool canBeThrown = false;
    [Min(0)] public int throwStrengthRequirement = 0;
    public GameObject throwProjectilePrefab;
    [Min(0.1f)] public float throwSpeed = 20f;
    [Min(0.1f)] public float throwLifetime = 2.5f;
    [Min(0f)] public float throwStaminaCost = 18f;
    [Range(0f, 1f)] public float throwBladeHitChance = 0.65f;
    [Range(0.1f, 1f)] public float throwHandleDamageMultiplier = 0.5f;
    [Range(0f, 1f)] public float throwBreakChance = 0.10f;

    [Header("Dropped Pickup Physics")]
    public Vector3 droppedPickupColliderCenter = new Vector3(0f, 0.05f, 0f);
    public Vector3 droppedPickupColliderSize = new Vector3(0.7f, 0.12f, 0.22f);
    [Min(0.01f)] public float droppedPickupMass = 2.2f;
    [Min(0f)] public float droppedPickupLinearDrag = 0.06f;
    [Min(0f)] public float droppedPickupAngularDrag = 0.45f;
    [Min(0f)] public float droppedForwardImpulse = 0.9f;
    [Min(0f)] public float droppedUpImpulse = 0.18f;
    public Vector3 droppedInitialTorque = new Vector3(2.5f, 0.3f, 1.2f);
    public Vector3 droppedModelLocalEuler = new Vector3(90f, 0f, 0f);

    public string GetRequirementsLabel()
    {
        bool hasStructured = strengthRequirement > 0 || dexterityRequirement > 0 || intelligenceRequirement > 0 || faithRequirement > 0;
        if (!hasStructured)
            return requirements ?? string.Empty;

        StringBuilder sb = new StringBuilder(32);
        AppendRequirement(sb, "STR", strengthRequirement);
        AppendRequirement(sb, "DEX", dexterityRequirement);
        AppendRequirement(sb, "INT", intelligenceRequirement);
        AppendRequirement(sb, "FAI", faithRequirement);
        return sb.ToString();
    }

    public float GetStrengthScalingFactor()
    {
        return RankToFactor(strengthScalingRank);
    }

    public float GetDexterityScalingFactor()
    {
        return RankToFactor(dexterityScalingRank);
    }

    public float GetIntelligenceScalingFactor()
    {
        return RankToFactor(intelligenceScalingRank);
    }

    public float GetFaithScalingFactor()
    {
        return RankToFactor(faithScalingRank);
    }

    public string GetScalingLabel()
    {
        StringBuilder sb = new StringBuilder(48);
        AppendScalingRank(sb, "STR", strengthScalingRank);
        AppendScalingRank(sb, "DEX", dexterityScalingRank);
        AppendScalingRank(sb, "INT", intelligenceScalingRank);
        AppendScalingRank(sb, "FAI", faithScalingRank);
        return sb.Length > 0 ? sb.ToString() : (scaling ?? string.Empty);
    }

    private static float RankToFactor(ScalingRank rank)
    {
        switch (rank)
        {
            case ScalingRank.S: return 0.75f;
            case ScalingRank.A: return 0.50f;
            case ScalingRank.B: return 0.375f;
            case ScalingRank.C: return 0.25f;
            case ScalingRank.D: return 0.125f;
            case ScalingRank.E: return 0.06f;
            default: return 0f;
        }
    }

    private static void AppendScalingRank(StringBuilder sb, string label, ScalingRank rank)
    {
        string valueLabel = RankToLabel(rank);
        if (string.IsNullOrEmpty(valueLabel)) return;
        if (sb.Length > 0) sb.Append(" / ");
        sb.Append(label).Append(' ').Append(valueLabel);
    }

    private static string RankToLabel(ScalingRank rank)
    {
        switch (rank)
        {
            case ScalingRank.S: return "S";
            case ScalingRank.A: return "A";
            case ScalingRank.B: return "B";
            case ScalingRank.C: return "C";
            case ScalingRank.D: return "D";
            case ScalingRank.E: return "E";
            default: return string.Empty;
        }
    }

    private static void AppendRequirement(StringBuilder sb, string label, int value)
    {
        if (value <= 0) return;
        if (sb.Length > 0) sb.Append(" / ");
        sb.Append(label).Append(' ').Append(value);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (category == WeaponCategory.Shield)
        {
            rangeType = WeaponRangeType.Melee;
            canBlock = true;
        }

        bool hasStructured = strengthRequirement > 0 || dexterityRequirement > 0 || intelligenceRequirement > 0 || faithRequirement > 0;
        if (hasStructured)
            requirements = GetRequirementsLabel();
        scaling = GetScalingLabel();
        baseValue = Mathf.Max(0, baseValue);
        upgradeCoinCost = Mathf.Max(0, upgradeCoinCost);
        physicalDamageGrowth = Mathf.Max(0f, physicalDamageGrowth);
        magicDamageGrowth = Mathf.Max(0f, magicDamageGrowth);
        criticalHitGrowth = Mathf.Max(0f, criticalHitGrowth);
        criticalChanceGrowth = Mathf.Max(0f, criticalChanceGrowth);
        strengthScalingRankGrowth = Mathf.Max(0, strengthScalingRankGrowth);
        dexterityScalingRankGrowth = Mathf.Max(0, dexterityScalingRankGrowth);
        intelligenceScalingRankGrowth = Mathf.Max(0, intelligenceScalingRankGrowth);
        faithScalingRankGrowth = Mathf.Max(0, faithScalingRankGrowth);
        physicalBlockGrowth = Mathf.Max(0f, physicalBlockGrowth);
        magicBlockGrowth = Mathf.Max(0f, magicBlockGrowth);
        stabilityGrowth = Mathf.Max(0f, stabilityGrowth);
        if (upgradeMaterialRequirements == null)
            upgradeMaterialRequirements = new List<UpgradeMaterialRequirement>();
        if (upgradeCostStages == null)
            upgradeCostStages = new List<UpgradeCostStage>();
        for (int i = 0; i < upgradeCostStages.Count; i++)
        {
            UpgradeCostStage stage = upgradeCostStages[i];
            if (stage == null) continue;
            stage.minimumTargetLevel = Mathf.Max(1, stage.minimumTargetLevel);
            stage.coinCost = Mathf.Max(0, stage.coinCost);
        }
    }
#endif
}
