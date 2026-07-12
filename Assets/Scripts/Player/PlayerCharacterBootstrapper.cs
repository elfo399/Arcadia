using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlayerCharacterBootstrapper
{
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
        PlayerStats stats = PlayerStats.instance;

        if (stats == null)
            return false;

        PlayerCharacterDatabase database = databaseOverride != null
            ? databaseOverride
            : stats.CharacterDatabase;

        if (database == null)
        {
            if (!stats.HasInspectorStartingCharacter)
            {
                Debug.LogWarning("[PlayerCharacterBootstrapper] PlayerCharacterDatabase non assegnato a PlayerStats.");
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
