/// <summary>One authoritative structural category for generated authored rooms.</summary>
public enum RoomType
{
    Start = 0,
    Normal = 1,
    Shop = 2,
    Treasure = 3,
    Challenge = 4,
    // Values 5 and 7 are intentionally reserved for migration of the former
    // Challenge and Parkour enum entries. Do not reuse them.
    Miniboss = 6,
    NpcEncounter = 9,
    SecretAccess = 10,
    Curch = 11,
    EvilCurch = 12,
    Boss = 13
}

/// <summary>Maps serialized values from the pre-unification room model.</summary>
public static class RoomTypeMigration
{
    private const int LegacyChallengeValue = 5;
    private const int LegacyParkourValue = 7;

    public static RoomType Normalize(RoomType roomType)
    {
        switch ((int)roomType)
        {
            case LegacyChallengeValue:
                return RoomType.Challenge;
            case LegacyParkourValue:
                return RoomType.Normal;
            default:
                return roomType;
        }
    }
}
