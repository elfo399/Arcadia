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
            completedToggle.enabled = true;
            completedToggle.SetIsOnWithoutNotify(completed);
            completedToggle.interactable = true;
            completedToggle.enabled = false;
        }

        if (checkImage != null)
            checkImage.enabled = completed;
    }

    private void ResolveReferences()
    {
        if (checkImage == null && completedToggle != null)
            checkImage = completedToggle.graphic as Image;
    }
}
