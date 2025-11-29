using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(EnemyHealth))]
public class EnemySetup : MonoBehaviour
{
    [Header("Configurazione Default")]
    public float defaultHeightOffset = 2.0f; // Usato solo se non c'è collider
    public float healthBarScale = 0.01f; 

    [Header("Prefab Barra (Opzionale)")]
    public GameObject customHealthBarPrefab;

    void Awake()
    {
        // 1. Creiamo (o troviamo) il punto della testa automaticamente
        Transform headPoint = GetOrCreateHeadPoint();

        // 2. Usiamo quel punto per piazzare tutto il resto
        SetupLockOnPoint(headPoint);
        SetupHealthBar(headPoint);
    }

    Transform GetOrCreateHeadPoint()
    {
        // Se l'hai messo a mano, usiamo quello
        Transform existing = transform.Find("HeadPoint");
        if (existing != null) return existing;

        // Altrimenti lo creiamo noi matematicamente
        GameObject headObj = new GameObject("HeadPoint");
        headObj.transform.SetParent(transform);
        
        // Calcoliamo l'altezza in base al Collider (se c'è)
        float yPos = defaultHeightOffset;
        
        CapsuleCollider col = GetComponent<CapsuleCollider>();
        if (col != null)
        {
            // Centro + Metà Altezza = Cima della testa
            yPos = col.center.y + (col.height / 2f);
        }
        else
        {
            // Fallback sul CharacterController se presente
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null)
            {
                yPos = cc.center.y + (cc.height / 2f);
            }
        }

        // Posizioniamo il punto
        headObj.transform.localPosition = new Vector3(0, yPos, 0);
        
        return headObj.transform;
    }

    void SetupLockOnPoint(Transform headPoint)
    {
        if (transform.Find("LockOnPoint") != null) return;

        GameObject lockPoint = new GameObject("LockOnPoint");
        lockPoint.transform.SetParent(transform);
        
        // Il lock-on va al PETTO, quindi 0.5 metri sotto la testa
        float lockY = headPoint.localPosition.y - 0.5f;
        lockPoint.transform.localPosition = new Vector3(0, lockY, 0);
    }

    void SetupHealthBar(Transform headPoint)
    {
        EnemyHealth healthScript = GetComponent<EnemyHealth>();
        if (healthScript == null || healthScript.healthBar != null) return;

        GameObject canvasObj = new GameObject("HealthBarCanvas");
        canvasObj.transform.SetParent(transform);
        
        // La barra va 0.3 metri SOPRA la testa
        float barY = headPoint.localPosition.y + 0.3f;
        canvasObj.transform.localPosition = new Vector3(0, barY, 0);
        
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasObj.transform.localScale = new Vector3(healthBarScale, healthBarScale, healthBarScale);
        
        RectTransform rt = canvasObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(150, 20);

        // --- Creazione Grafica Barra (uguale a prima) ---
        GameObject sliderObj;
        Slider sliderComponent;

        if (customHealthBarPrefab != null)
        {
            sliderObj = Instantiate(customHealthBarPrefab, canvasObj.transform);
            sliderObj.transform.localPosition = Vector3.zero;
            sliderComponent = sliderObj.GetComponent<Slider>();
        }
        else
        {
            sliderObj = new GameObject("HP_Bar");
            sliderObj.transform.SetParent(canvasObj.transform);
            sliderObj.transform.localPosition = Vector3.zero;
            sliderObj.transform.localScale = Vector3.one;
            
            sliderComponent = sliderObj.AddComponent<Slider>();
            
            // Background
            GameObject bg = new GameObject("Background");
            bg.transform.SetParent(sliderObj.transform);
            bg.transform.localPosition = Vector3.zero;
            bg.transform.localScale = Vector3.one;
            Image bgImg = bg.AddComponent<Image>();
            bgImg.color = Color.black;
            ExpandToFill(bg.GetComponent<RectTransform>());

            // Fill Area
            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform);
            fillArea.transform.localPosition = Vector3.zero;
            fillArea.transform.localScale = Vector3.one;
            RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
            ExpandToFill(fillAreaRect);
            fillAreaRect.offsetMin = new Vector2(2, 2);
            fillAreaRect.offsetMax = new Vector2(-2, -2);

            // Fill
            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform);
            fill.transform.localPosition = Vector3.zero;
            fill.transform.localScale = Vector3.one;
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = Color.red;
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            ExpandToFill(fillRect);

            sliderComponent.targetGraphic = bgImg;
            sliderComponent.fillRect = fillRect;
            sliderComponent.direction = Slider.Direction.LeftToRight;
            
            ExpandToFill(sliderObj.GetComponent<RectTransform>());
        }

        if (sliderComponent != null)
        {
            EnemyHealthBar barScript = sliderObj.GetComponent<EnemyHealthBar>();
            if (barScript == null) barScript = sliderObj.AddComponent<EnemyHealthBar>();
            
            barScript.slider = sliderComponent;
            healthScript.healthBar = barScript;
        }
    }

    void ExpandToFill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}