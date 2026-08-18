using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Runtime context for one authored room prefab. Gameplay belongs to RoomRule components.</summary>
public class Room : MonoBehaviour
{
    public static Room CurrentPlayerRoom { get; private set; }
    [Header("Definition")] public RoomData roomData;
    [HideInInspector] public string internalRoomType = "Normal";
    [Header("Quest Events")] [SerializeField] private string questTargetId; [SerializeField] private string questTargetTag;
    [Serializable] public struct DoorEntry { public string label; public Vector2Int gridOffset; public Vector2Int direction; public GameObject doorObject; public GameObject wallObject; public GameObject lockObject; [HideInInspector] public bool isConnected; }
    [Header("Doors")] public List<DoorEntry> doors = new List<DoorEntry>();
    [Header("Legacy key lock")] public bool isLocked;
    [Header("Compatibility state")] public bool roomCleared;
    [HideInInspector] public List<GameObject> activeEnemies = new List<GameObject>();
    [Header("Boss portal")] public GameObject floorPortalPrefab; public GameObject preplacedFloorPortal; public Vector3 portalSpawnOffset; public float portalDistanceFromCenter = 10f;
    [Header("Player spawn")] public Transform playerSpawnPoint;

    public string RuntimeId { get; private set; }
    public Vector2Int GridAnchor { get; private set; }
    public Vector2Int GridSize { get; private set; } = Vector2Int.one;
    public int Floor { get; private set; }
    public string PlacementRole { get; private set; }
    public int ActiveEnemyCount { get { PruneEnemies(); return activeEnemies.Count; } }
    public bool PlayerHasEntered { get; private set; }
    public bool BattleActive { get; private set; }

    private readonly List<RoomRule> rules = new List<RoomRule>();
    private SavedDungeonRoomState state;
    private RoomRuleContext context;
    private GameObject spawnedPortal;
    private bool initialized;

    public void ConfigureGeneratedInstance(string runtimeId, Vector2Int anchor, Vector2Int size, int floor, string role)
    { RuntimeId = runtimeId; GridAnchor = anchor; GridSize = size == Vector2Int.zero ? Vector2Int.one : size; Floor = floor; PlacementRole = role ?? "Normal"; internalRoomType = PlacementRole; }

    private void Start() { InitializeRuntime(); }
    private void InitializeRuntime()
    {
        if (initialized) return; initialized = true;
        if (string.IsNullOrWhiteSpace(RuntimeId)) ConfigureGeneratedInstance(gameObject.name, Vector2Int.zero, roomData != null ? roomData.size : Vector2Int.one, 0, internalRoomType);
        state = DungeonRunStateController.Active != null ? DungeonRunStateController.Active.GetRoom(RuntimeId) : new SavedDungeonRoomState { roomId = RuntimeId };
        context = new RoomRuleContext(this, state);
        rules.AddRange(GetComponents<RoomRule>());
        // Existing prefabs that only contain EnemySpawner retain their legacy combat behavior without prefab migration.
        if (rules.Count == 0 && GetComponentsInChildren<EnemySpawner>(true).Length > 0) rules.Add(gameObject.AddComponent<CombatRoomRule>());
        var savedById = new Dictionary<string, SavedDungeonRuleState>(StringComparer.Ordinal);
        if (state.rules != null) foreach (var saved in state.rules) if (saved != null && !string.IsNullOrWhiteSpace(saved.ruleId)) savedById[saved.ruleId] = saved;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in rules)
        { if (rule == null) continue; if (!ids.Add(rule.RuleId)) Debug.LogError($"[Room] Duplicate rule ID '{rule.RuleId}' in {name}.", this); savedById.TryGetValue(rule.RuleId, out var saved); rule.InitializeRule(context, saved); }
        roomCleared = state.completed;
        if (state.completed) { SetBattleLocks(false); TrySpawnFloorPortal(); }
        else if (roomData != null && (roomData.isShopRoom || roomData.isTreasureRoom || roomData.isBlessedRoom || roomData.isEvilRoom) && !roomData.isStartRoom) isLocked = true;
    }

    public bool CanOpenMenuHere() => !isLocked && !BattleActive && AreConnectedDoorsOpen();
    public void OpenDoor(Vector2Int relativePos, Vector2Int direction)
    { for (int i=0;i<doors.Count;i++) if (doors[i].gridOffset == relativePos && doors[i].direction == direction) { var d=doors[i]; if(d.wallObject) d.wallObject.SetActive(false); if(d.doorObject) d.doorObject.SetActive(true); if(isLocked && d.lockObject) d.lockObject.SetActive(true); d.isConnected=true; doors[i]=d; return; } }
    public void UnlockSpecialRoom() { isLocked=false; SetBattleLocks(false); }
    public void RegisterEnemy(GameObject enemy) { if (enemy != null && !activeEnemies.Contains(enemy)) { activeEnemies.Add(enemy); enemy.SetActive(false); } }
    public void EnemyDied(GameObject enemy) { activeEnemies.Remove(enemy); foreach (var rule in rules) if (rule != null) rule.OnEnemyDied(enemy); Persist(); }
    public void WakeUpEnemies() { foreach (var enemy in activeEnemies) if(enemy) enemy.SetActive(true); }
    public void BeginCombat(RoomRule source) { BattleActive=true; SetBattleLocks(true); state.encounterInProgress=true; foreach(var rule in rules) if(rule!=null && rule!=source) rule.OnEncounterStarted(); Persist(); }
    public void NotifyRuleChanged(RoomRule source) { EvaluateCompletion(); Persist(); }
    public SavedDungeonRuleState GetExternalState(string id)
    {
        if(state.rules!=null) foreach(var entry in state.rules) if(entry!=null&&entry.ruleId==id) return entry;
        var created=new SavedDungeonRuleState { ruleId=id }; var list=new List<SavedDungeonRuleState>(state.rules??Array.Empty<SavedDungeonRuleState>()); list.Add(created); state.rules=list.ToArray(); return created;
    }
    public void SaveExternalState(SavedDungeonRuleState external) { Persist(); }
    private void EvaluateCompletion()
    { if (roomCleared) return; foreach (var rule in rules) if (rule != null && rule.BlocksRoomCompletion && !rule.IsCompleted) return; roomCleared=true; state.completed=true; state.encounterInProgress=false; BattleActive=false; SetBattleLocks(false); foreach (var rule in rules) if(rule != null) rule.OnRoomCompleted(); QuestEvents.Raise(QuestObjectiveEventType.ClearRoom, ResolveQuestTargetId(), ResolveQuestTargetTag()); TrySpawnFloorPortal(); }
    private void Persist() { if (state == null) return; state.visited |= PlayerHasEntered; state.completed=roomCleared; state.rules=ExportRules(); }
    private SavedDungeonRuleState[] ExportRules() { var data=new List<SavedDungeonRuleState>(); var ids=new HashSet<string>(); foreach(var r in rules) if(r!=null) { data.Add(r.ExportState()); ids.Add(r.RuleId); } if(state.rules!=null) foreach(var saved in state.rules) if(saved!=null&&!ids.Contains(saved.ruleId)) data.Add(saved); return data.ToArray(); }
    private void OnTriggerEnter(Collider other)
    { if (!other.CompareTag("Player")) return; CurrentPlayerRoom=this; if (PlayerHasEntered || isLocked) return; PlayerHasEntered=true; state.visited=true; state.revealed=true; QuestEvents.Raise(QuestObjectiveEventType.EnterRoom, ResolveQuestTargetId(), ResolveQuestTargetTag()); foreach(var rule in rules) if(rule!=null) rule.OnPlayerEntered(); EvaluateCompletion(); Persist(); }
    private void OnTriggerStay(Collider other) { if(other.CompareTag("Player")) CurrentPlayerRoom=this; }
    private void OnTriggerExit(Collider other) { if(!other.CompareTag("Player")) return; if(CurrentPlayerRoom==this) CurrentPlayerRoom=null; foreach(var rule in rules) if(rule!=null) rule.OnPlayerExited(); Persist(); }
    private bool AreConnectedDoorsOpen() { foreach(var d in doors) if(d.isConnected && ((d.lockObject && d.lockObject.activeInHierarchy)||(d.wallObject&&d.wallObject.activeInHierarchy))) return false; return true; }
    private void SetBattleLocks(bool locked) { foreach(var d in doors) if(d.isConnected) { if(d.lockObject) d.lockObject.SetActive(locked); else if(d.wallObject) d.wallObject.SetActive(locked); if(d.doorObject) d.doorObject.SetActive(!locked); } }
    private void PruneEnemies() { activeEnemies.RemoveAll(enemy => enemy == null); }
    private string ResolveQuestTargetId() => !string.IsNullOrWhiteSpace(questTargetId) ? questTargetId.Trim() : roomData != null && !string.IsNullOrWhiteSpace(roomData.stableId) ? roomData.stableId : gameObject.name;
    private string ResolveQuestTargetTag() => !string.IsNullOrWhiteSpace(questTargetTag) ? questTargetTag.Trim() : PlacementRole ?? "room";
    private void TrySpawnFloorPortal()
    { if(spawnedPortal || roomData==null || !roomData.isBossRoom || !roomCleared) return; Vector3 pos=transform.position+portalSpawnOffset; if(preplacedFloorPortal) { spawnedPortal=preplacedFloorPortal; spawnedPortal.transform.position=pos; spawnedPortal.SetActive(true); } else if(floorPortalPrefab) spawnedPortal=Instantiate(floorPortalPrefab,pos,Quaternion.identity,transform); if(spawnedPortal && !spawnedPortal.GetComponent<FloorPortal>()) spawnedPortal.AddComponent<FloorPortal>(); }
}
