using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Linq;

public class MinimapManager : MonoBehaviour
{
    private enum MapMode
    {
        Dungeon,
        Hub
    }

    public static MinimapManager instance;
    public event System.Action MapStateChanged;

    [Header("Riferimenti UI")]
    public RectTransform mapContainer; 
    public GameObject roomIconPrefab;

    [Header("Icone Speciali")]
    public Sprite skullIcon;   
    public Sprite crownIcon;   
    public Sprite startIcon;   
    public Sprite shopIcon;
    public Sprite blessedIcon;
    public Sprite evilIcon;

    [Header("Colori")]
    public Color currentRoomColor = Color.white;
    public Color visitedRoomColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    public Color adjacentRoomColor = new Color(0.2f, 0.2f, 0.2f, 1f);

    [Header("Settings")]
    public float iconBaseSize = 20f; 
    public float iconSpacing = 0f;   
    [SerializeField] private bool hideInHubScene = false;

    [Header("Hub Map")]
    [SerializeField] private string hubSceneName = "HubScene";
    [SerializeField] private HubMapZone hubMapZone;
    [SerializeField] private Color hubMapBackgroundColor = new Color(0.12f, 0.15f, 0.15f, 0.9f);
    [SerializeField] private Color hubMapBorderColor = new Color(0.82f, 0.70f, 0.48f, 1f);
    [SerializeField] private Color hubPlayerMarkerColor = new Color(0.25f, 0.75f, 1f, 1f);
    [SerializeField] private Color hubPortalMarkerColor = new Color(0.72f, 0.36f, 1f, 1f);
    [SerializeField] private Vector2 hubPlayerMarkerSize = new Vector2(14f, 14f);
    [SerializeField] private Vector2 hubPortalMarkerSize = new Vector2(12f, 12f);
    [SerializeField, Min(1f)] private float hubMinimapWorldViewSize = 28f;
    [SerializeField] private bool createDefaultHubZoneIfMissing = true;
    [SerializeField] private Vector2 defaultHubMapCenterXZ = Vector2.zero;
    [SerializeField] private Vector2 defaultHubMapSizeXZ = new Vector2(80f, 80f);

    // --- STRUTTURE DATI PER FOG OF WAR ---
    private Dictionary<Vector2Int, GameObject> _roomIconObjects = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, RoomData> _roomData = new Dictionary<Vector2Int, RoomData>();
    private HashSet<Vector2Int> _visitedRoomAnchors = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> _revealedRoomAnchors = new HashSet<Vector2Int>(); // Stanze da mostrare permanentemente
    private Vector2Int _lastPlayerRoomAnchor = new Vector2Int(-999, -999);
    private const string RenderedMenuMapIconPrefix = "MenuMapRoomIcon_";
    private const string HubMapBackgroundName = "HubMap_Background";
    private const string HubMapPlayerMarkerName = "HubMap_PlayerMarker";
    private const string HubMapPortalMarkerName = "HubMap_PortalMarker";
    private const string RenderedHubMapPrefix = "MenuHubMap_";

    private MapMode currentMode = MapMode.Dungeon;
    private RectTransform hubMapRoot;
    private RectTransform hubPlayerMarker;
    private RectTransform hubPortalMarker;
    private Image hubMapBackgroundImage;
    private Transform hubPlayerTarget;
    private Transform hubPortalTarget;
    
    private float FullStep => iconBaseSize + iconSpacing;

    void Awake() 
    { 
        if (instance != null && instance != this)
        {
            if (instance.mapContainer == null && mapContainer != null)
            {
                Destroy(instance.gameObject);
                instance = this;
                return;
            }

            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        UpdateMapVisibilityForScene(SceneManager.GetActiveScene().name);
    }

    private void Update()
    {
        if (currentMode == MapMode.Hub)
            UpdateHubMapRuntime();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateMapVisibilityForScene(scene.name);
    }

    private void UpdateMapVisibilityForScene(string sceneName)
    {
        if (mapContainer == null)
            return;

        bool shouldShowMap = !hideInHubScene || !IsHubScene(sceneName);
        mapContainer.gameObject.SetActive(shouldShowMap);

        bool shouldUseHubMap = IsHubScene(sceneName);
        SetMapMode(shouldUseHubMap ? MapMode.Hub : MapMode.Dungeon);
    }

    private bool IsHubScene(string sceneName)
    {
        string resolvedHubSceneName = string.IsNullOrWhiteSpace(hubSceneName) ? "HubScene" : hubSceneName.Trim();
        return string.Equals(sceneName, resolvedHubSceneName, System.StringComparison.OrdinalIgnoreCase);
    }

    private void SetMapMode(MapMode mode)
    {
        if (currentMode == mode)
        {
            if (currentMode == MapMode.Hub)
                EnsureHubMapRuntime();
            return;
        }

        currentMode = mode;

        if (mode == MapMode.Hub)
        {
            HideDungeonRoomIcons();
            EnsureHubMapRuntime();
        }
        else
        {
            if (hubMapRoot != null)
                hubMapRoot.gameObject.SetActive(false);
            if (hubPlayerMarker != null)
                hubPlayerMarker.gameObject.SetActive(false);
        }

        MapStateChanged?.Invoke();
    }

    private void HideDungeonRoomIcons()
    {
        foreach (GameObject roomIcon in _roomIconObjects.Values)
        {
            if (roomIcon != null)
                roomIcon.SetActive(false);
        }
    }

    private void EnsureHubMapRuntime()
    {
        if (mapContainer == null || !ResolveHubMapZone())
            return;

        hubPlayerTarget = ResolveHubPlayerTarget();
        hubPortalTarget = ResolveHubPortalTarget();
        hubMapRoot = EnsureRectChild(mapContainer, "HubMap_RuntimeRoot");
        hubMapRoot.SetAsFirstSibling();
        hubMapRoot.gameObject.SetActive(true);
        ConfigureHubRuntimeContentSize();

        RectTransform background = EnsureRectChild(hubMapRoot, HubMapBackgroundName);
        StretchToParent(background);
        hubMapBackgroundImage = EnsureImage(background, hubMapBackgroundColor);
        hubMapBackgroundImage.sprite = hubMapZone.MapSprite;
        hubMapBackgroundImage.type = Image.Type.Simple;
        hubMapBackgroundImage.preserveAspect = hubMapZone.MapSprite != null;

        EnsureOutline(background, hubMapBorderColor, new Vector2(2f, -2f));

        hubPortalMarker = EnsureRectChild(hubMapRoot, HubMapPortalMarkerName);
        hubPortalMarker.sizeDelta = hubPortalMarkerSize;
        EnsureImage(hubPortalMarker, hubPortalMarkerColor).raycastTarget = false;
        hubPortalMarker.gameObject.SetActive(hubPortalTarget != null);

        hubPlayerMarker = EnsureRectChild(mapContainer, HubMapPlayerMarkerName);
        hubPlayerMarker.SetAsLastSibling();
        hubPlayerMarker.anchorMin = new Vector2(0.5f, 0.5f);
        hubPlayerMarker.anchorMax = new Vector2(0.5f, 0.5f);
        hubPlayerMarker.pivot = new Vector2(0.5f, 0.5f);
        hubPlayerMarker.anchoredPosition = Vector2.zero;
        hubPlayerMarker.sizeDelta = hubPlayerMarkerSize;
        EnsureImage(hubPlayerMarker, hubPlayerMarkerColor).raycastTarget = false;

        UpdateHubMapRuntime();
    }

    private void UpdateHubMapRuntime()
    {
        if (hubMapRoot == null || !ResolveHubMapZone())
            EnsureHubMapRuntime();

        if (hubMapRoot == null || hubMapZone == null)
            return;

        if (hubPlayerTarget == null)
            hubPlayerTarget = ResolveHubPlayerTarget();
        if (hubPortalTarget == null)
            hubPortalTarget = ResolveHubPortalTarget();

        ConfigureHubRuntimeContentSize();
        FollowHubPlayer();
        PositionHubWorldMarker(hubPortalMarker, hubPortalTarget, false);
        PositionHubCenteredPlayerMarker();
    }

    private bool ResolveHubMapZone()
    {
        if (hubMapZone != null)
            return true;

#if UNITY_2023_1_OR_NEWER
        hubMapZone = FindFirstObjectByType<HubMapZone>();
#else
        hubMapZone = FindObjectOfType<HubMapZone>();
#endif
        if (hubMapZone == null && createDefaultHubZoneIfMissing && IsHubScene(SceneManager.GetActiveScene().name))
        {
            GameObject zoneObject = new GameObject("HubMapZone_Runtime");
            hubMapZone = zoneObject.AddComponent<HubMapZone>();
            hubMapZone.Configure(defaultHubMapCenterXZ, defaultHubMapSizeXZ, ResolveHubPortalTarget());
        }

        return hubMapZone != null;
    }

    private Transform ResolveHubPlayerTarget()
    {
        if (PlayerController.CurrentPlayerTransform != null)
            return PlayerController.CurrentPlayerTransform;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            return player.transform;

        GameObject namedPlayer = GameObject.Find("Player");
        return namedPlayer != null ? namedPlayer.transform : null;
    }

    private Transform ResolveHubPortalTarget()
    {
        if (hubMapZone != null && hubMapZone.PortalMarkerTarget != null)
            return hubMapZone.PortalMarkerTarget;

        GameObject portal = GameObject.Find("DungeonPortal");
        return portal != null ? portal.transform : null;
    }

    private void ConfigureHubRuntimeContentSize()
    {
        if (hubMapRoot == null || mapContainer == null || hubMapZone == null)
            return;

        Rect containerRect = mapContainer.rect;
        if (containerRect.width <= 1f || containerRect.height <= 1f)
            return;

        Vector2 worldSize = hubMapZone.WorldSizeXZ;
        float viewSize = Mathf.Max(1f, hubMinimapWorldViewSize);
        float pixelsPerWorldUnit = Mathf.Min(containerRect.width, containerRect.height) / viewSize;
        Vector2 contentSize = new Vector2(
            Mathf.Max(containerRect.width, worldSize.x * pixelsPerWorldUnit),
            Mathf.Max(containerRect.height, worldSize.y * pixelsPerWorldUnit));

        hubMapRoot.anchorMin = new Vector2(0.5f, 0.5f);
        hubMapRoot.anchorMax = new Vector2(0.5f, 0.5f);
        hubMapRoot.pivot = new Vector2(0.5f, 0.5f);
        hubMapRoot.sizeDelta = contentSize;
        hubMapRoot.localScale = Vector3.one;
    }

    private void FollowHubPlayer()
    {
        if (hubMapRoot == null || mapContainer == null || hubMapZone == null || hubPlayerTarget == null)
            return;

        Vector2 normalized = hubMapZone.WorldToNormalized(hubPlayerTarget.position);
        Vector2 rootSize = hubMapRoot.rect.size;
        Vector2 containerSize = mapContainer.rect.size;
        Vector2 targetPosition = new Vector2(
            (0.5f - normalized.x) * rootSize.x,
            (0.5f - normalized.y) * rootSize.y);

        float maxX = Mathf.Max(0f, (rootSize.x - containerSize.x) * 0.5f);
        float maxY = Mathf.Max(0f, (rootSize.y - containerSize.y) * 0.5f);
        hubMapRoot.anchoredPosition = new Vector2(
            Mathf.Clamp(targetPosition.x, -maxX, maxX),
            Mathf.Clamp(targetPosition.y, -maxY, maxY));
    }

    private void PositionHubCenteredPlayerMarker()
    {
        if (hubPlayerMarker == null)
            return;

        if (hubPlayerTarget == null)
        {
            hubPlayerMarker.gameObject.SetActive(false);
            return;
        }

        hubPlayerMarker.gameObject.SetActive(true);
        hubPlayerMarker.anchoredPosition = Vector2.zero;
        hubPlayerMarker.localRotation = Quaternion.Euler(0f, 0f, -hubPlayerTarget.eulerAngles.y);
    }

    private void PositionHubWorldMarker(RectTransform marker, Transform target, bool rotateWithTarget)
    {
        if (marker == null)
            return;

        if (target == null)
        {
            marker.gameObject.SetActive(false);
            return;
        }

        marker.gameObject.SetActive(true);
        Vector2 normalized = hubMapZone.WorldToNormalized(target.position);
        Rect rect = hubMapRoot.rect;
        marker.anchoredPosition = new Vector2(
            (normalized.x - 0.5f) * rect.width,
            (normalized.y - 0.5f) * rect.height);
        marker.localRotation = rotateWithTarget
            ? Quaternion.Euler(0f, 0f, -target.eulerAngles.y)
            : Quaternion.identity;
    }

    private static RectTransform EnsureRectChild(Transform parent, string objectName)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
            return existing as RectTransform ?? existing.gameObject.AddComponent<RectTransform>();

        GameObject child = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static Image EnsureImage(RectTransform rect, Color color)
    {
        Image image = rect.GetComponent<Image>();
        if (image == null)
            image = rect.gameObject.AddComponent<Image>();

        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Outline EnsureOutline(RectTransform rect, Color color, Vector2 distance)
    {
        Outline outline = rect.GetComponent<Outline>();
        if (outline == null)
            outline = rect.gameObject.AddComponent<Outline>();

        outline.effectColor = color;
        outline.effectDistance = distance;
        return outline;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    public void ClearMap()
    {
        if (mapContainer != null)
        {
            foreach (Transform child in mapContainer) Destroy(child.gameObject);
        }

        hubMapRoot = null;
        hubMapBackgroundImage = null;
        hubPlayerMarker = null;
        hubPortalMarker = null;
        _roomIconObjects.Clear();
        _visitedRoomAnchors.Clear();
        _revealedRoomAnchors.Clear();
        _roomData.Clear();
        _lastPlayerRoomAnchor = new Vector2Int(-999,-999);
        MapStateChanged?.Invoke();
    }

    public void RegisterRoom(Vector2Int gridPos, RoomData data)
    {
        if (currentMode == MapMode.Hub)
            return;

        if (data == null || _roomData.ContainsKey(gridPos) || mapContainer == null || roomIconPrefab == null) return;

        GameObject newIconObj = Instantiate(roomIconPrefab, mapContainer);
        _roomData.Add(gridPos, data);
        _roomIconObjects.Add(gridPos, newIconObj);
        
        SetupIconVisuals(newIconObj, gridPos, data);
        
        newIconObj.SetActive(false);
        MapStateChanged?.Invoke();
    }

    private void SetupIconVisuals(GameObject iconObj, Vector2Int gridPos, RoomData data)
    {
        RectTransform rt = iconObj.GetComponent<RectTransform>();
        rt.pivot = new Vector2(0, 0);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;

        rt.anchoredPosition = new Vector2(gridPos.x * FullStep, gridPos.y * FullStep);
        rt.sizeDelta = new Vector2(
            (data.size.x * iconBaseSize) + ((data.size.x - 1) * iconSpacing),
            (data.size.y * iconBaseSize) + ((data.size.y - 1) * iconSpacing)
        );

        Image fillImage = iconObj.transform.Find("RoomFill")?.GetComponent<Image>() ?? iconObj.GetComponent<Image>();
        if (fillImage != null)
            fillImage.color = visitedRoomColor;

        Image overlayImg = iconObj.transform.Find("IconOverlay")?.GetComponent<Image>();
        if (overlayImg != null)
        {
            overlayImg.gameObject.SetActive(false);
            if      (data.isBossRoom && skullIcon != null)   { overlayImg.sprite = skullIcon;   overlayImg.gameObject.SetActive(true); }
            else if (data.isTreasureRoom && crownIcon != null) { overlayImg.sprite = crownIcon;   overlayImg.gameObject.SetActive(true); }
            else if (data.isStartRoom && startIcon != null)  { overlayImg.sprite = startIcon;   overlayImg.gameObject.SetActive(true); }
            else if (data.isShopRoom && shopIcon != null)    { overlayImg.sprite = shopIcon;    overlayImg.gameObject.SetActive(true); }
            else if (data.isBlessedRoom && blessedIcon != null){ overlayImg.sprite = blessedIcon; overlayImg.gameObject.SetActive(true); }
            else if (data.isEvilRoom && evilIcon != null)    { overlayImg.sprite = evilIcon;    overlayImg.gameObject.SetActive(true); }
        }
    }
    
    public void RevealStartingArea(Vector2Int startPosAnchor)
    {
        if (currentMode == MapMode.Hub)
            return;

        _lastPlayerRoomAnchor = startPosAnchor;
        UpdateMapVisibility(startPosAnchor);
    }

    public void UpdatePlayerPosition(Vector3 worldPos, float roomSize)
    {
        if (currentMode == MapMode.Hub)
            return;

        if (mapContainer == null)
            return;

        int gridX = Mathf.RoundToInt(worldPos.x / roomSize);
        int gridY = Mathf.RoundToInt(worldPos.z / roomSize);
        Vector2Int currentGridCell = new Vector2Int(gridX, gridY);

        Vector2 targetPos = -1 * new Vector2(currentGridCell.x * FullStep, currentGridCell.y * FullStep);
        mapContainer.anchoredPosition = Vector2.Lerp(mapContainer.anchoredPosition, targetPos, Time.deltaTime * 5f);
        
        if(GetAnchorForCell(currentGridCell, out Vector2Int currentRoomAnchor))
        {
            if (currentRoomAnchor == _lastPlayerRoomAnchor) return;
            
            _lastPlayerRoomAnchor = currentRoomAnchor;
            UpdateMapVisibility(currentRoomAnchor);
        }
    }

    private void UpdateMapVisibility(Vector2Int currentRoomAnchor)
    {
        // 1. Aggiungi la stanza corrente a quelle visitate e rivelate
        _visitedRoomAnchors.Add(currentRoomAnchor);
        _revealedRoomAnchors.Add(currentRoomAnchor);
        
        // 2. Aggiungi le stanze adiacenti a quelle rivelate
        foreach (var room in _roomData)
        {
            if (AreRoomsAdjacent(currentRoomAnchor, room.Key))
            {
                _revealedRoomAnchors.Add(room.Key);
            }
        }

        // 3. Itera su tutte le stanze e aggiorna la visibilità e il colore in base allo stato
        foreach (var entry in _roomData)
        {
            Vector2Int roomAnchor = entry.Key;
            GameObject iconObj = _roomIconObjects[roomAnchor];

            if (_revealedRoomAnchors.Contains(roomAnchor))
            {
                iconObj.SetActive(true);
                Image fillImage = iconObj.transform.Find("RoomFill")?.GetComponent<Image>() ?? iconObj.GetComponent<Image>();

                if (roomAnchor == currentRoomAnchor)
                {
                    if (fillImage != null)
                        fillImage.color = currentRoomColor;
                }
                else if (_visitedRoomAnchors.Contains(roomAnchor))
                {
                    if (fillImage != null)
                        fillImage.color = visitedRoomColor;
                }
                else // Rivelata ma non visitata (quindi adiacente a una visitata)
                {
                    if (fillImage != null)
                        fillImage.color = adjacentRoomColor;
                }
            }
            else
            {
                iconObj.SetActive(false);
            }
        }

        MapStateChanged?.Invoke();
    }

    public void RenderExploredMap(
        RectTransform targetContainer,
        float padding = 12f,
        float maxScale = 1f,
        bool includeUnrevealedRooms = false,
        bool overrideRoomFillColor = false,
        Color roomFillColor = default)
    {
        if (targetContainer == null)
            return;

        ClearRenderedMenuMap(targetContainer);

        if ((currentMode == MapMode.Hub || IsHubScene(SceneManager.GetActiveScene().name)) && ResolveHubMapZone())
        {
            RenderHubMap(targetContainer, padding);
            return;
        }

        if (roomIconPrefab == null || _roomData.Count == 0)
            return;

        List<Vector2Int> visibleAnchors = _roomData.Keys
            .Where(anchor => includeUnrevealedRooms || _revealedRoomAnchors.Contains(anchor))
            .ToList();

        if (visibleAnchors.Count == 0)
            return;

        RectInt bounds = BuildRoomBounds(visibleAnchors);
        Vector2 boundsCenter = new Vector2(bounds.xMin + bounds.width * 0.5f, bounds.yMin + bounds.height * 0.5f);
        float contentWidth = Mathf.Max(FullStep, bounds.width * FullStep);
        float contentHeight = Mathf.Max(FullStep, bounds.height * FullStep);
        float availableWidth = Mathf.Max(1f, targetContainer.rect.width - padding * 2f);
        float availableHeight = Mathf.Max(1f, targetContainer.rect.height - padding * 2f);
        float scaleLimit = maxScale > 0f ? maxScale : 1f;
        float fitScale = Mathf.Min(scaleLimit, availableWidth / contentWidth, availableHeight / contentHeight);
        fitScale = Mathf.Max(Mathf.Epsilon, fitScale);

        for (int i = 0; i < visibleAnchors.Count; i++)
        {
            Vector2Int roomAnchor = visibleAnchors[i];
            RoomData data = _roomData[roomAnchor];
            GameObject iconObj = Instantiate(roomIconPrefab, targetContainer);
            iconObj.name = $"{RenderedMenuMapIconPrefix}{roomAnchor.x}_{roomAnchor.y}";
            SetupIconVisuals(iconObj, roomAnchor, data);

            if (overrideRoomFillColor)
                ApplyRoomFillColor(iconObj, roomFillColor);
            else
                ApplyIconVisibilityState(iconObj, roomAnchor, _lastPlayerRoomAnchor);

            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            if (iconRect != null)
            {
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0, 0);
                iconRect.anchoredPosition = new Vector2(
                    (roomAnchor.x - boundsCenter.x) * FullStep * fitScale,
                    (roomAnchor.y - boundsCenter.y) * FullStep * fitScale
                );
                iconRect.localScale = Vector3.one * fitScale;
            }

            iconObj.SetActive(true);
        }
    }

    public bool RenderHubOverviewMap(RectTransform targetContainer, float padding = 12f)
    {
        if (targetContainer == null)
            return false;

        ClearRenderedMenuMap(targetContainer);
        if (!ResolveHubMapZone())
            return false;

        RenderHubMap(targetContainer, padding);
        return true;
    }

    private void RenderHubMap(
        RectTransform targetContainer,
        float padding)
    {
        RectTransform root = EnsureRectChild(targetContainer, RenderedHubMapPrefix + "Root");
        StretchToParent(root);
        root.SetAsLastSibling();

        RectTransform background = EnsureRectChild(root, RenderedHubMapPrefix + "Background");
        StretchToParent(background);
        background.SetAsFirstSibling();
        Image backgroundImage = EnsureImage(background, hubMapBackgroundColor);
        backgroundImage.sprite = hubMapZone.MapSprite;
        backgroundImage.preserveAspect = hubMapZone.MapSprite != null;

        EnsureOutline(background, hubMapBorderColor, new Vector2(2f, -2f));

        Transform playerTarget = ResolveHubPlayerTarget();
        Transform portalTarget = ResolveHubPortalTarget();

        RectTransform portalMarker = EnsureRectChild(root, RenderedHubMapPrefix + "PortalMarker");
        portalMarker.sizeDelta = hubPortalMarkerSize;
        EnsureImage(portalMarker, hubPortalMarkerColor);
        PositionHubMarkerForContainer(root, portalMarker, portalTarget, false, padding);

        RectTransform playerMarker = EnsureRectChild(root, RenderedHubMapPrefix + "PlayerMarker");
        playerMarker.sizeDelta = hubPlayerMarkerSize;
        EnsureImage(playerMarker, hubPlayerMarkerColor);
        PositionHubMarkerForContainer(root, playerMarker, playerTarget, true, padding);
    }

    private void PositionHubMarkerForContainer(
        RectTransform container,
        RectTransform marker,
        Transform target,
        bool rotateWithTarget,
        float padding)
    {
        if (marker == null)
            return;

        if (target == null)
        {
            marker.gameObject.SetActive(false);
            return;
        }

        marker.gameObject.SetActive(true);
        Vector2 normalized = hubMapZone.WorldToNormalized(target.position);
        Rect rect = container.rect;
        float inset = Mathf.Max(0f, padding);
        float width = Mathf.Max(1f, rect.width - inset * 2f);
        float height = Mathf.Max(1f, rect.height - inset * 2f);
        marker.anchoredPosition = new Vector2(
            (normalized.x - 0.5f) * width,
            (normalized.y - 0.5f) * height);
        marker.localRotation = rotateWithTarget
            ? Quaternion.Euler(0f, 0f, -target.eulerAngles.y)
            : Quaternion.identity;
    }

    private void ClearRenderedMenuMap(RectTransform targetContainer)
    {
        for (int i = targetContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = targetContainer.GetChild(i);
            if (child != null
                && (child.name.StartsWith(RenderedMenuMapIconPrefix, System.StringComparison.Ordinal)
                    || child.name.StartsWith(RenderedHubMapPrefix, System.StringComparison.Ordinal)))
                Destroy(child.gameObject);
        }
    }

    private RectInt BuildRoomBounds(List<Vector2Int> visibleAnchors)
    {
        Vector2Int firstAnchor = visibleAnchors[0];
        RoomData firstData = _roomData[firstAnchor];
        int minX = firstAnchor.x;
        int minY = firstAnchor.y;
        int maxX = firstAnchor.x + firstData.size.x;
        int maxY = firstAnchor.y + firstData.size.y;

        for (int i = 1; i < visibleAnchors.Count; i++)
        {
            Vector2Int anchor = visibleAnchors[i];
            RoomData data = _roomData[anchor];
            minX = Mathf.Min(minX, anchor.x);
            minY = Mathf.Min(minY, anchor.y);
            maxX = Mathf.Max(maxX, anchor.x + data.size.x);
            maxY = Mathf.Max(maxY, anchor.y + data.size.y);
        }

        return new RectInt(minX, minY, Mathf.Max(1, maxX - minX), Mathf.Max(1, maxY - minY));
    }

    private void ApplyIconVisibilityState(GameObject iconObj, Vector2Int roomAnchor, Vector2Int currentRoomAnchor)
    {
        Image fillImage = iconObj.transform.Find("RoomFill")?.GetComponent<Image>() ?? iconObj.GetComponent<Image>();
        if (fillImage == null)
            return;

        if (roomAnchor == currentRoomAnchor)
            fillImage.color = currentRoomColor;
        else if (_visitedRoomAnchors.Contains(roomAnchor))
            fillImage.color = visitedRoomColor;
        else
            fillImage.color = adjacentRoomColor;
    }

    private void ApplyRoomFillColor(GameObject iconObj, Color color)
    {
        Transform roomFill = iconObj.transform.Find("RoomFill");
        Image fillImage = roomFill != null ? roomFill.GetComponent<Image>() : null;
        if (fillImage == null)
            fillImage = iconObj.GetComponent<Image>();

        if (fillImage != null)
            fillImage.color = color;
    }
    
    private bool GetAnchorForCell(Vector2Int cell, out Vector2Int foundAnchor)
    {
        foreach(var entry in _roomData)
        {
            Vector2Int anchor = entry.Key;
            Vector2Int size = entry.Value.size;
            if (cell.x >= anchor.x && cell.x < anchor.x + size.x &&
                cell.y >= anchor.y && cell.y < anchor.y + size.y)
            {
                foundAnchor = anchor;
                return true;
            }
        }
        foundAnchor = new Vector2Int(-999, -999);
        return false;
    }

    // Controlla se due stanze, dati i loro anchor, sono adiacenti CARDINALMENTE.
    private bool AreRoomsAdjacent(Vector2Int anchor1, Vector2Int anchor2)
    {
        if (anchor1 == anchor2 || !_roomData.ContainsKey(anchor1) || !_roomData.ContainsKey(anchor2)) return false;

        RectInt rect1 = new RectInt(anchor1, _roomData[anchor1].size);
        RectInt rect2 = new RectInt(anchor2, _roomData[anchor2].size);

        // Intervalli Y si sovrappongono E intervalli X si toccano? (Adiacenza Orizzontale)
        bool horizontalOverlap = rect1.yMax > rect2.yMin && rect2.yMax > rect1.yMin;
        bool horizontalTouch = rect1.xMax == rect2.xMin || rect2.xMax == rect1.xMin;
        if (horizontalOverlap && horizontalTouch) return true;

        // Intervalli X si sovrappongono E intervalli Y si toccano? (Adiacenza Verticale)
        bool verticalOverlap = rect1.xMax > rect2.xMin && rect2.xMax > rect1.xMin;
        bool verticalTouch = rect1.yMax == rect2.yMin || rect2.yMax == rect1.yMin;
        if (verticalOverlap && verticalTouch) return true;

        return false;
    }

}
