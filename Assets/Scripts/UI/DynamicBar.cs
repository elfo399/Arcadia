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
    public bool resizeWithMax = false;
    // Minimum width of the bar frame
    public float baseWidth = 120f;
    // Additional width per maximum point
    public float widthPerPoint = 1f;
    // Padding applied horizontally to the fill
    public float horizontalPadding = 10f;

    // Width authored in the scene/prefab, captured once and reused for fixed bars.
    private float authoredBaseWidth = -1f;

    // Maximum value represented by the bar
    private float maxValue  = 1f;
    // Current value represented by the fill
    private float currentValue = 1f;

    private void Awake()
    {
        CacheAuthoredWidth();
    }

    private void OnEnable()
    {
        CacheAuthoredWidth();
    }

    private void CacheAuthoredWidth()
    {
        if (frameRect == null)
            frameRect = GetComponent<RectTransform>();

        if (frameRect == null)
            return;

        float width = frameRect.rect.width;
        if (width > 0f)
            authoredBaseWidth = width;
    }

    // Resize the frame and fill according to the new maximum
    public void SetMax(float max)
    {
        maxValue = Mathf.Max(1f, max);

        if (frameRect == null)
            frameRect = GetComponent<RectTransform>();

        if (authoredBaseWidth <= 0f)
            CacheAuthoredWidth();

        float fixedWidth = authoredBaseWidth > 0f ? authoredBaseWidth : baseWidth;
        float newWidth = resizeWithMax
            ? baseWidth + maxValue * widthPerPoint
            : fixedWidth;

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
