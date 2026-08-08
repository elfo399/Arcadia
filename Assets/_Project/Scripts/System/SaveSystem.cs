using UnityEngine;
using System.IO;

public static class SaveSystem
{
    public const int CurrentSaveVersion = 3;
    public const string SinglePlayerId = "player";
    public const string DefaultPlayerName = "Player";

    private const string SaveFileName = "gamedata.json";
    private const string LegacyCharacterSavePrefix = "gamedata_";
    private const string LegacyCharacterSaveExtension = ".json";
    private const string PlayerPrefsSelectedPlayerKey = "SelectedPlayerId";

    public static string GetSelectedPlayerId()
    {
        StoreSinglePlayerId();
        return SinglePlayerId;
    }

    public static void SelectPlayer(string playerId)
    {
        StoreSinglePlayerId();
    }

    public static GameData EnsurePlayerData(string playerId, string playerName, string selectedClassId = null)
    {
        GameData existingData = LoadData(playerId, allowLegacyFallback: true);
        if (existingData != null)
            return existingData;

        GameData newData = new GameData
        {
            saveVersion = CurrentSaveVersion,
            playerId = SinglePlayerId,
            playerName = ResolvePlayerName(playerName),
            selectedClassId = ResolveClassId(selectedClassId),
            startingClassApplied = false,
            usesUnifiedCoins = true
        };

        SaveData(newData);
        return newData;
    }

    public static void SaveData(GameData data)
    {
        if (data != null)
            PrepareSinglePlayerData(data);

        string path = GetSaveFilePath();
        string json = JsonUtility.ToJson(data, true);

        try
        {
            File.WriteAllText(path, json);
            StoreSinglePlayerId();
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

    public static GameData LoadData(string playerId, bool allowLegacyFallback = true)
    {
        GameData data = LoadDataFromPath(GetSaveFilePath(), logMissing: false);
        if (data != null)
        {
            PrepareSinglePlayerData(data);
            StoreSinglePlayerId();
            return data;
        }

        if (!allowLegacyFallback)
            return null;

        data = LoadLegacyPlayerData(playerId);
        if (data != null)
        {
            PrepareSinglePlayerData(data);
            SaveData(data);
            Debug.Log("[SaveSystem] Vecchio salvataggio migrato nel salvataggio unico del player.");
            return data;
        }

        Debug.Log("Nessun file di salvataggio trovato. Verranno usati i valori di default.");
        return null;
    }

    public static string GetSaveFilePath()
    {
        return Path.Combine(Application.persistentDataPath, SaveFileName);
    }

    public static string GetSaveFilePath(string playerId)
    {
        return GetSaveFilePath();
    }

    private static GameData LoadLegacyPlayerData(string requestedPlayerId)
    {
        string requestedId = NormalizePlayerId(requestedPlayerId);
        if (!string.IsNullOrWhiteSpace(requestedId))
        {
            GameData requestedData = LoadDataFromPath(GetLegacyPlayerSaveFilePath(requestedId), logMissing: false);
            if (requestedData != null)
                return requestedData;
        }

        string storedId = PlayerPrefs.GetString(PlayerPrefsSelectedPlayerKey, string.Empty);
        storedId = NormalizePlayerId(storedId);
        if (!string.IsNullOrWhiteSpace(storedId)
            && !string.Equals(storedId, requestedId, System.StringComparison.OrdinalIgnoreCase))
        {
            GameData storedData = LoadDataFromPath(GetLegacyPlayerSaveFilePath(storedId), logMissing: false);
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
            MigrateLegacyPlayerIdentityFields(data, json);
            MigrateLegacyCurrencyFields(data, json);
            MigrateSaveVersion(data);
            EnsureNarrativeCollections(data);
            Debug.Log($"Dati caricati con successo da: {path}");
            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Errore durante il caricamento dei dati: {e.Message}");
            return null;
        }
    }

    private static string GetLegacyPlayerSaveFilePath(string playerId)
    {
        return Path.Combine(
            Application.persistentDataPath,
            LegacyCharacterSavePrefix + SanitizeFileName(playerId) + LegacyCharacterSaveExtension);
    }

    private static string NormalizePlayerId(string playerId)
    {
        return string.IsNullOrWhiteSpace(playerId) ? string.Empty : playerId.Trim();
    }

    private static void PrepareSinglePlayerData(GameData data)
    {
        if (data == null)
            return;

        data.saveVersion = CurrentSaveVersion;
        data.playerId = SinglePlayerId;
        data.playerName = ResolvePlayerName(data.playerName);
        data.selectedClassId = ResolveClassId(data.selectedClassId);
        data.usesUnifiedCoins = true;
        EnsureNarrativeCollections(data);
    }

    private static string ResolvePlayerName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            return DefaultPlayerName;

        string normalized = playerName.Trim();
        return IsLegacyArchetypeName(normalized) ? DefaultPlayerName : normalized;
    }

    private static string ResolveClassId(string classId)
    {
        return string.IsNullOrWhiteSpace(classId) ? string.Empty : classId.Trim();
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

    private static void StoreSinglePlayerId()
    {
        PlayerPrefs.SetString(PlayerPrefsSelectedPlayerKey, SinglePlayerId);
        PlayerPrefs.Save();
    }

    private static string SanitizeFileName(string value)
    {
        string normalized = NormalizePlayerId(value);
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

    private static void MigrateLegacyPlayerIdentityFields(GameData data, string json)
    {
        if (data == null || string.IsNullOrWhiteSpace(json))
            return;

        if (string.IsNullOrWhiteSpace(data.playerId))
        {
            data.playerId = SinglePlayerId;
        }

        if (string.IsNullOrWhiteSpace(data.playerName))
        {
            string legacyPlayerName = ReadJsonString(json, "characterName");
            data.playerName = ResolvePlayerName(legacyPlayerName);
        }

        if (string.IsNullOrWhiteSpace(data.selectedClassId))
        {
            string legacyClassId = ReadJsonString(json, "selectedClassId");
            if (string.IsNullOrWhiteSpace(legacyClassId))
                legacyClassId = ReadJsonString(json, "startingClassId");
            if (string.IsNullOrWhiteSpace(legacyClassId))
                legacyClassId = ReadJsonString(json, "classId");
            if (string.IsNullOrWhiteSpace(legacyClassId))
            {
                legacyClassId = ReadJsonString(json, "selectedCharacterId");
                if (string.Equals(legacyClassId, SinglePlayerId, System.StringComparison.OrdinalIgnoreCase))
                    legacyClassId = string.Empty;
            }

            data.selectedClassId = ResolveClassId(legacyClassId);
        }

        if (!data.startingClassApplied)
            data.startingClassApplied = ReadJsonBool(json, "selectedCharacterStartApplied")
                                        || ReadJsonBool(json, "startingClassApplied");
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

                case 1:
                    data.playerId = SinglePlayerId;
                    data.playerName = ResolvePlayerName(data.playerName);
                    data.selectedClassId = ResolveClassId(data.selectedClassId);
                    data.saveVersion = 2;
                    break;

                case 2:
                    EnsureNarrativeCollections(data);
                    data.saveVersion = 3;
                    break;

                default:
                    Debug.LogWarning($"Migrazione non disponibile dalla versione {data.saveVersion}.");
                    return;
            }
        }
    }

    private static void EnsureNarrativeCollections(GameData data)
    {
        if (data == null)
            return;

        data.storyFlags ??= System.Array.Empty<string>();
        data.dialogueHistory ??= new SavedDialogueHistoryData();
        data.dialogueHistory.readNodeKeys ??= System.Array.Empty<string>();
        data.dialogueHistory.selectedChoiceKeys ??= System.Array.Empty<string>();
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

    private static bool ReadJsonBool(string json, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(fieldName))
            return false;

        var match = System.Text.RegularExpressions.Regex.Match(
            json,
            "\"" + System.Text.RegularExpressions.Regex.Escape(fieldName) + "\"\\s*:\\s*(true|false)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return match.Success && bool.TryParse(match.Groups[1].Value, out bool value) && value;
    }

    private static string ReadJsonString(string json, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(fieldName))
            return string.Empty;

        var match = System.Text.RegularExpressions.Regex.Match(
            json,
            "\"" + System.Text.RegularExpressions.Regex.Escape(fieldName) + "\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"");

        if (!match.Success)
            return string.Empty;

        return System.Text.RegularExpressions.Regex.Unescape(match.Groups[1].Value);
    }
}
