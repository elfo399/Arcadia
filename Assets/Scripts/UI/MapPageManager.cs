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
    [SerializeField] private TextMeshProUGUI coinValueText;
    [SerializeField] private Image playerPortraitImage;
    [SerializeField] private TMP_InputField playerNameInput;

    [Header("Map Layout")]
    [Tooltip("Riquadro esterno che contiene sfondo, area della mappa e cornice.")]
    [SerializeField] private RectTransform mapContainer;
    [Tooltip("Area interna nella quale vengono generate le stanze.")]
    [SerializeField] private RectTransform mapContentContainer;
    [Tooltip("Maschera applicata all'area interna della mappa.")]
    [SerializeField] private RectMask2D mapContentMask;
    [Tooltip("Immagine della sola cornice, renderizzata sopra le stanze.")]
    [SerializeField] private Image mapFrameOverlayImage;

    [Header("Player UI")]
    [SerializeField] private ProgressBarUI hpProgressBar;
    [SerializeField] private ProgressBarUI manaProgressBar;
    [SerializeField] private ProgressBarUI xpProgressBar;
    [SerializeField] private PlayerCharacterDatabase playerCharacterDatabase;
    [SerializeField] private string floorFormat = "Floor {0}";
    [SerializeField] private string themeFormat = "{0}";
    [SerializeField] private string playerNameFormat = "{0}";
    [SerializeField] private string weatherFormat = "{0}";
    [SerializeField] private string runTimerFormat = "{0}";
    [SerializeField] private string missingThemeLabel = "-";
    [SerializeField] private string defaultPlayerName = "Player";
    [SerializeField] private string currentWeather = "Clear";
    [SerializeField] private float mapPadding = 32f;
    [SerializeField] private float mapInnerPadding = 4f;
    [SerializeField] private float mapMaxScale = 0.85f;
    [SerializeField] private bool clipMapToContainer = true;
    [SerializeField] private bool showFullMapForTesting = true;
    [SerializeField] private bool matchBookPageRoomBackground = true;
    [SerializeField] private bool hidePlayerPortraitWhenMissing = true;
    [SerializeField] private Color bookPageRoomBackgroundColor = new Color(0.93f, 0.70f, 0.48f, 1f);

    private CoreGenerator subscribedGenerator;
    private MinimapManager subscribedMinimap;
    private TMP_InputField subscribedPlayerNameInput;
    private PlayerStats playerStats;
    private string lastDisplayedPlayerName;
    private string lastDisplayedWeather;
    private string lastDisplayedCharacterId;
    private Sprite lastDisplayedPlayerPortrait;
    private int lastDisplayedRunSeconds = int.MinValue;
    private int lastDisplayedCoins = int.MinValue;
    private float lastDisplayedHealth = float.MinValue;
    private float lastDisplayedMaxHealth = float.MinValue;
    private float lastDisplayedMana = float.MinValue;
    private float lastDisplayedMaxMana = float.MinValue;
    private int lastDisplayedLevelExperience = int.MinValue;
    private int lastDisplayedExperienceToNextLevel = int.MinValue;
    private const int RunTimerStartSeconds = 0;
    private const string MapContentName = "MapContent";
    private const string MapFrameOverlayName = "MapFrameOverlay";

    private bool mapLayersConfigured;

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
        ApplyCoinText();
        ApplyPlayerProgressBars();
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
            ApplyCoinText(forceRefresh: true);
            ApplyPlayerPortrait(forceRefresh: true);
            ApplyPlayerProgressBars(forceRefresh: true);
            RefreshMap();
            return;
        }

        ApplyTexts(generator.CurrentFloor, generator.ActiveThemeDisplayName);
        ApplyRunInfoTexts(forceRefresh: true);
        ApplyCoinText(forceRefresh: true);
        ApplyPlayerPortrait(forceRefresh: true);
        ApplyPlayerProgressBars(forceRefresh: true);
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

        RectTransform renderContainer = EnsureMapLayers();
        if (renderContainer == null)
            return;

        MinimapManager.instance.RenderExploredMap(
            renderContainer,
            mapInnerPadding,
            mapMaxScale,
            showFullMapForTesting,
            matchBookPageRoomBackground,
            bookPageRoomBackgroundColor);

        if (mapFrameOverlayImage != null)
            mapFrameOverlayImage.transform.SetAsLastSibling();
    }

    private RectTransform EnsureMapLayers()
    {
        if (mapLayersConfigured && mapContentContainer != null)
            return mapContentContainer;

        if (mapContentContainer == null)
        {
            Transform existingContent = mapContainer.Find(MapContentName);
            mapContentContainer = existingContent as RectTransform;

            if (mapContentContainer == null)
            {
                GameObject contentObject = new GameObject(MapContentName, typeof(RectTransform));
                mapContentContainer = contentObject.GetComponent<RectTransform>();
                mapContentContainer.SetParent(mapContainer, false);
            }
        }

        float inset = Mathf.Max(0f, mapPadding);
        mapContentContainer.anchorMin = Vector2.zero;
        mapContentContainer.anchorMax = Vector2.one;
        mapContentContainer.pivot = new Vector2(0.5f, 0.5f);
        mapContentContainer.offsetMin = new Vector2(inset, inset);
        mapContentContainer.offsetMax = new Vector2(-inset, -inset);
        mapContentContainer.localScale = Vector3.one;
        mapContentContainer.SetAsFirstSibling();

        if (mapContentMask == null)
            mapContentMask = mapContentContainer.GetComponent<RectMask2D>();
        if (clipMapToContainer && mapContentMask == null)
            mapContentMask = mapContentContainer.gameObject.AddComponent<RectMask2D>();
        if (mapContentMask != null)
            mapContentMask.enabled = clipMapToContainer;

        EnsureMapFrameOverlay();
        mapLayersConfigured = true;
        return mapContentContainer;
    }

    private void EnsureMapFrameOverlay()
    {
        if (mapFrameOverlayImage == null)
        {
            Transform existingOverlay = mapContainer.Find(MapFrameOverlayName);
            mapFrameOverlayImage = existingOverlay != null ? existingOverlay.GetComponent<Image>() : null;
        }

        if (mapFrameOverlayImage == null)
            return;

        RectTransform overlayRect = mapFrameOverlayImage.rectTransform;
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayRect.localScale = Vector3.one;
        mapFrameOverlayImage.type = Image.Type.Simple;
        mapFrameOverlayImage.preserveAspect = false;
        mapFrameOverlayImage.raycastTarget = false;
        mapFrameOverlayImage.transform.SetAsLastSibling();
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
            int elapsedSeconds = RunTimerStartSeconds + Mathf.FloorToInt(Mathf.Max(0f, Time.timeSinceLevelLoad));
            if (forceRefresh || elapsedSeconds != lastDisplayedRunSeconds)
            {
                runTimerText.text = FormatText(runTimerFormat, FormatElapsedTime(elapsedSeconds));
                lastDisplayedRunSeconds = elapsedSeconds;
            }
        }
    }

    private void ApplyCoinText(bool forceRefresh = false)
    {
        ResolveCoinValueText();
        if (coinValueText == null)
            return;

        ResolvePlayerStats();
        if (playerStats == null)
        {
            if (forceRefresh || lastDisplayedCoins != 0)
            {
                coinValueText.text = string.Empty;
                lastDisplayedCoins = 0;
            }
            return;
        }

        int coins = Mathf.Max(0, playerStats.runCoins);
        if (forceRefresh || coins != lastDisplayedCoins)
        {
            coinValueText.text = coins.ToString();
            lastDisplayedCoins = coins;
        }
    }

    private void ApplyPlayerPortrait(bool forceRefresh = false)
    {
        if (playerPortraitImage == null)
            return;

        PlayerCharacterData character = ResolveSelectedCharacter();
        string characterId = character != null ? character.GetCharacterId() : string.Empty;
        Sprite portrait = character != null ? character.portrait : null;

        if (!forceRefresh
            && characterId == lastDisplayedCharacterId
            && portrait == lastDisplayedPlayerPortrait)
        {
            return;
        }

        playerPortraitImage.sprite = portrait;
        playerPortraitImage.enabled = portrait != null || !hidePlayerPortraitWhenMissing;
        lastDisplayedCharacterId = characterId;
        lastDisplayedPlayerPortrait = portrait;
    }

    private void ApplyPlayerProgressBars(bool forceRefresh = false)
    {
        ResolvePlayerStats();

        if (playerStats == null)
        {
            ApplyProgress(hpProgressBar, 0f, string.Empty);
            ApplyProgress(manaProgressBar, 0f, string.Empty);
            ApplyProgress(xpProgressBar, 0f, string.Empty);
            return;
        }

        float currentHealth = Mathf.Clamp(playerStats.currentHealth, 0f, playerStats.maxHealth);
        float maxHealth = Mathf.Max(1f, playerStats.maxHealth);
        if (forceRefresh
            || !Mathf.Approximately(currentHealth, lastDisplayedHealth)
            || !Mathf.Approximately(maxHealth, lastDisplayedMaxHealth))
        {
            ApplyProgress(
                hpProgressBar,
                currentHealth / maxHealth,
                $"{Mathf.RoundToInt(currentHealth)}/{Mathf.RoundToInt(maxHealth)}");

            lastDisplayedHealth = currentHealth;
            lastDisplayedMaxHealth = maxHealth;
        }

        float currentMana = Mathf.Clamp(playerStats.currentMana, 0f, playerStats.maxMana);
        float maxMana = Mathf.Max(1f, playerStats.maxMana);
        if (forceRefresh
            || !Mathf.Approximately(currentMana, lastDisplayedMana)
            || !Mathf.Approximately(maxMana, lastDisplayedMaxMana))
        {
            ApplyProgress(
                manaProgressBar,
                currentMana / maxMana,
                $"{Mathf.RoundToInt(currentMana)}/{Mathf.RoundToInt(maxMana)}");

            lastDisplayedMana = currentMana;
            lastDisplayedMaxMana = maxMana;
        }

        int levelExperience = Mathf.Max(0, playerStats.levelExperience);
        int experienceToNextLevel = Mathf.Max(1, playerStats.experienceToNextLevel);
        if (forceRefresh
            || levelExperience != lastDisplayedLevelExperience
            || experienceToNextLevel != lastDisplayedExperienceToNextLevel)
        {
            ApplyProgress(
                xpProgressBar,
                (float)levelExperience / experienceToNextLevel,
                $"{levelExperience}/{experienceToNextLevel}");

            lastDisplayedLevelExperience = levelExperience;
            lastDisplayedExperienceToNextLevel = experienceToNextLevel;
        }
    }

    private void ResolveReferences()
    {
        if (generator == null)
            generator = CoreGenerator.Instance;

        if (weatherManager == null)
            weatherManager = WeatherManager.Instance;

        if (playerCharacterDatabase == null)
            playerCharacterDatabase = Resources.Load<PlayerCharacterDatabase>("PlayerCharacterDatabase");

        ResolveCoinValueText();
        ResolvePlayerStats();
    }

    private void ResolvePlayerStats()
    {
        if (playerStats == null)
            playerStats = PlayerStats.instance;
    }

    private void ResolveCoinValueText()
    {
        if (coinValueText != null)
            return;

        coinValueText = FindMapPageCoinCountText();
    }

    private static TextMeshProUGUI FindMapPageCoinCountText()
    {
        TextMeshProUGUI fallback = null;
        TextMeshProUGUI[] texts = FindObjectsOfType<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TextMeshProUGUI text = texts[i];
            if (text == null || text.name != "Count")
                continue;

            Transform parent = text.transform.parent;
            if (parent == null || parent.name != "Coin")
                continue;

            if (HasAncestor(parent, "MapPage"))
                return text;

            if (fallback == null)
                fallback = text;
        }

        return fallback;
    }

    private static bool HasAncestor(Transform transform, string ancestorName)
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.name == ancestorName)
                return true;

            current = current.parent;
        }

        return false;
    }

    private PlayerCharacterData ResolveSelectedCharacter()
    {
        if (playerCharacterDatabase == null)
            playerCharacterDatabase = Resources.Load<PlayerCharacterDatabase>("PlayerCharacterDatabase");

        if (playerCharacterDatabase == null)
            return null;

        string selectedCharacterId = PlayerStats.instance != null
            ? PlayerStats.instance.SelectedCharacterId
            : string.Empty;

        if (string.IsNullOrWhiteSpace(selectedCharacterId))
            selectedCharacterId = PlayerCharacterSelection.GetSelectedCharacterId();

        PlayerCharacterData explicitMatch = ResolveCharacterById(selectedCharacterId);
        return explicitMatch != null ? explicitMatch : playerCharacterDatabase.GetById(selectedCharacterId);
    }

    private PlayerCharacterData ResolveCharacterById(string characterId)
    {
        if (playerCharacterDatabase == null || string.IsNullOrWhiteSpace(characterId))
            return null;

        var characters = playerCharacterDatabase.Characters;
        if (characters == null)
            return null;

        string normalizedId = characterId.Trim();
        for (int i = 0; i < characters.Length; i++)
        {
            var candidate = characters[i];
            if (candidate == null)
                continue;

            if (string.Equals(candidate.GetCharacterId(), normalizedId, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        return null;
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
        if (!string.IsNullOrWhiteSpace(value))
        {
            defaultPlayerName = value.Trim();
            if (PlayerStats.instance != null)
                PlayerStats.instance.SetCharacterName(defaultPlayerName);
        }

        ApplyRunInfoTexts(forceRefresh: true);
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

    private static void ApplyProgress(ProgressBarUI bar, float normalized, string displayText)
    {
        if (bar == null) return;
        bar.SetProgress(normalized, displayText);
    }

    private string ResolvePlayerName()
    {
        PlayerCharacterData character = ResolveSelectedCharacter();
        if (character != null)
        {
            if (!string.IsNullOrWhiteSpace(character.displayName))
                return character.displayName.Trim();

            return character.GetCharacterId();
        }

        if (PlayerStats.instance != null && !string.IsNullOrWhiteSpace(PlayerStats.instance.CharacterName))
            return PlayerStats.instance.CharacterName.Trim();

        string inputName = playerNameInput != null ? playerNameInput.text : null;
        string resolvedName = string.IsNullOrWhiteSpace(inputName) ? defaultPlayerName : inputName;
        return string.IsNullOrWhiteSpace(resolvedName) ? "Player" : resolvedName.Trim();
    }

    private string ResolveCurrentWeather()
    {
        if (weatherManager == null)
            weatherManager = WeatherManager.Instance;

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
