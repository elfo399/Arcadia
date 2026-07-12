using UnityEngine;

public static class PlayerCharacterSelection
{
    public const string DefaultCharacterId = SaveSystem.SingleCharacterId;
    private static bool pendingNewCharacter;

    public static string PendingNewCharacterId => pendingNewCharacter ? SaveSystem.SingleCharacterId : string.Empty;

    public static string GetSelectedCharacterId()
    {
        return SaveSystem.SingleCharacterId;
    }

    public static void StartNewCharacter(PlayerCharacterData character)
    {
        StartNewCharacter();
    }

    public static void StartNewCharacter(string characterId)
    {
        StartNewCharacter();
    }

    public static void StartNewCharacter()
    {
        if (PlayerStats.instance != null && !string.IsNullOrWhiteSpace(PlayerStats.instance.SelectedCharacterId))
            PlayerStats.instance.SaveStatsImmediate();

        pendingNewCharacter = true;
        SaveSystem.SelectCharacter(SaveSystem.SingleCharacterId);
        SaveSystem.EnsureCharacterData(SaveSystem.SingleCharacterId, SaveSystem.DefaultCharacterName);
    }

    internal static void ClearPendingNewCharacter(string characterId)
    {
        pendingNewCharacter = false;
    }
}
