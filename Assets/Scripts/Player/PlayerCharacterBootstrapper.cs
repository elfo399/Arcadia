using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlayerCharacterBootstrapper
{
    private const string ResourcesDatabasePath = "PlayerCharacterDatabase";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyAfterInitialSceneLoad()
    {
        ApplyToCurrentPlayer();
    }

    public static bool ApplyToCurrentPlayer(PlayerCharacterDatabase databaseOverride = null)
    {
        PlayerStats stats = PlayerStats.instance != null
            ? PlayerStats.instance
            : Object.FindObjectOfType<PlayerStats>();

        if (stats == null)
            return false;

        PlayerCharacterDatabase database = databaseOverride != null
            ? databaseOverride
            : Resources.Load<PlayerCharacterDatabase>(ResourcesDatabasePath);

        if (database == null)
        {
            if (!stats.HasInspectorStartingCharacter)
            {
                Debug.LogWarning($"[PlayerCharacterBootstrapper] Missing Resources/{ResourcesDatabasePath}.asset.");
                return false;
            }
        }

        return stats.TryApplySelectedCharacterStart(database);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyToCurrentPlayer();
    }
}
