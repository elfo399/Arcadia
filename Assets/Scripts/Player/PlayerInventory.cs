using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Gestisce equip (3 slot per lato), usabili e inventario base.
/// Usa instanceId per distinguere copie identiche; una singola istanza non può stare in più slot.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
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

    private readonly List<InventoryItem> items = new();
    private readonly List<InventoryItem> magicInventorySlots = new();
    public IReadOnlyList<InventoryItem> Items => items;
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

    public void AddWeaponInstance(WeaponItem weapon, string instanceId)
    {
        if (weapon == null || string.IsNullOrWhiteSpace(instanceId)) return;
        if (HasWeaponInstanceInInventory(instanceId, weapon)) return;

        var restored = new InventoryItem(weapon, 1);
        restored.instanceId = instanceId;
        items.Add(restored);
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
        if (itemData == null) return 0;
        int total = 0;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it == null || it.itemData != itemData) continue;
            total += Mathf.Max(0, it.amount);
        }
        return total;
    }

    public bool TryConsumeItem(ItemData itemData, int amount, out int remainingTotal)
    {
        remainingTotal = 0;
        if (itemData == null) return false;
        int toConsume = Mathf.Max(1, amount);

        for (int i = items.Count - 1; i >= 0 && toConsume > 0; i--)
        {
            var it = items[i];
            if (it == null || it.itemData != itemData) continue;

            int stack = Mathf.Max(0, it.amount);
            if (stack <= 0)
            {
                items.RemoveAt(i);
                continue;
            }

            int used = Mathf.Min(stack, toConsume);
            it.amount = stack - used;
            toConsume -= used;

            if (it.amount <= 0)
                items.RemoveAt(i);
        }

        remainingTotal = GetTotalItemAmount(itemData);
        return toConsume == 0;
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
    public void AddItem(InventoryItem item) { if (item != null) items.Add(item); }
    public void AddWeaponLoot(WeaponItem weapon, int amount = 1)
    {
        if (weapon == null || amount <= 0)
            return;

        for (int i = 0; i < amount; i++)
            items.Add(new InventoryItem(weapon, 1));
    }

    public void AddArmorLoot(ArmorItemData armor, int amount = 1)
    {
        if (armor == null || amount <= 0)
            return;

        for (int i = 0; i < amount; i++)
            items.Add(new InventoryItem(armor, 1));
    }

    public void AddMagicLoot(MagicItemData magic, int amount = 1)
    {
        if (magic == null || amount <= 0)
            return;

        InventoryItem existing = FindStackableMagicItem(magic);
        if (existing != null)
        {
            existing.amount += amount;
            return;
        }

        items.Add(new InventoryItem(magic, amount));
    }

    public void AddUsableLoot(UsableItemData usable, int amount = 1)
    {
        if (usable == null || amount <= 0)
            return;

        InventoryItem existing = FindStackableUsableItem(usable);
        if (existing != null)
        {
            existing.amount += amount;
            return;
        }

        items.Add(new InventoryItem(usable, amount));
    }

    public void AddGenericItemLoot(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0)
            return;

        InventoryItem existing = FindStackableGenericItem(item);
        if (existing != null)
        {
            existing.amount += amount;
            return;
        }

        items.Add(new InventoryItem(item, amount));
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

    public void ApplySaveData(SavedPlayerInventoryData data)
    {
        if (data == null) return;

        EnsureLoadoutSize();

        EnsureItemDatabaseAssigned();
        var lookups = BuildAssetLookups();

        bool hasSavedItems = data.items != null && data.items.Length > 0;
        if (hasSavedItems)
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

            if (items.Count == 0 && fallbackItems.Count > 0)
            {
                items.AddRange(fallbackItems);
                Debug.LogWarning("[PlayerInventory] Save inventory presente ma non ripristinabile con i lookup correnti. Mantengo lo startingLoadout runtime.");
            }
        }
        // Fallback: se il save non contiene item, mantieni quelli iniziali (startingLoadout).

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
        if (itemDatabase != null) return;
        itemDatabase = Resources.Load<ItemDatabase>("ItemDatabase");
        if (itemDatabase == null)
        {
            Debug.LogWarning("[PlayerInventory] ItemDatabase non assegnato. Assegna un ItemDatabase in Inspector (o mettilo in Resources/ItemDatabase.asset) per un restore save affidabile.");
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
