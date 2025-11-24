using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    // Animator used to drive player movement states
    [SerializeField] private Animator animator;
    // Controller providing movement data and dodge state
    [SerializeField] private PlayerController playerController;
    // Combat component used to gate animation updates while attacking
    [SerializeField] private PlayerCombat combat;

    // Hashed parameter id for movement speed
    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    // Hashed parameter id for sprinting toggle
    private static readonly int IsSprintingParam = Animator.StringToHash("IsSprinting");

    // Auto-assign references when the component is reset in the editor
    void Reset()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (combat == null)
            combat = GetComponent<PlayerCombat>();
    }

    // Ensure combat reference exists at runtime
    void Awake()
    {
        if (combat == null)
            combat = GetComponent<PlayerCombat>();
    }

    // Update locomotion parameters when the character is free to move
    void Update()
    {
        if (animator == null || playerController == null || combat == null)
            return;

        if (combat.isAttacking)
            return;

        if (playerController.isDodging)
            return;

        float speed = playerController.moveAmount;

        animator.SetFloat(SpeedParam, speed);
        animator.SetBool(IsSprintingParam, playerController.isSprinting);
    }
}
