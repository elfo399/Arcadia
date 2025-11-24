using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    [Header("Armi equipaggiate")]
    // Weapon equipped in the right hand
    public WeaponItem rightHandWeapon;
    // Weapon equipped in the left hand
    public WeaponItem leftHandWeapon;

    [Header("Default unarmed")]
    // Default right-hand unarmed weapon
    public WeaponItem unarmedRight;
    // Default left-hand unarmed weapon
    public WeaponItem unarmedLeft;

    // Get the weapon currently usable for the specified hand
    public WeaponItem GetWeaponForHand(Hand hand)
    {
        WeaponItem equipped = (hand == Hand.Right) ? rightHandWeapon : leftHandWeapon;

        if (equipped != null)
            return equipped;

        return (hand == Hand.Right) ? unarmedRight : unarmedLeft;
    }
}

public enum Hand
{
    // Right-hand slot
    Right,
    // Left-hand slot
    Left
}

public enum AttackType
{
    // Light attack variant
    Light,
    // Heavy attack variant
    Heavy
}
