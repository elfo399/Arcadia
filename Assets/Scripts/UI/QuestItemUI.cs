using TMPro;
using UnityEngine;

public class QuestItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI questNameText;
    [SerializeField] private TextMeshProUGUI questLocationText;

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
    }

    private void ResolveReferences()
    {
        if (questNameText == null)
        {
            var nameTransform = transform.Find("Name");
            if (nameTransform != null)
                questNameText = nameTransform.GetComponent<TextMeshProUGUI>();
        }

        if (questLocationText == null)
        {
            var locationTransform = transform.Find("Location");
            if (locationTransform != null)
                questLocationText = locationTransform.GetComponent<TextMeshProUGUI>();
        }

        if (questNameText == null || questLocationText == null)
        {
            var allTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < allTexts.Length; i++)
            {
                if (allTexts[i] == null) continue;

                string n = allTexts[i].gameObject.name.ToLowerInvariant();
                if (questNameText == null && n.Contains("name")) questNameText = allTexts[i];
                if (questLocationText == null && n.Contains("location")) questLocationText = allTexts[i];
            }
        }
    }
}
