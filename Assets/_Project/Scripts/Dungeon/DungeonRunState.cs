using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public sealed class SavedDungeonRuleState { public string ruleId; public bool completed; public bool failed; public string payload; }
[Serializable] public sealed class SavedDungeonRoomState { public string roomId; public bool visited; public bool completed; public bool revealed; public bool rewardClaimed; public bool encounterInProgress; public SavedDungeonRuleState[] rules; }
[Serializable] public sealed class SavedDungeonFloorState { public int floorNumber; public SavedDungeonRoomState[] rooms; }
[Serializable] public sealed class SavedRunModifierState { public string modifierId; public int stacks; public RunModifierEffect effect; public float multiplierPerStack=1f; }
[Serializable] public sealed class SavedDungeonRunState
{
    public string runSeed; public int currentFloor; public SavedRunModifierState[] modifiers; public string[] oncePerRunEvents; public SavedDungeonFloorState currentFloorState;
    // v7 draft compatibility, retained for JsonUtility migration.
    public int floor; public SavedDungeonRoomState[] rooms;
}

/// <summary>Live authority for active-run data. GameData is only an import/export snapshot.</summary>
[DisallowMultipleComponent]
public sealed class DungeonRunStateController : MonoBehaviour
{
    private readonly Dictionary<string, SavedDungeonRoomState> rooms = new Dictionary<string, SavedDungeonRoomState>(StringComparer.Ordinal);
    private readonly HashSet<string> oncePerRunEvents = new HashSet<string>(StringComparer.Ordinal);
    private readonly List<SavedRunModifierState> modifiers = new List<SavedRunModifierState>();
    private string runSeed; private int currentFloor; private bool initialized;
    public static DungeonRunStateController Active { get; private set; }
    public bool IsInitialized => initialized; public string RunSeed => runSeed; public int CurrentFloor => currentFloor; public IReadOnlyList<SavedRunModifierState> Modifiers => modifiers;
    public void InitializeFromSave(string seed,int floorNumber,SavedDungeonRunState saved)
    {
        if(initialized){StartFloor(floorNumber);return;} initialized=true;runSeed=seed??string.Empty;currentFloor=Mathf.Max(1,floorNumber);rooms.Clear();modifiers.Clear();oncePerRunEvents.Clear();
        if(saved==null||!string.Equals(saved.runSeed??string.Empty,runSeed,StringComparison.Ordinal))return;
        if(saved.modifiers!=null)modifiers.AddRange(saved.modifiers); if(saved.oncePerRunEvents!=null)foreach(string id in saved.oncePerRunEvents)if(!string.IsNullOrWhiteSpace(id))oncePerRunEvents.Add(id);
        SavedDungeonFloorState floorState=saved.currentFloorState; if(floorState==null&&saved.rooms!=null)floorState=new SavedDungeonFloorState{floorNumber=saved.floor,rooms=saved.rooms}; if(floorState!=null&&floorState.floorNumber==currentFloor)ImportFloor(floorState);
    }
    public void StartFloor(int floorNumber){if(!initialized){InitializeFromSave(runSeed,floorNumber,null);return;}floorNumber=Mathf.Max(1,floorNumber);if(currentFloor==floorNumber)return;currentFloor=floorNumber;rooms.Clear();MarkDirty();}
    private void ImportFloor(SavedDungeonFloorState floorState){if(floorState.rooms==null)return;foreach(var state in floorState.rooms)if(state!=null&&!string.IsNullOrWhiteSpace(state.roomId)){state.encounterInProgress=false;rooms[state.roomId]=state;}}
    public SavedDungeonRoomState GetRoom(string id){if(string.IsNullOrWhiteSpace(id))return null;if(!rooms.TryGetValue(id,out var state)){state=new SavedDungeonRoomState{roomId=id,rules=Array.Empty<SavedDungeonRuleState>()};rooms.Add(id,state);}return state;}
    public bool TryGetRoom(string id,out SavedDungeonRoomState state)=>rooms.TryGetValue(id,out state);
    public bool ConsumeOncePerRun(string id){if(string.IsNullOrWhiteSpace(id)||!oncePerRunEvents.Add(id))return false;MarkDirty();return true;}
    public bool HasConsumedOncePerRun(string id)=>!string.IsNullOrWhiteSpace(id)&&oncePerRunEvents.Contains(id);
    public void SetModifiers(IEnumerable<SavedRunModifierState> values){modifiers.Clear();if(values!=null)modifiers.AddRange(values);MarkDirty();}
    public void MarkDirty(){if(initialized&&PlayerStats.instance!=null)PlayerStats.instance.SaveStats();}
    public SavedDungeonRunState Export()
    {
        if(CoreGenerator.Instance!=null&&MinimapManager.instance!=null)foreach(Room room in CoreGenerator.Instance.ActiveRooms)if(room!=null&&rooms.TryGetValue(room.RuntimeId,out var state)&&MinimapManager.instance.TryGetRoomVisibility(room.GridAnchor,out bool visited,out bool revealed)){state.visited|=visited;state.revealed|=revealed;}
        var records=new List<SavedDungeonRoomState>(rooms.Values);records.Sort((a,b)=>string.CompareOrdinal(a.roomId,b.roomId));var events=new List<string>(oncePerRunEvents);events.Sort(StringComparer.Ordinal);
        return new SavedDungeonRunState{runSeed=runSeed,currentFloor=currentFloor,modifiers=modifiers.ToArray(),oncePerRunEvents=events.ToArray(),currentFloorState=new SavedDungeonFloorState{floorNumber=currentFloor,rooms=records.ToArray()}};
    }
    public void ClearRun(){rooms.Clear();oncePerRunEvents.Clear();modifiers.Clear();runSeed=string.Empty;currentFloor=0;initialized=false;}
    private void Awake(){Active=this;} private void OnDestroy(){if(Active==this)Active=null;}
}

public static class DungeonDeterminism
{
    public static int Hash(params string[] values){unchecked{int hash=17;for(int i=0;i<values.Length;i++)foreach(char c in values[i]??string.Empty)hash=hash*31+c;return hash;}}
    public static System.Random Create(string runSeed,int floor,string roomId,string stream)=>new System.Random(Hash(runSeed,floor.ToString(),roomId,stream));
    public static string RoomId(string runSeed,int floor,Vector2Int anchor,string role,string definitionId)=>$"room|{runSeed}|f:{floor}|x:{anchor.x}|y:{anchor.y}|role:{role??string.Empty}|def:{definitionId??string.Empty}";
}
