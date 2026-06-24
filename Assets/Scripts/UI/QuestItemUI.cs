using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI questNameText;
    [SerializeField] private TextMeshProUGUI questLocationText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject completedIndicator;
    [Tooltip("Sprite shown on the right when this quest's reward can be claimed.")]
    [SerializeField] private GameObject claimableRewardIndicator;
    [SerializeField] private Color selectionBorderColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Vector2 selectionBorderThickness = new Vector2(3f, 3f);

    private Color defaultBackgroundColor = Color.clear;
    private bool hasDefaultBackgroundColor;
    private QuestSelectionFrameEffect[] selectionFrameEffects;
    private bool isSelected;
    private bool isFocused;

    public Graphic SelectionGraphic
    {
        get
        {
            ResolveReferences();
            return backgroundImage;
        }
    }

    private void Awake()
    {
        ResolveReferences();
        CacheDefaultBackgroundColor();
        SetSelected(false);
    }

    public void SetData(string title, string location, bool completed, bool rewardClaimable = false)
    {
        ResolveReferences();

        if (questNameText != null)
            questNameText.text = string.IsNullOrWhiteSpace(title) ? "New Quest" : title;

        if (questLocationText != null)
            questLocationText.text = string.IsNullOrWhiteSpace(location) ? "Unknown" : location;

        if (completedIndicator != null)
            completedIndicator.SetActive(completed);

        if (claimableRewardIndicator != null)
            claimableRewardIndicator.SetActive(rewardClaimable);
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        RefreshSelectionVisual();
    }

    public void SetFocused(bool focused)
    {
        isFocused = focused;
        RefreshSelectionVisual();
    }

    private void RefreshSelectionVisual()
    {
        ResolveReferences();
        CacheDefaultBackgroundColor();
        bool highlighted = isSelected || isFocused;

        if (backgroundImage != null && hasDefaultBackgroundColor)
            backgroundImage.color = defaultBackgroundColor;

        EnsureSelectionFrameEffects();
        for (int i = 0; i < selectionFrameEffects.Length; i++)
        {
            if (selectionFrameEffects[i] != null)
                selectionFrameEffects[i].enabled = highlighted;
        }

    }

    private void ResolveReferences()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (backgroundImage == null)
        {
            var panel = transform.Find("Panel");
            if (panel != null)
                backgroundImage = panel.GetComponent<Image>();
        }

        if (questNameText == null)
        {
            var nameTransform = transform.Find("Name");
            if (nameTransform != null)
                questNameText = nameTransform.GetComponent<TextMeshProUGUI>();

            if (questNameText == null)
            {
                var titleTransform = transform.Find("Title");
                if (titleTransform != null)
                    questNameText = titleTransform.GetComponent<TextMeshProUGUI>();
            }

            if (questNameText == null)
            {
                var panelTitle = transform.Find("Panel/Title");
                if (panelTitle != null)
                    questNameText = panelTitle.GetComponent<TextMeshProUGUI>();
            }
        }

        if (questLocationText == null)
        {
            var locationTransform = transform.Find("Location");
            if (locationTransform != null)
                questLocationText = locationTransform.GetComponent<TextMeshProUGUI>();

            if (questLocationText == null)
            {
                var panelLocation = transform.Find("Panel/Location");
                if (panelLocation != null)
                    questLocationText = panelLocation.GetComponent<TextMeshProUGUI>();
            }

            if (questLocationText == null)
            {
                var panelLoction = transform.Find("Panel/Loction");
                if (panelLoction != null)
                    questLocationText = panelLoction.GetComponent<TextMeshProUGUI>();
            }
        }

        if (completedIndicator == null)
        {
            var completedTransform = transform.Find("Completed");
            if (completedTransform == null)
                completedTransform = transform.Find("Panel/Completed");

            if (completedTransform != null)
                completedIndicator = completedTransform.gameObject;
            else
            {
                var allTransforms = GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < allTransforms.Length; i++)
                {
                    var t = allTransforms[i];
                    if (t == null) continue;
                    if (t == transform) continue;
                    if (t.name.ToLowerInvariant().Contains("completed"))
                    {
                        completedIndicator = t.gameObject;
                        break;
                    }
                }
            }
        }

        if (questNameText == null || questLocationText == null)
        {
            var allTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < allTexts.Length; i++)
            {
                if (allTexts[i] == null) continue;

                string n = allTexts[i].gameObject.name.ToLowerInvariant();
                if (questNameText == null && (n.Contains("name") || n.Contains("title"))) questNameText = allTexts[i];
                if (questLocationText == null && (n.Contains("location") || n.Contains("loction"))) questLocationText = allTexts[i];
            }
        }
    }

    private void CacheDefaultBackgroundColor()
    {
        if (hasDefaultBackgroundColor || backgroundImage == null)
            return;

        defaultBackgroundColor = backgroundImage.color;
        hasDefaultBackgroundColor = true;
    }

    private void EnsureSelectionFrameEffects()
    {
        if (selectionFrameEffects != null)
            return;

        var childImages = GetComponentsInChildren<Image>(true);
        var effects = new System.Collections.Generic.List<QuestSelectionFrameEffect>();
        float horizontalThickness = Mathf.Max(1f, selectionBorderThickness.x);
        float verticalThickness = Mathf.Max(1f, selectionBorderThickness.y);

        for (int i = 0; i < childImages.Length; i++)
        {
            var image = childImages[i];
            if (image == null || image.transform.parent != transform)
                continue;
            if (image == backgroundImage || image.sprite == null)
                continue;
            if (completedIndicator != null && image.gameObject == completedIndicator)
                continue;
            if (claimableRewardIndicator != null && image.gameObject == claimableRewardIndicator)
                continue;

            RectTransform rect = image.rectTransform;
            float horizontalOffset;
            if (rect.anchorMax.x - rect.anchorMin.x > 0.5f)
                horizontalOffset = 0f;
            else if (rect.anchorMin.x >= 0.5f)
                horizontalOffset = horizontalThickness;
            else
                horizontalOffset = -horizontalThickness;

            var effect = image.GetComponent<QuestSelectionFrameEffect>();
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
