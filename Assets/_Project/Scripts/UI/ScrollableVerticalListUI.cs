using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls a ScrollRect authored and linked in the Inspector.
/// This component never creates UI objects or components at runtime.
/// </summary>
[DisallowMultipleComponent]
public sealed class ScrollableVerticalListUI : MonoBehaviour
{
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform content;
    [SerializeField] private Scrollbar scrollbar;

    public void Refresh(bool resetToTop = false)
    {
        if (scrollRect == null || viewport == null || content == null)
            return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        Canvas.ForceUpdateCanvases();

        bool hasOverflow = content.rect.height > viewport.rect.height + 0.5f;
        if (scrollbar != null && scrollbar.gameObject.activeSelf != hasOverflow)
            scrollbar.gameObject.SetActive(hasOverflow);

        if (!resetToTop)
            return;

        scrollRect.StopMovement();
        scrollRect.verticalNormalizedPosition = 1f;
    }

    public void EnsureVisible(RectTransform row)
    {
        if (scrollRect == null || viewport == null || content == null || row == null)
            return;

        Refresh(false);
        Bounds contentBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, content);
        float hiddenHeight = contentBounds.size.y - viewport.rect.height;
        if (hiddenHeight <= 0.01f)
            return;

        Bounds rowBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, row);
        const float edgePadding = 2f;
        float normalizedPosition = scrollRect.verticalNormalizedPosition;
        float overflowBelow = viewport.rect.yMin + edgePadding - rowBounds.min.y;
        if (overflowBelow > 0f)
            normalizedPosition -= overflowBelow / hiddenHeight;
        else
        {
            float overflowAbove = rowBounds.max.y - (viewport.rect.yMax - edgePadding);
            if (overflowAbove > 0f)
                normalizedPosition += overflowAbove / hiddenHeight;
        }

        scrollRect.StopMovement();
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalizedPosition);
    }
}
