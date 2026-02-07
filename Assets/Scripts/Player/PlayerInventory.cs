using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestisce equip (3 slot per lato), usabili e inventario base.
/// Usa instanceId per distinguere copie identiche; una singola istanza non può stare in più slot.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    [Header("Armi equipaggiate")]
    public WeaponItem rightHandWeapon;
    public WeaponItem leftHandWeapon;

    [Header("Default unarmed")]
    public WeaponItem unarmedRight;
    public WeaponItem unarmedLeft;

    [Header("Usable equip")]
    public UsableItemData equippedUsable;

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
        rightLoadout[0] = rightHandWeapon;
        leftLoadout[0] = leftHandWeapon;
        usableLoadout[0] = equippedUsable;
        currentRightIndex = Mathf.Clamp(currentRightIndex, 0, rightLoadout.Length - 1);
        currentLeftIndex = Mathf.Clamp(currentLeftIndex, 0, leftLoadout.Length - 1);
        currentUsableIndex = Mathf.Clamp(currentUsableIndex, 0, usableLoadout.Length - 1);
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
        return rightLoadout[currentRightIndex] != null ? rightLoadout[currentRightIndex] : rightHandWeapon;
    }

    public WeaponItem GetCurrentLeftWeapon()
    {
        EnsureLoadoutSize();
        currentLeftIndex = Mathf.Clamp(currentLeftIndex, 0, leftLoadout.Length - 1);
        return leftLoadout[currentLeftIndex] != null ? leftLoadout[currentLeftIndex] : leftHandWeapon;
    }

    public UsableItemData GetCurrentUsable()
    {
        EnsureLoadoutSize();
        currentUsableIndex = Mathf.Clamp(currentUsableIndex, 0, usableLoadout.Length - 1);
        return usableLoadout[currentUsableIndex] != null ? usableLoadout[currentUsableIndex] : equippedUsable;
    }

    public void SetRightAtSlot(int slot, WeaponItem weapon, string instanceId)
    {
        EnsureLoadoutSize();
        slot = Mathf.Clamp(slot, 0, rightLoadout.Length - 1);
        rightHandWeapon = MoveWeaponWithInventorySync(weapon, instanceId, rightLoadout, rightInstanceIds, slot, leftLoadout, leftInstanceIds);
        currentRightIndex = slot;
    }

    public void SetLeftAtSlot(int slot, WeaponItem weapon, string instanceId)
    {
        EnsureLoadoutSize();
        slot = Mathf.Clamp(slot, 0, leftLoadout.Length - 1);
        leftHandWeapon = MoveWeaponWithInventorySync(weapon, instanceId, leftLoadout, leftInstanceIds, slot, rightLoadout, rightInstanceIds);
        currentLeftIndex = slot;
    }

    public void SetUsableAtSlot(int slot, UsableItemData usable, string instanceId)
    {
        EnsureLoadoutSize();
        slot = Mathf.Clamp(slot, 0, usableLoadout.Length - 1);
        equippedUsable = MoveUsableWithInventorySync(usable, instanceId, usableLoadout, usableInstanceIds, slot);
        currentUsableIndex = slot;
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
}

public enum Hand { Right, Left }
public enum AttackType { Light, Heavy }
