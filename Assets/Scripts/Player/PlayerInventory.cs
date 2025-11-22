using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    [Header("Armi equipaggiate")]
    public WeaponItem rightHandWeapon;
    public WeaponItem leftHandWeapon;

    [Header("Default unarmed")]
    public WeaponItem unarmedRight;
    public WeaponItem unarmedLeft;

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
    Right,
    Left
}

public enum AttackType
{
    Light,
    Heavy
}
