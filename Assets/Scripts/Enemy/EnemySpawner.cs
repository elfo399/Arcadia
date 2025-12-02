using UnityEngine;
using UnityEngine.AI;

public class EnemySpawner : MonoBehaviour
{
    [Header("Configurazione")]
    public SpawnTable spawnTable; 
    [Range(0, 100)] public int spawnChance = 50; 

    void Start()
    {
        // 1. Recupera Seed Globale
        int masterSeed = (CoreGenerator.Instance != null) ? CoreGenerator.Instance.currentMasterSeed : 0;
        
        // 2. Calcola Seed Locale
        int localSeed = masterSeed + (int)(transform.position.x * 1000) + (int)(transform.position.z * 1000);
        System.Random prng = new System.Random(localSeed);

        // 3. Lancia dado
        if (prng.Next(0, 101) > spawnChance)
        {
            Destroy(gameObject);
            return;
        }

        // 4. Pesca mostro
        if (spawnTable == null) return;
        
        // ORA FUNZIONA: SpawnTable ha il metodo che accetta prng
        EnemyData data = spawnTable.GetRandomEnemy(prng);

        if (data != null && data.prefab != null)
        {
            SpawnEnemy(data);
        }
    }

    void SpawnEnemy(EnemyData data)
    {
        GameObject enemy = Instantiate(data.prefab, transform.position, transform.rotation, transform);
        enemy.name = data.enemyName;

        // Iniezione Dati
        EnemyHealth health = enemy.GetComponent<EnemyHealth>();
        if (health != null) 
        {
            health.maxHealth = data.maxHealth;
            health.currentHealth = data.maxHealth; // ORA FUNZIONA: currentHealth è public
        }

        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        if (agent != null) agent.speed = data.moveSpeed;

        // La registrazione è gestita automaticamente da EnemyHealth.Start() ora,
        // ma lasciamo questo controllo per sicurezza se lo script parte prima
        if (enemy.CompareTag("Enemy"))
        {
            Room room = GetComponentInParent<Room>();
            if (room != null) room.RegisterEnemy(enemy);
        }
    }
}