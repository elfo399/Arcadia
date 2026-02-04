using UnityEngine;

/// <summary>
/// A simple class to represent an item in the inventory.
/// This would typically be a ScriptableObject or a more complex class,
/// but is kept simple as requested.
/// </summary>
[System.Serializable]
public class InventoryItem
{
    public Sprite icon;
    public int amount;
    public string title;
    public string description;

    // Per gli oggetti arma possiamo collegare direttamente il relativo ScriptableObject
    public WeaponItem weaponData;
    // Per oggetti generici non-arma
    public ItemData itemData;
    // Per oggetti usabili/consumabili
    public UsableItemData usableData;

    public InventoryItem(Sprite itemIcon, int quantity)
    {
        icon = itemIcon;
        amount = quantity;
        title = itemIcon != null ? itemIcon.name : string.Empty;
        description = string.Empty;
        weaponData = null;
        itemData = null;
        usableData = null;
    }

    public InventoryItem(WeaponItem weapon, int quantity = 1, string overrideTitle = null, string overrideDescription = null)
    {
        weaponData = weapon;
        icon = weapon != null ? weapon.icon : null;
        amount = quantity;
        title = overrideTitle ?? (weapon != null ? weapon.weaponName : string.Empty);
        description = overrideDescription ?? (weapon != null ? weapon.description : string.Empty);
        itemData = null;
        usableData = null;
    }

    public InventoryItem(ItemData item, int quantity = 1, string overrideTitle = null, string overrideDescription = null)
    {
        itemData = item;
        icon = item != null ? item.icon : null;
        amount = quantity;
        title = overrideTitle ?? (item != null ? item.itemName : string.Empty);
        description = overrideDescription ?? (item != null ? item.description : string.Empty);
        weaponData = null;
        usableData = null;
    }

    public InventoryItem(UsableItemData usable, int quantity = 1, string overrideTitle = null, string overrideDescription = null)
    {
        usableData = usable;
        icon = usable != null ? usable.icon : null;
        amount = quantity;
        title = overrideTitle ?? (usable != null ? usable.itemName : string.Empty);
        description = overrideDescription ?? (usable != null ? usable.description : string.Empty);
        weaponData = null;
        itemData = null;
    }
}
