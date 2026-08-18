using System;
using System.Collections.Generic;
using UnityEngine;

public enum DungeonChallengeMode { Gauntlet, TimedKill, PerfectCombat }

/// <summary>Voluntary encounter rule. Failure ends the encounter without corrupting persistent room state.</summary>
public sealed class ChallengeRoomRule : RoomRule, IInteractable
{
    public override bool BlocksRoomCompletion => true;
    [SerializeField] private DungeonChallengeMode mode;
    [SerializeField] private List<DungeonWaveDefinition> waves = new List<DungeonWaveDefinition>();
    [SerializeField, Min(1f)] private float timeLimitSeconds = 30f;
    [SerializeField] private bool failureCompletesRoom = true;
    [SerializeField] private bool allowRetry = true;
    [SerializeField] private string prompt = "Accept challenge";
    private bool active; private int waveIndex=-1; private float remaining;
    public override bool IsSatisfiedForRoomCompletion => IsCompleted || (IsFailed && failureCompletesRoom);
    protected override void OnRoomInitialized() { remaining=timeLimitSeconds; }
    private void OnEnable() { PlayerStats.DamageTaken += HandlePlayerDamage; }
    private void OnDisable() { PlayerStats.DamageTaken -= HandlePlayerDamage; }
    public void Interact(GameObject player) { if(!active&&!IsResolved) StartChallenge(); }
    public string GetPrompt() => active||IsCompleted||IsFailed ? string.Empty : prompt;
    private void StartChallenge() { if(waves.Count==0){Debug.LogError($"[ChallengeRoomRule] {name} requires at least one wave.",this);return;} active=true; remaining=timeLimitSeconds; StartRunning(); Context.Room.BeginCombat(this); StartNextWave(); }
    private void StartNextWave()
    {
        waveIndex++; if(waveIndex>=waves.Count){ active=false; Complete(); return; }
        DungeonWaveSpawnPoint[] points=GetComponentsInChildren<DungeonWaveSpawnPoint>(true); DungeonWaveDefinition wave=waves[waveIndex];
        List<SpawnTable> pools=wave!=null?wave.enemyPools:null;
        if(pools==null||pools.Count==0)pools=CoreGenerator.Instance!=null&&CoreGenerator.Instance.ActiveFloorDefinition!=null?CoreGenerator.Instance.ActiveFloorDefinition.enemyPools:null;
        var validPools=new List<SpawnTable>();if(pools!=null)foreach(var pool in pools)if(pool!=null)validPools.Add(pool);
        if(points.Length==0||validPools.Count==0){ EndFailure(); return; }
        var random=Context.CreateRandom(RuleId+":challenge:"+waveIndex); foreach(var point in points) point.Spawn(validPools[random.Next(validPools.Count)],random,Context.Room,RuleId); Context.Room.WakeUpEnemies(RuleId);
    }
    private void Update() { if(active && mode==DungeonChallengeMode.TimedKill && (remaining-=Time.deltaTime)<=0f) EndFailure(); }
    public override void OnEnemyDied(GameObject enemy,string ownerId) { if(active&&ownerId==RuleId&&Context.Room.GetEncounterEnemyCount(RuleId)==0) StartNextWave(); }
    private void HandlePlayerDamage(float amount) { if(active && mode==DungeonChallengeMode.PerfectCombat && amount>0f) EndFailure(); }
    private void EndFailure() { if(!active)return; if(allowRetry){ResetAttempt();ResetFailedAttempt();}else{active=false;Context.Room.ClearEncounter(RuleId);Fail();} }
    protected override void OnStateRestored(string payload) { ResetAttempt(); }
    protected override string CaptureState() => IsCompleted ? "success" : IsFailed ? "failed" : string.Empty;
    private void ResetAttempt(){active=false;waveIndex=-1;remaining=timeLimitSeconds;Context?.Room.ClearEncounter(RuleId);Context?.Room.ReleaseDoorLock(RuleId);}
}
