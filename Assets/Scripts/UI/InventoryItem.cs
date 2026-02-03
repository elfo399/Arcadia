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

    public InventoryItem(Sprite itemIcon, int quantity)
    {
        icon = itemIcon;
        amount = quantity;
    }
}
