using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class WeightedRoomVariant
{
    public Room room;
    [Min(0f), Tooltip("Relative spawn weight. The total does not need to be 100.")]
    public float spawnWeight = 1f;
}

[CreateAssetMenu(fileName = "DungeonRoomSet", menuName = "Dungeon/Room Set")]
public class DungeonRoomSet : ScriptableObject
{
    [Header("Start")]
    public Room startRoomPrefab;

    [Header("Normal (combat, puzzle, traversal)")]
    public WeightedRoomVariant[] normal1x1WeightedVariants;
    public WeightedRoomVariant[] normal2x1WeightedVariants;
    public WeightedRoomVariant[] normal1x2WeightedVariants;
    public WeightedRoomVariant[] normal2x2WeightedVariants;

    [Header("Boss")]
    public WeightedRoomVariant[] boss1x1WeightedVariants;
    public WeightedRoomVariant[] boss2x1WeightedVariants;
    public WeightedRoomVariant[] boss1x2WeightedVariants;
    public WeightedRoomVariant[] boss2x2WeightedVariants;

    [Header("Treasure")]
    public WeightedRoomVariant[] treasure1x1WeightedVariants;
    public WeightedRoomVariant[] treasure2x1WeightedVariants;
    public WeightedRoomVariant[] treasure1x2WeightedVariants;
    public WeightedRoomVariant[] treasure2x2WeightedVariants;

    [Header("Shop")]
    public WeightedRoomVariant[] shop1x1WeightedVariants;
    public WeightedRoomVariant[] shop2x1WeightedVariants;
    public WeightedRoomVariant[] shop1x2WeightedVariants;
    public WeightedRoomVariant[] shop2x2WeightedVariants;

    [Header("Curch")]
    public WeightedRoomVariant[] curch1x1WeightedVariants;
    public WeightedRoomVariant[] curch2x1WeightedVariants;
    public WeightedRoomVariant[] curch1x2WeightedVariants;
    public WeightedRoomVariant[] curch2x2WeightedVariants;

    [Header("Evil Curch")]
    public WeightedRoomVariant[] evilCurch1x1WeightedVariants;
    public WeightedRoomVariant[] evilCurch2x1WeightedVariants;
    public WeightedRoomVariant[] evilCurch1x2WeightedVariants;
    public WeightedRoomVariant[] evilCurch2x2WeightedVariants;

    [Header("Secret Access / Secret")]
    public WeightedRoomVariant[] secretAccessSecret1x1WeightedVariants;
    public WeightedRoomVariant[] secretAccessSecret2x1WeightedVariants;
    public WeightedRoomVariant[] secretAccessSecret1x2WeightedVariants;
    public WeightedRoomVariant[] secretAccessSecret2x2WeightedVariants;

    [Header("Secret Access / Super Secret")]
    public WeightedRoomVariant[] secretAccessSuperSecret1x1WeightedVariants;
    public WeightedRoomVariant[] secretAccessSuperSecret2x1WeightedVariants;
    public WeightedRoomVariant[] secretAccessSuperSecret1x2WeightedVariants;
    public WeightedRoomVariant[] secretAccessSuperSecret2x2WeightedVariants;

    [Header("Challenge (wave, no-heal, future variants)")]
    public WeightedRoomVariant[] challenge1x1WeightedVariants;
    public WeightedRoomVariant[] challenge2x1WeightedVariants;
    public WeightedRoomVariant[] challenge1x2WeightedVariants;
    public WeightedRoomVariant[] challenge2x2WeightedVariants;

    [Header("Miniboss")]
    public WeightedRoomVariant[] miniboss1x1WeightedVariants;
    public WeightedRoomVariant[] miniboss2x1WeightedVariants;
    public WeightedRoomVariant[] miniboss1x2WeightedVariants;
    public WeightedRoomVariant[] miniboss2x2WeightedVariants;

    [Header("NPC Encounter")]
    public WeightedRoomVariant[] npcEncounter1x1WeightedVariants;
    public WeightedRoomVariant[] npcEncounter2x1WeightedVariants;
    public WeightedRoomVariant[] npcEncounter1x2WeightedVariants;
    public WeightedRoomVariant[] npcEncounter2x2WeightedVariants;

    // Original arrays stay serialized as a safe migration bridge. Existing assets
    // are converted to equal-weight entries by OnValidate; accessors also read
    // them, so a player build cannot lose old references before reserialization.
    [HideInInspector] public Room[] normal1x1Variants;
    [HideInInspector] public Room[] normal2x1Variants;
    [HideInInspector] public Room[] normal1x2Variants;
    [HideInInspector] public Room[] normal2x2Variants;
    [HideInInspector] public Room[] boss1x1Variants;
    [HideInInspector] public Room[] boss2x1Variants;
    [HideInInspector] public Room[] boss1x2Variants;
    [HideInInspector] public Room[] boss2x2Variants;
    [HideInInspector] public Room[] treasure1x1Variants;
    [HideInInspector] public Room[] treasure2x1Variants;
    [HideInInspector] public Room[] treasure1x2Variants;
    [HideInInspector] public Room[] treasure2x2Variants;
    [HideInInspector] public Room[] shop1x1Variants;
    [HideInInspector] public Room[] shop2x1Variants;
    [HideInInspector] public Room[] shop1x2Variants;
    [HideInInspector] public Room[] shop2x2Variants;
    [HideInInspector] public Room[] curch1x1Variants;
    [HideInInspector] public Room[] curch2x1Variants;
    [HideInInspector] public Room[] curch1x2Variants;
    [HideInInspector] public Room[] curch2x2Variants;
    [HideInInspector] public Room[] evilCurch1x1Variants;
    [HideInInspector] public Room[] evilCurch2x1Variants;
    [HideInInspector] public Room[] evilCurch1x2Variants;
    [HideInInspector] public Room[] evilCurch2x2Variants;
    [HideInInspector] public Room[] secretAccessSecret1x1Variants;
    [HideInInspector] public Room[] secretAccessSecret2x1Variants;
    [HideInInspector] public Room[] secretAccessSecret1x2Variants;
    [HideInInspector] public Room[] secretAccessSecret2x2Variants;
    [HideInInspector] public Room[] secretAccessSuperSecret1x1Variants;
    [HideInInspector] public Room[] secretAccessSuperSecret2x1Variants;
    [HideInInspector] public Room[] secretAccessSuperSecret1x2Variants;
    [HideInInspector] public Room[] secretAccessSuperSecret2x2Variants;
    [HideInInspector] public Room[] challenge1x1Variants;
    [HideInInspector] public Room[] challenge2x1Variants;
    [HideInInspector] public Room[] challenge1x2Variants;
    [HideInInspector] public Room[] challenge2x2Variants;
    [HideInInspector] public Room[] miniboss1x1Variants;
    [HideInInspector] public Room[] miniboss2x1Variants;
    [HideInInspector] public Room[] miniboss1x2Variants;
    [HideInInspector] public Room[] miniboss2x2Variants;
    [HideInInspector] public Room[] npcEncounter1x1Variants;
    [HideInInspector] public Room[] npcEncounter2x1Variants;
    [HideInInspector] public Room[] npcEncounter1x2Variants;
    [HideInInspector] public Room[] npcEncounter2x2Variants;

    // Parkour and Wave remain import bridges for the existing semantic migrations.
    [SerializeField, HideInInspector, FormerlySerializedAs("wave1x1Variants")] private Room[] legacyWave1x1Variants;
    [SerializeField, HideInInspector, FormerlySerializedAs("wave2x1Variants")] private Room[] legacyWave2x1Variants;
    [SerializeField, HideInInspector, FormerlySerializedAs("wave1x2Variants")] private Room[] legacyWave1x2Variants;
    [SerializeField, HideInInspector, FormerlySerializedAs("wave2x2Variants")] private Room[] legacyWave2x2Variants;
    [SerializeField, HideInInspector, FormerlySerializedAs("parkour1x1Variants")] private Room[] legacyParkour1x1Variants;
    [SerializeField, HideInInspector, FormerlySerializedAs("parkour2x1Variants")] private Room[] legacyParkour2x1Variants;
    [SerializeField, HideInInspector, FormerlySerializedAs("parkour1x2Variants")] private Room[] legacyParkour1x2Variants;
    [SerializeField, HideInInspector, FormerlySerializedAs("parkour2x2Variants")] private Room[] legacyParkour2x2Variants;

    public WeightedRoomVariant[] GetNormal1x1Variants() => MergeVariants(normal1x1WeightedVariants, normal1x1Variants, legacyParkour1x1Variants);
    public WeightedRoomVariant[] GetNormal2x1Variants() => MergeVariants(normal2x1WeightedVariants, normal2x1Variants, legacyParkour2x1Variants);
    public WeightedRoomVariant[] GetNormal1x2Variants() => MergeVariants(normal1x2WeightedVariants, normal1x2Variants, legacyParkour1x2Variants);
    public WeightedRoomVariant[] GetNormal2x2Variants() => MergeVariants(normal2x2WeightedVariants, normal2x2Variants, legacyParkour2x2Variants);
    public WeightedRoomVariant[] GetBoss1x1Variants() => MergeVariants(boss1x1WeightedVariants, boss1x1Variants);
    public WeightedRoomVariant[] GetBoss2x1Variants() => MergeVariants(boss2x1WeightedVariants, boss2x1Variants);
    public WeightedRoomVariant[] GetBoss1x2Variants() => MergeVariants(boss1x2WeightedVariants, boss1x2Variants);
    public WeightedRoomVariant[] GetBoss2x2Variants() => MergeVariants(boss2x2WeightedVariants, boss2x2Variants);
    public WeightedRoomVariant[] GetTreasure1x1Variants() => MergeVariants(treasure1x1WeightedVariants, treasure1x1Variants);
    public WeightedRoomVariant[] GetTreasure2x1Variants() => MergeVariants(treasure2x1WeightedVariants, treasure2x1Variants);
    public WeightedRoomVariant[] GetTreasure1x2Variants() => MergeVariants(treasure1x2WeightedVariants, treasure1x2Variants);
    public WeightedRoomVariant[] GetTreasure2x2Variants() => MergeVariants(treasure2x2WeightedVariants, treasure2x2Variants);
    public WeightedRoomVariant[] GetShop1x1Variants() => MergeVariants(shop1x1WeightedVariants, shop1x1Variants);
    public WeightedRoomVariant[] GetShop2x1Variants() => MergeVariants(shop2x1WeightedVariants, shop2x1Variants);
    public WeightedRoomVariant[] GetShop1x2Variants() => MergeVariants(shop1x2WeightedVariants, shop1x2Variants);
    public WeightedRoomVariant[] GetShop2x2Variants() => MergeVariants(shop2x2WeightedVariants, shop2x2Variants);
    public WeightedRoomVariant[] GetCurch1x1Variants() => MergeVariants(curch1x1WeightedVariants, curch1x1Variants);
    public WeightedRoomVariant[] GetCurch2x1Variants() => MergeVariants(curch2x1WeightedVariants, curch2x1Variants);
    public WeightedRoomVariant[] GetCurch1x2Variants() => MergeVariants(curch1x2WeightedVariants, curch1x2Variants);
    public WeightedRoomVariant[] GetCurch2x2Variants() => MergeVariants(curch2x2WeightedVariants, curch2x2Variants);
    public WeightedRoomVariant[] GetEvilCurch1x1Variants() => MergeVariants(evilCurch1x1WeightedVariants, evilCurch1x1Variants);
    public WeightedRoomVariant[] GetEvilCurch2x1Variants() => MergeVariants(evilCurch2x1WeightedVariants, evilCurch2x1Variants);
    public WeightedRoomVariant[] GetEvilCurch1x2Variants() => MergeVariants(evilCurch1x2WeightedVariants, evilCurch1x2Variants);
    public WeightedRoomVariant[] GetEvilCurch2x2Variants() => MergeVariants(evilCurch2x2WeightedVariants, evilCurch2x2Variants);
    public WeightedRoomVariant[] GetSecretAccessSecret1x1Variants() => MergeVariants(secretAccessSecret1x1WeightedVariants, secretAccessSecret1x1Variants);
    public WeightedRoomVariant[] GetSecretAccessSecret2x1Variants() => MergeVariants(secretAccessSecret2x1WeightedVariants, secretAccessSecret2x1Variants);
    public WeightedRoomVariant[] GetSecretAccessSecret1x2Variants() => MergeVariants(secretAccessSecret1x2WeightedVariants, secretAccessSecret1x2Variants);
    public WeightedRoomVariant[] GetSecretAccessSecret2x2Variants() => MergeVariants(secretAccessSecret2x2WeightedVariants, secretAccessSecret2x2Variants);
    public WeightedRoomVariant[] GetSecretAccessSuperSecret1x1Variants() => MergeVariants(secretAccessSuperSecret1x1WeightedVariants, secretAccessSuperSecret1x1Variants);
    public WeightedRoomVariant[] GetSecretAccessSuperSecret2x1Variants() => MergeVariants(secretAccessSuperSecret2x1WeightedVariants, secretAccessSuperSecret2x1Variants);
    public WeightedRoomVariant[] GetSecretAccessSuperSecret1x2Variants() => MergeVariants(secretAccessSuperSecret1x2WeightedVariants, secretAccessSuperSecret1x2Variants);
    public WeightedRoomVariant[] GetSecretAccessSuperSecret2x2Variants() => MergeVariants(secretAccessSuperSecret2x2WeightedVariants, secretAccessSuperSecret2x2Variants);
    public WeightedRoomVariant[] GetChallenge1x1Variants() => MergeVariants(challenge1x1WeightedVariants, challenge1x1Variants, legacyWave1x1Variants);
    public WeightedRoomVariant[] GetChallenge2x1Variants() => MergeVariants(challenge2x1WeightedVariants, challenge2x1Variants, legacyWave2x1Variants);
    public WeightedRoomVariant[] GetChallenge1x2Variants() => MergeVariants(challenge1x2WeightedVariants, challenge1x2Variants, legacyWave1x2Variants);
    public WeightedRoomVariant[] GetChallenge2x2Variants() => MergeVariants(challenge2x2WeightedVariants, challenge2x2Variants, legacyWave2x2Variants);
    public WeightedRoomVariant[] GetMiniboss1x1Variants() => MergeVariants(miniboss1x1WeightedVariants, miniboss1x1Variants);
    public WeightedRoomVariant[] GetMiniboss2x1Variants() => MergeVariants(miniboss2x1WeightedVariants, miniboss2x1Variants);
    public WeightedRoomVariant[] GetMiniboss1x2Variants() => MergeVariants(miniboss1x2WeightedVariants, miniboss1x2Variants);
    public WeightedRoomVariant[] GetMiniboss2x2Variants() => MergeVariants(miniboss2x2WeightedVariants, miniboss2x2Variants);
    public WeightedRoomVariant[] GetNpcEncounter1x1Variants() => MergeVariants(npcEncounter1x1WeightedVariants, npcEncounter1x1Variants);
    public WeightedRoomVariant[] GetNpcEncounter2x1Variants() => MergeVariants(npcEncounter2x1WeightedVariants, npcEncounter2x1Variants);
    public WeightedRoomVariant[] GetNpcEncounter1x2Variants() => MergeVariants(npcEncounter1x2WeightedVariants, npcEncounter1x2Variants);
    public WeightedRoomVariant[] GetNpcEncounter2x2Variants() => MergeVariants(npcEncounter2x2WeightedVariants, npcEncounter2x2Variants);

    private static WeightedRoomVariant[] MergeVariants(WeightedRoomVariant[] weighted, params Room[][] legacyGroups)
    {
        var merged = new List<WeightedRoomVariant>();
        if (weighted != null)
            merged.AddRange(weighted);

        if (legacyGroups != null)
        {
            foreach (Room[] group in legacyGroups)
            {
                if (group == null)
                    continue;
                foreach (Room room in group)
                    if (room != null)
                        merged.Add(new WeightedRoomVariant { room = room, spawnWeight = 1f });
            }
        }

        return merged.ToArray();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        bool migrated = false;
        migrated |= MigrateLegacy(ref normal1x1WeightedVariants, ref normal1x1Variants);
        migrated |= MigrateLegacy(ref normal2x1WeightedVariants, ref normal2x1Variants);
        migrated |= MigrateLegacy(ref normal1x2WeightedVariants, ref normal1x2Variants);
        migrated |= MigrateLegacy(ref normal2x2WeightedVariants, ref normal2x2Variants);
        migrated |= MigrateLegacy(ref boss1x1WeightedVariants, ref boss1x1Variants);
        migrated |= MigrateLegacy(ref boss2x1WeightedVariants, ref boss2x1Variants);
        migrated |= MigrateLegacy(ref boss1x2WeightedVariants, ref boss1x2Variants);
        migrated |= MigrateLegacy(ref boss2x2WeightedVariants, ref boss2x2Variants);
        migrated |= MigrateLegacy(ref treasure1x1WeightedVariants, ref treasure1x1Variants);
        migrated |= MigrateLegacy(ref treasure2x1WeightedVariants, ref treasure2x1Variants);
        migrated |= MigrateLegacy(ref treasure1x2WeightedVariants, ref treasure1x2Variants);
        migrated |= MigrateLegacy(ref treasure2x2WeightedVariants, ref treasure2x2Variants);
        migrated |= MigrateLegacy(ref shop1x1WeightedVariants, ref shop1x1Variants);
        migrated |= MigrateLegacy(ref shop2x1WeightedVariants, ref shop2x1Variants);
        migrated |= MigrateLegacy(ref shop1x2WeightedVariants, ref shop1x2Variants);
        migrated |= MigrateLegacy(ref shop2x2WeightedVariants, ref shop2x2Variants);
        migrated |= MigrateLegacy(ref curch1x1WeightedVariants, ref curch1x1Variants);
        migrated |= MigrateLegacy(ref curch2x1WeightedVariants, ref curch2x1Variants);
        migrated |= MigrateLegacy(ref curch1x2WeightedVariants, ref curch1x2Variants);
        migrated |= MigrateLegacy(ref curch2x2WeightedVariants, ref curch2x2Variants);
        migrated |= MigrateLegacy(ref evilCurch1x1WeightedVariants, ref evilCurch1x1Variants);
        migrated |= MigrateLegacy(ref evilCurch2x1WeightedVariants, ref evilCurch2x1Variants);
        migrated |= MigrateLegacy(ref evilCurch1x2WeightedVariants, ref evilCurch1x2Variants);
        migrated |= MigrateLegacy(ref evilCurch2x2WeightedVariants, ref evilCurch2x2Variants);
        migrated |= MigrateLegacy(ref secretAccessSecret1x1WeightedVariants, ref secretAccessSecret1x1Variants);
        migrated |= MigrateLegacy(ref secretAccessSecret2x1WeightedVariants, ref secretAccessSecret2x1Variants);
        migrated |= MigrateLegacy(ref secretAccessSecret1x2WeightedVariants, ref secretAccessSecret1x2Variants);
        migrated |= MigrateLegacy(ref secretAccessSecret2x2WeightedVariants, ref secretAccessSecret2x2Variants);
        migrated |= MigrateLegacy(ref secretAccessSuperSecret1x1WeightedVariants, ref secretAccessSuperSecret1x1Variants);
        migrated |= MigrateLegacy(ref secretAccessSuperSecret2x1WeightedVariants, ref secretAccessSuperSecret2x1Variants);
        migrated |= MigrateLegacy(ref secretAccessSuperSecret1x2WeightedVariants, ref secretAccessSuperSecret1x2Variants);
        migrated |= MigrateLegacy(ref secretAccessSuperSecret2x2WeightedVariants, ref secretAccessSuperSecret2x2Variants);
        migrated |= MigrateLegacy(ref challenge1x1WeightedVariants, ref challenge1x1Variants);
        migrated |= MigrateLegacy(ref challenge2x1WeightedVariants, ref challenge2x1Variants);
        migrated |= MigrateLegacy(ref challenge1x2WeightedVariants, ref challenge1x2Variants);
        migrated |= MigrateLegacy(ref challenge2x2WeightedVariants, ref challenge2x2Variants);
        migrated |= MigrateLegacy(ref miniboss1x1WeightedVariants, ref miniboss1x1Variants);
        migrated |= MigrateLegacy(ref miniboss2x1WeightedVariants, ref miniboss2x1Variants);
        migrated |= MigrateLegacy(ref miniboss1x2WeightedVariants, ref miniboss1x2Variants);
        migrated |= MigrateLegacy(ref miniboss2x2WeightedVariants, ref miniboss2x2Variants);
        migrated |= MigrateLegacy(ref npcEncounter1x1WeightedVariants, ref npcEncounter1x1Variants);
        migrated |= MigrateLegacy(ref npcEncounter2x1WeightedVariants, ref npcEncounter2x1Variants);
        migrated |= MigrateLegacy(ref npcEncounter1x2WeightedVariants, ref npcEncounter1x2Variants);
        migrated |= MigrateLegacy(ref npcEncounter2x2WeightedVariants, ref npcEncounter2x2Variants);

        migrated |= MigrateLegacy(ref normal1x1WeightedVariants, ref legacyParkour1x1Variants);
        migrated |= MigrateLegacy(ref normal2x1WeightedVariants, ref legacyParkour2x1Variants);
        migrated |= MigrateLegacy(ref normal1x2WeightedVariants, ref legacyParkour1x2Variants);
        migrated |= MigrateLegacy(ref normal2x2WeightedVariants, ref legacyParkour2x2Variants);
        migrated |= MigrateLegacy(ref challenge1x1WeightedVariants, ref legacyWave1x1Variants);
        migrated |= MigrateLegacy(ref challenge2x1WeightedVariants, ref legacyWave2x1Variants);
        migrated |= MigrateLegacy(ref challenge1x2WeightedVariants, ref legacyWave1x2Variants);
        migrated |= MigrateLegacy(ref challenge2x2WeightedVariants, ref legacyWave2x2Variants);
        if (migrated)
            UnityEditor.EditorUtility.SetDirty(this);
    }

    private static bool MigrateLegacy(ref WeightedRoomVariant[] destination, ref Room[] legacy)
    {
        if (legacy == null || legacy.Length == 0)
            return false;

        var migrated = new List<WeightedRoomVariant>();
        if (destination != null)
            migrated.AddRange(destination);
        foreach (Room room in legacy)
            if (room != null)
                migrated.Add(new WeightedRoomVariant { room = room, spawnWeight = 1f });

        destination = migrated.ToArray();
        legacy = new Room[0];
        return true;
    }
#endif
}
