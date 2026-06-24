using UnityEngine;
using System.IO;

public static class SaveSystem
{
    public const int CurrentSaveVersion = 1;

    private const string LegacySaveFileName = "gamedata.json";
    private const string CharacterSavePrefix = "gamedata_";
    private const string CharacterSaveExtension = ".json";
    private const string PlayerPrefsSelectedCharacterKey = "SelectedCharacterId";

    public static string GetSelectedCharacterId()
    {
        return PlayerPrefs.GetString(PlayerPrefsSelectedCharacterKey, string.Empty);
    }

    public static void SelectCharacter(string characterId)
    {
        StoreSelectedCharacterId(characterId);
    }

    public static GameData EnsureCharacterData(string characterId, string characterName)
    {
        string normalizedId = NormalizeCharacterId(characterId);
        if (string.IsNullOrWhiteSpace(normalizedId))
            return null;

        string resolvedName = string.IsNullOrWhiteSpace(characterName) ? normalizedId : characterName.Trim();
        GameData existingData = LoadData(normalizedId, allowLegacyFallback: false, storeSelectedCharacter: false);
        if (existingData != null)
        {
            StoreSelectedCharacterId(normalizedId);
            return existingData;
        }

        GameData newData = new GameData
        {
            saveVersion = CurrentSaveVersion,
            selectedCharacterId = normalizedId,
            characterName = resolvedName,
            selectedCharacterStartApplied = false,
            usesUnifiedCoins = true
        };

        SaveData(newData);
        return newData;
    }

    public static void SaveData(GameData data)
    {
        if (data != null)
        {
            data.saveVersion = CurrentSaveVersion;
            data.usesUnifiedCoins = true;
        }

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
        string selectedCharacterId = GetSelectedCharacterId();
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
        return LoadData(characterId, allowLegacyFallback, storeSelectedCharacter: true);
    }

    private static GameData LoadData(string characterId, bool allowLegacyFallback, bool storeSelectedCharacter)
    {
        string normalizedId = NormalizeCharacterId(characterId);
        if (!string.IsNullOrWhiteSpace(normalizedId))
        {
            GameData characterData = LoadDataFromPath(GetCharacterSaveFilePath(normalizedId), logMissing: false);
            if (characterData != null)
            {
                if (string.IsNullOrWhiteSpace(characterData.selectedCharacterId)
                    || !string.Equals(characterData.selectedCharacterId.Trim(), normalizedId, System.StringComparison.OrdinalIgnoreCase))
                {
                    characterData.selectedCharacterId = normalizedId;
                }

                if (storeSelectedCharacter)
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
            if (storeSelectedCharacter && !string.IsNullOrWhiteSpace(legacyData.selectedCharacterId))
                StoreSelectedCharacterId(legacyData.selectedCharacterId);
            return legacyData;
        }

        return null;
    }

    public static string GetSaveFilePath()
    {
        string selectedCharacterId = GetSelectedCharacterId();
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
            MigrateLegacyCurrencyFields(data, json);
            MigrateSaveVersion(data);
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

    private static void MigrateLegacyCurrencyFields(GameData data, string json)
    {
        if (data == null || string.IsNullOrWhiteSpace(json) || data.usesUnifiedCoins)
            return;

        if (data.bankCoins > 0)
        {
            data.usesUnifiedCoins = true;
            return;
        }

        int legacyGold = ReadJsonInt(json, "bankGold");
        int legacySilver = ReadJsonInt(json, "bankSilver");
        int legacyCopper = ReadJsonInt(json, "bankCopper");

        if (legacyGold <= 0 && legacySilver <= 0 && legacyCopper <= 0)
            return;

        long migratedCoins = 0;
        migratedCoins += (long)Mathf.Max(0, legacyGold) * PlayerStats.GoldCoinValue;
        migratedCoins += (long)Mathf.Max(0, legacySilver) * PlayerStats.SilverCoinValue;
        migratedCoins += (long)Mathf.Max(0, legacyCopper) * PlayerStats.BronzeCoinValue;

        data.bankCoins = migratedCoins > int.MaxValue ? int.MaxValue : (int)migratedCoins;
        data.usesUnifiedCoins = true;
    }

    private static void MigrateSaveVersion(GameData data)
    {
        if (data == null)
            return;

        if (data.saveVersion > CurrentSaveVersion)
        {
            Debug.LogWarning(
                $"Il salvataggio usa una versione futura ({data.saveVersion}); "
                + $"la versione supportata e' {CurrentSaveVersion}.");
            return;
        }

        while (data.saveVersion < CurrentSaveVersion)
        {
            switch (data.saveVersion)
            {
                case 0:
                    // I salvataggi precedenti non contenevano checkpoint di run.
                    data.dungeonCheckpointActive = false;
                    data.dungeonFloor = 1;
                    data.dungeonSeed = string.Empty;
                    data.saveVersion = 1;
                    break;

                default:
                    Debug.LogWarning($"Migrazione non disponibile dalla versione {data.saveVersion}.");
                    return;
            }
        }
    }

    private static int ReadJsonInt(string json, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(fieldName))
            return 0;

        var match = System.Text.RegularExpressions.Regex.Match(
            json,
            "\"" + System.Text.RegularExpressions.Regex.Escape(fieldName) + "\"\\s*:\\s*(-?\\d+)");

        if (!match.Success)
            return 0;

        return int.TryParse(match.Groups[1].Value, out int value) ? value : 0;
    }
}
