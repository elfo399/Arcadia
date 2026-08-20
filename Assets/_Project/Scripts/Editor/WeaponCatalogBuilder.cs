using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>
/// Keeps every gameplay weapon prefab under the item database represented by one
/// complete WeaponItem and one entry in its category registry. It is safe to run
/// again when a prefab is added or a visual needs to be regenerated.
/// </summary>
public static class WeaponCatalogBuilder
{
    private const string Root = "Assets/_Project/Data/Database/Items/Weapons";
    private const int IconSize = 256;

    private sealed class RangedReferences
    {
        public WeaponAnimationProfile meleeProfile;
        public WeaponAnimationProfile bowProfile;
        public WeaponAnimationProfile wandProfile;
        public GameObject bowProjectile;
        public ItemData bowAmmo;
        public GameObject wandProjectile;
    }

    private readonly struct IconFraming
    {
        public readonly float rollDegrees;
        public readonly float horizontalOffset;
        public readonly float verticalOffset;
        public readonly float fill;

        public IconFraming(float rollDegrees, float horizontalOffset, float verticalOffset, float fill)
        {
            this.rollDegrees = rollDegrees;
            this.horizontalOffset = horizontalOffset;
            this.verticalOffset = verticalOffset;
            this.fill = fill;
        }
    }

    [MenuItem("Arcadia/Weapons/Rebuild Configured Weapon Catalog")]
    public static void RebuildConfiguredWeaponCatalog()
    {
        RangedReferences references = ReadSharedReferences();
        var byCategory = new Dictionary<string, List<WeaponItem>>(StringComparer.OrdinalIgnoreCase);
        int configured = 0;
        int iconsGenerated = 0;

        string[] prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { Root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(IsGameplayWeaponPrefab)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        try
        {
            for (int i = 0; i < prefabPaths.Length; i++)
            {
                string prefabPath = prefabPaths[i];
                EditorUtility.DisplayProgressBar("Arcadia weapons", prefabPath, (float)i / prefabPaths.Length);

                string categoryName = GetCategoryFolder(prefabPath);
                WeaponCategory category;
                if (!TryGetCategory(categoryName, out category))
                    continue;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                WeaponItem weapon = GetOrCreateDefinition(prefabPath);
                if (prefab == null || weapon == null)
                    continue;

                ConfigureWeapon(weapon, prefab, category, categoryName, references);
                Sprite icon = RenderAndImportIcon(prefabPath);
                if (icon != null)
                {
                    weapon.icon = icon;
                    iconsGenerated++;
                }

                EditorUtility.SetDirty(weapon);
                List<WeaponItem> categoryWeapons;
                if (!byCategory.TryGetValue(categoryName, out categoryWeapons))
                {
                    categoryWeapons = new List<WeaponItem>();
                    byCategory.Add(categoryName, categoryWeapons);
                }
                categoryWeapons.Add(weapon);
                configured++;
            }

            ConfigureUnarmed(references, byCategory);
            RefreshRegistries(byCategory);
            RefreshItemDatabases(byCategory);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[WeaponCatalogBuilder] Configured {configured} prefab weapons and rendered {iconsGenerated} 3D icons.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    public static void RebuildConfiguredWeaponCatalogForBatch()
    {
        try
        {
            RebuildConfiguredWeaponCatalog();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    [MenuItem("Arcadia/Weapons/Validate Configured Weapon Catalog")]
    public static void ValidateConfiguredWeaponCatalog()
    {
        var errors = new List<string>();
        var expectedByCategory = new Dictionary<string, List<WeaponItem>>(StringComparer.OrdinalIgnoreCase);
        string[] prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { Root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(IsGameplayWeaponPrefab)
            .ToArray();

        foreach (string prefabPath in prefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            WeaponItem weapon = AssetDatabase.LoadAssetAtPath<WeaponItem>(Path.ChangeExtension(prefabPath, ".asset"));
            if (weapon == null)
            {
                errors.Add("Missing definition: " + prefabPath);
                continue;
            }
            if (weapon.modelPrefab != prefab) errors.Add("Wrong modelPrefab: " + weapon.name);
            if (weapon.icon == null) errors.Add("Missing 3D icon: " + weapon.name);
            if (string.IsNullOrWhiteSpace(weapon.definitionId)) errors.Add("Missing definitionId: " + weapon.name);
            if (string.IsNullOrWhiteSpace(weapon.GetScalingLabel())) errors.Add("Missing scaling: " + weapon.name);
            if (string.IsNullOrWhiteSpace(weapon.GetRequirementsLabel())) errors.Add("Missing requirements: " + weapon.name);

            string category = GetCategoryFolder(prefabPath);
            List<WeaponItem> weapons;
            if (!expectedByCategory.TryGetValue(category, out weapons))
            {
                weapons = new List<WeaponItem>();
                expectedByCategory.Add(category, weapons);
            }
            weapons.Add(weapon);
        }

        foreach (KeyValuePair<string, List<WeaponItem>> pair in expectedByCategory)
        {
            ItemRegistry registry = AssetDatabase.LoadAssetAtPath<ItemRegistry>(Root + "/" + pair.Key + "/" + pair.Key + "Registry.asset");
            if (registry == null)
            {
                errors.Add("Missing registry: " + pair.Key);
                continue;
            }

            foreach (WeaponItem weapon in pair.Value)
                if (registry.entries == null || !registry.entries.Any(entry => entry != null && entry.weaponData == weapon && entry.icon == weapon.icon))
                    errors.Add("Registry entry missing or stale: " + weapon.name);
        }

        if (errors.Count > 0)
            throw new InvalidOperationException("[WeaponCatalogBuilder] Validation failed:\n - " + string.Join("\n - ", errors));

        Debug.Log($"[WeaponCatalogBuilder] Validation passed for {prefabPaths.Length} prefab weapons and {expectedByCategory.Count} registries.");
    }

    // Kept separate from the menu command so Unity batch mode exits only after
    // the validation has genuinely run, even immediately after a domain reload.
    public static void ValidateConfiguredWeaponCatalogForBatch()
    {
        try
        {
            ValidateConfiguredWeaponCatalog();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static RangedReferences ReadSharedReferences()
    {
        WeaponItem bow = AssetDatabase.LoadAssetAtPath<WeaponItem>(Root + "/Bow/Bow_Basic/Bow_Basic.asset");
        WeaponItem wand = AssetDatabase.LoadAssetAtPath<WeaponItem>(Root + "/Wand/Wand_Basic/Wand_Basic.asset");
        WeaponItem unarmed = AssetDatabase.LoadAssetAtPath<WeaponItem>(Root + "/Punch/UnArmed_Item.asset");
        return new RangedReferences
        {
            meleeProfile = unarmed != null ? unarmed.animationProfile : null,
            bowProfile = bow != null ? bow.animationProfile : null,
            wandProfile = wand != null ? wand.animationProfile : null,
            bowProjectile = bow != null ? bow.bowProjectilePrefab : null,
            bowAmmo = bow != null ? bow.bowAmmoItem : null,
            wandProjectile = wand != null ? wand.wandLightProjectilePrefab : null
        };
    }

    private static bool IsGameplayWeaponPrefab(string path)
    {
        if (string.IsNullOrEmpty(path) || path.IndexOf("/Non usare/", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;

        string category = GetCategoryFolder(path);
        if (!TryGetCategory(category, out _))
            return false;

        return !string.Equals(Path.GetFileNameWithoutExtension(path), "MagicProjectile", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetCategoryFolder(string path)
    {
        string relative = path.Substring(Root.Length).TrimStart('/');
        int separator = relative.IndexOf('/');
        return separator < 0 ? string.Empty : relative.Substring(0, separator);
    }

    private static bool TryGetCategory(string categoryName, out WeaponCategory category)
    {
        switch (categoryName)
        {
            case "Axe": category = WeaponCategory.Axe; return true;
            case "Bow": category = WeaponCategory.Bow; return true;
            case "Flail": category = WeaponCategory.Flail; return true;
            case "Hammer": category = WeaponCategory.Hammer; return true;
            case "Shield": category = WeaponCategory.Shield; return true;
            case "Spear": category = WeaponCategory.Spear; return true;
            case "Sword": category = WeaponCategory.StraightSword; return true;
            case "Wand": category = WeaponCategory.Wand; return true;
            default: category = WeaponCategory.Unarmed; return false;
        }
    }

    private static WeaponItem GetOrCreateDefinition(string prefabPath)
    {
        string expectedPath = Path.ChangeExtension(prefabPath, ".asset").Replace('\\', '/');
        WeaponItem weapon = AssetDatabase.LoadAssetAtPath<WeaponItem>(expectedPath);
        if (weapon != null)
            return weapon;

        string folder = Path.GetDirectoryName(prefabPath)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(folder))
        {
            string prefabName = Path.GetFileNameWithoutExtension(prefabPath);
            string[] siblingPrefabs = AssetDatabase.FindAssets("t:Prefab", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsGameplayWeaponPrefab)
                .ToArray();
            string existingPath = AssetDatabase.FindAssets("t:WeaponItem", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(path => string.Equals(Path.GetFileNameWithoutExtension(path), prefabName, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(existingPath) && siblingPrefabs.Length == 1)
                existingPath = AssetDatabase.FindAssets("t:WeaponItem", new[] { folder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .FirstOrDefault();
            if (!string.IsNullOrEmpty(existingPath))
            {
                string moveError = AssetDatabase.MoveAsset(existingPath, expectedPath);
                if (string.IsNullOrEmpty(moveError))
                    return AssetDatabase.LoadAssetAtPath<WeaponItem>(expectedPath);

                Debug.LogWarning($"[WeaponCatalogBuilder] Could not rename '{existingPath}' to '{expectedPath}': {moveError}");
            }
        }

        weapon = ScriptableObject.CreateInstance<WeaponItem>();
        AssetDatabase.CreateAsset(weapon, expectedPath);
        return weapon;
    }

    private static void ConfigureWeapon(WeaponItem weapon, GameObject prefab, WeaponCategory category, string categoryName, RangedReferences references)
    {
        int tier = GetTier(prefab.name, prefab.transform.parent != null ? prefab.transform.parent.name : string.Empty);
        bool magic = category == WeaponCategory.Wand;
        bool shield = category == WeaponCategory.Shield;

        weapon.definitionId = string.IsNullOrWhiteSpace(weapon.definitionId)
            ? "weapon." + categoryName.ToLowerInvariant() + "." + ToKey(prefab.name)
            : weapon.definitionId;
        weapon.weaponName = GetDisplayName(category, prefab.name, tier);
        weapon.modelPrefab = prefab;
        weapon.category = category;
        weapon.rangeType = (category == WeaponCategory.Bow || magic) ? WeaponItem.WeaponRangeType.Ranged : WeaponItem.WeaponRangeType.Melee;
        weapon.damageType = magic ? WeaponItem.DamageType.Magic : WeaponItem.DamageType.Physical;
        weapon.rarity = tier == 0 ? WeaponRarity.Common : tier == 1 ? WeaponRarity.Uncommon : WeaponRarity.Rare;
        weapon.canUpgrade = true;
        weapon.canCraft = false;
        weapon.upgradeCoinCost = 30 + tier * 35;
        weapon.upgradeMaterialRequirements = new List<UpgradeMaterialRequirement>();
        weapon.upgradeCostStages = BuildUpgradeStages(weapon.upgradeCoinCost);
        weapon.physicalDamageGrowth = 0.07f + tier * 0.015f;
        weapon.magicDamageGrowth = magic ? 0.09f + tier * 0.02f : 0f;
        weapon.criticalHitGrowth = 0.01f;
        weapon.criticalChanceGrowth = 0.005f;
        weapon.strengthScalingRankGrowth = 0;
        weapon.dexterityScalingRankGrowth = 0;
        weapon.intelligenceScalingRankGrowth = 0;
        weapon.faithScalingRankGrowth = 0;
        weapon.canBeThrown = false;
        weapon.throwProjectilePrefab = null;
        weapon.hasRightSkill = false;
        weapon.hasLeftSkill = false;
        weapon.isSpecialWeapon = false;
        weapon.droppedPickupColliderCenter = new Vector3(0f, 0.05f, 0f);
        weapon.droppedPickupColliderSize = new Vector3(0.7f, 0.12f, 0.22f);
        weapon.droppedPickupMass = 1.2f + tier * 0.25f;
        weapon.droppedPickupLinearDrag = 0.12f;
        weapon.droppedPickupAngularDrag = 0.3f;
        weapon.droppedForwardImpulse = 1.2f;
        weapon.droppedUpImpulse = 0.35f;
        weapon.droppedInitialTorque = new Vector3(3f, 0.7f, 1.5f);
        weapon.droppedModelLocalEuler = new Vector3(90f, 0f, 0f);

        ConfigureCombatStats(weapon, category, tier, shield);
        weapon.animationProfile = category == WeaponCategory.Bow ? references.bowProfile
            : magic ? references.wandProfile
            : references.meleeProfile;
        weapon.bowProjectilePrefab = category == WeaponCategory.Bow ? references.bowProjectile : null;
        weapon.bowAmmoItem = category == WeaponCategory.Bow ? references.bowAmmo : null;
        weapon.wandLightProjectilePrefab = magic ? references.wandProjectile : null;
        weapon.description = GetDescription(category, tier, shield, magic);
        weapon.requirements = weapon.GetRequirementsLabel();
        weapon.scaling = weapon.GetScalingLabel();
    }

    private static void ConfigureCombatStats(WeaponItem weapon, WeaponCategory category, int tier, bool shield)
    {
        int power = tier == 0 ? 0 : tier == 1 ? 1 : 2;
        weapon.magicDamage = 0;
        weapon.physicalDamage = 0;
        weapon.criticalHit = 1.1f;
        weapon.criticalChance = 0.06f;
        weapon.lightDamageMultiplier = 1f;
        weapon.heavyDamageMultiplier = 1.35f;
        weapon.canBlock = false;
        weapon.canParry = false;
        weapon.physicalBlockPercent = 0.1f;
        weapon.magicBlockPercent = 0.05f;
        weapon.stability = 5f;
        weapon.physicalBlockGrowth = 0f;
        weapon.magicBlockGrowth = 0f;
        weapon.stabilityGrowth = 0f;
        weapon.parryWindowStart = 0.05f;
        weapon.parryWindowDuration = 0.18f;

        switch (category)
        {
            case WeaponCategory.StraightSword:
                weapon.physicalDamage = 22 + power * 12;
                weapon.weight = 3.1f + power * 0.2f;
                weapon.lightAttackStaminaCost = 9f;
                weapon.heavyAttackStaminaCost = 17f;
                SetRanks(weapon, 3 + power, 2 + power, 0, 0);
                SetRequirements(weapon, 10 + power * 6, 8 + power * 4, 0, 0);
                weapon.criticalChance = 0.09f;
                break;
            case WeaponCategory.Axe:
                weapon.physicalDamage = 29 + power * 14;
                weapon.weight = 4.3f + power * 0.35f;
                weapon.lightAttackStaminaCost = 12f;
                weapon.heavyAttackStaminaCost = 23f;
                SetRanks(weapon, 4 + power, 1 + power, 0, 0);
                SetRequirements(weapon, 14 + power * 7, 5 + power * 3, 0, 0);
                break;
            case WeaponCategory.Flail:
                weapon.physicalDamage = 26 + power * 13;
                weapon.weight = 4.0f + power * 0.3f;
                weapon.lightAttackStaminaCost = 11f;
                weapon.heavyAttackStaminaCost = 21f;
                SetRanks(weapon, 3 + power, 2 + power / 2, 0, 0);
                SetRequirements(weapon, 12 + power * 6, 8 + power * 3, 0, 0);
                weapon.criticalChance = 0.1f;
                break;
            case WeaponCategory.Hammer:
                weapon.physicalDamage = 34 + power * 16;
                weapon.weight = 6.2f + power * 0.45f;
                weapon.lightAttackStaminaCost = 15f;
                weapon.heavyAttackStaminaCost = 28f;
                SetRanks(weapon, 4 + power, 1 + power, 0, 0);
                SetRequirements(weapon, 18 + power * 8, 3 + power * 2, 0, 0);
                weapon.criticalChance = 0.04f;
                weapon.heavyDamageMultiplier = 1.5f;
                break;
            case WeaponCategory.Spear:
                weapon.physicalDamage = 23 + power * 12;
                weapon.weight = 3.8f + power * 0.25f;
                weapon.lightAttackStaminaCost = 10f;
                weapon.heavyAttackStaminaCost = 19f;
                SetRanks(weapon, 2 + power, 4 + power, 0, 0);
                SetRequirements(weapon, 9 + power * 5, 13 + power * 6, 0, 0);
                weapon.criticalChance = 0.08f;
                break;
            case WeaponCategory.Bow:
                weapon.physicalDamage = 18 + power * 11;
                weapon.weight = 2.4f + power * 0.15f;
                weapon.lightAttackStaminaCost = 8f;
                weapon.heavyAttackStaminaCost = 15f;
                SetRanks(weapon, 1 + power, 4 + power, 0, 0);
                SetRequirements(weapon, 5 + power * 3, 12 + power * 6, 0, 0);
                weapon.criticalChance = 0.12f;
                break;
            case WeaponCategory.Wand:
                weapon.physicalDamage = 2;
                weapon.magicDamage = 24 + power * 14;
                weapon.weight = 1.6f + power * 0.1f;
                weapon.lightAttackStaminaCost = 6f;
                weapon.heavyAttackStaminaCost = 12f;
                SetRanks(weapon, 0, 0, 4 + power, 2 + power);
                SetRequirements(weapon, 0, 0, 12 + power * 7, 7 + power * 4);
                weapon.criticalChance = 0.07f;
                weapon.wandLightManaCost = 6f + power;
                weapon.wandLightCooldown = 0.22f;
                weapon.wandLightProjectileSpeed = 24f + power * 2f;
                weapon.wandLightProjectileLifetime = 3f;
                break;
            case WeaponCategory.Shield:
                weapon.physicalDamage = 8 + power * 4;
                weapon.weight = 4.8f + power * 0.6f;
                weapon.lightAttackStaminaCost = 12f;
                weapon.heavyAttackStaminaCost = 20f;
                SetRanks(weapon, 3 + power, 0, 0, 0);
                SetRequirements(weapon, 12 + power * 7, 0, 0, 0);
                weapon.canBlock = true;
                weapon.canParry = true;
                weapon.physicalBlockPercent = 0.7f + power * 0.1f;
                weapon.magicBlockPercent = 0.3f + power * 0.12f;
                weapon.stability = 28f + power * 14f;
                weapon.physicalBlockGrowth = 0.01f;
                weapon.magicBlockGrowth = 0.01f;
                weapon.stabilityGrowth = 1f;
                weapon.parryWindowDuration = 0.22f;
                break;
            default:
                SetRanks(weapon, 0, 0, 0, 0);
                SetRequirements(weapon, 0, 0, 0, 0);
                break;
        }

        weapon.baseValue = Mathf.RoundToInt((weapon.physicalDamage + weapon.magicDamage * 1.2f + weapon.stability * 0.25f) * (3f + power * 1.5f));
    }

    private static List<UpgradeCostStage> BuildUpgradeStages(int baseCost)
    {
        return new List<UpgradeCostStage>
        {
            new UpgradeCostStage { minimumTargetLevel = 1, coinCost = baseCost },
            new UpgradeCostStage { minimumTargetLevel = 4, coinCost = Mathf.RoundToInt(baseCost * 1.65f) },
            new UpgradeCostStage { minimumTargetLevel = 7, coinCost = Mathf.RoundToInt(baseCost * 2.35f) }
        };
    }

    private static void SetRanks(WeaponItem weapon, int strength, int dexterity, int intelligence, int faith)
    {
        weapon.strengthScalingRank = ToRank(strength);
        weapon.dexterityScalingRank = ToRank(dexterity);
        weapon.intelligenceScalingRank = ToRank(intelligence);
        weapon.faithScalingRank = ToRank(faith);
    }

    private static WeaponItem.ScalingRank ToRank(int value)
    {
        return (WeaponItem.ScalingRank)Mathf.Clamp(value, 0, (int)WeaponItem.ScalingRank.S);
    }

    private static void SetRequirements(WeaponItem weapon, int strength, int dexterity, int intelligence, int faith)
    {
        weapon.strengthRequirement = strength;
        weapon.dexterityRequirement = dexterity;
        weapon.intelligenceRequirement = intelligence;
        weapon.faithRequirement = faith;
    }

    private static int GetTier(string prefabName, string folderName)
    {
        string value = (prefabName + " " + folderName).ToLowerInvariant();
        if (value.Contains("epic")) return 2;
        if (value.Contains("medium")) return 1;
        return 0;
    }

    private static string GetDisplayName(WeaponCategory category, string prefabName, int tier)
    {
        string categoryName = category == WeaponCategory.StraightSword ? "Sword" : category.ToString();
        string suffix = prefabName.EndsWith("Epic2", StringComparison.OrdinalIgnoreCase) ? " II" : string.Empty;
        return categoryName + " " + (tier == 0 ? "Basic" : tier == 1 ? "Medium" : "Epic") + suffix;
    }

    private static string GetDescription(WeaponCategory category, int tier, bool shield, bool magic)
    {
        string quality = tier == 0 ? "affidabile" : tier == 1 ? "bilanciata" : "d'eccellenza";
        if (shield)
            return "Uno scudo " + quality + " progettato per parare e assorbire i colpi.";
        if (magic)
            return "Un catalizzatore " + quality + " che converte Intelligenza e Fede in danno magico.";
        if (category == WeaponCategory.Bow)
            return "Un arco " + quality + " pensato per attacchi a distanza basati su Destrezza.";
        return "Un'arma " + quality + " con requisiti e scaling calibrati per " + category + ".";
    }

    private static Sprite RenderAndImportIcon(string prefabPath)
    {
        Texture2D texture = RenderPrefabIcon(prefabPath);
        if (texture == null)
            return null;

        try
        {
            string iconPath = Path.ChangeExtension(prefabPath, ".png").Replace('\\', '/');
            string absolutePath = Application.dataPath + iconPath.Substring("Assets".Length);
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    private static Texture2D RenderPrefabIcon(string prefabPath)
    {
        GameObject contents = null;
        PreviewRenderUtility preview = null;
        RenderTexture renderTexture = null;
        Texture2D result = null;
        try
        {
            contents = PrefabUtility.LoadPrefabContents(prefabPath);
            Vector3 viewDirection = new Vector3(1.1f, 0.55f, -1.6f).normalized;
            IconFraming framing = GetIconFraming(prefabPath);
            ApplyIconFraming(contents, viewDirection, framing);
            Bounds bounds = GetBounds(contents);
            preview = new PreviewRenderUtility();
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = Color.clear;
            preview.ambientColor = new Color(0.58f, 0.62f, 0.70f, 1f);
            ConfigureUrpPreviewCamera(preview);
            preview.lights[0].intensity = 1.35f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            preview.lights[1].intensity = 0.75f;
            preview.lights[1].transform.rotation = Quaternion.Euler(340f, 215f, 0f);
            preview.AddSingleGO(contents);

            float radius = Mathf.Max(0.25f, bounds.extents.magnitude);
            Vector3 screenUp = Vector3.ProjectOnPlane(Vector3.up, viewDirection).normalized;
            Vector3 screenRight = Vector3.Cross(viewDirection, screenUp).normalized;
            Vector3 focusPoint = bounds.center
                + screenRight * (radius * framing.horizontalOffset)
                + screenUp * (radius * framing.verticalOffset);
            preview.camera.transform.position = focusPoint - viewDirection * (radius * 3.2f / framing.fill);
            preview.camera.transform.rotation = Quaternion.LookRotation(focusPoint - preview.camera.transform.position, Vector3.up);
            preview.camera.nearClipPlane = 0.01f;
            preview.camera.farClipPlane = radius * 8f + 10f;
            preview.camera.fieldOfView = 30f;

            renderTexture = RenderTexture.GetTemporary(IconSize, IconSize, 24, RenderTextureFormat.ARGB32);
            preview.camera.targetTexture = renderTexture;
            preview.camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            result = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false, false);
            result.ReadPixels(new Rect(0, 0, IconSize, IconSize), 0, 0);
            result.Apply();
            RenderTexture.active = previous;
            return result;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[WeaponCatalogBuilder] 3D icon generation failed for '{prefabPath}': {exception.Message}");
            if (result != null)
                Object.DestroyImmediate(result);
            return null;
        }
        finally
        {
            if (renderTexture != null)
                RenderTexture.ReleaseTemporary(renderTexture);
            if (preview != null)
                preview.Cleanup();
            if (contents != null)
                PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static Bounds GetBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(Vector3.zero, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static IconFraming GetIconFraming(string prefabPath)
    {
        string weaponName = Path.GetFileNameWithoutExtension(prefabPath);

        // Per-weapon overrides are deliberate art direction, not a consequence of each prefab's pivot.
        // Add an entry here whenever a new weapon needs a bespoke card composition.
        switch (weaponName)
        {
            case "Mace2H_Epic":
                return new IconFraming(0f, 0f, 0.04f, 0.92f);
        }

        switch (GetCategoryFolder(prefabPath))
        {
            case "Axe": return new IconFraming(-7f, 0f, 0.02f, 0.93f);
            case "Bow": return new IconFraming(-4f, 0f, 0f, 0.92f);
            case "Flail": return new IconFraming(-5f, 0f, 0.03f, 0.90f);
            case "Hammer": return new IconFraming(0f, 0f, 0.03f, 0.92f);
            case "Shield": return new IconFraming(0f, 0f, 0f, 0.94f);
            case "Spear": return new IconFraming(4f, 0f, 0f, 0.90f);
            case "Sword": return new IconFraming(-6f, 0f, 0.01f, 0.92f);
            case "Wand": return new IconFraming(7f, 0f, 0.02f, 0.92f);
            default: return new IconFraming(0f, 0f, 0f, 0.92f);
        }
    }

    private static void ApplyIconFraming(GameObject contents, Vector3 viewDirection, IconFraming framing)
    {
        if (Mathf.Approximately(framing.rollDegrees, 0f))
            return;

        Bounds initialBounds = GetBounds(contents);
        contents.transform.RotateAround(initialBounds.center, viewDirection, framing.rollDegrees);
    }

    private static void ConfigureUrpPreviewCamera(PreviewRenderUtility preview)
    {
        UniversalAdditionalCameraData cameraData = preview.camera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData == null)
            cameraData = preview.camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        cameraData.renderPostProcessing = false;
        cameraData.renderShadows = true;

        foreach (Light light in preview.lights)
            if (light != null && light.GetComponent<UniversalAdditionalLightData>() == null)
                light.gameObject.AddComponent<UniversalAdditionalLightData>();
    }

    private static void ConfigureUnarmed(RangedReferences references, Dictionary<string, List<WeaponItem>> byCategory)
    {
        string path = Root + "/Punch/UnArmed_Item.asset";
        WeaponItem unarmed = AssetDatabase.LoadAssetAtPath<WeaponItem>(path);
        if (unarmed == null)
            return;

        unarmed.definitionId = string.IsNullOrWhiteSpace(unarmed.definitionId) ? "weapon.punch.unarmed" : unarmed.definitionId;
        unarmed.weaponName = "Punch";
        unarmed.category = WeaponCategory.Unarmed;
        unarmed.rangeType = WeaponItem.WeaponRangeType.Melee;
        unarmed.damageType = WeaponItem.DamageType.Physical;
        unarmed.physicalDamage = 8;
        unarmed.magicDamage = 0;
        unarmed.weight = 0f;
        unarmed.canUpgrade = false;
        unarmed.canCraft = false;
        unarmed.animationProfile = references.meleeProfile;
        SetRanks(unarmed, 3, 2, 0, 0);
        SetRequirements(unarmed, 0, 0, 0, 0);
        unarmed.requirements = "None";
        unarmed.scaling = unarmed.GetScalingLabel();
        unarmed.description = "Pugni a mani nude. Non richiedono equipaggiamento.";
        EditorUtility.SetDirty(unarmed);
        byCategory["Punch"] = new List<WeaponItem> { unarmed };
    }

    private static void RefreshRegistries(Dictionary<string, List<WeaponItem>> byCategory)
    {
        foreach (KeyValuePair<string, List<WeaponItem>> pair in byCategory)
        {
            string registryPath = Root + "/" + pair.Key + "/" + pair.Key + "Registry.asset";
            ItemRegistry registry = AssetDatabase.LoadAssetAtPath<ItemRegistry>(registryPath);
            if (registry == null)
            {
                Debug.LogWarning($"[WeaponCatalogBuilder] Missing registry: {registryPath}");
                continue;
            }

            registry.entries = pair.Value
                .Where(weapon => weapon != null)
                .OrderBy(weapon => weapon.weaponName, StringComparer.OrdinalIgnoreCase)
                .Select(weapon => new ItemRegistry.Entry
                {
                    category = pair.Key,
                    key = ToKey(weapon.name),
                    itemName = weapon.weaponName,
                    icon = weapon.icon,
                    weaponData = weapon
                })
                .ToList();
            EditorUtility.SetDirty(registry);
        }
    }

    private static void RefreshItemDatabases(Dictionary<string, List<WeaponItem>> byCategory)
    {
        var buckets = new List<ItemDatabase.WeaponCategoryBucket>();
        foreach (KeyValuePair<string, List<WeaponItem>> pair in byCategory)
        {
            WeaponCategory category;
            if (!TryGetCategory(pair.Key, out category))
                continue;

            buckets.Add(new ItemDatabase.WeaponCategoryBucket
            {
                category = category,
                weapons = pair.Value
                    .Where(weapon => weapon != null)
                    .OrderBy(weapon => weapon.rarity)
                    .ThenBy(weapon => weapon.weaponName, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            });
        }

        buckets = buckets.OrderBy(bucket => bucket.category).ToList();
        string[] databasePaths = AssetDatabase.FindAssets("t:ItemDatabase")
            .Select(AssetDatabase.GUIDToAssetPath)
            .ToArray();
        foreach (string databasePath in databasePaths)
        {
            ItemDatabase database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(databasePath);
            if (database == null)
                continue;

            database.weaponsByCategory = buckets
                .Select(bucket => new ItemDatabase.WeaponCategoryBucket
                {
                    category = bucket.category,
                    weapons = new List<WeaponItem>(bucket.weapons)
                })
                .ToList();
            EditorUtility.SetDirty(database);
        }
    }

    private static string ToKey(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var builder = new System.Text.StringBuilder(value.Length + 4);
        for (int i = 0; i < value.Length; i++)
        {
            char character = char.ToLowerInvariant(value[i]);
            if (char.IsLetterOrDigit(character))
                builder.Append(character);
            else if (builder.Length > 0 && builder[builder.Length - 1] != '_')
                builder.Append('_');
        }
        return builder.ToString().Trim('_');
    }
}
