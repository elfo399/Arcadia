using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInventory))]
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(PlayerController))]
public class PlayerCombat : MonoBehaviour
{
    // Animator used to play attack and flask animations
    private Animator animator;
    // Inventory providing equipped weapons
    private PlayerInventory inventory;
    // Stats reference for stamina and flasks
    private PlayerStats stats;
    // Input actions for combat
    private PlayerControls controls;
    // Controller reference for movement state checks
    private PlayerController controller;

    [Header("Combat Flags")]
    // True while an attack animation is playing
    public bool isAttacking = false;

    [Header("Permissions")]
    // Gate to disable attacks when false
    public bool canAttack = true;

    // Cache component references and initialize input
    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        inventory = GetComponent<PlayerInventory>();
        stats = GetComponent<PlayerStats>();
        controller = GetComponent<PlayerController>();

        controls = new PlayerControls();
    }

    // Enable player combat input
    void OnEnable()
    {
        controls.Player.Enable();
    }

    // Disable player combat input
    void OnDisable()
    {
        controls.Player.Disable();
    }

    // Poll input for attacks and flask usage
    void Update()
    {
        HandleAttackInput();
        HandleFlaskInput();
    }

    // Process attack input if the player is allowed to attack
    void HandleAttackInput()
    {
        bool isRolling = controller != null && controller.IsRolling;

        if (isRolling ||
            controls.Player.SprintOrDodge.IsPressed() ||
            controls.Player.SprintOrDodge.WasPerformedThisFrame())
        {
            return;
        }

        if (!canAttack) return;
        if (isAttacking) return;

        if (controls.Player.LightAttackRight.WasPerformedThisFrame())
        {
            TryAttack(Hand.Right, AttackType.Light);
        }

        if (controls.Player.LightAttackLeft.WasPerformedThisFrame())
        {
            TryAttack(Hand.Left, AttackType.Light);
        }

        if (controls.Player.HeavyAttackRight.WasPerformedThisFrame())
        {
            TryAttack(Hand.Right, AttackType.Heavy);
        }

        if (controls.Player.HeavyAttackLeft.WasPerformedThisFrame())
        {
            TryAttack(Hand.Left, AttackType.Heavy);
        }
    }

    // Consume a flask if the player is free and grounded
    void HandleFlaskInput()
    {
        if (!controls.Player.UseFlask.WasPerformedThisFrame())
            return;

        bool isRolling = controller != null && controller.IsRolling;
        bool isGrounded = controller == null || controller.IsGrounded;

        if (isRolling) return;
        if (isAttacking) return;
        if (!isGrounded) return;

        stats.UseFlask();
    }

    // Try to perform an attack with the specified hand and type
    void TryAttack(Hand hand, AttackType type)
    {
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

    // Execute attack animation and flag handling
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

    // Reset attack flag after animation window
    void ResetAttackFlag()
    {
        isAttacking = false;
    }

    // Determine which animation name to use for the given attack
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
        else
        {
            if (type == AttackType.Light)
                return profile.leftHandLightAttackAnim;

            return profile.leftHandHeavyAttackAnim;
        }
    }
}
