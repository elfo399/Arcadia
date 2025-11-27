using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInventory))]
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(PlayerController))]
public class PlayerCombat : MonoBehaviour
{
    private Animator animator;
    private PlayerInventory inventory;
    private PlayerStats stats;
    private PlayerControls controls;
    private PlayerController controller;

    [Header("Combat Flags")]
    public bool isAttacking = false;

    [Header("Permissions")]
    public bool canAttack = true;

    void Awake()
    {
        // Spesso l'Animator è sul figlio (il modello), usiamo GetComponentInChildren
        animator = GetComponentInChildren<Animator>();
        inventory = GetComponent<PlayerInventory>();
        stats = GetComponent<PlayerStats>();
        controller = GetComponent<PlayerController>();
        controls = new PlayerControls();
    }

    void OnEnable() => controls.Player.Enable();
    void OnDisable() => controls.Player.Disable();

    void Update()
    {
        HandleAttackInput();
        HandleFlaskInput();
    }

    void HandleAttackInput()
    {
        // Se stiamo rollando o usando tasti speciali, niente attacco
        bool isRolling = controller != null && controller.IsRolling;
        if (isRolling) return;

        if (!canAttack || isAttacking) return;

        if (controls.Player.LightAttackRight.WasPerformedThisFrame()) TryAttack(Hand.Right, AttackType.Light);
        if (controls.Player.LightAttackLeft.WasPerformedThisFrame()) TryAttack(Hand.Left, AttackType.Light);
        if (controls.Player.HeavyAttackRight.WasPerformedThisFrame()) TryAttack(Hand.Right, AttackType.Heavy);
        if (controls.Player.HeavyAttackLeft.WasPerformedThisFrame()) TryAttack(Hand.Left, AttackType.Heavy);
    }

    void HandleFlaskInput()
    {
        if (!controls.Player.UseFlask.WasPerformedThisFrame()) return;

        bool isRolling = controller != null && controller.IsRolling;
        bool isGrounded = controller == null || controller.IsGrounded;

        if (isRolling || isAttacking || !isGrounded) return;

        stats.UseFlask();
    }

    void TryAttack(Hand hand, AttackType type)
    {
        if (!canAttack) return;
        if (controller != null && controller.IsRolling) return;

        WeaponItem weapon = inventory.GetWeaponForHand(hand);
        if (weapon == null) return;

        float staminaCost = (type == AttackType.Light) ? weapon.lightAttackStaminaCost : weapon.heavyAttackStaminaCost;

        if (!stats.HasStamina(staminaCost)) return;

        stats.SpendStamina(staminaCost);
        PerformAttack(weapon, hand, type);
    }

    void PerformAttack(WeaponItem weapon, Hand hand, AttackType type)
    {
        if (weapon.animationProfile == null) return;

        bool isAirAttack = controller != null && !controller.IsGrounded;
        string animToPlay = GetAttackAnimation(weapon.animationProfile, hand, type, isAirAttack);

        if (string.IsNullOrEmpty(animToPlay)) return;

        // --- MODIFICA CHIAVE: Blocca il movimento PRIMA di attaccare ---
        if (controller != null)
        {
            controller.StopMovementImmediate();
        }

        isAttacking = true;
        
        // CrossFade basso (0.1f) per rendere l'attacco reattivo
        animator.CrossFadeInFixedTime(animToPlay, 0.1f);

        // NOTA: Abbiamo RIMOSSO l'Invoke. 
        // Ora devi usare un Animation Event nell'animazione che chiama EndAttack()
    }

    // --- NUOVA FUNZIONE: Da chiamare tramite Animation Event ---
    public void EndAttack()
    {
        isAttacking = false;
    }

    string GetAttackAnimation(WeaponAnimationProfile profile, Hand hand, AttackType type, bool isAirAttack)
    {
        if (isAirAttack && type == AttackType.Light)
        {
            if (hand == Hand.Right && !string.IsNullOrEmpty(profile.rightHandAirAttackAnim)) return profile.rightHandAirAttackAnim;
            if (hand == Hand.Left && !string.IsNullOrEmpty(profile.leftHandAirAttackAnim)) return profile.leftHandAirAttackAnim;
        }

        if (hand == Hand.Right)
        {
            return (type == AttackType.Light) ? profile.rightHandLightAttackAnim : profile.rightHandHeavyAttackAnim;
        }
        else
        {
            return (type == AttackType.Light) ? profile.leftHandLightAttackAnim : profile.leftHandHeavyAttackAnim;
        }
    }
}