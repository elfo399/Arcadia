using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ItemRegistry.Entry))]
public class ItemRegistryEntryDrawer : PropertyDrawer
{
    private const float Line = 18f;
    private const float Gap = 2f;

    private enum EntryDataKind
    {
        Weapon,
        Usable,
        Item,
        Armor,
        Magic
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return (property.isExpanded ? 6 : 1) * (Line + Gap);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect row = new Rect(position.x, position.y, position.width, Line);
        string foldoutLabel = BuildFoldoutLabel(property, label);
        property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, foldoutLabel, true);

        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;

        row.y += Line + Gap;
        EditorGUI.PropertyField(row, property.FindPropertyRelative("category"));

        row.y += Line + Gap;
        EditorGUI.PropertyField(row, property.FindPropertyRelative("key"));

        row.y += Line + Gap;
        EditorGUI.PropertyField(row, property.FindPropertyRelative("itemName"));

        row.y += Line + Gap;
        EditorGUI.PropertyField(row, property.FindPropertyRelative("icon"));

        row.y += Line + Gap;
        DrawRelevantDataField(row, property);

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    private static string BuildFoldoutLabel(SerializedProperty property, GUIContent fallback)
    {
        string category = ReadString(property, "category");
        string key = ReadString(property, "key");
        string itemName = ReadString(property, "itemName");

        if (!string.IsNullOrWhiteSpace(category) && !string.IsNullOrWhiteSpace(key))
            return category.Trim() + " - " + key.Trim();

        if (!string.IsNullOrWhiteSpace(itemName))
            return itemName.Trim();

        return fallback.text;
    }

    private static void DrawRelevantDataField(Rect row, SerializedProperty property)
    {
        EntryDataKind kind = ResolveDataKind(property);

        switch (kind)
        {
            case EntryDataKind.Weapon:
                EditorGUI.PropertyField(row, property.FindPropertyRelative("weaponData"));
                break;
            case EntryDataKind.Usable:
                EditorGUI.PropertyField(row, property.FindPropertyRelative("usableData"));
                break;
            case EntryDataKind.Item:
                EditorGUI.PropertyField(row, property.FindPropertyRelative("itemData"));
                break;
            case EntryDataKind.Armor:
                EditorGUI.PropertyField(row, property.FindPropertyRelative("armorData"));
                break;
            case EntryDataKind.Magic:
                EditorGUI.PropertyField(row, property.FindPropertyRelative("magicData"));
                break;
        }
    }

    private static EntryDataKind ResolveDataKind(SerializedProperty property)
    {
        if (HasReference(property, "weaponData")) return EntryDataKind.Weapon;
        if (HasReference(property, "usableData")) return EntryDataKind.Usable;
        if (HasReference(property, "itemData")) return EntryDataKind.Item;
        if (HasReference(property, "armorData")) return EntryDataKind.Armor;
        if (HasReference(property, "magicData")) return EntryDataKind.Magic;

        string category = ReadString(property, "category");
        return ResolveDataKindFromCategory(category);
    }

    private static EntryDataKind ResolveDataKindFromCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return EntryDataKind.Item;

        string normalized = category.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "healing":
            case "heal":
            case "boost":
            case "attack":
            case "magic":
                return EntryDataKind.Magic;

            case "usable":
            case "usabili":
                return EntryDataKind.Usable;

            case "armor":
            case "armour":
            case "helmet":
            case "helmets":
            case "chestplate":
            case "chestplates":
            case "leggings":
            case "leggins":
            case "boots":
                return EntryDataKind.Armor;

            case "nouseable":
            case "nousable":
            case "no usable":
            case "no-usable":
            case "non usable":
            case "non-usable":
            case "nonusabili":
            case "ammo":
            case "arrow":
                return EntryDataKind.Item;

            default:
                return EntryDataKind.Weapon;
        }
    }

    private static bool HasReference(SerializedProperty property, string name)
    {
        SerializedProperty child = property.FindPropertyRelative(name);
        return child != null && child.objectReferenceValue != null;
    }

    private static string ReadString(SerializedProperty property, string name)
    {
        SerializedProperty child = property.FindPropertyRelative(name);
        return child != null ? child.stringValue : string.Empty;
    }
}
