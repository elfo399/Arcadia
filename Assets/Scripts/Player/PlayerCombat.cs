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
    public bool canAttack = true; // 👈 NEW: se false, nessun attacco parte

    void Awake()
    {
        animator    = GetComponentInChildren<Animator>();
        inventory   = GetComponent<PlayerInventory>();
        stats       = GetComponent<PlayerStats>();
        controller  = GetComponent<PlayerController>();

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
        // NON puoi attaccare se:
        // - sei in roll
        // - stai già attaccando
        // - il controller ti ha disabilitato gli attacchi
        if (!canAttack) return;
        if (isAttacking) return;
        if (controller != null && controller.isDodging) return;

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
        if (controller != null && controller.isDodging) return;

        // Prendiamo l'arma (o i pugni se la mano è vuota)
        WeaponItem weapon = inventory.GetWeaponForHand(hand);
        if (weapon == null)
        {
            Debug.Log("Nessuna arma per " + hand);
            return;
        }

        // Stamina cost in base al tipo di attacco (preso dall'arma)
        float staminaCost = (type == AttackType.Light)
            ? weapon.lightAttackStaminaCost
            : weapon.heavyAttackStaminaCost;

        // Se non hai stamina sufficiente, niente attacco
        if (!stats.HasStamina(staminaCost))
        {
            Debug.Log("Stamina insufficiente per " + type + " con " + weapon.weaponName);
            return;
        }

        // Consuma stamina
        stats.SpendStamina(staminaCost);

        // Esegui davvero l'attacco
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

        // per ora blocchiamo per mezzo secondo
        Invoke(nameof(ResetAttackFlag), 0.5f);
    }

    void ResetAttackFlag()
    {
        isAttacking = false;
    }

    string GetAttackAnimation(WeaponAnimationProfile profile, Hand hand, AttackType type, bool isAirAttack)
    {
        // AIR ATTACK (solo light)
        if (isAirAttack && type == AttackType.Light)
        {
            if (hand == Hand.Right && !string.IsNullOrEmpty(profile.rightHandAirAttackAnim))
                return profile.rightHandAirAttackAnim;

            if (hand == Hand.Left && !string.IsNullOrEmpty(profile.leftHandAirAttackAnim))
                return profile.leftHandAirAttackAnim;
        }

        // Normale
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
