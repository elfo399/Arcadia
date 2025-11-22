using UnityEngine;

[CreateAssetMenu(menuName = "RogueLike/Weapon Animation Profile")]
public class WeaponAnimationProfile : ScriptableObject
{
    [Header("Right Hand Attacks")]
    public string rightHandLightAttackAnim;
    public string rightHandHeavyAttackAnim;

    [Header("Left Hand Attacks")]
    public string leftHandLightAttackAnim;
    public string leftHandHeavyAttackAnim;

    [Header("Air Attacks (optional)")]
    public string rightHandAirAttackAnim;  
    public string leftHandAirAttackAnim;   
}
