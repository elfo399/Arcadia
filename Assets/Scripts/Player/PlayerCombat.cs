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
        HandleFlaskInput();   // usa la flask solo se sei libero
    }

    //──────────────── INPUT ATTACCO ────────────────

    void HandleAttackInput()
    {
        bool isRolling = controller != null && controller.IsRolling;

        // se stai rollando o premi il tasto roll → niente attacchi
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

    //──────────────── INPUT FLASK (Quadrato) ────────────────
    // IGNORA se stai saltando / rollando / attaccando.

    void HandleFlaskInput()
    {
        if (!controls.Player.UseFlask.WasPerformedThisFrame())
            return;

        bool isRolling  = controller != null && controller.IsRolling;
        bool isGrounded = controller == null || controller.IsGrounded;

        // se sei occupato → ignora
        if (isRolling) return;
        if (isAttacking) return;
        if (!isGrounded) return;   // in aria (salto / caduta) → ignora

        // qui sei: a terra, non roll, non attacco → puoi curarti
        stats.UseFlask();
    }

    //──────────────── LOGICA ATTACCO ────────────────

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
