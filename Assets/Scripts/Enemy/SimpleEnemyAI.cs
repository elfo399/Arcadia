using UnityEngine;
using UnityEngine.AI;

public class SimpleEnemyAI : MonoBehaviour
{
    [Header("Setup")]
    public NavMeshAgent agent;
    public Transform playerTarget;

    [Header("Parametri AI")]
    public float sightRange = 15f;  // Distanza a cui ti vede
    public float attackRange = 2f;  // Distanza a cui attacca

    // Stati interni
    private bool playerInSight;
    private bool playerInAttackRange;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        // 1. Disattiva subito l'agent per evitare errori se nasce a mezz'aria
        if (agent != null) agent.enabled = false;

        // 2. Trova il giocatore
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerTarget = p.transform;

        // 3. Aspetta un attimo che il NavMesh sia generato, poi "Snap to Ground"
        Invoke(nameof(ActivateAgent), 0.5f);
    }

    // Questa funzione risolve il problema dello zombie volante
    void ActivateAgent()
    {
        if (agent == null) return;

        // Cerca un punto valido sul NavMesh entro 5 metri dalla posizione di spawn
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 5.0f, NavMesh.AllAreas))
        {
            // Trovato! Sposta fisicamente lo zombie sul pavimento
            transform.position = hit.position; 
            
            // Warp è fondamentale: dice all'Agent "Tu ora sei qui, ricalcola tutto"
            agent.Warp(hit.position); 
            
            agent.enabled = true;
        }
        else
        {
            // Fallback se non trova il pavimento (magari è spawnato dentro un muro)
            Debug.LogWarning($"{gameObject.name}: NavMesh non trovato vicino allo spawn!");
            agent.enabled = true; // Proviamo ad attivarlo comunque
        }
    }

    void Update()
    {
        // Se il player è morto o l'agent non è ancora attivo, non fare nulla
        if (playerTarget == null || !agent.enabled || !agent.isOnNavMesh) return;

        // Calcola distanze
        float distance = Vector3.Distance(transform.position, playerTarget.position);

        playerInSight = distance <= sightRange;
        playerInAttackRange = distance <= attackRange;

        // Macchina a stati semplice
        if (playerInSight && !playerInAttackRange)
        {
            ChasePlayer();
        }
        else if (playerInSight && playerInAttackRange)
        {
            AttackPlayer();
        }
        else
        {
            // Se il player è lontano, sta fermo
            agent.isStopped = true;
        }
    }

    void ChasePlayer()
    {
        agent.isStopped = false;
        agent.SetDestination(playerTarget.position);
    }

    void AttackPlayer()
    {
        // Ferma il movimento per attaccare
        agent.isStopped = true;

        // Guarda il player (solo rotazione Y)
        Vector3 targetPostition = new Vector3(playerTarget.position.x, transform.position.y, playerTarget.position.z);
        transform.LookAt(targetPostition);

        // TODO: Qui aggiungeremo l'animazione attack e il danno
        // Debug.Log("Zombie sta attaccando!");
    }

    // Disegna i raggi di visione nell'editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}