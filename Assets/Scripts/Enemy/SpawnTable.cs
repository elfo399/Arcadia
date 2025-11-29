using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewSpawnTable", menuName = "Dungeon/Spawn Table")]
public class SpawnTable : ScriptableObject
{
    public List<EnemyData> enemies;

    // Pesca un nemico a caso basandosi sul "Peso" (Rarità)
    public EnemyData GetRandomEnemy()
    {
        if (enemies == null || enemies.Count == 0) return null;

        int totalWeight = 0;
        foreach (var e in enemies) totalWeight += e.spawnWeight;

        int randomValue = Random.Range(0, totalWeight);
        int currentWeight = 0;

        foreach (var e in enemies)
        {
            currentWeight += e.spawnWeight;
            if (randomValue < currentWeight)
            {
                return e;
            }
        }

        return enemies[0]; // Fallback se qualcosa va storto
    }
}