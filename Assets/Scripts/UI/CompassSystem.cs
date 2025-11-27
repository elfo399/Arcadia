using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CompassSystem : MonoBehaviour
{
    [Header("Riferimenti")]
    public Transform cameraTransform; // La Main Camera
    public RectTransform compassBarRect; // L'oggetto "CompassBar" (la maschera)
    
    [Header("Icone Cardinali (Trascina gli oggetti UI qui)")]
    public RectTransform iconNorth;
    public RectTransform iconSouth;
    public RectTransform iconEast;
    public RectTransform iconWest;

    [Header("Settings")]
    public float compassWidth = 400f; // DEVE ESSERE UGUALE alla Width della CompassBar
    public float viewAngle = 100f;    // Quanti gradi di mondo vedi nella barra (es. 90-100 è standard)

    // Lista per gestire tutto insieme
    private List<CompassMarker> markers = new List<CompassMarker>();

    // Classe interna per gestire ogni icona
    class CompassMarker
    {
        public RectTransform uiElement;
        public Vector3 worldDirection; // Per i punti cardinali (es. Vector3.forward)
        // public Transform targetObject; // Per i nemici (non usato ora ma predisposto)

        public CompassMarker(RectTransform ui, Vector3 dir)
        {
            uiElement = ui;
            worldDirection = dir;
        }
    }

    void Start()
    {
        if (cameraTransform == null) cameraTransform = Camera.main.transform;
        
        // Imposta la larghezza interna se non settata
        if (compassBarRect != null) compassWidth = compassBarRect.rect.width;

        // Registriamo i 4 punti cardinali
        // Nord = Forward (0,0,1)
        // Sud = Back (0,0,-1)
        // Est = Right (1,0,0)
        // Ovest = Left (-1,0,0)
        AddMarker(iconNorth, Vector3.forward);
        AddMarker(iconSouth, Vector3.back);
        AddMarker(iconEast, Vector3.right);
        AddMarker(iconWest, Vector3.left);
    }

    void AddMarker(RectTransform icon, Vector3 direction)
    {
        if (icon != null)
            markers.Add(new CompassMarker(icon, direction));
    }

    void Update()
    {
        foreach (var marker in markers)
        {
            UpdateMarkerPosition(marker);
        }
    }

    void UpdateMarkerPosition(CompassMarker marker)
    {
        // 1. Calcola l'angolo tra dove guardo io (Camera Forward) e il target
        // Usiamo SignedAngle per avere valori positivi (destra) e negativi (sinistra)
        // Proiettiamo tutto sul piano orizzontale (ignoriamo l'altezza Y)
        
        Vector3 camForward = cameraTransform.forward;
        camForward.y = 0;
        camForward.Normalize();

        Vector3 targetDir = marker.worldDirection;
        targetDir.y = 0;
        targetDir.Normalize();

        float angle = Vector3.SignedAngle(camForward, targetDir, Vector3.up);

        // 2. Controlla se è nel campo visivo della bussola
        // Se l'angolo è maggiore di metà viewAngle, è fuori schermo
        if (angle > -viewAngle / 2 && angle < viewAngle / 2)
        {
            marker.uiElement.gameObject.SetActive(true);

            // 3. Mappa l'angolo in pixel UI
            // Esempio: se angle è 0 (centro), x = 0.
            // Se angle è 45 gradi, x = (45 / viewAngle) * width
            float uiX = (angle / viewAngle) * compassWidth;

            marker.uiElement.anchoredPosition = new Vector2(uiX, 0);
        }
        else
        {
            // Nascondi se è fuori dalla barra (es. è alle mie spalle)
            marker.uiElement.gameObject.SetActive(false);
        }
    }
}