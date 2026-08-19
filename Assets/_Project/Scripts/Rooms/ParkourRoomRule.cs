using UnityEngine;

/// <summary>Blocks a Parkour room until its authored traversal objective signals completion.</summary>
[DisallowMultipleComponent]
public sealed class ParkourRoomRule : RoomRule
{
    public override bool BlocksRoomCompletion => true;
    [SerializeField] private bool startOnPlayerEntry = true;

    protected override void OnRoomInitialized()
    {
        if (Outcome == RoomRuleOutcome.Running)
            Context.Room.AcquireDoorLock(RuleId);
    }

    public override void OnPlayerEntered(bool firstVisit)
    {
        if (startOnPlayerEntry)
            StartTraversal();
    }

    public void StartTraversal()
    {
        if (IsResolved || Outcome == RoomRuleOutcome.Running)
            return;

        Context.Room.AcquireDoorLock(RuleId);
        StartRunning();
    }

    /// <summary>Call from an authored traversal trigger or a UnityEvent when the path/puzzle is complete.</summary>
    public void CompleteTraversal()
    {
        if (IsResolved)
            return;

        if (Outcome != RoomRuleOutcome.Running)
            StartTraversal();
        Complete();
    }
}
