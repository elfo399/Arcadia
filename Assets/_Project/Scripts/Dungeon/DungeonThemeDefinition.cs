using UnityEngine;

[CreateAssetMenu(fileName = "DungeonTheme", menuName = "Dungeon/Theme Definition")]
public class DungeonThemeDefinition : ScriptableObject
{
    public string themeId = "Theme";
    public string displayName = "New Theme";
    public DungeonRoomSet roomSet;

    [Header("Generation")]
    [Tooltip("Room quantities used when this theme is selected. This takes priority over the floor entry's fallback definition.")]
    public DungeonFloorDefinition roomComposition;
}
