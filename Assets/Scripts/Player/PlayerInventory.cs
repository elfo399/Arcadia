using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestisce slot arma equipaggiati e una lista inventario di base.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    [Header("Armi equipaggiate")]
    public WeaponItem rightHandWeapon;
    public WeaponItem leftHandWeapon;

    [Header("Default unarmed")]
    public WeaponItem unarmedRight;
    public WeaponItem unarmedLeft;

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
            var invItem = CreateInventoryItemFromEntry(entry);
            if (invItem != null)
            {
                items.Add(invItem);
            }
        }
    }

    /// <summary>
    /// Restituisce l'arma equipaggiata (o l'unarmed di default) per la mano richiesta.
    /// </summary>
    public WeaponItem GetWeaponForHand(Hand hand)
    {
        WeaponItem equipped = (hand == Hand.Right) ? rightHandWeapon : leftHandWeapon;
        if (equipped != null)
            return equipped;
        return (hand == Hand.Right) ? unarmedRight : unarmedLeft;
    }

    // --- API semplici per aggiungere/rimuovere oggetti (espandibile) ---
    public void AddItem(InventoryItem item)
    {
        if (item != null) items.Add(item);
    }

    public bool RemoveItem(InventoryItem item)
    {
        return items.Remove(item);
    }

    public void ClearItems()
    {
        items.Clear();
    }

    /// <summary>
    /// Sostituisce l'intera lista mantenendo l'ordine e gli slot vuoti.
    /// </summary>
    public void ReplaceAllItems(List<InventoryItem> newItems)
    {
        items.Clear();
        if (newItems != null) items.AddRange(newItems);
    }

    private InventoryItem CreateInventoryItemFromEntry(StartingItemEntry entry)
    {
        if (entry == null) return null;

        int qty = Mathf.Max(1, entry.quantity);
        int assigned = (entry.weapon != null ? 1 : 0) + (entry.item != null ? 1 : 0) + (entry.usable != null ? 1 : 0);
        if (assigned == 0)
        {
            return null;
        }
        if (assigned > 1)
        {
            Debug.LogWarning("[PlayerInventory] Starting item entry ha più di un campo settato. Userò la priorità: Weapon > Usable > Item.");
        }

        if (entry.weapon != null) return new InventoryItem(entry.weapon, qty);
        if (entry.usable != null) return new InventoryItem(entry.usable, qty);
        return new InventoryItem(entry.item, qty);
    }
}

public enum Hand
{
    Right,
    Left
}

public enum AttackType
{
    Light,
    Heavy
}
