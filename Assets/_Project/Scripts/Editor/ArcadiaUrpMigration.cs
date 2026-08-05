using System.Collections.Generic;
using UnityEditor;
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
}
