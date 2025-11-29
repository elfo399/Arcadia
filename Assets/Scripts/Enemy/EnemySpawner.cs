using UnityEngine;
using UnityEngine.AI;

public class EnemySpawner : MonoBehaviour
{
    [Header("Configurazione")]
    public SpawnTable spawnTable; // La lista dei mostri del livello
    [Range(0, 100)] public int spawnChance = 50; // Probabilità

    void Start()
    {
        // 1. Lancia il dado
        if (Random.Range(0, 100) > spawnChance)
        {
            Destroy(gameObject);
            return;
        }

        // 2. Pesca il mostro
        if (spawnTable == null) return;
        EnemyData data = spawnTable.GetRandomEnemy();

        if (data != null && data.prefab != null)
        {
            SpawnEnemy(data);
        }
    }

    void SpawnEnemy(EnemyData data)
    {
        // Crea il mostro
        GameObject enemy = Instantiate(data.prefab, transform.position, transform.rotation, transform);
        enemy.name = data.enemyName;

        // --- INIEZIONE DATI (Sovrascrivi le stats del prefab con quelle del Data) ---
        
        // Vita
        EnemyHealth health = enemy.GetComponent<EnemyHealth>();
        if (health != null) 
        {
            health.maxHealth = data.maxHealth;
            // Reset vita corrente (importante!)
            // Nota: Assicurati che EnemyHealth abbia un metodo o proprietà per resettare, 
            // oppure fallo via reflection/public field se currentHealth è pubblico.
            // Se currentHealth è privato e inizializzato in Start(), cambiare maxHealth qui funziona perché Start() parte dopo.
        }

        // Velocità
        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        if (agent != null) 
        {
            agent.speed = data.moveSpeed;
        }

        // --- REGISTRAZIONE STANZA (Fondamentale per le porte!) ---
        if (enemy.CompareTag("Enemy"))
        {
            Room room = GetComponentInParent<Room>();
            if (room != null) 
            {
                room.RegisterEnemy(enemy);
            }
        }
    }
}