using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hosts an existing NPC prefab/component inside an authored dungeon room. It
/// deliberately delegates dialogue, quest, merchant and blacksmith behaviour
/// to components already present on that NPC.
/// </summary>
public sealed class DungeonNpcEncounter : MonoBehaviour, IInteractable
{
    [SerializeField] private string encounterId;
    [SerializeField] private GameObject existingNpc;
    [SerializeField] private GameObject npcPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private DungeonRequirement[] requirements;
    [SerializeField] private DungeonOccurrencePolicy occurrence = DungeonOccurrencePolicy.OncePerSave;
    [SerializeField] private bool activateOnRoomEnter = true;
    [SerializeField] private bool startDialogueImmediately = true;
    [SerializeField] private string discoveredFlag;
    [SerializeField] private string hubUnlockFlag;
    [SerializeField] private DungeonOutcome[] activationOutcomes;
    [SerializeField] private string prompt = "Approach";
    private Room room; private SavedDungeonRuleState state; private GameObject spawnedNpc;
    private string SaveFlag => "dungeon.npc."+encounterId;

    private void Start()
    {
        room=GetComponentInParent<Room>();if(room==null)return;state=room.GetExternalState("npc:"+encounterId);
        if(state.completed)EnsureNpc();else if(existingNpc!=null)existingNpc.SetActive(false);
    }
    private void OnTriggerEnter(Collider other){if(activateOnRoomEnter&&other.CompareTag("Player"))Activate(other.gameObject);}
    public void Interact(GameObject player){Activate(player);}
    public string GetPrompt()=>CanActivate(PlayerStats.instance)?prompt:string.Empty;

    private bool Activate(GameObject player)
    {
        PlayerStats stats=player!=null?player.GetComponentInParent<PlayerStats>():PlayerStats.instance;if(!CanActivate(stats))return false;
        if(!DungeonOutcomeResolution.TryResolveAll(activationOutcomes,stats,index=>ContextRandom("outcome:"+index),out List<DungeonResolvedOutcome> resolved))return false;
        if(!DungeonOutcomeResolution.ApplyAll(resolved,stats))return false;
        GameObject npc=EnsureNpc();if(npc==null)return false;
        if(!string.IsNullOrWhiteSpace(discoveredFlag))stats.SetStoryFlag(discoveredFlag,false);if(!string.IsNullOrWhiteSpace(hubUnlockFlag))stats.SetStoryFlag(hubUnlockFlag,false);
        if(occurrence==DungeonOccurrencePolicy.OncePerRun)DungeonRunStateController.Active?.ConsumeOncePerRun(encounterId);if(occurrence==DungeonOccurrencePolicy.OncePerSave)stats.SetStoryFlag(SaveFlag,false);
        state.completed=true;room.SaveExternalState(state);
        if(startDialogueImmediately)npc.GetComponentInChildren<NPCInteractable>(true)?.Interact(player??stats.gameObject);
        return true;
    }
    private bool CanActivate(PlayerStats stats)
    {
        if(room==null||stats==null||string.IsNullOrWhiteSpace(encounterId))return false;
        if(requirements!=null)foreach(DungeonRequirement requirement in requirements)if(requirement!=null&&!requirement.IsMet(stats))return false;
        if(occurrence==DungeonOccurrencePolicy.OncePerSave)return !stats.HasStoryFlag(SaveFlag);
        if(occurrence==DungeonOccurrencePolicy.OncePerRun)return DungeonRunStateController.Active==null?!state.completed:!DungeonRunStateController.Active.HasConsumedOncePerRun(encounterId);
        return true;
    }
    private GameObject EnsureNpc()
    {
        if(existingNpc!=null){existingNpc.SetActive(true);return existingNpc;}if(spawnedNpc!=null)return spawnedNpc;if(npcPrefab==null)return null;
        Transform point=spawnPoint!=null?spawnPoint:transform;spawnedNpc=Instantiate(npcPrefab,point.position,point.rotation,transform);return spawnedNpc;
    }
    private System.Random ContextRandom(string stream)=>DungeonDeterminism.Create(DungeonRunStateController.Active?.RunSeed??string.Empty,room!=null?room.Floor:0,room!=null?room.RuntimeId:string.Empty,"npc:"+encounterId+":"+stream);
#if UNITY_EDITOR
    private void OnValidate(){if(string.IsNullOrWhiteSpace(encounterId)){encounterId="npc-"+Guid.NewGuid().ToString("N");UnityEditor.EditorUtility.SetDirty(this);}}
#endif
}
