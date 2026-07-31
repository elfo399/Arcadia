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

    public InventoryItem(Sprite itemIcon, int quantity)
    {
        icon = itemIcon;
        amount = quantity;
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
        // le armi non sono stackabili visivamente: amount viene usato solo per duplicare in griglia
        amount = Mathf.Max(1, quantity);
        title = overrideTitle ?? (weapon != null ? weapon.weaponName : string.Empty);
        description = overrideDescription ?? (weapon != null ? weapon.description : string.Empty);
        itemData = null;
        usableData = null;
        armorData = null;
        magicData = null;
        instanceId = System.Guid.NewGuid().ToString();
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
        armorData = null;
        magicData = null;
        instanceId = System.Guid.NewGuid().ToString();
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
        armorData = null;
        magicData = null;
        instanceId = System.Guid.NewGuid().ToString();
    }

    public InventoryItem(ArmorItemData armor, int quantity = 1, string overrideTitle = null, string overrideDescription = null)
    {
        armorData = armor;
        icon = armor != null ? armor.icon : null;
        amount = quantity;
        title = overrideTitle ?? (armor != null ? armor.itemName : string.Empty);
        description = overrideDescription ?? (armor != null ? armor.description : string.Empty);
        weaponData = null;
        itemData = null;
        usableData = null;
        magicData = null;
        instanceId = System.Guid.NewGuid().ToString();
    }

    public InventoryItem(MagicItemData magic, int quantity = 1, string overrideTitle = null, string overrideDescription = null)
    {
        magicData = magic;
        icon = magic != null ? magic.icon : null;
        amount = quantity;
        title = overrideTitle ?? (magic != null ? magic.magicName : string.Empty);
        description = overrideDescription ?? (magic != null ? magic.description : string.Empty);
        weaponData = null;
        itemData = null;
        usableData = null;
        armorData = null;
        instanceId = System.Guid.NewGuid().ToString();
    }
}
