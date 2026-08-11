using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(QuestManager.QuestRewardData))]
public class QuestManagerRewardDrawer : QuestRewardDrawerBase
{
}

[CustomPropertyDrawer(typeof(QuestRewardEntryData))]
public class InventoryQuestRewardDrawer : QuestRewardDrawerBase
{
}

public class QuestRewardDrawerBase : PropertyDrawer
{
    private const float Line = 18f;
    private const float Gap = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        int lines = 3;
        QuestRewardType type = GetRewardType(property);
        if (type != QuestRewardType.Experience)
            lines += 1;
        return lines * (Line + Gap);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect row = new Rect(position.x, position.y, position.width, Line);
        EditorGUI.PrefixLabel(row, label);

        row.y += Line + Gap;
        SerializedProperty rewardType = property.FindPropertyRelative("rewardType");
        EditorGUI.PropertyField(row, rewardType);

        row.y += Line + Gap;
        EditorGUI.PropertyField(row, property.FindPropertyRelative("amount"));

        row.y += Line + Gap;
        DrawAssetField(row, property, (QuestRewardType)rewardType.enumValueIndex);

        EditorGUI.EndProperty();
    }

    private static QuestRewardType GetRewardType(SerializedProperty property)
    {
        SerializedProperty rewardType = property.FindPropertyRelative("rewardType");
        if (rewardType == null)
            return QuestRewardType.Item;
        return (QuestRewardType)rewardType.enumValueIndex;
    }

    private static void DrawAssetField(Rect row, SerializedProperty property, QuestRewardType type)
    {
        switch (type)
        {
            case QuestRewardType.Weapon:
                EditorGUI.PropertyField(row, property.FindPropertyRelative("weaponAsset"), new GUIContent("Weapon"));
                break;
            case QuestRewardType.Usable:
                EditorGUI.PropertyField(row, property.FindPropertyRelative("usableAsset"), new GUIContent("Usable"));
                break;
            case QuestRewardType.Item:
                EditorGUI.PropertyField(row, property.FindPropertyRelative("itemAsset"), new GUIContent("Item"));
                break;
            case QuestRewardType.Magic:
                EditorGUI.PropertyField(row, property.FindPropertyRelative("magicAsset"), new GUIContent("Magic"));
                break;
            case QuestRewardType.MagicBlueprint:
                EditorGUI.PropertyField(row, property.FindPropertyRelative("magicBlueprintAsset"), new GUIContent("Magic Blueprint"));
                break;
            case QuestRewardType.Armor:
                EditorGUI.PropertyField(row, property.FindPropertyRelative("armorAsset"), new GUIContent("Armor"));
                break;
            case QuestRewardType.Experience:
                EditorGUI.HelpBox(row, "Amount = experience awarded", MessageType.None);
                break;
        }
    }
}

