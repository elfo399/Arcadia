using UnityEngine;

public class FpsDisplay : MonoBehaviour
{
    private const float UpdateInterval = 0.25f;

    private float elapsedTime;
    private int frameCount;
    private int currentFps;
    private GUIStyle labelStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (FindObjectOfType<FpsDisplay>() != null)
            return;

        var display = new GameObject("FPS Display");
        DontDestroyOnLoad(display);
        display.AddComponent<FpsDisplay>();
    }

    private void Update()
    {
        elapsedTime += Time.unscaledDeltaTime;
        frameCount++;

        if (elapsedTime < UpdateInterval)
            return;

        currentFps = Mathf.RoundToInt(frameCount / elapsedTime);
        elapsedTime = 0f;
        frameCount = 0;
    }

    private void OnGUI()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }

        GUI.Label(new Rect(12f, 8f, 180f, 40f), $"FPS: {currentFps}", labelStyle);
    }
}
