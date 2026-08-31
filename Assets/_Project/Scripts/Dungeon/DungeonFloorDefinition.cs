using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName="DungeonFloor", menuName="Dungeon/Floor Definition")]
public sealed class DungeonFloorDefinition : ScriptableObject
{
    [Serializable]
    public sealed class RoomCount
    {
        [Tooltip("Disabled categories are not placed. Normal is enabled by default for legacy floor assets.")]
        public bool enabled = true;
        [Min(0)] public int min = 0;
        [Min(0)] public int max = 0;
        public int Resolve(System.Random random) => !enabled ? 0 : random.Next(Mathf.Min(min, max), Mathf.Max(min, max) + 1);
        public void Normalize() { if (max < min) max = min; }
    }

    [Serializable]
    public sealed class RoomSetChoice
    {
        public DungeonRoomSet roomSet;
        [Min(1)] public int weight = 1;
    }

    [Header("Placement counts")]
    [Tooltip("Total Normal slots. Combat, puzzle and traversal prefabs are selected from the same pool.")]
    public RoomCount normalRooms = new RoomCount { min = 15, max = 15 };
    public RoomCount shopRooms = new RoomCount { min = 1, max = 1 };
    public RoomCount treasureRooms = new RoomCount { min = 1, max = 1 };
    public RoomCount bossRooms = new RoomCount { min = 1, max = 1 };
    [Tooltip("Total special Challenge slots. The prefab rule determines Wave, No Healing or another future variant.")]
    public RoomCount challengeRooms = new RoomCount { enabled = false, min = 0, max = 0 };
    [SerializeField, HideInInspector, FormerlySerializedAs("waveRooms")]
    private RoomCount legacyWaveRooms = new RoomCount { enabled = false, min = 0, max = 0 };
    public RoomCount minibossRooms = new RoomCount { enabled = false, min = 0, max = 0 };
    [SerializeField, HideInInspector, FormerlySerializedAs("parkourRooms")]
    private RoomCount legacyParkourRooms = new RoomCount { enabled = false, min = 0, max = 0 };
    public RoomCount npcEncounterRooms = new RoomCount { enabled = false, min = 0, max = 0 };
    [FormerlySerializedAs("curchRooms")]
    public RoomCount churchRooms = new RoomCount { enabled = false };
    [Tooltip("Generated SecretAccess rooms whose authored prefab contains a normal-secret discovery mechanism.")]
    public RoomCount secretAccessSecretRooms = new RoomCount { enabled = false, min = 0, max = 0 };
    [Tooltip("Generated SecretAccess rooms whose authored prefab contains a super-secret discovery mechanism.")]
    public RoomCount secretAccessSuperSecretRooms = new RoomCount { enabled = false, min = 0, max = 0 };

    [Header("Content pools")]
    [Tooltip("Optional weighted room-set override. Empty means the selected theme room set remains authoritative.")]
    public List<RoomSetChoice> allowedRoomSets = new List<RoomSetChoice>();
    public List<SpawnTable> enemyPools = new List<SpawnTable>();
    [Tooltip("Authoring switches consumed by room rules/encounters on this floor.")]
    public bool challengesAvailable = true;
    public bool shrinesAvailable = true;

    public RoomCount GetCount(RoomType roomType)
    {
        switch (roomType)
        {
            case RoomType.Normal: return normalRooms;
            case RoomType.Shop: return shopRooms;
            case RoomType.Treasure: return treasureRooms;
            case RoomType.Boss: return bossRooms;
            case RoomType.Challenge: return challengeRooms;
            case RoomType.Miniboss: return minibossRooms;
            case RoomType.NpcEncounter: return npcEncounterRooms;
            case RoomType.Curch: return churchRooms;
            default: return null;
        }
    }

    public RoomCount GetChurchCount() => churchRooms;

    public int ResolveChallengeCount(System.Random challengeRandom, System.Random legacyWaveRandom)
    {
        int challengeCount = challengeRooms == null ? 0 : challengeRooms.Resolve(challengeRandom);
        int legacyWaveCount = legacyWaveRooms == null ? 0 : legacyWaveRooms.Resolve(legacyWaveRandom);
        return challengeCount + legacyWaveCount;
    }

    public RoomCount GetSecretAccessCount(bool superSecret)
    {
        return superSecret ? secretAccessSuperSecretRooms : secretAccessSecretRooms;
    }

    private void OnValidate()
    {
        normalRooms?.Normalize(); shopRooms?.Normalize(); treasureRooms?.Normalize(); bossRooms?.Normalize(); challengeRooms?.Normalize(); legacyWaveRooms?.Normalize(); minibossRooms?.Normalize(); legacyParkourRooms?.Normalize(); npcEncounterRooms?.Normalize(); churchRooms?.Normalize(); secretAccessSecretRooms?.Normalize(); secretAccessSuperSecretRooms?.Normalize();
        MergeLegacyWaveCount();
    }

    private void MergeLegacyWaveCount()
    {
        if (legacyWaveRooms == null || !legacyWaveRooms.enabled || (legacyWaveRooms.min <= 0 && legacyWaveRooms.max <= 0))
            return;

        if (challengeRooms == null)
            challengeRooms = new RoomCount { enabled = false, min = 0, max = 0 };

        challengeRooms.enabled = challengeRooms.enabled || legacyWaveRooms.enabled;
        challengeRooms.min += legacyWaveRooms.min;
        challengeRooms.max += legacyWaveRooms.max;
        challengeRooms.Normalize();
        legacyWaveRooms = new RoomCount { enabled = false, min = 0, max = 0 };
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
}
