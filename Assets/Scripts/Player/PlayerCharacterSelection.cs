using UnityEngine;

public static class PlayerCharacterSelection
{
    public const string DefaultCharacterId = "warrior";
    private const string PlayerPrefsSelectedCharacterKey = "SelectedCharacterId";
    private static string pendingNewCharacterId;

    public static string PendingNewCharacterId => pendingNewCharacterId;

    public static string GetSelectedCharacterId()
    {
        string storedId = PlayerPrefs.GetString(PlayerPrefsSelectedCharacterKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(storedId))
            return storedId;

        GameData data = SaveSystem.LoadData();
        if (data != null && !string.IsNullOrWhiteSpace(data.selectedCharacterId))
            return data.selectedCharacterId;

        return DefaultCharacterId;
    }

    public static void StartNewCharacter(PlayerCharacterData character)
    {
        if (character == null)
        {
            Debug.LogWarning("[PlayerCharacterSelection] Character missing.");
            return;
        }

        StartNewCharacter(character.GetCharacterId(), character.displayName);
    }

    public static void StartNewCharacter(string characterId)
    {
        StartNewCharacter(characterId, null);
    }

    private static void StartNewCharacter(string characterId, string characterName)
    {
        string resolvedId = string.IsNullOrWhiteSpace(characterId) ? DefaultCharacterId : characterId.Trim();
        string resolvedName = string.IsNullOrWhiteSpace(characterName) ? resolvedId : characterName.Trim();

        if (PlayerStats.instance != null)
            PlayerStats.instance.SaveStatsImmediate();

        pendingNewCharacterId = resolvedId;
        PlayerPrefs.SetString(PlayerPrefsSelectedCharacterKey, resolvedId);
        PlayerPrefs.Save();

        if (!SaveSystem.HasData(resolvedId))
        {
            SaveSystem.SaveData(new GameData
            {
                selectedCharacterId = resolvedId,
                characterName = resolvedName,
                selectedCharacterStartApplied = false
            });
        }
    }

    internal static void ClearPendingNewCharacter(string characterId)
    {
        if (string.IsNullOrWhiteSpace(pendingNewCharacterId))
            return;

        if (string.IsNullOrWhiteSpace(characterId)
            || string.Equals(pendingNewCharacterId, characterId.Trim(), System.StringComparison.OrdinalIgnoreCase))
        {
            pendingNewCharacterId = string.Empty;
        }
    }
}
