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

    [TextArea]
    [Header("Description")]
    // Descrizione testuale per il pannello dettagli
    public string description;

    [Header("Category")]
    // Weapon category classification
    public WeaponCategory category;

    [Header("Danni")]
    // Physical damage dealt by the weapon
    public int physicalDamage = 10;
    // Moltiplicatore o chance di colpo critico (interpretazione libera)
    public float criticalHit = 1.1f;
    // Peso usato per il bilanciamento (UI / equip load)
    public float weight = 3f;

    [Header("Scaling")]
    // Nota di scaling (es. STR C / DEX B). Stringa libera per l'UI.
    public string scaling = "STR C / DEX D";

    [Header("Requisiti")]
    // Requisiti minimi per impugnare (testo libero per semplicità)
    public string requirements = "STR 10 / DEX 8";

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
