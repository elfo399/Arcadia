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
    [SerializeField] private int magicInitialSlotCount = 12;

    [Header("Drag & Drop")]
    [SerializeField] private Canvas dragCanvas;
    [SerializeField] private Image dragPreviewTemplate;

    [Header("Magic Detail")]
    [SerializeField] private bool autoWireMagicReferences = false;
    [SerializeField] private GameObject magicDetailRoot;
    [SerializeField] private Image magicImage;
    [SerializeField] private TextMeshProUGUI magicTitle;
    [SerializeField] private TextMeshProUGUI magicDesc;
    [SerializeField] private TextMeshProUGUI magicDamageText;
    [SerializeField] private TextMeshProUGUI magicCriticalText;
    [SerializeField] private TextMeshProUGUI magicScalingText;
    [SerializeField] private TextMeshProUGUI magicRequirementsText;
    [FormerlySerializedAs("equipMagicButton")]
    [SerializeField] private Button equipButton;

    private readonly List<InventorySlot> slots = new();
    private readonly List<InventoryItem> currentItems = new();
    private PlayerInventory playerInventory;
    private EquipmentManager equipmentManager;
    private Image activeDragPreview;
    private int dragOriginIndex = -1;
    private bool showPadFocus;
    private int currentSelectedIndex = -1;
    private int padFocusIndex = -1;
    private bool isInitialized;

    public void Initialize(PlayerInventory inventory, EquipmentManager equipment)
    {
        playerInventory = inventory != null ? inventory : playerInventory;
        equipmentManager = equipment != null ? equipment : equipmentManager;

        if (autoWireMagicReferences && magicSlotParent == null)
            AutoWireMagicReferences();

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
        isInitialized = true;
    }

    public void Cleanup()
    {
        ClearDragPreview();
        dragOriginIndex = -1;
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
        ApplyPadFocusVisual(index);
        if (HasItem(index))
            ShowItemDetailsByIndex(index);
        else
            ClearDetail();
        UpdateEquipButtonState();
    }

    public void HandleSlotBeginDrag(int index, PointerEventData eventData)
    {
        if (!HasItem(index)) return;

        dragOriginIndex = index;
        Vector2 iconSize = Vector2.zero;
        if (IsValidSlotIndex(index) && slots[index] != null)
            iconSize = slots[index].GetIconSize();

        CreateDragPreview(GetItemIcon(currentItems[index]), eventData, iconSize);
    }

    public void HandleSlotDrag(PointerEventData eventData)
    {
        if (activeDragPreview != null)
            activeDragPreview.rectTransform.position = eventData.position;
    }

    public void HandleSlotEndDrag()
    {
        ClearDragPreview();
        dragOriginIndex = -1;
    }

    public void HandleSlotDrop(int targetIndex)
    {
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

    public void HandleSlotSelected(int index)
    {
        HandleSlotPointerDown(index);
    }

    public void HandleSlotSubmit(int index)
    {
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
        EnsurePlayerInventory();

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
        EnsurePlayerInventory();
        currentItems.Clear();
        if (playerInventory != null)
        {
            currentItems.AddRange(playerInventory.GetMagicInventorySlotLayout(magicInitialSlotCount));
        }

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
        EnsurePlayerInventory();
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
        if (magicDamageText != null) magicDamageText.text = magic.magicDamage.ToString();
        if (magicCriticalText != null) magicCriticalText.text = magic.criticalHit.ToString("0.##");
        if (magicScalingText != null) magicScalingText.text = magic.scaling ?? string.Empty;
        if (magicRequirementsText != null) magicRequirementsText.text = magic.requirements ?? string.Empty;
        UpdateEquipButtonState();
    }

    private void ClearDetail()
    {
        currentSelectedIndex = -1;
        if (magicDetailRoot != null) magicDetailRoot.SetActive(false);
        if (magicImage != null) magicImage.sprite = null;
        if (magicTitle != null) magicTitle.text = string.Empty;
        if (magicDesc != null) magicDesc.text = string.Empty;
        if (magicDamageText != null) magicDamageText.text = string.Empty;
        if (magicCriticalText != null) magicCriticalText.text = string.Empty;
        if (magicScalingText != null) magicScalingText.text = string.Empty;
        if (magicRequirementsText != null) magicRequirementsText.text = string.Empty;
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
        EnsurePlayerInventory();
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

        Canvas targetCanvas = dragCanvas;
        if (targetCanvas == null)
            targetCanvas = GetComponentInParent<Canvas>();
        if (targetCanvas == null) return;

        if (dragPreviewTemplate == null)
        {
            GameObject go = new GameObject("MagicDragPreview");
            go.transform.SetParent(targetCanvas.transform, false);
            activeDragPreview = go.AddComponent<Image>();
            activeDragPreview.raycastTarget = false;
        }
        else
        {
            activeDragPreview = Instantiate(dragPreviewTemplate, targetCanvas.transform);
        }

        activeDragPreview.sprite = icon;
        activeDragPreview.preserveAspect = true;
        activeDragPreview.raycastTarget = false;

        if (iconSize == Vector2.zero)
        {
            iconSize = activeDragPreview.rectTransform.sizeDelta;
            if (iconSize == Vector2.zero)
                iconSize = new Vector2(48f, 48f);
        }

        activeDragPreview.rectTransform.sizeDelta = iconSize;
        activeDragPreview.rectTransform.position = eventData.position;
        activeDragPreview.gameObject.SetActive(true);
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

    private void EnsurePlayerInventory()
    {
    }

    private void AutoWireMagicReferences()
    {
        GameObject magicRootObject = equipmentManager != null ? equipmentManager.MagicBackground : null;
        if (magicRootObject == null)
            magicRootObject = FindDeepChildByName(transform.root, "MagicBackground")?.gameObject;

        var root = magicRootObject != null ? magicRootObject.transform : null;
        if (root == null) return;

        if (magicSlotParent == null)
            magicSlotParent = FindDescendantByPath(root, "GridBackground/GridInv") ?? FindDeepChildByName(root, "GridInv");

        if (magicDetailRoot == null)
            magicDetailRoot = FindDeepChildByName(root, "DescMagic")?.gameObject;

        var detailTf = magicDetailRoot != null ? magicDetailRoot.transform : null;
        if (detailTf != null)
        {
            if (magicImage == null)
            {
                var imageTf = FindDeepChildByName(detailTf, "Image");
                if (imageTf != null) magicImage = imageTf.GetComponent<Image>();
            }

            if (magicTitle == null) magicTitle = FindDeepTextByName(detailTf, "Title");
            if (magicDesc == null) magicDesc = FindDeepTextByName(detailTf, "Desc");
            if (magicDamageText == null) magicDamageText = FindDeepTextByName(detailTf, "Damage");
            if (magicCriticalText == null) magicCriticalText = FindDeepTextByName(detailTf, "Critical");
            if (magicScalingText == null) magicScalingText = FindDeepTextByName(detailTf, "Scaling");
            if (magicRequirementsText == null) magicRequirementsText = FindDeepTextByName(detailTf, "Requirement");
        }
    }

    private static Transform FindDescendantByPath(Transform root, string path)
    {
        return root != null && !string.IsNullOrWhiteSpace(path) ? root.Find(path) : null;
    }

    private static Transform FindDeepChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName)) return null;
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child.name == childName)
                return child;
            var found = FindDeepChildByName(child, childName);
            if (found != null)
                return found;
        }
        return null;
    }

    private static TextMeshProUGUI FindDeepTextByName(Transform root, string childName)
    {
        var tf = FindDeepChildByName(root, childName);
        return tf != null ? tf.GetComponent<TextMeshProUGUI>() : null;
    }
}
