using UnityEngine;
using System.IO;

public static class SaveSystem
{
    private const string LegacySaveFileName = "gamedata.json";
    private const string CharacterSavePrefix = "gamedata_";
    private const string CharacterSaveExtension = ".json";
    private const string PlayerPrefsSelectedCharacterKey = "SelectedCharacterId";

    public static void SaveData(GameData data)
    {
        string path = GetSaveFilePath(data != null ? data.selectedCharacterId : string.Empty);
        string json = JsonUtility.ToJson(data, true); // 'true' per formattare il JSON in modo leggibile

        try
        {
            File.WriteAllText(path, json);
            if (data != null && !string.IsNullOrWhiteSpace(data.selectedCharacterId))
                StoreSelectedCharacterId(data.selectedCharacterId);
            Debug.Log($"Dati salvati con successo in: {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Errore durante il salvataggio dei dati: {e.Message}");
        }
    }

    public static GameData LoadData()
    {
        string selectedCharacterId = PlayerPrefs.GetString(PlayerPrefsSelectedCharacterKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(selectedCharacterId))
        {
            GameData selectedData = LoadData(selectedCharacterId);
            if (selectedData != null)
                return selectedData;
        }

        GameData legacyData = LoadLegacyData();
        if (legacyData != null && !string.IsNullOrWhiteSpace(legacyData.selectedCharacterId))
        {
            GameData selectedData = LoadData(legacyData.selectedCharacterId, allowLegacyFallback: false);
            if (selectedData != null)
            {
                StoreSelectedCharacterId(selectedData.selectedCharacterId);
                return selectedData;
            }

            StoreSelectedCharacterId(legacyData.selectedCharacterId);
        }

        return legacyData;
    }

    public static GameData LoadData(string characterId, bool allowLegacyFallback = true)
    {
        string normalizedId = NormalizeCharacterId(characterId);
        if (!string.IsNullOrWhiteSpace(normalizedId))
        {
            GameData characterData = LoadDataFromPath(GetCharacterSaveFilePath(normalizedId), logMissing: false);
            if (characterData != null)
            {
                if (string.IsNullOrWhiteSpace(characterData.selectedCharacterId))
                    characterData.selectedCharacterId = normalizedId;
                StoreSelectedCharacterId(characterData.selectedCharacterId);
                return characterData;
            }
        }

        if (!allowLegacyFallback)
            return null;

        GameData legacyData = LoadLegacyData();
        if (legacyData == null)
            return null;

        if (string.IsNullOrWhiteSpace(normalizedId)
            || string.IsNullOrWhiteSpace(legacyData.selectedCharacterId)
            || string.Equals(legacyData.selectedCharacterId.Trim(), normalizedId, System.StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(legacyData.selectedCharacterId))
                legacyData.selectedCharacterId = normalizedId;
            if (!string.IsNullOrWhiteSpace(legacyData.selectedCharacterId))
                StoreSelectedCharacterId(legacyData.selectedCharacterId);
            return legacyData;
        }

        return null;
    }

    public static bool HasData(string characterId)
    {
        string normalizedId = NormalizeCharacterId(characterId);
        if (string.IsNullOrWhiteSpace(normalizedId))
            return false;

        if (File.Exists(GetCharacterSaveFilePath(normalizedId)))
            return true;

        GameData legacyData = LoadLegacyData(logMissing: false);
        return legacyData != null
            && !string.IsNullOrWhiteSpace(legacyData.selectedCharacterId)
            && string.Equals(legacyData.selectedCharacterId.Trim(), normalizedId, System.StringComparison.OrdinalIgnoreCase);
    }

    public static string GetSaveFilePath()
    {
        string selectedCharacterId = PlayerPrefs.GetString(PlayerPrefsSelectedCharacterKey, string.Empty);
        return GetSaveFilePath(selectedCharacterId);
    }

    public static string GetSaveFilePath(string characterId)
    {
        string normalizedId = NormalizeCharacterId(characterId);
        return string.IsNullOrWhiteSpace(normalizedId)
            ? GetLegacySaveFilePath()
            : GetCharacterSaveFilePath(normalizedId);
    }

    private static GameData LoadLegacyData(bool logMissing = true)
    {
        return LoadDataFromPath(GetLegacySaveFilePath(), logMissing);
    }

    private static GameData LoadDataFromPath(string path, bool logMissing = true)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (!File.Exists(path))
        {
            if (logMissing)
                Debug.Log("Nessun file di salvataggio trovato. Verranno usati i valori di default.");
            return null; // Ritorna null se non c'e' un file
        }

        try
        {
            string json = File.ReadAllText(path);
            GameData data = JsonUtility.FromJson<GameData>(json);
            Debug.Log($"Dati caricati con successo da: {path}");
            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Errore durante il caricamento dei dati: {e.Message}");
            return null; // In caso di errore, ritorna null per evitare crash
        }
    }

    private static string GetLegacySaveFilePath()
    {
        return Path.Combine(Application.persistentDataPath, LegacySaveFileName);
    }

    private static string GetCharacterSaveFilePath(string characterId)
    {
        return Path.Combine(Application.persistentDataPath, CharacterSavePrefix + SanitizeFileName(characterId) + CharacterSaveExtension);
    }

    private static string NormalizeCharacterId(string characterId)
    {
        return string.IsNullOrWhiteSpace(characterId) ? string.Empty : characterId.Trim();
    }

    private static void StoreSelectedCharacterId(string characterId)
    {
        string normalizedId = NormalizeCharacterId(characterId);
        if (string.IsNullOrWhiteSpace(normalizedId))
            return;

        PlayerPrefs.SetString(PlayerPrefsSelectedCharacterKey, normalizedId);
        PlayerPrefs.Save();
    }

    private static string SanitizeFileName(string value)
    {
        string normalized = NormalizeCharacterId(value);
        if (string.IsNullOrWhiteSpace(normalized))
            return "unknown";

        char[] invalid = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalid.Length; i++)
            normalized = normalized.Replace(invalid[i], '_');

        return normalized;
    }
}
