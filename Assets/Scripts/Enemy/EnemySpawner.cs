using UnityEngine;
using UnityEngine.AI;

public class EnemySpawner : MonoBehaviour
{
    [Header("Configurazione")]
    public SpawnTable spawnTable;

    void Start()
    {
        int masterSeed = (CoreGenerator.Instance != null) ? CoreGenerator.Instance.currentMasterSeed : 0;
        int localSeed = masterSeed + (int)(transform.position.x * 1000) + (int)(transform.position.z * 1000);
        System.Random prng = new System.Random(localSeed);

        if (spawnTable == null) return;

        EnemyData data = spawnTable.GetRandomEnemy(prng);
        if (data != null && data.prefab != null)
            SpawnEnemy(data);
    }

    void SpawnEnemy(EnemyData data)
    {
        GameObject enemy = Instantiate(data.prefab, transform.position, transform.rotation, transform);
        enemy.name = data.enemyName;

        EnemyHealth health = enemy.GetComponent<EnemyHealth>();
        if (health != null)
        {
            health.maxHealth = data.maxHealth;
            health.currentHealth = data.maxHealth;
            health.experienceReward = Mathf.Max(0, data.experienceReward);
        }

        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        if (agent != null) agent.speed = data.moveSpeed;

        SimpleEnemyAI ai = enemy.GetComponent<SimpleEnemyAI>();
        if (ai != null)
        {
            ai.SetPlayerTarget(ResolvePlayerTarget());
            ai.ConfigureFromData(data);
        }

        if (enemy.CompareTag("Enemy"))
        {
            Room room = GetComponentInParent<Room>();
            if (room != null) room.RegisterEnemy(enemy);
        }
    }

    private static Transform ResolvePlayerTarget()
    {
        if (CoreGenerator.Instance != null && CoreGenerator.Instance.playerTransform != null)
            return CoreGenerator.Instance.playerTransform;

        if (PlayerController.CurrentPlayerTransform != null)
            return PlayerController.CurrentPlayerTransform;

        return PlayerStats.instance != null ? PlayerStats.instance.transform : null;
    }
}
