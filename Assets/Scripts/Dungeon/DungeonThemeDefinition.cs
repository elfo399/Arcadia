using UnityEngine;

[CreateAssetMenu(fileName = "DungeonTheme", menuName = "Dungeon/Theme Definition")]
public class DungeonThemeDefinition : ScriptableObject
{
    public string themeId = "Theme";
    public string displayName = "New Theme";
    public DungeonRoomSet roomSet;
}
