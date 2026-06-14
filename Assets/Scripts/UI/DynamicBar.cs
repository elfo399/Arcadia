using UnityEngine;
using UnityEngine.UI;

public class DynamicBar : MonoBehaviour
{
    [Header("References")]
    // Frame that resizes based on maximum value
    public RectTransform frameRect;
    // Fill image representing the current amount
    public Image fillImage;

    [Header("Segmented Frame")]
    public RectTransform frameStart;
    public RectTransform frameMiddle;
    public RectTransform frameEnd;

    [Header("Segmented Fill")]
    public RectTransform fillStart;
    public RectTransform fillMiddle;
    public RectTransform fillEnd;

    [Header("Settings")]
    public bool resizeWithMax = false;
    // Minimum width of the bar frame
    public float baseWidth = 120f;
    // Additional width per maximum point
    public float widthPerPoint = 1f;
    // Padding applied horizontally to the fill
    public float horizontalPadding = 10f;

    [Header("Solid Fill")]
    public bool resizeFillImageInsteadOfFillAmount = false;
    public bool clearFillSpriteForSolidColor = false;
    public float solidFillHeight = 10f;

    [Header("Layout")]
    public bool updateLayoutElement = true;

    // Width authored in the scene/prefab, captured once and reused for fixed bars.
    private float authoredBaseWidth = -1f;
    private float authoredFrameStartWidth = -1f;
    private float authoredFrameEndWidth = -1f;
    private float authoredFillStartWidth = -1f;
    private float authoredFillEndWidth = -1f;
    private float currentVisualWidth = -1f;
    private LayoutElement layoutElement;

    // Maximum value represented by the bar
    private float maxValue  = 1f;
    // Current value represented by the fill
    private float currentValue = 1f;

    public bool UsesSegmentedLayout => HasSegmentedFrame() || HasSegmentedFill();

    public void SetFillColor(Color color)
    {
        SetImageColor(fillImage, color);
        SetRectImageColor(fillStart, color);
        SetRectImageColor(fillMiddle, color);
        SetRectImageColor(fillEnd, color);
    }

    private void Awake()
    {
        CacheAuthoredWidth();
    }

    private void OnEnable()
    {
        CacheAuthoredWidth();
    }

    private void OnValidate()
    {
        CacheAuthoredWidth();
        ConfigureSolidFillImage();
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

        CacheSegmentWidth(frameStart, ref authoredFrameStartWidth);
        CacheSegmentWidth(frameEnd, ref authoredFrameEndWidth);
        CacheSegmentWidth(fillStart, ref authoredFillStartWidth);
        CacheSegmentWidth(fillEnd, ref authoredFillEndWidth);
    }

    private static void CacheSegmentWidth(RectTransform rect, ref float cachedWidth)
    {
        if (rect == null || cachedWidth > 0f)
            return;

        float width = rect.rect.width;
        if (width <= 0f)
            width = rect.sizeDelta.x;

        if (width > 0f)
            cachedWidth = width;
    }

    // Resize the frame and fill according to the new maximum
    public void SetMax(float max)
    {
        maxValue = Mathf.Max(1f, max);

        if (frameRect == null)
            frameRect = GetComponent<RectTransform>();

        if (authoredBaseWidth <= 0f)
            CacheAuthoredWidth();

        SetWidth(CalculateWidthForMax());
        SetCurrent(currentValue);
    }

    public void SetMax(float max, float width)
    {
        maxValue = Mathf.Max(1f, max);

        if (frameRect == null)
            frameRect = GetComponent<RectTransform>();

        if (authoredBaseWidth <= 0f)
            CacheAuthoredWidth();

        SetWidth(Mathf.Max(0f, width));
        SetCurrent(currentValue);
    }

    private float CalculateWidthForMax()
    {
        float fixedWidth = authoredBaseWidth > 0f ? authoredBaseWidth : baseWidth;
        return resizeWithMax
            ? baseWidth + maxValue * widthPerPoint
            : fixedWidth;
    }

    private void SetWidth(float newWidth)
    {
        currentVisualWidth = newWidth;

        if (updateLayoutElement)
            ApplyLayoutWidth(newWidth);

        if (frameRect != null)
            frameRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, newWidth);

        if (HasSegmentedFrame())
        {
            LayoutThreePart(
                frameStart,
                frameMiddle,
                frameEnd,
                authoredFrameStartWidth,
                authoredFrameEndWidth,
                0f,
                newWidth);
            EnsureFrameDrawOrder();
        }

        if (!HasSegmentedFill())
            ResizeLegacyFillBounds(newWidth);
    }

    private void ResizeLegacyFillBounds(float newWidth)
    {
        if (fillImage == null)
            return;

        RectTransform fillRect = fillImage.rectTransform;
        float fillWidth = Mathf.Max(0f, newWidth - horizontalPadding * 2f);

        fillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, fillWidth);
        fillRect.anchoredPosition = new Vector2(horizontalPadding, fillRect.anchoredPosition.y);
    }

    // Update the fill amount according to the current value
    public void SetCurrent(float current)
    {
        currentValue = Mathf.Clamp(current, 0f, maxValue);

        float ratio = currentValue / maxValue;
        if (HasSegmentedFill())
        {
            float totalWidth = GetFrameWidth();
            float fillWidth = Mathf.Max(0f, totalWidth - horizontalPadding * 2f) * ratio;
            LayoutThreePart(
                fillStart,
                fillMiddle,
                fillEnd,
                authoredFillStartWidth,
                authoredFillEndWidth,
                horizontalPadding,
                fillWidth);
            return;
        }

        if (fillImage != null)
        {
            if (resizeFillImageInsteadOfFillAmount)
                ResizeSolidFill(ratio);
            else
                fillImage.fillAmount = ratio;
        }
    }

    private void ResizeSolidFill(float ratio)
    {
        if (fillImage == null)
            return;

        ConfigureSolidFillImage();

        RectTransform fillRect = fillImage.rectTransform;
        float fillWidth = Mathf.Max(0f, GetFrameWidth() - horizontalPadding * 2f) * Mathf.Clamp01(ratio);

        fillRect.anchorMin = new Vector2(0f, fillRect.anchorMin.y);
        fillRect.anchorMax = new Vector2(0f, fillRect.anchorMax.y);
        fillRect.pivot = new Vector2(0f, fillRect.pivot.y);
        if (solidFillHeight > 0f)
            fillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, solidFillHeight);
        fillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, fillWidth);
        fillRect.anchoredPosition = new Vector2(horizontalPadding, fillRect.anchoredPosition.y);
    }

    private void ConfigureSolidFillImage()
    {
        if (!resizeFillImageInsteadOfFillAmount || fillImage == null)
            return;

        if (clearFillSpriteForSolidColor)
            fillImage.sprite = null;

        fillImage.type = Image.Type.Simple;
        fillImage.fillAmount = 1f;
    }

    private void EnsureFrameDrawOrder()
    {
        if (frameStart != null)
            frameStart.SetAsLastSibling();
        if (frameMiddle != null)
            frameMiddle.SetAsLastSibling();
        if (frameEnd != null)
            frameEnd.SetAsLastSibling();
    }

    private float GetFrameWidth()
    {
        if (currentVisualWidth > 0f)
            return currentVisualWidth;

        if (frameRect != null)
        {
            float width = frameRect.rect.width;
            if (width > 0f)
                return width;
        }

        return CalculateWidthForMax();
    }

    private void ApplyLayoutWidth(float width)
    {
        if (frameRect == null)
            return;

        if (layoutElement == null)
            layoutElement = frameRect.GetComponent<LayoutElement>();

        if (layoutElement == null)
            layoutElement = frameRect.gameObject.AddComponent<LayoutElement>();

        layoutElement.minWidth = width;
        layoutElement.preferredWidth = width;
        layoutElement.flexibleWidth = 0f;
    }

    private bool HasSegmentedFrame()
    {
        return frameStart != null || frameMiddle != null || frameEnd != null;
    }

    private bool HasSegmentedFill()
    {
        return fillStart != null || fillMiddle != null || fillEnd != null;
    }

    private static void LayoutThreePart(
        RectTransform start,
        RectTransform middle,
        RectTransform end,
        float authoredStartWidth,
        float authoredEndWidth,
        float offsetX,
        float totalWidth)
    {
        float startNaturalWidth = authoredStartWidth > 0f ? authoredStartWidth : GetCurrentWidth(start);
        float endNaturalWidth = authoredEndWidth > 0f ? authoredEndWidth : GetCurrentWidth(end);

        float startWidth = Mathf.Min(startNaturalWidth, totalWidth);
        float remainingAfterStart = Mathf.Max(0f, totalWidth - startWidth);
        float endWidth = Mathf.Min(endNaturalWidth, remainingAfterStart);
        float middleWidth = Mathf.Max(0f, totalWidth - startWidth - endWidth);

        ApplySegment(start, offsetX, startWidth);
        ApplySegment(middle, offsetX + startWidth, middleWidth);
        ApplySegment(end, offsetX + startWidth + middleWidth, endWidth);
    }

    private static float GetCurrentWidth(RectTransform rect)
    {
        if (rect == null)
            return 0f;

        float width = rect.rect.width;
        if (width > 0f)
            return width;

        return Mathf.Max(0f, rect.sizeDelta.x);
    }

    private static void ApplySegment(RectTransform rect, float x, float width)
    {
        if (rect == null)
            return;

        rect.gameObject.SetActive(width > 0.01f);
        rect.anchorMin = new Vector2(0f, rect.anchorMin.y);
        rect.anchorMax = new Vector2(0f, rect.anchorMax.y);
        rect.pivot = new Vector2(0f, rect.pivot.y);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
    }

    private static void SetRectImageColor(RectTransform rect, Color color)
    {
        if (rect == null)
            return;

        SetImageColor(rect.GetComponent<Image>(), color);
    }

    private static void SetImageColor(Image image, Color color)
    {
        if (image == null)
            return;

        image.color = color;
    }
}
