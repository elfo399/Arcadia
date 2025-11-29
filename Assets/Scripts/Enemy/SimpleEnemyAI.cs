using UnityEngine;
using UnityEngine.AI;

// Automazione: Aggiunge i componenti fisici se mancano!
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(EnemyHealth))]
public class SimpleEnemyAI : MonoBehaviour
{
    [Header("Setup")]
    public NavMeshAgent agent;
    public Transform playerTarget;

    [Header("Parametri AI")]
    public float sightRange = 15f;
    public float attackRange = 2f;

    // Stati interni
    private bool playerInSight;
    private bool playerInAttackRange;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        
        // Configura la fisica per evitare bug col NavMesh
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = true; 
        rb.useGravity = false;

        // Configura il collider se necessario
        CapsuleCollider col = GetComponent<CapsuleCollider>();
        col.isTrigger = false; // Deve essere solido per essere colpito
    }

    void Start()
    {
        // Disattiva agent all'inizio per evitare errori di spawn
        if (agent != null) agent.enabled = false;

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerTarget = p.transform;

        // Snap to ground dopo che il dungeon è generato
        Invoke(nameof(ActivateAgent), 0.5f);
    }

    void ActivateAgent()
    {
        if (agent == null) return;

        NavMeshHit hit;
        // Cerca il pavimento entro 5 metri
        if (NavMesh.SamplePosition(transform.position, out hit, 5.0f, NavMesh.AllAreas))
        {
            transform.position = hit.position; 
            agent.Warp(hit.position); 
            agent.enabled = true;
        }
        else
        {
            // Se fallisce, prova ad attivarlo dov'è
            agent.enabled = true;
        }
    }

    void Update()
    {
        if (playerTarget == null || !agent.enabled || !agent.isOnNavMesh) return;

        float distance = Vector3.Distance(transform.position, playerTarget.position);
        playerInSight = distance <= sightRange;
        playerInAttackRange = distance <= attackRange;

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
        agent.isStopped = true;
        
        // Guarda il player (solo rotazione Y)
        Vector3 targetPos = new Vector3(playerTarget.position.x, transform.position.y, playerTarget.position.z);
        transform.LookAt(targetPos);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}