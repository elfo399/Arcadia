using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewSpawnTable", menuName = "Dungeon/Spawn Table")]
public class SpawnTable : ScriptableObject
{
    public List<EnemyData> enemies;

    public EnemyData GetRandomEnemy(System.Random prng)
    {
        if (enemies == null || enemies.Count == 0) return null;
        if (prng == null) prng = new System.Random();

        int totalWeight = 0;
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e == null || e.prefab == null) continue;
            totalWeight += Mathf.Max(0, e.spawnWeight);
        }
        if (totalWeight <= 0) return null;

        int randomValue = prng.Next(0, totalWeight);
        int currentWeight = 0;

        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e == null || e.prefab == null) continue;
            int weight = Mathf.Max(0, e.spawnWeight);
            if (weight <= 0) continue;

            currentWeight += weight;
            if (randomValue < currentWeight) return e;
        }

        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e != null && e.prefab != null) return e;
        }
        return null;
    }

    // Fallback per random standard
    public EnemyData GetRandomEnemy()
    {
        return GetRandomEnemy(new System.Random());
    }
}
