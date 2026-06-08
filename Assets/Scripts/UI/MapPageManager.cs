using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapPageManager : MonoBehaviour
{
    [SerializeField] private CoreGenerator generator;
    [SerializeField] private WeatherManager weatherManager;
    [SerializeField] private TextMeshProUGUI floorText;
    [SerializeField] private TextMeshProUGUI themeText;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI weatherText;
    [SerializeField] private TextMeshProUGUI runTimerText;
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private RectTransform mapContainer;
    [SerializeField] private string floorFormat = "Floor {0}";
    [SerializeField] private string themeFormat = "{0}";
    [SerializeField] private string playerNameFormat = "{0}";
    [SerializeField] private string weatherFormat = "{0}";
    [SerializeField] private string runTimerFormat = "{0}";
    [SerializeField] private string missingThemeLabel = "-";
    [SerializeField] private string defaultPlayerName = "Player";
    [SerializeField] private string currentWeather = "Clear";
    [SerializeField] private float mapPadding = 24f;
    [SerializeField] private float mapMaxScale = 0.85f;
    [SerializeField] private bool showFullMapForTesting = true;
    [SerializeField] private bool matchBookPageRoomBackground = true;
    [SerializeField] private Color bookPageRoomBackgroundColor = new Color(0.93f, 0.70f, 0.48f, 1f);

    private CoreGenerator subscribedGenerator;
    private MinimapManager subscribedMinimap;
    private TMP_InputField subscribedPlayerNameInput;
    private string lastDisplayedPlayerName;
    private string lastDisplayedWeather;
    private int lastDisplayedRunSeconds = int.MinValue;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToGenerator();
        SubscribeToMinimap();
        SubscribeToPlayerNameInput();
        Refresh();
    }

    private void Start()
    {
        ResolveReferences();
        SubscribeToGenerator();
        SubscribeToMinimap();
        SubscribeToPlayerNameInput();
        Refresh();
    }

    private void Update()
    {
        ApplyRunInfoTexts();
    }

    private void OnDisable()
    {
        UnsubscribeFromGenerator();
        UnsubscribeFromMinimap();
        UnsubscribeFromPlayerNameInput();
    }

    private void OnDestroy()
    {
        UnsubscribeFromGenerator();
        UnsubscribeFromMinimap();
        UnsubscribeFromPlayerNameInput();
    }

    public void Refresh()
    {
        ResolveReferences();

        if (generator == null)
        {
            ApplyTexts(0, string.Empty);
            ApplyRunInfoTexts(forceRefresh: true);
            RefreshMap();
            return;
        }

        ApplyTexts(generator.CurrentFloor, generator.ActiveThemeDisplayName);
        ApplyRunInfoTexts(forceRefresh: true);
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

    private void ApplyRunInfoTexts(bool forceRefresh = false)
    {
        if (playerNameText != null)
        {
            string resolvedPlayerName = ResolvePlayerName();
            string formattedPlayerName = FormatText(playerNameFormat, resolvedPlayerName);
            if (forceRefresh || formattedPlayerName != lastDisplayedPlayerName)
            {
                playerNameText.text = formattedPlayerName;
                lastDisplayedPlayerName = formattedPlayerName;
            }
        }

        if (weatherText != null)
        {
            string resolvedWeather = ResolveCurrentWeather();
            string formattedWeather = FormatText(weatherFormat, resolvedWeather);
            if (forceRefresh || formattedWeather != lastDisplayedWeather)
            {
                weatherText.text = formattedWeather;
                lastDisplayedWeather = formattedWeather;
            }
        }

        if (runTimerText != null)
        {
            int elapsedSeconds = Mathf.FloorToInt(Mathf.Max(0f, Time.timeSinceLevelLoad));
            if (forceRefresh || elapsedSeconds != lastDisplayedRunSeconds)
            {
                runTimerText.text = FormatText(runTimerFormat, FormatElapsedTime(elapsedSeconds));
                lastDisplayedRunSeconds = elapsedSeconds;
            }
        }
    }

    public void SetPlayerName(string value)
    {
        defaultPlayerName = value;
        ApplyRunInfoTexts(forceRefresh: true);
    }

    public void SetCurrentWeather(string value)
    {
        currentWeather = value;
        ApplyRunInfoTexts(forceRefresh: true);
    }

    private void ResolveReferences()
    {
        if (generator == null)
            generator = CoreGenerator.Instance != null ? CoreGenerator.Instance : FindObjectOfType<CoreGenerator>();

        if (weatherManager == null)
            weatherManager = WeatherManager.Instance != null ? WeatherManager.Instance : FindObjectOfType<WeatherManager>();

        if (floorText == null)
            floorText = FindTextByObjectName("Floor");

        if (themeText == null)
            themeText = FindTextByObjectName("Theme");

        if (playerNameText == null)
            playerNameText = FindTextByObjectNames("PlayerName", "PlayerNameText");

        if (weatherText == null)
            weatherText = FindTextByObjectNames("CurrentWeather", "Weather", "WeatherText");

        if (runTimerText == null)
            runTimerText = FindTextByObjectNames("RunTimer", "RunTimerText", "Timer", "TimerText");

        if (playerNameInput == null)
            playerNameInput = FindInputByObjectNames("PlayerNameInput", "NameInput", "PlayerInput");

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

    private void SubscribeToPlayerNameInput()
    {
        if (subscribedPlayerNameInput == playerNameInput)
            return;

        UnsubscribeFromPlayerNameInput();

        if (playerNameInput == null)
            return;

        playerNameInput.onValueChanged.AddListener(HandlePlayerNameInputChanged);
        subscribedPlayerNameInput = playerNameInput;
    }

    private void UnsubscribeFromPlayerNameInput()
    {
        if (subscribedPlayerNameInput == null)
            return;

        subscribedPlayerNameInput.onValueChanged.RemoveListener(HandlePlayerNameInputChanged);
        subscribedPlayerNameInput = null;
    }

    private void HandlePlayerNameInputChanged(string value)
    {
        defaultPlayerName = value;
        ApplyRunInfoTexts(forceRefresh: true);
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

    private static TextMeshProUGUI FindTextByObjectNames(params string[] objectNames)
    {
        for (int i = 0; i < objectNames.Length; i++)
        {
            TextMeshProUGUI text = FindTextByObjectName(objectNames[i]);
            if (text != null)
                return text;
        }

        return null;
    }

    private static TMP_InputField FindInputByObjectNames(params string[] objectNames)
    {
        TMP_InputField[] inputs = FindObjectsOfType<TMP_InputField>(true);
        for (int i = 0; i < objectNames.Length; i++)
        {
            for (int j = 0; j < inputs.Length; j++)
            {
                if (inputs[j].name == objectNames[i])
                    return inputs[j];
            }
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

    private string ResolvePlayerName()
    {
        string inputName = playerNameInput != null ? playerNameInput.text : null;
        string resolvedName = string.IsNullOrWhiteSpace(inputName) ? defaultPlayerName : inputName;
        return string.IsNullOrWhiteSpace(resolvedName) ? "Player" : resolvedName.Trim();
    }

    private string ResolveCurrentWeather()
    {
        if (weatherManager == null)
            weatherManager = WeatherManager.Instance != null ? WeatherManager.Instance : FindObjectOfType<WeatherManager>();

        if (weatherManager != null && !string.IsNullOrWhiteSpace(weatherManager.CurrentDisplayName))
            return weatherManager.CurrentDisplayName.Trim();

        return string.IsNullOrWhiteSpace(currentWeather) ? "-" : currentWeather.Trim();
    }

    private static string FormatElapsedTime(int totalSeconds)
    {
        totalSeconds = Mathf.Max(0, totalSeconds);
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;

        if (hours > 0)
            return $"{hours}:{minutes:00}:{seconds:00}";

        return $"{minutes:00}:{seconds:00}";
    }
}
