using UnityEngine;

[CreateAssetMenu(menuName = "RogueLike/Magic Item")]
public class MagicItemData : ScriptableObject
{
    public enum MagicCategory
    {
        Attack,
        Boost,
        Healing
    }

    public enum MagicEffectType
    {
        Damage,
        HealHealth,
        RestoreMana,
        BoostAttribute
    }

    public enum BoostAttribute
    {
        None,
        Vigor,
        Mind,
        Endurance,
        Strength,
        Dexterity,
        Intelligence,
        Faith
    }

    [Header("Info")]
    public string magicName;
    [TextArea] public string description;
    public Sprite icon;
    public MagicCategory category = MagicCategory.Attack;
    public MagicEffectType effectType = MagicEffectType.Damage;

    [Header("Stats")]
    public int magicDamage = 10;
    public int healAmount = 0;
    public BoostAttribute boostAttribute = BoostAttribute.None;
    public int boostAmount = 0;
    [Min(0f)] public float boostDurationSeconds = 0f;
    public float criticalHit = 1f;
    public string scaling = "INT C";
    public string requirements = "INT 10+";

    [Header("Cast")]
    [Min(0f)] public float manaCost = 12f;
    [Min(0f)] public float castTime = 0.35f;
    [Min(0f)] public float castCooldown = 0.45f;

    [Header("Projectile")]
    public GameObject projectilePrefab;
    [Min(0.1f)] public float projectileSpeed = 18f;
    [Min(0.1f)] public float projectileLifetime = 4f;
    public Vector3 spawnOffset = new Vector3(0f, 1.2f, 0.7f);
    public LayerMask hitMask = ~0;
}
