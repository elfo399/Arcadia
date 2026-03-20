using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(TreasureChestLootTable.LootEntry))]
public class TreasureChestLootEntryDrawer : PropertyDrawer
{
    private const float Line = 18f;
    private const float Gap = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return (Line * 3f) + (Gap * 2f);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect row = new Rect(position.x, position.y, position.width, Line);
        EditorGUI.PrefixLabel(row, label);

        row.y += Line + Gap;
        EditorGUI.PropertyField(row, property.FindPropertyRelative("dropChance"), new GUIContent("Drop %"));

        row.y += Line + Gap;
        DrawRewardField(row, property);

        EditorGUI.EndProperty();
    }

    private static void DrawRewardField(Rect row, SerializedProperty property)
    {
        SerializedProperty itemProp = property.FindPropertyRelative("item");
        SerializedProperty usableProp = property.FindPropertyRelative("usable");
        SerializedProperty magicProp = property.FindPropertyRelative("magic");
        SerializedProperty armorProp = property.FindPropertyRelative("armor");
        SerializedProperty weaponProp = property.FindPropertyRelative("weapon");

        Object current = GetAssignedAsset(itemProp, usableProp, magicProp, armorProp, weaponProp);
        Object picked = EditorGUI.ObjectField(row, "Reward", current, typeof(ScriptableObject), false);
        if (picked == current)
            return;

        itemProp.objectReferenceValue = null;
        usableProp.objectReferenceValue = null;
        magicProp.objectReferenceValue = null;
        armorProp.objectReferenceValue = null;
        weaponProp.objectReferenceValue = null;

        if (picked == null)
            return;

        if (picked is WeaponItem weapon)
            weaponProp.objectReferenceValue = weapon;
        else if (picked is ArmorItemData armor)
            armorProp.objectReferenceValue = armor;
        else if (picked is MagicItemData magic)
            magicProp.objectReferenceValue = magic;
        else if (picked is UsableItemData usable)
            usableProp.objectReferenceValue = usable;
        else if (picked is ItemData item)
            itemProp.objectReferenceValue = item;
    }

    private static Object GetAssignedAsset(
        SerializedProperty itemProp,
        SerializedProperty usableProp,
        SerializedProperty magicProp,
        SerializedProperty armorProp,
        SerializedProperty weaponProp)
    {
        if (weaponProp != null && weaponProp.objectReferenceValue != null) return weaponProp.objectReferenceValue;
        if (armorProp != null && armorProp.objectReferenceValue != null) return armorProp.objectReferenceValue;
        if (magicProp != null && magicProp.objectReferenceValue != null) return magicProp.objectReferenceValue;
        if (usableProp != null && usableProp.objectReferenceValue != null) return usableProp.objectReferenceValue;
        if (itemProp != null && itemProp.objectReferenceValue != null) return itemProp.objectReferenceValue;
        return null;
    }
}
