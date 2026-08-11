using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestRewardItemUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI typeText;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private TextMeshProUGUI itemNameText;

    private void Awake()
    {
        ResolveReferences();
    }

    public void SetData(Sprite icon, string type, int amount, string itemName)
    {
        ResolveReferences();

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        if (typeText != null) typeText.text = string.IsNullOrWhiteSpace(type) ? string.Empty : type;
        if (amountText != null) amountText.text = amount > 0 ? amount.ToString("N0") : string.Empty;
        if (itemNameText != null) itemNameText.text = string.IsNullOrWhiteSpace(itemName) ? string.Empty : itemName;
    }

    public void SetRequirementData(Sprite icon, string itemName, int owned, int required)
    {
        ResolveReferences();

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        if (typeText != null)
            typeText.text = itemName ?? string.Empty;
        if (amountText != null)
            amountText.text = $"{Mathf.Max(0, owned)}/{Mathf.Max(0, required)}";
        if (itemNameText != null)
            itemNameText.text = string.Empty;
    }

    private void ResolveReferences()
    {
        // Tutti i riferimenti sono assegnati esplicitamente nel prefab.
    }
}
