using UnityEngine;

[CreateAssetMenu(menuName = "RogueLike/Item")]
public class ItemData : ScriptableObject
{
    [Header("Info")]
    public string itemName;
    [TextArea] public string description;
    public Sprite icon;
}
