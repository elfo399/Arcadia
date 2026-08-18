using System;
using UnityEngine;

public enum RoomRuleOutcome { Pending, Running, Succeeded, Failed }
public sealed class RoomRuleContext
{
    public Room Room { get; private set; } public SavedDungeonRoomState State { get; private set; }
    public System.Random CreateRandom(string stream)=>DungeonDeterminism.Create(DungeonRunStateController.Active!=null?DungeonRunStateController.Active.RunSeed:string.Empty,Room.Floor,Room.RuntimeId,stream);
    internal RoomRuleContext(Room room,SavedDungeonRoomState state){Room=room;State=state;}
}

public abstract class RoomRule : MonoBehaviour
{
    [SerializeField] private string ruleId;
    [SerializeField] private bool blocksRoomCompletion=true;
    [SerializeField] private bool locksConnectedDoors;
    protected RoomRuleContext Context { get; private set; }
    public string RuleId=>string.IsNullOrWhiteSpace(ruleId)?GetType().Name:ruleId.Trim();
    public void SetEditorRuleId(string value){if(!string.IsNullOrWhiteSpace(value))ruleId=value.Trim();}
#if UNITY_EDITOR
    protected virtual void OnValidate(){if(string.IsNullOrWhiteSpace(ruleId)){ruleId="rule-"+Guid.NewGuid().ToString("N");UnityEditor.EditorUtility.SetDirty(this);}}
#endif
    public virtual bool BlocksRoomCompletion=>blocksRoomCompletion;
    public bool LocksConnectedDoors=>locksConnectedDoors;
    public RoomRuleOutcome Outcome { get; private set; }
    public bool IsCompleted=>Outcome==RoomRuleOutcome.Succeeded;
    public bool IsFailed=>Outcome==RoomRuleOutcome.Failed;
    public bool IsResolved=>IsCompleted||IsFailed;
    public virtual bool IsSatisfiedForRoomCompletion => IsCompleted;
    internal void InitializeRule(RoomRuleContext context,SavedDungeonRuleState saved)
    {
        Context=context; Outcome=saved!=null?(saved.completed?RoomRuleOutcome.Succeeded:saved.failed?RoomRuleOutcome.Failed:RoomRuleOutcome.Pending):RoomRuleOutcome.Pending;
        OnRoomInitialized();if(saved!=null)OnStateRestored(saved.payload);
    }
    internal SavedDungeonRuleState ExportState()=>new SavedDungeonRuleState{ruleId=RuleId,completed=IsCompleted,failed=IsFailed,payload=CaptureState()};
    protected void StartRunning(){if(IsResolved)return;Outcome=RoomRuleOutcome.Running;Context.Room.NotifyRuleChanged(this);}
    protected void Complete(){if(IsResolved)return;Outcome=RoomRuleOutcome.Succeeded;Context.Room.ReleaseDoorLock(RuleId);Context.Room.NotifyRuleChanged(this);}
    protected void Fail(){if(IsResolved)return;Outcome=RoomRuleOutcome.Failed;Context.Room.ReleaseDoorLock(RuleId);Context.Room.NotifyRuleChanged(this);}
    protected void ResetForRetry(){if(!IsFailed)return;Outcome=RoomRuleOutcome.Pending;Context.Room.NotifyRuleChanged(this);}
    protected void ResetFailedAttempt(){Outcome=RoomRuleOutcome.Pending;Context.Room.ReleaseDoorLock(RuleId);Context.Room.NotifyRuleChanged(this);}
    protected void AcquireDoorLock(){if(LocksConnectedDoors)Context.Room.AcquireDoorLock(RuleId);}
    protected virtual void OnRoomInitialized(){}
    protected virtual void OnStateRestored(string payload){}
    protected virtual string CaptureState()=>string.Empty;
    public virtual void OnPlayerEntered(bool firstVisit){}
    public virtual void OnPlayerExited(){}
    public virtual void OnEncounterStarted(){}
    public virtual void OnEnemyDied(GameObject enemy,string ownerId){}
    public virtual void OnRuleChanged(RoomRule rule){}
    public virtual void OnRoomCompleted(){}
}
