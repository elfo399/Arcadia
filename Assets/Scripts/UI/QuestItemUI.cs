using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI questNameText;
    [SerializeField] private TextMeshProUGUI questLocationText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject completedIndicator;
    [SerializeField] private Outline selectionOutline;
    [SerializeField] private Color selectionBorderColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Vector2 selectionBorderThickness = new Vector2(3f, 3f);

    private Color defaultBackgroundColor = Color.clear;
    private bool hasDefaultBackgroundColor;

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
        EnsureSelectionOutline();
        SetSelected(false);
    }

    public void SetData(string title, string location, bool completed)
    {
        ResolveReferences();

        if (questNameText != null)
            questNameText.text = string.IsNullOrWhiteSpace(title) ? "New Quest" : title;

        if (questLocationText != null)
            questLocationText.text = string.IsNullOrWhiteSpace(location) ? "Unknown" : location;

        if (completedIndicator != null)
            completedIndicator.SetActive(completed);
    }

    public void SetSelected(bool selected)
    {
        ResolveReferences();
        CacheDefaultBackgroundColor();
        EnsureSelectionOutline();

        if (backgroundImage != null && hasDefaultBackgroundColor)
            backgroundImage.color = defaultBackgroundColor;

        if (selectionOutline != null)
            selectionOutline.enabled = selected;
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

        if (selectionOutline == null && backgroundImage != null)
            selectionOutline = backgroundImage.GetComponent<Outline>();

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

    private void EnsureSelectionOutline()
    {
        if (backgroundImage == null)
            return;

        if (selectionOutline == null)
            selectionOutline = backgroundImage.GetComponent<Outline>();
        if (selectionOutline == null)
            selectionOutline = backgroundImage.gameObject.AddComponent<Outline>();

        selectionOutline.effectColor = selectionBorderColor;
        selectionOutline.effectDistance = selectionBorderThickness;
        selectionOutline.useGraphicAlpha = true;
    }
}
