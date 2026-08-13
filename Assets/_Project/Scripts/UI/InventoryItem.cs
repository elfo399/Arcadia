using UnityEngine;

/// <summary>
/// A simple class to represent an item in the inventory.
/// This would typically be a ScriptableObject or a more complex class,
/// but is kept simple as requested.
/// </summary>
[System.Serializable]
public class InventoryItem
{
    public string instanceId;
    public Sprite icon;
    public int amount;
    public int upgradeLevel;
    public string title;
    public string description;

    // Per gli oggetti arma possiamo collegare direttamente il relativo ScriptableObject
    public WeaponItem weaponData;
    // Per oggetti generici non-arma
    public ItemData itemData;
    // Per oggetti usabili/consumabili
    public UsableItemData usableData;
    // Per armature
    public ArmorItemData armorData;
    // Per magie/spell
    public MagicItemData magicData;

    public static string CreateInstanceId()
    {
        return System.Guid.NewGuid().ToString("N");
    }

    public InventoryItem(Sprite itemIcon, int quantity)
    {
        instanceId = CreateInstanceId();
        icon = itemIcon;
        amount = quantity;
        upgradeLevel = 0;
        title = itemIcon != null ? itemIcon.name : string.Empty;
        description = string.Empty;
        weaponData = null;
        itemData = null;
        usableData = null;
        armorData = null;
        magicData = null;
    }

    public InventoryItem(WeaponItem weapon, int quantity = 1, string overrideTitle = null, string overrideDescription = null)
    {
        weaponData = weapon;
        icon = weapon != null ? weapon.icon : null;
        // Weapons own individual runtime state and are never stored as a stack.
        amount = 1;
        upgradeLevel = 0;
        title = overrideTitle ?? (weapon != null ? weapon.weaponName : string.Empty);
        description = overrideDescription ?? (weapon != null ? weapon.description : string.Empty);
        itemData = null;
        usableData = null;
        armorData = null;
        magicData = null;
        instanceId = CreateInstanceId();
    }

    public InventoryItem(ItemData item, int quantity = 1, string overrideTitle = null, string overrideDescription = null)
    {
        itemData = item;
        icon = item != null ? item.icon : null;
        amount = quantity;
        upgradeLevel = 0;
        title = overrideTitle ?? (item != null ? item.itemName : string.Empty);
        description = overrideDescription ?? (item != null ? item.description : string.Empty);
        weaponData = null;
        usableData = null;
        armorData = null;
        magicData = null;
        instanceId = CreateInstanceId();
    }

    public InventoryItem(UsableItemData usable, int quantity = 1, string overrideTitle = null, string overrideDescription = null)
    {
        usableData = usable;
        icon = usable != null ? usable.icon : null;
        amount = quantity;
        upgradeLevel = 0;
        title = overrideTitle ?? (usable != null ? usable.itemName : string.Empty);
        description = overrideDescription ?? (usable != null ? usable.description : string.Empty);
        weaponData = null;
        itemData = null;
        armorData = null;
        magicData = null;
        instanceId = CreateInstanceId();
    }

    public InventoryItem(ArmorItemData armor, int quantity = 1, string overrideTitle = null, string overrideDescription = null)
    {
        armorData = armor;
        icon = armor != null ? armor.icon : null;
        // Armor owns individual runtime state and is never stored as a stack.
        amount = 1;
        upgradeLevel = 0;
        title = overrideTitle ?? (armor != null ? armor.itemName : string.Empty);
        description = overrideDescription ?? (armor != null ? armor.description : string.Empty);
        weaponData = null;
        itemData = null;
        usableData = null;
        magicData = null;
        instanceId = CreateInstanceId();
    }

    public InventoryItem(MagicItemData magic, int quantity = 1, string overrideTitle = null, string overrideDescription = null)
    {
        magicData = magic;
        icon = magic != null ? magic.icon : null;
        amount = quantity;
        upgradeLevel = 0;
        title = overrideTitle ?? (magic != null ? magic.magicName : string.Empty);
        description = overrideDescription ?? (magic != null ? magic.description : string.Empty);
        weaponData = null;
        itemData = null;
        usableData = null;
        armorData = null;
        instanceId = CreateInstanceId();
    }
}
