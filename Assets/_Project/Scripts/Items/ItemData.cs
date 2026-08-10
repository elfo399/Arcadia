using UnityEngine;

[CreateAssetMenu(menuName = "RogueLike/Item")]
public class ItemData : ScriptableObject
{
    [Header("Info")]
    public string itemName;
    [Min(0)] public int baseValue = 1;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Stats")]
    [Min(0f)] public float weight = 0.2f;
}
