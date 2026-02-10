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

    private void ResolveReferences()
    {
        if (iconImage == null)
        {
            var images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] == null) continue;
                if (images[i].gameObject == gameObject) continue;
                string n = images[i].gameObject.name.ToLowerInvariant();
                if (n.Contains("icon"))
                {
                    iconImage = images[i];
                    break;
                }
            }
        }

        if (typeText == null) typeText = FindTextContains("type") ?? FindTextContains("category");
        if (amountText == null) amountText = FindTextContains("amount") ?? FindTextContains("qty") ?? FindTextContains("quantity") ?? FindTextContains("value");
        if (itemNameText == null) itemNameText = FindTextContains("item") ?? FindTextContains("name") ?? FindTextContains("title");

        // Fallback robusto per prefab annidati (Reward/Reward/Category, Reward/Reward/Value, Reward/Title)
        if (typeText == null || amountText == null || itemNameText == null)
        {
            var allTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < allTexts.Length; i++)
            {
                if (allTexts[i] == null) continue;
                string n = allTexts[i].gameObject.name.ToLowerInvariant();
                if (typeText == null && n.Contains("category")) typeText = allTexts[i];
                else if (amountText == null && n.Contains("value")) amountText = allTexts[i];
                else if (itemNameText == null && n.Contains("title")) itemNameText = allTexts[i];
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
