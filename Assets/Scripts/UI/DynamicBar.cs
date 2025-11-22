using UnityEngine;
using UnityEngine.UI;

public class DynamicBar : MonoBehaviour
{
    [Header("References")]
    public RectTransform frameRect;   // il frame da allargare (bordo)
    public Image fillImage;           // la barra interna (Filled)

    [Header("Settings")]
    public float baseWidth = 120f;      // larghezza minima del frame
    public float widthPerPoint = 1f;    // quanti pixel per ogni punto di max
    public float horizontalPadding = 10f; // margine sinistra/destra del fill

    private float maxValue  = 1f;
    private float currentValue = 1f;

    // MAX = cambia la dimensione del frame in base al valore massimo
    public void SetMax(float max)
    {
        maxValue = Mathf.Max(1f, max);   // evita 0

        if (frameRect == null)
            frameRect = GetComponent<RectTransform>();

        // 1) Calcola larghezza del FRAME
        float newWidth = baseWidth + maxValue * widthPerPoint;
        frameRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, newWidth);

        // 2) Adatta il FILL alla nuova larghezza del frame
        if (fillImage != null)
        {
            RectTransform fillRect = fillImage.rectTransform;

            float fillWidth = newWidth - horizontalPadding * 2f;
            if (fillWidth < 0f) fillWidth = 0f;

            // larghezza del fill
            fillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, fillWidth);
            // posizione del fill (spostato del padding da sinistra)
            fillRect.anchoredPosition = new Vector2(horizontalPadding, fillRect.anchoredPosition.y);
        }

        // 3) aggiorna il riempimento con il nuovo max
        SetCurrent(currentValue);
    }

    // CURRENT = aggiorna solo il fill (quanto è piena la barra)
    public void SetCurrent(float current)
    {
        currentValue = Mathf.Clamp(current, 0f, maxValue);

        if (fillImage != null)
            fillImage.fillAmount = currentValue / maxValue;
    }
}
