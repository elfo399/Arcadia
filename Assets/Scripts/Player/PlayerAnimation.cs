using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController playerController;

    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int IsSprintingParam = Animator.StringToHash("IsSprinting");

    void Reset()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (playerController == null)
            playerController = GetComponent<PlayerController>();
    }

    void Update()
    {
        if (animator == null || playerController == null)
            return;

        // 0..1: intensità del movimento (Idle/Run/Sprint)
        float speed = playerController.moveAmount;

        animator.SetFloat(SpeedParam, speed);
        animator.SetBool(IsSprintingParam, playerController.isSprinting);
    }
}
