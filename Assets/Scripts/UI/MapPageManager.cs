using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapPageManager : MonoBehaviour
{
    [SerializeField] private CoreGenerator generator;
    [SerializeField] private TextMeshProUGUI floorText;
    [SerializeField] private TextMeshProUGUI themeText;
    [SerializeField] private RectTransform mapContainer;
    [SerializeField] private string floorFormat = "Floor {0}";
    [SerializeField] private string themeFormat = "{0}";
    [SerializeField] private string missingThemeLabel = "-";
    [SerializeField] private float mapPadding = 24f;
    [SerializeField] private float mapMaxScale = 0.85f;
    [SerializeField] private bool showFullMapForTesting = true;
    [SerializeField] private bool matchBookPageRoomBackground = true;
    [SerializeField] private Color bookPageRoomBackgroundColor = new Color(0.93f, 0.70f, 0.48f, 1f);

    private CoreGenerator subscribedGenerator;
    private MinimapManager subscribedMinimap;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToGenerator();
        SubscribeToMinimap();
        Refresh();
    }

    private void Start()
    {
        ResolveReferences();
        SubscribeToGenerator();
        SubscribeToMinimap();
        Refresh();
    }

    private void OnDisable()
    {
        UnsubscribeFromGenerator();
        UnsubscribeFromMinimap();
    }

    private void OnDestroy()
    {
        UnsubscribeFromGenerator();
        UnsubscribeFromMinimap();
    }

    public void Refresh()
    {
        ResolveReferences();

        if (generator == null)
        {
            ApplyTexts(0, string.Empty);
            RefreshMap();
            return;
        }

        ApplyTexts(generator.CurrentFloor, generator.ActiveThemeDisplayName);
        RefreshMap();
    }

    private void HandleFloorThemeChanged(int floor, string themeName)
    {
        ApplyTexts(floor, themeName);
    }

    private void HandleMapStateChanged()
    {
        RefreshMap();
    }

    private void RefreshMap()
    {
        if (MinimapManager.instance == null || mapContainer == null)
            return;

        MinimapManager.instance.RenderExploredMap(
            mapContainer,
            mapPadding,
            mapMaxScale,
            showFullMapForTesting,
            matchBookPageRoomBackground,
            bookPageRoomBackgroundColor);
    }

    private void ApplyTexts(int floor, string themeName)
    {
        if (floorText != null)
            floorText.text = floor > 0 ? FormatText(floorFormat, floor) : string.Empty;

        if (themeText != null)
        {
            string resolvedTheme = string.IsNullOrWhiteSpace(themeName) ? missingThemeLabel : themeName;
            themeText.text = FormatText(themeFormat, resolvedTheme);
        }
    }

    private void ResolveReferences()
    {
        if (generator == null)
            generator = CoreGenerator.Instance != null ? CoreGenerator.Instance : FindObjectOfType<CoreGenerator>();

        if (floorText == null)
            floorText = FindTextByObjectName("Floor");

        if (themeText == null)
            themeText = FindTextByObjectName("Theme");

        if (mapContainer == null)
            mapContainer = FindMenuMapContainer();
    }

    private void SubscribeToGenerator()
    {
        if (subscribedGenerator == generator)
            return;

        UnsubscribeFromGenerator();

        if (generator == null)
            return;

        generator.FloorThemeChanged += HandleFloorThemeChanged;
        subscribedGenerator = generator;
    }

    private void UnsubscribeFromGenerator()
    {
        if (subscribedGenerator == null)
            return;

        subscribedGenerator.FloorThemeChanged -= HandleFloorThemeChanged;
        subscribedGenerator = null;
    }

    private void SubscribeToMinimap()
    {
        MinimapManager minimap = MinimapManager.instance;
        if (subscribedMinimap == minimap)
            return;

        UnsubscribeFromMinimap();

        if (minimap == null)
            return;

        minimap.MapStateChanged += HandleMapStateChanged;
        subscribedMinimap = minimap;
    }

    private void UnsubscribeFromMinimap()
    {
        if (subscribedMinimap == null)
            return;

        subscribedMinimap.MapStateChanged -= HandleMapStateChanged;
        subscribedMinimap = null;
    }

    private static TextMeshProUGUI FindTextByObjectName(string objectName)
    {
        TextMeshProUGUI[] texts = FindObjectsOfType<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].name == objectName)
                return texts[i];
        }

        return null;
    }

    private RectTransform FindMenuMapContainer()
    {
        Transform[] transforms = FindObjectsOfType<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.name != "Map")
                continue;

            if (candidate.GetComponent<Button>() != null)
                continue;

            Transform parent = candidate.parent;
            while (parent != null)
            {
                if (parent.name == "MapPage")
                    return candidate as RectTransform;

                parent = parent.parent;
            }
        }

        return null;
    }

    private static string FormatText(string format, object value)
    {
        if (string.IsNullOrEmpty(format))
            return value?.ToString() ?? string.Empty;

        try
        {
            return string.Format(format, value);
        }
        catch (FormatException)
        {
            return value?.ToString() ?? string.Empty;
        }
    }
}
