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
    private PlayerController controller;
    private PlayerAnimationEvents animationEvents;
    private TargetLockSystem targetLockSystem;

    [Header("Stato Combattimento")]
    public bool isAttacking = false;
    public bool canAttack = true;

    [Header("Magic Cast (Prototype)")]
    [SerializeField] private bool enableMagicCastPrototype = false;
    [SerializeField] private Transform magicCastPoint;
    [SerializeField] private float magicCastPointHeightOffset = -0.35f;
    [SerializeField] private Key magicCastKey = Key.C;
    [SerializeField] private float fallbackMagicCooldown = 0.45f;
    [SerializeField] private float fallbackProjectileSpeed = 18f;
    [SerializeField] private float fallbackProjectileLifetime = 4f;
    private float lastMagicCastTime = -999f;
    private readonly float[] lastWandLightCastTime = { -999f, -999f };
    private readonly float[] lastWandHeavyCastTime = { -999f, -999f };
    private readonly float[] lastBowShotTime = { -999f, -999f };

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
    }

    void Update()
    {
        HandleAttackInput();
        HandleMagicInput();
        HandleFlaskInput();
    }

    void HandleAttackInput()
    {
        if (controller == null || controller.Controls == null) return;

        // Se l'inventario è aperto, non fare nulla
        if (controller != null && controller.IsInventoryOpen) return;

        // Se stiamo rollando, niente attacchi
        if (controller != null && controller.IsRolling) return;
        
        // Se stiamo già attaccando o siamo bloccati, esci
        if (!canAttack || isAttacking) return;

        if (controller.Controls.Player.LightAttackRight.WasPerformedThisFrame())
            TryAttack(Hand.Right, AttackType.Light);

        if (controller.Controls.Player.LightAttackLeft.WasPerformedThisFrame())
            TryAttack(Hand.Left, AttackType.Light);

        if (controller.Controls.Player.HeavyAttackRight.WasPerformedThisFrame())
            TryAttack(Hand.Right, AttackType.Heavy);

        if (controller.Controls.Player.HeavyAttackLeft.WasPerformedThisFrame())
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
        if (controller.inventoryUI != null)
            controller.inventoryUI.RefreshEquipmentCross();
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

    private void TryCastWithWand(WeaponItem wand, Hand hand, AttackType type)
    {
        if (wand == null || stats == null) return;
        if (controller != null && (!controller.IsGrounded || controller.IsRolling || controller.IsInventoryOpen)) return;

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

            if (!stats.UseMana(Mathf.Max(0f, wand.wandLightManaCost))) return;
            stats.SpendStamina(staminaCost);

            int damage = ComputeAttackDamage(wand, AttackType.Light).damage;
            Vector3 spawnPos = GetSpawnPosition(magicCastPoint, wand.wandLightSpawnOffset);
            PlayWeaponActionAnimation(wand, hand, AttackType.Light);
            SpawnProjectile(projectilePrefab, spawnPos, fireDir, damage, wand.wandLightProjectileSpeed, wand.wandLightProjectileLifetime, wand.wandHitMask);
            lastWandLightCastTime[handIndex] = Time.time;
            return;
        }

        MagicItemData equipped = inventory != null ? inventory.GetCurrentMagic() : null;
        if (equipped == null || equipped.projectilePrefab == null) return;

        float heavyCooldown = equipped.castCooldown > 0f ? equipped.castCooldown : fallbackMagicCooldown;
        if (Time.time < lastWandHeavyCastTime[handIndex] + heavyCooldown) return;
        if (!stats.UseMana(Mathf.Max(0f, equipped.manaCost))) return;
        stats.SpendStamina(staminaCost);

        int finalMagicDamage = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0, stats.GetBaseMagicDamage()) + Mathf.Max(0, equipped.magicDamage)));
        Vector3 heavySpawnPos = GetSpawnPosition(magicCastPoint, equipped.spawnOffset);
        float heavySpeed = equipped.projectileSpeed > 0f ? equipped.projectileSpeed : fallbackProjectileSpeed;
        float heavyLifetime = equipped.projectileLifetime > 0f ? equipped.projectileLifetime : fallbackProjectileLifetime;
        PlayWeaponActionAnimation(wand, hand, AttackType.Heavy);
        SpawnProjectile(equipped.projectilePrefab, heavySpawnPos, fireDir, finalMagicDamage, heavySpeed, heavyLifetime, equipped.hitMask);
        lastWandHeavyCastTime[handIndex] = Time.time;
    }

    private void TryShootBow(WeaponItem bow, Hand hand, AttackType type)
    {
        if (bow == null || stats == null || inventory == null) return;
        if (controller != null && (!controller.IsGrounded || controller.IsRolling || controller.IsInventoryOpen)) return;
        if (bow.bowProjectilePrefab == null || bow.bowAmmoItem == null) return;

        int handIndex = hand == Hand.Right ? 0 : 1;
        float cooldown = Mathf.Max(0f, bow.bowShotCooldown);
        if (Time.time < lastBowShotTime[handIndex] + cooldown) return;

        float staminaCost = (type == AttackType.Light) ? bow.lightAttackStaminaCost : bow.heavyAttackStaminaCost;
        if (!stats.HasStamina(staminaCost)) return;

        int ammoCount = inventory.GetAmmoCountForWeapon(bow);
        if (ammoCount <= 0) return;

        Vector3 fireDir = GetMagicFireDirection();
        Vector3 spawnPos = GetSpawnPosition(magicCastPoint, bow.bowSpawnOffset);
        int damage = ComputeAttackDamage(bow, type).damage;
        PlayWeaponActionAnimation(bow, hand, type);

        SpawnProjectile(
            bow.bowProjectilePrefab,
            spawnPos,
            fireDir,
            damage,
            bow.bowProjectileSpeed > 0f ? bow.bowProjectileSpeed : fallbackProjectileSpeed,
            bow.bowProjectileLifetime > 0f ? bow.bowProjectileLifetime : fallbackProjectileLifetime,
            bow.bowHitMask
        );

        stats.SpendStamina(staminaCost);
        inventory.TryConsumeItem(bow.bowAmmoItem, 1, out _);
        if (controller != null && controller.inventoryUI != null)
            controller.inventoryUI.RefreshEquipmentCross();

        lastBowShotTime[handIndex] = Time.time;
    }

    private void PlayWeaponActionAnimation(WeaponItem weapon, Hand hand, AttackType type)
    {
        if (weapon == null || weapon.animationProfile == null || animator == null) return;
        bool isAirAttack = controller != null && !controller.IsGrounded;
        string animToPlay = GetAttackAnimation(weapon.animationProfile, hand, type, isAirAttack);
        if (string.IsNullOrWhiteSpace(animToPlay)) return;
        animator.CrossFadeInFixedTime(animToPlay, 0.08f);
    }

    private Vector3 GetSpawnPosition(Transform castPoint, Vector3 localOffset)
    {
        Vector3 verticalOffset = transform.up * magicCastPointHeightOffset;
        if (castPoint != null)
            return castPoint.position + verticalOffset;
        return transform.position + transform.TransformDirection(localOffset) + verticalOffset;
    }

    private void SpawnProjectile(GameObject projectilePrefab, Vector3 spawnPos, Vector3 fireDir, int damage, float speed, float lifetime, LayerMask hitMask)
    {
        if (projectilePrefab == null) return;

        Quaternion rotation = fireDir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(fireDir) : transform.rotation;
        GameObject projectileObj = Instantiate(projectilePrefab, spawnPos, rotation);
        MagicProjectile projectile = projectileObj.GetComponent<MagicProjectile>();
        if (projectile == null)
            projectile = projectileObj.AddComponent<MagicProjectile>();

        float finalSpeed = speed > 0f ? speed : fallbackProjectileSpeed;
        float finalLifetime = lifetime > 0f ? lifetime : fallbackProjectileLifetime;
        projectile.Initialize(transform, fireDir, Mathf.Max(1, damage), finalSpeed, finalLifetime, hitMask);
    }

    private Vector3 GetMagicFireDirection()
    {
        if (targetLockSystem == null)
            targetLockSystem = GetComponentInChildren<TargetLockSystem>(true) ?? GetComponentInParent<TargetLockSystem>();

        if (targetLockSystem != null && targetLockSystem.isLockedOn && targetLockSystem.currentTarget != null)
        {
            Vector3 origin = magicCastPoint != null ? magicCastPoint.position : transform.position + Vector3.up * 1.2f;
            Vector3 targetPoint = GetLockTargetAimPoint(targetLockSystem.currentTarget);
            Vector3 toTarget = targetPoint - origin;
            if (toTarget.sqrMagnitude > 0.0001f)
                return toTarget.normalized;
        }

        return transform.forward;
    }

    private static Vector3 GetLockTargetAimPoint(Transform target)
    {
        if (target == null) return Vector3.zero;
        Transform lockPoint = target.Find("LockOnPoint");
        if (lockPoint != null) return lockPoint.position;

        // Preferisci il centro collider (petto/corpo) invece del pivot ai piedi.
        Collider col = target.GetComponentInChildren<Collider>();
        if (col != null)
            return col.bounds.center;

        return target.position + Vector3.up * 1.1f;
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
    }

    void HandleMagicInput()
    {
        if (!enableMagicCastPrototype) return;
        if (controller == null || controller.Controls == null) return;
        if (controller.IsInventoryOpen) return;
        if (controller.IsRolling || isAttacking || !canAttack) return;

        if (Keyboard.current == null || !Keyboard.current[magicCastKey].wasPressedThisFrame) return;

        MagicItemData equippedMagic = inventory != null ? inventory.GetCurrentMagic() : null;
        if (equippedMagic == null || equippedMagic.projectilePrefab == null) return;

        float cooldown = equippedMagic.castCooldown > 0f ? equippedMagic.castCooldown : fallbackMagicCooldown;
        if (Time.time < lastMagicCastTime + cooldown) return;

        float manaCost = Mathf.Max(0f, equippedMagic.manaCost);
        if (stats != null && !stats.UseMana(manaCost)) return;

        Vector3 spawnPos = GetSpawnPosition(magicCastPoint, equippedMagic.spawnOffset);

        Vector3 fireDir = GetMagicFireDirection();
        int finalMagicDamage = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0, stats != null ? stats.GetBaseMagicDamage() : 0) + Mathf.Max(0, equippedMagic.magicDamage)));
        float speed = equippedMagic.projectileSpeed > 0f ? equippedMagic.projectileSpeed : fallbackProjectileSpeed;
        float lifetime = equippedMagic.projectileLifetime > 0f ? equippedMagic.projectileLifetime : fallbackProjectileLifetime;
        SpawnProjectile(equippedMagic.projectilePrefab, spawnPos, fireDir, finalMagicDamage, speed, lifetime, equippedMagic.hitMask);

        lastMagicCastTime = Time.time;
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
            scalingBonus = Mathf.Max(0f, weapon.dexterityScaling) * Mathf.Max(0, stats.dexterity);
        }
        else if (isMagicWeapon)
        {
            scalingBonus = Mathf.Max(0f, weapon.intelligenceScaling) * Mathf.Max(0, stats.intelligence);
        }
        else
        {
            scalingBonus = Mathf.Max(0f, weapon.strengthScaling) * Mathf.Max(0, stats.strength)
                           + Mathf.Max(0f, weapon.dexterityScaling) * Mathf.Max(0, stats.dexterity);
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

        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(rawDamage));
        return (finalDamage, isCritical);
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
