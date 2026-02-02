using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Linq;

public class MinimapManager : MonoBehaviour
{
    public static MinimapManager instance;

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

    // --- STRUTTURE DATI PER FOG OF WAR ---
    private Dictionary<Vector2Int, GameObject> _roomIconObjects = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, RoomData> _roomData = new Dictionary<Vector2Int, RoomData>();
    private HashSet<Vector2Int> _visitedRoomAnchors = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> _revealedRoomAnchors = new HashSet<Vector2Int>(); // Stanze da mostrare permanentemente
    private Vector2Int _lastPlayerRoomAnchor = new Vector2Int(-999, -999);
    
    private float FullStep => iconBaseSize + iconSpacing;

    void Awake() 
    { 
        if (instance == null) instance = this; 
    }

    void Start()
    {
        if (SceneManager.GetActiveScene().name == "HubScene" && mapContainer != null)
        {
            mapContainer.gameObject.SetActive(false);
        }
    }

    public void ClearMap()
    {
        foreach (Transform child in mapContainer) Destroy(child.gameObject);
        _roomIconObjects.Clear();
        _visitedRoomAnchors.Clear();
        _revealedRoomAnchors.Clear();
        _roomData.Clear();
        _lastPlayerRoomAnchor = new Vector2Int(-999,-999);
    }

    public void RegisterRoom(Vector2Int gridPos, RoomData data)
    {
        if (data == null || _roomData.ContainsKey(gridPos)) return;

        GameObject newIconObj = Instantiate(roomIconPrefab, mapContainer);
        _roomData.Add(gridPos, data);
        _roomIconObjects.Add(gridPos, newIconObj);
        
        SetupIconVisuals(newIconObj, gridPos, data);
        
        newIconObj.SetActive(false);
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
        _lastPlayerRoomAnchor = startPosAnchor;
        UpdateMapVisibility(startPosAnchor);
    }

    public void UpdatePlayerPosition(Vector3 worldPos, float roomSize)
    {
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
                    fillImage.color = currentRoomColor;
                }
                else if (_visitedRoomAnchors.Contains(roomAnchor))
                {
                    fillImage.color = visitedRoomColor;
                }
                else // Rivelata ma non visitata (quindi adiacente a una visitata)
                {
                    fillImage.color = adjacentRoomColor;
                }
            }
            else
            {
                iconObj.SetActive(false);
            }
        }
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