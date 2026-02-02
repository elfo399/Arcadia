using UnityEngine;
using System.IO;

public static class SaveSystem
{
    private static readonly string SAVE_FILE_NAME = "gamedata.json";

    public static void SaveData(GameData data)
    {
        string path = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
        string json = JsonUtility.ToJson(data, true); // 'true' per formattare il JSON in modo leggibile

        try
        {
            File.WriteAllText(path, json);
            Debug.Log($"Dati salvati con successo in: {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Errore durante il salvataggio dei dati: {e.Message}");
        }
    }

    public static GameData LoadData()
    {
        string path = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);

        if (!File.Exists(path))
        {
            Debug.Log("Nessun file di salvataggio trovato. Verranno usati i valori di default.");
            return null; // Ritorna null se non c'è un file
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

    public static string GetSaveFilePath()
    {
        return Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
    }
}
