using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class ThirdPartyColliderBatchTool
{
    private const string ThirdPartyRoot = "Assets/_ThirdParty";
    private const string UpgradeVersion = "2026-09-10-v1";
    private const string SessionScheduledKey = "Arcadia.ThirdPartyColliders.Scheduled";

    static ThirdPartyColliderBatchTool()
    {
        ScheduleAutomaticUpgrade();
    }

    [MenuItem("Tools/Arcadia/Third Party/Add Missing Colliders")]
    private static void RunFromMenu()
    {
        if (!EditorUtility.DisplayDialog(
                "Third Party Colliders",
                "Aggiungere i collider mancanti a tutti i modelli e prefab sotto Assets/_ThirdParty?",
                "Aggiungi",
                "Annulla"))
        {
            return;
        }

        RunUpgrade(true);
    }

    private static void ScheduleAutomaticUpgrade()
    {
        if (EditorPrefs.GetString(GetProjectUpgradeKey(), string.Empty) == UpgradeVersion ||
            SessionState.GetBool(SessionScheduledKey, false))
        {
            return;
        }

        SessionState.SetBool(SessionScheduledKey, true);
        EditorApplication.delayCall += RunScheduledUpgrade;
    }

    private static void RunScheduledUpgrade()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RunScheduledUpgrade;
            return;
        }

        RunUpgrade(false);
    }

    private static void RunUpgrade(bool force)
    {
        string upgradeKey = GetProjectUpgradeKey();
        if (!force && EditorPrefs.GetString(upgradeKey, string.Empty) == UpgradeVersion)
        {
            return;
        }

        int updatedImporters = 0;
        int updatedPrefabs = 0;
        int addedColliders = 0;
        var failures = new List<string>();

        try
        {
            updatedImporters = EnableModelImporterColliders(failures);
            UpdatePrefabColliders(ref updatedPrefabs, ref addedColliders, failures);
            AssetDatabase.SaveAssets();

            if (failures.Count == 0)
            {
                EditorPrefs.SetString(upgradeKey, UpgradeVersion);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        string result = $"[ThirdPartyColliderBatchTool] Completato. " +
                        $"Importer aggiornati: {updatedImporters}, prefab aggiornati: {updatedPrefabs}, " +
                        $"collider aggiunti: {addedColliders}.";

        if (failures.Count == 0)
        {
            Debug.Log(result);
        }
        else
        {
            Debug.LogWarning(result + " Errori:\n" + string.Join("\n", failures));
        }
    }

    private static int EnableModelImporterColliders(List<string> failures)
    {
        string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { ThirdPartyRoot });
        int updated = 0;

        for (int index = 0; index < modelGuids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(modelGuids[index]);
            EditorUtility.DisplayProgressBar(
                "Third Party Colliders",
                $"Importer modello {index + 1}/{modelGuids.Length}",
                modelGuids.Length == 0 ? 0f : (float)index / modelGuids.Length * 0.35f);

            try
            {
                if (AssetImporter.GetAtPath(path) is ModelImporter importer && !importer.addCollider)
                {
                    importer.addCollider = true;
                    importer.SaveAndReimport();
                    updated++;
                }
            }
            catch (Exception exception)
            {
                failures.Add($"Importer '{path}': {exception.Message}");
            }
        }

        return updated;
    }

    private static void UpdatePrefabColliders(
        ref int updatedPrefabs,
        ref int addedColliders,
        List<string> failures)
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { ThirdPartyRoot });

        for (int index = 0; index < prefabGuids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[index]);
            if (!string.Equals(Path.GetExtension(path), ".prefab", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            EditorUtility.DisplayProgressBar(
                "Third Party Colliders",
                $"Prefab {index + 1}/{prefabGuids.Length}",
                0.35f + (prefabGuids.Length == 0 ? 0f : (float)index / prefabGuids.Length * 0.65f));

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                int addedToPrefab = AddMissingColliders(root);
                if (addedToPrefab == 0)
                {
                    continue;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                updatedPrefabs++;
                addedColliders += addedToPrefab;
            }
            catch (Exception exception)
            {
                failures.Add($"Prefab '{path}': {exception.Message}");
            }
            finally
            {
                if (root != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }
    }

    private static int AddMissingColliders(GameObject root)
    {
        int added = 0;

        foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (meshFilter.sharedMesh == null || IsNestedPrefabContent(meshFilter.gameObject) ||
                HasSolidColliderInParents(meshFilter.transform, root.transform))
            {
                continue;
            }

            Rigidbody rigidbody = meshFilter.GetComponentInParent<Rigidbody>();
            if (rigidbody != null && !rigidbody.isKinematic)
            {
                BoxCollider box = meshFilter.gameObject.AddComponent<BoxCollider>();
                box.center = meshFilter.sharedMesh.bounds.center;
                box.size = ClampSize(meshFilter.sharedMesh.bounds.size);
            }
            else
            {
                MeshCollider meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = meshFilter.sharedMesh;
                meshCollider.convex = false;
            }

            added++;
        }

        foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer.sharedMesh == null || IsNestedPrefabContent(renderer.gameObject) ||
                HasSolidColliderInParents(renderer.transform, root.transform))
            {
                continue;
            }

            Bounds bounds = renderer.localBounds;
            CapsuleCollider capsule = renderer.gameObject.AddComponent<CapsuleCollider>();
            capsule.center = bounds.center;
            capsule.direction = 1;
            capsule.height = Mathf.Max(0.02f, bounds.size.y);
            capsule.radius = Mathf.Max(0.01f, Mathf.Min(capsule.height * 0.5f,
                Mathf.Max(bounds.extents.x, bounds.extents.z)));
            added++;
        }

        return added;
    }

    private static bool IsNestedPrefabContent(GameObject gameObject)
    {
        return PrefabUtility.IsPartOfPrefabInstance(gameObject);
    }

    private static bool HasSolidColliderInParents(Transform current, Transform prefabRoot)
    {
        while (current != null)
        {
            foreach (Collider collider in current.GetComponents<Collider>())
            {
                if (collider.enabled && !collider.isTrigger)
                {
                    return true;
                }
            }

            if (current == prefabRoot)
            {
                break;
            }

            current = current.parent;
        }

        return false;
    }

    private static Vector3 ClampSize(Vector3 size)
    {
        const float minimum = 0.02f;
        return new Vector3(
            Mathf.Max(minimum, size.x),
            Mathf.Max(minimum, size.y),
            Mathf.Max(minimum, size.z));
    }

    private static string GetProjectUpgradeKey()
    {
        return $"Arcadia.ThirdPartyColliders.{Hash128.Compute(Application.dataPath)}";
    }
}
