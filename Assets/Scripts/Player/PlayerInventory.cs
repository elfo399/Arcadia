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
    public UsableItemData[] usableLoadout = new UsableItemData[3];
    private string[] rightInstanceIds = new string[3];
    private string[] leftInstanceIds = new string[3];
    private string[] usableInstanceIds = new string[3];
    public int currentRightIndex = 0;
    public int currentLeftIndex = 0;
    public int currentUsableIndex = 0;

    [Header("Runtime Equipped (Debug)")]
    [SerializeField] private WeaponItem equippedRightRuntime;
    [SerializeField] private WeaponItem equippedLeftRuntime;
    [SerializeField] private UsableItemData equippedUsableRuntime;

    [System.Serializable]
    public class StartingItemEntry
    {
        public WeaponItem weapon;
        public ItemData item;
        public UsableItemData usable;
        public int quantity = 1;
    }

    [Header("Item iniziali (per test)")]
    [SerializeField] private List<StartingItemEntry> startingLoadout = new();

    private readonly List<InventoryItem> items = new();
    public IReadOnlyList<InventoryItem> Items => items;

    void Awake()
    {
        items.Clear();
        foreach (var entry in startingLoadout)
        {
            if (entry == null) continue;
            int qty = Mathf.Max(1, entry.quantity);
            int assigned = (entry.weapon != null ? 1 : 0) + (entry.item != null ? 1 : 0) + (entry.usable != null ? 1 : 0);
            if (assigned == 0) continue;
            if (assigned > 1) Debug.LogWarning("[PlayerInventory] Starting item entry ha più di un campo settato. Priorità: Weapon > Usable > Item.");

            if (entry.weapon != null)
            {
                for (int i = 0; i < qty; i++) items.Add(new InventoryItem(entry.weapon, 1));
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
        currentUsableIndex = SelectInitialIndex(usableLoadout);
        SyncEquippedReferences();
    }

    private void EnsureLoadoutSize()
    {
        if (rightLoadout == null || rightLoadout.Length != 3) rightLoadout = new WeaponItem[3];
        if (leftLoadout == null || leftLoadout.Length != 3) leftLoadout = new WeaponItem[3];
        if (usableLoadout == null || usableLoadout.Length != 3) usableLoadout = new UsableItemData[3];
        if (rightInstanceIds == null || rightInstanceIds.Length != 3) rightInstanceIds = new string[3];
        if (leftInstanceIds == null || leftInstanceIds.Length != 3) leftInstanceIds = new string[3];
        if (usableInstanceIds == null || usableInstanceIds.Length != 3) usableInstanceIds = new string[3];
    }

    public WeaponItem GetWeaponForHand(Hand hand)
    {
        WeaponItem equipped = (hand == Hand.Right) ? GetCurrentRightWeapon() : GetCurrentLeftWeapon();
        return equipped != null ? equipped : (hand == Hand.Right ? unarmedRight : unarmedLeft);
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

    public bool CycleRightWeapon(int direction = 1)
    {
        EnsureLoadoutSize();
        return CycleWeaponInternal(rightLoadout, ref currentRightIndex, direction);
    }

    public bool CycleLeftWeapon(int direction = 1)
    {
        EnsureLoadoutSize();
        return CycleWeaponInternal(leftLoadout, ref currentLeftIndex, direction);
    }

    public bool CycleUsable(int direction = 1)
    {
        EnsureLoadoutSize();
        return CycleUsableInternal(usableLoadout, ref currentUsableIndex, direction);
    }

    public bool IsInstanceEquipped(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId)) return false;
        EnsureLoadoutSize();
        return ContainsInstanceId(rightInstanceIds, instanceId)
               || ContainsInstanceId(leftInstanceIds, instanceId)
               || ContainsInstanceId(usableInstanceIds, instanceId);
    }

    // Inventory management
    public void AddItem(InventoryItem item) { if (item != null) items.Add(item); }
    public bool RemoveItem(InventoryItem item) { return items.Remove(item); }
    public void ClearItems() { items.Clear(); }
    public void ReplaceAllItems(List<InventoryItem> newItems)
    {
        items.Clear();
        if (newItems != null) items.AddRange(newItems);
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

    private bool RemoveWeaponInstanceFromInventory(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId)) return false;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it != null && it.weaponData != null && it.instanceId == instanceId)
            {
                items.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    private void AddWeaponInstanceToInventory(string instanceId, WeaponItem weapon)
    {
        if (weapon == null) return;
        var inv = new InventoryItem(weapon, 1);
        if (!string.IsNullOrEmpty(instanceId)) inv.instanceId = instanceId;
        items.Add(inv);
    }

    private bool RemoveUsableInstanceFromInventory(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId)) return false;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it != null && it.usableData != null && it.instanceId == instanceId)
            {
                if (it.amount > 1) it.amount -= 1;
                else items.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    private void AddUsableInstanceToInventory(string instanceId, UsableItemData usable)
    {
        if (usable == null) return;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it != null && it.usableData == usable && it.instanceId == instanceId)
            {
                it.amount += 1;
                return;
            }
        }
        var inv = new InventoryItem(usable, 1);
        if (!string.IsNullOrEmpty(instanceId)) inv.instanceId = instanceId;
        items.Add(inv);
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

    private bool CycleWeaponInternal(WeaponItem[] loadout, ref int currentIndex, int direction)
    {
        if (loadout == null || loadout.Length == 0) return false;

        int dir = direction >= 0 ? 1 : -1;
        currentIndex = (Mathf.Clamp(currentIndex, 0, loadout.Length - 1) + dir + loadout.Length) % loadout.Length;
        SyncEquippedReferences();
        return true;
    }

    private bool CycleUsableInternal(UsableItemData[] loadout, ref int currentIndex, int direction)
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
        currentUsableIndex = Mathf.Clamp(currentUsableIndex, 0, usableLoadout.Length - 1);

        var right = rightLoadout[currentRightIndex];
        var left = leftLoadout[currentLeftIndex];
        var usable = usableLoadout[currentUsableIndex];
        equippedRightRuntime = right != null ? right : unarmedRight;
        equippedLeftRuntime = left != null ? left : unarmedLeft;
        equippedUsableRuntime = usable;
    }

    public SavedPlayerInventoryData CreateSaveData()
    {
        EnsureLoadoutSize();

        var data = new SavedPlayerInventoryData
        {
            items = SerializeInventoryItems(),
            rightLoadout = SerializeWeaponLoadout(rightLoadout, rightInstanceIds),
            leftLoadout = SerializeWeaponLoadout(leftLoadout, leftInstanceIds),
            usableLoadout = SerializeUsableLoadout(usableLoadout, usableInstanceIds),
            currentRightIndex = currentRightIndex,
            currentLeftIndex = currentLeftIndex,
            currentUsableIndex = currentUsableIndex
        };

        return data;
    }

    public void ApplySaveData(SavedPlayerInventoryData data)
    {
        if (data == null) return;

        EnsureLoadoutSize();

        var lookups = BuildAssetLookups();

        items.Clear();
        if (data.items != null)
        {
            for (int i = 0; i < data.items.Length; i++)
            {
                var saved = data.items[i];
                if (saved == null) continue;

                var restored = DeserializeInventoryItem(saved, lookups);
                if (restored != null) items.Add(restored);
            }
        }

        DeserializeWeaponLoadout(data.rightLoadout, rightLoadout, rightInstanceIds, lookups.weapons);
        DeserializeWeaponLoadout(data.leftLoadout, leftLoadout, leftInstanceIds, lookups.weapons);
        DeserializeUsableLoadout(data.usableLoadout, usableLoadout, usableInstanceIds, lookups.usables);

        currentRightIndex = Mathf.Clamp(data.currentRightIndex, 0, rightLoadout.Length - 1);
        currentLeftIndex = Mathf.Clamp(data.currentLeftIndex, 0, leftLoadout.Length - 1);
        currentUsableIndex = Mathf.Clamp(data.currentUsableIndex, 0, usableLoadout.Length - 1);
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

    private InventoryItem DeserializeInventoryItem(SavedInventoryItemData saved, (Dictionary<string, WeaponItem> weapons, Dictionary<string, UsableItemData> usables, Dictionary<string, ItemData> items) lookups)
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

    private static (Dictionary<string, WeaponItem> weapons, Dictionary<string, UsableItemData> usables, Dictionary<string, ItemData> items) BuildAssetLookups()
    {
        var weaponLookup = new Dictionary<string, WeaponItem>();
        var usableLookup = new Dictionary<string, UsableItemData>();
        var itemLookup = new Dictionary<string, ItemData>();

        RegisterWeapons(weaponLookup, Resources.LoadAll<WeaponItem>(""));
        RegisterUsables(usableLookup, Resources.LoadAll<UsableItemData>(""));
        RegisterItems(itemLookup, Resources.LoadAll<ItemData>(""));

        RegisterWeapons(weaponLookup, Resources.FindObjectsOfTypeAll<WeaponItem>());
        RegisterUsables(usableLookup, Resources.FindObjectsOfTypeAll<UsableItemData>());
        RegisterItems(itemLookup, Resources.FindObjectsOfTypeAll<ItemData>());

        return (weaponLookup, usableLookup, itemLookup);
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
}

public enum Hand { Right, Left }
public enum AttackType { Light, Heavy }
