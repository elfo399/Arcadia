using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProgressBarUI : MonoBehaviour
{
    public enum FillDirection
    {
        LeftToRight,
        RightToLeft,
        BottomToTop,
        TopToBottom
    }

    [SerializeField] private RectTransform fillRect;
    [SerializeField] private Image fillImage;
    [SerializeField] private Color progressColor = Color.red;
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private FillDirection fillDirection = FillDirection.LeftToRight;
    [SerializeField, Range(0f, 1f)] private float value = 1f;
    [SerializeField] private bool hideFillWhenEmpty = true;

    public float Value => value;
    public Color ProgressColor => progressColor;

    private void Awake()
    {
        ApplyVisuals();
        ApplyProgress();
    }

    private void OnValidate()
    {
        value = Mathf.Clamp01(value);
        ApplyVisuals();
        ApplyProgress();
    }

    public void SetProgress(float normalized)
    {
        value = Mathf.Clamp01(normalized);
        ApplyProgress();
    }

    public void SetProgress(float normalized, string displayText)
    {
        SetProgress(normalized);
        SetDisplayText(displayText);
    }

    public void SetDisplayText(string displayText)
    {
        if (valueText != null)
            valueText.text = displayText ?? string.Empty;
    }

    public void SetFillColor(Color color)
    {
        progressColor = color;
        ApplyVisuals();
    }

    private void ApplyVisuals()
    {
        if (fillImage != null)
            fillImage.color = progressColor;
    }

    private void ApplyProgress()
    {
        RectTransform targetRect = fillRect;
        if (targetRect == null && fillImage != null)
            targetRect = fillImage.rectTransform;
        if (targetRect == null)
            return;

        float normalized = Mathf.Clamp01(value);

        switch (fillDirection)
        {
            case FillDirection.RightToLeft:
                targetRect.anchorMin = new Vector2(1f - normalized, 0f);
                targetRect.anchorMax = new Vector2(1f, 1f);
                break;
            case FillDirection.BottomToTop:
                targetRect.anchorMin = new Vector2(0f, 0f);
                targetRect.anchorMax = new Vector2(1f, normalized);
                break;
            case FillDirection.TopToBottom:
                targetRect.anchorMin = new Vector2(0f, 1f - normalized);
                targetRect.anchorMax = new Vector2(1f, 1f);
                break;
            default:
                targetRect.anchorMin = new Vector2(0f, 0f);
                targetRect.anchorMax = new Vector2(normalized, 1f);
                break;
        }

        targetRect.offsetMin = Vector2.zero;
        targetRect.offsetMax = Vector2.zero;

        bool shouldShowFill = normalized > 0.0001f;
        if (hideFillWhenEmpty && targetRect.gameObject.activeSelf != shouldShowFill)
            targetRect.gameObject.SetActive(shouldShowFill);
    }

}
