using UnityEngine;
using System.Collections.Generic;

public class WeaponDamage : MonoBehaviour
{
    [Header("Parametri")]
    public int damage = 10; // Danno inflitto per colpo

    [Header("Debug")]
    [SerializeField] private Collider damageCollider;
    // Lista per ricordarsi chi abbiamo già colpito in questo singolo attacco
    // (Così non colpiamo lo stesso nemico 30 volte in un secondo)
    private List<IDamageable> hitTargets = new List<IDamageable>();

    void Awake()
    {
        damageCollider = GetComponent<Collider>();
        
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
        hitTargets.Clear(); // Nuovo colpo, resetta la lista dei colpiti
        if (damageCollider != null) damageCollider.enabled = true;
    }

    // Chiamata dall'Animation Event
    public void DisableDamage()
    {
        if (damageCollider != null) damageCollider.enabled = false;
    }

    // Logica di collisione
    void OnTriggerEnter(Collider other)
    {
        // 1. Cerchiamo se l'oggetto toccato ha l'interfaccia "IDamageable"
        // (Cerca sia sull'oggetto colpito che sui suoi padri, utile se colpisci un braccio ma lo script è sul corpo)
        IDamageable target = other.GetComponent<IDamageable>();
        
        if (target == null) 
            target = other.GetComponentInParent<IDamageable>();

        // 2. Se è un bersaglio valido
        if (target != null)
        {
            // 3. Verifica se è il giocatore stesso (per non colpirsi da soli)
            if (other.CompareTag("Player")) return;

            // 4. Se non l'abbiamo già colpito in questo swing
            if (!hitTargets.Contains(target))
            {
                // Applica il danno
                target.TakeDamage(damage);
                
                // Aggiungi alla lista dei "già colpiti" per questo attacco
                hitTargets.Add(target);

                // Qui puoi mettere effetti:
                // AudioManager.Play("HitSound");
                // Instantiate(bloodEffect, other.ClosestPoint(transform.position), Quaternion.identity);
            }
        }
    }
}