using UnityEngine;

[CreateAssetMenu(menuName = "RogueLike/Magic Item")]
public class MagicItemData : ScriptableObject
{
    [Header("Info")]
    public string magicName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Stats")]
    public int magicDamage = 10;
    public float criticalHit = 1f;
    public string scaling = "INT C";
    public string requirements = "INT 10+";
}
