using UnityEngine;

[CreateAssetMenu(menuName = "RogueLike/Weapon")]
public class WeaponItem : ScriptableObject
{
    // Display name of the weapon
    public string weaponName;
    
    [Header("Visual")]
    // Icon used in UI slots
    public Sprite icon;
    // Prefab for the weapon model
    public GameObject modelPrefab;

    [Header("Category")]
    // Weapon category classification
    public WeaponCategory category;

    [Header("Danni")]
    // Physical damage dealt by the weapon
    public int physicalDamage = 10;

    [Header("Animation Profile")]
    // Animation mappings used for attacks
    public WeaponAnimationProfile animationProfile;

    [Header("Stamina Cost")]
    // Stamina cost for light attacks
    public float lightAttackStaminaCost = 10f;
    // Stamina cost for heavy attacks
    public float heavyAttackStaminaCost = 20f;

    [Header("Abilit\u00e0 (per il futuro)")]
    // Whether the weapon has a right-hand skill
    public bool hasRightSkill;
    // Whether the weapon has a left-hand skill
    public bool hasLeftSkill;

    [Header("Special Weapon")]
    // Marks the weapon as special or unique
    public bool isSpecialWeapon;
}
