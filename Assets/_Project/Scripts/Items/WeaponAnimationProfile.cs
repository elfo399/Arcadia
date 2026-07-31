using UnityEngine;

[CreateAssetMenu(menuName = "RogueLike/Weapon Animation Profile")]
public class WeaponAnimationProfile : ScriptableObject
{
    [Header("Right Hand Attacks")]
    // Animation name for right-hand light attack
    public string rightHandLightAttackAnim;
    // Animation name for right-hand heavy attack
    public string rightHandHeavyAttackAnim;

    [Header("Left Hand Attacks")]
    // Animation name for left-hand light attack
    public string leftHandLightAttackAnim;
    // Animation name for left-hand heavy attack
    public string leftHandHeavyAttackAnim;

    [Header("Air Attacks (optional)")]
    // Animation name for right-hand air attack
    public string rightHandAirAttackAnim;  
    // Animation name for left-hand air attack
    public string leftHandAirAttackAnim;   
}
