using System;
using TMPro;
using UnityEngine;

public class MapPageManager : MonoBehaviour
{
    [SerializeField] private CoreGenerator generator;
    [SerializeField] private TextMeshProUGUI floorText;
    [SerializeField] private TextMeshProUGUI themeText;
    [SerializeField] private string floorFormat = "Floor {0}";
    [SerializeField] private string themeFormat = "{0}";
    [SerializeField] private string missingThemeLabel = "-";

    private CoreGenerator subscribedGenerator;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToGenerator();
        Refresh();
    }

    private void Start()
    {
        ResolveReferences();
        SubscribeToGenerator();
        Refresh();
    }

    private void OnDisable()
    {
        UnsubscribeFromGenerator();
    }

    private void OnDestroy()
    {
        UnsubscribeFromGenerator();
    }

    public void Refresh()
    {
        ResolveReferences();

        if (generator == null)
        {
            ApplyTexts(0, string.Empty);
            return;
        }

        ApplyTexts(generator.CurrentFloor, generator.ActiveThemeDisplayName);
    }

    private void HandleFloorThemeChanged(int floor, string themeName)
    {
        ApplyTexts(floor, themeName);
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
