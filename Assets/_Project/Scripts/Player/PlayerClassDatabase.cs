using System;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "PlayerClassDatabase", menuName = "Arcadia/Player Class Database")]
public class PlayerClassDatabase : ScriptableObject
{
    [SerializeField, FormerlySerializedAs("defaultCharacter")] private PlayerClassData defaultClass;
    [SerializeField, FormerlySerializedAs("characters")] private PlayerClassData[] classes = Array.Empty<PlayerClassData>();

    public PlayerClassData DefaultClass => defaultClass != null ? defaultClass : GetFirstClass();
    public PlayerClassData[] Classes => classes;

    public PlayerClassData GetById(string classId)
    {
        if (classes == null || classes.Length == 0)
            return DefaultClass;

        if (string.IsNullOrWhiteSpace(classId))
            return DefaultClass;

        string normalizedId = classId.Trim();
        for (int i = 0; i < classes.Length; i++)
        {
            PlayerClassData playerClass = classes[i];
            if (playerClass == null)
                continue;

            if (string.Equals(playerClass.GetClassId(), normalizedId, StringComparison.OrdinalIgnoreCase))
                return playerClass;
        }

        return DefaultClass;
    }

    private PlayerClassData GetFirstClass()
    {
        if (classes == null)
            return null;

        for (int i = 0; i < classes.Length; i++)
        {
            if (classes[i] != null)
                return classes[i];
        }

        return null;
    }
}
