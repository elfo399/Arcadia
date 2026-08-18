using UnityEditor;
using UnityEngine;

public static class RoomTypeValidation
{
    private const string RoomPrefabRoot = "Assets/_Project/Data/Database/Floor/Floors";

    [MenuItem("Arcadia/Validation/Validate Room Type Rules")]
    private static void ValidateRoomTypeRules()
    {
        int warnings = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { RoomPrefabRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Room room = contents.GetComponentInChildren<Room>(true);
                if (room == null || room.roomData == null)
                    continue;

                switch (room.roomData.roomType)
                {
                    case RoomType.Wave:
                        warnings += WarnIfMissing<WaveRoomRule>(room, path, "Wave");
                        break;
                    case RoomType.Challenge:
                        warnings += WarnIfMissing<ChallengeRoomRule>(room, path, "Challenge");
                        break;
                    case RoomType.Miniboss:
                        warnings += WarnIfMissing<MinibossRoomRule>(room, path, "Miniboss");
                        break;
                    case RoomType.Boss:
                        warnings += WarnIfMissing<CombatRoomRule>(room, path, "Boss");
                        break;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        Debug.Log($"[RoomTypeValidation] Completed; {warnings} room-type/rule warnings.");
    }

    private static int WarnIfMissing<T>(Room room, string path, string label) where T : Component
    {
        if (room.GetComponentInChildren<T>(true) != null)
            return 0;

        Debug.LogWarning($"[RoomTypeValidation] {label} RoomData has no {typeof(T).Name}: {path}", room);
        return 1;
    }
}
