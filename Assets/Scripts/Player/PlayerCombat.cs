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
    public bool canAttack = true; // se false, nessun attacco parte

    void Awake()
    {
        animator   = GetComponentInChildren<Animator>();
        inventory  = GetComponent<PlayerInventory>();
        stats      = GetComponent<PlayerStats>();
        controller = GetComponent<PlayerController>();

        controls = new PlayerControls();
    }

    void OnEnable()
    {
        controls.Player.Enable();
    }

    void OnDisable()
    {
        controls.Player.Disable();
    }

    void Update()
    {
        HandleAttackInput();
        HandleFlaskInput();
    }

    void HandleAttackInput()
    {
        bool isRolling = controller != null && controller.IsRolling;

        // Se stai rollando o stai premendo il tasto di roll nello stesso frame,
        // blocchiamo subito tutti gli input di attacco.
        if (isRolling ||
            controls.Player.SprintOrDodge.IsPressed() ||
            controls.Player.SprintOrDodge.WasPerformedThisFrame())
        {
            return;
        }

        if (!canAttack) return;
        if (isAttacking) return;

        // R1 → attacco leggero mano destra
        if (controls.Player.LightAttackRight.WasPerformedThisFrame())
        {
            TryAttack(Hand.Right, AttackType.Light);
        }

        // L1 → attacco leggero mano sinistra
        if (controls.Player.LightAttackLeft.WasPerformedThisFrame())
        {
            TryAttack(Hand.Left, AttackType.Light);
        }

        // R2 → attacco pesante mano destra
        if (controls.Player.HeavyAttackRight.WasPerformedThisFrame())
        {
            TryAttack(Hand.Right, AttackType.Heavy);
        }

        // L2 → attacco pesante mano sinistra
        if (controls.Player.HeavyAttackLeft.WasPerformedThisFrame())
        {
            TryAttack(Hand.Left, AttackType.Heavy);
        }
    }

    void HandleFlaskInput()
    {
        if (controls.Player.UseFlask.WasPerformedThisFrame())
        {
            stats.UseFlask();
        }
    }

    void TryAttack(Hand hand, AttackType type)
    {
        // sicurezza extra
        if (!canAttack) return;
        if (controller != null && controller.IsRolling) return;

        WeaponItem weapon = inventory.GetWeaponForHand(hand);
        if (weapon == null)
        {
            Debug.Log("Nessuna arma per " + hand);
            return;
        }

        float staminaCost = (type == AttackType.Light)
            ? weapon.lightAttackStaminaCost
            : weapon.heavyAttackStaminaCost;

        if (!stats.HasStamina(staminaCost))
        {
            Debug.Log("Stamina insufficiente per " + type + " con " + weapon.weaponName);
            return;
        }

        stats.SpendStamina(staminaCost);

        PerformAttack(weapon, hand, type);
    }

    void PerformAttack(WeaponItem weapon, Hand hand, AttackType type)
    {
        if (weapon.animationProfile == null)
        {
            Debug.LogWarning("Weapon " + weapon.weaponName + " non ha AnimationProfile!");
            return;
        }

        bool isAirAttack = controller != null && !controller.IsGrounded;

        string animToPlay = GetAttackAnimation(weapon.animationProfile, hand, type, isAirAttack);
        if (string.IsNullOrEmpty(animToPlay))
        {
            Debug.Log("Nessuna animazione definita per " + weapon.weaponName + " " + hand + " " + type);
            return;
        }

        isAttacking = true;
        animator.CrossFadeInFixedTime(animToPlay, 0.1f);

        Invoke(nameof(ResetAttackFlag), 0.5f);
    }

    void ResetAttackFlag()
    {
        isAttacking = false;
    }

    string GetAttackAnimation(WeaponAnimationProfile profile, Hand hand, AttackType type, bool isAirAttack)
    {
        if (isAirAttack && type == AttackType.Light)
        {
            if (hand == Hand.Right && !string.IsNullOrEmpty(profile.rightHandAirAttackAnim))
                return profile.rightHandAirAttackAnim;

            if (hand == Hand.Left && !string.IsNullOrEmpty(profile.leftHandAirAttackAnim))
                return profile.leftHandAirAttackAnim;
        }

        if (hand == Hand.Right)
        {
            if (type == AttackType.Light)
                return profile.rightHandLightAttackAnim;

            return profile.rightHandHeavyAttackAnim;
        }
        else // Left
        {
            if (type == AttackType.Light)
                return profile.leftHandLightAttackAnim;

            return profile.leftHandHeavyAttackAnim;
        }
    }
}
