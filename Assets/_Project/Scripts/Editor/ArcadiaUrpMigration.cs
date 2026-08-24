using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// One-click Built-in Render Pipeline to URP migration for Arcadia.
/// The operation is intentionally idempotent so it is safe to keep as a project utility.
/// </summary>
public static class ArcadiaUrpMigration
{
    private const string RepairMarkerPath = "Library/ArcadiaRepairPinkMaterials.request";
    private const string BackupRoot = "Library/ArcadiaMaterialBackupBeforeUrpRepair";
    private const string ReportPath = "Logs/ArcadiaUrpMaterialRepair.txt";

    [InitializeOnLoadMethod]
    private static void RunRequestedRepair()
    {
        if (File.Exists(RepairMarkerPath))
            EditorApplication.delayCall += TryRunRequestedRepair;
    }

    private static void TryRunRequestedRepair()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryRunRequestedRepair;
            return;
        }

        string request = File.ReadAllText(RepairMarkerPath).Trim();
        File.Delete(RepairMarkerPath);
        if (string.Equals(request, "restore-shadergraphs", StringComparison.OrdinalIgnoreCase))
            RestoreShaderGraphMaterials();
        else
            RepairPinkMaterials();
    }

    [MenuItem("Arcadia/Rendering/Migrate Project To URP")]
    public static void Migrate()
    {
        if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset)
        {
            Debug.Log("Arcadia URP migration skipped: URP is already the default render pipeline.");
            Validate();
            return;
        }

        var converters = new List<ConverterId>
        {
            ConverterId.RenderSettings,
            ConverterId.Material,
            ConverterId.AnimationClip,
            ConverterId.PPv2
        };

        Converters.RunInBatchMode(
            ConverterContainerId.BuiltInToURP,
            converters,
            ConverterFilter.Inclusive);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Validate();
    }

    [MenuItem("Arcadia/Rendering/Validate URP Configuration")]
    public static void Validate()
    {
        var defaultAsset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if (defaultAsset == null)
            throw new System.InvalidOperationException("URP is not assigned in Graphics Settings.");

        for (var index = 0; index < QualitySettings.names.Length; index++)
        {
            var qualityAsset = QualitySettings.GetRenderPipelineAssetAt(index);
            if (!(qualityAsset is UniversalRenderPipelineAsset))
            {
                throw new System.InvalidOperationException(
                    $"Quality level '{QualitySettings.names[index]}' does not use a URP asset.");
            }
        }

        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (lit == null || !lit.isSupported || unlit == null || !unlit.isSupported)
            throw new System.InvalidOperationException("Required URP shaders are missing or unsupported.");

        Debug.Log(
            $"Arcadia URP validation passed. Default asset: '{defaultAsset.name}', " +
            $"quality levels: {QualitySettings.names.Length}.");
    }

    [MenuItem("Arcadia/Rendering/Repair Pink Materials")]
    public static void RepairPinkMaterials()
    {
        string[] materialPaths = AssetDatabase.FindAssets("t:Material", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !string.IsNullOrEmpty(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        BackupMaterials(materialPaths);
        List<MaterialUpgrader> upgraders = GetOfficialMaterialUpgraders();
        int officialConversions = 0;
        int fallbackConversions = 0;
        var changes = new List<string>();

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string path in materialPaths)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                    continue;

                string before = ShaderName(material);
                string message = string.Empty;
                MaterialUpgrader.Upgrade(material, upgraders, MaterialUpgrader.UpgradeFlags.None, ref message);
                string afterOfficial = ShaderName(material);
                if (!string.Equals(before, afterOfficial, StringComparison.Ordinal))
                {
                    officialConversions++;
                    changes.Add($"OFFICIAL | {before} -> {afterOfficial} | {path}");
                    EditorUtility.SetDirty(material);
                }

                if (NeedsFallback(material))
                {
                    string fallbackBefore = ShaderName(material);
                    if (ConvertToUrpLit(material, fallbackBefore))
                    {
                        fallbackConversions++;
                        changes.Add($"FALLBACK | {fallbackBefore} -> {ShaderName(material)} | {path}");
                        EditorUtility.SetDirty(material);
                    }
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        var remaining = materialPaths
            .Select(path => new { path, material = AssetDatabase.LoadAssetAtPath<Material>(path) })
            .Where(entry => entry.material != null && NeedsFallback(entry.material))
            .Select(entry => $"{ShaderName(entry.material)} | {entry.path}")
            .ToList();

        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        var report = new List<string>
        {
            $"Materials scanned: {materialPaths.Length}",
            $"Official URP conversions: {officialConversions}",
            $"Fallback URP conversions: {fallbackConversions}",
            $"Remaining incompatible materials: {remaining.Count}",
            string.Empty,
            "CHANGES"
        };
        report.AddRange(changes);
        report.Add(string.Empty);
        report.Add("REMAINING");
        report.AddRange(remaining);
        File.WriteAllLines(ReportPath, report);
        Debug.Log($"Arcadia material repair completed. Official: {officialConversions}, fallback: {fallbackConversions}, remaining: {remaining.Count}. Report: {ReportPath}");
    }

    [MenuItem("Arcadia/Rendering/Restore Specialized Shader Graph Materials")]
    public static void RestoreShaderGraphMaterials()
    {
        if (!File.Exists(ReportPath))
            throw new FileNotFoundException("The material repair report was not found.", ReportPath);

        int restored = 0;
        foreach (string line in File.ReadAllLines(ReportPath))
        {
            if (!line.StartsWith("FALLBACK | ", StringComparison.Ordinal))
                continue;

            string[] parts = line.Split(new[] { " | " }, StringSplitOptions.None);
            if (parts.Length != 3)
                continue;

            string originalShader = parts[1].Split(new[] { " -> " }, StringSplitOptions.None)[0];
            string assetPath = parts[2];
            string backupPath = Path.Combine(BackupRoot, assetPath).Replace('/', Path.DirectorySeparatorChar);
            bool specializedGraph = originalShader.StartsWith("Shader Graphs/", StringComparison.OrdinalIgnoreCase)
                || originalShader.StartsWith("LB Shader/", StringComparison.OrdinalIgnoreCase);
            if (!specializedGraph || !File.Exists(backupPath))
                continue;

            File.Copy(backupPath, assetPath, true);
            restored++;
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
        Debug.Log($"Arcadia specialized Shader Graph restore completed. Restored assets: {restored}.");
    }

    private static List<MaterialUpgrader> GetOfficialMaterialUpgraders()
    {
        var upgraders = new List<MaterialUpgrader>();
        Type type = Type.GetType("UnityEditor.Rendering.Universal.UniversalRenderPipelineMaterialUpgrader, Unity.RenderPipelines.Universal.Editor");
        MethodInfo method = type?.GetMethod("GetUpgraders", BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null)
            throw new InvalidOperationException("Unity URP material upgrader was not found.");

        object[] arguments = { upgraders };
        method.Invoke(null, arguments);
        return (List<MaterialUpgrader>)arguments[0];
    }

    private static bool NeedsFallback(Material material)
    {
        if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader" || !material.shader.isSupported)
            return true;

        string shaderName = material.shader.name;
        if (shaderName.StartsWith("Universal Render Pipeline/", StringComparison.OrdinalIgnoreCase) ||
            shaderName.StartsWith("TextMeshPro/", StringComparison.OrdinalIgnoreCase) ||
            shaderName.StartsWith("UI/", StringComparison.OrdinalIgnoreCase) ||
            shaderName.StartsWith("Sprites/", StringComparison.OrdinalIgnoreCase) ||
            shaderName.StartsWith("Skybox/", StringComparison.OrdinalIgnoreCase) ||
            shaderName.StartsWith("Hidden/", StringComparison.OrdinalIgnoreCase))
            return false;

        string shaderPath = AssetDatabase.GetAssetPath(material.shader);
        if (shaderPath.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase))
            return false;

        string pipelineTag = material.GetTag("RenderPipeline", false, string.Empty);
        return pipelineTag.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) < 0;
    }

    private static bool ConvertToUrpLit(Material material, string originalShaderName)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
            return false;

        Texture main = GetTexture(material, "_BaseMap", "_BaseTexture", "_MainTex", "_MainTexture", "_Diffuse", "_Albedo", "_Texture");
        Texture normal = GetTexture(material, "_BumpMap", "_NormalMap", "_Normal");
        Texture metallicMap = GetTexture(material, "_MetallicGlossMap", "_MetallicMap", "_SpecGlossMap");
        Texture emissionMap = GetTexture(material, "_EmissionMap", "_EmissiveMap");
        Color color = GetColor(material, Color.white, "_BaseColor", "_Color", "_TintColor");
        Color emissionColor = GetColor(material, Color.black, "_EmissionColor", "_EmissiveColor");
        float metallic = GetFloat(material, 0f, "_Metallic");
        float smoothness = GetFloat(material, 0.5f, "_Smoothness", "_Glossiness", "_Gloss");
        float cutoff = GetFloat(material, 0.5f, "_Cutoff", "_AlphaCutoff");

        string key = (originalShaderName + " " + material.name).ToLowerInvariant();
        bool transparent = key.Contains("transparent") || key.Contains("fade") || key.Contains("water") || color.a < 0.999f;
        bool cutout = key.Contains("cutout") || key.Contains("alpha test") || key.Contains("leaf") || key.Contains("foliage") || key.Contains("grass") || key.Contains("plant");
        bool doubleSided = cutout || key.Contains("vegetation") || key.Contains("double sided") || key.Contains("doublesided");

        var serializedMaterial = new SerializedObject(material);
        SerializedProperty parent = serializedMaterial.FindProperty("m_Parent");
        if (parent != null && parent.objectReferenceValue != null)
        {
            parent.objectReferenceValue = null;
            serializedMaterial.ApplyModifiedPropertiesWithoutUndo();
        }

        material.shader = lit;
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Cutoff", cutoff);
        material.SetFloat("_Cull", doubleSided ? 0f : 2f);
        if (main != null)
            material.SetTexture("_BaseMap", main);
        if (normal != null)
        {
            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
        }
        if (metallicMap != null)
        {
            material.SetTexture("_MetallicGlossMap", metallicMap);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        if (emissionMap != null || emissionColor.maxColorComponent > 0.001f)
        {
            material.SetTexture("_EmissionMap", emissionMap);
            material.SetColor("_EmissionColor", emissionColor);
            material.EnableKeyword("_EMISSION");
        }

        if (transparent)
        {
            material.SetFloat("_Surface", 1f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
        else if (cutout)
        {
            material.SetFloat("_AlphaClip", 1f);
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.AlphaTest;
        }
        else
        {
            material.SetFloat("_Surface", 0f);
            material.SetOverrideTag("RenderType", "Opaque");
            material.SetInt("_SrcBlend", (int)BlendMode.One);
            material.SetInt("_DstBlend", (int)BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            material.renderQueue = -1;
        }
        return true;
    }

    private static void BackupMaterials(IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            string destination = Path.Combine(BackupRoot, path).Replace('/', Path.DirectorySeparatorChar);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            if (!File.Exists(destination))
                File.Copy(path, destination);
        }
    }

    private static Texture GetTexture(Material material, params string[] names)
    {
        foreach (string name in names)
            if (material.HasProperty(name) && material.GetTexture(name) != null)
                return material.GetTexture(name);
        return null;
    }

    private static Color GetColor(Material material, Color fallback, params string[] names)
    {
        foreach (string name in names)
            if (material.HasProperty(name))
                return material.GetColor(name);
        return fallback;
    }

    private static float GetFloat(Material material, float fallback, params string[] names)
    {
        foreach (string name in names)
            if (material.HasProperty(name))
                return material.GetFloat(name);
        return fallback;
    }

    private static string ShaderName(Material material) => material.shader != null ? material.shader.name : "<missing>";
}
