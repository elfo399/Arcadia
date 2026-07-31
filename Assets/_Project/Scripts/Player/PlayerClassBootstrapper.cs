using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlayerClassBootstrapper
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

    public static bool ApplyToCurrentPlayer(PlayerClassDatabase databaseOverride = null)
    {
        PlayerStats stats = PlayerStats.instance;

        if (stats == null)
            return false;

        PlayerClassDatabase database = databaseOverride != null
            ? databaseOverride
            : stats.ClassDatabase;

        if (database == null)
        {
            if (!stats.HasInspectorStartingClass)
            {
                Debug.LogWarning("[PlayerClassBootstrapper] PlayerClassDatabase non assegnato a PlayerStats.");
                return false;
            }
        }

        return stats.TryApplySelectedClassStart(database);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyToCurrentPlayer();
    }
}
