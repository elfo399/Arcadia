using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Small authored composition for a miniboss: existing SpawnTable + encounter
/// ownership + optional reward rule + persistent DungeonOutcome progression.
/// Put reward pedestals in the same room and gate them with this RuleId.
/// </summary>
public sealed class MinibossRoomRule : RoomRule, IInteractable
{
    public override bool BlocksRoomCompletion => floorAllowsMiniboss;
    [SerializeField] private SpawnTable enemyPool;
    [SerializeField] private bool startOnPlayerEntry = true;
    [SerializeField] private string prompt = "Challenge miniboss";
    [SerializeField] private DungeonOutcome[] victoryOutcomes;
    private bool active;
    private bool floorAllowsMiniboss = true;
    private List<DungeonResolvedOutcome> resolvedVictory;

    protected override void OnRoomInitialized()
    {
        floorAllowsMiniboss = CoreGenerator.Instance == null || CoreGenerator.Instance.ActiveFloorDefinition == null || CoreGenerator.Instance.ActiveFloorDefinition.minibossesAvailable;
    }

    protected override void OnStateRestored(string payload) { active = false; resolvedVictory = null; }
    public override void OnPlayerEntered(bool firstVisit) { if(startOnPlayerEntry) StartEncounter(); }
    public void Interact(GameObject player) { if(!startOnPlayerEntry) StartEncounter(); }
    public string GetPrompt() => floorAllowsMiniboss && !startOnPlayerEntry && !IsResolved ? prompt : string.Empty;

    private void StartEncounter()
    {
        if(!floorAllowsMiniboss||active||IsResolved||enemyPool==null)return;
        if(!DungeonOutcomeResolution.TryResolveAll(victoryOutcomes,PlayerStats.instance,index=>Context.CreateRandom(RuleId+":victory:"+index),out resolvedVictory))
        {
            Debug.LogWarning($"[MinibossRoomRule] Victory outcomes cannot be applied for '{name}'; encounter was not started.",this);return;
        }
        DungeonWaveSpawnPoint[] points=GetComponentsInChildren<DungeonWaveSpawnPoint>(true);
        if(points.Length==0){Debug.LogError($"[MinibossRoomRule] '{name}' requires an authored DungeonWaveSpawnPoint.",this);return;}
        active=true;StartRunning();Context.Room.BeginCombat(this);System.Random random=Context.CreateRandom(RuleId+":spawn");
        foreach(DungeonWaveSpawnPoint point in points)point.Spawn(enemyPool,random,Context.Room,RuleId);
        Context.Room.WakeUpEnemies(RuleId);if(Context.Room.GetEncounterEnemyCount(RuleId)==0)Finish();
    }

    public override void OnEnemyDied(GameObject enemy,string ownerId)
    {
        if(active&&!IsResolved&&ownerId==RuleId&&Context.Room.GetEncounterEnemyCount(RuleId)==0)Finish();
    }

    private void Finish()
    {
        if(IsResolved)return;
        if(!DungeonOutcomeResolution.ApplyAll(resolvedVictory,PlayerStats.instance)){Fail();return;}
        active=false;Complete();
    }
}
