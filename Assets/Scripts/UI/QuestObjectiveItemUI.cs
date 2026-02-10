using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestObjectiveItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Toggle completedToggle;
    [SerializeField] private Image checkImage;

    private void Awake()
    {
        ResolveReferences();
    }

    public void SetData(string title, string description, bool completed)
    {
        ResolveReferences();

        if (titleText != null) titleText.text = string.IsNullOrWhiteSpace(title) ? "Objective" : title;
        if (descriptionText != null) descriptionText.text = string.IsNullOrWhiteSpace(description) ? string.Empty : description;

        if (completedToggle != null)
        {
            completedToggle.SetIsOnWithoutNotify(completed);
            completedToggle.interactable = false;
        }

        if (checkImage != null)
            checkImage.enabled = completed;
    }

    private void ResolveReferences()
    {
        if (titleText == null)
            titleText = FindTextContains("title");

        if (descriptionText == null)
            descriptionText = FindTextContains("objective")
                              ?? FindTextContains("desc")
                              ?? FindTextContains("description");

        if (completedToggle == null)
            completedToggle = GetComponentInChildren<Toggle>(true);

        if (checkImage == null)
        {
            var images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] == null) continue;
                string n = images[i].gameObject.name.ToLowerInvariant();
                if (n.Contains("check"))
                {
                    checkImage = images[i];
                    break;
                }
            }
        }

        if (titleText == null || descriptionText == null)
        {
            var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] == null) continue;
                if (titleText == null) titleText = texts[i];
                else if (descriptionText == null && texts[i] != titleText) descriptionText = texts[i];
            }
        }
    }

    private TextMeshProUGUI FindTextContains(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null) continue;
            if (texts[i].gameObject.name.ToLowerInvariant().Contains(token))
                return texts[i];
        }
        return null;
    }
}
