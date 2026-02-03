using System.Collections.Generic;
using UnityEngine;

public class InventoryUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField]
    private InventorySlot[] slots;

    void Start()
    {
        // Se non assegnato in Inspector, popola automaticamente dai figli
        if (slots == null || slots.Length == 0)
        {
            slots = GetComponentsInChildren<InventorySlot>();
        }
        // Initialize the UI by clearing all slots
        ClearAllSlots();
    }

    /// <summary>
    /// Updates the inventory UI based on a list of items.
    /// </summary>
    /// <param name="inventoryData">The list of items to display.</param>
    public void UpdateUI(List<InventoryItem> inventoryData)
    {
        // Clear all slots before updating
        ClearAllSlots();

        // Populate slots with the new inventory data
        for (int i = 0; i < slots.Length; i++)
        {
            if (i < inventoryData.Count && inventoryData[i] != null)
            {
                slots[i].Setup(inventoryData[i].icon, inventoryData[i].amount);
            }
            else
            {
                // Ensure any extra slots are cleared
                slots[i].Clear();
            }
        }
    }

    /// <summary>
    /// A helper method to clear all slots.
    /// </summary>
    private void ClearAllSlots()
    {
        if (slots == null) return;
        foreach (var slot in slots)
        {
            if(slot != null)
            {
                slot.Clear();
            }
        }
    }

    // --- Example Usage ---
    /*
    [Header("Example Data")]
    [SerializeField] private List<InventoryItem> exampleInventory;
    [SerializeField] private bool triggerUpdate = false;

    // In the Inspector, you can add some example items and then
    // check the 'triggerUpdate' boolean to test the UI update.
    void Update()
    {
        if (triggerUpdate)
        {
            triggerUpdate = false;
            // Create a mock list of items for demonstration
            // In a real game, this would come from an InventoryController or PlayerInventory
            UpdateUI(exampleInventory);
        }
    }
    */
}
