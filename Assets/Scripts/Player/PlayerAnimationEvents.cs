using UnityEngine;

public class PlayerAnimationEvents : MonoBehaviour
{
    private PlayerCombat combat;

    void Awake()
    {
        // Cerca lo script PlayerCombat nel genitore (il Player principale)
        combat = GetComponentInParent<PlayerCombat>();
    }

    // Aggiungi un evento nell'animazione che chiama QUESTA funzione
    public void CallEndAttack()
    {
        if (combat != null)
        {
            combat.EndAttack();
        }
    }
}