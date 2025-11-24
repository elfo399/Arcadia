using UnityEngine;
using UnityEngine.UI;

public class DynamicBar : MonoBehaviour
{
    [Header("References")]
    // Frame that resizes based on maximum value
    public RectTransform frameRect;
    // Fill image representing the current amount
    public Image fillImage;

    [Header("Settings")]
    // Minimum width of the bar frame
    public float baseWidth = 120f;
    // Additional width per maximum point
    public float widthPerPoint = 1f;
    // Padding applied horizontally to the fill
    public float horizontalPadding = 10f;

    // Maximum value represented by the bar
    private float maxValue  = 1f;
    // Current value represented by the fill
    private float currentValue = 1f;

    // Resize the frame and fill according to the new maximum
    public void SetMax(float max)
    {
        maxValue = Mathf.Max(1f, max);

        if (frameRect == null)
            frameRect = GetComponent<RectTransform>();

        float newWidth = baseWidth + maxValue * widthPerPoint;
        frameRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, newWidth);

        if (fillImage != null)
        {
            RectTransform fillRect = fillImage.rectTransform;

            float fillWidth = newWidth - horizontalPadding * 2f;
            if (fillWidth < 0f) fillWidth = 0f;

            fillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, fillWidth);
            fillRect.anchoredPosition = new Vector2(horizontalPadding, fillRect.anchoredPosition.y);
        }

        SetCurrent(currentValue);
    }

    // Update the fill amount according to the current value
    public void SetCurrent(float current)
    {
        currentValue = Mathf.Clamp(current, 0f, maxValue);

        if (fillImage != null)
            fillImage.fillAmount = currentValue / maxValue;
    }
}
