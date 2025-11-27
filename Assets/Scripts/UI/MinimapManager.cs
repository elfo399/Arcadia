using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MinimapManager : MonoBehaviour
{
    public static MinimapManager instance;

    [Header("Riferimenti UI")]
    public RectTransform mapContainer; 
    public GameObject roomIconPrefab;

    [Header("Icone Speciali")]
    public Sprite skullIcon;   // Boss
    public Sprite crownIcon;   // Treasure
    public Sprite startIcon;   // Start

    [Header("Colori")]
    public Color currentRoomColor = Color.white;
    public Color normalRoomColor = new Color(0.3f, 0.3f, 0.3f, 1f); 
    public Color specialRoomColor = new Color(0.3f, 0.3f, 0.3f, 1f);

    [Header("Settings")]
    public float iconBaseSize = 20f; 
    public float iconSpacing = 0f;   

    private Dictionary<Vector2Int, Image> gridIcons = new Dictionary<Vector2Int, Image>();
    private Vector2Int lastGridPos = new Vector2Int(-999, -999);
    private float FullStep => iconBaseSize + iconSpacing;

    void Awake() { if (instance == null) instance = this; }

    public void ClearMap()
    {
        foreach (Transform child in mapContainer) Destroy(child.gameObject);
        gridIcons.Clear();
    }

    // --- ORA ACCETTA ROOMDATA ---
    public void RegisterRoom(Vector2Int gridPos, RoomData data)
    {
        if (data == null) return;

        GameObject newIconObj = Instantiate(roomIconPrefab, mapContainer);
        RectTransform rt = newIconObj.GetComponent<RectTransform>();

        // Ancore e Pivot
        rt.pivot = new Vector2(0, 0);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;

        // Posizione
        Vector2 uiPos = new Vector2(gridPos.x * FullStep, gridPos.y * FullStep);
        rt.anchoredPosition = uiPos;

        // Calcolo Dimensioni basato su data.size
        float width = (data.size.x * iconBaseSize) + ((data.size.x - 1) * iconSpacing);
        float height = (data.size.y * iconBaseSize) + ((data.size.y - 1) * iconSpacing);
        rt.sizeDelta = new Vector2(width, height);

        // Grafica
        Image baseImage = newIconObj.GetComponent<Image>();
        Image fillImage = null;
        Transform fillTrans = newIconObj.transform.Find("RoomFill");
        
        if (fillTrans != null) fillImage = fillTrans.GetComponent<Image>();
        else fillImage = baseImage; // Fallback

        fillImage.color = normalRoomColor;

        // --- GESTIONE ICONE BASATA SUI TUOI BOOL ---
        Transform overlayTrans = newIconObj.transform.Find("IconOverlay");
        if (overlayTrans != null)
        {
            Image overlayImg = overlayTrans.GetComponent<Image>();
            overlayImg.gameObject.SetActive(false); 

            if (data.isBossRoom && skullIcon != null)
            {
                fillImage.color = specialRoomColor;
                overlayImg.sprite = skullIcon;
                overlayImg.gameObject.SetActive(true);
            }
            else if (data.isTreasureRoom && crownIcon != null)
            {
                fillImage.color = specialRoomColor;
                overlayImg.sprite = crownIcon;
                overlayImg.gameObject.SetActive(true);
            }
            else if (data.isStartRoom && startIcon != null)
            {
                fillImage.color = specialRoomColor; 
                overlayImg.sprite = startIcon;
                overlayImg.gameObject.SetActive(true);
            }
        }

        // Registrazione nel dizionario per l'illuminazione
        for (int x = 0; x < data.size.x; x++) {
            for (int y = 0; y < data.size.y; y++) {
                Vector2Int cell = gridPos + new Vector2Int(x, y);
                if (!gridIcons.ContainsKey(cell)) gridIcons.Add(cell, fillImage);
            }
        }
    }

    public void UpdatePlayerPosition(Vector3 worldPos, float roomSize)
    {
        int gridX = Mathf.RoundToInt(worldPos.x / roomSize);
        int gridY = Mathf.RoundToInt(worldPos.z / roomSize);
        Vector2Int currentGridPos = new Vector2Int(gridX, gridY);

        Vector2 targetPos = -1 * new Vector2(gridX * FullStep, gridY * FullStep);
        mapContainer.anchoredPosition = Vector2.Lerp(mapContainer.anchoredPosition, targetPos, Time.deltaTime * 5f);

        if (currentGridPos == lastGridPos) return;

        if (gridIcons.ContainsKey(lastGridPos) && gridIcons[lastGridPos] != null) 
            gridIcons[lastGridPos].color = normalRoomColor;

        if (gridIcons.ContainsKey(currentGridPos) && gridIcons[currentGridPos] != null) 
            gridIcons[currentGridPos].color = currentRoomColor;

        lastGridPos = currentGridPos;
    }
}