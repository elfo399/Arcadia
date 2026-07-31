using UnityEngine;
using System.IO;

public static class SaveSystem
{
    public const int CurrentSaveVersion = 1;
    public const string SingleCharacterId = "player";
    public const string DefaultCharacterName = "Player";

    private const string SaveFileName = "gamedata.json";
    private const string LegacyCharacterSavePrefix = "gamedata_";
    private const string LegacyCharacterSaveExtension = ".json";
    private const string PlayerPrefsSelectedCharacterKey = "SelectedCharacterId";

    public static string GetSelectedCharacterId()
    {
        StoreSingleCharacterId();
        return SingleCharacterId;
    }

    public static void SelectCharacter(string characterId)
    {
        StoreSingleCharacterId();
    }

    public static GameData EnsureCharacterData(string characterId, string characterName)
    {
        GameData existingData = LoadData(characterId, allowLegacyFallback: true);
        if (existingData != null)
            return existingData;

        GameData newData = new GameData
        {
            saveVersion = CurrentSaveVersion,
            selectedCharacterId = SingleCharacterId,
            characterName = ResolveCharacterName(characterName),
            selectedCharacterStartApplied = false,
            usesUnifiedCoins = true
        };

        SaveData(newData);
        return newData;
    }

    public static void SaveData(GameData data)
    {
        if (data != null)
            PrepareSingleCharacterData(data);

        string path = GetSaveFilePath();
        string json = JsonUtility.ToJson(data, true);

        try
        {
            File.WriteAllText(path, json);
            StoreSingleCharacterId();
            Debug.Log($"Dati salvati con successo in: {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Errore durante il salvataggio dei dati: {e.Message}");
        }
    }

    public static GameData LoadData()
    {
        return LoadData(null, allowLegacyFallback: true);
    }

    public static GameData LoadData(string characterId, bool allowLegacyFallback = true)
    {
        GameData data = LoadDataFromPath(GetSaveFilePath(), logMissing: false);
        if (data != null)
        {
            PrepareSingleCharacterData(data);
            StoreSingleCharacterId();
            return data;
        }

        if (!allowLegacyFallback)
            return null;

        data = LoadLegacyCharacterData(characterId);
        if (data != null)
        {
            PrepareSingleCharacterData(data);
            SaveData(data);
            Debug.Log("[SaveSystem] Vecchio salvataggio per personaggio migrato nel salvataggio unico.");
            return data;
        }

        Debug.Log("Nessun file di salvataggio trovato. Verranno usati i valori di default.");
        return null;
    }

    public static string GetSaveFilePath()
    {
        return Path.Combine(Application.persistentDataPath, SaveFileName);
    }

    public static string GetSaveFilePath(string characterId)
    {
        return GetSaveFilePath();
    }

    private static GameData LoadLegacyCharacterData(string requestedCharacterId)
    {
        string requestedId = NormalizeCharacterId(requestedCharacterId);
        if (!string.IsNullOrWhiteSpace(requestedId))
        {
            GameData requestedData = LoadDataFromPath(GetLegacyCharacterSaveFilePath(requestedId), logMissing: false);
            if (requestedData != null)
                return requestedData;
        }

        string storedId = PlayerPrefs.GetString(PlayerPrefsSelectedCharacterKey, string.Empty);
        storedId = NormalizeCharacterId(storedId);
        if (!string.IsNullOrWhiteSpace(storedId)
            && !string.Equals(storedId, requestedId, System.StringComparison.OrdinalIgnoreCase))
        {
            GameData storedData = LoadDataFromPath(GetLegacyCharacterSaveFilePath(storedId), logMissing: false);
            if (storedData != null)
                return storedData;
        }

        try
        {
            var directory = new DirectoryInfo(Application.persistentDataPath);
            if (!directory.Exists)
                return null;

            FileInfo newest = null;
            FileInfo[] files = directory.GetFiles(LegacyCharacterSavePrefix + "*" + LegacyCharacterSaveExtension);
            for (int i = 0; i < files.Length; i++)
            {
                FileInfo file = files[i];
                if (file == null || string.Equals(file.Name, SaveFileName, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                if (newest == null || file.LastWriteTimeUtc > newest.LastWriteTimeUtc)
                    newest = file;
            }

            return newest != null ? LoadDataFromPath(newest.FullName, logMissing: false) : null;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SaveSystem] Impossibile cercare vecchi salvataggi per personaggio: {e.Message}");
            return null;
        }
    }

    private static GameData LoadDataFromPath(string path, bool logMissing = true)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (!File.Exists(path))
        {
            if (logMissing)
                Debug.Log("Nessun file di salvataggio trovato. Verranno usati i valori di default.");
            return null;
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
            return null;
        }
    }

    private static string GetLegacyCharacterSaveFilePath(string characterId)
    {
        return Path.Combine(
            Application.persistentDataPath,
            LegacyCharacterSavePrefix + SanitizeFileName(characterId) + LegacyCharacterSaveExtension);
    }

    private static string NormalizeCharacterId(string characterId)
    {
        return string.IsNullOrWhiteSpace(characterId) ? string.Empty : characterId.Trim();
    }

    private static void PrepareSingleCharacterData(GameData data)
    {
        if (data == null)
            return;

        data.saveVersion = CurrentSaveVersion;
        data.selectedCharacterId = SingleCharacterId;
        data.characterName = ResolveCharacterName(data.characterName);
        data.usesUnifiedCoins = true;
    }

    private static string ResolveCharacterName(string characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName))
            return DefaultCharacterName;

        string normalized = characterName.Trim();
        return IsLegacyArchetypeName(normalized) ? DefaultCharacterName : normalized;
    }

    private static bool IsLegacyArchetypeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string normalized = value.Trim();
        return string.Equals(normalized, "warrior", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "guerriero", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "mage", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "maga", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "assassin", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "robert", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "archer", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "arciere", System.StringComparison.OrdinalIgnoreCase);
    }

    private static void StoreSingleCharacterId()
    {
        PlayerPrefs.SetString(PlayerPrefsSelectedCharacterKey, SingleCharacterId);
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
