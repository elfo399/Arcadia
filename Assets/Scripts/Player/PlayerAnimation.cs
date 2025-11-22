using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerCombat combat;

    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int IsSprintingParam = Animator.StringToHash("IsSprinting");

    void Reset()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (combat == null)
            combat = GetComponent<PlayerCombat>();
    }

    void Awake()
    {
        if (combat == null)
            combat = GetComponent<PlayerCombat>();
    }

    void Update()
    {
        if (animator == null || playerController == null || combat == null)
            return;

        // ❗ NON aggiornare nulla se stai attaccando
        if (combat.isAttacking)
            return;

        // ❗ NON aggiornare nulla se stai rollando
        if (playerController.isDodging)
            return;

        // Se sei libero, allora aggiorni la locomozione
        float speed = playerController.moveAmount;

        animator.SetFloat(SpeedParam, speed);
        animator.SetBool(IsSprintingParam, playerController.isSprinting);
    }
}
