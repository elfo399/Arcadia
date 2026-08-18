using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public sealed class DungeonWaveDefinition { public List<SpawnTable> enemyPools = new List<SpawnTable>(); }

/// <summary>Ordered authored-spawn-point encounter. Each interrupted wave restarts from wave zero after loading.</summary>
public sealed class WaveRoomRule : RoomRule
{
    [SerializeField] private List<DungeonWaveDefinition> waves = new List<DungeonWaveDefinition>();
    [SerializeField] private bool startOnPlayerEntry = true;
    private int currentWave = -1;
    private bool running;
    public override void OnPlayerEntered() { if(startOnPlayerEntry && !IsCompleted) StartWaves(); }
    public void StartWaves() { if(running||IsCompleted)return; running=true; Context.Room.BeginCombat(this); StartNextWave(); }
    private void StartNextWave()
    {
        currentWave++; if(currentWave>=waves.Count){ Complete(); return; }
        var points=GetComponentsInChildren<DungeonWaveSpawnPoint>(true); if(points.Length==0){ Debug.LogError($"[WaveRoomRule] {name} requires DungeonWaveSpawnPoint components.",this); Fail(); return; }
        var wave=waves[currentWave]; if(wave==null || wave.enemyPools==null || wave.enemyPools.Count==0){ Debug.LogError($"[WaveRoomRule] {name} wave {currentWave} has no pools.",this); Fail(); return; }
        var random=Context.CreateRandom(RuleId+":wave:"+currentWave); for(int i=0;i<points.Length;i++) points[i].Spawn(wave.enemyPools[random.Next(wave.enemyPools.Count)],random); Context.Room.WakeUpEnemies();
        if(Context.Room.ActiveEnemyCount==0) StartNextWave();
    }
    public override void OnEnemyDied(GameObject enemy) { if(running && Context.Room.ActiveEnemyCount==0) StartNextWave(); }
    protected override void OnStateRestored(string payload) { running=false; currentWave=-1; }
}

public sealed class DungeonWaveSpawnPoint : MonoBehaviour
{
    public void Spawn(SpawnTable pool, System.Random random)
    {
        if(pool==null)return; EnemyData data=pool.GetRandomEnemy(random); if(data==null||data.prefab==null)return; var enemy=Instantiate(data.prefab,transform.position,transform.rotation,transform);
        var health=enemy.GetComponent<EnemyHealth>(); if(health!=null){health.maxHealth=data.maxHealth;health.currentHealth=data.maxHealth;health.experienceReward=Mathf.Max(0,data.experienceReward);} var room=GetComponentInParent<Room>(); if(room!=null)room.RegisterEnemy(enemy);
    }
}
