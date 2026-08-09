using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class DialogueChoiceUI : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TMP_Text choiceText;
    [SerializeField] private GameObject heardIndicator;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Color selectionBorderColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Vector2 selectionBorderThickness = new Vector2(3f, 3f);
    [SerializeField, Range(0.1f, 1f)] private float disabledAlpha = 0.45f;

    private Button cachedButton;
    private CanvasGroup cachedCanvasGroup;
    private QuestSelectionFrameEffect[] selectionFrameEffects;
    private bool isSelected;
    private bool isPointerOver;

    public Button Button => cachedButton != null ? cachedButton : cachedButton = GetComponent<Button>();

    private void Awake()
    {
        EnsureSelectionFrameEffects();
        RefreshSelectionVisual();
    }

    private void OnDisable()
    {
        isSelected = false;
        isPointerOver = false;
        RefreshSelectionVisual();
    }

    public void Bind(string text, bool enabled, bool showHeardIndicator)
    {
        if (choiceText != null)
            choiceText.text = text ?? string.Empty;

        if (heardIndicator != null)
            heardIndicator.SetActive(showHeardIndicator);

        Button.interactable = enabled;
        CanvasGroup canvasGroup = GetOrCreateCanvasGroup();
        canvasGroup.alpha = enabled ? 1f : disabledAlpha;
        RefreshSelectionVisual();
    }

    private CanvasGroup GetOrCreateCanvasGroup()
    {
        if (cachedCanvasGroup == null)
        {
            cachedCanvasGroup = GetComponent<CanvasGroup>();
            if (cachedCanvasGroup == null)
                cachedCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        return cachedCanvasGroup;
    }

    public void OnSelect(BaseEventData eventData)
    {
        isSelected = true;
        RefreshSelectionVisual();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        RefreshSelectionVisual();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerOver = true;
        RefreshSelectionVisual();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerOver = false;
        RefreshSelectionVisual();
    }

    private void RefreshSelectionVisual()
    {
        EnsureSelectionFrameEffects();
        bool highlighted = Button.interactable && (isSelected || isPointerOver);
        for (int i = 0; i < selectionFrameEffects.Length; i++)
        {
            if (selectionFrameEffects[i] != null)
                selectionFrameEffects[i].enabled = highlighted;
        }
    }

    private void EnsureSelectionFrameEffects()
    {
        if (selectionFrameEffects != null)
            return;

        Image[] childImages = GetComponentsInChildren<Image>(true);
        var effects = new List<QuestSelectionFrameEffect>();
        float horizontalThickness = Mathf.Max(1f, selectionBorderThickness.x);
        float verticalThickness = Mathf.Max(1f, selectionBorderThickness.y);

        for (int i = 0; i < childImages.Length; i++)
        {
            Image image = childImages[i];
            if (image == null || image.transform.parent != transform)
                continue;
            if (image == backgroundImage || image.sprite == null)
                continue;
            if (heardIndicator != null && image.gameObject == heardIndicator)
                continue;

            RectTransform rect = image.rectTransform;
            float horizontalOffset;
            if (rect.anchorMax.x - rect.anchorMin.x > 0.5f)
                horizontalOffset = 0f;
            else if (rect.anchorMin.x >= 0.5f)
                horizontalOffset = horizontalThickness;
            else
                horizontalOffset = -horizontalThickness;

            QuestSelectionFrameEffect effect = image.GetComponent<QuestSelectionFrameEffect>();
            if (effect == null)
                effect = image.gameObject.AddComponent<QuestSelectionFrameEffect>();

            effect.Configure(
                selectionBorderColor,
                new Vector2(horizontalOffset, -verticalThickness),
                new Vector2(horizontalOffset, verticalThickness));
            effect.enabled = false;
            effects.Add(effect);
        }

        selectionFrameEffects = effects.ToArray();
    }
}
