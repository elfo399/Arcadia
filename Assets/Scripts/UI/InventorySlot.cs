using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// UI cell that supports mouse drag&drop and gamepad selection.
/// Delegates logic to InventoryUI via callbacks.
/// </summary>
public class InventorySlot : MonoBehaviour,
    IPointerDownHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IDropHandler,
    ISelectHandler,
    ISubmitHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private bool logMissingReferences = false;
    [SerializeField] private bool displayOnly = false; // se true mostra solo l'icona, nessuna interazione
    [SerializeField] private Outline focusOutline;
    [SerializeField] private Color focusBorderColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Vector2 focusBorderThickness = new Vector2(3f, 3f);

    private int slotIndex = -1;
    private InventoryUI owner;
    private Color defaultBackgroundColor = Color.white;
    private bool hasDefaultBackgroundColor = false;

    void Awake()
    {
        ResolveReferences();
        CacheDefaultBackgroundColor();
        EnsureFocusOutline();
        SetFocused(false);
        Clear();
    }

    public void Init(int index, InventoryUI inventory)
    {
        slotIndex = index;
        owner = inventory;
    }

    public void SetDisplayOnly(bool value)
    {
        displayOnly = value;
    }

    public void SetFocused(bool focused)
    {
        ResolveReferences();
        EnsureFocusOutline();

        if (backgroundImage != null && hasDefaultBackgroundColor)
        {
            // Non alterare il tema dello slot: ripristina sempre il colore originale.
            backgroundImage.color = defaultBackgroundColor;
        }

        if (focusOutline != null)
        {
            focusOutline.enabled = focused;
        }
    }

    /// <summary>
    /// Sets up the slot with the given item information.
    /// </summary>
    /// <param name="itemIcon">The sprite for the item icon.</param>
    /// <param name="quantity">The number of items in the stack.</param>
    public void Setup(Sprite itemIcon, int quantity, bool isEquipped = false, bool forceShowQuantity = false)
    {
        ResolveReferences();

        if (iconImage == null)
        {
            if (logMissingReferences) Debug.LogWarning($"InventorySlot '{name}' non ha un iconImage assegnato.");
            return;
        }

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

        if (forceShowQuantity)
        {
            quantityText.enabled = true;
            quantityText.text = Mathf.Max(0, quantity).ToString();
        }
        else if (quantity > 1)
        {
            quantityText.enabled = true;
            quantityText.text = isEquipped ? $"{quantity} E" : quantity.ToString();
        }
        else if (isEquipped)
        {
            quantityText.enabled = true;
            quantityText.text = "E";
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
        ResolveReferences();

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

    // --- EventSystem handlers ---
    public void OnPointerDown(PointerEventData eventData)
    {
        if (displayOnly) return;
        owner?.HandleSlotPointerDown(slotIndex);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (displayOnly) return;
        owner?.HandleSlotBeginDrag(slotIndex, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        owner?.HandleSlotDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (displayOnly) return;
        owner?.HandleSlotEndDrag();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (displayOnly) return;
        owner?.HandleSlotDrop(slotIndex);
    }

    // Gamepad/keyboard selection + submit for swap
    public void OnSelect(BaseEventData eventData)
    {
        if (displayOnly) return;
        owner?.HandleSlotSelected(slotIndex);
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (displayOnly) return;
        owner?.HandleSlotSubmit(slotIndex);
    }

    /// <summary>
    /// Returns the current icon rect size so the drag preview can mirror grid visuals.
    /// </summary>
    public Vector2 GetIconSize()
    {
        ResolveReferences();
        if (iconImage != null && iconImage.rectTransform != null)
        {
            var rect = iconImage.rectTransform.rect;
            return new Vector2(rect.width, rect.height);
        }
        return Vector2.zero;
    }

    // ------- Helpers --------
    private void ResolveReferences()
    {
        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }

        if (iconImage == null)
        {
            // 1) Cerca child chiamato "Icon"
            var iconTransform = transform.Find("Icon");
            if (iconTransform != null)
                iconImage = iconTransform.GetComponent<Image>();

            // 2) Prima Image figlia diversa dal background
            if (iconImage == null)
            {
                var images = GetComponentsInChildren<Image>(true);
                foreach (var img in images)
                {
                    if (img == null) continue;
                    if (img == GetComponent<Image>()) continue; // salta eventuale bg sullo stesso GO
                    iconImage = img;
                    break;
                }
            }
        }

        if (quantityText == null)
        {
            // Cerca child chiamato "QuantityText" oppure il primo TMP figlio
            var qtTransform = transform.Find("QuantityText");
            if (qtTransform != null)
                quantityText = qtTransform.GetComponent<TextMeshProUGUI>();
            if (quantityText == null)
            {
                quantityText = GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }
    }

    private void CacheDefaultBackgroundColor()
    {
        if (backgroundImage != null)
        {
            defaultBackgroundColor = backgroundImage.color;
            hasDefaultBackgroundColor = true;
        }
    }

    private void EnsureFocusOutline()
    {
        if (focusOutline == null)
        {
            focusOutline = GetComponent<Outline>();
            if (focusOutline == null)
            {
                focusOutline = gameObject.AddComponent<Outline>();
            }
        }

        if (focusOutline != null)
        {
            focusOutline.effectColor = focusBorderColor;
            focusOutline.effectDistance = focusBorderThickness;
            focusOutline.useGraphicAlpha = true;
        }
    }
}
