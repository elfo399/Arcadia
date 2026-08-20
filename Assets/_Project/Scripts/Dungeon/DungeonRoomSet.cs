using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "DungeonRoomSet", menuName = "Dungeon/Room Set")]
public class DungeonRoomSet : ScriptableObject
{
    [Header("Start")]
    public Room startRoomPrefab;

    [Header("Normal (combat, puzzle, traversal)")]
    public Room[] normal1x1Variants;
    public Room[] normal2x1Variants;
    public Room[] normal1x2Variants;
    public Room[] normal2x2Variants;

    [Header("Boss")]
    public Room[] boss1x1Variants;
    public Room[] boss2x1Variants;
    public Room[] boss1x2Variants;
    public Room[] boss2x2Variants;

    [Header("Treasure")]
    public Room[] treasure1x1Variants;
    public Room[] treasure2x1Variants;
    public Room[] treasure1x2Variants;
    public Room[] treasure2x2Variants;

    [Header("Shop")]
    public Room[] shop1x1Variants;
    public Room[] shop2x1Variants;
    public Room[] shop1x2Variants;
    public Room[] shop2x2Variants;

    [Header("Curch")]
    public Room[] curch1x1Variants;
    public Room[] curch2x2Variants;

    [Header("Evil Curch")]
    public Room[] evilCurch1x1Variants;
    public Room[] evilCurch2x2Variants;

    [Header("Secret Access / Secret")]
    public Room[] secretAccessSecret1x1Variants;
    public Room[] secretAccessSecret2x1Variants;
    public Room[] secretAccessSecret1x2Variants;
    public Room[] secretAccessSecret2x2Variants;

    [Header("Secret Access / Super Secret")]
    public Room[] secretAccessSuperSecret1x1Variants;
    public Room[] secretAccessSuperSecret2x1Variants;
    public Room[] secretAccessSuperSecret1x2Variants;
    public Room[] secretAccessSuperSecret2x2Variants;

    [Header("Challenge (wave, no-heal, future variants)")]
    public Room[] challenge1x1Variants;
    public Room[] challenge2x1Variants;
    public Room[] challenge1x2Variants;
    public Room[] challenge2x2Variants;

    // Kept only as an import bridge. OnValidate moves old Wave entries into the
    // visible Challenge arrays; runtime accessors also merge them in player builds.
    [SerializeField, HideInInspector, FormerlySerializedAs("wave1x1Variants")]
    private Room[] legacyWave1x1Variants;
    [SerializeField, HideInInspector, FormerlySerializedAs("wave2x1Variants")]
    private Room[] legacyWave2x1Variants;
    [SerializeField, HideInInspector, FormerlySerializedAs("wave1x2Variants")]
    private Room[] legacyWave1x2Variants;
    [SerializeField, HideInInspector, FormerlySerializedAs("wave2x2Variants")]
    private Room[] legacyWave2x2Variants;

    [Header("Miniboss")]
    public Room[] miniboss1x1Variants;
    public Room[] miniboss2x1Variants;
    public Room[] miniboss1x2Variants;
    public Room[] miniboss2x2Variants;

    // Parkour is Normal content now. These hidden fields import existing room-set
    // assets without keeping Parkour as a selectable structural pool.
    [SerializeField, HideInInspector, FormerlySerializedAs("parkour1x1Variants")]
    private Room[] legacyParkour1x1Variants;
    [SerializeField, HideInInspector, FormerlySerializedAs("parkour2x1Variants")]
    private Room[] legacyParkour2x1Variants;
    [SerializeField, HideInInspector, FormerlySerializedAs("parkour1x2Variants")]
    private Room[] legacyParkour1x2Variants;
    [SerializeField, HideInInspector, FormerlySerializedAs("parkour2x2Variants")]
    private Room[] legacyParkour2x2Variants;

    [Header("NPC Encounter")]
    public Room[] npcEncounter1x1Variants;
    public Room[] npcEncounter2x1Variants;
    public Room[] npcEncounter1x2Variants;
    public Room[] npcEncounter2x2Variants;

    public Room[] GetNormal1x1Variants() => MergeVariants(normal1x1Variants, legacyParkour1x1Variants);
    public Room[] GetNormal2x1Variants() => MergeVariants(normal2x1Variants, legacyParkour2x1Variants);
    public Room[] GetNormal1x2Variants() => MergeVariants(normal1x2Variants, legacyParkour1x2Variants);
    public Room[] GetNormal2x2Variants() => MergeVariants(normal2x2Variants, legacyParkour2x2Variants);

    public Room[] GetChallenge1x1Variants() => MergeVariants(challenge1x1Variants, legacyWave1x1Variants);
    public Room[] GetChallenge2x1Variants() => MergeVariants(challenge2x1Variants, legacyWave2x1Variants);
    public Room[] GetChallenge1x2Variants() => MergeVariants(challenge1x2Variants, legacyWave1x2Variants);
    public Room[] GetChallenge2x2Variants() => MergeVariants(challenge2x2Variants, legacyWave2x2Variants);

    private static Room[] MergeVariants(params Room[][] groups)
    {
        var merged = new List<Room>();
        if (groups == null)
            return merged.ToArray();

        foreach (Room[] group in groups)
        {
            if (group == null)
                continue;
            foreach (Room room in group)
                if (room != null && !merged.Contains(room))
                    merged.Add(room);
        }

        return merged.ToArray();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        bool migrated = false;
        migrated |= MergeLegacy(ref normal1x1Variants, ref legacyParkour1x1Variants);
        migrated |= MergeLegacy(ref normal2x1Variants, ref legacyParkour2x1Variants);
        migrated |= MergeLegacy(ref normal1x2Variants, ref legacyParkour1x2Variants);
        migrated |= MergeLegacy(ref normal2x2Variants, ref legacyParkour2x2Variants);
        migrated |= MergeLegacy(ref challenge1x1Variants, ref legacyWave1x1Variants);
        migrated |= MergeLegacy(ref challenge2x1Variants, ref legacyWave2x1Variants);
        migrated |= MergeLegacy(ref challenge1x2Variants, ref legacyWave1x2Variants);
        migrated |= MergeLegacy(ref challenge2x2Variants, ref legacyWave2x2Variants);
        if (migrated)
            UnityEditor.EditorUtility.SetDirty(this);
    }

    private static bool MergeLegacy(ref Room[] destination, ref Room[] legacy)
    {
        if (legacy == null || legacy.Length == 0)
            return false;

        destination = MergeVariants(destination, legacy);
        legacy = new Room[0];
        return true;
    }
#endif
}
