using UnityEngine;

[System.Serializable]
public enum ItemCategory
{
    Generic,
    Material
}

[CreateAssetMenu(menuName = "RogueLike/Item")]
public class ItemData : ScriptableObject
{
    [Header("Persistence")]
    [Tooltip("Stable save identifier. Do not change after this definition ships.")]
    public string definitionId;

    [Header("Info")]
    public string itemName;
    [Min(0)] public int baseValue = 1;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Classification")]
    public ItemCategory category = ItemCategory.Generic;

    [Header("Stats")]
    [Min(0f)] public float weight = 0.2f;
}
