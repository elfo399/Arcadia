using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class InventoryUIManager : MonoBehaviour, IInventorySlotHandler
{
    public enum WalletSource { Run, Bank }

    private enum Filter { All, Weapons, Armors, Usables }

    [Header("Slot Grid")]
    [SerializeField] private InventorySlot slotPrefab;
    [SerializeField] private Transform slotParent;
    [SerializeField] private int initialSlotCount = 0;

    [Header("Drag & Drop")]
    [SerializeField] private Canvas dragCanvas;
    [SerializeField] private Image dragPreviewTemplate;

    [Header("Detail Panel - Shared")]
    [SerializeField] private GameObject detailRoot;
    [SerializeField] private Image detailIcon;
    [SerializeField] private TextMeshProUGUI detailTitle;
    [SerializeField] private TextMeshProUGUI detailDescription;

    [Header("Detail Panel - Weapon / Shield Display")]
    [SerializeField] private GameObject weaponDetailRoot;
    [SerializeField] private Image weaponImage;
    [SerializeField] private TextMeshProUGUI weaponTitle;
    [SerializeField] private TextMeshProUGUI weaponDesc;

    [Header("Detail Panel - Weapon Stats")]
    [SerializeField] private GameObject weaponDescriptionRoot;
    [SerializeField] private GameObject weaponStatsRoot;
    [SerializeField] private TextMeshProUGUI weaponDamageText;
    [SerializeField] private TextMeshProUGUI weaponCriticalText;
    [SerializeField] private TextMeshProUGUI weaponWeightText;
    [SerializeField] private TextMeshProUGUI weaponScalingText;
    [SerializeField] private TextMeshProUGUI weaponRequirementsText;

    [Header("Detail Panel - Shield Stats")]
    [SerializeField] private GameObject shieldDescriptionRoot;
    [SerializeField] private TextMeshProUGUI shieldTitle;
    [SerializeField] private TextMeshProUGUI shieldDesc;
    [SerializeField] private TextMeshProUGUI shieldDamageText;
    [SerializeField] private TextMeshProUGUI shieldCriticalText;
    [SerializeField] private TextMeshProUGUI shieldWeightText;
    [SerializeField] private TextMeshProUGUI shieldScalingText;
    [SerializeField] private TextMeshProUGUI shieldRequirementsText;
    [SerializeField] private TextMeshProUGUI weaponPhysicalDefenseText;
    [SerializeField] private TextMeshProUGUI weaponMagicDefenseText;

    [Header("Detail Panel - Armor Variant")]
    [SerializeField] private GameObject armorDescriptionRoot;
    [SerializeField] private TextMeshProUGUI armorTitle;
    [SerializeField] private TextMeshProUGUI armorDesc;
    [SerializeField] private TextMeshProUGUI armorWeightText;
    [SerializeField] private TextMeshProUGUI armorPhysicalDefenseText;
    [SerializeField] private TextMeshProUGUI armorMagicDefenseText;

    [Header("Detail Panel - Item / Usable")]
    [SerializeField] private GameObject itemDetailRoot;
    [SerializeField] private Image itemImage;
    [SerializeField] private TextMeshProUGUI itemTitle;
    [SerializeField] private TextMeshProUGUI itemDesc;

    [Header("Action Button")]
    [FormerlySerializedAs("equipWeaponButton")]
    [SerializeField] private Button equipButton;

    [Header("Wallet UI")]
    [SerializeField] private TextMeshProUGUI goldValueText;
    [SerializeField] private TextMeshProUGUI silverValueText;
    [SerializeField] private TextMeshProUGUI copperValueText;
    [SerializeField] private TextMeshProUGUI keyValueText;
    [SerializeField] private WalletSource walletSource = WalletSource.Run;
    [SerializeField] private bool autoRefreshWallet = true;

    private readonly List<InventorySlot> slots = new();
    private List<InventoryItem> currentItems = new();
    private List<InventoryItem> sourceItems = new();
    private PlayerInventory playerInventory;
    private EquipmentManager equipmentManager;
    private Transform inventorySlotParent;
    private int inventoryInitialSlotCount;
    private Image activeDragPreview;
    private int dragOriginIndex = -1;
    private int selectedPadIndex = -1;
    private int currentSelectedIndex = -1;
    private int padFocusIndex = -1;
    private bool showPadFocus;
    private Filter currentFilter = Filter.All;
    private Filter lastFilter = Filter.All;
    private bool equipSelectionMode;
    private EquipmentManager.EquipTarget pendingEquipTarget = EquipmentManager.EquipTarget.None;
    private int pendingEquipSlot;
    private ArmorItemData.ArmorSlot pendingArmorSlot = ArmorItemData.ArmorSlot.Helmet;
    private ArmorItemData.ArmorSlot? activeArmorFilterSlot;
    private PlayerStats playerStats;
    private bool isInitialized;

    public void Initialize(PlayerInventory inventory, EquipmentManager equipment)
    {
        equipmentManager = equipment != null ? equipment : equipmentManager;
        playerInventory = inventory != null ? inventory : playerInventory;

        EnsureEquipmentManager();
        AutoWireArmorDetailReferences();

        if (slotParent == null)
            slotParent = transform;

        inventorySlotParent = slotParent;
        inventoryInitialSlotCount = initialSlotCount;

        if (slotPrefab == null && slotParent != null && slots.Count == 0)
        {
            slots.AddRange(slotParent.GetComponentsInChildren<InventorySlot>(true));
            for (int i = 0; i < slots.Count; i++)
                slots[i].Init(i, this);
        }

        if (slotPrefab != null && initialSlotCount > 0 && slots.Count == 0)
            EnsureSlots(initialSlotCount);

        CachePlayerStats();
        if (autoRefreshWallet)
            RefreshWalletUI();

        if (isInitialized)
        {
            UpdateEquipButtonState();
            return;
        }

        ClearAllSlots();
        ClearDetailPanel();
        UpdateEquipButtonState();
        isInitialized = true;
    }

    public void Cleanup()
    {
        if (playerStats != null)
        {
            playerStats.OnBankChanged -= HandleBankChanged;
            playerStats.OnRunWalletChanged -= HandleRunWalletChanged;
            playerStats.OnKeysChanged -= HandleKeysChanged;
        }

        ClearDragPreview();
        dragOriginIndex = -1;
    }

    public void SetPlayerInventory(PlayerInventory inventory)
    {
        playerInventory = inventory;
    }

    public void SetPadFocusVisible(bool visible)
    {
        showPadFocus = visible;
        ApplyPadFocusVisual(showPadFocus ? padFocusIndex : -1);
    }

    public List<InventoryItem> GetCurrentItemsSnapshot() => new(currentItems);
    public List<InventoryItem> GetSourceItemsSnapshot() => new(sourceItems);
    public int GetCapacity() => initialSlotCount;

    public void UpdateUI(List<InventoryItem> inventoryData)
    {
        SetSourceItems(inventoryData);
    }

    public void SetSourceItems(List<InventoryItem> inventoryData)
    {
        sourceItems = NormalizeSourceItems(inventoryData);
        ApplyFilterInternal(currentFilter);
    }

    public void RefreshSourceItemsFromPlayer()
    {
        EnsurePlayerInventory();
        if (playerInventory == null)
            return;
        SetSourceItems(new List<InventoryItem>(playerInventory.Items));
        if (autoRefreshWallet)
            RefreshWalletUI();
    }

    public void RefreshWalletUI()
    {
        CachePlayerStats();
        if (playerStats == null)
            return;

        if (walletSource == WalletSource.Bank)
            SetWalletValues(playerStats.bankGold, playerStats.bankSilver, playerStats.bankCopper);
        else
            SetWalletValues(playerStats.runGold, playerStats.runSilver, playerStats.runCopper);

        SetKeyValue(playerStats.currentKeys);
    }

    public void ShowWeaponsFilter()
    {
        activeArmorFilterSlot = null;
        lastFilter = Filter.Weapons;
        ApplyFilterInternal(Filter.Weapons);
    }

    public void ShowUsablesFilter()
    {
        activeArmorFilterSlot = null;
        lastFilter = Filter.Usables;
        ApplyFilterInternal(Filter.Usables);
    }

    public void ShowArmorsFilter(ArmorItemData.ArmorSlot slot)
    {
        activeArmorFilterSlot = slot;
        lastFilter = Filter.Armors;
        ApplyFilterInternal(Filter.Armors);
    }

    public void ShowAllFilter()
    {
        activeArmorFilterSlot = null;
        lastFilter = Filter.All;
        ApplyFilterInternal(Filter.All);
    }

    public void ApplyLastFilter()
    {
        ApplyFilterInternal(lastFilter);
    }

    public void ResetFilterToAll()
    {
        activeArmorFilterSlot = null;
        lastFilter = Filter.All;
        ApplyFilterInternal(Filter.All);
        ClearEquipSelectionContext();
        ResetEquipTarget();
    }

    public void PrepareWeaponEquipSelectionView()
    {
        PrepareEquipSelectionView(ShowWeaponsFilter);
    }

    public void PrepareUsableEquipSelectionView()
    {
        PrepareEquipSelectionView(ShowUsablesFilter);
    }

    public void PrepareArmorEquipSelectionView(ArmorItemData.ArmorSlot slot)
    {
        PrepareEquipSelectionView(() => ShowArmorsFilter(slot));
    }

    public void CloseEquipGridView()
    {
        ClearEquipSelectionContext();
        ResetFilterToAll();
        ClearDetailPanel();
        currentSelectedIndex = -1;
        selectedPadIndex = -1;
    }

    public void BuildSlots(int count)
    {
        if (slotParent == null) slotParent = transform;
        if (slotPrefab == null || slotParent == null)
        {
            Debug.LogWarning("InventoryUIManager: slotPrefab o slotParent non assegnato.");
            return;
        }

        for (int i = slotParent.childCount - 1; i >= 0; i--)
            Destroy(slotParent.GetChild(i).gameObject);

        slots.Clear();
        EnsureSlots(count);
        ClearAllSlots();
    }

    public void HandleSlotPointerDown(int index)
    {
        if (!HasItem(index))
        {
            ClearDetailPanel();
            ApplyPadFocusVisual(index);
            UpdateEquipButtonState();
            return;
        }

        selectedPadIndex = index;
        ApplyPadFocusVisual(index);
        ShowItemDetailsByIndex(index);
        UpdateEquipButtonState();
    }

    public void HandleSlotBeginDrag(int index, PointerEventData eventData)
    {
        if (!HasItem(index)) return;
        dragOriginIndex = index;
        var iconSize = Vector2.zero;
        if (IsValidIndex(index) && index < slots.Count && slots[index] != null)
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
        ShowItemDetailsByIndex(targetIndex);
    }

    public void HandleSlotSelected(int index)
    {
        ApplyPadFocusVisual(index);
        if (HasItem(index))
            ShowItemDetailsByIndex(index);
        else
            ClearDetailPanel();

        UpdateEquipButtonState();
    }

    public void HandleSlotSubmit(int index)
    {
        if (selectedPadIndex < 0)
        {
            if (HasItem(index))
            {
                selectedPadIndex = index;
                ShowItemDetailsByIndex(index);
            }
            return;
        }

        SwapItems(selectedPadIndex, index);
        selectedPadIndex = -1;
        ShowItemDetailsByIndex(index);
    }

    public void FocusDefaultPadSlot()
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

        SetPadFocus(fallback);
    }

    public void MovePadFocusHorizontal(int direction)
    {
        if (slots.Count == 0) return;
        int dir = direction >= 0 ? 1 : -1;
        int start = padFocusIndex;
        if (start < 0 || start >= slots.Count) start = 0;
        int next = (start + dir + slots.Count) % slots.Count;
        SetPadFocus(next);
    }

    public void MovePadFocusVertical(int direction)
    {
        if (slots.Count == 0) return;
        int dir = direction >= 0 ? 1 : -1;
        int start = padFocusIndex;
        if (start < 0 || start >= slots.Count) start = 0;

        int step = GetGridColumnCount();
        int next = (start + (dir * step)) % slots.Count;
        if (next < 0) next += slots.Count;
        SetPadFocus(next);
    }

    public void ConfirmPadSelection()
    {
        if (IsEquipButtonInteractable())
        {
            OnEquipButtonClick();
            return;
        }

        if (TryEquipFocusedPadItem())
            return;

        if (padFocusIndex < 0 || padFocusIndex >= slots.Count)
        {
            FocusDefaultPadSlot();
            if (padFocusIndex < 0 || padFocusIndex >= slots.Count) return;
        }

        HandleSlotSubmit(padFocusIndex);
        SetPadFocus(padFocusIndex);
    }

    public void OnEquipButtonClick()
    {
        if (currentSelectedIndex < 0 || !HasItem(currentSelectedIndex)) return;
        EnsureEquipmentManager();

        var item = currentItems[currentSelectedIndex];
        if (item == null) return;

        var target = GetEffectiveEquipTarget();
        if (item.weaponData != null && CanEquipWeaponForTarget(target))
        {
            OnEquipWeaponButtonClick();
            return;
        }

        if (item.armorData != null && CanEquipArmorForTarget(item, target))
        {
            OnEquipArmorButtonClick();
            return;
        }

        if (item.usableData != null && CanEquipUsableForTarget(target))
            OnEquipUsableButtonClick();
    }

    public void OnEquipWeaponButtonClick()
    {
        if (currentSelectedIndex < 0 || !HasItem(currentSelectedIndex)) return;
        EnsurePlayerInventory();
        EnsureEquipmentManager();
        if (playerInventory == null) return;

        var target = GetEffectiveEquipTarget();
        if (target == EquipmentManager.EquipTarget.Armor)
        {
            OnEquipArmorButtonClick();
            return;
        }

        var item = currentItems[currentSelectedIndex];
        if (item.weaponData == null) return;

        WeaponItem newWeapon = item.weaponData;
        int targetSlot = GetEffectiveEquipSlot();

        if (target == EquipmentManager.EquipTarget.None)
        {
            if (newWeapon.category == WeaponCategory.Shield)
            {
                targetSlot = Mathf.Clamp(playerInventory.currentLeftIndex, 0, playerInventory.leftLoadout.Length - 1);
                playerInventory.SetLeftAtSlot(targetSlot, newWeapon, item.instanceId);
            }
            else
            {
                targetSlot = Mathf.Clamp(playerInventory.currentRightIndex, 0, playerInventory.rightLoadout.Length - 1);
                playerInventory.SetRightAtSlot(targetSlot, newWeapon, item.instanceId);
            }

            CompletePostDirectEquipAction();
            return;
        }

        if (target == EquipmentManager.EquipTarget.Right)
            playerInventory.SetRightAtSlot(targetSlot, newWeapon, item.instanceId);
        else if (target == EquipmentManager.EquipTarget.Left)
            playerInventory.SetLeftAtSlot(targetSlot, newWeapon, item.instanceId);
        else
            return;

        CompleteEquipAction();
    }

    public void OnEquipArmorButtonClick()
    {
        if (currentSelectedIndex < 0 || !HasItem(currentSelectedIndex)) return;
        EnsurePlayerInventory();
        EnsureEquipmentManager();
        if (playerInventory == null) return;

        var item = currentItems[currentSelectedIndex];
        if (item == null || item.armorData == null) return;

        var target = GetEffectiveEquipTarget();
        ArmorItemData.ArmorSlot targetSlot = target == EquipmentManager.EquipTarget.Armor && equipmentManager != null
            ? GetEffectiveArmorSlot()
            : item.armorData.slot;

        if (target != EquipmentManager.EquipTarget.None && target != EquipmentManager.EquipTarget.Armor) return;
        if (item.armorData.slot != targetSlot) return;

        playerInventory.SetArmorAtSlot(targetSlot, item.armorData, item.instanceId);
        if (target == EquipmentManager.EquipTarget.None)
            CompletePostDirectEquipAction();
        else
            CompleteEquipAction();
    }

    public void OnEquipUsableButtonClick()
    {
        if (currentSelectedIndex < 0 || !HasItem(currentSelectedIndex)) return;
        EnsurePlayerInventory();
        EnsureEquipmentManager();
        if (playerInventory == null) return;

        var item = currentItems[currentSelectedIndex];
        if (item.usableData == null) return;
        var target = GetEffectiveEquipTarget();
        int targetSlot = GetEffectiveEquipSlot();

        if (target == EquipmentManager.EquipTarget.None)
        {
            targetSlot = Mathf.Clamp(playerInventory.currentUsableIndex, 0, playerInventory.usableLoadout.Length - 1);
            playerInventory.SetUsableAtSlot(targetSlot, item.usableData, item.instanceId);
            CompletePostDirectEquipAction();
            return;
        }

        if (target == EquipmentManager.EquipTarget.Bottom)
            playerInventory.SetUsableAtSlot(targetSlot, item.usableData, item.instanceId);
        else
            return;

        CompleteEquipAction();
    }

    private void ApplyFilterInternal(Filter filter)
    {
        currentFilter = filter;
        currentItems = new List<InventoryItem>(sourceItems.Count);
        for (int i = 0; i < sourceItems.Count; i++)
        {
            var item = sourceItems[i];
            if (MatchesFilter(item, filter))
                currentItems.Add(item);
            else
                currentItems.Add(null);
        }

        int neededSlots = Mathf.Max(currentItems.Count, initialSlotCount);
        EnsureSlots(neededSlots);
        ClearAllSlots();

        for (int i = 0; i < slots.Count; i++)
        {
            if (i >= neededSlots)
            {
                slots[i].Clear();
                slots[i].gameObject.SetActive(false);
                continue;
            }

            InventoryItem item = i < currentItems.Count ? currentItems[i] : null;
            if (item != null)
                slots[i].Setup(GetItemIcon(item), item.amount, IsItemEquipped(item));
            else
                slots[i].Clear();

            slots[i].gameObject.SetActive(true);
        }

        selectedPadIndex = -1;
        currentSelectedIndex = -1;
        ApplyPadFocusVisual(-1);
        ClearDetailPanel();
        UpdateEquipButtonState();
    }

    private bool MatchesFilter(InventoryItem item, Filter filter)
    {
        if (item == null) return true;
        switch (filter)
        {
            case Filter.All: return !IsMagicInventoryItem(item);
            case Filter.Weapons: return item.weaponData != null;
            case Filter.Armors:
                if (item.armorData == null) return false;
                return !activeArmorFilterSlot.HasValue || item.armorData.slot == activeArmorFilterSlot.Value;
            case Filter.Usables: return item.usableData != null;
            default: return true;
        }
    }

    private static bool IsMagicInventoryItem(InventoryItem item)
    {
        return item != null && item.magicData != null;
    }

    private void EnsurePlayerInventory()
    {
        if (playerInventory == null)
            playerInventory = FindObjectOfType<PlayerInventory>();
    }

    private void EnsureEquipmentManager()
    {
        if (equipmentManager == null)
            equipmentManager = FindObjectOfType<EquipmentManager>(true);
    }

    private bool IsValidIndex(int index) => index >= 0 && index < slots.Count;
    private bool HasItem(int index) => index >= 0 && index < currentItems.Count && currentItems[index] != null;

    private void SwapItems(int a, int b)
    {
        if (!IsValidIndex(a) || !IsValidIndex(b) || a == b) return;

        int maxIndex = Mathf.Max(a, b);
        while (currentItems.Count <= maxIndex)
            currentItems.Add(null);

        var temp = currentItems[a];
        currentItems[a] = currentItems[b];
        currentItems[b] = temp;

        if (sourceItems.Count <= a) ExtendSourceToIndex(a);
        if (sourceItems.Count <= b) ExtendSourceToIndex(b);

        var tempSrc = sourceItems[a];
        sourceItems[a] = sourceItems[b];
        sourceItems[b] = tempSrc;

        PersistSourceItemsToPlayer();
        RefreshSlot(a);
        RefreshSlot(b);
        RefreshDetailSelection();
    }

    private void PersistSourceItemsToPlayer()
    {
        EnsurePlayerInventory();
        if (playerInventory == null)
            return;

        playerInventory.ReplaceAllItems(new List<InventoryItem>(sourceItems));
    }

    private void ExtendSourceToIndex(int index)
    {
        while (sourceItems.Count <= index)
            sourceItems.Add(null);
    }

    private void RefreshSlot(int index)
    {
        if (!IsValidIndex(index) || index >= slots.Count) return;

        var item = currentItems[index];
        if (item != null)
            slots[index].Setup(GetItemIcon(item), item.amount, IsItemEquipped(item));
        else
            slots[index].Clear();

        slots[index].gameObject.SetActive(true);
        UpdateEquipButtonState();
    }

    private void ClearDetailPanel()
    {
        currentSelectedIndex = -1;

        HideDetailViews();

        if (detailIcon != null)
        {
            detailIcon.enabled = false;
            detailIcon.sprite = null;
        }

        if (detailTitle != null) detailTitle.text = string.Empty;
        if (detailDescription != null) detailDescription.text = string.Empty;
        if (detailRoot != null) detailRoot.SetActive(false);

        ClearWeaponStatTexts();
        ClearShieldStatTexts();
        ClearArmorStatTexts();
    }

    private void HideDetailViews()
    {
        if (weaponDetailRoot != null) weaponDetailRoot.SetActive(false);
        if (itemDetailRoot != null) itemDetailRoot.SetActive(false);
        if (weaponDescriptionRoot != null) weaponDescriptionRoot.SetActive(false);
        if (shieldDescriptionRoot != null) shieldDescriptionRoot.SetActive(false);
        if (armorDescriptionRoot != null) armorDescriptionRoot.SetActive(false);
        SetWeaponStatsRootActive(false);
    }

    private void ClearWeaponStatTexts()
    {
        if (weaponDamageText != null) weaponDamageText.text = string.Empty;
        if (weaponCriticalText != null) weaponCriticalText.text = string.Empty;
        if (weaponWeightText != null) weaponWeightText.text = string.Empty;
        if (weaponScalingText != null) weaponScalingText.text = string.Empty;
        if (weaponRequirementsText != null) weaponRequirementsText.text = string.Empty;
    }

    private void ClearShieldStatTexts()
    {
        if (shieldDamageText != null) shieldDamageText.text = string.Empty;
        if (shieldCriticalText != null) shieldCriticalText.text = string.Empty;
        if (shieldWeightText != null) shieldWeightText.text = string.Empty;
        if (shieldScalingText != null) shieldScalingText.text = string.Empty;
        if (shieldRequirementsText != null) shieldRequirementsText.text = string.Empty;
        if (weaponPhysicalDefenseText != null) weaponPhysicalDefenseText.text = string.Empty;
        if (weaponMagicDefenseText != null) weaponMagicDefenseText.text = string.Empty;
    }

    private void ClearArmorStatTexts()
    {
        if (armorWeightText != null) armorWeightText.text = string.Empty;
        if (armorPhysicalDefenseText != null) armorPhysicalDefenseText.text = string.Empty;
        if (armorMagicDefenseText != null) armorMagicDefenseText.text = string.Empty;
    }

    private void SetCommonDetailContent(Sprite icon, string title, string description)
    {
        if (detailIcon != null)
        {
            detailIcon.enabled = icon != null;
            detailIcon.sprite = icon;
        }

        if (detailTitle != null) detailTitle.text = title ?? string.Empty;
        if (detailDescription != null) detailDescription.text = description ?? string.Empty;
    }

    private void SetWeaponDetailContent(Sprite icon, string title, string description)
    {
        if (weaponImage != null) weaponImage.sprite = icon;
        if (weaponTitle != null) weaponTitle.text = title ?? string.Empty;
        if (weaponDesc != null) weaponDesc.text = description ?? string.Empty;
    }

    private void SetArmorDetailContent(Sprite icon, string title, string description)
    {
        if (weaponImage != null) weaponImage.sprite = icon;

        TextMeshProUGUI targetTitle = armorTitle != null ? armorTitle : weaponTitle;
        if (targetTitle != null) targetTitle.text = title ?? string.Empty;

        TextMeshProUGUI targetDescription = armorDesc != null ? armorDesc : weaponDesc;
        if (targetDescription != null) targetDescription.text = description ?? string.Empty;
    }

    private void SetShieldDetailContent(Sprite icon, string title, string description)
    {
        if (weaponImage != null) weaponImage.sprite = icon;

        TextMeshProUGUI targetTitle = shieldTitle != null ? shieldTitle : weaponTitle;
        if (targetTitle != null) targetTitle.text = title ?? string.Empty;

        TextMeshProUGUI targetDescription = shieldDesc != null ? shieldDesc : weaponDesc;
        if (targetDescription != null) targetDescription.text = description ?? string.Empty;
    }

    private void SetItemDetailContent(Sprite icon, string title, string description)
    {
        if (itemImage != null) itemImage.sprite = icon;
        if (itemTitle != null) itemTitle.text = title ?? string.Empty;
        if (itemDesc != null) itemDesc.text = description ?? string.Empty;
    }

    private void ShowWeaponDetail(WeaponItem weapon, Sprite icon, string title, string description)
    {
        bool isShield = weapon.category == WeaponCategory.Shield;

        if (weaponDetailRoot != null) weaponDetailRoot.SetActive(true);
        if (weaponDescriptionRoot != null) weaponDescriptionRoot.SetActive(!isShield);
        if (shieldDescriptionRoot != null) shieldDescriptionRoot.SetActive(isShield);

        if (isShield)
            SetShieldDetailContent(icon, title, description);
        else
            SetWeaponDetailContent(icon, title, description);
        SetCommonDetailContent(icon, title, description);

        if (isShield)
        {
            if (shieldDamageText != null) shieldDamageText.text = weapon.physicalDamage.ToString();
            if (shieldCriticalText != null) shieldCriticalText.text = weapon.criticalHit.ToString("0.##");
            if (shieldWeightText != null) shieldWeightText.text = weapon.weight.ToString("0.##");
            if (shieldScalingText != null) shieldScalingText.text = weapon.GetScalingLabel();
            if (shieldRequirementsText != null) shieldRequirementsText.text = weapon.GetRequirementsLabel();
            if (weaponPhysicalDefenseText != null)
                weaponPhysicalDefenseText.text = Mathf.RoundToInt(Mathf.Clamp01(weapon.physicalBlockPercent) * 100f).ToString();
            if (weaponMagicDefenseText != null)
                weaponMagicDefenseText.text = Mathf.RoundToInt(Mathf.Clamp01(weapon.magicBlockPercent) * 100f).ToString();
            SetWeaponStatsRootActive(false);
            return;
        }

        if (weaponDamageText != null) weaponDamageText.text = weapon.physicalDamage.ToString();
        if (weaponCriticalText != null) weaponCriticalText.text = weapon.criticalHit.ToString("0.##");
        if (weaponWeightText != null) weaponWeightText.text = weapon.weight.ToString("0.##");
        if (weaponScalingText != null) weaponScalingText.text = weapon.GetScalingLabel();
        if (weaponRequirementsText != null) weaponRequirementsText.text = weapon.GetRequirementsLabel();
        SetWeaponStatsRootActive(true);
    }

    private bool TryShowArmorDetail(ArmorItemData armor, Sprite icon, string title, string description)
    {
        if (armorDescriptionRoot == null)
            return false;

        if (weaponDetailRoot != null) weaponDetailRoot.SetActive(true);
        if (armorDescriptionRoot != null) armorDescriptionRoot.SetActive(true);

        SetArmorDetailContent(icon, title, description);
        SetCommonDetailContent(icon, title, description);

        if (armorWeightText != null) armorWeightText.text = armor.weight.ToString("0.##");
        if (armorPhysicalDefenseText != null) armorPhysicalDefenseText.text = armor.physicalDefense.ToString();
        if (armorMagicDefenseText != null) armorMagicDefenseText.text = armor.magicDefense.ToString();

        return true;
    }

    private void ShowGenericItemDetail(Sprite icon, string title, string description)
    {
        if (weaponDetailRoot != null && itemDetailRoot != null && itemDetailRoot.transform.IsChildOf(weaponDetailRoot.transform))
            weaponDetailRoot.SetActive(true);

        if (itemDetailRoot != null) itemDetailRoot.SetActive(true);

        SetItemDetailContent(icon, title, description);
        SetCommonDetailContent(icon, title, description);
    }

    private void ShowItemDetailsByIndex(int index)
    {
        if (!HasItem(index))
        {
            ClearDetailPanel();
            return;
        }

        currentSelectedIndex = index;
        ShowItemDetails(currentItems[index]);
    }

    private void RefreshDetailSelection()
    {
        if (currentSelectedIndex >= 0)
            ShowItemDetailsByIndex(currentSelectedIndex);
        else
            ClearDetailPanel();
    }

    private void ShowItemDetails(InventoryItem item)
    {
        if (item == null)
        {
            ClearDetailPanel();
            return;
        }

        if (detailRoot != null) detailRoot.SetActive(true);
        HideDetailViews();

        Sprite icon = GetItemIcon(item);
        string title = item.title;
        string description = item.description;

        var weapon = item.weaponData;
        var usable = item.usableData;
        var armor = item.armorData;
        var itemData = item.itemData;

        if (weapon != null)
        {
            if (weapon.icon != null) icon = weapon.icon;
            if (!string.IsNullOrEmpty(weapon.weaponName)) title = weapon.weaponName;
            if (!string.IsNullOrEmpty(weapon.description)) description = weapon.description;

            ShowWeaponDetail(weapon, icon, title, description);
            equipmentManager?.RefreshEquipmentCross();
            UpdateEquipButtonState();
            return;
        }

        if (usable != null)
        {
            if (usable.icon != null) icon = usable.icon;
            if (!string.IsNullOrEmpty(usable.itemName)) title = usable.itemName;
            if (!string.IsNullOrEmpty(usable.description)) description = usable.description;
        }
        else if (armor != null)
        {
            if (armor.icon != null) icon = armor.icon;
            if (!string.IsNullOrEmpty(armor.itemName)) title = armor.itemName;
            if (!string.IsNullOrEmpty(armor.description)) description = armor.description;

            if (TryShowArmorDetail(armor, icon, title, description))
            {
                equipmentManager?.RefreshEquipmentCross();
                UpdateEquipButtonState();
                return;
            }
        }
        else if (itemData != null)
        {
            if (itemData.icon != null) icon = itemData.icon;
            if (!string.IsNullOrEmpty(itemData.itemName)) title = itemData.itemName;
            if (!string.IsNullOrEmpty(itemData.description)) description = itemData.description;
        }

        ShowGenericItemDetail(icon, title, description);

        equipmentManager?.RefreshEquipmentCross();
        UpdateEquipButtonState();
    }

    private void AutoWireArmorDetailReferences()
    {
        Transform root = transform.root;
        if (root == null)
            return;

        Transform inventoryRoot = FindDeepChildByName(root, "invBackground");
        if (inventoryRoot == null)
            return;

        if (weaponDetailRoot == null)
            weaponDetailRoot = FindDeepChildByName(inventoryRoot, "DescWeapon")?.gameObject;

        Transform detailRootTransform = weaponDetailRoot != null ? weaponDetailRoot.transform : null;
        if (detailRootTransform == null)
            return;

        if (weaponImage == null)
        {
            Transform imageTf = FindDeepChildByName(detailRootTransform, "Image");
            if (imageTf != null) weaponImage = imageTf.GetComponent<Image>();
        }

        if (weaponTitle == null)
            weaponTitle = FindDeepTextByName(detailRootTransform, "Title");

        if (weaponDesc == null)
            weaponDesc = FindDeepTextByName(detailRootTransform, "Desc_Custom")
                ?? FindDeepTextByName(detailRootTransform, "Desc");

        if (weaponDescriptionRoot == null)
            weaponDescriptionRoot = FindDeepChildByName(detailRootTransform, "WeaponColumn")?.gameObject
                ?? FindDeepChildByName(detailRootTransform, "WeaponCollumn")?.gameObject;

        if (shieldDescriptionRoot == null)
            shieldDescriptionRoot = FindDeepChildByName(detailRootTransform, "ShieldColumn")?.gameObject
                ?? FindDeepChildByName(detailRootTransform, "ShieldCollumn")?.gameObject;

        if (shieldDescriptionRoot != null)
        {
            if (shieldTitle == null)
                shieldTitle = FindDeepTextByName(shieldDescriptionRoot.transform, "TitleShield")
                    ?? FindDeepTextByName(shieldDescriptionRoot.transform, "Title")
                    ?? FindDeepTextByName(shieldDescriptionRoot.transform, "Name");
            if (shieldDesc == null)
                shieldDesc = FindDeepTextByName(shieldDescriptionRoot.transform, "DescShield")
                    ?? FindDeepTextByName(shieldDescriptionRoot.transform, "Desc_Custom")
                    ?? FindDeepTextByName(shieldDescriptionRoot.transform, "Desc")
                    ?? FindDeepTextByName(shieldDescriptionRoot.transform, "Description");
            if (shieldDamageText == null)
                shieldDamageText = FindStatValueText(shieldDescriptionRoot.transform, "Damage");
            if (shieldCriticalText == null)
                shieldCriticalText = FindStatValueText(shieldDescriptionRoot.transform, "Critical");
            if (shieldWeightText == null)
                shieldWeightText = FindStatValueText(shieldDescriptionRoot.transform, "Weight");
            if (shieldScalingText == null)
                shieldScalingText = FindStatValueText(shieldDescriptionRoot.transform, "Scaling");
            if (shieldRequirementsText == null)
                shieldRequirementsText = FindStatValueText(shieldDescriptionRoot.transform, "Requirement");
            if (weaponPhysicalDefenseText == null)
                weaponPhysicalDefenseText = FindStatValueText(shieldDescriptionRoot.transform, "Def Phy");
            if (weaponMagicDefenseText == null)
                weaponMagicDefenseText = FindStatValueText(shieldDescriptionRoot.transform, "Def Mag");

            if (shieldTitle == null)
                shieldTitle = weaponTitle;
            if (shieldDesc == null)
                shieldDesc = weaponDesc;
        }

        Transform itemRootTransform = itemDetailRoot != null ? itemDetailRoot.transform : null;
        if (itemRootTransform == null)
        {
            Transform explicitItemRoot = FindDeepChildByName(inventoryRoot, "DescItem");
            if (explicitItemRoot != null)
            {
                itemDetailRoot = explicitItemRoot.gameObject;
                itemRootTransform = explicitItemRoot;
            }
            else if (detailRoot != null)
            {
                itemDetailRoot = detailRoot;
                itemRootTransform = detailRoot.transform;
            }
        }

        if (itemRootTransform != null)
        {
            Transform itemFirstColumn = FindDeepChildByName(itemRootTransform, "FirstColumn")
                ?? FindDeepChildByName(itemRootTransform, "FirstCollumn")
                ?? FindDeepChildByName(itemRootTransform, "FirstCollumn (1)")
                ?? FindDeepChildByName(itemRootTransform, "FirstCollumn (2)");
            Transform itemSecondColumn = FindDeepChildByName(itemRootTransform, "SecondColumn")
                ?? FindDeepChildByName(itemRootTransform, "SecondCollumn")
                ?? FindDeepChildByName(itemRootTransform, "SecondCollumn (1)")
                ?? FindDeepChildByName(itemRootTransform, "SecondCollumn (2)");

            if (itemImage == null || itemImage == detailIcon)
            {
                Transform imageTf = itemFirstColumn != null
                    ? FindDeepChildByName(itemFirstColumn, "Image")
                    : FindDeepChildByName(itemRootTransform, "Image");
                if (imageTf != null) itemImage = imageTf.GetComponent<Image>();
            }

            if (itemTitle == null || itemTitle == detailTitle)
            {
                itemTitle = itemFirstColumn != null
                    ? FindDeepTextByName(itemFirstColumn, "Title")
                        ?? FindDeepTextByName(itemFirstColumn, "Title desc")
                        ?? FindDeepTextByName(itemFirstColumn, "Name")
                    : null;

                if (itemTitle == null)
                    itemTitle = FindDeepTextByName(itemRootTransform, "Title")
                        ?? FindDeepTextByName(itemRootTransform, "Title desc")
                        ?? FindDeepTextByName(itemRootTransform, "Name");
            }

            if (itemDesc == null || itemDesc == detailDescription)
            {
                itemDesc = itemSecondColumn != null
                    ? FindDeepTextByName(itemSecondColumn, "Desc_Custom")
                        ?? FindDeepTextByName(itemSecondColumn, "DescCustom")
                        ?? FindDeepTextByName(itemSecondColumn, "Desc")
                        ?? FindDeepTextByName(itemSecondColumn, "Description")
                    : null;

                if (itemDesc == null)
                    itemDesc = FindDeepTextByName(itemRootTransform, "Desc_Custom")
                        ?? FindDeepTextByName(itemRootTransform, "DescCustom")
                        ?? FindDeepTextByName(itemRootTransform, "Desc")
                        ?? FindDeepTextByName(itemRootTransform, "Description");
            }
        }

        if (armorDescriptionRoot == null)
            armorDescriptionRoot = FindDeepChildByName(detailRootTransform, "ArmorColumn")?.gameObject
                ?? FindDeepChildByName(detailRootTransform, "ArmorCollumn")?.gameObject;

        if (armorDescriptionRoot == null)
            return;

        if (armorTitle == null)
            armorTitle = FindDeepTextByName(armorDescriptionRoot.transform, "TitleArmor")
                ?? FindDeepTextByName(armorDescriptionRoot.transform, "Title")
                ?? FindDeepTextByName(armorDescriptionRoot.transform, "Name");

        if (armorDesc == null)
            armorDesc = FindDeepTextByName(armorDescriptionRoot.transform, "DescArmor")
                ?? FindDeepTextByName(armorDescriptionRoot.transform, "Desc_Custom")
                ?? FindDeepTextByName(armorDescriptionRoot.transform, "Desc")
                ?? FindDeepTextByName(armorDescriptionRoot.transform, "Description");
        if (armorWeightText == null)
            armorWeightText = FindStatValueText(armorDescriptionRoot.transform, "Weight");
        if (armorPhysicalDefenseText == null)
            armorPhysicalDefenseText = FindStatValueText(armorDescriptionRoot.transform, "Def Phy");
        if (armorMagicDefenseText == null)
            armorMagicDefenseText = FindStatValueText(armorDescriptionRoot.transform, "Def Mag");

        // Preserve existing UI behaviour when the armor panel has no dedicated description text yet.
        if (armorTitle == null)
            armorTitle = weaponTitle;
        if (armorDesc == null)
            armorDesc = weaponDesc;
    }

    private static TextMeshProUGUI FindStatValueText(Transform root, string statRootName)
    {
        Transform statRoot = FindDeepChildByName(root, statRootName);
        if (statRoot == null)
            return null;

        var texts = statRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (texts == null || texts.Length == 0)
            return null;

        return texts[texts.Length - 1];
    }

    private static Transform FindDeepChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
                return child;

            Transform nested = FindDeepChildByName(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static TextMeshProUGUI FindDeepTextByName(Transform root, string childName)
    {
        Transform tf = FindDeepChildByName(root, childName);
        return tf != null ? tf.GetComponent<TextMeshProUGUI>() : null;
    }

    private List<InventoryItem> NormalizeSourceItems(List<InventoryItem> data)
    {
        return data != null ? new List<InventoryItem>(data) : new List<InventoryItem>();
    }

    private void PrepareEquipSelectionView(System.Action applyFilter)
    {
        EnsurePlayerInventory();
        EnsureEquipmentManager();
        CacheEquipSelectionContext();
        SwitchSlotContainer(inventorySlotParent != null ? inventorySlotParent : slotParent, inventoryInitialSlotCount);
        equipmentManager?.ShowInventoryPanel();
        RefreshSourceItemsFromPlayer();
        applyFilter?.Invoke();
        UpdateEquipButtonState();
    }

    private void SetWeaponStatsRootActive(bool active)
    {
        if (weaponStatsRoot == null)
            return;

        // In current scenes weaponStatsRoot is often the same object as weaponDetailRoot (DescWeapon).
        // Avoid disabling the whole detail panel when switching to shield/armor variants.
        if (weaponStatsRoot == weaponDetailRoot)
            return;

        weaponStatsRoot.SetActive(active);
    }

    private void SwitchSlotContainer(Transform newParent, int minSlotCount)
    {
        if (newParent == null) return;

        if (slotParent != newParent)
        {
            slotParent = newParent;
            slots.Clear();
        }

        if (slotParent != null && slots.Count == 0)
        {
            var existing = slotParent.GetComponentsInChildren<InventorySlot>(true);
            slots.AddRange(existing);
            for (int i = 0; i < slots.Count; i++)
                slots[i].Init(i, this);
        }

        int targetCount = Mathf.Max(0, minSlotCount);
        if (targetCount > 0)
            EnsureSlots(targetCount);
    }

    private void ClearAllSlots()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
                slots[i].Clear();
        }
    }

    private void EnsureSlots(int required)
    {
        if (slotParent == null) slotParent = transform;
        if (slotParent == null) return;

        if (slotPrefab == null)
        {
            if (slots.Count < required && slots.Count > 0)
            {
                var template = slots[0];
                while (slots.Count < required)
                {
                    var clone = Instantiate(template, slotParent);
                    clone.Init(slots.Count, this);
                    clone.gameObject.SetActive(true);
                    slots.Add(clone);
                }
            }
            return;
        }

        while (slots.Count < required)
        {
            var slot = Instantiate(slotPrefab, slotParent);
            slot.Init(slots.Count, this);
            slot.gameObject.SetActive(true);
            slots.Add(slot);
        }
    }

    private void UpdateEquipButtonState()
    {
        if (equipButton == null)
            return;

        bool canEquip = CanEquipCurrentSelection();
        equipButton.gameObject.SetActive(canEquip);
        equipButton.interactable = canEquip;
    }

    private void ResetEquipTarget()
    {
        equipmentManager?.ResetEquipTarget();

        if (equipButton == null)
            return;

        equipButton.gameObject.SetActive(false);
        equipButton.interactable = false;
    }

    private bool IsEquipButtonInteractable()
    {
        return equipButton != null
               && equipButton.gameObject.activeInHierarchy
               && equipButton.interactable;
    }

    private bool CanEquipCurrentSelection()
    {
        EnsureEquipmentManager();
        if (currentSelectedIndex < 0 || !HasItem(currentSelectedIndex))
            return false;

        return CanEquipItemForTarget(currentItems[currentSelectedIndex], GetEffectiveEquipTarget());
    }

    private bool CanEquipItemForTarget(InventoryItem item, EquipmentManager.EquipTarget target)
    {
        if (item == null)
            return false;

        if (item.weaponData != null)
            return CanEquipWeaponForTarget(target);

        if (item.armorData != null)
            return CanEquipArmorForTarget(item, target);

        if (item.usableData != null)
            return CanEquipUsableForTarget(target);

        return false;
    }

    private static bool CanEquipWeaponForTarget(EquipmentManager.EquipTarget target)
    {
        return target == EquipmentManager.EquipTarget.None
               || target == EquipmentManager.EquipTarget.Right
               || target == EquipmentManager.EquipTarget.Left;
    }

    private bool CanEquipArmorForTarget(InventoryItem item, EquipmentManager.EquipTarget target)
    {
        if (item?.armorData == null)
            return false;

        if (target == EquipmentManager.EquipTarget.None)
            return true;

        return target == EquipmentManager.EquipTarget.Armor
               && item.armorData.slot == GetEffectiveArmorSlot();
    }

    private static bool CanEquipUsableForTarget(EquipmentManager.EquipTarget target)
    {
        return target == EquipmentManager.EquipTarget.None
               || target == EquipmentManager.EquipTarget.Bottom;
    }

    private Sprite GetItemIcon(InventoryItem item)
    {
        if (item == null) return null;
        if (item.icon != null) return item.icon;
        if (item.weaponData != null && item.weaponData.icon != null) return item.weaponData.icon;
        if (item.armorData != null && item.armorData.icon != null) return item.armorData.icon;
        if (item.usableData != null && item.usableData.icon != null) return item.usableData.icon;
        if (item.itemData != null && item.itemData.icon != null) return item.itemData.icon;
        return null;
    }

    private bool IsItemEquipped(InventoryItem item)
    {
        if (item == null || string.IsNullOrEmpty(item.instanceId)) return false;
        EnsurePlayerInventory();
        return playerInventory != null && playerInventory.IsInstanceEquipped(item.instanceId);
    }

    private void SetPadFocus(int index)
    {
        if (slots.Count == 0 || index < 0 || index >= slots.Count) return;

        padFocusIndex = index;
        ApplyPadFocusVisual(index);
        HandleSlotSelected(index);

        if (EventSystem.current != null && slots[index] != null)
            EventSystem.current.SetSelectedGameObject(slots[index].gameObject);
    }

    private void ApplyPadFocusVisual(int focusedIndex)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
                slots[i].SetFocused(showPadFocus && i == focusedIndex);
        }
    }

    private int GetGridColumnCount()
    {
        var grid = slotParent != null ? slotParent.GetComponent<GridLayoutGroup>() : null;
        if (grid != null && grid.constraintCount > 0)
        {
            if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
                return Mathf.Max(1, grid.constraintCount);

            if (grid.constraint == GridLayoutGroup.Constraint.FixedRowCount)
            {
                int rows = Mathf.Max(1, grid.constraintCount);
                return Mathf.Max(1, Mathf.CeilToInt((float)slots.Count / rows));
            }
        }

        return Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(slots.Count)));
    }

    private void CreateDragPreview(Sprite icon, PointerEventData eventData, Vector2 iconSize)
    {
        ClearDragPreview();
        if (icon == null) return;

        Canvas targetCanvas = dragCanvas;
        if (targetCanvas == null)
            targetCanvas = GetComponentInParent<Canvas>();
        if (targetCanvas == null)
            targetCanvas = FindObjectOfType<Canvas>();
        if (targetCanvas == null) return;

        if (dragPreviewTemplate == null)
        {
            GameObject go = new GameObject("DragPreview");
            go.transform.SetParent(targetCanvas.transform, false);
            activeDragPreview = go.AddComponent<Image>();
            activeDragPreview.raycastTarget = false;
        }
        else
        {
            activeDragPreview = Instantiate(dragPreviewTemplate, targetCanvas.transform);
        }

        activeDragPreview.sprite = icon;
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

    private void CachePlayerStats()
    {
        if (playerStats != null) return;
        playerStats = PlayerStats.instance != null ? PlayerStats.instance : FindObjectOfType<PlayerStats>();
        if (playerStats != null)
        {
            playerStats.OnBankChanged += HandleBankChanged;
            playerStats.OnRunWalletChanged += HandleRunWalletChanged;
            playerStats.OnKeysChanged += HandleKeysChanged;
        }
    }

    private void HandleBankChanged(int gold, int silver, int copper)
    {
        if (walletSource == WalletSource.Bank)
            SetWalletValues(gold, silver, copper);
    }

    private void HandleRunWalletChanged(int gold, int silver, int copper)
    {
        if (walletSource == WalletSource.Run)
            SetWalletValues(gold, silver, copper);
        SetKeyValue(playerStats != null ? playerStats.currentKeys : 0);
    }

    private void HandleKeysChanged(int keys)
    {
        SetKeyValue(keys);
    }

    private void SetWalletValues(int gold, int silver, int copper)
    {
        if (goldValueText != null) goldValueText.text = gold.ToString();
        if (silverValueText != null) silverValueText.text = silver.ToString();
        if (copperValueText != null) copperValueText.text = copper.ToString();
    }

    private void SetKeyValue(int keys)
    {
        if (keyValueText != null) keyValueText.text = Mathf.Max(0, keys).ToString();
    }

    private bool TryEquipFocusedPadItem()
    {
        var target = GetEffectiveEquipTarget();
        if (target == EquipmentManager.EquipTarget.None) return false;
        if (padFocusIndex < 0 || !HasItem(padFocusIndex)) return false;

        currentSelectedIndex = padFocusIndex;
        var focused = currentItems[padFocusIndex];
        if (focused == null) return false;

        if (!CanEquipItemForTarget(focused, target))
            return false;

        OnEquipButtonClick();
        return true;
    }

    private void CompleteEquipAction()
    {
        EnsureEquipmentManager();
        RefreshSlot(currentSelectedIndex);
        RefreshDetailSelection();
        equipmentManager?.RefreshEquipmentCross();
        ResetEquipTarget();
        if (equipmentManager != null)
        {
            equipmentManager.CloseEquipGrid();
            equipmentManager.FocusEquipmentCrossDefault();
        }
        else
        {
            CloseEquipGridView();
        }

        ForceShowEquipmentView();
    }

    private void CompleteDirectEquipAction()
    {
        RefreshSlot(currentSelectedIndex);
        RefreshDetailSelection();
        equipmentManager?.RefreshEquipmentCross();
    }

    private EquipmentManager.EquipTarget GetEffectiveEquipTarget()
    {
        EquipmentManager.EquipTarget currentTarget = equipmentManager != null ? equipmentManager.CurrentEquipTarget : EquipmentManager.EquipTarget.None;
        if (currentTarget != EquipmentManager.EquipTarget.None)
            return currentTarget;

        return equipSelectionMode ? pendingEquipTarget : EquipmentManager.EquipTarget.None;
    }

    private int GetEffectiveEquipSlot()
    {
        if (equipmentManager != null && equipmentManager.CurrentEquipTarget != EquipmentManager.EquipTarget.None)
            return equipmentManager.CurrentEquipSlot;

        return pendingEquipSlot;
    }

    private ArmorItemData.ArmorSlot GetEffectiveArmorSlot()
    {
        if (equipmentManager != null && equipmentManager.CurrentEquipTarget == EquipmentManager.EquipTarget.Armor)
            return equipmentManager.CurrentArmorSlot;

        return pendingArmorSlot;
    }

    private void CacheEquipSelectionContext()
    {
        if (equipmentManager == null)
            return;

        pendingEquipTarget = equipmentManager.CurrentEquipTarget;
        pendingEquipSlot = equipmentManager.CurrentEquipSlot;
        pendingArmorSlot = equipmentManager.CurrentArmorSlot;
        equipSelectionMode = pendingEquipTarget != EquipmentManager.EquipTarget.None;
    }

    private void ClearEquipSelectionContext()
    {
        equipSelectionMode = false;
        pendingEquipTarget = EquipmentManager.EquipTarget.None;
        pendingEquipSlot = 0;
        pendingArmorSlot = ArmorItemData.ArmorSlot.Helmet;
    }

    private void CompletePostDirectEquipAction()
    {
        bool shouldReturnToEquipment = equipSelectionMode
                                       || GetEffectiveEquipTarget() != EquipmentManager.EquipTarget.None
                                       || IsEquipmentTabActive();

        if (shouldReturnToEquipment)
            CompleteEquipAction();
        else
            CompleteDirectEquipAction();
    }

    private bool IsEquipmentTabActive()
    {
        EnsureEquipmentManager();
        if (equipmentManager != null && equipmentManager.IsEquipmentCrossModeActive())
            return true;

        MenuManager menuManager = FindObjectOfType<MenuManager>(true);
        return menuManager != null
               && string.Equals(menuManager.CurrentTabKey, "Equipment", System.StringComparison.OrdinalIgnoreCase);
    }

    private void ForceShowEquipmentView()
    {
        Transform root = transform.root;
        if (root == null)
            return;

        Transform inventoryRoot = FindDeepChildByName(root, "invBackground");
        if (inventoryRoot != null)
            inventoryRoot.gameObject.SetActive(false);

        Transform equipmentRoot = FindDeepChildByName(root, "EquipmentBackground");
        if (equipmentRoot != null)
            equipmentRoot.gameObject.SetActive(true);

        Transform magicRoot = FindDeepChildByName(root, "MagicBackground");
        if (magicRoot != null)
            magicRoot.gameObject.SetActive(false);
    }

}
