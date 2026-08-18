using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DungeonFloorThemeTable", menuName = "Dungeon/Floor Theme Table")]
public class DungeonFloorThemeTable : ScriptableObject
{
    [Serializable]
    public class ThemeChoice
    {
        public DungeonThemeDefinition theme;
        [Min(1)] public int weight = 1;
    }

    [Serializable]
    public class FloorThemeEntry
    {
        [Min(1)] public int floorNumber = 1;
        public List<ThemeChoice> themes = new List<ThemeChoice>();
        [Tooltip("Optional generation/content overrides; theme selection remains in this table.")]
        public DungeonFloorDefinition floorDefinition;
    }

    public List<FloorThemeEntry> floors = new List<FloorThemeEntry>();

    public FloorThemeEntry GetEntryForFloor(int floor)
    {
        if (floors == null)
            return null;

        for (int i = 0; i < floors.Count; i++)
        {
            FloorThemeEntry entry = floors[i];
            if (entry != null && entry.floorNumber == floor)
                return entry;
        }

        return null;
    }
}
