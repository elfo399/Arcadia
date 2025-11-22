using UnityEngine;

[CreateAssetMenu(menuName = "RogueLike/Weapon")]
public class WeaponItem : ScriptableObject
{
    public string weaponName;
    
    [Header("Visual")]
    public Sprite icon;
    public GameObject modelPrefab;

    [Header("Category")]
    public WeaponCategory category;

    [Header("Danni")]
    public int physicalDamage = 10;

    [Header("Animation Profile")]
    public WeaponAnimationProfile animationProfile;

    [Header("Stamina Cost")]
    public float lightAttackStaminaCost = 10f;
    public float heavyAttackStaminaCost = 20f;

    [Header("Abilità (per il futuro)")]
    public bool hasRightSkill;
    public bool hasLeftSkill;

    [Header("Special Weapon")]
    public bool isSpecialWeapon;
}
