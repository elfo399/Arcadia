using System.Collections.Generic;
using UnityEngine;

public enum DungeonChallengeMode
{
    Wave = 0,
    TimedKill = 1,
    PerfectCombat = 2,
    NoHealing = 3
}

/// <summary>
/// Voluntary wave-based Challenge variant. The mode adds the authored constraint;
/// future Challenge implementations can use a different IChallengeRoomVariant rule.
/// </summary>
public sealed class ChallengeRoomRule : RoomRule, IInteractable, ITriggeredRoomEncounter, IChallengeRoomVariant
{
    public override bool BlocksRoomCompletion => floorAllowsChallenge;

    [SerializeField] private DungeonChallengeMode mode;
    [SerializeField] private List<DungeonWaveDefinition> waves = new List<DungeonWaveDefinition>();
    [SerializeField, Min(1f)] private float timeLimitSeconds = 30f;
    [SerializeField] private bool failureCompletesRoom = true;
    [SerializeField] private bool allowRetry = true;
    [SerializeField] private string prompt = "Accept challenge";

    private bool active;
    private int waveIndex = -1;
    private float remaining;
    private bool floorAllowsChallenge = true;
    private PlayerStats healingBlockedPlayer;
    private string healingBlockId;

    public override bool IsSatisfiedForRoomCompletion => IsCompleted || (IsFailed && failureCompletesRoom);

    protected override void OnRoomInitialized()
    {
        remaining = timeLimitSeconds;
        floorAllowsChallenge = CoreGenerator.Instance == null
            || CoreGenerator.Instance.ActiveFloorDefinition == null
            || CoreGenerator.Instance.ActiveFloorDefinition.challengesAvailable;
    }

    private void OnEnable()
    {
        PlayerStats.DamageTaken += HandlePlayerDamage;
        if (active && mode == DungeonChallengeMode.NoHealing)
            SetHealingBlocked(true);
    }

    private void OnDisable()
    {
        PlayerStats.DamageTaken -= HandlePlayerDamage;
        SetHealingBlocked(false);
    }

    public void Interact(GameObject player)
    {
        if (floorAllowsChallenge && !active && !IsResolved)
            StartChallenge();
    }

    public string GetPrompt()
    {
        return !floorAllowsChallenge || active || IsCompleted || IsFailed ? string.Empty : prompt;
    }

    private bool StartChallenge()
    {
        if (!CanStartFromTrigger())
            return false;

        active = true;
        remaining = timeLimitSeconds;
        SetHealingBlocked(mode == DungeonChallengeMode.NoHealing);
        StartRunning();
        Context.Room.BeginCombat(this);
        StartNextWave();
        return true;
    }

    public bool TryStartFromTrigger() => StartChallenge();

    public bool CanStartFromTrigger()
    {
        if (!floorAllowsChallenge || active || IsResolved || waves.Count == 0
            || GetComponentsInChildren<DungeonWaveSpawnPoint>(true).Length == 0)
            return false;

        foreach (DungeonWaveDefinition wave in waves)
        {
            List<SpawnTable> pools = wave != null ? wave.enemyPools : null;
            if (pools == null || pools.Count == 0)
                pools = CoreGenerator.Instance != null && CoreGenerator.Instance.ActiveFloorDefinition != null
                    ? CoreGenerator.Instance.ActiveFloorDefinition.enemyPools
                    : null;
            if (pools == null || pools.Find(pool => pool != null) == null)
                return false;
        }

        return true;
    }

    private void StartNextWave()
    {
        waveIndex++;
        if (waveIndex >= waves.Count)
        {
            active = false;
            SetHealingBlocked(false);
            Complete();
            return;
        }

        DungeonWaveSpawnPoint[] points = GetComponentsInChildren<DungeonWaveSpawnPoint>(true);
        DungeonWaveDefinition wave = waves[waveIndex];
        List<SpawnTable> pools = wave != null ? wave.enemyPools : null;
        if (pools == null || pools.Count == 0)
            pools = CoreGenerator.Instance != null && CoreGenerator.Instance.ActiveFloorDefinition != null
                ? CoreGenerator.Instance.ActiveFloorDefinition.enemyPools
                : null;

        var validPools = new List<SpawnTable>();
        if (pools != null)
            foreach (SpawnTable pool in pools)
                if (pool != null)
                    validPools.Add(pool);

        if (points.Length == 0 || validPools.Count == 0)
        {
            EndFailure();
            return;
        }

        System.Random random = Context.CreateRandom(RuleId + ":challenge:" + waveIndex);
        foreach (DungeonWaveSpawnPoint point in points)
            point.Spawn(validPools[random.Next(validPools.Count)], random, Context.Room, RuleId);
        Context.Room.WakeUpEnemies(RuleId);

        if (Context.Room.GetEncounterEnemyCount(RuleId) == 0)
            StartNextWave();
    }

    private void Update()
    {
        if (!active)
            return;

        // Also handles a late-spawned/replaced player and runtime mode changes.
        SetHealingBlocked(mode == DungeonChallengeMode.NoHealing);
        if (mode == DungeonChallengeMode.TimedKill && (remaining -= Time.deltaTime) <= 0f)
            EndFailure();
    }

    public override void OnEnemyDied(GameObject enemy, string ownerId)
    {
        if (active && ownerId == RuleId && Context.Room.GetEncounterEnemyCount(RuleId) == 0)
            StartNextWave();
    }

    public override void OnRoomCompleted()
    {
        SetHealingBlocked(false);
    }

    private void HandlePlayerDamage(float amount)
    {
        if (active && mode == DungeonChallengeMode.PerfectCombat && amount > 0f)
            EndFailure();
    }

    private void EndFailure()
    {
        if (!active)
            return;

        SetHealingBlocked(false);
        if (allowRetry)
        {
            ResetAttempt();
            ResetFailedAttempt();
        }
        else
        {
            active = false;
            Context.Room.ClearEncounter(RuleId);
            Fail();
        }
    }

    protected override void OnStateRestored(string payload)
    {
        ResetAttempt();
    }

    protected override string CaptureState()
    {
        return IsCompleted ? "success" : IsFailed ? "failed" : string.Empty;
    }

    private void ResetAttempt()
    {
        active = false;
        waveIndex = -1;
        remaining = timeLimitSeconds;
        SetHealingBlocked(false);
        if (Context != null)
        {
            Context.Room.ClearEncounter(RuleId);
            Context.Room.ReleaseDoorLock(RuleId);
        }
    }

    private void SetHealingBlocked(bool shouldBlock)
    {
        if (!shouldBlock)
        {
            if (healingBlockedPlayer != null)
                healingBlockedPlayer.ReleaseHealingBlock(GetHealingBlockId());
            healingBlockedPlayer = null;
            return;
        }

        PlayerStats target = PlayerStats.instance;
        if (target == null)
            return;

        if (healingBlockedPlayer != null && healingBlockedPlayer != target)
            healingBlockedPlayer.ReleaseHealingBlock(GetHealingBlockId());

        target.AcquireHealingBlock(GetHealingBlockId());
        healingBlockedPlayer = target;
    }

    private string GetHealingBlockId()
    {
        if (!string.IsNullOrEmpty(healingBlockId))
            return healingBlockId;

        string roomId = Context != null && Context.Room != null ? Context.Room.RuntimeId : null;
        healingBlockId = !string.IsNullOrWhiteSpace(roomId)
            ? "challenge:" + roomId + ":" + RuleId
            : "challenge-instance:" + GetInstanceID();
        return healingBlockId;
    }
}
