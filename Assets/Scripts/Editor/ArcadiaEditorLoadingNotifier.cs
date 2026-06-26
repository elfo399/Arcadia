#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Compilation;
using UnityEngine;

[InitializeOnLoad]
public static class ArcadiaEditorLoadingNotifier
{
    private const string ProgressTitle = "Arcadia";
    private const string CompilingMessage = "Compilazione script in corso...";

    private static bool isCompiling;
    private static double compilationStartTime;

    static ArcadiaEditorLoadingNotifier()
    {
        CompilationPipeline.compilationStarted -= OnCompilationStarted;
        CompilationPipeline.compilationStarted += OnCompilationStarted;
        CompilationPipeline.compilationFinished -= OnCompilationFinished;
        CompilationPipeline.compilationFinished += OnCompilationFinished;
        AssemblyReloadEvents.beforeAssemblyReload -= ClearProgress;
        AssemblyReloadEvents.beforeAssemblyReload += ClearProgress;
        EditorApplication.update -= UpdateProgress;
        EditorApplication.update += UpdateProgress;
    }

    private static void OnCompilationStarted(object context)
    {
        isCompiling = true;
        compilationStartTime = EditorApplication.timeSinceStartup;
        EditorUtility.DisplayProgressBar(ProgressTitle, CompilingMessage, 0.2f);
        ShowNotification(CompilingMessage);
    }

    private static void OnCompilationFinished(object context)
    {
        isCompiling = false;
        ClearProgress();
        ShowNotification("Script compilati.");
    }

    private static void UpdateProgress()
    {
        if (!isCompiling)
            return;

        float pulse = Mathf.PingPong((float)(EditorApplication.timeSinceStartup - compilationStartTime) * 0.35f, 0.6f);
        EditorUtility.DisplayProgressBar(ProgressTitle, CompilingMessage, 0.2f + pulse);
    }

    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        isCompiling = false;
        ClearProgress();
        ShowNotification("Script aggiornati.");
    }

    private static void ClearProgress()
    {
        EditorUtility.ClearProgressBar();
    }

    private static void ShowNotification(string message)
    {
        GUIContent content = new GUIContent(message);
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.ShowNotification(content, 2d);
            return;
        }

        EditorWindow focusedWindow = EditorWindow.focusedWindow;
        if (focusedWindow != null)
            focusedWindow.ShowNotification(content, 2d);
    }
}
#endif
