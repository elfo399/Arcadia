#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneBundlePrefabCreator
{
    private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
    private const string HubScenePath = "Assets/_Project/Scenes/HubScene.unity";
    private const string BundlePrefabPath = "Assets/_Project/Prefabs/SceneBundles/GameSceneBundle.prefab";
    private const string AutoRunHubRequestRelativePath = "Temp/ArcadiaCreateHubScene.request";

    [MenuItem("Tools/Arcadia/Scene Bundles/Create GameScene Bundle Prefab")]
    public static void CreateGameSceneBundlePrefab()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        CreateGameSceneBundlePrefabInternal(true);
    }

    [MenuItem("Tools/Arcadia/Scene Bundles/Create Hub Scene From GameScene")]
    public static void CreateHubSceneFromGameSceneBundle()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        CreateHubSceneFromGameSceneBundleInternal();
    }

    [InitializeOnLoadMethod]
    private static void RunHubCreationRequest()
    {
        string requestPath = GetProjectPath(AutoRunHubRequestRelativePath);
        if (!File.Exists(requestPath))
            return;

        File.Delete(requestPath);
        EditorApplication.delayCall += CreateHubSceneFromGameSceneBundle;
    }

    private static bool CreateGameSceneBundlePrefabInternal(bool restorePreviousScene)
    {
        EnsureFolder(Path.GetDirectoryName(BundlePrefabPath)?.Replace("\\", "/"));

        Scene previousScene = SceneManager.GetActiveScene();
        Scene sourceScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

        GameObject bundleRoot = new GameObject("GameSceneBundle");
        List<GameObject> sourceRoots = CollectBundleRoots(sourceScene);

        if (sourceRoots.Count == 0)
        {
            Object.DestroyImmediate(bundleRoot);
            Debug.LogWarning("[SceneBundlePrefabCreator] No roots copied; bundle prefab not created.");
            return false;
        }

        List<GameObject> copiedRoots = DuplicateRootsTogether(sourceRoots);
        foreach (GameObject copy in copiedRoots)
            copy.transform.SetParent(bundleRoot.transform, true);

        PrefabUtility.SaveAsPrefabAsset(bundleRoot, BundlePrefabPath);
        Object.DestroyImmediate(bundleRoot);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (restorePreviousScene && previousScene.IsValid() && !string.IsNullOrWhiteSpace(previousScene.path) && previousScene.path != sourceScene.path)
            EditorSceneManager.OpenScene(previousScene.path, OpenSceneMode.Single);

        Debug.Log($"[SceneBundlePrefabCreator] Saved bundle prefab: {BundlePrefabPath}");
        return true;
    }

    private static void CreateHubSceneFromGameSceneBundleInternal()
    {
        if (!CreateGameSceneBundlePrefabInternal(false))
            return;

        EnsureFolder(Path.GetDirectoryName(HubScenePath)?.Replace("\\", "/"));

        Scene hubScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject bundlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BundlePrefabPath);
        if (bundlePrefab == null)
        {
            Debug.LogError($"[SceneBundlePrefabCreator] Bundle prefab not found: {BundlePrefabPath}");
            return;
        }

        GameObject bundleInstance = (GameObject)PrefabUtility.InstantiatePrefab(bundlePrefab, hubScene);
        bundleInstance.name = "GameSceneBundle";

        CreateHubFloor();
        CreateDungeonPortal();
        AddSceneToBuildSettings(HubScenePath);
        AddSceneToBuildSettings(GameScenePath);

        EditorSceneManager.SaveScene(hubScene, HubScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[SceneBundlePrefabCreator] Saved hub scene: {HubScenePath}");
    }

    private static void CreateHubFloor()
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "HubFloor";
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(6f, 1f, 6f);
        GameObjectUtility.SetStaticEditorFlags(floor, StaticEditorFlags.BatchingStatic);
    }

    private static void CreateDungeonPortal()
    {
        GameObject portal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        portal.name = "DungeonPortal";
        portal.transform.position = new Vector3(0f, 0.05f, 5f);
        portal.transform.localScale = new Vector3(1.5f, 0.1f, 1.5f);

        Collider portalCollider = portal.GetComponent<Collider>();
        if (portalCollider != null)
            portalCollider.isTrigger = true;

        SceneLoader loader = portal.AddComponent<SceneLoader>();
        loader.sceneToLoad = "GameScene";
    }

    private static List<GameObject> CollectBundleRoots(Scene scene)
    {
        List<GameObject> roots = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "GameSceneBundle")
                continue;

            roots.Add(root);
        }

        return roots;
    }

    private static List<GameObject> DuplicateRootsTogether(List<GameObject> sourceRoots)
    {
        Object[] previousSelection = Selection.objects;

        Selection.objects = sourceRoots.ToArray();
        Unsupported.DuplicateGameObjectsUsingPasteboard();

        List<GameObject> copies = new List<GameObject>(Selection.gameObjects);
        foreach (GameObject copy in copies)
            copy.name = Regex.Replace(copy.name.Replace(" Copy", string.Empty), @" \(\d+\)$", string.Empty);

        Selection.objects = previousSelection;
        return copies;
    }

    private static void EnsureFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string folder = Path.GetFileName(path);

        if (!string.IsNullOrWhiteSpace(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, folder);
    }

    private static void AddSceneToBuildSettings(string scenePath)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (EditorBuildSettingsScene scene in scenes)
        {
            if (scene.path == scenePath)
                return;
        }

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static string GetProjectPath(string relativePath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        return string.IsNullOrWhiteSpace(projectRoot)
            ? relativePath
            : Path.Combine(projectRoot, relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
    }
}
#endif
