using System;
using System.Collections.Generic;
using UnityEngine;

// JsonUtility-friendly DTOs.  No Unity object references are persisted here.
[Serializable] public sealed class SavedDungeonRuleState { public string ruleId; public bool completed; public bool failed; public string payload; }
[Serializable] public sealed class SavedDungeonRoomState
{
    public string roomId;
    public bool visited;
    public bool completed;
    public bool revealed;
    public bool rewardClaimed;
    public bool encounterInProgress;
    public SavedDungeonRuleState[] rules;
}
[Serializable] public sealed class SavedRunModifierState { public string modifierId; public int stacks; }
[Serializable] public sealed class SavedDungeonRunState
{
    public string runSeed;
    public int floor;
    public SavedDungeonRoomState[] rooms;
    public SavedRunModifierState[] modifiers;
    public string[] oncePerRunEvents;
}

/// <summary>Owns only the active-run snapshot. Incomplete encounters intentionally restart on load.</summary>
[DisallowMultipleComponent]
public sealed class DungeonRunStateController : MonoBehaviour
{
    private readonly Dictionary<string, SavedDungeonRoomState> rooms = new Dictionary<string, SavedDungeonRoomState>(StringComparer.Ordinal);
    private readonly HashSet<string> oncePerRunEvents = new HashSet<string>(StringComparer.Ordinal);
    private readonly List<SavedRunModifierState> modifiers = new List<SavedRunModifierState>();
    private string runSeed;
    private int floor;
    public static DungeonRunStateController Active { get; private set; }

    public void BeginOrRestore(string seed, int floorNumber, SavedDungeonRunState saved)
    {
        runSeed = seed ?? string.Empty;
        floor = Mathf.Max(1, floorNumber);
        rooms.Clear(); oncePerRunEvents.Clear(); modifiers.Clear();
        if (saved == null || saved.runSeed != runSeed || saved.floor != floor) return;
        if (saved.rooms != null) foreach (var state in saved.rooms)
            if (state != null && !string.IsNullOrWhiteSpace(state.roomId))
            {
                // Unfinished combat/waves/challenges restart deterministically rather than serializing transient actors.
                state.encounterInProgress = false;
                rooms[state.roomId] = state;
            }
        if (saved.oncePerRunEvents != null) foreach (var id in saved.oncePerRunEvents) if (!string.IsNullOrWhiteSpace(id)) oncePerRunEvents.Add(id);
        if (saved.modifiers != null) modifiers.AddRange(saved.modifiers);
    }

    public SavedDungeonRoomState GetRoom(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (!rooms.TryGetValue(id, out var state))
        {
            state = new SavedDungeonRoomState { roomId = id, rules = Array.Empty<SavedDungeonRuleState>() };
            rooms.Add(id, state);
        }
        return state;
    }
    public bool TryGetRoom(string id, out SavedDungeonRoomState state) => rooms.TryGetValue(id, out state);
    public bool ConsumeOncePerRun(string id) => !string.IsNullOrWhiteSpace(id) && oncePerRunEvents.Add(id);
    public IReadOnlyList<SavedRunModifierState> Modifiers => modifiers;
    public void SetModifiers(IEnumerable<SavedRunModifierState> values) { modifiers.Clear(); if (values != null) modifiers.AddRange(values); }
    public SavedDungeonRunState Export()
    {
        // Minimap fog is presentation state, but it is backed into the same
        // deterministic room records so a reload does not erase exploration.
        if (CoreGenerator.Instance != null && MinimapManager.instance != null)
            foreach (Room room in CoreGenerator.Instance.ActiveRooms)
                if (room != null && rooms.TryGetValue(room.RuntimeId, out SavedDungeonRoomState state)
                    && MinimapManager.instance.TryGetRoomVisibility(room.GridAnchor, out bool visited, out bool revealed))
                { state.visited |= visited; state.revealed |= revealed; }
        var list = new List<SavedDungeonRoomState>(rooms.Values); list.Sort((a,b) => string.CompareOrdinal(a.roomId,b.roomId));
        return new SavedDungeonRunState { runSeed = runSeed, floor = floor, rooms = list.ToArray(), modifiers = modifiers.ToArray(), oncePerRunEvents = new List<string>(oncePerRunEvents).ToArray() };
    }
    public void ClearRun() { rooms.Clear(); oncePerRunEvents.Clear(); modifiers.Clear(); runSeed = string.Empty; floor = 0; }
    private void Awake() { Active = this; }
    private void OnDestroy() { if (Active == this) Active = null; }
}

public static class DungeonDeterminism
{
    public static int Hash(params string[] values)
    {
        unchecked { int hash = 17; for (int i=0;i<values.Length;i++) foreach(char c in values[i] ?? string.Empty) hash = hash * 31 + c; return hash; }
    }
    public static System.Random Create(string runSeed, int floor, string roomId, string stream) => new System.Random(Hash(runSeed, floor.ToString(), roomId, stream));
    public static string RoomId(string runSeed, int floor, Vector2Int anchor, string role, string definitionId) =>
        $"r:{Hash(runSeed, floor.ToString(), anchor.x.ToString(), anchor.y.ToString(), role ?? string.Empty, definitionId ?? string.Empty):X8}";
}
