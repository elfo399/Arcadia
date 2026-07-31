#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CleanHubSceneBuilder
{
    private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
    private const string HubScenePath = "Assets/_Project/Scenes/HubScene.unity";
    private const string AutoRunRequestRelativePath = "Temp/ArcadiaRebuildCleanHub.request";

    private static readonly string[] SourceRoots =
    {
        "__SYSTEM",
        "__ENTITIES",
        "__UI",
        "EventSystem",
        "Directional Light"
    };

    [MenuItem("Tools/Arcadia/Hub/Rebuild Clean Hub Scene")]
    public static void RebuildCleanHubScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        RebuildCleanHubSceneInternal();
    }

    [InitializeOnLoadMethod]
    private static void RunRequestedBuild()
    {
        string requestPath = GetProjectPath(AutoRunRequestRelativePath);
        if (!File.Exists(requestPath))
            return;

        File.Delete(requestPath);
        EditorApplication.delayCall += RebuildCleanHubScene;
    }

    private static void RebuildCleanHubSceneInternal()
    {
        EnsureFolder(Path.GetDirectoryName(HubScenePath)?.Replace("\\", "/"));

        Scene sourceScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        List<GameObject> sourceRoots = FindSourceRoots(sourceScene);
        if (sourceRoots.Count == 0)
        {
            Debug.LogError($"[CleanHubSceneBuilder] No source roots found in {GameScenePath}.");
            return;
        }

        List<GameObject> copiedRoots = DuplicateRootsTogether(sourceRoots);
        Scene hubScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SetActiveScene(hubScene);

        foreach (GameObject root in copiedRoots)
            SceneManager.MoveGameObjectToScene(root, hubScene);

        CleanHubCopies(copiedRoots);
        CreateHubFloor(hubScene);
        GameObject dungeonPortal = CreateDungeonPortal(hubScene);
        CreateHubMapZone(hubScene, dungeonPortal != null ? dungeonPortal.transform : null);
        AddSceneToBuildSettings(HubScenePath);
        AddSceneToBuildSettings(GameScenePath);

        EditorSceneManager.SaveScene(hubScene, HubScenePath);
        EditorSceneManager.CloseScene(sourceScene, true);
        EditorSceneManager.OpenScene(HubScenePath, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[CleanHubSceneBuilder] Rebuilt clean hub scene: {HubScenePath}");
    }

    private static List<GameObject> FindSourceRoots(Scene scene)
    {
        List<GameObject> roots = new List<GameObject>();
        foreach (string rootName in SourceRoots)
        {
            GameObject root = FindRootObject(scene, rootName);
            if (root == null)
            {
                Debug.LogWarning($"[CleanHubSceneBuilder] Missing root '{rootName}' in {GameScenePath}.");
                continue;
            }

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
            copy.name = CleanDuplicateName(copy.name);

        Selection.objects = previousSelection;
        return copies;
    }

    private static void CleanHubCopies(List<GameObject> roots)
    {
        GameObject systemRoot = FindObjectByName(roots, "__SYSTEM");
        if (systemRoot != null)
        {
            DestroyChildIfPresent(systemRoot.transform, "GeneratorManager");
            DestroyChildIfPresent(systemRoot.transform, "MinimapManager");
            systemRoot.transform.position = Vector3.zero;
        }

        GameObject entitiesRoot = FindObjectByName(roots, "__ENTITIES");
        if (entitiesRoot != null)
            MovePlayerToHubSpawn(entitiesRoot.transform);

        GameObject uiRoot = FindObjectByName(roots, "__UI");
        if (uiRoot != null)
            uiRoot.SetActive(true);

        GameObject hudCanvas = FindDescendant(roots, "HUD_Canvas");
        if (hudCanvas != null)
            hudCanvas.SetActive(true);

        GameObject hudInventory = FindDescendant(roots, "HUD_Inventory");
        if (hudInventory != null)
            hudInventory.SetActive(false);
    }

    private static void MovePlayerToHubSpawn(Transform entitiesRoot)
    {
        Transform player = FindChildRecursive(entitiesRoot, "Player");
        if (player == null)
            return;

        Vector3 spawnPosition = new Vector3(0f, 0.1f, 0f);
        Vector3 delta = spawnPosition - player.position;
        entitiesRoot.position += delta;
    }

    private static void CreateHubFloor(Scene scene)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "HubFloor";
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(8f, 1f, 8f);
        SceneManager.MoveGameObjectToScene(floor, scene);
        GameObjectUtility.SetStaticEditorFlags(floor, StaticEditorFlags.BatchingStatic);
    }

    private static GameObject CreateDungeonPortal(Scene scene)
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
        SceneManager.MoveGameObjectToScene(portal, scene);
        return portal;
    }

    private static void CreateHubMapZone(Scene scene, Transform portalTarget)
    {
        GameObject mapZoneObject = new GameObject("HubMapZone");
        HubMapZone mapZone = mapZoneObject.AddComponent<HubMapZone>();
        mapZone.Configure(Vector2.zero, new Vector2(80f, 80f), portalTarget);
        SceneManager.MoveGameObjectToScene(mapZoneObject, scene);
    }

    private static GameObject FindRootObject(Scene scene, string rootName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == rootName)
                return root;
        }

        return null;
    }

    private static GameObject FindObjectByName(List<GameObject> roots, string objectName)
    {
        foreach (GameObject root in roots)
        {
            if (root != null && root.name == objectName)
                return root;
        }

        return null;
    }

    private static GameObject FindDescendant(List<GameObject> roots, string objectName)
    {
        foreach (GameObject root in roots)
        {
            Transform child = FindChildRecursive(root.transform, objectName);
            if (child != null)
                return child.gameObject;
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent.name == childName)
            return parent;

        foreach (Transform child in parent)
        {
            Transform match = FindChildRecursive(child, childName);
            if (match != null)
                return match;
        }

        return null;
    }

    private static void DestroyChildIfPresent(Transform parent, string childName)
    {
        Transform child = FindChildRecursive(parent, childName);
        if (child != null)
            Object.DestroyImmediate(child.gameObject);
    }

    private static string CleanDuplicateName(string name)
    {
        return Regex.Replace(name.Replace(" Copy", string.Empty), @" \(\d+\)$", string.Empty);
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
