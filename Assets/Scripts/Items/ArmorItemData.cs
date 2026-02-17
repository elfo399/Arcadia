using UnityEngine;

[CreateAssetMenu(menuName = "RogueLike/Armor Item")]
public class ArmorItemData : ScriptableObject
{
    public enum ArmorSlot
    {
        Helmet,
        Chestplate,
        Leggings,
        Boots
    }

    [Header("Info")]
    public string itemName;
    [TextArea] public string description;
    public Sprite icon;
    public ArmorSlot slot = ArmorSlot.Helmet;

    [Header("Stats")]
    [Min(0f)] public float weight = 1f;
    public int physicalDefense = 0;
    public int magicDefense = 0;
}
