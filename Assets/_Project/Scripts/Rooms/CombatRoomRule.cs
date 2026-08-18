using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatRoomRule : RoomRule
{
    [SerializeField] private bool startOnPlayerEntry = true;
    private bool started;
    protected override void OnStateRestored(string payload) { started = false; } // unfinished encounters deliberately restart
    public override void OnPlayerEntered()
    {
        if (IsCompleted || !startOnPlayerEntry) return;
        started = true; Context.Room.BeginCombat(this); Context.Room.WakeUpEnemies();
        if (Context.Room.ActiveEnemyCount == 0) Complete();
    }
    public override void OnEnemyDied(GameObject enemy)
    {
        if (started && !IsCompleted && Context.Room.ActiveEnemyCount == 0) Complete();
    }
    protected override string CaptureState() => started ? "started" : string.Empty;
}
