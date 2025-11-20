using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController playerController;

    private static readonly int SpeedParam = Animator.StringToHash("Speed");

    void Reset()
    {
        // Prova a trovare automaticamente i riferimenti
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (playerController == null)
            playerController = GetComponent<PlayerController>();
    }

    void Update()
    {
        if (animator == null || playerController == null)
            return;

        // Leggiamo quanto si sta muovendo il player (0 = fermo, 1 = max input)
        float speed = playerController.moveAmount;

        // Passiamo il valore all'Animator (parametro "Speed")
        animator.SetFloat(SpeedParam, speed);
    }
}
