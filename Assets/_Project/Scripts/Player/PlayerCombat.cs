using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(PlayerInventory))]
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(PlayerController))]
public class PlayerCombat : MonoBehaviour
{
    // Componenti
    private Animator animator;
    private PlayerInventory inventory;
    private PlayerStats stats;
    private PlayerController controller;
    private PlayerAnimationEvents animationEvents;
    private TargetLockSystem targetLockSystem;

    [Header("Stato Combattimento")]
    public bool isAttacking = false;
    public bool canAttack = true;
    [SerializeField] private float shieldBlockHoldThreshold = 0.18f;
    [SerializeField, Range(30f, 180f)] private float blockFrontAngle = 140f;
    [SerializeField] private float minimumBlockStaminaCost = 2f;
    [SerializeField] private float blockStabilityScale = 0.01f;
    [SerializeField] private float guardBreakDuration = 0.9f;
    [SerializeField] private string blockingAnimatorParameter = "IsBlocking";
    [SerializeField] private string parryAnimatorTrigger = "Parry";
    [SerializeField] private float parryTotalLockTime = 0.45f;
    [SerializeField] private float parryRecoveryTime = 0.12f;
    [SerializeField] private float parryStaggerDuration = 1.1f;
    private bool isBlockingRight;
    private bool isBlockingLeft;
    private bool pendingShieldRightAction;
    private bool pendingShieldLeftAction;
    private float pendingShieldRightStartTime = -999f;
    private float pendingShieldLeftStartTime = -999f;
    private bool isGuardBroken;
    private bool hasBlockingAnimatorParameter;
    private bool hasParryAnimatorTrigger;
    private Coroutine guardBreakRoutine;
    private Coroutine parryRoutine;
    private bool isParryWindowOpen;
    private bool isParrying;

    [Header("Magic Cast (Prototype)")]
    [SerializeField] private bool enableMagicCastPrototype = false;
    [SerializeField] private Transform magicCastPoint;
    [SerializeField] private float magicCastPointHeightOffset = -0.35f;
    [SerializeField] private Key magicCastKey = Key.C;
    [SerializeField] private float fallbackMagicCooldown = 0.45f;
    [SerializeField] private float fallbackProjectileSpeed = 18f;
    [SerializeField] private float fallbackProjectileLifetime = 4f;
    [SerializeField] private float wandLightCastWindup = 0.16f;
    [SerializeField] private float wandHeavyCastWindup = 0.26f;
    [SerializeField] private float bowLightShotWindup = 0.2f;
    [SerializeField] private float bowHeavyShotWindup = 0.3f;
    [SerializeField] private float rangedRecoveryTime = 0.12f;
    [SerializeField] private float rangedSpawnBackOffset = 0.35f;
    [SerializeField] private float castPointForwardOffsetScale = 0.15f;
    [SerializeField] private bool lockRangedUntilAnimationEnds = true;
    [SerializeField] private float rangedMinTotalLockTime = 0.55f;
    [SerializeField] private float fallbackMeleeUnlockTime = 0.9f;
    [Header("Weapon Throw")]
    [SerializeField] private bool enableWeaponThrow = true;
    [SerializeField] private float throwMinLockTime = 0.35f;
    [SerializeField] private LayerMask throwHitMask = ~0;
    [SerializeField] private float throwSpawnForwardOffset = 0.55f;
    [SerializeField] private float throwSpawnHeightOffset = 1.05f;
    [SerializeField] private float throwArcUpBias = 0.08f;
    private float lastMagicCastTime = -999f;
    private readonly float[] lastWandLightCastTime = { -999f, -999f };
    private readonly float[] lastWandHeavyCastTime = { -999f, -999f };
    private readonly float[] lastBowShotTime = { -999f, -999f };
    private Coroutine rangedActionRoutine;
    private Coroutine meleeUnlockRoutine;
    private bool rangedAttackLockActive;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        inventory = GetComponent<PlayerInventory>();
        stats = GetComponent<PlayerStats>();
        controller = GetComponent<PlayerController>();
        animationEvents = GetComponentInChildren<PlayerAnimationEvents>(true);
        targetLockSystem = GetComponentInChildren<TargetLockSystem>(true);
        if (targetLockSystem == null)
            targetLockSystem = GetComponentInParent<TargetLockSystem>();
        CacheAnimatorParameters();
    }

    void Update()
    {
        HandleBlockInput();
        UpdateBlockingAnimator();
        HandleParryInput();
        HandleAttackInput();
        HandleMagicInput();
        HandleFlaskInput();
    }

    public bool IsBlocking => isBlockingLeft || isBlockingRight;

    private void HandleBlockInput()
    {
        isBlockingRight = false;
        isBlockingLeft = false;

        if (controller == null || controller.Controls == null) return;
        if (controller.IsInventoryOpen || controller.IsRolling) return;
        if (isGuardBroken) return;
        if (isAttacking || rangedActionRoutine != null || isParrying) return;

        UpdateShieldTapHoldState(Hand.Right);
        UpdateShieldTapHoldState(Hand.Left);
    }

    private void UpdateShieldTapHoldState(Hand hand)
    {
        if (!UsesShieldTapHoldBehavior(hand))
        {
            ClearShieldPendingState(hand);
            return;
        }

        InputAction action = hand == Hand.Right
            ? controller.Controls.Player.LightAttackRight
            : controller.Controls.Player.LightAttackLeft;

        if (action == null)
        {
            ClearShieldPendingState(hand);
            return;
        }

        bool pending = hand == Hand.Right ? pendingShieldRightAction : pendingShieldLeftAction;
        float startTime = hand == Hand.Right ? pendingShieldRightStartTime : pendingShieldLeftStartTime;

        if (!pending && action.WasPerformedThisFrame())
        {
            SetShieldPendingState(hand, true, Time.time);
            pending = true;
            startTime = Time.time;
        }

        if (!pending)
            return;

        if (action.WasReleasedThisFrame())
        {
            bool shortTap = Time.time < startTime + Mathf.Max(0f, shieldBlockHoldThreshold);
            ClearShieldPendingState(hand);
            if (shortTap && canAttack && !isAttacking && rangedActionRoutine == null)
                TryAttack(hand, AttackType.Light);
            return;
        }

        if (action.IsPressed() && Time.time >= startTime + Mathf.Max(0f, shieldBlockHoldThreshold))
        {
            if (hand == Hand.Right)
                isBlockingRight = true;
            else
                isBlockingLeft = true;
        }
    }

    private void SetShieldPendingState(Hand hand, bool pending, float startTime)
    {
        if (hand == Hand.Right)
        {
            pendingShieldRightAction = pending;
            pendingShieldRightStartTime = startTime;
        }
        else
        {
            pendingShieldLeftAction = pending;
            pendingShieldLeftStartTime = startTime;
        }
    }

    private void ClearShieldPendingState(Hand hand)
    {
        SetShieldPendingState(hand, false, -999f);
    }

    private bool UsesShieldTapHoldBehavior(Hand hand)
    {
        if (inventory == null)
            return false;

        WeaponItem equipped = inventory.GetWeaponForHand(hand);
        return equipped != null && equipped.category == WeaponCategory.Shield && equipped.canBlock;
    }

    private bool UsesShieldParryBehavior(Hand hand)
    {
        if (inventory == null)
            return false;

        WeaponItem equipped = inventory.GetWeaponForHand(hand);
        return equipped != null && equipped.category == WeaponCategory.Shield && equipped.canParry;
    }

    public bool TryDefendIncomingDamage(ref float amount, WeaponItem.DamageType damageType = WeaponItem.DamageType.Physical, Vector3? sourcePosition = null, Transform attacker = null)
    {
        if (TryParryIncomingDamage(ref amount, sourcePosition, attacker))
            return true;

        return TryBlockIncomingDamage(ref amount, damageType, sourcePosition);
    }

    public bool TryBlockIncomingDamage(ref float amount, WeaponItem.DamageType damageType = WeaponItem.DamageType.Physical, Vector3? sourcePosition = null)
    {
        if (amount <= 0f || stats == null)
            return false;

        WeaponItem blockingShield = GetActiveBlockingShield();
        if (blockingShield == null)
            return false;
        if (sourcePosition.HasValue && !IsWithinBlockAngle(sourcePosition.Value))
            return false;

        float stabilityFactor = 1f - Mathf.Clamp01(blockingShield.stability * Mathf.Max(0f, blockStabilityScale));
        float staminaCost = Mathf.Max(minimumBlockStaminaCost, amount * Mathf.Max(0.1f, stabilityFactor));
        if (!stats.HasStamina(staminaCost))
        {
            TriggerGuardBreak();
            return false;
        }

        stats.SpendStamina(staminaCost);
        float blockedPercent = damageType == WeaponItem.DamageType.Magic
            ? Mathf.Clamp01(blockingShield.magicBlockPercent)
            : Mathf.Clamp01(blockingShield.physicalBlockPercent);
        amount *= (1f - blockedPercent);
        if (stats.currentStamina <= 0.01f)
            TriggerGuardBreak();
        return true;
    }

    private bool TryParryIncomingDamage(ref float amount, Vector3? sourcePosition, Transform attacker)
    {
        if (!isParryWindowOpen)
            return false;
        if (attacker == null)
            return false;
        if (sourcePosition.HasValue && !IsWithinBlockAngle(sourcePosition.Value))
            return false;

        amount = 0f;
        CompleteParry(attacker);
        return true;
    }

    private bool IsWithinBlockAngle(Vector3 sourcePosition)
    {
        Vector3 toSource = sourcePosition - transform.position;
        toSource.y = 0f;
        if (toSource.sqrMagnitude <= 0.0001f)
            return true;

        float angle = Vector3.Angle(transform.forward, toSource.normalized);
        return angle <= Mathf.Clamp(blockFrontAngle, 1f, 180f) * 0.5f;
    }

    private WeaponItem GetActiveBlockingShield()
    {
        WeaponItem best = null;

        if (isBlockingLeft)
        {
            var left = inventory != null ? inventory.GetWeaponForHand(Hand.Left) : null;
            if (left != null && left.category == WeaponCategory.Shield && left.canBlock)
                best = left;
        }

        if (isBlockingRight)
        {
            var right = inventory != null ? inventory.GetWeaponForHand(Hand.Right) : null;
            if (right != null && right.category == WeaponCategory.Shield && right.canBlock)
            {
                if (best == null || right.physicalBlockPercent > best.physicalBlockPercent)
                    best = right;
            }
        }

        return best;
    }

    private void TriggerGuardBreak()
    {
        if (isGuardBroken)
            return;

        isBlockingRight = false;
        isBlockingLeft = false;
        ClearShieldPendingState(Hand.Right);
        ClearShieldPendingState(Hand.Left);
        CancelParryState();
        UpdateBlockingAnimator();

        if (guardBreakRoutine != null)
            StopCoroutine(guardBreakRoutine);
        guardBreakRoutine = StartCoroutine(GuardBreakRoutine());
    }

    private IEnumerator GuardBreakRoutine()
    {
        isGuardBroken = true;
        canAttack = false;
        if (controller != null)
        {
            controller.canMove = false;
            controller.StopMovementImmediate();
        }

        yield return new WaitForSeconds(Mathf.Max(0.05f, guardBreakDuration));

        if (controller != null && !controller.IsInventoryOpen)
            controller.canMove = true;
        canAttack = true;
        isGuardBroken = false;
        guardBreakRoutine = null;
    }

    private void CacheAnimatorParameters()
    {
        hasBlockingAnimatorParameter = AnimatorHasParameter(blockingAnimatorParameter);
        hasParryAnimatorTrigger = AnimatorHasParameter(parryAnimatorTrigger, AnimatorControllerParameterType.Trigger);
    }

    private bool AnimatorHasParameter(string parameterName, AnimatorControllerParameterType? expectedType = null)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
            return false;

        var parameters = animator.parameters;
        if (parameters == null)
            return false;

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name != parameterName)
                continue;
            if (expectedType.HasValue && parameters[i].type != expectedType.Value)
                continue;
            if (!expectedType.HasValue && parameters[i].type != AnimatorControllerParameterType.Bool)
                continue;
            return true;
        }

        return false;
    }

    private void UpdateBlockingAnimator()
    {
        if (animator == null || !hasBlockingAnimatorParameter)
            return;

        animator.SetBool(blockingAnimatorParameter, IsBlocking && !isGuardBroken);
    }

    private void HandleParryInput()
    {
        if (controller == null || controller.Controls == null) return;
        if (controller.IsInventoryOpen || controller.IsRolling) return;
        if (isGuardBroken || isAttacking || rangedActionRoutine != null || isParrying) return;

        if (UsesShieldParryBehavior(Hand.Right) && controller.Controls.Player.HeavyAttackRight.WasPerformedThisFrame())
        {
            StartParry(Hand.Right);
            return;
        }

        if (UsesShieldParryBehavior(Hand.Left) && controller.Controls.Player.HeavyAttackLeft.WasPerformedThisFrame())
        {
            StartParry(Hand.Left);
            return;
        }
    }

    private void StartParry(Hand hand)
    {
        if (inventory == null)
            return;

        WeaponItem shield = inventory.GetWeaponForHand(hand);
        if (shield == null || shield.category != WeaponCategory.Shield || !shield.canParry)
            return;

        if (parryRoutine != null)
            StopCoroutine(parryRoutine);
        parryRoutine = StartCoroutine(ParryRoutine(shield));
    }

    private IEnumerator ParryRoutine(WeaponItem shield)
    {
        isParrying = true;
        isAttacking = true;
        isParryWindowOpen = false;
        isBlockingLeft = false;
        isBlockingRight = false;
        ClearShieldPendingState(Hand.Right);
        ClearShieldPendingState(Hand.Left);
        UpdateBlockingAnimator();

        if (controller != null)
            controller.StopMovementImmediate();
        if (animator != null && hasParryAnimatorTrigger)
            animator.SetTrigger(parryAnimatorTrigger);

        float start = Mathf.Max(0f, shield.parryWindowStart);
        float duration = Mathf.Max(0.01f, shield.parryWindowDuration);
        float totalLock = Mathf.Max(parryTotalLockTime, start + duration + Mathf.Max(0f, parryRecoveryTime));

        if (start > 0f)
            yield return new WaitForSeconds(start);

        isParryWindowOpen = true;
        yield return new WaitForSeconds(duration);
        isParryWindowOpen = false;

        float remaining = Mathf.Max(0f, totalLock - (start + duration));
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        isParrying = false;
        isAttacking = false;
        parryRoutine = null;
    }

    private void CancelParryState()
    {
        if (parryRoutine != null)
            StopCoroutine(parryRoutine);

        isParryWindowOpen = false;
        isParrying = false;
        parryRoutine = null;
    }

    private void CompleteParry(Transform attacker)
    {
        CancelParryState();
        isAttacking = false;

        if (attacker == null)
            return;

        var parryable = attacker.GetComponent<SimpleEnemyAI>();
        if (parryable == null)
            parryable = attacker.GetComponentInParent<SimpleEnemyAI>();
        if (parryable != null)
            parryable.ApplyParryStagger(parryStaggerDuration);
    }

    void HandleAttackInput()
    {
        if (controller == null || controller.Controls == null) return;

        // Se l'inventario è aperto, non fare nulla
        if (controller != null && controller.IsInventoryOpen) return;

        // Se stiamo rollando, niente attacchi
        if (controller != null && controller.IsRolling) return;
        
        // Se stiamo già attaccando o siamo bloccati, esci
        if (!canAttack || isAttacking || rangedActionRoutine != null) return;

        if (TryHandleWeaponThrowInput())
            return;

        if (!UsesShieldTapHoldBehavior(Hand.Right) && controller.Controls.Player.LightAttackRight.WasPerformedThisFrame())
            TryAttack(Hand.Right, AttackType.Light);

        if (!UsesShieldTapHoldBehavior(Hand.Left) && controller.Controls.Player.LightAttackLeft.WasPerformedThisFrame())
            TryAttack(Hand.Left, AttackType.Light);

        if (!UsesShieldParryBehavior(Hand.Right) && controller.Controls.Player.HeavyAttackRight.WasPerformedThisFrame())
            TryAttack(Hand.Right, AttackType.Heavy);

        if (!UsesShieldParryBehavior(Hand.Left) && controller.Controls.Player.HeavyAttackLeft.WasPerformedThisFrame())
            TryAttack(Hand.Left, AttackType.Heavy);
    }

    void HandleFlaskInput()
    {
        if (controller == null || controller.Controls == null) return;
        if (controller.IsInventoryOpen) return;
        if (!controller.Controls.Player.UseFlask.WasPerformedThisFrame()) return;

        bool isRolling = controller.IsRolling;
        bool isGrounded = controller.IsGrounded;

        if (isRolling || isAttacking || !isGrounded) return;
        if (inventory == null || stats == null) return;
        if (stats.IsUsableOnCooldown()) return;

        if (!inventory.TryPeekCurrentUsable(out var usable, out _)) return;
        if (usable == null) return;
        if (!stats.TryApplyUsableEffect(usable)) return;

        if (!inventory.TryConsumeCurrentUsable(out _, out int remainingAmount))
            return;

        stats.SetFlaskCountVisual(remainingAmount);
        controller?.menuManager?.RefreshEquipmentUI();
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

        if (weapon.category == WeaponCategory.Wand)
        {
            TryCastWithWand(weapon, hand, type);
            return;
        }
        if (weapon.category == WeaponCategory.Bow)
        {
            TryShootBow(weapon, hand, type);
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

    private bool TryHandleWeaponThrowInput()
    {
        if (!enableWeaponThrow) return false;
        if (controller == null || controller.Controls == null) return false;
        if (controller.IsInventoryOpen || controller.IsRolling) return false;

        var gp = Gamepad.current;
        if (gp == null || !gp.buttonNorth.isPressed) return false; // Triangle hold

        if (controller.Controls.Player.LightAttackRight.WasPerformedThisFrame())
            return TryThrowWeaponFromHand(Hand.Right);
        if (controller.Controls.Player.LightAttackLeft.WasPerformedThisFrame())
            return TryThrowWeaponFromHand(Hand.Left);

        return false;
    }

    private bool TryThrowWeaponFromHand(Hand hand)
    {
        if (inventory == null || stats == null) return false;

        WeaponItem equipped = hand == Hand.Right ? inventory.GetCurrentRightWeapon() : inventory.GetCurrentLeftWeapon();
        if (equipped == null) return false;
        if (hand == Hand.Right && equipped == inventory.unarmedRight) return false;
        if (hand == Hand.Left && equipped == inventory.unarmedLeft) return false;
        if (!equipped.canBeThrown) return false;
        if (equipped.throwProjectilePrefab == null) return false;
        if (equipped.throwStrengthRequirement > 0 && stats.strength < equipped.throwStrengthRequirement) return false;

        float staminaCost = Mathf.Max(0f, equipped.throwStaminaCost);
        if (!stats.HasStamina(staminaCost)) return false;

        string instanceId = inventory.GetCurrentWeaponInstanceId(hand);
        if (string.IsNullOrWhiteSpace(instanceId)) return false;
        if (!inventory.HasWeaponInstanceInInventoryPublic(instanceId, equipped)) return false;

        if (!inventory.TryUnequipCurrentWeaponForThrow(hand, out var thrownWeapon, out var thrownInstanceId))
            return false;

        if (!inventory.TryRemoveWeaponInstanceFromInventory(thrownInstanceId, thrownWeapon))
        {
            // rollback soft: rimetti l'arma nello slot se non è stato possibile rimuoverla dall'inventario
            if (hand == Hand.Right)
                inventory.SetRightAtSlot(inventory.currentRightIndex, thrownWeapon, thrownInstanceId);
            else
                inventory.SetLeftAtSlot(inventory.currentLeftIndex, thrownWeapon, thrownInstanceId);
            return false;
        }

        stats.SpendStamina(staminaCost);

        var computed = ComputeAttackDamage(thrownWeapon, AttackType.Light);
        Vector3 fireDir = GetThrowDirection();
        Vector3 spawnPos = transform.position
                           + transform.forward * throwSpawnForwardOffset
                           + transform.up * throwSpawnHeightOffset;
        float speed = thrownWeapon.throwSpeed > 0f ? thrownWeapon.throwSpeed : fallbackProjectileSpeed;
        float life = thrownWeapon.throwLifetime > 0f ? thrownWeapon.throwLifetime : fallbackProjectileLifetime;
        SpawnThrownWeaponProjectile(thrownWeapon, thrownInstanceId, spawnPos, fireDir, computed.damage, speed, life, throwHitMask);

        controller?.menuManager?.RefreshEquipmentUI();

        StartRangedAction(0f, throwMinLockTime, null);
        return true;
    }

    private void TryCastWithWand(WeaponItem wand, Hand hand, AttackType type)
    {
        if (wand == null || stats == null) return;
        if (rangedActionRoutine != null) return;
        if (controller != null && (controller.IsRolling || controller.IsInventoryOpen)) return;

        int handIndex = hand == Hand.Right ? 0 : 1;
        Vector3 fireDir = GetMagicFireDirection();
        float staminaCost = (type == AttackType.Light) ? wand.lightAttackStaminaCost : wand.heavyAttackStaminaCost;
        if (!stats.HasStamina(staminaCost)) return;

        if (type == AttackType.Light)
        {
            GameObject projectilePrefab = wand.wandLightProjectilePrefab;
            if (projectilePrefab == null)
            {
                var equippedMagic = inventory != null ? inventory.GetCurrentMagic() : null;
                if (equippedMagic != null) projectilePrefab = equippedMagic.projectilePrefab;
            }
            if (projectilePrefab == null) return;

            float cooldown = Mathf.Max(0f, wand.wandLightCooldown);
            if (Time.time < lastWandLightCastTime[handIndex] + cooldown) return;

            var computed = ComputeAttackDamage(wand, AttackType.Light);
            int damage = computed.damage;
            Vector3 spawnPos = GetSpawnPosition(magicCastPoint, wand.wandLightSpawnOffset, fireDir);
            if (!TryPlayWeaponActionAnimation(wand, hand, AttackType.Light, out string lightAnim))
                return;

            lastWandLightCastTime[handIndex] = Time.time;
            float lightClipLength = GetAnimationClipLength(lightAnim);
            StartRangedAction(ResolveActionWindup(lightAnim, wandLightCastWindup, 0.5f), lightClipLength, () =>
            {
                if (!stats.UseMana(Mathf.Max(0f, wand.wandLightManaCost))) return;
                if (!stats.HasStamina(staminaCost)) return;
                stats.SpendStamina(staminaCost);
                string sourceLabel = $"{(string.IsNullOrWhiteSpace(wand.weaponName) ? wand.name : wand.weaponName)} | Hand:{hand} | Type:Light";
                SpawnProjectile(projectilePrefab, spawnPos, fireDir, damage, wand.wandLightProjectileSpeed, wand.wandLightProjectileLifetime, wand.wandHitMask, sourceLabel, computed.isCritical);
            });
            return;
        }

        MagicItemData equipped = inventory != null ? inventory.GetCurrentMagic() : null;
        if (!CanCastEquippedMagic(equipped)) return;

        float heavyCooldown = equipped.castCooldown > 0f ? equipped.castCooldown : fallbackMagicCooldown;
        if (Time.time < lastWandHeavyCastTime[handIndex] + heavyCooldown) return;

        if (!TryPlayWeaponActionAnimation(wand, hand, AttackType.Heavy, out string heavyAnim))
            return;

        lastWandHeavyCastTime[handIndex] = Time.time;
        float heavyClipLength = GetAnimationClipLength(heavyAnim);
        float magicCastTime = Mathf.Max(0f, equipped.castTime);
        float heavyWindup = Mathf.Max(ResolveActionWindup(heavyAnim, wandHeavyCastWindup, 0.58f), magicCastTime);
        StartRangedAction(heavyWindup, heavyClipLength, () =>
        {
            if (!stats.UseMana(Mathf.Max(0f, equipped.manaCost))) return;
            if (!stats.HasStamina(staminaCost)) return;
            stats.SpendStamina(staminaCost);
            string sourceLabel = $"{(string.IsNullOrWhiteSpace(wand.weaponName) ? wand.name : wand.weaponName)} | Hand:{hand} | Type:Heavy(Magic:{equipped.magicName})";
            ExecuteEquippedMagic(equipped, fireDir, sourceLabel);
        });
    }

    private void TryShootBow(WeaponItem bow, Hand hand, AttackType type)
    {
        if (bow == null || stats == null || inventory == null) return;
        if (rangedActionRoutine != null) return;
        if (controller != null && (controller.IsRolling || controller.IsInventoryOpen)) return;
        if (bow.bowProjectilePrefab == null || bow.bowAmmoItem == null) return;

        int handIndex = hand == Hand.Right ? 0 : 1;
        float cooldown = Mathf.Max(0f, bow.bowShotCooldown);
        if (Time.time < lastBowShotTime[handIndex] + cooldown) return;

        float staminaCost = (type == AttackType.Light) ? bow.lightAttackStaminaCost : bow.heavyAttackStaminaCost;
        if (!stats.HasStamina(staminaCost)) return;

        int ammoCount = inventory.GetAmmoCountForWeapon(bow);
        if (ammoCount <= 0) return;

        Vector3 fireDir = GetMagicFireDirection();
        Vector3 spawnPos = GetSpawnPosition(magicCastPoint, bow.bowSpawnOffset, fireDir);
        var computed = ComputeAttackDamage(bow, type);
        int damage = computed.damage;
        if (!TryPlayWeaponActionAnimation(bow, hand, type, out string bowAnim))
            return;

        float windup = type == AttackType.Heavy ? bowHeavyShotWindup : bowLightShotWindup;
        float normalizedFirePoint = type == AttackType.Heavy ? 0.62f : 0.52f;
        lastBowShotTime[handIndex] = Time.time;
        float bowClipLength = GetAnimationClipLength(bowAnim);
        StartRangedAction(ResolveActionWindup(bowAnim, windup, normalizedFirePoint), bowClipLength, () =>
        {
            if (!stats.HasStamina(staminaCost)) return;
            if (!inventory.TryConsumeItem(bow.bowAmmoItem, 1, out _)) return;

            stats.SpendStamina(staminaCost);
            SpawnProjectile(
                bow.bowProjectilePrefab,
                spawnPos,
                fireDir,
                damage,
                bow.bowProjectileSpeed > 0f ? bow.bowProjectileSpeed : fallbackProjectileSpeed,
                bow.bowProjectileLifetime > 0f ? bow.bowProjectileLifetime : fallbackProjectileLifetime,
                bow.bowHitMask,
                $"{(string.IsNullOrWhiteSpace(bow.weaponName) ? bow.name : bow.weaponName)} | Hand:{hand} | Type:{type}",
                computed.isCritical
            );

            controller?.menuManager?.RefreshEquipmentUI();
        });
    }

    private bool TryPlayWeaponActionAnimation(WeaponItem weapon, Hand hand, AttackType type, out string animToPlay)
    {
        animToPlay = null;
        if (weapon == null || weapon.animationProfile == null || animator == null) return false;
        bool isAirAttack = controller != null && !controller.IsGrounded;
        animToPlay = GetAttackAnimation(weapon.animationProfile, hand, type, isAirAttack);
        if (string.IsNullOrWhiteSpace(animToPlay)) return false;

        int stateHash = Animator.StringToHash(animToPlay);
        if (!animator.HasState(0, stateHash))
        {
            Debug.LogWarning($"[PlayerCombat] Stato animazione non trovato: '{animToPlay}'. Colpo annullato.");
            return false;
        }

        animator.CrossFadeInFixedTime(animToPlay, 0.08f);
        return true;
    }

    private float ResolveActionWindup(string animName, float fallbackWindup, float firePointNormalized)
    {
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(animName))
            return Mathf.Max(0f, fallbackWindup);

        var clips = animator.runtimeAnimatorController.animationClips;
        if (clips == null || clips.Length == 0)
            return Mathf.Max(0f, fallbackWindup);

        float clipLength = -1f;
        for (int i = 0; i < clips.Length; i++)
        {
            var clip = clips[i];
            if (clip == null) continue;
            if (clip.name == animName)
            {
                clipLength = clip.length;
                break;
            }
        }

        if (clipLength <= 0f)
            return Mathf.Max(0f, fallbackWindup);

        float normalized = Mathf.Clamp(firePointNormalized, 0.1f, 0.9f);
        float byClip = clipLength * normalized;
        return Mathf.Max(Mathf.Max(0f, fallbackWindup), byClip);
    }

    private float GetAnimationClipLength(string animName)
    {
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(animName))
            return 0f;

        var clips = animator.runtimeAnimatorController.animationClips;
        if (clips == null || clips.Length == 0)
            return 0f;

        for (int i = 0; i < clips.Length; i++)
        {
            var clip = clips[i];
            if (clip == null) continue;
            if (clip.name == animName)
                return Mathf.Max(0f, clip.length);
        }

        return 0f;
    }

    private void StartRangedAction(float windup, float clipLength, System.Action onFire)
    {
        if (rangedActionRoutine != null)
            return;
        rangedActionRoutine = StartCoroutine(RangedActionRoutine(windup, clipLength, onFire));
    }

    private IEnumerator RangedActionRoutine(float windup, float clipLength, System.Action onFire)
    {
        if (controller != null)
            controller.StopMovementImmediate();

        rangedAttackLockActive = true;
        isAttacking = true;
        if (windup > 0f)
            yield return new WaitForSeconds(windup);

        onFire?.Invoke();

        float remainingAnimTime = 0f;
        if (lockRangedUntilAnimationEnds && clipLength > 0f)
            remainingAnimTime = Mathf.Max(0f, clipLength - Mathf.Max(0f, windup));

        float minAfterFireFromTotalLock = Mathf.Max(0f, rangedMinTotalLockTime - Mathf.Max(0f, windup));
        float waitAfterFire = Mathf.Max(remainingAnimTime, rangedRecoveryTime, minAfterFireFromTotalLock);
        if (waitAfterFire > 0f)
            yield return new WaitForSeconds(waitAfterFire);

        rangedAttackLockActive = false;
        isAttacking = false;
        rangedActionRoutine = null;
    }

    private Vector3 GetSpawnPosition(Transform castPoint, Vector3 localOffset, Vector3 launchDirection)
    {
        Vector3 basePosition;
        if (castPoint != null)
        {
            // Il cast point e' gia' vicino alla mano/arco.
            // Applica solo l'offset laterale/verticale pieno e riduci molto il forward
            // per evitare che il proiettile nasca gia' oltre un bersaglio vicino.
            Vector3 sideUpOffset =
                castPoint.right * localOffset.x +
                castPoint.up * localOffset.y;

            float forwardOffset = localOffset.z * Mathf.Max(0f, castPointForwardOffsetScale);
            basePosition = castPoint.position + sideUpOffset + castPoint.forward * forwardOffset;
        }
        else
            basePosition = transform.position + transform.TransformDirection(localOffset);

        Vector3 direction = launchDirection.sqrMagnitude > 0.0001f ? launchDirection.normalized : transform.forward;
        return basePosition + transform.up * magicCastPointHeightOffset - direction * Mathf.Max(0f, rangedSpawnBackOffset);
    }

    private void SpawnProjectile(GameObject projectilePrefab, Vector3 spawnPos, Vector3 fireDir, int damage, float speed, float lifetime, LayerMask hitMask, string sourceLabel = "Projectile", bool isCritical = false)
    {
        if (projectilePrefab == null) return;

        Quaternion rotation = fireDir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(fireDir) : transform.rotation;
        GameObject projectileObj = Instantiate(projectilePrefab, spawnPos, rotation);
        MagicProjectile projectile = projectileObj.GetComponent<MagicProjectile>();
        if (projectile == null)
            projectile = projectileObj.AddComponent<MagicProjectile>();

        float finalSpeed = speed > 0f ? speed : fallbackProjectileSpeed;
        float finalLifetime = lifetime > 0f ? lifetime : fallbackProjectileLifetime;
        projectile.Initialize(transform, fireDir, Mathf.Max(1, damage), finalSpeed, finalLifetime, hitMask, sourceLabel, isCritical);
    }

    private void SpawnThrownWeaponProjectile(WeaponItem weapon, string instanceId, Vector3 spawnPos, Vector3 fireDir, int damage, float speed, float lifetime, LayerMask hitMask)
    {
        if (weapon == null || string.IsNullOrWhiteSpace(instanceId)) return;

        GameObject prefab = weapon.throwProjectilePrefab;
        if (prefab == null)
        {
            Debug.LogError($"[PlayerCombat] throwProjectilePrefab mancante su arma '{weapon.weaponName}'.");
            return;
        }

        Quaternion rot = fireDir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(fireDir) : transform.rotation;
        GameObject go = Instantiate(prefab, spawnPos, rot);
        var col = go.GetComponent<Collider>();
        if (col != null)
        {
            // Evita nascita in overlap col terreno quando il collider del prefab è alto.
            float lift = Mathf.Max(0.05f, col.bounds.extents.y * 0.4f);
            go.transform.position += Vector3.up * lift;
        }
        var throwProj = go.GetComponent<WeaponThrowProjectile>();
        if (throwProj == null)
        {
            Debug.LogError($"[PlayerCombat] Prefab '{prefab.name}' senza WeaponThrowProjectile.");
            Destroy(go);
            return;
        }

        throwProj.Initialize(transform, weapon, instanceId, fireDir, Mathf.Max(1, damage), speed, lifetime, hitMask);
    }

    private Vector3 GetMagicFireDirection()
    {
        if (targetLockSystem == null)
            targetLockSystem = GetComponentInChildren<TargetLockSystem>(true) ?? GetComponentInParent<TargetLockSystem>();

        if (targetLockSystem != null && targetLockSystem.isLockedOn && targetLockSystem.currentTarget != null)
        {
            Vector3 origin = magicCastPoint != null ? magicCastPoint.position : transform.position + Vector3.up * 1.2f;
            Vector3 targetPoint = targetLockSystem.CurrentLockAimPoint;
            Vector3 toTarget = targetPoint - origin;
            if (toTarget.sqrMagnitude > 0.0001f)
                return toTarget.normalized;
        }

        return transform.forward;
    }

    private Vector3 GetThrowDirection()
    {
        Vector3 baseDir = transform.forward;

        if (targetLockSystem == null)
            targetLockSystem = GetComponentInChildren<TargetLockSystem>(true) ?? GetComponentInParent<TargetLockSystem>();

        if (targetLockSystem != null && targetLockSystem.isLockedOn && targetLockSystem.currentTarget != null)
        {
            Vector3 toTarget = targetLockSystem.CurrentLockAimPoint - transform.position;
            toTarget.y = 0f; // throw più stabile, evita impennate verticali
            if (toTarget.sqrMagnitude > 0.0001f)
                baseDir = toTarget.normalized;
        }

        baseDir.y += throwArcUpBias;
        if (baseDir.sqrMagnitude <= 0.0001f) return transform.forward;
        return baseDir.normalized;
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

        var computed = ComputeAttackDamage(weapon, type);
        if (animationEvents != null)
            animationEvents.PrepareAttackDamage(hand, computed.damage, computed.isCritical, type);

        // 1. Ferma il movimento (Feeling Souls-like)
        if (controller != null) controller.StopMovementImmediate();

        // 2. Setta flag
        isAttacking = true;

        // 3. Lancia animazione (CrossFade basso per reattività istantanea)
        animator.CrossFadeInFixedTime(animToPlay, 0.1f);
        float clipLength = GetAnimationClipLength(animToPlay);
        StartMeleeUnlockFallback(clipLength > 0f ? clipLength : fallbackMeleeUnlockTime);
    }

    void HandleMagicInput()
    {
        if (!enableMagicCastPrototype) return;
        if (controller == null || controller.Controls == null) return;
        if (controller.IsInventoryOpen) return;
        if (controller.IsRolling || isAttacking || !canAttack) return;

        if (Keyboard.current == null || !Keyboard.current[magicCastKey].wasPressedThisFrame) return;

        MagicItemData equippedMagic = inventory != null ? inventory.GetCurrentMagic() : null;
        if (!CanCastEquippedMagic(equippedMagic)) return;

        float cooldown = equippedMagic.castCooldown > 0f ? equippedMagic.castCooldown : fallbackMagicCooldown;
        if (Time.time < lastMagicCastTime + cooldown) return;

        float manaCost = Mathf.Max(0f, equippedMagic.manaCost);
        if (stats != null && !stats.UseMana(manaCost)) return;

        float castTime = Mathf.Max(0f, equippedMagic.castTime);
        lastMagicCastTime = Time.time;
        StartRangedAction(castTime, 0f, () =>
        {
            string sourceLabel = $"MagicCastKey | Magic:{equippedMagic.magicName}";
            ExecuteEquippedMagic(equippedMagic, GetMagicFireDirection(), sourceLabel);
        });
    }

    private bool CanCastEquippedMagic(MagicItemData magic)
    {
        if (magic == null) return false;

        switch (magic.effectType)
        {
            case MagicItemData.MagicEffectType.Damage:
                return magic.projectilePrefab != null;
            case MagicItemData.MagicEffectType.HealHealth:
            case MagicItemData.MagicEffectType.RestoreMana:
                return magic.healAmount > 0;
            case MagicItemData.MagicEffectType.BoostAttribute:
                return magic.boostAttribute != MagicItemData.BoostAttribute.None && magic.boostAmount != 0;
            default:
                return false;
        }
    }

    private void ExecuteEquippedMagic(MagicItemData magic, Vector3 fireDir, string sourceLabel)
    {
        if (magic == null || stats == null) return;

        if (magic.effectType == MagicItemData.MagicEffectType.Damage)
        {
            int finalMagicDamage = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0, stats.GetBaseMagicDamage()) + Mathf.Max(0, magic.magicDamage)));
            Vector3 spawnPos = GetSpawnPosition(magicCastPoint, magic.spawnOffset, fireDir);
            float speed = magic.projectileSpeed > 0f ? magic.projectileSpeed : fallbackProjectileSpeed;
            float lifetime = magic.projectileLifetime > 0f ? magic.projectileLifetime : fallbackProjectileLifetime;
            SpawnProjectile(magic.projectilePrefab, spawnPos, fireDir, finalMagicDamage, speed, lifetime, magic.hitMask, sourceLabel, false);
            return;
        }

        stats.TryApplyMagicEffect(magic);
        controller?.menuManager?.RefreshEquipmentUI();
    }

    private (int damage, bool isCritical) ComputeAttackDamage(WeaponItem weapon, AttackType type)
    {
        if (weapon == null || stats == null)
            return (0, false);

        bool isRangedWeapon = weapon.rangeType == WeaponItem.WeaponRangeType.Ranged;
        bool isMagicWeapon = weapon.damageType == WeaponItem.DamageType.Magic;
        float playerBaseDamage = isRangedWeapon
            ? Mathf.Max(0, stats.GetBaseRangedDamage())
            : (isMagicWeapon
                ? Mathf.Max(0, stats.GetBaseMagicDamage())
                : Mathf.Max(0, stats.GetBasePhysicalDamage()));
        float weaponBaseDamage = isMagicWeapon
            ? Mathf.Max(0, weapon.magicDamage)
            : Mathf.Max(0, weapon.physicalDamage);
        float baseDamage = playerBaseDamage + weaponBaseDamage;

        // Lo scaling viene definito nel WeaponData:
        // - Physical: STR/DEX
        // - Magic: INT only
        float scalingBonus = 0f;
        if (isRangedWeapon)
        {
            // Ranged: scala con DEX (archi e affini)
            scalingBonus = Mathf.Max(0f, weapon.GetDexterityScalingFactor())
                           * PlayerStats.GetEffectiveAttributeValue(stats.EffectiveDexterity);
        }
        else if (isMagicWeapon)
        {
            scalingBonus = Mathf.Max(0f, weapon.GetIntelligenceScalingFactor())
                           * PlayerStats.GetEffectiveAttributeValue(stats.EffectiveIntelligence);
        }
        else
        {
            scalingBonus = Mathf.Max(0f, weapon.GetStrengthScalingFactor())
                           * PlayerStats.GetEffectiveAttributeValue(stats.EffectiveStrength)
                           + Mathf.Max(0f, weapon.GetDexterityScalingFactor())
                           * PlayerStats.GetEffectiveAttributeValue(stats.EffectiveDexterity);
        }

        float attackMultiplier = type == AttackType.Heavy
            ? Mathf.Max(0.1f, weapon.heavyDamageMultiplier)
            : Mathf.Max(0.1f, weapon.lightDamageMultiplier);

        float rawDamage = (baseDamage + scalingBonus) * attackMultiplier;

        bool isCritical = false;
        float critChance = Mathf.Clamp01(weapon.criticalChance);
        if (critChance > 0f && Random.value <= critChance)
        {
            isCritical = true;
            rawDamage *= Mathf.Max(1f, weapon.criticalHit);
        }

        // Se i requisiti non sono soddisfatti, il danno effettivo è al 50%.
        if (!HasWeaponRequirements(weapon))
            rawDamage *= 0.5f;

        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(rawDamage));
        return (finalDamage, isCritical);
    }

    private bool HasWeaponRequirements(WeaponItem weapon)
    {
        if (weapon == null || stats == null) return true;

        if (weapon.strengthRequirement > 0 && stats.EffectiveStrength < weapon.strengthRequirement) return false;
        if (weapon.dexterityRequirement > 0 && stats.EffectiveDexterity < weapon.dexterityRequirement) return false;
        if (weapon.intelligenceRequirement > 0 && stats.EffectiveIntelligence < weapon.intelligenceRequirement) return false;
        if (weapon.faithRequirement > 0 && stats.EffectiveFaith < weapon.faithRequirement) return false;

        return true;
    }

    private void StartMeleeUnlockFallback(float duration)
    {
        if (meleeUnlockRoutine != null)
            StopCoroutine(meleeUnlockRoutine);
        meleeUnlockRoutine = StartCoroutine(MeleeUnlockFallbackRoutine(Mathf.Max(0.05f, duration + 0.05f)));
    }

    private IEnumerator MeleeUnlockFallbackRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!rangedAttackLockActive)
            isAttacking = false;
        meleeUnlockRoutine = null;
    }

    // Chiamata dall'Animation Event (tramite PlayerAnimationEvents.cs)
    public void EndAttack()
    {
        if (rangedAttackLockActive)
            return;
        if (meleeUnlockRoutine != null)
        {
            StopCoroutine(meleeUnlockRoutine);
            meleeUnlockRoutine = null;
        }
        isAttacking = false;
    }

    private void OnDisable()
    {
        if (rangedActionRoutine != null)
        {
            StopCoroutine(rangedActionRoutine);
            rangedActionRoutine = null;
        }
        if (meleeUnlockRoutine != null)
        {
            StopCoroutine(meleeUnlockRoutine);
            meleeUnlockRoutine = null;
        }
        rangedAttackLockActive = false;
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
