using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI questNameText;
    [SerializeField] private TextMeshProUGUI questLocationText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject completedIndicator;
    [SerializeField] private Color selectedBackgroundColor = new Color(1f, 0.85f, 0.25f, 0.2f);
    [SerializeField] private Color normalBackgroundColor = Color.clear;

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
        if (backgroundImage != null)
            backgroundImage.color = selected ? selectedBackgroundColor : normalBackgroundColor;
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
}
