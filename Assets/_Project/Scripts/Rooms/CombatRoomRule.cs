using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatRoomRule : RoomRule, ITriggeredRoomEncounter
{
    public override bool BlocksRoomCompletion => true;
    [SerializeField] private bool startOnPlayerEntry = true;
    private bool started;
    protected override void OnStateRestored(string payload) { started = false; } // unfinished encounters deliberately restart
    protected override void OnRoomInitialized()
    {
        Context.Room.AdoptEncounter("legacy", RuleId);
    }
    public override void OnPlayerEntered(bool firstVisit)
    {
        if (!startOnPlayerEntry) return;
        TryStartFromTrigger();
    }
    public bool TryStartFromTrigger()
    {
        if (IsResolved || started) return false;
        started = true; StartRunning(); Context.Room.BeginCombat(this); Context.Room.WakeUpEnemies(RuleId);
        if (Context.Room.GetEncounterEnemyCount(RuleId) == 0) Complete();
        return true;
    }
    public bool CanStartFromTrigger()=>!IsResolved&&!started;
    public override void OnEnemyDied(GameObject enemy, string ownerId)
    {
        if (started && !IsResolved && ownerId == RuleId && Context.Room.GetEncounterEnemyCount(RuleId) == 0) Complete();
    }
    protected override string CaptureState() => started ? "started" : string.Empty;
}
