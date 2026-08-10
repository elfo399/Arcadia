using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponItem))]
public class WeaponItemEditor : Editor
{
    private SerializedProperty weaponNameProp;
    private SerializedProperty iconProp;
    private SerializedProperty modelPrefabProp;
    private SerializedProperty descriptionProp;

    private SerializedProperty categoryProp;
    private SerializedProperty rangeTypeProp;

    private SerializedProperty damageTypeProp;
    private SerializedProperty physicalDamageProp;
    private SerializedProperty magicDamageProp;
    private SerializedProperty criticalHitProp;
    private SerializedProperty criticalChanceProp;
    private SerializedProperty lightDamageMultiplierProp;
    private SerializedProperty heavyDamageMultiplierProp;
    private SerializedProperty weightProp;

    private SerializedProperty rarityProp;
    private SerializedProperty canUpgradeProp;
    private SerializedProperty canCraftProp;
    private SerializedProperty upgradeCoinCostProp;
    private SerializedProperty upgradeMaterialRequirementsProp;
    private SerializedProperty upgradeCostStagesProp;
    private SerializedProperty physicalDamageGrowthProp;
    private SerializedProperty magicDamageGrowthProp;
    private SerializedProperty criticalHitGrowthProp;
    private SerializedProperty criticalChanceGrowthProp;
    private SerializedProperty strengthScalingRankGrowthProp;
    private SerializedProperty dexterityScalingRankGrowthProp;
    private SerializedProperty intelligenceScalingRankGrowthProp;
    private SerializedProperty faithScalingRankGrowthProp;
    private SerializedProperty physicalBlockGrowthProp;
    private SerializedProperty magicBlockGrowthProp;
    private SerializedProperty stabilityGrowthProp;

    private SerializedProperty scalingProp;
    private SerializedProperty strengthScalingRankProp;
    private SerializedProperty dexterityScalingRankProp;
    private SerializedProperty intelligenceScalingRankProp;
    private SerializedProperty faithScalingRankProp;

    private SerializedProperty strengthRequirementProp;
    private SerializedProperty dexterityRequirementProp;
    private SerializedProperty intelligenceRequirementProp;
    private SerializedProperty faithRequirementProp;
    private SerializedProperty requirementsProp;

    private SerializedProperty animationProfileProp;
    private SerializedProperty lightAttackStaminaCostProp;
    private SerializedProperty heavyAttackStaminaCostProp;

    private SerializedProperty canBlockProp;
    private SerializedProperty canParryProp;
    private SerializedProperty physicalBlockPercentProp;
    private SerializedProperty magicBlockPercentProp;
    private SerializedProperty stabilityProp;
    private SerializedProperty parryWindowStartProp;
    private SerializedProperty parryWindowDurationProp;

    private SerializedProperty hasRightSkillProp;
    private SerializedProperty hasLeftSkillProp;
    private SerializedProperty isSpecialWeaponProp;

    private SerializedProperty wandLightProjectilePrefabProp;
    private SerializedProperty wandLightManaCostProp;
    private SerializedProperty wandLightCooldownProp;
    private SerializedProperty wandLightProjectileSpeedProp;
    private SerializedProperty wandLightProjectileLifetimeProp;
    private SerializedProperty wandLightSpawnOffsetProp;
    private SerializedProperty wandHitMaskProp;

    private SerializedProperty bowProjectilePrefabProp;
    private SerializedProperty bowAmmoItemProp;
    private SerializedProperty bowShotCooldownProp;
    private SerializedProperty bowProjectileSpeedProp;
    private SerializedProperty bowProjectileLifetimeProp;
    private SerializedProperty bowSpawnOffsetProp;
    private SerializedProperty bowHitMaskProp;

    private SerializedProperty canBeThrownProp;
    private SerializedProperty throwStrengthRequirementProp;
    private SerializedProperty throwProjectilePrefabProp;
    private SerializedProperty throwSpeedProp;
    private SerializedProperty throwLifetimeProp;
    private SerializedProperty throwStaminaCostProp;
    private SerializedProperty throwBladeHitChanceProp;
    private SerializedProperty throwHandleDamageMultiplierProp;
    private SerializedProperty throwBreakChanceProp;

    private SerializedProperty droppedPickupColliderCenterProp;
    private SerializedProperty droppedPickupColliderSizeProp;
    private SerializedProperty droppedPickupMassProp;
    private SerializedProperty droppedPickupLinearDragProp;
    private SerializedProperty droppedPickupAngularDragProp;
    private SerializedProperty droppedForwardImpulseProp;
    private SerializedProperty droppedUpImpulseProp;
    private SerializedProperty droppedInitialTorqueProp;
    private SerializedProperty droppedModelLocalEulerProp;

    private void OnEnable()
    {
        weaponNameProp = serializedObject.FindProperty("weaponName");
        iconProp = serializedObject.FindProperty("icon");
        modelPrefabProp = serializedObject.FindProperty("modelPrefab");
        descriptionProp = serializedObject.FindProperty("description");

        categoryProp = serializedObject.FindProperty("category");
        rangeTypeProp = serializedObject.FindProperty("rangeType");

        damageTypeProp = serializedObject.FindProperty("damageType");
        physicalDamageProp = serializedObject.FindProperty("physicalDamage");
        magicDamageProp = serializedObject.FindProperty("magicDamage");
        criticalHitProp = serializedObject.FindProperty("criticalHit");
        criticalChanceProp = serializedObject.FindProperty("criticalChance");
        lightDamageMultiplierProp = serializedObject.FindProperty("lightDamageMultiplier");
        heavyDamageMultiplierProp = serializedObject.FindProperty("heavyDamageMultiplier");
        weightProp = serializedObject.FindProperty("weight");

        rarityProp = serializedObject.FindProperty("rarity");
        canUpgradeProp = serializedObject.FindProperty("canUpgrade");
        canCraftProp = serializedObject.FindProperty("canCraft");
        upgradeCoinCostProp = serializedObject.FindProperty("upgradeCoinCost");
        upgradeMaterialRequirementsProp = serializedObject.FindProperty("upgradeMaterialRequirements");
        upgradeCostStagesProp = serializedObject.FindProperty("upgradeCostStages");
        physicalDamageGrowthProp = serializedObject.FindProperty("physicalDamageGrowth");
        magicDamageGrowthProp = serializedObject.FindProperty("magicDamageGrowth");
        criticalHitGrowthProp = serializedObject.FindProperty("criticalHitGrowth");
        criticalChanceGrowthProp = serializedObject.FindProperty("criticalChanceGrowth");
        strengthScalingRankGrowthProp = serializedObject.FindProperty("strengthScalingRankGrowth");
        dexterityScalingRankGrowthProp = serializedObject.FindProperty("dexterityScalingRankGrowth");
        intelligenceScalingRankGrowthProp = serializedObject.FindProperty("intelligenceScalingRankGrowth");
        faithScalingRankGrowthProp = serializedObject.FindProperty("faithScalingRankGrowth");
        physicalBlockGrowthProp = serializedObject.FindProperty("physicalBlockGrowth");
        magicBlockGrowthProp = serializedObject.FindProperty("magicBlockGrowth");
        stabilityGrowthProp = serializedObject.FindProperty("stabilityGrowth");

        scalingProp = serializedObject.FindProperty("scaling");
        strengthScalingRankProp = serializedObject.FindProperty("strengthScalingRank");
        dexterityScalingRankProp = serializedObject.FindProperty("dexterityScalingRank");
        intelligenceScalingRankProp = serializedObject.FindProperty("intelligenceScalingRank");
        faithScalingRankProp = serializedObject.FindProperty("faithScalingRank");

        strengthRequirementProp = serializedObject.FindProperty("strengthRequirement");
        dexterityRequirementProp = serializedObject.FindProperty("dexterityRequirement");
        intelligenceRequirementProp = serializedObject.FindProperty("intelligenceRequirement");
        faithRequirementProp = serializedObject.FindProperty("faithRequirement");
        requirementsProp = serializedObject.FindProperty("requirements");

        animationProfileProp = serializedObject.FindProperty("animationProfile");
        lightAttackStaminaCostProp = serializedObject.FindProperty("lightAttackStaminaCost");
        heavyAttackStaminaCostProp = serializedObject.FindProperty("heavyAttackStaminaCost");

        canBlockProp = serializedObject.FindProperty("canBlock");
        canParryProp = serializedObject.FindProperty("canParry");
        physicalBlockPercentProp = serializedObject.FindProperty("physicalBlockPercent");
        magicBlockPercentProp = serializedObject.FindProperty("magicBlockPercent");
        stabilityProp = serializedObject.FindProperty("stability");
        parryWindowStartProp = serializedObject.FindProperty("parryWindowStart");
        parryWindowDurationProp = serializedObject.FindProperty("parryWindowDuration");

        hasRightSkillProp = serializedObject.FindProperty("hasRightSkill");
        hasLeftSkillProp = serializedObject.FindProperty("hasLeftSkill");
        isSpecialWeaponProp = serializedObject.FindProperty("isSpecialWeapon");

        wandLightProjectilePrefabProp = serializedObject.FindProperty("wandLightProjectilePrefab");
        wandLightManaCostProp = serializedObject.FindProperty("wandLightManaCost");
        wandLightCooldownProp = serializedObject.FindProperty("wandLightCooldown");
        wandLightProjectileSpeedProp = serializedObject.FindProperty("wandLightProjectileSpeed");
        wandLightProjectileLifetimeProp = serializedObject.FindProperty("wandLightProjectileLifetime");
        wandLightSpawnOffsetProp = serializedObject.FindProperty("wandLightSpawnOffset");
        wandHitMaskProp = serializedObject.FindProperty("wandHitMask");

        bowProjectilePrefabProp = serializedObject.FindProperty("bowProjectilePrefab");
        bowAmmoItemProp = serializedObject.FindProperty("bowAmmoItem");
        bowShotCooldownProp = serializedObject.FindProperty("bowShotCooldown");
        bowProjectileSpeedProp = serializedObject.FindProperty("bowProjectileSpeed");
        bowProjectileLifetimeProp = serializedObject.FindProperty("bowProjectileLifetime");
        bowSpawnOffsetProp = serializedObject.FindProperty("bowSpawnOffset");
        bowHitMaskProp = serializedObject.FindProperty("bowHitMask");

        canBeThrownProp = serializedObject.FindProperty("canBeThrown");
        throwStrengthRequirementProp = serializedObject.FindProperty("throwStrengthRequirement");
        throwProjectilePrefabProp = serializedObject.FindProperty("throwProjectilePrefab");
        throwSpeedProp = serializedObject.FindProperty("throwSpeed");
        throwLifetimeProp = serializedObject.FindProperty("throwLifetime");
        throwStaminaCostProp = serializedObject.FindProperty("throwStaminaCost");
        throwBladeHitChanceProp = serializedObject.FindProperty("throwBladeHitChance");
        throwHandleDamageMultiplierProp = serializedObject.FindProperty("throwHandleDamageMultiplier");
        throwBreakChanceProp = serializedObject.FindProperty("throwBreakChance");

        droppedPickupColliderCenterProp = serializedObject.FindProperty("droppedPickupColliderCenter");
        droppedPickupColliderSizeProp = serializedObject.FindProperty("droppedPickupColliderSize");
        droppedPickupMassProp = serializedObject.FindProperty("droppedPickupMass");
        droppedPickupLinearDragProp = serializedObject.FindProperty("droppedPickupLinearDrag");
        droppedPickupAngularDragProp = serializedObject.FindProperty("droppedPickupAngularDrag");
        droppedForwardImpulseProp = serializedObject.FindProperty("droppedForwardImpulse");
        droppedUpImpulseProp = serializedObject.FindProperty("droppedUpImpulse");
        droppedInitialTorqueProp = serializedObject.FindProperty("droppedInitialTorque");
        droppedModelLocalEulerProp = serializedObject.FindProperty("droppedModelLocalEuler");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(weaponNameProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Visual", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(iconProp);
        EditorGUILayout.PropertyField(modelPrefabProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(descriptionProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Category", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(categoryProp);
        EditorGUILayout.PropertyField(rangeTypeProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Danni", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(damageTypeProp);
        EditorGUILayout.PropertyField(physicalDamageProp);
        EditorGUILayout.PropertyField(magicDamageProp);
        EditorGUILayout.PropertyField(criticalHitProp);
        EditorGUILayout.PropertyField(criticalChanceProp);
        EditorGUILayout.PropertyField(lightDamageMultiplierProp);
        EditorGUILayout.PropertyField(heavyDamageMultiplierProp);
        EditorGUILayout.PropertyField(weightProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Blacksmith", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(rarityProp);
        EditorGUILayout.PropertyField(canUpgradeProp);
        EditorGUILayout.PropertyField(canCraftProp);
        EditorGUILayout.PropertyField(upgradeCoinCostProp);
        EditorGUILayout.PropertyField(upgradeMaterialRequirementsProp, true);
        EditorGUILayout.PropertyField(upgradeCostStagesProp, true);
        EditorGUILayout.LabelField("Upgrade Growth", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(physicalDamageGrowthProp);
        EditorGUILayout.PropertyField(magicDamageGrowthProp);
        EditorGUILayout.PropertyField(criticalHitGrowthProp);
        EditorGUILayout.PropertyField(criticalChanceGrowthProp);
        EditorGUILayout.PropertyField(strengthScalingRankGrowthProp);
        EditorGUILayout.PropertyField(dexterityScalingRankGrowthProp);
        EditorGUILayout.PropertyField(intelligenceScalingRankGrowthProp);
        EditorGUILayout.PropertyField(faithScalingRankGrowthProp);
        EditorGUILayout.PropertyField(physicalBlockGrowthProp);
        EditorGUILayout.PropertyField(magicBlockGrowthProp);
        EditorGUILayout.PropertyField(stabilityGrowthProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Scaling", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(scalingProp);
        EditorGUILayout.PropertyField(strengthScalingRankProp);
        EditorGUILayout.PropertyField(dexterityScalingRankProp);
        EditorGUILayout.PropertyField(intelligenceScalingRankProp);
        EditorGUILayout.PropertyField(faithScalingRankProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Requisiti", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(strengthRequirementProp);
        EditorGUILayout.PropertyField(dexterityRequirementProp);
        EditorGUILayout.PropertyField(intelligenceRequirementProp);
        EditorGUILayout.PropertyField(faithRequirementProp);
        EditorGUILayout.PropertyField(requirementsProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Animation Profile", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(animationProfileProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Stamina Cost", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(lightAttackStaminaCostProp);
        EditorGUILayout.PropertyField(heavyAttackStaminaCostProp);

        WeaponCategory category = (WeaponCategory)categoryProp.enumValueIndex;

        if (category == WeaponCategory.Shield)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Shield (Block / Parry)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(canBlockProp);
            EditorGUILayout.PropertyField(canParryProp);
            EditorGUILayout.PropertyField(physicalBlockPercentProp);
            EditorGUILayout.PropertyField(magicBlockPercentProp);
            EditorGUILayout.PropertyField(stabilityProp);
            EditorGUILayout.PropertyField(parryWindowStartProp);
            EditorGUILayout.PropertyField(parryWindowDurationProp);
        }

        if (category == WeaponCategory.Wand)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Wand (Magic Casting)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(wandLightProjectilePrefabProp);
            EditorGUILayout.PropertyField(wandLightManaCostProp);
            EditorGUILayout.PropertyField(wandLightCooldownProp);
            EditorGUILayout.PropertyField(wandLightProjectileSpeedProp);
            EditorGUILayout.PropertyField(wandLightProjectileLifetimeProp);
            EditorGUILayout.PropertyField(wandLightSpawnOffsetProp);
            EditorGUILayout.PropertyField(wandHitMaskProp);
        }

        if (category == WeaponCategory.Bow)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Bow (Arrow Shooting)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(bowProjectilePrefabProp);
            EditorGUILayout.PropertyField(bowAmmoItemProp);
            EditorGUILayout.PropertyField(bowShotCooldownProp);
            EditorGUILayout.PropertyField(bowProjectileSpeedProp);
            EditorGUILayout.PropertyField(bowProjectileLifetimeProp);
            EditorGUILayout.PropertyField(bowSpawnOffsetProp);
            EditorGUILayout.PropertyField(bowHitMaskProp);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Abilita (per il futuro)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(hasRightSkillProp);
        EditorGUILayout.PropertyField(hasLeftSkillProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Special Weapon", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(isSpecialWeaponProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Throw", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(canBeThrownProp);
        EditorGUILayout.PropertyField(throwStrengthRequirementProp);
        EditorGUILayout.PropertyField(throwProjectilePrefabProp);
        EditorGUILayout.PropertyField(throwSpeedProp);
        EditorGUILayout.PropertyField(throwLifetimeProp);
        EditorGUILayout.PropertyField(throwStaminaCostProp);
        EditorGUILayout.PropertyField(throwBladeHitChanceProp);
        EditorGUILayout.PropertyField(throwHandleDamageMultiplierProp);
        EditorGUILayout.PropertyField(throwBreakChanceProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Dropped Pickup Physics", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(droppedPickupColliderCenterProp);
        EditorGUILayout.PropertyField(droppedPickupColliderSizeProp);
        EditorGUILayout.PropertyField(droppedPickupMassProp);
        EditorGUILayout.PropertyField(droppedPickupLinearDragProp);
        EditorGUILayout.PropertyField(droppedPickupAngularDragProp);
        EditorGUILayout.PropertyField(droppedForwardImpulseProp);
        EditorGUILayout.PropertyField(droppedUpImpulseProp);
        EditorGUILayout.PropertyField(droppedInitialTorqueProp);
        EditorGUILayout.PropertyField(droppedModelLocalEulerProp);

        serializedObject.ApplyModifiedProperties();
    }
}
