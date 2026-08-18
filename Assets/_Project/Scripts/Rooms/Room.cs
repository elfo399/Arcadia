using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One generated authored room. It owns lifecycle/doors; rules own gameplay.</summary>
public class Room : MonoBehaviour
{
    public static Room CurrentPlayerRoom { get; private set; }
    [Header("Definition")] public RoomData roomData; [HideInInspector] public string internalRoomType="Normal";
    [SerializeField] private string questTargetId; [SerializeField] private string questTargetTag;
    [Serializable] public struct DoorEntry{public string label;public Vector2Int gridOffset;public Vector2Int direction;public GameObject doorObject;public GameObject wallObject;public GameObject lockObject;[HideInInspector]public bool isConnected;}
    public List<DoorEntry> doors=new List<DoorEntry>();
    [Tooltip("Legacy authored entry lock. It is not inferred from room category.")] public bool isLocked;
    [Tooltip("Compatibility only: preserves old special-room key locks. Disable on newly authored unlocked special rooms.")] [SerializeField] private bool legacySpecialEntryLock=true;
    [HideInInspector] public bool roomCleared; [HideInInspector] public List<GameObject> activeEnemies=new List<GameObject>();
    public GameObject floorPortalPrefab; public GameObject preplacedFloorPortal; public Vector3 portalSpawnOffset; public float portalDistanceFromCenter=10f; public Transform playerSpawnPoint;
    public string RuntimeId{get;private set;} public Vector2Int GridAnchor{get;private set;} public Vector2Int GridSize{get;private set;}=Vector2Int.one; public int Floor{get;private set;} public string PlacementRole{get;private set;}
    public int ActiveEnemyCount{get{PruneEnemies();return activeEnemies.Count;}} public bool PlayerHasEntered{get;private set;} public bool BattleActive=>doorLocks.Count>0;
    private readonly List<RoomRule> rules=new List<RoomRule>(); private readonly Dictionary<string,List<GameObject>> encounterEnemies=new Dictionary<string,List<GameObject>>(StringComparer.Ordinal); private readonly HashSet<string> doorLocks=new HashSet<string>(StringComparer.Ordinal);
    private SavedDungeonRoomState state; private RoomRuleContext context; private GameObject spawnedPortal; private bool initialized; private string legacyEncounterOwner="legacy";
    public void ConfigureGeneratedInstance(string id,Vector2Int anchor,Vector2Int size,int floor,string role){RuntimeId=id;GridAnchor=anchor;GridSize=size==Vector2Int.zero?Vector2Int.one:size;Floor=floor;PlacementRole=role??"Normal";internalRoomType=PlacementRole;}
    public void InitializeGeneratedRuntime(){InitializeRuntime();}
    private void Start()=>InitializeRuntime();
    private void InitializeRuntime()
    {
        if(initialized)return;initialized=true;if(string.IsNullOrWhiteSpace(RuntimeId))ConfigureGeneratedInstance(gameObject.name,Vector2Int.zero,roomData!=null?roomData.size:Vector2Int.one,0,internalRoomType);
        state=DungeonRunStateController.Active!=null?DungeonRunStateController.Active.GetRoom(RuntimeId):new SavedDungeonRoomState{roomId=RuntimeId,rules=Array.Empty<SavedDungeonRuleState>()};context=new RoomRuleContext(this,state);if(legacySpecialEntryLock&&roomData!=null&&!roomData.isStartRoom&&(roomData.isShopRoom||roomData.isTreasureRoom||roomData.isBlessedRoom||roomData.isEvilRoom))isLocked=true;if(isLocked)doorLocks.Add("legacy-entry");
        rules.AddRange(GetComponents<RoomRule>());
        bool hasEncounter=false;foreach(var rule in rules)if(rule is CombatRoomRule||rule is WaveRoomRule)hasEncounter=true;
        // Legacy spawners still get combat even when a reward/event rule is added.
        if(!hasEncounter&&GetComponentsInChildren<EnemySpawner>(true).Length>0)rules.Add(gameObject.AddComponent<CombatRoomRule>());
        bool hasModernReward=false;foreach(var rule in rules)if(rule is RoomRewardRule)hasModernReward=true;
        if(!hasModernReward&&roomData!=null&&roomData.rewards!=null&&roomData.rewards.Count>0)rules.Add(gameObject.AddComponent<LegacyRoomRewardRule>());
        var saved=new Dictionary<string,SavedDungeonRuleState>(StringComparer.Ordinal);if(state.rules!=null)foreach(var item in state.rules)if(item!=null&&!string.IsNullOrWhiteSpace(item.ruleId))saved[item.ruleId]=item;
        var ids=new HashSet<string>(StringComparer.Ordinal);foreach(var rule in rules)if(rule!=null){if(!ids.Add(rule.RuleId))Debug.LogError($"[Room] duplicate Rule ID '{rule.RuleId}' in {name}.",this);saved.TryGetValue(rule.RuleId,out var previous);rule.InitializeRule(context,previous);}
        roomCleared=state.completed;if(roomCleared){RefreshDoors();TrySpawnFloorPortal();}else RefreshDoors();
    }
    public RoomRule GetRule(string id){foreach(var rule in rules)if(rule!=null&&string.Equals(rule.RuleId,id,StringComparison.Ordinal))return rule;return null;}
    public bool CanOpenMenuHere()=>!isLocked&&!BattleActive&&AreConnectedDoorsOpen();
    public void OpenDoor(Vector2Int relativePos,Vector2Int direction){for(int i=0;i<doors.Count;i++)if(doors[i].gridOffset==relativePos&&doors[i].direction==direction){var d=doors[i];d.isConnected=true;doors[i]=d;RefreshDoors();return;}}
    public void UnlockSpecialRoom(){isLocked=false;ReleaseDoorLock("legacy-entry");}
    public void AcquireDoorLock(string reason){if(!string.IsNullOrWhiteSpace(reason)&&doorLocks.Add(reason))RefreshDoors();}
    public void ReleaseDoorLock(string reason){if(!string.IsNullOrWhiteSpace(reason)&&doorLocks.Remove(reason))RefreshDoors();}
    private void RefreshDoors(){bool locked=doorLocks.Count>0;foreach(var d in doors)if(d.isConnected){if(d.lockObject)d.lockObject.SetActive(locked);else if(d.wallObject)d.wallObject.SetActive(locked);if(d.doorObject)d.doorObject.SetActive(!locked);}}
    public void RegisterEnemy(GameObject enemy,string ownerId="legacy")
    {if(enemy==null)return;ownerId=ownerId=="legacy"?legacyEncounterOwner:ownerId;if(!activeEnemies.Contains(enemy))activeEnemies.Add(enemy);if(!encounterEnemies.TryGetValue(ownerId??"legacy",out var list)){list=new List<GameObject>();encounterEnemies[ownerId??"legacy"]=list;}if(!list.Contains(enemy))list.Add(enemy);enemy.SetActive(false);}
    public void AdoptEncounter(string oldOwner,string newOwner){if(oldOwner=="legacy")legacyEncounterOwner=newOwner;if(encounterEnemies.TryGetValue(oldOwner,out var list)){encounterEnemies.Remove(oldOwner);if(!encounterEnemies.TryGetValue(newOwner,out var target)){target=new List<GameObject>();encounterEnemies[newOwner]=target;}target.AddRange(list);}}
    public int GetEncounterEnemyCount(string ownerId){if(!encounterEnemies.TryGetValue(ownerId,out var list))return 0;list.RemoveAll(x=>x==null);return list.Count;}
    public void WakeUpEnemies(string ownerId){if(encounterEnemies.TryGetValue(ownerId,out var list))foreach(var enemy in list)if(enemy)enemy.SetActive(true);}
    public void ClearEncounter(string ownerId){if(!encounterEnemies.TryGetValue(ownerId,out var list))return;foreach(var enemy in list)if(enemy)Destroy(enemy);list.Clear();}
    public void EnemyDied(GameObject enemy){activeEnemies.Remove(enemy);string owner="legacy";foreach(var pair in encounterEnemies)if(pair.Value.Remove(enemy)){owner=pair.Key;break;}foreach(var rule in rules)if(rule!=null)rule.OnEnemyDied(enemy,owner);Persist();}
    public void BeginCombat(RoomRule source){if(source==null)return;AcquireDoorLock(source.RuleId);state.encounterInProgress=true;foreach(var rule in rules)if(rule!=null&&rule!=source)rule.OnEncounterStarted();Persist();}
    public void NotifyRuleChanged(RoomRule source){foreach(var rule in rules)if(rule!=null&&rule!=source)rule.OnRuleChanged(source);EvaluateCompletion();Persist();}
    private void EvaluateCompletion(){if(roomCleared)return;foreach(var rule in rules)if(rule!=null&&rule.BlocksRoomCompletion&&!rule.IsSatisfiedForRoomCompletion)return;roomCleared=true;state.completed=true;state.encounterInProgress=false;doorLocks.Clear();RefreshDoors();foreach(var rule in rules)if(rule!=null)rule.OnRoomCompleted();QuestEvents.Raise(QuestObjectiveEventType.ClearRoom,ResolveQuestTargetId(),ResolveQuestTargetTag());TrySpawnFloorPortal();}
    public SavedDungeonRuleState GetExternalState(string id){if(state.rules!=null)foreach(var item in state.rules)if(item!=null&&item.ruleId==id)return item;var created=new SavedDungeonRuleState{ruleId=id};var list=new List<SavedDungeonRuleState>(state.rules??Array.Empty<SavedDungeonRuleState>());list.Add(created);state.rules=list.ToArray();return created;}
    public void SaveExternalState(SavedDungeonRuleState external){Persist();}
    private void Persist(){if(state==null)return;state.visited|=PlayerHasEntered;state.completed=roomCleared;state.rules=ExportRules();DungeonRunStateController.Active?.MarkDirty();}
    private SavedDungeonRuleState[] ExportRules(){var data=new List<SavedDungeonRuleState>();var ids=new HashSet<string>();foreach(var r in rules)if(r!=null){data.Add(r.ExportState());ids.Add(r.RuleId);}if(state.rules!=null)foreach(var saved in state.rules)if(saved!=null&&!ids.Contains(saved.ruleId))data.Add(saved);return data.ToArray();}
    private void OnTriggerEnter(Collider other){if(!other.CompareTag("Player"))return;CurrentPlayerRoom=this;if(PlayerHasEntered||isLocked)return;bool first=!state.visited;PlayerHasEntered=true;state.visited=true;state.revealed=true;QuestEvents.Raise(QuestObjectiveEventType.EnterRoom,ResolveQuestTargetId(),ResolveQuestTargetTag());foreach(var rule in rules)if(rule!=null)rule.OnPlayerEntered(first);EvaluateCompletion();Persist();}
    private void OnTriggerStay(Collider other){if(other.CompareTag("Player"))CurrentPlayerRoom=this;}
    private void OnTriggerExit(Collider other){if(!other.CompareTag("Player"))return;if(CurrentPlayerRoom==this)CurrentPlayerRoom=null;foreach(var rule in rules)if(rule!=null)rule.OnPlayerExited();Persist();}
    private bool AreConnectedDoorsOpen(){foreach(var d in doors)if(d.isConnected&&((d.lockObject&&d.lockObject.activeInHierarchy)||(d.wallObject&&d.wallObject.activeInHierarchy)))return false;return true;}
    private void PruneEnemies(){activeEnemies.RemoveAll(x=>x==null);}
    private string ResolveQuestTargetId()=>!string.IsNullOrWhiteSpace(questTargetId)?questTargetId.Trim():roomData!=null&&!string.IsNullOrWhiteSpace(roomData.stableId)?roomData.stableId:gameObject.name;
    private string ResolveQuestTargetTag()=>!string.IsNullOrWhiteSpace(questTargetTag)?questTargetTag.Trim():PlacementRole??"room";
    private void TrySpawnFloorPortal(){if(spawnedPortal||roomData==null||!roomData.isBossRoom||!roomCleared)return;Vector3 pos=GetPortalTargetPosition();if(preplacedFloorPortal){spawnedPortal=preplacedFloorPortal;spawnedPortal.transform.position=pos;spawnedPortal.SetActive(true);}else if(floorPortalPrefab)spawnedPortal=Instantiate(floorPortalPrefab,pos,Quaternion.identity,transform);if(!spawnedPortal)return;if(!spawnedPortal.GetComponent<FloorPortal>())spawnedPortal.AddComponent<FloorPortal>();Collider col=spawnedPortal.GetComponent<Collider>();if(!col){BoxCollider box=spawnedPortal.AddComponent<BoxCollider>();box.size=new Vector3(1f,2f,1f);col=box;}col.isTrigger=true;}
    private Vector3 GetPortalTargetPosition(){Vector2Int entrance=Vector2Int.zero;foreach(var door in doors)if(door.isConnected){entrance=door.direction;break;}Vector3 direction=entrance==Vector2Int.zero?Vector3.forward:new Vector3(-entrance.x,0,-entrance.y).normalized;return transform.position+direction*portalDistanceFromCenter+portalSpawnOffset;}
}
