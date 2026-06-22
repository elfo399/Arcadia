using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(QuestManager.QuestObjectiveData))]
public class QuestManagerObjectiveDrawer : QuestObjectiveDrawerBase
{
}

[CustomPropertyDrawer(typeof(QuestObjectiveEntryData))]
public class InventoryQuestObjectiveDrawer : QuestObjectiveDrawerBase
{
}

public class QuestObjectiveDrawerBase : PropertyDrawer
{
    private const float Line = 18f;
    private const float Gap = 2f;

    private static readonly string[] AllTags = { "", "enemy", "item", "weapon", "armor", "magic", "usable", "key", "coin", "room", "Normal", "Boss", "Treasure", "Shop", "Blessed", "Evil", "Start", "chest", "floor", "Player", "Interactable", "NPC", "Door", "Chest" };
    private static readonly string[] EnemyTags = { "", "enemy" };
    private static readonly string[] CollectItemTags = { "", "item", "weapon", "armor", "magic", "usable", "key", "coin" };
    private static readonly string[] InteractionTags = { "", "Player", "Interactable", "NPC", "Door", "Chest" };
    private static readonly string[] RoomTags = { "", "room", "Normal", "Boss", "Treasure", "Shop", "Blessed", "Evil", "Start" };
    private static readonly string[] ChestTags = { "", "chest" };
    private static readonly string[] FloorTags = { "", "floor" };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return 10 * (Line + Gap);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect row = new Rect(position.x, position.y, position.width, Line);
        EditorGUI.PrefixLabel(row, label);

        DrawNext(ref row, property.FindPropertyRelative("title"), "Title");
        DrawNext(ref row, property.FindPropertyRelative("description"), "Description");

        SerializedProperty eventType = property.FindPropertyRelative("eventType");
        DrawNext(ref row, eventType, "Event Type");

        DrawNext(ref row, property.FindPropertyRelative("targetObject"), "Target Object");
        DrawTargetTagPopup(ref row, property, eventType);
        DrawNext(ref row, property.FindPropertyRelative("targetId"), "Target Id Fallback");
        DrawNext(ref row, property.FindPropertyRelative("requiredAmount"), "Required Amount");
        DrawNext(ref row, property.FindPropertyRelative("currentAmount"), "Current Amount");
        DrawNext(ref row, property.FindPropertyRelative("completed"), "Completed");

        EditorGUI.EndProperty();
    }

    private static void DrawNext(ref Rect row, SerializedProperty property, string label)
    {
        row.y += Line + Gap;
        if (property != null)
            EditorGUI.PropertyField(row, property, new GUIContent(label));
    }

    private static void DrawTargetTagPopup(ref Rect row, SerializedProperty property, SerializedProperty eventTypeProperty)
    {
        row.y += Line + Gap;

        SerializedProperty targetTag = property.FindPropertyRelative("targetTag");
        if (targetTag == null)
            return;

        QuestObjectiveEventType eventType = eventTypeProperty != null
            ? (QuestObjectiveEventType)eventTypeProperty.enumValueIndex
            : QuestObjectiveEventType.None;

        string[] options = BuildOptionsWithCurrent(GetTagOptions(eventType), targetTag.stringValue);
        string[] labels = BuildLabels(options);
        int currentIndex = FindOptionIndex(options, targetTag.stringValue);
        int selectedIndex = EditorGUI.Popup(row, "Target Tag", currentIndex, labels);
        if (selectedIndex >= 0 && selectedIndex < options.Length)
            targetTag.stringValue = options[selectedIndex];
    }

    private static string[] GetTagOptions(QuestObjectiveEventType eventType)
    {
        switch (eventType)
        {
            case QuestObjectiveEventType.KillEnemy:
                return EnemyTags;
            case QuestObjectiveEventType.CollectItem:
                return CollectItemTags;
            case QuestObjectiveEventType.Interact:
                return InteractionTags;
            case QuestObjectiveEventType.EnterRoom:
            case QuestObjectiveEventType.ClearRoom:
                return RoomTags;
            case QuestObjectiveEventType.OpenChest:
                return ChestTags;
            case QuestObjectiveEventType.ReachFloor:
                return FloorTags;
            default:
                return AllTags;
        }
    }

    private static string[] BuildOptionsWithCurrent(string[] baseOptions, string current)
    {
        if (string.IsNullOrWhiteSpace(current) || Contains(baseOptions, current))
            return baseOptions;

        var options = new List<string>(baseOptions) { current };
        return options.ToArray();
    }

    private static string[] BuildLabels(string[] options)
    {
        string[] labels = new string[options.Length];
        for (int i = 0; i < options.Length; i++)
            labels[i] = string.IsNullOrEmpty(options[i]) ? "Any" : options[i];
        return labels;
    }

    private static int FindOptionIndex(string[] options, string current)
    {
        for (int i = 0; i < options.Length; i++)
        {
            if (string.Equals(options[i], current, System.StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return 0;
    }

    private static bool Contains(string[] options, string value)
    {
        for (int i = 0; i < options.Length; i++)
        {
            if (string.Equals(options[i], value, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
