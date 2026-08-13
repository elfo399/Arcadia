using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DEBUG / TEST only. Builds one valid, database-driven player state and then
/// leaves the gameplay systems untouched. Disabled by default in every scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerTestBootstrap : MonoBehaviour
{
    [Header("DEBUG / TEST")]
    [SerializeField] private bool enableTestPlayerSetup;
    [SerializeField] private bool resetInventoryBeforeSetup = true;
    [SerializeField] private bool giveTestInventory = true;
    [SerializeField] private bool giveTestMaterials = true;
    [SerializeField] private bool giveTestCoins = true;
    [SerializeField] private bool giveTestStats = true;
    [SerializeField] private bool giveTestMagics = true;
    [SerializeField] private bool equipTestLoadout = true;
    [Tooltip("When false, test mutations stay runtime-only and all PlayerStats saves are suppressed for this session.")]
    [SerializeField] private bool persistTestSetup;

    [Header("Amounts")]
    [SerializeField, Min(0)] private int testCoins = 99999;
    [SerializeField, Min(1)] private int materialAmount = 999;
    [SerializeField, Min(1)] private int minimumTestStat = 50;
    [SerializeField, Min(0)] private int statRequirementMargin = 5;
    [SerializeField, Range(1, 4)] private int learnedMagicCount = 3;
    [SerializeField, Range(0, 3)] private int preparedMagicCount = 3;
    [SerializeField, Range(0, 2)] private int foundMagicCount = 2;

    private bool appliedThisSession;
    private PlayerInventory inventory;
    private PlayerStats stats;

    private IEnumerator Start()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!enableTestPlayerSetup)
            yield break;

        inventory = GetComponent<PlayerInventory>();
        stats = GetComponent<PlayerStats>();
        int frames = 0;
        while (stats != null && !stats.IsPersistentStateReady && frames++ < 600)
            yield return null;

        if (stats != null && !stats.IsPersistentStateReady)
        {
            Debug.LogWarning("[PlayerTestBootstrap] Stato persistente non pronto: setup annullato per non sovrascrivere il caricamento.", this);
            yield break;
        }

        ApplyTestPlayerSetupInternal(force: false);
#else
        yield break;
#endif
    }

    [ContextMenu("Apply Test Player Setup")]
    private void ApplyTestPlayerSetup()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[PlayerTestBootstrap] Entra in Play Mode prima di applicare il setup manuale.", this);
            return;
        }

        ApplyTestPlayerSetupInternal(force: true);
#else
        Debug.LogWarning("[PlayerTestBootstrap] Il bootstrap e' disponibile solo in Editor o Development Build.", this);
#endif
    }

    private void ApplyTestPlayerSetupInternal(bool force)
    {
        if (appliedThisSession && !force)
            return;

        inventory ??= GetComponent<PlayerInventory>();
        stats ??= GetComponent<PlayerStats>();
        if (inventory == null || stats == null)
        {
            Debug.LogWarning("[PlayerTestBootstrap] PlayerInventory o PlayerStats mancanti: setup annullato.", this);
            return;
        }

        ItemDatabase database = inventory.ItemDatabase;
        if (database == null)
        {
            Debug.LogWarning("[PlayerTestBootstrap] ItemDatabase mancante: setup annullato.", this);
            return;
        }

        stats.SetRuntimeSaveSuppressedForTesting(!persistTestSetup);
        if (resetInventoryBeforeSetup)
            inventory.ClearRunInventory(save: false);

        var report = new SetupReport();
        if (giveTestStats) ApplyTestStats(database);
        if (giveTestCoins) ApplyTestCoins();
        if (giveTestMaterials) report.materials = FillMaterialStorage(database);
        if (giveTestInventory) FillNormalInventory(database, report);
        if (giveTestMagics) ConfigureTestMagics(database, report);
        if (equipTestLoadout) EquipTestLoadout(report);

        appliedThisSession = true;
        if (persistTestSetup)
            stats.SaveStatsImmediate();

        string skipped = report.normalSkipped == 0
            ? "none"
            : string.Join(", ", report.skippedNormalNames);
        Debug.Log($"[PlayerTestBootstrap] Setup applicato una volta | normal {inventory.NormalUsedSlots}/{inventory.NormalInventoryCapacity}, magic {inventory.MagicUsedSlots}/{inventory.MagicInventoryCapacity}, materials {report.materials}, normal added {report.normalAdded}, skipped normal ({report.normalSkipped}): {skipped}, learned {report.learned}, prepared {report.prepared}, found {report.found}, magic equipped {report.magicEquipped}.", this);
    }

    private void ApplyTestCoins()
    {
        int target = Mathf.Max(0, testCoins);
        if (stats.runCoins < target)
            stats.AddCoins(target - stats.runCoins, save: false);
    }

    private void ApplyTestStats(ItemDatabase database)
    {
        int[] required = new int[7];
        IReadOnlyList<MagicRecipeData> recipes = database.MagicRecipes;
        for (int i = 0; i < recipes.Count; i++)
        {
            MagicItemData magic = recipes[i] != null ? recipes[i].resultMagic : null;
            if (magic == null || magic.StatRequirements == null) continue;
            for (int j = 0; j < magic.StatRequirements.Count; j++)
            {
                MagicStatRequirement requirement = magic.StatRequirements[j];
                if (requirement == null) continue;
                int index = (int)requirement.attribute;
                if (index >= 0 && index < required.Length)
                    required[index] = Mathf.Max(required[index], Mathf.Max(1, requirement.requiredValue));
            }
        }

        int margin = Mathf.Max(0, statRequirementMargin);
        stats.vigor = Mathf.Max(stats.vigor, Mathf.Max(minimumTestStat, required[(int)MagicStatAttribute.Vigor] + margin));
        stats.mind = Mathf.Max(stats.mind, Mathf.Max(minimumTestStat, required[(int)MagicStatAttribute.Mind] + margin));
        stats.endurance = Mathf.Max(stats.endurance, Mathf.Max(minimumTestStat, required[(int)MagicStatAttribute.Endurance] + margin));
        stats.strength = Mathf.Max(stats.strength, Mathf.Max(minimumTestStat, required[(int)MagicStatAttribute.Strength] + margin));
        stats.dexterity = Mathf.Max(stats.dexterity, Mathf.Max(minimumTestStat, required[(int)MagicStatAttribute.Dexterity] + margin));
        stats.intelligence = Mathf.Max(stats.intelligence, Mathf.Max(minimumTestStat, required[(int)MagicStatAttribute.Intelligence] + margin));
        stats.faith = Mathf.Max(stats.faith, Mathf.Max(minimumTestStat, required[(int)MagicStatAttribute.Faith] + margin));
        stats.RecalculateDerivedStats(keepCurrentRatio: false);
        stats.currentHealth = stats.maxHealth;
        stats.currentMana = stats.maxMana;
        stats.currentStamina = stats.maxStamina;
    }

    private int FillMaterialStorage(ItemDatabase database)
    {
        int filled = 0;
        int target = Mathf.Max(1, materialAmount);
        for (int i = 0; i < database.items.Count; i++)
        {
            ItemData item = database.items[i];
            if (item == null || item.category != ItemCategory.Material) continue;
            int missing = target - stats.MaterialStorage.GetAmount(item);
            if (missing > 0 && stats.MaterialStorage.TryAdd(item, missing)) filled++;
        }
        if (filled == 0)
            Debug.LogWarning("[PlayerTestBootstrap] Nessun materiale valido trovato in ItemDatabase.", this);
        return filled;
    }

    private void FillNormalInventory(ItemDatabase database, SetupReport report)
    {
        foreach (WeaponItem weapon in database.BuildFlatWeaponList())
            if (weapon != null) AddNormalItem(weapon, 1, report);
        for (int i = 0; i < database.armors.Count; i++)
            if (database.armors[i] != null) AddNormalItem(database.armors[i], 1, report);
        for (int i = 0; i < database.usables.Count; i++)
            if (database.usables[i] != null) AddNormalItem(database.usables[i], 3, report);
        for (int i = 0; i < database.items.Count; i++)
        {
            ItemData item = database.items[i];
            if (item != null && item.category != ItemCategory.Material) AddNormalItem(item, 3, report);
        }
    }

    private void AddNormalItem(ScriptableObject item, int amount, SetupReport report)
    {
        if (inventory.TryAddItem(item, amount, save: false)) report.normalAdded++;
        else
        {
            report.normalSkipped++;
            report.skippedNormalNames.Add(item.name);
        }
    }

    private void ConfigureTestMagics(ItemDatabase database, SetupReport report)
    {
        var learnedRecipes = new List<MagicRecipeData>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<MagicRecipeData> catalog = database.MagicRecipes;
        for (int i = 0; i < catalog.Count && learnedRecipes.Count < Mathf.Max(1, learnedMagicCount); i++)
        {
            MagicRecipeData recipe = catalog[i];
            if (recipe == null || recipe.resultMagic == null || string.IsNullOrWhiteSpace(recipe.recipeId) || !ids.Add(recipe.recipeId.Trim())) continue;
            stats.UnlockMagicRecipe(recipe.recipeId, save: false);
            stats.LearnMagicRecipe(recipe.recipeId, save: false);
            learnedRecipes.Add(recipe);
            report.learned++;
        }

        int preparedTarget = Mathf.Min(Mathf.Max(0, preparedMagicCount), learnedRecipes.Count);
        for (int i = 0; i < preparedTarget; i++)
            if (inventory.TrySetPreparedMagicAtSlot(i, learnedRecipes[i].recipeId, stats.KnowsMagicRecipe)) report.prepared++;

        var foundCandidates = new List<MagicItemData>();
        var seenMagic = new HashSet<MagicItemData>();
        for (int i = 0; i < database.magics.Count; i++)
            if (database.magics[i] != null && seenMagic.Add(database.magics[i])) foundCandidates.Add(database.magics[i]);
        for (int i = 0; i < learnedRecipes.Count; i++)
            if (learnedRecipes[i].resultMagic != null && seenMagic.Add(learnedRecipes[i].resultMagic)) foundCandidates.Add(learnedRecipes[i].resultMagic);

        int targetFound = Mathf.Min(Mathf.Max(0, foundMagicCount), foundCandidates.Count);
        for (int i = 0; i < targetFound; i++)
            if (inventory.TryAddItem(foundCandidates[i], 1, save: false)) report.found++;

        if (learnedRecipes.Count == 0)
            Debug.LogWarning("[PlayerTestBootstrap] Nessuna MagicRecipeData valida nel catalogo condiviso.", this);
    }

    private void EquipTestLoadout(SetupReport report)
    {
        WeaponItem firstWeapon = null;
        WeaponItem secondWeapon = null;
        UsableItemData firstUsable = null;
        var equippedArmorSlots = new HashSet<ArmorItemData.ArmorSlot>();
        for (int i = 0; i < inventory.Items.Count; i++)
        {
            InventoryItem item = inventory.Items[i];
            if (item == null) continue;
            if (item.weaponData != null)
            {
                if (firstWeapon == null) firstWeapon = item.weaponData;
                else if (secondWeapon == null && item.weaponData != firstWeapon) secondWeapon = item.weaponData;
            }
            if (firstUsable == null && item.usableData != null) firstUsable = item.usableData;
            if (item.armorData != null && equippedArmorSlots.Add(item.armorData.slot))
                inventory.SetArmorAtSlot(item.armorData.slot, item.armorData, item.instanceId);
        }

        if (firstWeapon != null && TryFindInventoryItem(firstWeapon, out InventoryItem right)) inventory.SetRightAtSlot(0, firstWeapon, right.instanceId);
        if (secondWeapon != null && TryFindInventoryItem(secondWeapon, out InventoryItem left)) inventory.SetLeftAtSlot(0, secondWeapon, left.instanceId);
        if (firstUsable != null && TryFindInventoryItem(firstUsable, out InventoryItem usable)) inventory.SetUsableAtSlot(0, firstUsable, usable.instanceId);

        if (!inventory.TryGetMagicInventoryLayout(out MagicInventorySlotView[] layout)) return;
        int prepared = FindMagicSlot(layout, MagicInventorySlotSource.Prepared);
        int found = FindMagicSlot(layout, MagicInventorySlotSource.Found);
        if (prepared >= 0 && inventory.TryEquipMagicInventorySlot(0, prepared)) report.magicEquipped++;
        if (found >= 0 && inventory.TryEquipMagicInventorySlot(1, found)) report.magicEquipped++;
    }

    private bool TryFindInventoryItem(ScriptableObject asset, out InventoryItem result)
    {
        result = null;
        for (int i = 0; i < inventory.Items.Count; i++)
        {
            InventoryItem item = inventory.Items[i];
            if (item == null) continue;
            if ((asset is WeaponItem weapon && item.weaponData == weapon) || (asset is UsableItemData usable && item.usableData == usable))
            {
                result = item;
                return true;
            }
        }
        return false;
    }

    private static int FindMagicSlot(IReadOnlyList<MagicInventorySlotView> layout, MagicInventorySlotSource source)
    {
        for (int i = 0; i < layout.Count; i++)
            if (layout[i].Source == source && layout[i].Magic != null) return i;
        return -1;
    }

    private sealed class SetupReport
    {
        public int materials;
        public int normalAdded;
        public int normalSkipped;
        public int learned;
        public int prepared;
        public int found;
        public int magicEquipped;
        public readonly List<string> skippedNormalNames = new();
    }
}
