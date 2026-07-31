using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemRegistry", menuName = "RogueLike/Item Registry")]
public class ItemRegistry : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public string category;
        public string key;
        public string itemName;
        public Sprite icon;
        public WeaponItem weaponData;
        public UsableItemData usableData;
        public ItemData itemData;
        public ArmorItemData armorData;
        public MagicItemData magicData;
    }

    public List<Entry> entries = new();
}
