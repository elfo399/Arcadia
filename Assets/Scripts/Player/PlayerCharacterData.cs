using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewPlayerCharacter", menuName = "Arcadia/Player Character")]
public class PlayerCharacterData : ScriptableObject
{
    [Serializable]
    public class StartingInventoryEntry
    {
        public WeaponItem weapon;
        public MagicItemData magic;
        public ArmorItemData armor;
        public ItemData item;
        public UsableItemData usable;
        [Min(1)] public int quantity = 1;
    }

    [Header("Identity")]
    public string characterId = "warrior";
    public string displayName = "Warrior";
    [TextArea] public string description;
    public Sprite portrait;
    public GameObject previewPrefab;

    [Header("Prefab Override")]
    public GameObject playerPrefab;

    [Header("Level")]
    [Min(1)] public int startingLevel = 1;
    [Min(1)] public int experienceToNextLevel = 100;

    [Header("Attributes")]
    [Min(1)] public int vigor = 10;
    [Min(1)] public int mind = 10;
    [Min(1)] public int endurance = 10;
    [Min(1)] public int strength = 10;
    [Min(1)] public int dexterity = 10;
    [Min(1)] public int intelligence = 10;
    [Min(1)] public int faith = 10;

    [Header("Base Resources")]
    [Min(1f)] public float baseMaxHealth = 100f;
    [Min(1f)] public float baseMaxStamina = 100f;
    [Min(1f)] public float baseMaxMana = 50f;
    [Min(0)] public int maxFlasks = 3;
    [Min(0f)] public float flaskHealAmount = 40f;

    [Header("Alignment")]
    public int karma = 0;
    public int benedetto = 0;
    public int malefico = 0;

    [Header("Starting Loadout")]
    public WeaponItem[] rightLoadout = new WeaponItem[3];
    public WeaponItem[] leftLoadout = new WeaponItem[3];
    public MagicItemData[] magicLoadout = new MagicItemData[3];
    public int[] magicLoadoutQuantities = new int[3] { 1, 1, 1 };
    public UsableItemData[] usableLoadout = new UsableItemData[3];
    public int[] usableLoadoutQuantities = new int[3] { 1, 1, 1 };
    public ArmorItemData[] armorLoadout = new ArmorItemData[4];

    [Header("Backpack")]
    public StartingInventoryEntry[] backpackItems = Array.Empty<StartingInventoryEntry>();

    public string GetCharacterId()
    {
        return string.IsNullOrWhiteSpace(characterId) ? name : characterId.Trim();
    }

    public void ApplyStartingInventory(PlayerInventory inventory)
    {
        if (inventory == null)
            return;

        inventory.ReplaceAllItems(new List<InventoryItem>());
        ClearLoadout(inventory);
        ApplyWeaponLoadout(inventory, rightLoadout, true);
        ApplyWeaponLoadout(inventory, leftLoadout, false);
        ApplyMagicLoadout(inventory);
        ApplyUsableLoadout(inventory);
        ApplyArmorLoadout(inventory);
        AddBackpackItems(inventory);
    }

    private static void ClearLoadout(PlayerInventory inventory)
    {
        for (int i = 0; i < 3; i++)
        {
            inventory.SetRightAtSlot(i, null, null);
            inventory.SetLeftAtSlot(i, null, null);
            inventory.SetMagicAtSlot(i, null, null);
            inventory.SetUsableAtSlot(i, null, null);
        }

        inventory.SetArmorAtSlot(ArmorItemData.ArmorSlot.Helmet, null, null);
        inventory.SetArmorAtSlot(ArmorItemData.ArmorSlot.Chestplate, null, null);
        inventory.SetArmorAtSlot(ArmorItemData.ArmorSlot.Leggings, null, null);
        inventory.SetArmorAtSlot(ArmorItemData.ArmorSlot.Boots, null, null);
    }

    private static void ApplyWeaponLoadout(PlayerInventory inventory, WeaponItem[] loadout, bool rightHand)
    {
        if (loadout == null)
            return;

        int count = Mathf.Min(loadout.Length, 3);
        for (int i = 0; i < count; i++)
        {
            WeaponItem weapon = loadout[i];
            if (weapon == null)
                continue;

            InventoryItem item = new InventoryItem(weapon, 1);
            inventory.AddItem(item);
            if (rightHand)
                inventory.SetRightAtSlot(i, weapon, item.instanceId);
            else
                inventory.SetLeftAtSlot(i, weapon, item.instanceId);
        }
    }

    private void ApplyMagicLoadout(PlayerInventory inventory)
    {
        if (magicLoadout == null)
            return;

        int count = Mathf.Min(magicLoadout.Length, 3);
        for (int i = 0; i < count; i++)
        {
            MagicItemData magic = magicLoadout[i];
            if (magic == null)
                continue;

            InventoryItem item = new InventoryItem(magic, GetQuantity(magicLoadoutQuantities, i));
            inventory.AddItem(item);
            inventory.SetMagicAtSlot(i, magic, item.instanceId);
        }
    }

    private void ApplyUsableLoadout(PlayerInventory inventory)
    {
        if (usableLoadout == null)
            return;

        int count = Mathf.Min(usableLoadout.Length, 3);
        for (int i = 0; i < count; i++)
        {
            UsableItemData usable = usableLoadout[i];
            if (usable == null)
                continue;

            InventoryItem item = new InventoryItem(usable, GetQuantity(usableLoadoutQuantities, i));
            inventory.AddItem(item);
            inventory.SetUsableAtSlot(i, usable, item.instanceId);
        }
    }

    private void ApplyArmorLoadout(PlayerInventory inventory)
    {
        if (armorLoadout == null)
            return;

        for (int i = 0; i < armorLoadout.Length; i++)
        {
            ArmorItemData armor = armorLoadout[i];
            if (armor == null)
                continue;

            InventoryItem item = new InventoryItem(armor, 1);
            inventory.AddItem(item);
            inventory.SetArmorAtSlot(armor.slot, armor, item.instanceId);
        }
    }

    private void AddBackpackItems(PlayerInventory inventory)
    {
        if (backpackItems == null)
            return;

        for (int i = 0; i < backpackItems.Length; i++)
        {
            StartingInventoryEntry entry = backpackItems[i];
            if (entry == null)
                continue;

            int quantity = Mathf.Max(1, entry.quantity);
            int assigned = CountAssignedItemTypes(entry);
            if (assigned == 0)
                continue;
            if (assigned > 1)
                Debug.LogWarning($"[PlayerCharacterData] Backpack entry in {name} has multiple item references. Priority: Weapon > Magic > Armor > Usable > Item.");

            if (entry.weapon != null)
            {
                for (int copy = 0; copy < quantity; copy++)
                    inventory.AddItem(new InventoryItem(entry.weapon, 1));
                continue;
            }

            if (entry.magic != null)
            {
                inventory.AddItem(new InventoryItem(entry.magic, quantity));
                continue;
            }

            if (entry.armor != null)
            {
                for (int copy = 0; copy < quantity; copy++)
                    inventory.AddItem(new InventoryItem(entry.armor, 1));
                continue;
            }

            if (entry.usable != null)
            {
                inventory.AddItem(new InventoryItem(entry.usable, quantity));
                continue;
            }

            inventory.AddItem(new InventoryItem(entry.item, quantity));
        }
    }

    private static int CountAssignedItemTypes(StartingInventoryEntry entry)
    {
        int assigned = 0;
        if (entry.weapon != null) assigned++;
        if (entry.magic != null) assigned++;
        if (entry.armor != null) assigned++;
        if (entry.item != null) assigned++;
        if (entry.usable != null) assigned++;
        return assigned;
    }

    private static int GetQuantity(int[] quantities, int index)
    {
        if (quantities == null || index < 0 || index >= quantities.Length)
            return 1;

        return Mathf.Max(1, quantities[index]);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureArraySize(ref rightLoadout, 3);
        EnsureArraySize(ref leftLoadout, 3);
        EnsureArraySize(ref magicLoadout, 3);
        EnsureArraySize(ref magicLoadoutQuantities, 3, 1);
        EnsureArraySize(ref usableLoadout, 3);
        EnsureArraySize(ref usableLoadoutQuantities, 3, 1);
        EnsureArraySize(ref armorLoadout, 4);
    }

    private static void EnsureArraySize<T>(ref T[] array, int size)
    {
        if (array == null)
        {
            array = new T[size];
            return;
        }

        if (array.Length == size)
            return;

        Array.Resize(ref array, size);
    }

    private static void EnsureArraySize(ref int[] array, int size, int defaultValue)
    {
        int previousLength = array != null ? array.Length : 0;
        if (array == null)
            array = new int[size];
        else if (array.Length != size)
            Array.Resize(ref array, size);

        for (int i = previousLength; i < array.Length; i++)
            array[i] = defaultValue;
    }
#endif
}
