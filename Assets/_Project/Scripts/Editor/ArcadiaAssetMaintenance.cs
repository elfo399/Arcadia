using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Small, explicit repair actions for legacy imported project assets.</summary>
public static class ArcadiaAssetMaintenance
{
    private const string HubTerrainPath = "Assets/_Project/Art/Terrain/HubTerrain.asset";
    private static readonly string[] ReportedShaderGraphPaths =
    {
        "Assets/_ThirdParty/Texture/TerrainSampleAssets/ShaderGraphs/Subgraphs/URPSSS.shadersubgraph",
        "Assets/_ThirdParty/Texture/TerrainSampleAssets/ShaderGraphs/Subgraphs/Wind.shadersubgraph",
        "Assets/_ThirdParty/Texture/TerrainSampleAssets/ShaderGraphs/TerrainGrass.shadergraph"
    };

    [MenuItem("Arcadia/Maintenance/Repair Reported Legacy Assets")]
    public static void RepairReportedLegacyAssets()
    {
        NormalizeHubTerrainName();
        int repairedScenes = DetachLegacyDemoLightingData();
        AssetDatabase.ImportAsset(HubTerrainPath, ImportAssetOptions.ForceUpdate);
        foreach (string graphPath in ReportedShaderGraphPaths)
            AssetDatabase.ImportAsset(graphPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
        Debug.Log($"[ArcadiaAssetMaintenance] Hub terrain normalized; detached obsolete baked lighting from {repairedScenes} third-party demo scene(s).");
    }

    public static void NormalizeHubTerrainName()
    {
        TerrainData terrain = AssetDatabase.LoadAssetAtPath<TerrainData>(HubTerrainPath);
        if (terrain == null)
            throw new InvalidOperationException("HubTerrain.asset could not be loaded as TerrainData.");

        if (terrain.name == "HubTerrain")
            return;

        terrain.name = "HubTerrain";
        EditorUtility.SetDirty(terrain);
    }

    private static int DetachLegacyDemoLightingData()
    {
        int repaired = 0;
        string[] searchRoots = { "Assets/Hovl Studio", "Assets/_ThirdParty" };
        foreach (string sceneGuid in AssetDatabase.FindAssets("t:Scene", searchRoots))
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
            if (!IsThirdPartyDemoScene(scenePath))
                continue;

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (Lightmapping.lightingDataAsset == null)
                continue;

            Lightmapping.lightingDataAsset = null;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            repaired++;
        }

        return repaired;
    }

    private static bool IsThirdPartyDemoScene(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath) || scenePath.IndexOf("Assets/_Project/", StringComparison.OrdinalIgnoreCase) == 0)
            return false;

        string fileName = Path.GetFileNameWithoutExtension(scenePath);
        return scenePath.IndexOf("/Demo", StringComparison.OrdinalIgnoreCase) >= 0
            || fileName.IndexOf("Sample", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
