using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(DialogueConditionGroup))]
public sealed class DialogueConditionGroupDrawer : PropertyDrawer
{
    private const float VerticalGap = 2f;
    private const float RemoveButtonWidth = 22f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight * 2f + VerticalGap;
        if (!property.isExpanded)
            return height;

        SerializedProperty conditions = property.FindPropertyRelative("conditions");
        SerializedProperty groups = property.FindPropertyRelative("groups");
        if (conditions == null || groups == null)
            return height;

        height += VerticalGap + EditorGUI.GetPropertyHeight(conditions, true);
        height += VerticalGap + EditorGUIUtility.singleLineHeight;

        for (int i = 0; i < groups.arraySize; i++)
        {
            SerializedProperty child = groups.GetArrayElementAtIndex(i);
            height += VerticalGap;
            height += child.managedReferenceValue == null
                ? EditorGUIUtility.singleLineHeight
                : EditorGUI.GetPropertyHeight(child, true);
        }

        height += VerticalGap + EditorGUIUtility.singleLineHeight;
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty logic = property.FindPropertyRelative("logic");
        SerializedProperty negate = property.FindPropertyRelative("negate");
        SerializedProperty conditions = property.FindPropertyRelative("conditions");
        SerializedProperty groups = property.FindPropertyRelative("groups");

        if (logic == null || negate == null || conditions == null || groups == null)
        {
            EditorGUI.LabelField(position, label, new GUIContent("Gruppo non serializzabile"));
            EditorGUI.EndProperty();
            return;
        }

        float y = position.y;
        Rect header = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
        DrawHeader(header, property, label, negate);
        y += EditorGUIUtility.singleLineHeight + VerticalGap;
        DrawLabeledField(
            new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight),
            "Mode",
            logic);
        y += EditorGUIUtility.singleLineHeight + VerticalGap;

        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        int previousIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = previousIndent + 1;

        float conditionsHeight = EditorGUI.GetPropertyHeight(conditions, true);
        EditorGUI.PropertyField(
            new Rect(position.x, y, position.width, conditionsHeight),
            conditions,
            new GUIContent("Conditions"),
            true);
        y += conditionsHeight + VerticalGap;

        EditorGUI.LabelField(
            new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight),
            $"Nested Groups ({groups.arraySize})",
            EditorStyles.miniBoldLabel);
        y += EditorGUIUtility.singleLineHeight + VerticalGap;

        int removeIndex = -1;
        for (int i = 0; i < groups.arraySize; i++)
        {
            SerializedProperty child = groups.GetArrayElementAtIndex(i);
            float childHeight = child.managedReferenceValue == null
                ? EditorGUIUtility.singleLineHeight
                : EditorGUI.GetPropertyHeight(child, true);

            Rect childRect = new Rect(
                position.x,
                y,
                Mathf.Max(0f, position.width - RemoveButtonWidth - 3f),
                childHeight);
            Rect removeRect = new Rect(
                childRect.xMax + 3f,
                y,
                RemoveButtonWidth,
                EditorGUIUtility.singleLineHeight);

            if (child.managedReferenceValue == null)
            {
                if (GUI.Button(childRect, $"Create Group {i}"))
                {
                    child.managedReferenceValue = new DialogueConditionGroup();
                    child.isExpanded = true;
                    property.serializedObject.ApplyModifiedProperties();
                }
            }
            else
            {
                EditorGUI.PropertyField(childRect, child, new GUIContent($"Group {i}"), true);
            }

            if (GUI.Button(removeRect, new GUIContent("x", "Rimuovi questo gruppo annidato.")))
                removeIndex = i;

            y += childHeight + VerticalGap;
        }

        Rect addRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
        if (GUI.Button(addRect, "+ Add Nested Group"))
        {
            int newIndex = groups.arraySize;
            groups.arraySize++;
            SerializedProperty child = groups.GetArrayElementAtIndex(newIndex);
            child.managedReferenceValue = new DialogueConditionGroup();
            child.isExpanded = true;
            property.serializedObject.ApplyModifiedProperties();
        }

        if (removeIndex >= 0 && removeIndex < groups.arraySize)
        {
            groups.DeleteArrayElementAtIndex(removeIndex);
            property.serializedObject.ApplyModifiedProperties();
        }

        EditorGUI.indentLevel = previousIndent;
        EditorGUI.EndProperty();
    }

    private static void DrawHeader(
        Rect rect,
        SerializedProperty property,
        GUIContent label,
        SerializedProperty negate)
    {
        const float negateWidth = 56f;
        const float gap = 4f;
        float foldoutWidth = Mathf.Max(0f, rect.width - negateWidth - gap);
        Rect foldoutRect = new Rect(rect.x, rect.y, foldoutWidth, rect.height);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

        Rect negateRect = new Rect(rect.xMax - negateWidth, rect.y, negateWidth, rect.height);
        EditorGUI.PropertyField(negateRect, negate, new GUIContent("NOT"));
    }

    private static void DrawLabeledField(Rect rect, string label, SerializedProperty value)
    {
        Rect indented = EditorGUI.IndentedRect(rect);
        int oldIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        float labelWidth = Mathf.Clamp(indented.width * 0.34f, 52f, 100f);
        labelWidth = Mathf.Min(labelWidth, Mathf.Max(0f, indented.width - 44f));
        EditorGUI.LabelField(new Rect(indented.x, indented.y, labelWidth, indented.height), label);
        EditorGUI.PropertyField(
            new Rect(indented.x + labelWidth + 3f, indented.y, Mathf.Max(0f, indented.width - labelWidth - 3f), indented.height),
            value,
            GUIContent.none);
        EditorGUI.indentLevel = oldIndent;
    }
}

[CustomPropertyDrawer(typeof(DialogueItemReference))]
public sealed class DialogueItemReferenceDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect content = EditorGUI.PrefixLabel(position, label);
        SerializedProperty typeProperty = property.FindPropertyRelative("itemType");
        SerializedProperty assetProperty = GetSelectedAssetProperty(property, typeProperty);

        const float typeWidth = 82f;
        const float gap = 3f;
        Rect typeRect = new Rect(content.x, content.y, Mathf.Min(typeWidth, content.width), content.height);
        Rect assetRect = new Rect(
            typeRect.xMax + gap,
            content.y,
            Mathf.Max(0f, content.xMax - typeRect.xMax - gap),
            content.height);

        EditorGUI.PropertyField(typeRect, typeProperty, GUIContent.none);
        if (assetProperty != null)
            EditorGUI.PropertyField(assetRect, assetProperty, GUIContent.none);

        EditorGUI.EndProperty();
    }

    private static SerializedProperty GetSelectedAssetProperty(
        SerializedProperty property,
        SerializedProperty typeProperty)
    {
        DialogueItemType type = (DialogueItemType)typeProperty.enumValueIndex;
        switch (type)
        {
            case DialogueItemType.Weapon:
                return property.FindPropertyRelative("weapon");
            case DialogueItemType.Armor:
                return property.FindPropertyRelative("armor");
            case DialogueItemType.Magic:
                return property.FindPropertyRelative("magic");
            case DialogueItemType.Usable:
                return property.FindPropertyRelative("usable");
            default:
                return property.FindPropertyRelative("item");
        }
    }
}

[CustomPropertyDrawer(typeof(DialogueCondition))]
public sealed class DialogueConditionDrawer : PropertyDrawer
{
    private const float VerticalGap = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        int lineCount = GetLineCount(GetType(property));
        return lineCount * EditorGUIUtility.singleLineHeight + (lineCount - 1) * VerticalGap;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float y = position.y;
        DrawHeaderLabel(new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight), property, label);
        y += EditorGUIUtility.singleLineHeight + VerticalGap;
        DrawPropertyLine(position, ref y, property, "type", "Type");

        int oldIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = oldIndent + 1;

        DialogueConditionType type = GetType(property);
        switch (type)
        {
            case DialogueConditionType.PlayerAttribute:
                DrawPropertyLine(position, ref y, property, "playerAttribute", "Attribute");
                DrawPropertyLine(position, ref y, property, "comparison", "Operator");
                DrawPropertyLine(position, ref y, property, "value", "Value");
                break;

            case DialogueConditionType.PlayerLevel:
            case DialogueConditionType.Karma:
            case DialogueConditionType.HasCoins:
            case DialogueConditionType.DungeonFloor:
                DrawPropertyLine(position, ref y, property, "comparison", "Operator");
                DrawPropertyLine(position, ref y, property, "value", "Value");
                break;

            case DialogueConditionType.QuestState:
                DrawPropertyLine(position, ref y, property, "id", "Quest ID");
                DrawPropertyLine(position, ref y, property, "questState", "State");
                break;

            case DialogueConditionType.StoryFlag:
                DrawPropertyLine(position, ref y, property, "id", "Story Flag");
                DrawPropertyLine(position, ref y, property, "expected", "Expected");
                break;

            case DialogueConditionType.HasItem:
                DrawPropertyLine(position, ref y, property, "item", "Item");
                DrawPropertyLine(position, ref y, property, "expected", "Expected");
                break;

            case DialogueConditionType.ItemAmount:
                DrawPropertyLine(position, ref y, property, "item", "Item");
                DrawPropertyLine(position, ref y, property, "comparison", "Operator");
                DrawPropertyLine(position, ref y, property, "value", "Value");
                break;

            case DialogueConditionType.DialogueNodeRead:
                DrawPropertyLine(position, ref y, property, "conversationId", "Conversation (optional)");
                DrawPropertyLine(position, ref y, property, "nodeId", "Node ID");
                DrawPropertyLine(position, ref y, property, "expected", "Expected");
                break;

            case DialogueConditionType.DialogueChoiceSeen:
                DrawPropertyLine(position, ref y, property, "conversationId", "Conversation (optional)");
                DrawPropertyLine(position, ref y, property, "nodeId", "Node ID");
                DrawPropertyLine(position, ref y, property, "choiceId", "Choice ID");
                DrawPropertyLine(position, ref y, property, "expected", "Expected");
                break;
        }

        EditorGUI.indentLevel = oldIndent;
        EditorGUI.EndProperty();
    }

    private static DialogueConditionType GetType(SerializedProperty property)
    {
        SerializedProperty typeProperty = property.FindPropertyRelative("type");
        return (DialogueConditionType)typeProperty.enumValueIndex;
    }

    private static int GetLineCount(DialogueConditionType type)
    {
        switch (type)
        {
            case DialogueConditionType.PlayerAttribute:
            case DialogueConditionType.ItemAmount:
                return 5;

            case DialogueConditionType.QuestState:
            case DialogueConditionType.StoryFlag:
            case DialogueConditionType.HasItem:
                return 4;

            case DialogueConditionType.DialogueNodeRead:
                return 5;

            case DialogueConditionType.DialogueChoiceSeen:
                return 6;

            default:
                return 4;
        }
    }

    private static void DrawHeaderLabel(Rect rect, SerializedProperty property, GUIContent label)
    {
        Rect indented = EditorGUI.IndentedRect(rect);
        const float negateWidth = 58f;
        const float gap = 4f;
        Rect labelRect = new Rect(indented.x, indented.y, Mathf.Max(0f, indented.width - negateWidth - gap), indented.height);
        Rect negateRect = new Rect(indented.xMax - negateWidth, indented.y, negateWidth, indented.height);
        EditorGUI.LabelField(labelRect, label, EditorStyles.boldLabel);
        EditorGUI.PropertyField(negateRect, property.FindPropertyRelative("negate"), new GUIContent("NOT"));
    }

    private static void DrawPropertyLine(
        Rect bounds,
        ref float y,
        SerializedProperty property,
        string propertyName,
        string label)
    {
        Rect line = EditorGUI.IndentedRect(NextLine(bounds, ref y));
        int oldIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        float labelWidth = Mathf.Clamp(line.width * 0.36f, 60f, 120f);
        labelWidth = Mathf.Min(labelWidth, Mathf.Max(0f, line.width - 44f));
        Rect labelRect = new Rect(line.x, line.y, labelWidth, line.height);
        Rect fieldRect = new Rect(
            labelRect.xMax + 3f,
            line.y,
            Mathf.Max(0f, line.xMax - labelRect.xMax - 3f),
            line.height);
        EditorGUI.LabelField(labelRect, label);
        EditorGUI.PropertyField(fieldRect, property.FindPropertyRelative(propertyName), GUIContent.none, true);
        EditorGUI.indentLevel = oldIndent;
    }

    private static Rect NextLine(Rect bounds, ref float y)
    {
        Rect result = new Rect(bounds.x, y, bounds.width, EditorGUIUtility.singleLineHeight);
        y += EditorGUIUtility.singleLineHeight + VerticalGap;
        return result;
    }
}

[CustomPropertyDrawer(typeof(DialogueAction))]
public sealed class DialogueActionDrawer : PropertyDrawer
{
    private const float VerticalGap = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        int lineCount = GetLineCount(GetType(property));
        return lineCount * EditorGUIUtility.singleLineHeight + (lineCount - 1) * VerticalGap;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float y = position.y;
        DrawHeader(new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight), property, label);
        y += EditorGUIUtility.singleLineHeight + VerticalGap;

        int oldIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = oldIndent + 1;

        DialogueActionType type = GetType(property);
        switch (type)
        {
            case DialogueActionType.ModifyKarma:
                DrawPropertyLine(position, ref y, property, "amount", "Delta");
                break;

            case DialogueActionType.GiveAttributePoint:
            case DialogueActionType.AddCoins:
            case DialogueActionType.RemoveCoins:
            case DialogueActionType.RestoreHealth:
            case DialogueActionType.RestoreMana:
            case DialogueActionType.RestoreStamina:
            case DialogueActionType.RestoreFlasks:
                DrawPropertyLine(position, ref y, property, "amount", "Amount");
                break;

            case DialogueActionType.AddItem:
            case DialogueActionType.RemoveItem:
                DrawPropertyLine(position, ref y, property, "item", "Item");
                DrawPropertyLine(position, ref y, property, "amount", "Amount");
                break;

            case DialogueActionType.StartQuest:
                DrawPropertyLine(position, ref y, property, "questDefinition", "Quest");
                break;

            case DialogueActionType.CompleteQuest:
            case DialogueActionType.FailQuest:
                DrawPropertyLine(position, ref y, property, "id", "Quest ID");
                break;

            case DialogueActionType.SetStoryFlag:
            case DialogueActionType.ClearStoryFlag:
                DrawPropertyLine(position, ref y, property, "id", "Story Flag");
                break;

            case DialogueActionType.OpenService:
                DrawPropertyLine(position, ref y, property, "serviceId", "Service ID");
                break;

            case DialogueActionType.Teleport:
                DrawPropertyLine(position, ref y, property, "teleportTargetId", "Target ID");
                DrawPropertyLine(position, ref y, property, "teleportSceneName", "Scene (optional)");
                DrawPropertyLine(position, ref y, property, "useTeleportTargetRotation", "Use target rotation");
                break;
        }

        EditorGUI.indentLevel = oldIndent;
        EditorGUI.EndProperty();
    }

    private static DialogueActionType GetType(SerializedProperty property)
    {
        SerializedProperty typeProperty = property.FindPropertyRelative("type");
        return (DialogueActionType)typeProperty.enumValueIndex;
    }

    private static int GetLineCount(DialogueActionType type)
    {
        switch (type)
        {
            case DialogueActionType.AddItem:
            case DialogueActionType.RemoveItem:
                return 3;
            case DialogueActionType.Teleport:
                return 4;
            default:
                return 2;
        }
    }

    private static void DrawHeader(Rect rect, SerializedProperty property, GUIContent label)
    {
        Rect content = EditorGUI.PrefixLabel(rect, label);
        const float stopWidth = 54f;
        Rect typeRect = new Rect(content.x, content.y, Mathf.Max(0f, content.width - stopWidth - 4f), content.height);
        Rect stopRect = new Rect(typeRect.xMax + 4f, content.y, stopWidth, content.height);

        EditorGUI.PropertyField(typeRect, property.FindPropertyRelative("type"), GUIContent.none);
        EditorGUI.PropertyField(
            stopRect,
            property.FindPropertyRelative("stopOnFailure"),
            new GUIContent("Stop", "Interrompe il batch se questa action fallisce."));
    }

    private static void DrawPropertyLine(
        Rect bounds,
        ref float y,
        SerializedProperty property,
        string propertyName,
        string label)
    {
        Rect line = new Rect(bounds.x, y, bounds.width, EditorGUIUtility.singleLineHeight);
        y += EditorGUIUtility.singleLineHeight + VerticalGap;
        EditorGUI.PropertyField(
            line,
            property.FindPropertyRelative(propertyName),
            new GUIContent(label),
            true);
    }
}
