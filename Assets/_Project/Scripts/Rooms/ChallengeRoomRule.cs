using System;
using System.Collections.Generic;
using UnityEngine;

public enum DungeonChallengeMode { Gauntlet, TimedKill, PerfectCombat }

/// <summary>Voluntary encounter rule. Failure ends the encounter without corrupting persistent room state.</summary>
public sealed class ChallengeRoomRule : RoomRule, IInteractable
{
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
        if(points.Length==0||wave==null||wave.enemyPools==null||wave.enemyPools.Count==0){ EndFailure(); return; }
        var random=Context.CreateRandom(RuleId+":challenge:"+waveIndex); foreach(var point in points) point.Spawn(wave.enemyPools[random.Next(wave.enemyPools.Count)],random,Context.Room,RuleId); Context.Room.WakeUpEnemies(RuleId);
    }
    private void Update() { if(active && mode==DungeonChallengeMode.TimedKill && (remaining-=Time.deltaTime)<=0f) EndFailure(); }
    public override void OnEnemyDied(GameObject enemy,string ownerId) { if(active&&ownerId==RuleId&&Context.Room.GetEncounterEnemyCount(RuleId)==0) StartNextWave(); }
    private void HandlePlayerDamage(float amount) { if(active && mode==DungeonChallengeMode.PerfectCombat && amount>0f) EndFailure(); }
    private void EndFailure() { if(!active)return; active=false; Context.Room.ClearEncounter(RuleId); if(allowRetry)ResetFailedAttempt(); else Fail(); }
    protected override void OnStateRestored(string payload) { active=false; waveIndex=-1; remaining=timeLimitSeconds; }
    protected override string CaptureState() => IsCompleted ? "success" : IsFailed ? "failed" : string.Empty;
}
