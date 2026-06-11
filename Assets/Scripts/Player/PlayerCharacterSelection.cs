using UnityEngine;

public static class PlayerCharacterSelection
{
    public const string DefaultCharacterId = "warrior";
    private const string PlayerPrefsSelectedCharacterKey = "SelectedCharacterId";
    private static string pendingNewCharacterId;

    public static string PendingNewCharacterId => pendingNewCharacterId;

    public static string GetSelectedCharacterId()
    {
        GameData data = SaveSystem.LoadData();
        if (data != null && !string.IsNullOrWhiteSpace(data.selectedCharacterId))
            return data.selectedCharacterId;

        string storedId = PlayerPrefs.GetString(PlayerPrefsSelectedCharacterKey, string.Empty);
        return string.IsNullOrWhiteSpace(storedId) ? DefaultCharacterId : storedId;
    }

    public static void StartNewCharacter(PlayerCharacterData character)
    {
        if (character == null)
        {
            Debug.LogWarning("[PlayerCharacterSelection] Character missing.");
            return;
        }

        StartNewCharacter(character.GetCharacterId());
    }

    public static void StartNewCharacter(string characterId)
    {
        string resolvedId = string.IsNullOrWhiteSpace(characterId) ? DefaultCharacterId : characterId.Trim();
        pendingNewCharacterId = resolvedId;
        PlayerPrefs.SetString(PlayerPrefsSelectedCharacterKey, resolvedId);
        PlayerPrefs.Save();

        SaveSystem.SaveData(new GameData
        {
            selectedCharacterId = resolvedId,
            selectedCharacterStartApplied = false
        });
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
