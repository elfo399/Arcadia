using UnityEngine;
using System.Collections.Generic;

public class WeaponDamage : MonoBehaviour
{
    [Header("Parametri")]
    [SerializeField] private Hand hand = Hand.Right;
    [SerializeField] private int fallbackDamage = 10; // Danno usato se non trova un'arma valida
    [SerializeField] private int damage = 10; // Danno runtime applicato in questo swing
    [SerializeField] private bool logDamage = true;

    [Header("Debug")]
    [SerializeField] private Collider damageCollider;
    [SerializeField] private WeaponItem currentWeapon;
    [SerializeField] private bool usedPreparedDamage = false;
    [SerializeField] private bool lastHitWasCritical = false;
    [SerializeField] private AttackType lastAttackType = AttackType.Light;
    private bool hasPreparedDamage = false;
    private int preparedDamage = 0;
    private bool preparedCritical = false;
    private AttackType preparedAttackType = AttackType.Light;
    // Lista per ricordarsi chi abbiamo gia colpito in questo singolo attacco
    // (Cosi non colpiamo lo stesso nemico 30 volte in un secondo)
    private readonly List<IDamageable> hitTargets = new List<IDamageable>();
    private PlayerInventory playerInventory;

    void Awake()
    {
        damageCollider = GetComponent<Collider>();
        playerInventory = GetComponentInParent<PlayerInventory>();

        // Setup automatico di sicurezza
        if (damageCollider == null)
        {
            Debug.LogError("Manca il Collider su " + gameObject.name + "! Aggiungilo.");
        }
        else
        {
            damageCollider.isTrigger = true; // Deve attraversare, non spingere
            damageCollider.enabled = false;  // Parte spento
        }
    }

    // Chiamata dall'Animation Event (tramite PlayerAnimationEvents)
    public void EnableDamage()
    {
        if (hasPreparedDamage)
        {
            damage = Mathf.Max(0, preparedDamage);
            usedPreparedDamage = true;
            lastHitWasCritical = preparedCritical;
            lastAttackType = preparedAttackType;
            hasPreparedDamage = false;
        }
        else
        {
            usedPreparedDamage = false;
            lastHitWasCritical = false;
            lastAttackType = AttackType.Light;
            RefreshDamageFromEquippedWeapon();
        }
        hitTargets.Clear(); // Nuovo colpo, resetta la lista dei colpiti
        if (damageCollider != null) damageCollider.enabled = true;
    }

    // Chiamata dall'Animation Event
    public void DisableDamage()
    {
        if (damageCollider != null) damageCollider.enabled = false;
    }

    public void SetHand(Hand value)
    {
        hand = value;
    }

    public void SetPreparedHit(int computedDamage, bool isCritical, AttackType attackType)
    {
        preparedDamage = Mathf.Max(0, computedDamage);
        preparedCritical = isCritical;
        preparedAttackType = attackType;
        hasPreparedDamage = true;
    }

    private void RefreshDamageFromEquippedWeapon()
    {
        if (playerInventory == null)
            playerInventory = GetComponentInParent<PlayerInventory>();

        if (playerInventory == null)
        {
            currentWeapon = null;
            damage = Mathf.Max(0, fallbackDamage);
            return;
        }

        currentWeapon = playerInventory.GetWeaponForHand(hand);
        if (currentWeapon != null)
        {
            damage = Mathf.Max(0, currentWeapon.physicalDamage);
        }
        else
        {
            damage = Mathf.Max(0, fallbackDamage);
        }
    }

    // Logica di collisione
    void OnTriggerEnter(Collider other)
    {
        // 1. Cerchiamo se l'oggetto toccato ha l'interfaccia "IDamageable"
        // (Cerca sia sull'oggetto colpito che sui suoi padri, utile se colpisci un braccio ma lo script e sul corpo)
        IDamageable target = other.GetComponent<IDamageable>();

        if (target == null)
            target = other.GetComponentInParent<IDamageable>();

        // 2. Se e un bersaglio valido
        if (target != null)
        {
            // 3. Verifica se e il giocatore stesso (per non colpirsi da soli)
            if (other.CompareTag("Player")) return;

            // 4. Se non l'abbiamo gia colpito in questo swing
            if (!hitTargets.Contains(target))
            {
                // Applica il danno
                target.TakeDamage(damage);

                if (logDamage)
                {
                    string weaponName = currentWeapon != null && !string.IsNullOrWhiteSpace(currentWeapon.weaponName)
                        ? currentWeapon.weaponName
                        : (currentWeapon != null ? currentWeapon.name : "Fallback");
                    string targetName = other != null ? other.name : "Unknown";
                    string attackTypeName = lastAttackType.ToString();
                    string critTag = lastHitWasCritical ? " CRIT" : string.Empty;
                    Debug.Log($"[Damage] {weaponName} | Hand:{hand} | Type:{attackTypeName} | Dmg:{damage}{critTag} -> Target:{targetName}");
                }

                // Aggiungi alla lista dei "gia colpiti" per questo attacco
                hitTargets.Add(target);

                // Qui puoi mettere effetti:
                // AudioManager.Play("HitSound");
                // Instantiate(bloodEffect, other.ClosestPoint(transform.position), Quaternion.identity);
            }
        }
    }
}
