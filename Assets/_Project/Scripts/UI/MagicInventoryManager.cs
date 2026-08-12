using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class MagicInventoryManager : MonoBehaviour, IInventorySlotHandler
{
    [Header("Magic UI")]
    [SerializeField] private InventorySlot slotPrefab;
    [SerializeField] private Transform magicSlotParent;
    [SerializeField] private int magicInitialSlotCount = PlayerInventory.DefaultMagicInventoryCapacity;

    [Header("Magic Empty State")]
    [Tooltip("Object shown when the player does not own any magic.")]
    [SerializeField] private GameObject noMagicBanner;
    [Tooltip("Grid hidden while the magic inventory is empty. If omitted, Magic Slot Parent is used.")]
    [SerializeField] private GameObject magicGridRoot;

    [Header("Magic Detail")]
    [SerializeField] private GameObject magicDetailRoot;
    [SerializeField] private Image magicImage;
    [SerializeField] private TextMeshProUGUI magicTitle;
    [SerializeField] private TextMeshProUGUI magicDesc;
    [SerializeField] private TextMeshProUGUI magicDamageText;
    [SerializeField] private TextMeshProUGUI magicCriticalText;
    [SerializeField] private TextMeshProUGUI magicScalingText;
    [SerializeField] private TextMeshProUGUI magicRequirementsText;
    [Header("Magic Detail - New Stats")]
    [SerializeField] private GameObject attackStatsRoot;
    [SerializeField] private TextMeshProUGUI attackDamageText;
    [SerializeField] private TextMeshProUGUI attackCriticalText;
    [SerializeField] private TextMeshProUGUI attackManaCostText;
    [SerializeField] private GameObject boostStatsRoot;
    [SerializeField] private TextMeshProUGUI boostAttributeText;
    [SerializeField] private TextMeshProUGUI boostAmountText;
    [SerializeField] private TextMeshProUGUI boostDurationText;
    [SerializeField] private TextMeshProUGUI boostManaCostText;
    [SerializeField] private GameObject healingStatsRoot;
    [SerializeField] private TextMeshProUGUI healingTypeText;
    [SerializeField] private TextMeshProUGUI healingAmountText;
    [SerializeField] private TextMeshProUGUI healingManaCostText;
    [FormerlySerializedAs("equipMagicButton")]
    [SerializeField] private Button equipButton;

    private readonly List<InventorySlot> slots = new();
    private readonly List<InventoryItem> currentItems = new();
    private PlayerInventory playerInventory;
    private EquipmentManager equipmentManager;
    [SerializeField] private Canvas dragCanvas;
    [SerializeField] private RectTransform dragPreviewRoot;
    private Image activeDragPreview;
    private int dragOriginIndex = -1;
    private bool slotInputEnabled = true;
    private bool showPadFocus;
    private int currentSelectedIndex = -1;
    private int padFocusIndex = -1;
    private bool isInitialized;

    public void Initialize(PlayerInventory inventory, EquipmentManager equipment)
    {
        playerInventory = inventory != null ? inventory : playerInventory;
        equipmentManager = equipment != null ? equipment : equipmentManager;

        if (magicSlotParent == null)
            return;

        if (slotPrefab == null && slots.Count == 0)
        {
            slots.AddRange(magicSlotParent.GetComponentsInChildren<InventorySlot>(true));
            for (int i = 0; i < slots.Count; i++)
                slots[i].Init(i, this);
        }

        if (slotPrefab != null && magicInitialSlotCount > 0 && slots.Count == 0)
            EnsureSlots(magicInitialSlotCount);

        if (isInitialized)
        {
            UpdateEquipButtonState();
            return;
        }

        ClearSlots();
        ClearDetail();
        UpdateEquipButtonState();
        UpdateMagicEmptyState(false);
        isInitialized = true;
    }

    public void Cleanup()
    {
        CancelActiveDrag();
    }

    public void HideActiveDetailForMenuClose()
    {
        ClearDetail();
        UpdateEquipButtonState();
    }

    private void OnDisable()
    {
        CancelActiveDrag();
    }

    public int GetCapacity() => magicInitialSlotCount;

    public void SetPlayerInventory(PlayerInventory inventory)
    {
        playerInventory = inventory;
    }

    public void SetPadFocusVisible(bool visible)
    {
        showPadFocus = visible;
        ApplyPadFocusVisual(showPadFocus ? padFocusIndex : -1);
    }

    public void ShowMagicTab()
    {
        RefreshFromPlayer();
        SetEquipButtonState(false, false);
    }

    public void PrepareMagicEquipSelectionView()
    {
        equipmentManager?.ShowMagicPanel();
        RefreshFromPlayer();
        UpdateEquipButtonState();
    }

    public void HandleSlotPointerDown(int index)
    {
        if (!slotInputEnabled) return;

        ApplyPadFocusVisual(index);
        if (HasItem(index))
            ShowItemDetailsByIndex(index);
        else
            ClearDetail();
        UpdateEquipButtonState();
    }

    public void HandleSlotBeginDrag(int index, PointerEventData eventData)
    {
        if (!slotInputEnabled) return;
        if (!HasItem(index)) return;

        dragOriginIndex = index;
        Vector2 iconSize = Vector2.zero;
        if (IsValidSlotIndex(index) && slots[index] != null)
            iconSize = slots[index].GetIconSize();

        CreateDragPreview(GetItemIcon(currentItems[index]), eventData, iconSize);
    }

    public void HandleSlotDrag(PointerEventData eventData)
    {
        if (!slotInputEnabled) return;
        if (activeDragPreview != null)
            MoveDragPreview(eventData);
    }

    public void HandleSlotEndDrag()
    {
        CancelActiveDrag();
    }

    public void HandleSlotDrop(int targetIndex)
    {
        if (!slotInputEnabled)
        {
            CancelActiveDrag();
            return;
        }

        if (dragOriginIndex >= 0)
            SwapItems(dragOriginIndex, targetIndex);

        ClearDragPreview();
        dragOriginIndex = -1;

        if (HasItem(targetIndex))
            ShowItemDetailsByIndex(targetIndex);
        else
            ClearDetail();

        UpdateEquipButtonState();
    }

    public void CancelActiveDrag()
    {
        ClearDragPreview();
        dragOriginIndex = -1;
    }

    public void SetSlotInputEnabled(bool enabled)
    {
        slotInputEnabled = enabled;
        if (!slotInputEnabled)
            CancelActiveDrag();
    }

    public void HandleSlotSelected(int index)
    {
        if (!slotInputEnabled) return;
        HandleSlotPointerDown(index);
    }

    public void HandleSlotSubmit(int index)
    {
        if (!slotInputEnabled) return;

        if (!HasItem(index))
            return;

        ShowItemDetailsByIndex(index);
    }

    public void FocusDefaultPadSlot(bool selectItem = true)
    {
        if (slots.Count == 0) return;

        int fallback = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            if (HasItem(i))
            {
                fallback = i;
                break;
            }
        }

        SetPadFocus(fallback, selectItem);
    }

    public void MovePadFocusHorizontal(int direction)
    {
        if (slots.Count == 0) return;
        int dir = direction >= 0 ? 1 : -1;
        int start = padFocusIndex;
        if (start < 0 || start >= slots.Count) start = 0;
        SetPadFocus((start + dir + slots.Count) % slots.Count);
    }

    public void MovePadFocusVertical(int direction)
    {
        if (slots.Count == 0) return;
        int dir = direction >= 0 ? 1 : -1;
        int start = padFocusIndex;
        if (start < 0 || start >= slots.Count) start = 0;
        int next = (start + (dir * GetGridColumnCount())) % slots.Count;
        if (next < 0) next += slots.Count;
        SetPadFocus(next);
    }

    public void ConfirmPadSelection()
    {
        if (IsEquipButtonInteractable())
        {
            OnEquipMagicButtonClick();
            return;
        }

        if (padFocusIndex < 0 || !HasItem(padFocusIndex))
        {
            FocusDefaultPadSlot(false);
            return;
        }

        ShowItemDetailsByIndex(padFocusIndex);
    }

    public void OnEquipMagicButtonClick()
    {
        int targetIndex = currentSelectedIndex;
        if (!HasItem(targetIndex) && HasItem(padFocusIndex))
            targetIndex = padFocusIndex;

        if (!HasItem(targetIndex)) return;
        if (playerInventory == null || equipmentManager == null) return;
        if (equipmentManager.CurrentEquipTarget != EquipmentManager.EquipTarget.Top) return;

        currentSelectedIndex = targetIndex;
        var item = currentItems[targetIndex];
        var magic = item != null ? item.magicData : null;
        if (magic == null) return;

        int slotIndex = equipmentManager.CurrentTopIndex;
        playerInventory.SetMagicAtSlot(slotIndex, magic, item.instanceId);
        if (playerInventory.GetCurrentMagic() != magic)
            playerInventory.ForceSetMagicAtSlot(slotIndex, magic, item.instanceId);

        equipmentManager?.RefreshEquipmentCross();
        equipmentManager.CloseEquipGrid();
    }

    public void CloseEquipGridView()
    {
        currentSelectedIndex = -1;
        padFocusIndex = -1;
        ApplyPadFocusVisual(-1);
        ClearDetail();
        UpdateEquipButtonState();
    }

    private void RefreshFromPlayer()
    {
        currentItems.Clear();
        if (playerInventory != null)
        {
            currentItems.AddRange(playerInventory.GetMagicInventorySlotLayout(magicInitialSlotCount));
        }

        // Attiva prima la griglia: al primo ingresso questo permette ad Awake degli
        // InventorySlot di terminare prima che Setup assegni le icone.
        UpdateMagicEmptyState(HasAnyMagic());
        RefreshSlotsFromCurrentItems();

        currentSelectedIndex = -1;
        ClearPadFocus();
        ClearDetail();
        UpdateEquipButtonState();
    }

    private void RefreshSlotsFromCurrentItems()
    {
        int activeSlotCount = Mathf.Max(magicInitialSlotCount, currentItems.Count, 1);
        EnsureSlots(activeSlotCount);
        ClearSlots();

        for (int i = 0; i < slots.Count; i++)
        {
            if (i < currentItems.Count && currentItems[i] != null)
                slots[i].Setup(GetItemIcon(currentItems[i]), currentItems[i].amount, IsItemEquipped(currentItems[i]));
            else
                slots[i].Clear();

            slots[i].gameObject.SetActive(i < activeSlotCount);
        }
    }

    private void ClearSlots()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
                slots[i].Clear();
        }
    }

    private void EnsureSlots(int required)
    {
        if (magicSlotParent == null) return;

        if (slotPrefab == null)
        {
            if (slots.Count < required && slots.Count > 0)
            {
                var template = slots[0];
                while (slots.Count < required)
                {
                    var clone = Instantiate(template, magicSlotParent);
                    clone.Init(slots.Count, this);
                    clone.gameObject.SetActive(true);
                    slots.Add(clone);
                }
            }
            return;
        }

        while (slots.Count < required)
        {
            var slot = Instantiate(slotPrefab, magicSlotParent);
            slot.Init(slots.Count, this);
            slot.gameObject.SetActive(true);
            slots.Add(slot);
        }
    }

    private bool IsValidSlotIndex(int index) => index >= 0 && index < slots.Count;
    private bool HasItem(int index) => index >= 0 && index < currentItems.Count && currentItems[index] != null;

    private bool HasAnyMagic()
    {
        for (int i = 0; i < currentItems.Count; i++)
        {
            if (currentItems[i]?.magicData != null)
                return true;
        }

        return false;
    }

    private void UpdateMagicEmptyState(bool hasMagic)
    {
        GameObject gridRoot = magicGridRoot != null
            ? magicGridRoot
            : magicSlotParent != null ? magicSlotParent.gameObject : null;

        if (gridRoot != null && gridRoot.activeSelf != hasMagic)
            gridRoot.SetActive(hasMagic);
        if (noMagicBanner != null && noMagicBanner.activeSelf == hasMagic)
            noMagicBanner.SetActive(!hasMagic);
    }

    private void SwapItems(int a, int b)
    {
        if (!IsValidSlotIndex(a) || !IsValidSlotIndex(b) || a == b) return;

        int maxIndex = Mathf.Max(a, b);
        while (currentItems.Count <= maxIndex)
            currentItems.Add(null);

        var temp = currentItems[a];
        currentItems[a] = currentItems[b];
        currentItems[b] = temp;

        PersistMagicSlotLayout();
        RefreshSlotsFromCurrentItems();
        ApplyPadFocusVisual(showPadFocus ? b : -1);
    }

    private void PersistMagicSlotLayout()
    {
        if (playerInventory == null)
            return;

        playerInventory.SetMagicInventorySlotLayout(currentItems, magicInitialSlotCount);
    }

    private void SetPadFocus(int index, bool selectItem = true)
    {
        if (index < 0 || index >= slots.Count) return;
        padFocusIndex = index;
        ApplyPadFocusVisual(index);
        if (selectItem)
            ShowItemDetailsByIndex(index);
        if (EventSystem.current != null && slots[index] != null)
            EventSystem.current.SetSelectedGameObject(slots[index].gameObject);
    }

    private void ClearPadFocus()
    {
        padFocusIndex = -1;
        ApplyPadFocusVisual(-1);
        if (EventSystem.current != null && IsCurrentEventSelectionOwnedSlot())
            EventSystem.current.SetSelectedGameObject(null);
    }

    private bool IsCurrentEventSelectionOwnedSlot()
    {
        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null)
            return false;

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null && selected == slots[i].gameObject)
                return true;
        }

        return false;
    }

    private void ApplyPadFocusVisual(int focusedIndex)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
                slots[i].SetFocused(showPadFocus && i == focusedIndex);
        }
    }

    private void ShowItemDetailsByIndex(int index)
    {
        if (!HasItem(index))
        {
            ClearDetail();
            return;
        }

        currentSelectedIndex = index;
        var magic = currentItems[index].magicData;
        if (magic == null)
        {
            ClearDetail();
            return;
        }

        if (magicDetailRoot != null) magicDetailRoot.SetActive(true);
        if (magicImage != null) magicImage.sprite = magic.icon;
        if (magicTitle != null) magicTitle.text = magic.magicName ?? string.Empty;
        if (magicDesc != null) magicDesc.text = magic.description ?? string.Empty;
        UpdateMagicTypeSections(magic);
        SetText(ResolveText(attackDamageText, magicDamageText), magic.magicDamage.ToString());
        SetText(ResolveText(attackCriticalText, magicCriticalText), magic.criticalHit.ToString("0.##"));
        SetText(attackManaCostText, MagicItemData.FormatCompact(magic.manaCost));
        if (magicScalingText != null) magicScalingText.text = magic.scaling ?? string.Empty;
        if (magicRequirementsText != null) magicRequirementsText.text = magic.GetRequirementsLabel();
        SetText(boostAttributeText, MagicItemData.FormatBoostAttribute(magic.boostAttribute));
        SetText(boostAmountText, MagicItemData.FormatSignedAmount(magic.boostAmount));
        SetText(boostDurationText, MagicItemData.FormatDuration(magic.boostDurationSeconds));
        SetText(boostManaCostText, MagicItemData.FormatCompact(magic.manaCost));
        SetText(healingTypeText, MagicItemData.FormatHealingType(magic.effectType));
        SetText(healingAmountText, magic.healAmount.ToString());
        SetText(healingManaCostText, MagicItemData.FormatCompact(magic.manaCost));
        UpdateEquipButtonState();
    }

    private void ClearDetail()
    {
        currentSelectedIndex = -1;
        if (magicDetailRoot != null) magicDetailRoot.SetActive(false);
        SetMagicStatsRoots(false, false, false);
        if (magicImage != null) magicImage.sprite = null;
        if (magicTitle != null) magicTitle.text = string.Empty;
        if (magicDesc != null) magicDesc.text = string.Empty;
        if (magicDamageText != null) magicDamageText.text = string.Empty;
        if (magicCriticalText != null) magicCriticalText.text = string.Empty;
        if (magicScalingText != null) magicScalingText.text = string.Empty;
        if (magicRequirementsText != null) magicRequirementsText.text = string.Empty;
        SetText(attackDamageText, string.Empty);
        SetText(attackCriticalText, string.Empty);
        SetText(attackManaCostText, string.Empty);
        SetText(boostAttributeText, string.Empty);
        SetText(boostAmountText, string.Empty);
        SetText(boostDurationText, string.Empty);
        SetText(boostManaCostText, string.Empty);
        SetText(healingTypeText, string.Empty);
        SetText(healingAmountText, string.Empty);
        SetText(healingManaCostText, string.Empty);
    }

    private void UpdateMagicTypeSections(MagicItemData magic)
    {
        bool attack = magic != null && magic.IsVisualCategory(MagicItemData.MagicCategory.Attack);
        bool boost = magic != null && magic.IsVisualCategory(MagicItemData.MagicCategory.Boost);
        bool healing = magic != null && magic.IsVisualCategory(MagicItemData.MagicCategory.Healing);

        SetMagicStatsRoots(attack, boost, healing);
    }

    private void SetMagicStatsRoots(bool attack, bool boost, bool healing)
    {
        if (attackStatsRoot != null) attackStatsRoot.SetActive(attack);
        if (boostStatsRoot != null) boostStatsRoot.SetActive(boost);
        if (healingStatsRoot != null) healingStatsRoot.SetActive(healing);
    }

    private static TextMeshProUGUI ResolveText(TextMeshProUGUI preferred, TextMeshProUGUI fallback)
    {
        return preferred != null ? preferred : fallback;
    }

    private static void SetText(TextMeshProUGUI target, string value)
    {
        if (target != null)
            target.text = value ?? string.Empty;
    }

    private void UpdateEquipButtonState()
    {
        bool canEquip = equipmentManager != null
                        && equipmentManager.CurrentEquipTarget == EquipmentManager.EquipTarget.Top
                        && currentSelectedIndex >= 0
                        && HasItem(currentSelectedIndex)
                        && currentItems[currentSelectedIndex]?.magicData != null;
        SetEquipButtonState(canEquip, canEquip);
    }

    private void SetEquipButtonState(bool visible, bool interactable)
    {
        if (equipButton == null) return;
        equipButton.gameObject.SetActive(visible);
        equipButton.interactable = visible && interactable;
    }

    private bool IsEquipButtonInteractable()
    {
        return equipButton != null
               && equipButton.gameObject.activeInHierarchy
               && equipButton.interactable;
    }

    private bool IsItemEquipped(InventoryItem item)
    {
        if (item == null || string.IsNullOrEmpty(item.instanceId)) return false;
        return playerInventory != null && playerInventory.IsInstanceEquipped(item.instanceId);
    }

    private Sprite GetItemIcon(InventoryItem item)
    {
        if (item == null) return null;
        return item.magicData != null ? item.magicData.icon : item.icon;
    }

    private void CreateDragPreview(Sprite icon, PointerEventData eventData, Vector2 iconSize)
    {
        ClearDragPreview();
        if (icon == null) return;

        Canvas targetCanvas = ResolveDragCanvas();
        if (targetCanvas == null) return;

        RectTransform previewRoot = ResolveDragPreviewRoot(targetCanvas);
        if (previewRoot == null) return;

        GameObject go = new GameObject("MagicDragPreview", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(previewRoot, false);
        activeDragPreview = go.GetComponent<Image>();
        activeDragPreview.raycastTarget = false;

        activeDragPreview.sprite = icon;
        activeDragPreview.color = Color.white;
        activeDragPreview.preserveAspect = true;
        activeDragPreview.raycastTarget = false;

        if (iconSize == Vector2.zero)
        {
            iconSize = activeDragPreview.rectTransform.sizeDelta;
            if (iconSize == Vector2.zero)
                iconSize = new Vector2(48f, 48f);
        }

        activeDragPreview.rectTransform.sizeDelta = iconSize;
        activeDragPreview.transform.SetAsLastSibling();
        MoveDragPreview(eventData);
        activeDragPreview.gameObject.SetActive(true);
    }

    private void MoveDragPreview(PointerEventData eventData)
    {
        if (activeDragPreview == null || eventData == null)
            return;

        RectTransform parentRect = activeDragPreview.rectTransform.parent as RectTransform;
        if (parentRect != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
        {
            activeDragPreview.rectTransform.anchoredPosition = localPoint;
            return;
        }

        activeDragPreview.rectTransform.position = eventData.position;
    }

    private Canvas ResolveDragCanvas()
    {
        return dragCanvas != null && dragCanvas.isActiveAndEnabled ? dragCanvas : null;
    }

    private RectTransform ResolveDragPreviewRoot(Canvas targetCanvas)
    {
        if (dragPreviewRoot != null)
        {
            dragPreviewRoot.SetAsLastSibling();
            return dragPreviewRoot;
        }

        GameObject root = new GameObject("DragPreviewLayer", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        root.transform.SetParent(targetCanvas.transform, false);
        root.transform.SetAsLastSibling();

        RectTransform rectTransform = root.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = targetCanvas.sortingOrder + 1000;

        CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        dragPreviewRoot = rectTransform;
        return dragPreviewRoot;
    }

    private void ClearDragPreview()
    {
        if (activeDragPreview == null) return;
        Destroy(activeDragPreview.gameObject);
        activeDragPreview = null;
    }

    private int GetGridColumnCount()
    {
        var grid = magicSlotParent != null ? magicSlotParent.GetComponent<GridLayoutGroup>() : null;
        if (grid != null && grid.constraintCount > 0 && grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            return Mathf.Max(1, grid.constraintCount);
        return Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, slots.Count))));
    }

}
