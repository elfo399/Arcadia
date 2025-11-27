using UnityEngine;

public class PlayerAnimationEvents : MonoBehaviour
{
    private PlayerCombat combat;
    
    [Header("Hitbox Mani")]
    public WeaponDamage rightHandDamage; // Trascina qui l'osso Hand_R
    public WeaponDamage leftHandDamage;  // Trascina qui l'osso Hand_L

    // In futuro, qui metteremo anche lo slot per la Spada
    // public WeaponDamage currentWeapon; 

    void Awake()
    {
        combat = GetComponentInParent<PlayerCombat>();
    }

    public void CallEndAttack()
    {
        if (combat != null) combat.EndAttack();
    }

    // --- EVENTI PER MANO DESTRA ---
    public void EnableRightHand()
    {
        if (rightHandDamage != null) rightHandDamage.EnableDamage();
    }

    public void DisableRightHand()
    {
        if (rightHandDamage != null) rightHandDamage.DisableDamage();
    }

    // --- EVENTI PER MANO SINISTRA ---
    public void EnableLeftHand()
    {
        if (leftHandDamage != null) leftHandDamage.EnableDamage();
    }

    public void DisableLeftHand()
    {
        if (leftHandDamage != null) leftHandDamage.DisableDamage();
    }
}