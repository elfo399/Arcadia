using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInventory))]
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(PlayerController))]
public class PlayerCombat : MonoBehaviour
{
    // Componenti
    private Animator animator;
    private PlayerInventory inventory;
    private PlayerStats stats;
    private PlayerControls controls;
    private PlayerController controller;

    [Header("Stato Combattimento")]
    public bool isAttacking = false;
    public bool canAttack = true;

    void Awake()
    {
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
        // Se stiamo rollando, niente attacchi
        if (controller != null && controller.IsRolling) return;
        
        // Se stiamo già attaccando o siamo bloccati, esci
        if (!canAttack || isAttacking) return;

        if (controls.Player.LightAttackRight.WasPerformedThisFrame())
            TryAttack(Hand.Right, AttackType.Light);

        if (controls.Player.LightAttackLeft.WasPerformedThisFrame())
            TryAttack(Hand.Left, AttackType.Light);

        if (controls.Player.HeavyAttackRight.WasPerformedThisFrame())
            TryAttack(Hand.Right, AttackType.Heavy);

        if (controls.Player.HeavyAttackLeft.WasPerformedThisFrame())
            TryAttack(Hand.Left, AttackType.Heavy);
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
        // 1. Recupera l'arma (o i Pugni se slot vuoto)
        WeaponItem weapon = inventory.GetWeaponForHand(hand);

        // DEBUG SICUREZZA 1: Arma mancante
        if (weapon == null)
        {
            Debug.LogError($"[PlayerCombat] ERRORE GRAVE: Nessuna arma trovata per la mano {hand}! " +
                           "Controlla PlayerInventory: gli slot 'Unarmed Right/Left' DEVONO avere il file 'Unarmed_Item'.");
            return;
        }

        // 2. Calcola costo Stamina
        float staminaCost = (type == AttackType.Light) ? weapon.lightAttackStaminaCost : weapon.heavyAttackStaminaCost;

        // DEBUG SICUREZZA 2: Stamina
        if (!stats.HasStamina(staminaCost))
        {
            // Debug.Log("Stamina insufficiente per attaccare!");
            return;
        }

        // 3. Esegui
        stats.SpendStamina(staminaCost);
        PerformAttack(weapon, hand, type);
    }

    void PerformAttack(WeaponItem weapon, Hand hand, AttackType type)
    {
        // DEBUG SICUREZZA 3: Profilo Animazioni
        if (weapon.animationProfile == null)
        {
            Debug.LogError($"[PlayerCombat] L'arma '{weapon.weaponName}' non ha un Animation Profile assegnato! Assegnalo nell'Inspector.");
            return;
        }

        bool isAirAttack = controller != null && !controller.IsGrounded;
        string animToPlay = GetAttackAnimation(weapon.animationProfile, hand, type, isAirAttack);

        if (string.IsNullOrEmpty(animToPlay))
        {
            Debug.LogWarning($"[PlayerCombat] Nessuna animazione trovata nel profilo per {hand} - {type}");
            return;
        }

        // 1. Ferma il movimento (Feeling Souls-like)
        if (controller != null) controller.StopMovementImmediate();

        // 2. Setta flag
        isAttacking = true;

        // 3. Lancia animazione (CrossFade basso per reattività istantanea)
        animator.CrossFadeInFixedTime(animToPlay, 0.1f);
    }

    // Chiamata dall'Animation Event (tramite PlayerAnimationEvents.cs)
    public void EndAttack()
    {
        isAttacking = false;
    }

    string GetAttackAnimation(WeaponAnimationProfile profile, Hand hand, AttackType type, bool isAirAttack)
    {
        // Attacchi Aerei
        if (isAirAttack && type == AttackType.Light)
        {
            if (hand == Hand.Right && !string.IsNullOrEmpty(profile.rightHandAirAttackAnim)) 
                return profile.rightHandAirAttackAnim;
            if (hand == Hand.Left && !string.IsNullOrEmpty(profile.leftHandAirAttackAnim)) 
                return profile.leftHandAirAttackAnim;
        }

        // Attacchi Terra
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