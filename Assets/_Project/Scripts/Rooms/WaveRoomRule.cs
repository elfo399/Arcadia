using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public sealed class DungeonWaveDefinition { public List<SpawnTable> enemyPools = new List<SpawnTable>(); }

/// <summary>Ordered authored-spawn-point encounter. Each interrupted wave restarts from wave zero after loading.</summary>
public sealed class WaveRoomRule : RoomRule
{
    public override bool BlocksRoomCompletion => true;
    [SerializeField] private List<DungeonWaveDefinition> waves = new List<DungeonWaveDefinition>();
    [SerializeField] private bool startOnPlayerEntry = true;
    private int currentWave = -1;
    private bool running;
    public override void OnPlayerEntered(bool firstVisit) { if(startOnPlayerEntry && !IsResolved) StartWaves(); }
    public void StartWaves() { if(running||IsResolved)return; running=true; StartRunning(); Context.Room.BeginCombat(this); StartNextWave(); }
    private void StartNextWave()
    {
        currentWave++; if(currentWave>=waves.Count){ Complete(); return; }
        var points=GetComponentsInChildren<DungeonWaveSpawnPoint>(true); if(points.Length==0){ Debug.LogError($"[WaveRoomRule] {name} requires DungeonWaveSpawnPoint components.",this); Fail(); return; }
        var wave=waves[currentWave]; List<SpawnTable> pools=wave!=null?wave.enemyPools:null;
        if(pools==null||pools.Count==0)pools=CoreGenerator.Instance!=null&&CoreGenerator.Instance.ActiveFloorDefinition!=null?CoreGenerator.Instance.ActiveFloorDefinition.enemyPools:null;
        var validPools=new List<SpawnTable>();if(pools!=null)foreach(var pool in pools)if(pool!=null)validPools.Add(pool);
        if(validPools.Count==0){ Debug.LogError($"[WaveRoomRule] {name} wave {currentWave} has no pools.",this); Fail(); return; }
        var random=Context.CreateRandom(RuleId+":wave:"+currentWave); for(int i=0;i<points.Length;i++) points[i].Spawn(validPools[random.Next(validPools.Count)],random,Context.Room,RuleId); Context.Room.WakeUpEnemies(RuleId);
        if(Context.Room.GetEncounterEnemyCount(RuleId)==0) StartNextWave();
    }
    public override void OnEnemyDied(GameObject enemy,string ownerId) { if(running&&ownerId==RuleId&&Context.Room.GetEncounterEnemyCount(RuleId)==0) StartNextWave(); }
    protected override void OnStateRestored(string payload) { running=false; currentWave=-1; }
}

public sealed class DungeonWaveSpawnPoint : MonoBehaviour
{
    public void Spawn(SpawnTable pool, System.Random random, Room room, string ownerId)
    {
        if(pool==null)return; EnemyData data=pool.GetRandomEnemy(random); if(data==null||data.prefab==null)return; GameObject enemy=EnemySpawner.SpawnConfigured(data,transform.position,transform.rotation,transform); if(room!=null)room.RegisterEnemy(enemy,ownerId);
    }
}
