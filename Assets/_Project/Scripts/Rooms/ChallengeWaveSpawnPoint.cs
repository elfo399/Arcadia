using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ChallengeWaveDefinition
{
    public List<SpawnTable> enemyPools = new List<SpawnTable>();
}

/// <summary>Authored enemy spawn position used by ChallengeRoomRule waves.</summary>
public sealed class ChallengeWaveSpawnPoint : MonoBehaviour
{
    public void Spawn(SpawnTable pool, System.Random random, Room room, string ownerId)
    {
        if (pool == null)
            return;

        EnemyData data = pool.GetRandomEnemy(random);
        if (data == null || data.prefab == null)
            return;

        GameObject enemy = EnemySpawner.SpawnConfigured(data, transform.position, transform.rotation, transform);
        if (room != null)
            room.RegisterEnemy(enemy, ownerId);
    }
}
