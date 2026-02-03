using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventorySlot : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI quantityText;

    void Awake()
    {
        // Ensure components are assigned, disable them by default
        if (iconImage == null)
        {
            iconImage = GetComponentInChildren<Image>();
        }
        if (quantityText == null)
        {
            quantityText = GetComponentInChildren<TextMeshProUGUI>();
        }
        Clear();
    }

    /// <summary>
    /// Sets up the slot with the given item information.
    /// </summary>
    /// <param name="itemIcon">The sprite for the item icon.</param>
    /// <param name="quantity">The number of items in the stack.</param>
    public void Setup(Sprite itemIcon, int quantity)
    {
        if (itemIcon != null)
        {
            iconImage.enabled = true;
            iconImage.sprite = itemIcon;
        }
        else
        {
            Clear();
            return;
        }

        if (quantity > 1)
        {
            quantityText.enabled = true;
            quantityText.text = quantity.ToString();
        }
        else
        {
            quantityText.enabled = false;
        }
    }

    /// <summary>
    /// Clears the slot, hiding the icon and quantity text.
    /// </summary>
    public void Clear()
    {
        if (iconImage != null)
        {
            iconImage.enabled = false;
            iconImage.sprite = null;
        }
        if (quantityText != null)
        {
            quantityText.enabled = false;
            quantityText.text = "";
        }
    }
}
