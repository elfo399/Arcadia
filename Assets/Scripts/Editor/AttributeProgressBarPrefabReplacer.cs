#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class AttributeProgressBarPrefabReplacer
{
    private const string ProgressBarPrefabPath = "Assets/Prefabs/UI/ProgressBarUI.prefab";
    private const string ScenesFolder = "Assets/Scenes";

    [MenuItem("Tools/Arcadia/UI/Replace Attribute Bars With Prefab")]
    public static void ReplaceInProjectScenes()
    {
        GameObject progressBarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProgressBarPrefabPath);
        if (progressBarPrefab == null)
        {
            Debug.LogError($"[AttributeProgressBarPrefabReplacer] Missing prefab at {ProgressBarPrefabPath}");
            return;
        }

        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string[] scenePaths = AssetDatabase.FindAssets("t:Scene", new[] { ScenesFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path)
            .ToArray();

        foreach (string scenePath in scenePaths)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!ReplaceInScene(scene, progressBarPrefab))
                continue;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[AttributeProgressBarPrefabReplacer] Replaced attribute progress bars in {scenePath}");
        }
    }

    private static bool ReplaceInScene(Scene scene, GameObject progressBarPrefab)
    {
        bool changed = false;
        AttributesUIManager[] managers = Object.FindObjectsOfType<AttributesUIManager>(true);

        for (int i = 0; i < managers.Length; i++)
        {
            AttributesUIManager manager = managers[i];
            if (manager == null || manager.gameObject.scene != scene)
                continue;

            var managerSo = new SerializedObject(manager);
            ProgressBarUI xpBar = ResolveBar(managerSo, "attributesXpProgressBar", manager.transform.root, "Center/Panel");
            ProgressBarUI loadBar = ResolveBar(managerSo, "attributesLoadProgressBar", manager.transform.root, "Left/Load");

            ProgressBarUI replacedXp = ReplaceBarWithPrefab(xpBar, progressBarPrefab, "XpProgressBar");
            ProgressBarUI replacedLoad = ReplaceBarWithPrefab(loadBar, progressBarPrefab, "LoadProgressBar");

            changed |= SetReference(managerSo, "attributesXpProgressBar", replacedXp);
            changed |= SetReference(managerSo, "attributesLoadProgressBar", replacedLoad);
            managerSo.ApplyModifiedPropertiesWithoutUndo();
        }

        return changed;
    }

    private static ProgressBarUI ResolveBar(SerializedObject managerSo, string propertyName, Transform managerRoot, string fallbackPath)
    {
        ProgressBarUI bar = managerSo.FindProperty(propertyName)?.objectReferenceValue as ProgressBarUI;
        if (bar != null)
            return bar;

        Transform skillRoot = FindDeepChildByName(managerRoot, "SkillBackground");
        Transform fallbackRoot = FindDescendantByPath(skillRoot, fallbackPath);
        return fallbackRoot != null ? fallbackRoot.GetComponentInChildren<ProgressBarUI>(true) : null;
    }

    private static ProgressBarUI ReplaceBarWithPrefab(ProgressBarUI current, GameObject prefab, string objectName)
    {
        if (current == null)
            return null;

        GameObject currentRoot = current.gameObject;
        if (IsProgressBarPrefabInstance(currentRoot))
            return current;

        RectTransform currentRect = currentRoot.GetComponent<RectTransform>();
        if (currentRect == null)
            return current;

        Transform parent = currentRect.parent;
        int siblingIndex = currentRect.GetSiblingIndex();
        bool wasActive = currentRoot.activeSelf;

        GameObject replacement = (GameObject)PrefabUtility.InstantiatePrefab(prefab, currentRoot.scene);
        replacement.name = objectName;
        replacement.SetActive(wasActive);

        RectTransform replacementRect = replacement.GetComponent<RectTransform>();
        replacementRect.SetParent(parent, false);
        replacementRect.SetSiblingIndex(siblingIndex);
        CopyRectTransform(currentRect, replacementRect);

        ProgressBarUI replacementBar = replacement.GetComponent<ProgressBarUI>();
        CopyBarSerializedState(current, replacementBar);
        CopyImages(current, replacementBar);

        Object.DestroyImmediate(currentRoot);
        return replacementBar;
    }

    private static bool IsProgressBarPrefabInstance(GameObject gameObject)
    {
        GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(gameObject);
        if (instanceRoot != gameObject)
            return false;

        Object source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(gameObject);
        return source != null && AssetDatabase.GetAssetPath(source) == ProgressBarPrefabPath;
    }

    private static void CopyRectTransform(RectTransform source, RectTransform destination)
    {
        destination.anchorMin = source.anchorMin;
        destination.anchorMax = source.anchorMax;
        destination.anchoredPosition = source.anchoredPosition;
        destination.sizeDelta = source.sizeDelta;
        destination.pivot = source.pivot;
        destination.localRotation = source.localRotation;
        destination.localScale = source.localScale;
        destination.localPosition = source.localPosition;
        destination.localEulerAngles = source.localEulerAngles;
    }

    private static void CopyBarSerializedState(ProgressBarUI source, ProgressBarUI destination)
    {
        var sourceSo = new SerializedObject(source);
        var destinationSo = new SerializedObject(destination);

        CopyProperty(sourceSo, destinationSo, "fillDirection");
        CopyProperty(sourceSo, destinationSo, "progressColor");
        CopyProperty(sourceSo, destinationSo, "value");
        CopyProperty(sourceSo, destinationSo, "hideFillWhenEmpty");

        destinationSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CopyProperty(SerializedObject source, SerializedObject destination, string propertyName)
    {
        SerializedProperty sourceProperty = source.FindProperty(propertyName);
        SerializedProperty destinationProperty = destination.FindProperty(propertyName);
        if (sourceProperty == null || destinationProperty == null)
            return;

        switch (sourceProperty.propertyType)
        {
            case SerializedPropertyType.Boolean:
                destinationProperty.boolValue = sourceProperty.boolValue;
                break;
            case SerializedPropertyType.Enum:
                destinationProperty.enumValueIndex = sourceProperty.enumValueIndex;
                break;
            case SerializedPropertyType.Float:
                destinationProperty.floatValue = sourceProperty.floatValue;
                break;
            case SerializedPropertyType.ObjectReference:
                destinationProperty.objectReferenceValue = sourceProperty.objectReferenceValue;
                break;
        }
    }

    private static void CopyImages(ProgressBarUI source, ProgressBarUI destination)
    {
        Image sourceBackground = source.GetComponent<Image>();
        Image destinationBackground = destination.GetComponent<Image>();
        CopyImage(sourceBackground, destinationBackground);

        Image sourceFill = GetSerializedImage(source, "fillImage");
        Image destinationFill = GetSerializedImage(destination, "fillImage");
        CopyImage(sourceFill, destinationFill);
    }

    private static Image GetSerializedImage(ProgressBarUI bar, string propertyName)
    {
        var serializedObject = new SerializedObject(bar);
        return serializedObject.FindProperty(propertyName)?.objectReferenceValue as Image;
    }

    private static void CopyImage(Image source, Image destination)
    {
        if (source == null || destination == null)
            return;

        destination.sprite = source.sprite;
        destination.type = source.type;
        destination.color = source.color;
        destination.preserveAspect = source.preserveAspect;
        destination.fillCenter = source.fillCenter;
        destination.raycastTarget = false;
    }

    private static bool SetReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue == value)
            return false;

        property.objectReferenceValue = value;
        return true;
    }

    private static Transform FindDescendantByPath(Transform root, string path)
    {
        if (root == null || string.IsNullOrWhiteSpace(path))
            return null;

        string[] parts = path.Split('/');
        Transform current = root;
        for (int i = 0; i < parts.Length; i++)
        {
            if (current == null)
                return null;

            current = current.Find(parts[i]);
        }

        return current;
    }

    private static Transform FindDeepChildByName(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, objectName, System.StringComparison.OrdinalIgnoreCase))
                return child;

            Transform nested = FindDeepChildByName(child, objectName);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
#endif
