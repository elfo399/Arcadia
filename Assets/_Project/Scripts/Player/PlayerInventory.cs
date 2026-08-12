using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Gestisce equip (3 slot per lato), usabili e inventario base.
/// Usa instanceId per distinguere copie identiche; una singola istanza non può stare in più slot.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    public const int DefaultMagicInventoryCapacity = 6;

    // Weapons and armor are represented by one InventoryItem per copy. This
    // generous per-operation ceiling prevents malformed dialogue data from
    // allocating millions of managed objects in a single frame. Stackable
    // quantities are saturated separately at int.MaxValue.
    public const int MaxNonStackedItemsPerAddOperation = 4096;

    [SerializeField, HideInInspector, FormerlySerializedAs("rightHandWeapon")]
    private WeaponItem legacyRightHandWeapon;
    [SerializeField, HideInInspector, FormerlySerializedAs("leftHandWeapon")]
    private WeaponItem legacyLeftHandWeapon;

    [Header("Default unarmed")]
    public WeaponItem unarmedRight;
    public WeaponItem unarmedLeft;

    [SerializeField, HideInInspector, FormerlySerializedAs("equippedUsable")]
    private UsableItemData legacyEquippedUsable;

    public WeaponItem rightHandWeapon => GetCurrentRightWeapon();
    public WeaponItem leftHandWeapon => GetCurrentLeftWeapon();
    public UsableItemData equippedUsable => GetCurrentUsable();

    [Header("Loadout (3 slot per lato)")]
    public WeaponItem[] rightLoadout = new WeaponItem[3];
    public WeaponItem[] leftLoadout = new WeaponItem[3];
    public MagicItemData[] magicLoadout = new MagicItemData[3];
    public UsableItemData[] usableLoadout = new UsableItemData[3];
    public ArmorItemData[] armorLoadout = new ArmorItemData[4];
    private string[] rightInstanceIds = new string[3];
    private string[] leftInstanceIds = new string[3];
    private string[] magicInstanceIds = new string[3];
    private string[] usableInstanceIds = new string[3];
    private string[] armorInstanceIds = new string[4];
    public int currentRightIndex = 0;
    public int currentLeftIndex = 0;
    public int currentMagicIndex = 0;
    public int currentUsableIndex = 0;

    [Header("Runtime Equipped (Debug)")]
    [SerializeField] private WeaponItem equippedRightRuntime;
    [SerializeField] private WeaponItem equippedLeftRuntime;
    [SerializeField] private MagicItemData equippedMagicRuntime;
    [SerializeField] private UsableItemData equippedUsableRuntime;

    [System.Serializable]
    public class StartingItemEntry
    {
        public WeaponItem weapon;
        public MagicItemData magic;
        public ArmorItemData armor;
        public ItemData item;
        public UsableItemData usable;
        public int quantity = 1;
    }

    [Header("Item iniziali (per test)")]
    [SerializeField] private List<StartingItemEntry> startingLoadout = new();
    [Header("Database")]
    [SerializeField] private ItemDatabase itemDatabase;

    [Header("Gameplay Capacity")]
    [SerializeField, Min(1)] private int normalInventoryCapacity = 30;
    [SerializeField, Min(1)] private int magicInventoryCapacity = DefaultMagicInventoryCapacity;

    private readonly List<InventoryItem> items = new();
    private readonly List<InventoryItem> magicInventorySlots = new();
    public IReadOnlyList<InventoryItem> Items => items;
    public int NormalInventoryCapacity => Mathf.Max(1, normalInventoryCapacity);
    public int MagicInventoryCapacity => Mathf.Max(1, magicInventoryCapacity);
    public int NormalUsedSlots => CountUsedSlots(magic: false);
    public int MagicUsedSlots => CountUsedSlots(magic: true);
    public bool IsInitialized { get; private set; }
    private ItemDatabase cachedLookupDatabase;
    private (Dictionary<string, WeaponItem> weapons, Dictionary<string, MagicItemData> magics, Dictionary<string, ArmorItemData> armors, Dictionary<string, UsableItemData> usables, Dictionary<string, ItemData> items) cachedAssetLookups;

    void Awake()
    {
        EnsureItemDatabaseAssigned();
        items.Clear();
        foreach (var entry in startingLoadout)
        {
            if (entry == null) continue;
            int qty = Mathf.Max(1, entry.quantity);
            int assigned = (entry.weapon != null ? 1 : 0) + (entry.magic != null ? 1 : 0) + (entry.armor != null ? 1 : 0) + (entry.item != null ? 1 : 0) + (entry.usable != null ? 1 : 0);
            if (assigned == 0) continue;
            if (assigned > 1) Debug.LogWarning("[PlayerInventory] Starting item entry ha più di un campo settato. Priorità: Weapon > Magic > Armor > Usable > Item.");

            if (entry.weapon != null)
            {
                for (int i = 0; i < qty; i++) items.Add(new InventoryItem(entry.weapon, 1));
                continue;
            }
            if (entry.magic != null)
            {
                items.Add(new InventoryItem(entry.magic, qty));
                continue;
            }
            if (entry.armor != null)
            {
                items.Add(new InventoryItem(entry.armor, qty));
                continue;
            }
            if (entry.usable != null)
            {
                items.Add(new InventoryItem(entry.usable, qty));
                continue;
            }
            items.Add(new InventoryItem(entry.item, qty));
        }

        EnsureLoadoutSize();
        SeedLoadoutFromLegacyFields();
        currentRightIndex = SelectInitialIndex(rightLoadout);
        currentLeftIndex = SelectInitialIndex(leftLoadout);
        currentMagicIndex = SelectInitialIndex(magicLoadout);
        currentUsableIndex = SelectInitialIndex(usableLoadout);
        SyncEquippedReferences();
        IsInitialized = true;
    }

    private void EnsureLoadoutSize()
    {
        if (rightLoadout == null || rightLoadout.Length != 3) rightLoadout = new WeaponItem[3];
        if (leftLoadout == null || leftLoadout.Length != 3) leftLoadout = new WeaponItem[3];
        if (magicLoadout == null || magicLoadout.Length != 3) magicLoadout = new MagicItemData[3];
        if (usableLoadout == null || usableLoadout.Length != 3) usableLoadout = new UsableItemData[3];
        if (armorLoadout == null || armorLoadout.Length != 4) armorLoadout = new ArmorItemData[4];
        if (rightInstanceIds == null || rightInstanceIds.Length != 3) rightInstanceIds = new string[3];
        if (leftInstanceIds == null || leftInstanceIds.Length != 3) leftInstanceIds = new string[3];
        if (magicInstanceIds == null || magicInstanceIds.Length != 3) magicInstanceIds = new string[3];
        if (usableInstanceIds == null || usableInstanceIds.Length != 3) usableInstanceIds = new string[3];
        if (armorInstanceIds == null || armorInstanceIds.Length != 4) armorInstanceIds = new string[4];
    }

    public WeaponItem GetWeaponForHand(Hand hand)
    {
        WeaponItem equipped = (hand == Hand.Right) ? GetCurrentRightWeapon() : GetCurrentLeftWeapon();
        return equipped != null ? equipped : (hand == Hand.Right ? unarmedRight : unarmedLeft);
    }

    /// <summary>
    /// Restituisce la copia esatta equipaggiata, quando il loadout contiene il
    /// suo instanceId. Il fallback per i loadout legacy evita di rompere save
    /// precedenti che non avevano ancora un id per slot.
    /// </summary>
    public InventoryItem GetInventoryItemForHand(Hand hand)
    {
        EnsureLoadoutSize();
        WeaponItem weapon = hand == Hand.Right ? GetCurrentRightWeapon() : GetCurrentLeftWeapon();
        if (weapon == null) return null;
        string instanceId = GetCurrentWeaponInstanceId(hand);
        InventoryItem fallback = null;
        for (int i = 0; i < items.Count; i++)
        {
            InventoryItem item = items[i];
            if (item == null || item.weaponData != weapon) continue;
            if (!string.IsNullOrWhiteSpace(instanceId) && item.instanceId == instanceId)
                return item;
            fallback ??= item;
        }
        return fallback;
    }

    public InventoryItem FindWeaponInstance(string instanceId, WeaponItem weapon = null)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return null;
        for (int i = 0; i < items.Count; i++)
        {
            InventoryItem item = items[i];
            if (item == null || item.instanceId != instanceId) continue;
            if (weapon == null || item.weaponData == weapon) return item;
        }
        return null;
    }

    public string GetCurrentWeaponInstanceId(Hand hand)
    {
        EnsureLoadoutSize();
        if (hand == Hand.Right)
        {
            currentRightIndex = Mathf.Clamp(currentRightIndex, 0, rightInstanceIds.Length - 1);
            return rightInstanceIds[currentRightIndex];
        }

        currentLeftIndex = Mathf.Clamp(currentLeftIndex, 0, leftInstanceIds.Length - 1);
        return leftInstanceIds[currentLeftIndex];
    }

    public bool TryUnequipCurrentWeaponForThrow(Hand hand, out WeaponItem weapon, out string instanceId)
    {
        weapon = null;
        instanceId = null;
        EnsureLoadoutSize();

        if (hand == Hand.Right)
        {
            currentRightIndex = Mathf.Clamp(currentRightIndex, 0, rightLoadout.Length - 1);
            weapon = rightLoadout[currentRightIndex];
            instanceId = rightInstanceIds[currentRightIndex];
            if (weapon == null || string.IsNullOrWhiteSpace(instanceId)) return false;
            rightLoadout[currentRightIndex] = null;
            rightInstanceIds[currentRightIndex] = null;
            SyncEquippedReferences();
            return true;
        }

        currentLeftIndex = Mathf.Clamp(currentLeftIndex, 0, leftLoadout.Length - 1);
        weapon = leftLoadout[currentLeftIndex];
        instanceId = leftInstanceIds[currentLeftIndex];
        if (weapon == null || string.IsNullOrWhiteSpace(instanceId)) return false;
        leftLoadout[currentLeftIndex] = null;
        leftInstanceIds[currentLeftIndex] = null;
        SyncEquippedReferences();
        return true;
    }

    public bool TryRemoveWeaponInstanceFromInventory(string instanceId, WeaponItem weapon)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || weapon == null) return false;
        EnsureLoadoutSize();
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var it = items[i];
            if (it == null) continue;
            if (it.weaponData == weapon && it.instanceId == instanceId)
            {
                items.RemoveAt(i);
                ClearWeaponInstanceFromLoadouts(instanceId);
                SyncEquippedReferences();
                return true;
            }
        }
        return false;
    }

    public bool HasWeaponInstanceInInventoryPublic(string instanceId, WeaponItem weapon)
    {
        return HasWeaponInstanceInInventory(instanceId, weapon);
    }

    /// <summary>
    /// Adds the specific weapon instance represented by a world pickup. The
    /// caller-provided instance id is preserved so a stale duplicate pickup
    /// can be recognised on subsequent interactions.
    /// </summary>
    public bool TryAddWeaponInstance(WeaponItem weapon, string instanceId, bool save = true)
    {
        if (weapon == null || string.IsNullOrWhiteSpace(instanceId)) return false;
        if (HasWeaponInstanceInInventory(instanceId, weapon) || IsInstanceKnown(instanceId)) return false;
        if (!CanAddItem(weapon, 1)) return false;

        var pickup = new InventoryItem(weapon, 1);
        pickup.instanceId = instanceId;
        items.Add(pickup);
        SyncEquippedReferences();
        RaiseCollectItemEvent(weapon.name, "weapon", 1);
        if (save) RequestInventorySave();
        return true;
    }

    private void ClearWeaponInstanceFromLoadouts(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return;
        for (int i = 0; i < rightLoadout.Length; i++)
        {
            if (rightInstanceIds[i] != instanceId) continue;
            rightLoadout[i] = null;
            rightInstanceIds[i] = null;
        }
        for (int i = 0; i < leftLoadout.Length; i++)
        {
            if (leftInstanceIds[i] != instanceId) continue;
            leftLoadout[i] = null;
            leftInstanceIds[i] = null;
        }
    }

    public WeaponItem GetCurrentRightWeapon()
    {
        EnsureLoadoutSize();
        currentRightIndex = Mathf.Clamp(currentRightIndex, 0, rightLoadout.Length - 1);
        return rightLoadout[currentRightIndex];
    }

    public WeaponItem GetCurrentLeftWeapon()
    {
        EnsureLoadoutSize();
        currentLeftIndex = Mathf.Clamp(currentLeftIndex, 0, leftLoadout.Length - 1);
        return leftLoadout[currentLeftIndex];
    }

    public UsableItemData GetCurrentUsable()
    {
        EnsureLoadoutSize();
        currentUsableIndex = Mathf.Clamp(currentUsableIndex, 0, usableLoadout.Length - 1);
        return usableLoadout[currentUsableIndex];
    }

    public MagicItemData GetCurrentMagic()
    {
        EnsureLoadoutSize();
        currentMagicIndex = Mathf.Clamp(currentMagicIndex, 0, magicLoadout.Length - 1);
        return magicLoadout[currentMagicIndex];
    }

    public bool TryPeekCurrentUsable(out UsableItemData usable, out int amount)
    {
        EnsureLoadoutSize();
        usable = GetCurrentUsable();
        amount = 0;
        if (usable == null) return false;

        string currentInstanceId = usableInstanceIds != null && currentUsableIndex >= 0 && currentUsableIndex < usableInstanceIds.Length
            ? usableInstanceIds[currentUsableIndex]
            : null;

        var entry = FindUsableInventoryEntry(usable, currentInstanceId);
        if (entry == null) return false;

        amount = Mathf.Max(0, entry.amount);
        return amount > 0;
    }

    public bool TryConsumeCurrentUsable(out UsableItemData consumedUsable, out int remainingAmount)
    {
        EnsureLoadoutSize();
        consumedUsable = null;
        remainingAmount = 0;

        int slot = Mathf.Clamp(currentUsableIndex, 0, usableLoadout.Length - 1);
        var usable = usableLoadout[slot];
        if (usable == null) return false;

        string currentInstanceId = usableInstanceIds != null && slot < usableInstanceIds.Length
            ? usableInstanceIds[slot]
            : null;

        var entry = FindUsableInventoryEntry(usable, currentInstanceId);
        if (entry == null || entry.amount <= 0) return false;

        consumedUsable = usable;
        entry.amount = Mathf.Max(0, entry.amount - 1);
        remainingAmount = entry.amount;

        if (entry.amount <= 0)
        {
            items.Remove(entry);
            // Se lo stack finisce, svuota gli slot che puntano a quella stessa istanza.
            for (int i = 0; i < usableLoadout.Length; i++)
            {
                bool sameId = !string.IsNullOrEmpty(currentInstanceId) && usableInstanceIds[i] == currentInstanceId;
                bool fallbackSameUsable = string.IsNullOrEmpty(currentInstanceId) && usableLoadout[i] == usable;
                if (!sameId && !fallbackSameUsable) continue;
                usableLoadout[i] = null;
                usableInstanceIds[i] = null;
            }
        }

        SyncEquippedReferences();
        return true;
    }

    public int GetTotalItemAmount(ItemData itemData)
    {
        return GetTotalItemAmount((ScriptableObject)itemData);
    }

    public int GetTotalItemAmount(ScriptableObject itemAsset)
    {
        if (itemAsset == null) return 0;

        long total = 0;
        for (int i = 0; i < items.Count; i++)
        {
            InventoryItem item = items[i];
            if (!InventoryItemMatchesAsset(item, itemAsset)) continue;

            total += Mathf.Max(0, item.amount);
            if (total >= int.MaxValue)
                return int.MaxValue;
        }

        return (int)total;
    }

    public bool HasItem(ScriptableObject itemAsset, int amount = 1)
    {
        return itemAsset != null && amount > 0 && GetTotalItemAmount(itemAsset) >= amount;
    }

    public bool HasFreeNormalInventorySlots(int requiredSlots = 1)
    {
        return requiredSlots >= 0 && NormalUsedSlots <= NormalInventoryCapacity - requiredSlots;
    }

    public bool HasFreeMagicInventorySlots(int requiredSlots = 1)
    {
        return requiredSlots >= 0 && MagicUsedSlots <= MagicInventoryCapacity - requiredSlots;
    }

    /// <summary>
    /// Performs the allocation/overflow checks used by TryAddItem without
    /// mutating the inventory. Dialogue batches can use this to reject invalid
    /// quantities before running earlier actions.
    /// </summary>
    public bool CanAddItem(ScriptableObject itemAsset, int amount = 1)
    {
        if (itemAsset == null || amount <= 0)
            return false;

        if (itemAsset is WeaponItem || itemAsset is ArmorItemData)
        {
            return amount <= MaxNonStackedItemsPerAddOperation
                   && items.Count <= int.MaxValue - amount
                   && HasFreeNormalInventorySlots(amount);
        }

        InventoryItem existing = itemAsset switch
        {
            MagicItemData magic => FindStackableMagicItem(magic),
            UsableItemData usable => FindStackableUsableItem(usable),
            ItemData item => FindStackableGenericItem(item),
            _ => null
        };

        if (itemAsset is not (MagicItemData or UsableItemData or ItemData))
            return false;

        if (existing != null)
            return Mathf.Max(0, existing.amount) <= int.MaxValue - amount;

        return itemAsset is MagicItemData
            ? HasFreeMagicInventorySlots()
            : HasFreeNormalInventorySlots();
    }

    public bool TryAddItem(ScriptableObject itemAsset, int amount = 1, bool save = true)
    {
        if (!CanAddItem(itemAsset, amount))
        {
            if ((itemAsset is WeaponItem || itemAsset is ArmorItemData)
                && amount > MaxNonStackedItemsPerAddOperation)
            {
                Debug.LogWarning(
                    $"[PlayerInventory] Aggiunta rifiutata: {amount} copie non stackabili " +
                    $"superano il limite per operazione ({MaxNonStackedItemsPerAddOperation}).",
                    this);
            }
            return false;
        }

        bool changed;
        switch (itemAsset)
        {
            case WeaponItem weapon:
                changed = TryAddWeaponLoot(weapon, amount);
                break;
            case MagicItemData magic:
                changed = TryAddMagicLoot(magic, amount);
                break;
            case ArmorItemData armor:
                changed = TryAddArmorLoot(armor, amount);
                break;
            case UsableItemData usable:
                changed = TryAddUsableLoot(usable, amount);
                break;
            case ItemData item:
                changed = TryAddGenericItemLoot(item, amount);
                break;
            default:
                return false;
        }

        if (changed && save)
            RequestInventorySave();
        return changed;
    }

    public bool TryRemoveItem(
        ScriptableObject itemAsset,
        int amount,
        out int remainingTotal,
        bool save = true)
    {
        remainingTotal = GetTotalItemAmount(itemAsset);
        if (itemAsset == null || amount <= 0 || remainingTotal < amount)
            return false;

        int toRemove = amount;
        for (int i = items.Count - 1; i >= 0 && toRemove > 0; i--)
        {
            InventoryItem item = items[i];
            if (!InventoryItemMatchesAsset(item, itemAsset))
                continue;

            int stackAmount = Mathf.Max(0, item.amount);
            if (stackAmount <= 0)
            {
                ClearRemovedItemFromLoadouts(item);
                items.RemoveAt(i);
                continue;
            }

            int removed = Mathf.Min(stackAmount, toRemove);
            item.amount = stackAmount - removed;
            toRemove -= removed;

            if (item.amount <= 0)
            {
                ClearRemovedItemFromLoadouts(item);
                items.RemoveAt(i);
            }
        }

        // The availability pre-check makes this operation atomic. Reaching a
        // non-zero remainder would indicate externally corrupted inventory data.
        if (toRemove > 0)
        {
            Debug.LogError("[PlayerInventory] Rimozione atomica fallita dopo un pre-check valido.", this);
            remainingTotal = GetTotalItemAmount(itemAsset);
            return false;
        }

        remainingTotal = GetTotalItemAmount(itemAsset);
        if (itemAsset is not ItemData)
        {
            SyncMagicInventorySlots();
            SyncEquippedReferences();
        }
        if (save)
            RequestInventorySave();
        return true;
    }

    public bool TryRemoveInstance(string instanceId, int amount, out int remainingAmount, bool save = true)
    {
        remainingAmount = 0;
        if (string.IsNullOrEmpty(instanceId) || amount <= 0) return false;
        for (int i = 0; i < items.Count; i++)
        {
            InventoryItem item = items[i];
            if (item == null || !string.Equals(item.instanceId, instanceId, System.StringComparison.Ordinal)) continue;
            if (item.amount < amount) return false;
            item.amount -= amount;
            remainingAmount = item.amount;
            if (item.amount <= 0)
            {
                ClearRemovedItemFromLoadouts(item);
                items.RemoveAt(i);
            }
            SyncMagicInventorySlots();
            SyncEquippedReferences();
            if (save) RequestInventorySave();
            return true;
        }
        return false;
    }

    public bool TryGetItemByInstanceId(string instanceId, out InventoryItem item)
    {
        item = null;
        if (string.IsNullOrWhiteSpace(instanceId)) return false;

        for (int i = 0; i < items.Count; i++)
        {
            InventoryItem candidate = items[i];
            if (candidate == null || !string.Equals(candidate.instanceId, instanceId, System.StringComparison.Ordinal))
                continue;

            item = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Stacca l'istanza senza modificarne amount o metadata. Serve ai transfer
    /// atomici full-stack e agli oggetti non stackabili.
    /// </summary>
    public bool TryDetachInstance(string instanceId, out InventoryItem item, bool save = true)
    {
        item = null;
        if (!string.IsNullOrWhiteSpace(instanceId))
        {
            for (int i = 0; i < items.Count; i++)
            {
                InventoryItem candidate = items[i];
                if (candidate == null || !string.Equals(candidate.instanceId, instanceId, System.StringComparison.Ordinal))
                    continue;

                item = candidate;
                items.RemoveAt(i);
                ClearRemovedItemFromLoadouts(candidate);
                SyncMagicInventorySlots();
                SyncEquippedReferences();
                if (save) RequestInventorySave();
                return true;
            }
        }

        return false;
    }

    public bool TryAdjustInstanceAmount(string instanceId, int delta, out int newAmount, bool save = true)
    {
        newAmount = 0;
        if (string.IsNullOrWhiteSpace(instanceId) || delta == 0)
            return false;

        if (!TryGetItemByInstanceId(instanceId, out InventoryItem item))
            return false;

        long updated = (long)item.amount + delta;
        if (updated <= 0 || updated > int.MaxValue)
            return false;

        item.amount = (int)updated;
        newAmount = item.amount;
        SyncMagicInventorySlots();
        SyncEquippedReferences();
        if (save) RequestInventorySave();
        return true;
    }

    public void ClearRunInventory(bool save = false)
    {
        items.Clear();
        magicInventorySlots.Clear();

        EnsureLoadoutSize();
        System.Array.Clear(rightLoadout, 0, rightLoadout.Length);
        System.Array.Clear(leftLoadout, 0, leftLoadout.Length);
        System.Array.Clear(magicLoadout, 0, magicLoadout.Length);
        System.Array.Clear(usableLoadout, 0, usableLoadout.Length);
        System.Array.Clear(armorLoadout, 0, armorLoadout.Length);
        System.Array.Clear(rightInstanceIds, 0, rightInstanceIds.Length);
        System.Array.Clear(leftInstanceIds, 0, leftInstanceIds.Length);
        System.Array.Clear(magicInstanceIds, 0, magicInstanceIds.Length);
        System.Array.Clear(usableInstanceIds, 0, usableInstanceIds.Length);
        System.Array.Clear(armorInstanceIds, 0, armorInstanceIds.Length);

        currentRightIndex = 0;
        currentLeftIndex = 0;
        currentMagicIndex = 0;
        currentUsableIndex = 0;
        SyncMagicInventorySlots();
        SyncEquippedReferences();

        if (save)
            RequestInventorySave();
    }

    public bool TryConsumeItem(ItemData itemData, int amount, out int remainingTotal)
    {
        return TryRemoveItem(
            (ScriptableObject)itemData,
            Mathf.Max(1, amount),
            out remainingTotal,
            save: false);
    }

    public int GetAmmoCountForWeapon(WeaponItem weapon)
    {
        if (weapon == null || weapon.category != WeaponCategory.Bow || weapon.bowAmmoItem == null)
            return 0;
        return GetTotalItemAmount(weapon.bowAmmoItem);
    }

    public void SetRightAtSlot(int slot, WeaponItem weapon, string instanceId)
    {
        EnsureLoadoutSize();
        slot = Mathf.Clamp(slot, 0, rightLoadout.Length - 1);
        MoveWeaponWithInventorySync(weapon, instanceId, rightLoadout, rightInstanceIds, slot, leftLoadout, leftInstanceIds);
        currentRightIndex = slot;
        SyncEquippedReferences();
    }

    public void SetLeftAtSlot(int slot, WeaponItem weapon, string instanceId)
    {
        EnsureLoadoutSize();
        slot = Mathf.Clamp(slot, 0, leftLoadout.Length - 1);
        MoveWeaponWithInventorySync(weapon, instanceId, leftLoadout, leftInstanceIds, slot, rightLoadout, rightInstanceIds);
        currentLeftIndex = slot;
        SyncEquippedReferences();
    }

    public void SetUsableAtSlot(int slot, UsableItemData usable, string instanceId)
    {
        EnsureLoadoutSize();
        slot = Mathf.Clamp(slot, 0, usableLoadout.Length - 1);
        MoveUsableWithInventorySync(usable, instanceId, usableLoadout, usableInstanceIds, slot);
        currentUsableIndex = slot;
        SyncEquippedReferences();
    }

    public void SetMagicAtSlot(int slot, MagicItemData magic, string instanceId)
    {
        EnsureLoadoutSize();
        slot = Mathf.Clamp(slot, 0, magicLoadout.Length - 1);
        MoveMagicWithInventorySync(magic, instanceId, magicLoadout, magicInstanceIds, slot);
        currentMagicIndex = slot;
        SyncEquippedReferences();
    }

    public void SetArmorAtSlot(ArmorItemData.ArmorSlot slot, ArmorItemData armor, string instanceId)
    {
        EnsureLoadoutSize();
        if (armor != null && armor.slot != slot)
            return;

        int slotIndex = ArmorSlotToIndex(slot);
        if (slotIndex < 0 || slotIndex >= armorLoadout.Length)
            return;

        MoveArmorWithInventorySync(armor, instanceId, armorLoadout, armorInstanceIds, slotIndex);
        SyncEquippedReferences();

        PlayerStats stats = PlayerStats.instance != null ? PlayerStats.instance : GetComponent<PlayerStats>();
        if (stats != null)
        {
            string armorName = armor != null ? armor.itemName : "None";
            stats.RefreshArmorTotals(logTotals: true, reason: $"equip {slot} -> {armorName}");
        }
    }

    // Compatibilità: forza lo slot magia anche per magie derivate da altri tipi (es. weapon magic legacy).
    public void ForceSetMagicAtSlot(int slot, MagicItemData magic, string instanceId)
    {
        EnsureLoadoutSize();
        slot = Mathf.Clamp(slot, 0, magicLoadout.Length - 1);
        magicLoadout[slot] = magic;
        magicInstanceIds[slot] = string.IsNullOrWhiteSpace(instanceId) ? null : instanceId;
        currentMagicIndex = slot;
        SyncEquippedReferences();
    }

    public bool CycleRightWeapon(int direction = 1)
    {
        EnsureLoadoutSize();
        return CycleIndexInternal(rightLoadout, ref currentRightIndex, direction);
    }

    public bool CycleLeftWeapon(int direction = 1)
    {
        EnsureLoadoutSize();
        return CycleIndexInternal(leftLoadout, ref currentLeftIndex, direction);
    }

    public bool CycleUsable(int direction = 1)
    {
        EnsureLoadoutSize();
        return CycleIndexInternal(usableLoadout, ref currentUsableIndex, direction);
    }

    public bool CycleMagic(int direction = 1)
    {
        EnsureLoadoutSize();
        return CycleIndexInternal(magicLoadout, ref currentMagicIndex, direction);
    }

    public bool IsInstanceEquipped(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId)) return false;
        EnsureLoadoutSize();
        return ContainsInstanceId(rightInstanceIds, instanceId)
               || ContainsInstanceId(leftInstanceIds, instanceId)
               || ContainsInstanceId(magicInstanceIds, instanceId)
               || ContainsInstanceId(usableInstanceIds, instanceId)
               || ContainsInstanceId(armorInstanceIds, instanceId);
    }

    public bool IsArmorInstanceEquipped(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId)) return false;
        EnsureLoadoutSize();
        return ContainsInstanceId(armorInstanceIds, instanceId);
    }

    // Inventory management
    public void AddItem(InventoryItem item)
    {
        if (item != null)
            TryAddItemInstance(item, save: false);
    }

    /// <summary>
    /// Adds a concrete inventory entry while enforcing gameplay capacity.
    /// </summary>
    public bool TryAddItemInstance(InventoryItem item, bool save = true)
    {
        if (item == null || item.amount <= 0) return false;
        if (!string.IsNullOrWhiteSpace(item.instanceId) && IsInstanceKnown(item.instanceId)) return false;

        ScriptableObject asset = GetAssetForInventoryItem(item);
        if (asset == null) return false;
        if (!CanAddItem(asset, item.amount)) return false;

        InventoryItem existing = FindStackableInventoryItem(asset);
        if (existing != null)
        {
            if (Mathf.Max(0, existing.amount) > int.MaxValue - item.amount) return false;
            existing.amount += item.amount;
        }
        else
        {
            if ((item.weaponData != null || item.armorData != null) && string.IsNullOrWhiteSpace(item.instanceId)) return false;
            items.Add(item);
        }
        SyncMagicInventorySlots();
        SyncEquippedReferences();
        if (save) RequestInventorySave();
        return true;
    }

    /// <summary>
    /// Restores material quantities that were just removed by a failed run
    /// transaction. This is intentionally internal and silent: it does not
    /// invoke gameplay pickup events, save, or enforce capacity because it
    /// restores the immediately previous inventory state.
    /// </summary>
    internal bool TryRestoreItemAmountSilently(ItemData item, int amount)
    {
        if (item == null || item.category != ItemCategory.Material || amount <= 0) return false;

        InventoryItem existing = FindStackableGenericItem(item);
        if (existing != null)
        {
            if (Mathf.Max(0, existing.amount) > int.MaxValue - amount)
            {
                Debug.LogError($"[PlayerInventory] Rollback materiale rifiutato: overflow per {item.name}.", this);
                return false;
            }

            existing.amount += amount;
        }
        else
        {
            items.Add(new InventoryItem(item, amount));
        }

        SyncEquippedReferences();
        return true;
    }

    private int CountUsedSlots(bool magic)
    {
        int count = 0;
        for (int i = 0; i < items.Count; i++)
        {
            InventoryItem item = items[i];
            if (item != null && (item.magicData != null) == magic)
                count++;
        }
        return count;
    }

    private static ScriptableObject GetAssetForInventoryItem(InventoryItem item)
    {
        if (item == null) return null;
        return item.weaponData as ScriptableObject
               ?? item.armorData as ScriptableObject
               ?? item.magicData as ScriptableObject
               ?? item.usableData as ScriptableObject
               ?? item.itemData as ScriptableObject;
    }

    private InventoryItem FindStackableInventoryItem(ScriptableObject asset)
    {
        return asset switch
        {
            MagicItemData magic => FindStackableMagicItem(magic),
            UsableItemData usable => FindStackableUsableItem(usable),
            ItemData item => FindStackableGenericItem(item),
            _ => null
        };
    }

    private bool IsInstanceKnown(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return false;
        for (int i = 0; i < items.Count; i++)
            if (items[i] != null && items[i].instanceId == instanceId) return true;
        return false;
    }
    public void AddWeaponLoot(WeaponItem weapon, int amount = 1)
    {
        TryAddItem(weapon, amount);
    }

    private bool TryAddWeaponLoot(WeaponItem weapon, int amount)
    {
        if (weapon == null || amount <= 0)
            return false;
        if (amount > MaxNonStackedItemsPerAddOperation || items.Count > int.MaxValue - amount)
        {
            Debug.LogWarning(
                $"[PlayerInventory] Aggiunta arma rifiutata: quantita non stackabile non sicura ({amount}).",
                this);
            return false;
        }

        for (int i = 0; i < amount; i++)
            items.Add(new InventoryItem(weapon, 1));

        RaiseCollectItemEvent(weapon.name, "weapon", amount);
        return true;
    }

    public void AddArmorLoot(ArmorItemData armor, int amount = 1)
    {
        TryAddItem(armor, amount);
    }

    private bool TryAddArmorLoot(ArmorItemData armor, int amount)
    {
        if (armor == null || amount <= 0)
            return false;
        if (amount > MaxNonStackedItemsPerAddOperation || items.Count > int.MaxValue - amount)
        {
            Debug.LogWarning(
                $"[PlayerInventory] Aggiunta armatura rifiutata: quantita non stackabile non sicura ({amount}).",
                this);
            return false;
        }

        for (int i = 0; i < amount; i++)
            items.Add(new InventoryItem(armor, 1));

        RaiseCollectItemEvent(armor.name, "armor", amount);
        return true;
    }

    public void AddMagicLoot(MagicItemData magic, int amount = 1)
    {
        TryAddItem(magic, amount);
    }

    private bool TryAddMagicLoot(MagicItemData magic, int amount)
    {
        if (magic == null || amount <= 0)
            return false;

        InventoryItem existing = FindStackableMagicItem(magic);
        if (existing != null)
        {
            int added = SaturatingAddToStack(existing, amount);
            if (added <= 0)
                return false;
            RaiseCollectItemEvent(magic.name, "magic", added);
            return true;
        }

        items.Add(new InventoryItem(magic, amount));
        RaiseCollectItemEvent(magic.name, "magic", amount);
        return true;
    }

    public void AddUsableLoot(UsableItemData usable, int amount = 1)
    {
        TryAddItem(usable, amount);
    }

    private bool TryAddUsableLoot(UsableItemData usable, int amount)
    {
        if (usable == null || amount <= 0)
            return false;

        InventoryItem existing = FindStackableUsableItem(usable);
        if (existing != null)
        {
            int added = SaturatingAddToStack(existing, amount);
            if (added <= 0)
                return false;
            RaiseCollectItemEvent(usable.name, "usable", added);
            return true;
        }

        items.Add(new InventoryItem(usable, amount));
        RaiseCollectItemEvent(usable.name, "usable", amount);
        return true;
    }

    public void AddGenericItemLoot(ItemData item, int amount = 1)
    {
        TryAddItem(item, amount);
    }

    private bool TryAddGenericItemLoot(ItemData item, int amount)
    {
        if (item == null || amount <= 0)
            return false;

        InventoryItem existing = FindStackableGenericItem(item);
        if (existing != null)
        {
            int added = SaturatingAddToStack(existing, amount);
            if (added <= 0)
                return false;
            RaiseCollectItemEvent(item.name, "item", added);
            return true;
        }

        items.Add(new InventoryItem(item, amount));
        RaiseCollectItemEvent(item.name, "item", amount);
        return true;
    }

    private static int SaturatingAddToStack(InventoryItem stack, int amount)
    {
        if (stack == null || amount <= 0)
            return 0;

        int current = Mathf.Max(0, stack.amount);
        int updated = amount >= int.MaxValue - current
            ? int.MaxValue
            : current + amount;
        stack.amount = updated;
        return updated - current;
    }

    private static void RaiseCollectItemEvent(string targetId, string targetTag, int amount)
    {
        QuestEvents.Raise(QuestObjectiveEventType.CollectItem, targetId, targetTag, Mathf.Max(1, amount));
    }

    public void ReplaceAllItems(List<InventoryItem> newItems)
    {
        items.Clear();
        if (newItems != null) items.AddRange(newItems);
        SyncMagicInventorySlots();
    }

    public List<InventoryItem> GetMagicInventorySlotLayout(int minSlotCount)
    {
        SyncMagicInventorySlots(minSlotCount);
        return new List<InventoryItem>(magicInventorySlots);
    }

    public void SetMagicInventorySlotLayout(IReadOnlyList<InventoryItem> slotLayout, int minSlotCount)
    {
        magicInventorySlots.Clear();

        if (slotLayout != null)
        {
            for (int i = 0; i < slotLayout.Count; i++)
            {
                var item = slotLayout[i];
                magicInventorySlots.Add(item != null && item.magicData != null ? item : null);
            }
        }

        SyncMagicInventorySlots(minSlotCount);
        ApplyMagicInventorySlotOrderToItems();
    }

    private static bool InventoryItemMatchesAsset(InventoryItem item, ScriptableObject itemAsset)
    {
        if (item == null || itemAsset == null)
            return false;

        return itemAsset switch
        {
            WeaponItem weapon => item.weaponData == weapon,
            MagicItemData magic => item.magicData == magic,
            ArmorItemData armor => item.armorData == armor,
            UsableItemData usable => item.usableData == usable,
            ItemData genericItem => item.itemData == genericItem,
            _ => false
        };
    }

    private void ClearRemovedItemFromLoadouts(InventoryItem item)
    {
        if (item == null)
            return;

        string instanceId = item.instanceId;
        bool hasInstanceId = !string.IsNullOrWhiteSpace(instanceId);

        if (item.weaponData != null)
        {
            ClearLoadoutEntry(rightLoadout, rightInstanceIds, item.weaponData, instanceId, hasInstanceId);
            ClearLoadoutEntry(leftLoadout, leftInstanceIds, item.weaponData, instanceId, hasInstanceId);
        }
        else if (item.magicData != null)
        {
            ClearLoadoutEntry(magicLoadout, magicInstanceIds, item.magicData, instanceId, hasInstanceId);
        }
        else if (item.armorData != null)
        {
            ClearLoadoutEntry(armorLoadout, armorInstanceIds, item.armorData, instanceId, hasInstanceId);
        }
        else if (item.usableData != null)
        {
            ClearLoadoutEntry(usableLoadout, usableInstanceIds, item.usableData, instanceId, hasInstanceId);
        }
    }

    private static void ClearLoadoutEntry<T>(
        T[] loadout,
        string[] instanceIds,
        T asset,
        string instanceId,
        bool hasInstanceId)
        where T : UnityEngine.Object
    {
        if (loadout == null || instanceIds == null)
            return;

        int count = Mathf.Min(loadout.Length, instanceIds.Length);
        for (int i = 0; i < count; i++)
        {
            bool matches = hasInstanceId
                ? string.Equals(instanceIds[i], instanceId, System.StringComparison.Ordinal)
                : loadout[i] == asset && string.IsNullOrWhiteSpace(instanceIds[i]);
            if (!matches) continue;

            loadout[i] = null;
            instanceIds[i] = null;
        }
    }

    private void RequestInventorySave()
    {
        PlayerStats stats = PlayerStats.instance != null ? PlayerStats.instance : GetComponent<PlayerStats>();
        if (stats != null)
            stats.SaveStats();
    }

    private InventoryItem FindStackableGenericItem(ItemData item)
    {
        for (int i = 0; i < items.Count; i++)
        {
            InventoryItem existing = items[i];
            if (existing != null && existing.itemData == item)
                return existing;
        }

        return null;
    }

    private void SyncMagicInventorySlots(int minSlotCount = 0)
    {
        var validMagicItems = new HashSet<InventoryItem>();
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item != null && item.magicData != null)
                validMagicItems.Add(item);
        }

        if (magicInventorySlots.Count == 0)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item != null && item.magicData != null)
                    magicInventorySlots.Add(item);
            }
        }
        else
        {
            for (int i = 0; i < magicInventorySlots.Count; i++)
            {
                var item = magicInventorySlots[i];
                if (item == null || item.magicData == null || !validMagicItems.Contains(item))
                    magicInventorySlots[i] = null;
            }
        }

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item == null || item.magicData == null || magicInventorySlots.Contains(item))
                continue;

            int emptyIndex = magicInventorySlots.IndexOf(null);
            if (emptyIndex >= 0)
                magicInventorySlots[emptyIndex] = item;
            else
                magicInventorySlots.Add(item);
        }

        int required = Mathf.Max(0, minSlotCount);
        while (magicInventorySlots.Count < required)
            magicInventorySlots.Add(null);
    }

    private void ApplyMagicInventorySlotOrderToItems()
    {
        var orderedMagicItems = new List<InventoryItem>();
        for (int i = 0; i < magicInventorySlots.Count; i++)
        {
            var item = magicInventorySlots[i];
            if (item != null && item.magicData != null && items.Contains(item) && !orderedMagicItems.Contains(item))
                orderedMagicItems.Add(item);
        }

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item != null && item.magicData != null && !orderedMagicItems.Contains(item))
                orderedMagicItems.Add(item);
        }

        int magicIndex = 0;
        for (int i = 0; i < items.Count && magicIndex < orderedMagicItems.Count; i++)
        {
            var item = items[i];
            if (item == null || item.magicData == null)
                continue;

            items[i] = orderedMagicItems[magicIndex];
            magicIndex++;
        }
    }

    private InventoryItem FindStackableUsableItem(UsableItemData usable)
    {
        for (int i = 0; i < items.Count; i++)
        {
            InventoryItem existing = items[i];
            if (existing != null && existing.usableData == usable)
                return existing;
        }

        return null;
    }

    private InventoryItem FindStackableMagicItem(MagicItemData magic)
    {
        for (int i = 0; i < items.Count; i++)
        {
            InventoryItem existing = items[i];
            if (existing != null && existing.magicData == magic)
                return existing;
        }

        return null;
    }

    // Equip helpers
    private WeaponItem MoveWeaponWithInventorySync(WeaponItem weapon, string instanceId, WeaponItem[] targetLoadout, string[] targetIds, int targetSlot, WeaponItem[] otherLoadout, string[] otherIds)
    {
        var previous = targetLoadout[targetSlot];
        var prevId = targetIds[targetSlot];

        if (weapon == null)
        {
            targetLoadout[targetSlot] = null;
            targetIds[targetSlot] = null;
            return null;
        }

        if (RemoveFromLoadouts(weapon, instanceId, targetLoadout, targetIds, targetSlot, otherLoadout, otherIds))
        {
            targetLoadout[targetSlot] = weapon;
            targetIds[targetSlot] = instanceId;
            return weapon;
        }

        if (HasWeaponInstanceInInventory(instanceId, weapon))
        {
            targetLoadout[targetSlot] = weapon;
            targetIds[targetSlot] = instanceId;
            return weapon;
        }

        targetIds[targetSlot] = prevId;
        return previous;
    }

    private UsableItemData MoveUsableWithInventorySync(UsableItemData usable, string instanceId, UsableItemData[] targetLoadout, string[] targetIds, int targetSlot)
    {
        var previous = targetLoadout[targetSlot];
        var prevId = targetIds[targetSlot];

        if (usable == null)
        {
            targetLoadout[targetSlot] = null;
            targetIds[targetSlot] = null;
            return null;
        }

        if (RemoveFromLoadoutUsable(usable, instanceId, targetLoadout, targetIds, targetSlot))
        {
            targetLoadout[targetSlot] = usable;
            targetIds[targetSlot] = instanceId;
            return usable;
        }

        if (HasUsableInstanceInInventory(instanceId, usable))
        {
            targetLoadout[targetSlot] = usable;
            targetIds[targetSlot] = instanceId;
            return usable;
        }

        targetIds[targetSlot] = prevId;
        return previous;
    }

    private MagicItemData MoveMagicWithInventorySync(MagicItemData magic, string instanceId, MagicItemData[] targetLoadout, string[] targetIds, int targetSlot)
    {
        var previous = targetLoadout[targetSlot];
        var prevId = targetIds[targetSlot];

        if (magic == null)
        {
            targetLoadout[targetSlot] = null;
            targetIds[targetSlot] = null;
            return null;
        }

        if (RemoveFromLoadoutMagic(magic, instanceId, targetLoadout, targetIds, targetSlot))
        {
            targetLoadout[targetSlot] = magic;
            targetIds[targetSlot] = instanceId;
            return magic;
        }

        if (HasMagicInstanceInInventory(instanceId, magic))
        {
            targetLoadout[targetSlot] = magic;
            targetIds[targetSlot] = instanceId;
            return magic;
        }

        targetIds[targetSlot] = prevId;
        return previous;
    }

    private ArmorItemData MoveArmorWithInventorySync(ArmorItemData armor, string instanceId, ArmorItemData[] targetLoadout, string[] targetIds, int targetSlot)
    {
        var previous = targetLoadout[targetSlot];
        var prevId = targetIds[targetSlot];

        if (armor == null)
        {
            targetLoadout[targetSlot] = null;
            targetIds[targetSlot] = null;
            return null;
        }

        if (RemoveFromLoadoutArmor(armor, instanceId, targetLoadout, targetIds, targetSlot))
        {
            targetLoadout[targetSlot] = armor;
            targetIds[targetSlot] = instanceId;
            return armor;
        }

        if (HasArmorInstanceInInventory(instanceId, armor))
        {
            targetLoadout[targetSlot] = armor;
            targetIds[targetSlot] = instanceId;
            return armor;
        }

        targetIds[targetSlot] = prevId;
        return previous;
    }

    private bool ContainsInstanceId(string[] ids, string instanceId)
    {
        if (ids == null || string.IsNullOrEmpty(instanceId)) return false;
        for (int i = 0; i < ids.Length; i++)
        {
            if (ids[i] == instanceId) return true;
        }
        return false;
    }

    private bool HasWeaponInstanceInInventory(string instanceId, WeaponItem weapon)
    {
        if (string.IsNullOrEmpty(instanceId) || weapon == null) return false;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it != null && it.weaponData == weapon && it.instanceId == instanceId) return true;
        }
        return false;
    }

    private bool HasUsableInstanceInInventory(string instanceId, UsableItemData usable)
    {
        if (string.IsNullOrEmpty(instanceId) || usable == null) return false;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it != null && it.usableData == usable && it.instanceId == instanceId) return true;
        }
        return false;
    }

    private InventoryItem FindUsableInventoryEntry(UsableItemData usable, string instanceId)
    {
        if (usable == null) return null;

        if (!string.IsNullOrEmpty(instanceId))
        {
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it == null) continue;
                if (it.usableData == usable && it.instanceId == instanceId)
                    return it;
            }
        }

        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it == null) continue;
            if (it.usableData == usable)
                return it;
        }

        return null;
    }

    private bool HasMagicInstanceInInventory(string instanceId, MagicItemData magic)
    {
        if (string.IsNullOrEmpty(instanceId) || magic == null) return false;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it != null && it.magicData == magic && it.instanceId == instanceId) return true;
        }
        return false;
    }

    private bool HasArmorInstanceInInventory(string instanceId, ArmorItemData armor)
    {
        if (string.IsNullOrEmpty(instanceId) || armor == null) return false;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it != null && it.armorData == armor && it.instanceId == instanceId) return true;
        }
        return false;
    }

    private bool RemoveFromLoadouts(WeaponItem weapon, string instanceId, WeaponItem[] primary, string[] primaryIds, int targetSlot, WeaponItem[] secondary, string[] secondaryIds)
    {
        if (primary != null && primaryIds != null)
        {
            for (int i = 0; i < primary.Length; i++)
            {
                if (i == targetSlot) continue;
                if (primary[i] == weapon && primaryIds[i] == instanceId)
                {
                    primary[i] = null;
                    primaryIds[i] = null;
                    return true;
                }
            }
        }
        if (secondary != null && secondaryIds != null)
        {
            for (int i = 0; i < secondary.Length; i++)
            {
                if (secondary[i] == weapon && secondaryIds[i] == instanceId)
                {
                    secondary[i] = null;
                    secondaryIds[i] = null;
                    return true;
                }
            }
        }
        return false;
    }

    private bool RemoveFromLoadoutUsable(UsableItemData usable, string instanceId, UsableItemData[] loadout, string[] loadoutIds, int targetSlot)
    {
        if (loadout == null || loadoutIds == null) return false;
        for (int i = 0; i < loadout.Length; i++)
        {
            if (i == targetSlot) continue;
            if (loadout[i] == usable && loadoutIds[i] == instanceId)
            {
                loadout[i] = null;
                loadoutIds[i] = null;
                return true;
            }
        }
        return false;
    }

    private bool RemoveFromLoadoutMagic(MagicItemData magic, string instanceId, MagicItemData[] loadout, string[] loadoutIds, int targetSlot)
    {
        if (loadout == null || loadoutIds == null) return false;
        for (int i = 0; i < loadout.Length; i++)
        {
            if (i == targetSlot) continue;
            if (loadout[i] == magic && loadoutIds[i] == instanceId)
            {
                loadout[i] = null;
                loadoutIds[i] = null;
                return true;
            }
        }
        return false;
    }

    private bool RemoveFromLoadoutArmor(ArmorItemData armor, string instanceId, ArmorItemData[] loadout, string[] loadoutIds, int targetSlot)
    {
        if (loadout == null || loadoutIds == null) return false;
        for (int i = 0; i < loadout.Length; i++)
        {
            if (i == targetSlot) continue;
            if (loadout[i] == armor && loadoutIds[i] == instanceId)
            {
                loadout[i] = null;
                loadoutIds[i] = null;
                return true;
            }
        }
        return false;
    }

    private void SeedLoadoutFromLegacyFields()
    {
        if (IsAllNull(rightLoadout) && legacyRightHandWeapon != null)
        {
            rightLoadout[0] = legacyRightHandWeapon;
        }
        if (IsAllNull(leftLoadout) && legacyLeftHandWeapon != null)
        {
            leftLoadout[0] = legacyLeftHandWeapon;
        }
        if (IsAllNull(usableLoadout) && legacyEquippedUsable != null)
        {
            usableLoadout[0] = legacyEquippedUsable;
        }
    }

    private static bool IsAllNull<T>(T[] array) where T : class
    {
        if (array == null || array.Length == 0) return true;
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i] != null) return false;
        }
        return true;
    }

    private static int SelectInitialIndex<T>(T[] loadout) where T : class
    {
        if (loadout == null || loadout.Length == 0) return 0;

        // Default richiesto: preferisci sempre il primo slot se ha item.
        if (loadout[0] != null) return 0;

        // Fallback: primo slot non vuoto.
        for (int i = 0; i < loadout.Length; i++)
        {
            if (loadout[i] != null) return i;
        }
        return 0;
    }

    private bool CycleIndexInternal<T>(T[] loadout, ref int currentIndex, int direction) where T : class
    {
        if (loadout == null || loadout.Length == 0) return false;

        int dir = direction >= 0 ? 1 : -1;
        currentIndex = (Mathf.Clamp(currentIndex, 0, loadout.Length - 1) + dir + loadout.Length) % loadout.Length;
        SyncEquippedReferences();
        return true;
    }

    private void SyncEquippedReferences()
    {
        EnsureLoadoutSize();
        currentRightIndex = Mathf.Clamp(currentRightIndex, 0, rightLoadout.Length - 1);
        currentLeftIndex = Mathf.Clamp(currentLeftIndex, 0, leftLoadout.Length - 1);
        currentMagicIndex = Mathf.Clamp(currentMagicIndex, 0, magicLoadout.Length - 1);
        currentUsableIndex = Mathf.Clamp(currentUsableIndex, 0, usableLoadout.Length - 1);

        var right = rightLoadout[currentRightIndex];
        var left = leftLoadout[currentLeftIndex];
        var magic = magicLoadout[currentMagicIndex];
        var usable = usableLoadout[currentUsableIndex];
        equippedRightRuntime = right != null ? right : unarmedRight;
        equippedLeftRuntime = left != null ? left : unarmedLeft;
        equippedMagicRuntime = magic;
        equippedUsableRuntime = usable;

        PlayerStats stats = PlayerStats.instance != null ? PlayerStats.instance : GetComponent<PlayerStats>();
        if (stats != null)
            stats.RefreshArmorTotals();
    }

    public SavedPlayerInventoryData CreateSaveData()
    {
        EnsureLoadoutSize();

        var data = new SavedPlayerInventoryData
        {
            items = SerializeInventoryItems(),
            rightLoadout = SerializeWeaponLoadout(rightLoadout, rightInstanceIds),
            leftLoadout = SerializeWeaponLoadout(leftLoadout, leftInstanceIds),
            magicLoadout = SerializeMagicLoadout(magicLoadout, magicInstanceIds),
            usableLoadout = SerializeUsableLoadout(usableLoadout, usableInstanceIds),
            armorLoadout = SerializeArmorLoadout(armorLoadout, armorInstanceIds),
            currentRightIndex = currentRightIndex,
            currentLeftIndex = currentLeftIndex,
            currentMagicIndex = currentMagicIndex,
            currentUsableIndex = currentUsableIndex
        };

        return data;
    }

    public SavedInventoryItemData CreateSaveDataForItem(InventoryItem it)
    {
        if (it == null) return null;
        string itemType = "item";
        string assetName = string.Empty;
        string itemName = string.Empty;

        if (it.weaponData != null) { itemType = "weapon"; assetName = it.weaponData.name; itemName = it.weaponData.weaponName; }
        else if (it.magicData != null) { itemType = "magic"; assetName = it.magicData.name; itemName = it.magicData.magicName; }
        else if (it.armorData != null) { itemType = "armor"; assetName = it.armorData.name; itemName = it.armorData.itemName; }
        else if (it.usableData != null) { itemType = "usable"; assetName = it.usableData.name; itemName = it.usableData.itemName; }
        else if (it.itemData != null) { itemType = "item"; assetName = it.itemData.name; itemName = it.itemData.itemName; }
        else return null;

        return new SavedInventoryItemData
        {
            itemType = itemType,
            assetName = assetName,
            itemName = string.IsNullOrWhiteSpace(itemName) ? it.title : itemName,
            instanceId = it.instanceId,
            upgradeLevel = it.weaponData != null ? WeaponUpgradeRules.ClampLevel(it.weaponData, it.upgradeLevel) : 0,
            amount = Mathf.Max(1, it.amount),
            title = it.title,
            description = it.description
        };
    }

    public InventoryItem RestoreInventoryItemFromSaveData(SavedInventoryItemData saved)
    {
        if (saved == null) return null;
        InventoryItem restored = DeserializeInventoryItem(saved, BuildAssetLookups());
        return restored;
    }

    public ItemData ResolveItemDataByAssetName(string assetName)
    {
        return ResolveItem(assetName, BuildAssetLookups().items);
    }

    public void ApplySaveData(SavedPlayerInventoryData data)
    {
        if (data == null) return;

        EnsureLoadoutSize();

        EnsureItemDatabaseAssigned();
        var lookups = BuildAssetLookups();

        // null = vecchio salvataggio senza il campo inventario: mantieni il fallback iniziale.
        // Array vuoto = inventario realmente vuoto: deve cancellare lo startingLoadout.
        if (data.items != null)
        {
            var fallbackItems = new List<InventoryItem>(items);
            items.Clear();
            for (int i = 0; i < data.items.Length; i++)
            {
                var saved = data.items[i];
                if (saved == null) continue;

                var restored = DeserializeInventoryItem(saved, lookups);
                if (restored != null) items.Add(restored);
            }

            if (data.items.Length > 0 && items.Count == 0 && fallbackItems.Count > 0)
            {
                items.AddRange(fallbackItems);
                Debug.LogWarning("[PlayerInventory] Save inventory presente ma non ripristinabile con i lookup correnti. Mantengo lo startingLoadout runtime.");
            }
        }
        // Fallback solo per salvataggi legacy con data.items == null.

        // Se il save contiene slot, questi sovrascrivono lo startingLoadout anche quando sono vuoti.
        if (HasSavedLoadoutSlots(data.rightLoadout))
            DeserializeWeaponLoadout(data.rightLoadout, rightLoadout, rightInstanceIds, lookups.weapons);
        if (HasSavedLoadoutSlots(data.leftLoadout))
            DeserializeWeaponLoadout(data.leftLoadout, leftLoadout, leftInstanceIds, lookups.weapons);
        if (HasSavedLoadoutSlots(data.magicLoadout))
            DeserializeMagicLoadout(data.magicLoadout, magicLoadout, magicInstanceIds, lookups.magics);
        if (HasSavedLoadoutSlots(data.usableLoadout))
            DeserializeUsableLoadout(data.usableLoadout, usableLoadout, usableInstanceIds, lookups.usables);
        if (HasSavedLoadoutSlots(data.armorLoadout))
            DeserializeArmorLoadout(data.armorLoadout, armorLoadout, armorInstanceIds, lookups.armors);
        EnsureLoadoutInstancesInInventory();

        currentRightIndex = Mathf.Clamp(data.currentRightIndex, 0, rightLoadout.Length - 1);
        currentLeftIndex = Mathf.Clamp(data.currentLeftIndex, 0, leftLoadout.Length - 1);
        currentMagicIndex = Mathf.Clamp(data.currentMagicIndex, 0, magicLoadout.Length - 1);
        currentUsableIndex = Mathf.Clamp(data.currentUsableIndex, 0, usableLoadout.Length - 1);
        SyncMagicInventorySlots();
        SyncEquippedReferences();
    }

    private SavedInventoryItemData[] SerializeInventoryItems()
    {
        if (items == null || items.Count == 0) return System.Array.Empty<SavedInventoryItemData>();

        var result = new SavedInventoryItemData[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it == null) continue;

            string itemType = "item";
            string assetName = string.Empty;
            string itemName = string.Empty;

            if (it.weaponData != null)
            {
                itemType = "weapon";
                assetName = it.weaponData.name;
                itemName = it.weaponData.weaponName;
            }
            else if (it.magicData != null)
            {
                itemType = "magic";
                assetName = it.magicData.name;
                itemName = it.magicData.magicName;
            }
            else if (it.armorData != null)
            {
                itemType = "armor";
                assetName = it.armorData.name;
                itemName = it.armorData.itemName;
            }
            else if (it.usableData != null)
            {
                itemType = "usable";
                assetName = it.usableData.name;
                itemName = it.usableData.itemName;
            }
            else if (it.itemData != null)
            {
                itemType = "item";
                assetName = it.itemData.name;
                itemName = it.itemData.itemName;
            }

            result[i] = new SavedInventoryItemData
            {
                itemType = itemType,
                assetName = assetName,
                itemName = string.IsNullOrWhiteSpace(itemName) ? it.title : itemName,
                instanceId = it.instanceId,
                upgradeLevel = it.weaponData != null ? WeaponUpgradeRules.ClampLevel(it.weaponData, it.upgradeLevel) : 0,
                amount = Mathf.Max(1, it.amount),
                title = it.title,
                description = it.description
            };
        }

        return result;
    }

    private SavedLoadoutSlotData[] SerializeWeaponLoadout(WeaponItem[] loadout, string[] ids)
    {
        if (loadout == null || ids == null || loadout.Length == 0) return System.Array.Empty<SavedLoadoutSlotData>();
        var result = new SavedLoadoutSlotData[loadout.Length];

        for (int i = 0; i < loadout.Length; i++)
        {
            var w = loadout[i];
            result[i] = new SavedLoadoutSlotData
            {
                assetName = w != null ? w.name : string.Empty,
                instanceId = ids != null && i < ids.Length ? ids[i] : string.Empty
            };
        }

        return result;
    }

    private SavedLoadoutSlotData[] SerializeUsableLoadout(UsableItemData[] loadout, string[] ids)
    {
        if (loadout == null || ids == null || loadout.Length == 0) return System.Array.Empty<SavedLoadoutSlotData>();
        var result = new SavedLoadoutSlotData[loadout.Length];

        for (int i = 0; i < loadout.Length; i++)
        {
            var u = loadout[i];
            result[i] = new SavedLoadoutSlotData
            {
                assetName = u != null ? u.name : string.Empty,
                instanceId = ids != null && i < ids.Length ? ids[i] : string.Empty
            };
        }

        return result;
    }

    private SavedLoadoutSlotData[] SerializeMagicLoadout(MagicItemData[] loadout, string[] ids)
    {
        if (loadout == null || ids == null || loadout.Length == 0) return System.Array.Empty<SavedLoadoutSlotData>();
        var result = new SavedLoadoutSlotData[loadout.Length];

        for (int i = 0; i < loadout.Length; i++)
        {
            var m = loadout[i];
            result[i] = new SavedLoadoutSlotData
            {
                assetName = m != null ? m.name : string.Empty,
                instanceId = ids != null && i < ids.Length ? ids[i] : string.Empty
            };
        }

        return result;
    }

    private SavedLoadoutSlotData[] SerializeArmorLoadout(ArmorItemData[] loadout, string[] ids)
    {
        if (loadout == null || ids == null || loadout.Length == 0) return System.Array.Empty<SavedLoadoutSlotData>();
        var result = new SavedLoadoutSlotData[loadout.Length];

        for (int i = 0; i < loadout.Length; i++)
        {
            var armor = loadout[i];
            result[i] = new SavedLoadoutSlotData
            {
                assetName = armor != null ? armor.name : string.Empty,
                instanceId = ids != null && i < ids.Length ? ids[i] : string.Empty
            };
        }

        return result;
    }

    private void DeserializeWeaponLoadout(SavedLoadoutSlotData[] source, WeaponItem[] targetLoadout, string[] targetIds, Dictionary<string, WeaponItem> weaponLookup)
    {
        for (int i = 0; i < targetLoadout.Length; i++)
        {
            targetLoadout[i] = null;
            targetIds[i] = null;
        }
        if (source == null || weaponLookup == null) return;

        int len = Mathf.Min(source.Length, targetLoadout.Length);
        for (int i = 0; i < len; i++)
        {
            var slot = source[i];
            if (slot == null) continue;
            targetLoadout[i] = ResolveWeapon(slot.assetName, weaponLookup);
            targetIds[i] = string.IsNullOrWhiteSpace(slot.instanceId) ? null : slot.instanceId;
        }
    }

    private void DeserializeUsableLoadout(SavedLoadoutSlotData[] source, UsableItemData[] targetLoadout, string[] targetIds, Dictionary<string, UsableItemData> usableLookup)
    {
        for (int i = 0; i < targetLoadout.Length; i++)
        {
            targetLoadout[i] = null;
            targetIds[i] = null;
        }
        if (source == null || usableLookup == null) return;

        int len = Mathf.Min(source.Length, targetLoadout.Length);
        for (int i = 0; i < len; i++)
        {
            var slot = source[i];
            if (slot == null) continue;
            targetLoadout[i] = ResolveUsable(slot.assetName, usableLookup);
            targetIds[i] = string.IsNullOrWhiteSpace(slot.instanceId) ? null : slot.instanceId;
        }
    }

    private void DeserializeMagicLoadout(SavedLoadoutSlotData[] source, MagicItemData[] targetLoadout, string[] targetIds, Dictionary<string, MagicItemData> magicLookup)
    {
        for (int i = 0; i < targetLoadout.Length; i++)
        {
            targetLoadout[i] = null;
            targetIds[i] = null;
        }
        if (source == null || magicLookup == null) return;

        int len = Mathf.Min(source.Length, targetLoadout.Length);
        for (int i = 0; i < len; i++)
        {
            var slot = source[i];
            if (slot == null) continue;
            targetLoadout[i] = ResolveMagic(slot.assetName, magicLookup);
            targetIds[i] = string.IsNullOrWhiteSpace(slot.instanceId) ? null : slot.instanceId;
        }
    }

    private void DeserializeArmorLoadout(SavedLoadoutSlotData[] source, ArmorItemData[] targetLoadout, string[] targetIds, Dictionary<string, ArmorItemData> armorLookup)
    {
        for (int i = 0; i < targetLoadout.Length; i++)
        {
            targetLoadout[i] = null;
            targetIds[i] = null;
        }
        if (source == null || armorLookup == null) return;

        int len = Mathf.Min(source.Length, targetLoadout.Length);
        for (int i = 0; i < len; i++)
        {
            var slot = source[i];
            if (slot == null) continue;
            targetLoadout[i] = ResolveArmor(slot.assetName, armorLookup);
            targetIds[i] = string.IsNullOrWhiteSpace(slot.instanceId) ? null : slot.instanceId;
        }
    }

    private InventoryItem DeserializeInventoryItem(SavedInventoryItemData saved, (Dictionary<string, WeaponItem> weapons, Dictionary<string, MagicItemData> magics, Dictionary<string, ArmorItemData> armors, Dictionary<string, UsableItemData> usables, Dictionary<string, ItemData> items) lookups)
    {
        if (saved == null) return null;
        string type = string.IsNullOrWhiteSpace(saved.itemType) ? "item" : saved.itemType.Trim().ToLowerInvariant();
        InventoryItem restored;

        if (type == "weapon")
        {
            var weapon = ResolveWeapon(saved.assetName, lookups.weapons);
            if (weapon == null) return null;
            restored = new InventoryItem(weapon, 1, saved.title, saved.description);
            restored.amount = 1;
        }
        else if (type == "magic")
        {
            var magic = ResolveMagic(saved.assetName, lookups.magics);
            if (magic == null) return null;
            restored = new InventoryItem(magic, Mathf.Max(1, saved.amount), saved.title, saved.description);
        }
        else if (type == "armor")
        {
            var armor = ResolveArmor(saved.assetName, lookups.armors);
            if (armor == null) return null;
            restored = new InventoryItem(armor, Mathf.Max(1, saved.amount), saved.title, saved.description);
        }
        else if (type == "usable")
        {
            var usable = ResolveUsable(saved.assetName, lookups.usables);
            if (usable == null) return null;
            restored = new InventoryItem(usable, Mathf.Max(1, saved.amount), saved.title, saved.description);
        }
        else
        {
            var item = ResolveItem(saved.assetName, lookups.items);
            if (item == null) return null;
            restored = new InventoryItem(item, Mathf.Max(1, saved.amount), saved.title, saved.description);
        }

        restored.instanceId = string.IsNullOrWhiteSpace(saved.instanceId) ? restored.instanceId : saved.instanceId;
        if (restored.weaponData != null)
            restored.upgradeLevel = WeaponUpgradeRules.ClampLevel(restored.weaponData, saved.upgradeLevel);
        if (!string.IsNullOrWhiteSpace(saved.title)) restored.title = saved.title;
        if (!string.IsNullOrWhiteSpace(saved.description)) restored.description = saved.description;
        return restored;
    }

    private static WeaponItem ResolveWeapon(string assetName, Dictionary<string, WeaponItem> lookup)
    {
        if (lookup == null || string.IsNullOrWhiteSpace(assetName)) return null;
        lookup.TryGetValue(assetName.Trim().ToLowerInvariant(), out var result);
        return result;
    }

    private static UsableItemData ResolveUsable(string assetName, Dictionary<string, UsableItemData> lookup)
    {
        if (lookup == null || string.IsNullOrWhiteSpace(assetName)) return null;
        lookup.TryGetValue(assetName.Trim().ToLowerInvariant(), out var result);
        return result;
    }

    private static ItemData ResolveItem(string assetName, Dictionary<string, ItemData> lookup)
    {
        if (lookup == null || string.IsNullOrWhiteSpace(assetName)) return null;
        lookup.TryGetValue(assetName.Trim().ToLowerInvariant(), out var result);
        return result;
    }

    private static MagicItemData ResolveMagic(string assetName, Dictionary<string, MagicItemData> lookup)
    {
        if (lookup == null || string.IsNullOrWhiteSpace(assetName)) return null;
        lookup.TryGetValue(assetName.Trim().ToLowerInvariant(), out var result);
        return result;
    }

    private static ArmorItemData ResolveArmor(string assetName, Dictionary<string, ArmorItemData> lookup)
    {
        if (lookup == null || string.IsNullOrWhiteSpace(assetName)) return null;
        lookup.TryGetValue(assetName.Trim().ToLowerInvariant(), out var result);
        return result;
    }

    private void EnsureItemDatabaseAssigned()
    {
        if (itemDatabase == null)
        {
            Debug.LogWarning("[PlayerInventory] ItemDatabase non assegnato in Inspector: il restore del save non e' affidabile.");
        }
    }

    private (Dictionary<string, WeaponItem> weapons, Dictionary<string, MagicItemData> magics, Dictionary<string, ArmorItemData> armors, Dictionary<string, UsableItemData> usables, Dictionary<string, ItemData> items) BuildAssetLookups()
    {
        if (itemDatabase != null && cachedLookupDatabase == itemDatabase && cachedAssetLookups.weapons != null)
            return cachedAssetLookups;

        var weaponLookup = new Dictionary<string, WeaponItem>();
        var magicLookup = new Dictionary<string, MagicItemData>();
        var armorLookup = new Dictionary<string, ArmorItemData>();
        var usableLookup = new Dictionary<string, UsableItemData>();
        var itemLookup = new Dictionary<string, ItemData>();

        if (itemDatabase != null)
        {
            RegisterWeapons(weaponLookup, itemDatabase.BuildFlatWeaponList());
            RegisterMagics(magicLookup, itemDatabase.magics);
            RegisterArmors(armorLookup, itemDatabase.armors);
            RegisterUsables(usableLookup, itemDatabase.usables);
            RegisterItems(itemLookup, itemDatabase.items);
        }

        cachedLookupDatabase = itemDatabase;
        cachedAssetLookups = (weaponLookup, magicLookup, armorLookup, usableLookup, itemLookup);
        return cachedAssetLookups;
    }

    private static void RegisterWeapons(Dictionary<string, WeaponItem> lookup, WeaponItem[] source)
    {
        if (lookup == null || source == null) return;
        for (int i = 0; i < source.Length; i++)
        {
            var w = source[i];
            if (w == null || string.IsNullOrWhiteSpace(w.name)) continue;
            string key = w.name.Trim().ToLowerInvariant();
            if (!lookup.ContainsKey(key)) lookup.Add(key, w);
        }
    }

    private static void RegisterUsables(Dictionary<string, UsableItemData> lookup, UsableItemData[] source)
    {
        if (lookup == null || source == null) return;
        for (int i = 0; i < source.Length; i++)
        {
            var u = source[i];
            if (u == null || string.IsNullOrWhiteSpace(u.name)) continue;
            string key = u.name.Trim().ToLowerInvariant();
            if (!lookup.ContainsKey(key)) lookup.Add(key, u);
        }
    }

    private static void RegisterMagics(Dictionary<string, MagicItemData> lookup, MagicItemData[] source)
    {
        if (lookup == null || source == null) return;
        for (int i = 0; i < source.Length; i++)
        {
            var m = source[i];
            if (m == null || string.IsNullOrWhiteSpace(m.name)) continue;
            string key = m.name.Trim().ToLowerInvariant();
            if (!lookup.ContainsKey(key)) lookup.Add(key, m);
        }
    }

    private static void RegisterArmors(Dictionary<string, ArmorItemData> lookup, ArmorItemData[] source)
    {
        if (lookup == null || source == null) return;
        for (int i = 0; i < source.Length; i++)
        {
            var a = source[i];
            if (a == null || string.IsNullOrWhiteSpace(a.name)) continue;
            string key = a.name.Trim().ToLowerInvariant();
            if (!lookup.ContainsKey(key)) lookup.Add(key, a);
        }
    }

    private static void RegisterItems(Dictionary<string, ItemData> lookup, ItemData[] source)
    {
        if (lookup == null || source == null) return;
        for (int i = 0; i < source.Length; i++)
        {
            var it = source[i];
            if (it == null || string.IsNullOrWhiteSpace(it.name)) continue;
            string key = it.name.Trim().ToLowerInvariant();
            if (!lookup.ContainsKey(key)) lookup.Add(key, it);
        }
    }

    private static void RegisterWeapons(Dictionary<string, WeaponItem> lookup, List<WeaponItem> source)
    {
        if (lookup == null || source == null) return;
        for (int i = 0; i < source.Count; i++)
        {
            var w = source[i];
            if (w == null || string.IsNullOrWhiteSpace(w.name)) continue;
            string key = w.name.Trim().ToLowerInvariant();
            if (!lookup.ContainsKey(key)) lookup.Add(key, w);
        }
    }

    private static void RegisterUsables(Dictionary<string, UsableItemData> lookup, List<UsableItemData> source)
    {
        if (lookup == null || source == null) return;
        for (int i = 0; i < source.Count; i++)
        {
            var u = source[i];
            if (u == null || string.IsNullOrWhiteSpace(u.name)) continue;
            string key = u.name.Trim().ToLowerInvariant();
            if (!lookup.ContainsKey(key)) lookup.Add(key, u);
        }
    }

    private static void RegisterMagics(Dictionary<string, MagicItemData> lookup, List<MagicItemData> source)
    {
        if (lookup == null || source == null) return;
        for (int i = 0; i < source.Count; i++)
        {
            var m = source[i];
            if (m == null || string.IsNullOrWhiteSpace(m.name)) continue;
            string key = m.name.Trim().ToLowerInvariant();
            if (!lookup.ContainsKey(key)) lookup.Add(key, m);
        }
    }

    private static void RegisterArmors(Dictionary<string, ArmorItemData> lookup, List<ArmorItemData> source)
    {
        if (lookup == null || source == null) return;
        for (int i = 0; i < source.Count; i++)
        {
            var a = source[i];
            if (a == null || string.IsNullOrWhiteSpace(a.name)) continue;
            string key = a.name.Trim().ToLowerInvariant();
            if (!lookup.ContainsKey(key)) lookup.Add(key, a);
        }
    }

    private static void RegisterItems(Dictionary<string, ItemData> lookup, List<ItemData> source)
    {
        if (lookup == null || source == null) return;
        for (int i = 0; i < source.Count; i++)
        {
            var it = source[i];
            if (it == null || string.IsNullOrWhiteSpace(it.name)) continue;
            string key = it.name.Trim().ToLowerInvariant();
            if (!lookup.ContainsKey(key)) lookup.Add(key, it);
        }
    }

    private static bool HasSavedLoadoutSlots(SavedLoadoutSlotData[] source)
    {
        return source != null && source.Length > 0;
    }

    private void EnsureLoadoutInstancesInInventory()
    {
        EnsureLoadoutSize();

        EnsureWeaponLoadoutInInventory(rightLoadout, rightInstanceIds);
        EnsureWeaponLoadoutInInventory(leftLoadout, leftInstanceIds);
        EnsureMagicLoadoutInInventory(magicLoadout, magicInstanceIds);
        EnsureUsableLoadoutInInventory(usableLoadout, usableInstanceIds);
        EnsureArmorLoadoutInInventory(armorLoadout, armorInstanceIds);
    }

    private void EnsureWeaponLoadoutInInventory(WeaponItem[] loadout, string[] ids)
    {
        if (loadout == null || ids == null) return;
        int len = Mathf.Min(loadout.Length, ids.Length);
        for (int i = 0; i < len; i++)
        {
            var weapon = loadout[i];
            string instanceId = ids[i];
            if (weapon == null || string.IsNullOrWhiteSpace(instanceId)) continue;
            if (HasWeaponInstanceInInventory(instanceId, weapon)) continue;

            var restored = new InventoryItem(weapon, 1);
            restored.instanceId = instanceId;
            items.Add(restored);
        }
    }

    private void EnsureUsableLoadoutInInventory(UsableItemData[] loadout, string[] ids)
    {
        if (loadout == null || ids == null) return;
        int len = Mathf.Min(loadout.Length, ids.Length);
        for (int i = 0; i < len; i++)
        {
            var usable = loadout[i];
            string instanceId = ids[i];
            if (usable == null || string.IsNullOrWhiteSpace(instanceId)) continue;
            if (HasUsableInstanceInInventory(instanceId, usable)) continue;

            var restored = new InventoryItem(usable, 1);
            restored.instanceId = instanceId;
            items.Add(restored);
        }
    }

    private void EnsureMagicLoadoutInInventory(MagicItemData[] loadout, string[] ids)
    {
        if (loadout == null || ids == null) return;
        int len = Mathf.Min(loadout.Length, ids.Length);
        for (int i = 0; i < len; i++)
        {
            var magic = loadout[i];
            string instanceId = ids[i];
            if (magic == null || string.IsNullOrWhiteSpace(instanceId)) continue;
            if (HasMagicInstanceInInventory(instanceId, magic)) continue;

            var restored = new InventoryItem(magic, 1);
            restored.instanceId = instanceId;
            items.Add(restored);
        }
    }

    private void EnsureArmorLoadoutInInventory(ArmorItemData[] loadout, string[] ids)
    {
        if (loadout == null || ids == null) return;
        int len = Mathf.Min(loadout.Length, ids.Length);
        for (int i = 0; i < len; i++)
        {
            var armor = loadout[i];
            string instanceId = ids[i];
            if (armor == null || string.IsNullOrWhiteSpace(instanceId)) continue;
            if (HasArmorInstanceInInventory(instanceId, armor)) continue;

            var restored = new InventoryItem(armor, 1);
            restored.instanceId = instanceId;
            items.Add(restored);
        }
    }

    private static int ArmorSlotToIndex(ArmorItemData.ArmorSlot slot)
    {
        switch (slot)
        {
            case ArmorItemData.ArmorSlot.Helmet: return 0;
            case ArmorItemData.ArmorSlot.Chestplate: return 1;
            case ArmorItemData.ArmorSlot.Leggings: return 2;
            case ArmorItemData.ArmorSlot.Boots: return 3;
            default: return -1;
        }
    }
}

public enum Hand { Right, Left }
public enum AttackType { Light, Heavy }
