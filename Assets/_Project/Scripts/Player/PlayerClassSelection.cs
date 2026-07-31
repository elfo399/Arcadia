using UnityEngine;

public static class PlayerClassSelection
{
    public const string DefaultPlayerId = SaveSystem.SingleCharacterId;
    private static bool pendingNewPlayer;
    private static string pendingStartingClassId;
    private static PlayerClassData pendingStartingClass;

    public static string PendingNewPlayerId => pendingNewPlayer ? SaveSystem.SingleCharacterId : string.Empty;
    public static string PendingStartingClassId => pendingNewPlayer ? pendingStartingClassId : string.Empty;
    public static PlayerClassData PendingStartingClass => pendingNewPlayer ? pendingStartingClass : null;

    public static string GetSelectedPlayerId()
    {
        return SaveSystem.SingleCharacterId;
    }

    public static void StartNewPlayer(PlayerClassData playerClass)
    {
        pendingStartingClass = playerClass;
        pendingStartingClassId = playerClass != null ? playerClass.GetClassId() : string.Empty;
        StartNewPlayer();
    }

    public static void StartNewPlayer(string classId)
    {
        pendingStartingClass = null;
        pendingStartingClassId = string.IsNullOrWhiteSpace(classId) ? string.Empty : classId.Trim();
        StartNewPlayer();
    }

    public static void StartNewPlayer()
    {
        if (PlayerStats.instance != null && !string.IsNullOrWhiteSpace(PlayerStats.instance.SelectedCharacterId))
            PlayerStats.instance.SaveStatsImmediate();

        pendingNewPlayer = true;
        if (pendingStartingClass == null && string.IsNullOrWhiteSpace(pendingStartingClassId))
            pendingStartingClassId = string.Empty;
        SaveSystem.SelectCharacter(SaveSystem.SingleCharacterId);
        SaveSystem.EnsureCharacterData(SaveSystem.SingleCharacterId, SaveSystem.DefaultCharacterName);
    }

    internal static void ClearPendingNewPlayer()
    {
        pendingNewPlayer = false;
        pendingStartingClass = null;
        pendingStartingClassId = string.Empty;
    }

}
