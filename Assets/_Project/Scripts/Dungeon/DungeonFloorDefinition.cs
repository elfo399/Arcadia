using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName="DungeonFloor", menuName="Dungeon/Floor Definition")]
public sealed class DungeonFloorDefinition : ScriptableObject
{
    public enum DungeonMoralRoomPolicy { Independent, AlignmentExclusive }
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
    [Tooltip("min == max creates an exact quantity. These counts replace CoreGenerator's legacy fixed special rooms when this asset is assigned.")]
    public RoomCount normalRooms = new RoomCount { min = 15, max = 15 };
    public RoomCount shopRooms = new RoomCount { min = 1, max = 1 };
    public RoomCount treasureRooms = new RoomCount { min = 1, max = 1 };
    public RoomCount bossRooms = new RoomCount { min = 1, max = 1 };
    public RoomCount curchRooms = new RoomCount { enabled = false };
    public RoomCount evilCurchRooms = new RoomCount { enabled = false };
    [Tooltip("Independent allows both authored moral room categories. AlignmentExclusive selects only the stronger alignment; ties spawn neither.")]
    public DungeonMoralRoomPolicy moralRoomPolicy = DungeonMoralRoomPolicy.Independent;

    [Header("Content pools")]
    [Tooltip("Optional weighted room-set override. Empty means the selected theme room set remains authoritative.")]
    public List<RoomSetChoice> allowedRoomSets = new List<RoomSetChoice>();
    public List<LootPoolDefinition> lootPools = new List<LootPoolDefinition>();
    public List<SpawnTable> enemyPools = new List<SpawnTable>();
    [Tooltip("Authoring switches consumed by room rules/encounters on this floor.")]
    public bool challengesAvailable = true;
    public bool shrinesAvailable = true;
    public bool minibossesAvailable = true;

    public RoomCount GetCount(string category)
    {
        switch (category)
        {
            case "Normal": return normalRooms;
            case "Shop": return shopRooms;
            case "Treasure": return treasureRooms;
            case "Boss": return bossRooms;
            case "Curch": return curchRooms;
            case "EvilCurch": return evilCurchRooms;
            default: return null;
        }
    }

    private void OnValidate()
    {
        normalRooms?.Normalize(); shopRooms?.Normalize(); treasureRooms?.Normalize(); bossRooms?.Normalize(); curchRooms?.Normalize(); evilCurchRooms?.Normalize();
    }
}
