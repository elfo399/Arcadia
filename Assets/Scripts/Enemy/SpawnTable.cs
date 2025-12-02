using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewSpawnTable", menuName = "Dungeon/Spawn Table")]
public class SpawnTable : ScriptableObject
{
    public List<EnemyData> enemies;

    public EnemyData GetRandomEnemy(System.Random prng)
    {
        if (enemies == null || enemies.Count == 0) return null;

        int totalWeight = 0;
        foreach (var e in enemies) totalWeight += e.spawnWeight;

        int randomValue = prng.Next(0, totalWeight);
        int currentWeight = 0;

        foreach (var e in enemies)
        {
            currentWeight += e.spawnWeight;
            if (randomValue < currentWeight) return e;
        }
        return enemies[0];
    }

    // Fallback per random standard
    public EnemyData GetRandomEnemy()
    {
        return GetRandomEnemy(new System.Random());
    }
}