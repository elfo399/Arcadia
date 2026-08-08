using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(DialogueConditionGroup))]
public sealed class DialogueConditionGroupDrawer : PropertyDrawer
{
    private const float VerticalGap = 2f;
    private const float RemoveButtonWidth = 22f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
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
        DrawHeader(header, property, label, logic, negate);
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
        SerializedProperty logic,
        SerializedProperty negate)
    {
        float labelWidth = Mathf.Min(EditorGUIUtility.labelWidth, Mathf.Max(80f, rect.width * 0.46f));
        Rect foldoutRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

        Rect content = new Rect(foldoutRect.xMax, rect.y, Mathf.Max(0f, rect.xMax - foldoutRect.xMax), rect.height);
        const float negateWidth = 54f;
        Rect logicRect = new Rect(content.x, content.y, Mathf.Max(0f, content.width - negateWidth - 3f), content.height);
        Rect negateRect = new Rect(logicRect.xMax + 3f, content.y, negateWidth, content.height);

        EditorGUI.PropertyField(logicRect, logic, GUIContent.none);
        EditorGUI.PropertyField(negateRect, negate, new GUIContent("NOT"));
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
        DrawHeader(new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight), property, label);
        y += EditorGUIUtility.singleLineHeight + VerticalGap;

        int oldIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = oldIndent + 1;

        DialogueConditionType type = GetType(property);
        switch (type)
        {
            case DialogueConditionType.PlayerAttribute:
                DrawPropertyLine(position, ref y, property, "playerAttribute", "Attribute");
                DrawComparisonLine(position, ref y, property);
                break;

            case DialogueConditionType.PlayerLevel:
            case DialogueConditionType.Karma:
            case DialogueConditionType.Benedetto:
            case DialogueConditionType.Malefico:
            case DialogueConditionType.HasCoins:
            case DialogueConditionType.DungeonFloor:
                DrawComparisonLine(position, ref y, property);
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
                DrawComparisonLine(position, ref y, property);
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
            case DialogueConditionType.QuestState:
            case DialogueConditionType.StoryFlag:
            case DialogueConditionType.HasItem:
            case DialogueConditionType.ItemAmount:
                return 3;

            case DialogueConditionType.DialogueNodeRead:
                return 4;

            case DialogueConditionType.DialogueChoiceSeen:
                return 5;

            default:
                return 2;
        }
    }

    private static void DrawHeader(Rect rect, SerializedProperty property, GUIContent label)
    {
        Rect content = EditorGUI.PrefixLabel(rect, label);
        const float negateWidth = 58f;
        Rect typeRect = new Rect(content.x, content.y, Mathf.Max(0f, content.width - negateWidth - 4f), content.height);
        Rect negateRect = new Rect(typeRect.xMax + 4f, content.y, negateWidth, content.height);

        EditorGUI.PropertyField(typeRect, property.FindPropertyRelative("type"), GUIContent.none);
        EditorGUI.PropertyField(negateRect, property.FindPropertyRelative("negate"), new GUIContent("NOT"));
    }

    private static void DrawComparisonLine(Rect bounds, ref float y, SerializedProperty property)
    {
        Rect line = NextLine(bounds, ref y);
        Rect content = EditorGUI.PrefixLabel(line, new GUIContent("Comparison"));
        float operatorWidth = Mathf.Max(80f, content.width * 0.58f);
        Rect operatorRect = new Rect(content.x, content.y, operatorWidth, content.height);
        Rect valueRect = new Rect(operatorRect.xMax + 3f, content.y, Mathf.Max(0f, content.xMax - operatorRect.xMax - 3f), content.height);

        EditorGUI.PropertyField(operatorRect, property.FindPropertyRelative("comparison"), GUIContent.none);
        EditorGUI.PropertyField(valueRect, property.FindPropertyRelative("value"), GUIContent.none);
    }

    private static void DrawPropertyLine(
        Rect bounds,
        ref float y,
        SerializedProperty property,
        string propertyName,
        string label)
    {
        EditorGUI.PropertyField(
            NextLine(bounds, ref y),
            property.FindPropertyRelative(propertyName),
            new GUIContent(label),
            true);
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
            case DialogueActionType.ModifyBenedetto:
            case DialogueActionType.ModifyMalefico:
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
