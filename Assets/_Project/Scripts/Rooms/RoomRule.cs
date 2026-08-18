using System;
using UnityEngine;

public sealed class RoomRuleContext
{
    public Room Room { get; private set; }
    public SavedDungeonRoomState State { get; private set; }
    public System.Random CreateRandom(string stream) => DungeonDeterminism.Create(CoreGenerator.Instance != null ? CoreGenerator.Instance.gameSeedString : string.Empty, Room.Floor, Room.RuntimeId, stream);
    internal RoomRuleContext(Room room, SavedDungeonRoomState state) { Room = room; State = state; }
}

public abstract class RoomRule : MonoBehaviour
{
    [SerializeField] private string ruleId = "rule";
    [SerializeField] private bool blocksRoomCompletion = true;
    [SerializeField] private bool locksConnectedDoors;
    protected RoomRuleContext Context { get; private set; }
    public string RuleId => string.IsNullOrWhiteSpace(ruleId) ? GetType().Name : ruleId.Trim();
    public bool BlocksRoomCompletion => blocksRoomCompletion;
    public bool LocksConnectedDoors => locksConnectedDoors;
    public bool IsCompleted { get; private set; }
    public bool IsFailed { get; private set; }
    internal void InitializeRule(RoomRuleContext context, SavedDungeonRuleState saved)
    {
        Context = context; IsCompleted = saved != null && saved.completed; IsFailed = saved != null && saved.failed;
        OnRoomInitialized(); if (saved != null) OnStateRestored(saved.payload);
    }
    internal SavedDungeonRuleState ExportState() => new SavedDungeonRuleState { ruleId = RuleId, completed = IsCompleted, failed = IsFailed, payload = CaptureState() };
    protected void Complete() { if (IsCompleted) return; IsCompleted = true; Context.Room.NotifyRuleChanged(this); }
    protected void Fail() { if (IsFailed) return; IsFailed = true; Context.Room.NotifyRuleChanged(this); }
    protected virtual void OnRoomInitialized() { }
    protected virtual void OnStateRestored(string payload) { }
    protected virtual string CaptureState() => string.Empty;
    public virtual void OnPlayerEntered() { }
    public virtual void OnPlayerExited() { }
    public virtual void OnEncounterStarted() { }
    public virtual void OnEnemyDied(GameObject enemy) { }
    public virtual void OnRoomCompleted() { }
}
