using UnityEngine;

[CreateAssetMenu(menuName = "RogueLike/Armor Item")]
public class ArmorItemData : ScriptableObject
{
    [Header("Persistence")]
    [Tooltip("Stable save identifier. Do not change after this definition ships.")]
    public string definitionId;

    public enum ArmorSlot
    {
        Helmet,
        Chestplate,
        Leggings,
        Boots
    }

    [Header("Info")]
    public string itemName;
    [Min(0)] public int baseValue = 1;
    [TextArea] public string description;
    public Sprite icon;
    public ArmorSlot slot = ArmorSlot.Helmet;

    [Header("Stats")]
    [Min(0f)] public float weight = 1f;
    public int physicalDefense = 0;
    public int magicDefense = 0;
}
