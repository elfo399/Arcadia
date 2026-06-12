using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class EquipmentManager : MonoBehaviour, IInventorySlotHandler
{
    public enum EquipTarget { None, Right, Left, Bottom, Top, Armor }
    private enum EquipCrossFocus { Right, Left, Bottom, Top, Armor }
    private const int RightSlotBase = 0;
    private const int LeftSlotBase = 3;
    private const int BottomSlotBase = 6;
    private const int TopSlotBase = 9;
    private const int ArmorSlotBase = 12;

    [Header("Equipment Slot Prefab")]
    [SerializeField] private InventorySlot slotPrefab;

    [Header("HUD Cross Icons (solo overlay esterno)")]
    [SerializeField] private Image hudCrossTop;
    [SerializeField] private Image hudCrossRight;
    [SerializeField] private Image hudCrossBottom;
    [SerializeField] private Image hudCrossLeft;

    [Header("HUD Cross Containers")]
    [SerializeField] private Transform hudRightContainer;
    [SerializeField] private Transform hudLeftContainer;
    [SerializeField] private Transform hudBottomContainer;
    [SerializeField] private Transform hudTopContainer;

    [Header("Equipment Slot Containers")]
    [SerializeField] private Transform rightEquipContainer;
    [SerializeField] private Transform rightEquipContainer2;
    [SerializeField] private Transform rightEquipContainer3;
    [SerializeField] private Transform leftEquipContainer;
    [SerializeField] private Transform leftEquipContainer2;
    [SerializeField] private Transform leftEquipContainer3;
    [SerializeField] private Transform bottomEquipContainer;
    [SerializeField] private Transform bottomEquipContainer2;
    [SerializeField] private Transform bottomEquipContainer3;
    [SerializeField] private Transform topEquipContainer;
    [SerializeField] private Transform topEquipContainer2;
    [SerializeField] private Transform topEquipContainer3;

    [Header("Equipment Roots")]
    [SerializeField] private GameObject equipmentBackground;
    [SerializeField] private GameObject inventoryBackground;
    [SerializeField] private GameObject magicBackground;

    [Header("Armor Slot Containers")]
    [SerializeField] private Transform armorHelmetContainer;
    [SerializeField] private Transform armorChestplateContainer;
    [SerializeField] private Transform armorLeggingsContainer;
    [SerializeField] private Transform armorBootsContainer;

    [Header("Dependencies")]
    [SerializeField] private InventoryUIManager inventoryUIManager;
    [SerializeField] private MagicInventoryManager magicInventoryManager;

    private PlayerInventory playerInventory;
    private readonly InventorySlot[] rightEquipSlots = new InventorySlot[3];
    private readonly InventorySlot[] leftEquipSlots = new InventorySlot[3];
    private readonly InventorySlot[] bottomEquipSlots = new InventorySlot[3];
    private readonly InventorySlot[] topEquipSlots = new InventorySlot[3];
    private InventorySlot hudRightSlot;
    private InventorySlot hudLeftSlot;
    private InventorySlot hudBottomSlot;
    private InventorySlot hudTopSlot;
    private readonly InventorySlot[] armorEquipSlots = new InventorySlot[4];
    private bool equipSlotsBuilt;
    private bool hudSlotsBuilt;
    private bool showPadFocus;
    private EquipCrossFocus equipCrossFocus = EquipCrossFocus.Right;
    private int currentTopIndex;
    private int currentArmorIndex;

    public EquipTarget CurrentEquipTarget { get; private set; } = EquipTarget.None;
    public int CurrentEquipSlot { get; private set; }
    public ArmorItemData.ArmorSlot CurrentArmorSlot { get; private set; } = ArmorItemData.ArmorSlot.Helmet;
    public int CurrentTopIndex => currentTopIndex;
    public GameObject MagicBackground => magicBackground;

    public void Initialize(PlayerInventory inventory, InventoryUIManager inventoryManager = null, MagicInventoryManager magicManager = null)
    {
        playerInventory = inventory != null ? inventory : playerInventory;
        inventoryUIManager = inventoryManager != null ? inventoryManager : inventoryUIManager;
        magicInventoryManager = magicManager != null ? magicManager : magicInventoryManager;
        BuildEquipSlotsIfNeeded();
        BuildHudSlotsIfNeeded();
        RefreshEquipmentCross();
    }

    public void SetPlayerInventory(PlayerInventory inventory)
    {
        playerInventory = inventory;
    }

    public void SetPadFocusVisible(bool visible)
    {
        showPadFocus = visible;
        ApplyEquipmentCrossFocusVisual();
    }

    public void ResetEquipTarget()
    {
        CurrentEquipTarget = EquipTarget.None;
    }

    public bool IsEquipmentCrossModeActive()
    {
        if (equipmentBackground == null) return false;
        bool equipVisible = equipmentBackground.activeInHierarchy;
        bool invHidden = inventoryBackground == null || !inventoryBackground.activeInHierarchy;
        return equipVisible && invHidden;
    }

    public bool HasEquipGridOpen()
    {
        return (inventoryBackground != null && inventoryBackground.activeInHierarchy)
               || (magicBackground != null && magicBackground.activeInHierarchy);
    }

    public void BeginEquipRight(int slot)
    {
        EnsurePlayerInventory();
        CurrentEquipTarget = EquipTarget.Right;
        CurrentEquipSlot = Mathf.Clamp(slot, 0, 2);
        if (playerInventory != null)
            playerInventory.currentRightIndex = CurrentEquipSlot;
        inventoryUIManager?.PrepareWeaponEquipSelectionView();
    }

    public void BeginEquipLeft(int slot)
    {
        EnsurePlayerInventory();
        CurrentEquipTarget = EquipTarget.Left;
        CurrentEquipSlot = Mathf.Clamp(slot, 0, 2);
        if (playerInventory != null)
            playerInventory.currentLeftIndex = CurrentEquipSlot;
        inventoryUIManager?.PrepareWeaponEquipSelectionView();
    }

    public void BeginEquipBottom(int slot)
    {
        EnsurePlayerInventory();
        CurrentEquipTarget = EquipTarget.Bottom;
        CurrentEquipSlot = Mathf.Clamp(slot, 0, 2);
        if (playerInventory != null)
            playerInventory.currentUsableIndex = CurrentEquipSlot;
        inventoryUIManager?.PrepareUsableEquipSelectionView();
    }

    public void BeginEquipTop(int slot)
    {
        EnsurePlayerInventory();
        CurrentEquipTarget = EquipTarget.Top;
        currentTopIndex = Mathf.Clamp(slot, 0, 2);
        if (playerInventory != null)
            playerInventory.currentMagicIndex = currentTopIndex;
        magicInventoryManager?.PrepareMagicEquipSelectionView();
    }

    public void BeginEquipArmor(ArmorItemData.ArmorSlot slot)
    {
        EnsurePlayerInventory();
        CurrentEquipTarget = EquipTarget.Armor;
        CurrentArmorSlot = slot;
        currentArmorIndex = Mathf.Clamp((int)slot, 0, armorEquipSlots.Length - 1);
        inventoryUIManager?.PrepareArmorEquipSelectionView(slot);
    }

    public void OnArmorHelmetClick() => BeginEquipArmor(ArmorItemData.ArmorSlot.Helmet);
    public void OnArmorChestplateClick() => BeginEquipArmor(ArmorItemData.ArmorSlot.Chestplate);
    public void OnArmorLeggingsClick() => BeginEquipArmor(ArmorItemData.ArmorSlot.Leggings);
    public void OnArmorBootsClick() => BeginEquipArmor(ArmorItemData.ArmorSlot.Boots);

    public void CloseEquipGrid()
    {
        ShowEquipmentInventory(false);
        ResetEquipTarget();
        inventoryUIManager?.CloseEquipGridView();
        magicInventoryManager?.CloseEquipGridView();
    }

    public void ShowInventoryPanel()
    {
        ShowEquipmentInventory(true);
    }

    public void HideMenuContentPanels()
    {
        if (equipmentBackground != null) equipmentBackground.SetActive(false);
        if (inventoryBackground != null) inventoryBackground.SetActive(false);
        if (magicBackground != null) magicBackground.SetActive(false);
    }

    public void FocusEquipmentCrossDefault()
    {
        EnsurePlayerInventory();
        int idx = playerInventory != null ? Mathf.Clamp(playerInventory.currentRightIndex, 0, 2) : 0;
        SetEquipmentCrossFocus(EquipCrossFocus.Right, idx);
    }

    public void NavigateEquipmentRight() => MoveEquipmentFocus(Vector2.right);
    public void NavigateEquipmentLeft() => MoveEquipmentFocus(Vector2.left);
    public void NavigateEquipmentDown() => MoveEquipmentFocus(Vector2.down);
    public void NavigateEquipmentUp() => MoveEquipmentFocus(Vector2.up);

    public void ConfirmEquipmentSelection()
    {
        switch (equipCrossFocus)
        {
            case EquipCrossFocus.Right:
                BeginEquipRight(GetCurrentCrossIndex(EquipCrossFocus.Right));
                break;
            case EquipCrossFocus.Left:
                BeginEquipLeft(GetCurrentCrossIndex(EquipCrossFocus.Left));
                break;
            case EquipCrossFocus.Bottom:
                BeginEquipBottom(GetCurrentCrossIndex(EquipCrossFocus.Bottom));
                break;
            case EquipCrossFocus.Top:
                BeginEquipTop(GetCurrentCrossIndex(EquipCrossFocus.Top));
                break;
            case EquipCrossFocus.Armor:
                BeginEquipArmor((ArmorItemData.ArmorSlot)Mathf.Clamp(GetCurrentCrossIndex(EquipCrossFocus.Armor), 0, armorEquipSlots.Length - 1));
                break;
        }
    }

    public void RefreshEquipmentCross()
    {
        BuildEquipSlotsIfNeeded();
        BuildHudSlotsIfNeeded();
        EnsurePlayerInventory();

        if (playerInventory != null)
        {
            var rightEquipped = playerInventory.GetWeaponForHand(Hand.Right);
            var leftEquipped = playerInventory.GetWeaponForHand(Hand.Left);
            var rightFrontIcon = rightEquipped != null ? rightEquipped.icon : null;
            var leftFrontIcon = leftEquipped != null ? leftEquipped.icon : null;
            int rightHudAmount = 1;
            int leftHudAmount = 1;
            bool rightShowAmmo = false;
            bool leftShowAmmo = false;

            if (rightEquipped != null && rightEquipped.category == WeaponCategory.Bow)
            {
                rightHudAmount = playerInventory.GetAmmoCountForWeapon(rightEquipped);
                rightShowAmmo = true;
            }
            if (leftEquipped != null && leftEquipped.category == WeaponCategory.Bow)
            {
                leftHudAmount = playerInventory.GetAmmoCountForWeapon(leftEquipped);
                leftShowAmmo = true;
            }

            SetBackLayerIcon(hudCrossRight);
            SetBackLayerIcon(hudCrossLeft);

            UpdateEquipVisuals(rightEquipSlots, playerInventory.rightLoadout);
            UpdateEquipVisuals(leftEquipSlots, playerInventory.leftLoadout);

            UpdateEquipVisual(hudRightSlot, rightFrontIcon, rightHudAmount, rightShowAmmo);
            UpdateEquipVisual(hudLeftSlot, leftFrontIcon, leftHudAmount, leftShowAmmo);
        }

        Sprite usableIcon = null;
        int usableAmount = 1;
        if (playerInventory != null && playerInventory.GetCurrentUsable() != null)
        {
            usableIcon = playerInventory.GetCurrentUsable().icon;
            if (playerInventory.TryPeekCurrentUsable(out _, out int currentAmount))
                usableAmount = Mathf.Max(1, currentAmount);
        }
        SetBackLayerIcon(hudCrossBottom);
        UpdateEquipVisuals(bottomEquipSlots, playerInventory != null ? playerInventory.usableLoadout : null);
        UpdateHudVisual(hudBottomSlot, usableIcon, usableAmount, usableAmount > 0);

        SetBackLayerIcon(hudCrossTop);
        UpdateEquipVisuals(topEquipSlots, playerInventory != null ? playerInventory.magicLoadout : null);
        var magicEquipped = playerInventory != null ? playerInventory.GetCurrentMagic() : null;
        UpdateHudVisual(hudTopSlot, magicEquipped != null ? magicEquipped.icon : null, 1);

        UpdateEquipVisuals(armorEquipSlots, playerInventory != null ? playerInventory.armorLoadout : null);

        ApplyEquipmentCrossFocusVisual();
    }

    private void ShowEquipmentInventory(bool showInventoryPanel)
    {
        if (equipmentBackground != null) equipmentBackground.SetActive(!showInventoryPanel);
        if (inventoryBackground != null) inventoryBackground.SetActive(showInventoryPanel);
        if (magicBackground != null) magicBackground.SetActive(false);
    }

    private void ShowEquipmentMagic(bool showMagicPanel)
    {
        if (equipmentBackground != null) equipmentBackground.SetActive(!showMagicPanel);
        if (inventoryBackground != null) inventoryBackground.SetActive(false);
        if (magicBackground != null) magicBackground.SetActive(showMagicPanel);
    }

    public void ShowMagicPanel()
    {
        ShowEquipmentMagic(true);
    }

    private void BuildEquipSlotsIfNeeded()
    {
        if (equipSlotsBuilt) return;

        rightEquipSlots[0] = CreateEquipSlot(rightEquipContainer, RightSlotBase, this, false);
        rightEquipSlots[1] = CreateEquipSlot(rightEquipContainer2, RightSlotBase + 1, this, false);
        rightEquipSlots[2] = CreateEquipSlot(rightEquipContainer3, RightSlotBase + 2, this, false);

        leftEquipSlots[0] = CreateEquipSlot(leftEquipContainer, LeftSlotBase, this, false);
        leftEquipSlots[1] = CreateEquipSlot(leftEquipContainer2, LeftSlotBase + 1, this, false);
        leftEquipSlots[2] = CreateEquipSlot(leftEquipContainer3, LeftSlotBase + 2, this, false);

        bottomEquipSlots[0] = CreateEquipSlot(bottomEquipContainer, BottomSlotBase, this, false);
        bottomEquipSlots[1] = CreateEquipSlot(bottomEquipContainer2, BottomSlotBase + 1, this, false);
        bottomEquipSlots[2] = CreateEquipSlot(bottomEquipContainer3, BottomSlotBase + 2, this, false);

        topEquipSlots[0] = CreateEquipSlot(topEquipContainer, TopSlotBase, this, false);
        topEquipSlots[1] = CreateEquipSlot(topEquipContainer2, TopSlotBase + 1, this, false);
        topEquipSlots[2] = CreateEquipSlot(topEquipContainer3, TopSlotBase + 2, this, false);

        ResolveArmorContainersFromHierarchy();
        armorEquipSlots[0] = CreateEquipSlot(armorHelmetContainer, ArmorSlotBase, this, false);
        armorEquipSlots[1] = CreateEquipSlot(armorChestplateContainer, ArmorSlotBase + 1, this, false);
        armorEquipSlots[2] = CreateEquipSlot(armorLeggingsContainer, ArmorSlotBase + 2, this, false);
        armorEquipSlots[3] = CreateEquipSlot(armorBootsContainer, ArmorSlotBase + 3, this, false);

        equipSlotsBuilt = true;
    }

    private void BuildHudSlotsIfNeeded()
    {
        if (hudSlotsBuilt) return;

        hudRightSlot = CreateEquipSlot(hudRightContainer);
        hudLeftSlot = CreateEquipSlot(hudLeftContainer);
        hudBottomSlot = CreateEquipSlot(hudBottomContainer);
        hudTopSlot = CreateEquipSlot(hudTopContainer);

        if (hudRightSlot) hudRightSlot.gameObject.SetActive(true);
        if (hudLeftSlot) hudLeftSlot.gameObject.SetActive(true);
        if (hudBottomSlot) hudBottomSlot.gameObject.SetActive(true);
        if (hudTopSlot) hudTopSlot.gameObject.SetActive(true);

        hudSlotsBuilt = true;
    }

    private InventorySlot CreateEquipSlot(Transform parent, int slotIndex = -1, IInventorySlotHandler owner = null, bool displayOnly = true)
    {
        if (slotPrefab == null || parent == null) return null;

        var existing = parent.GetComponentInChildren<InventorySlot>();
        if (existing != null)
        {
            existing.Init(slotIndex, owner);
            existing.SetDisplayOnly(displayOnly);
            var existingImage = existing.GetComponent<Image>();
            if (existingImage != null) existingImage.raycastTarget = !displayOnly;
            return existing;
        }

        var slot = Instantiate(slotPrefab, parent);
        slot.Init(slotIndex, owner);
        slot.SetDisplayOnly(displayOnly);
        slot.gameObject.SetActive(true);
        var img = slot.GetComponent<Image>();
        if (img != null) img.raycastTarget = !displayOnly;
        return slot;
    }

    private void ResolveArmorContainersFromHierarchy()
    {
        armorHelmetContainer = ResolveArmorContainer(armorHelmetContainer, "helmet");
        armorChestplateContainer = ResolveArmorContainer(armorChestplateContainer, "chestplate");
        armorLeggingsContainer = ResolveArmorContainer(armorLeggingsContainer, "leggings");
        armorBootsContainer = ResolveArmorContainer(armorBootsContainer, "boots");

        LogMissingArmorContainer(armorHelmetContainer, "helmet");
        LogMissingArmorContainer(armorChestplateContainer, "chestplate");
        LogMissingArmorContainer(armorLeggingsContainer, "leggings");
        LogMissingArmorContainer(armorBootsContainer, "boots");
    }

    private void SetBackLayerIcon(Image target)
    {
        if (target == null) return;
        target.sprite = null;
        target.enabled = false;
    }

    private void UpdateEquipVisual(InventorySlot slot, Sprite icon, int amount, bool forceShowQuantity = false)
    {
        if (slot == null) return;
        if (icon != null)
            slot.Setup(icon, amount, false, forceShowQuantity);
        else
            slot.Clear();
    }

    private void UpdateHudVisual(InventorySlot slot, Sprite icon, int amount, bool forceShowQuantity = false)
    {
        if (slot == null) return;
        slot.gameObject.SetActive(true);
        if (icon != null)
            slot.Setup(icon, Mathf.Max(forceShowQuantity ? 0 : 1, amount), false, forceShowQuantity);
        else
            slot.Clear();
    }

    private void UpdateEquipVisuals(InventorySlot[] slots, WeaponItem[] loadout)
    {
        if (slots == null || loadout == null) return;
        int len = Mathf.Min(slots.Length, loadout.Length);
        for (int i = 0; i < len; i++)
            UpdateEquipVisual(slots[i], loadout[i] != null ? loadout[i].icon : null, 1);
    }

    private void UpdateEquipVisuals(InventorySlot[] slots, UsableItemData[] loadout)
    {
        if (slots == null || loadout == null) return;
        int len = Mathf.Min(slots.Length, loadout.Length);
        for (int i = 0; i < len; i++)
            UpdateEquipVisual(slots[i], loadout[i] != null ? loadout[i].icon : null, 1);
    }

    private void UpdateEquipVisuals(InventorySlot[] slots, MagicItemData[] loadout)
    {
        if (slots == null || loadout == null) return;
        int len = Mathf.Min(slots.Length, loadout.Length);
        for (int i = 0; i < len; i++)
            UpdateEquipVisual(slots[i], loadout[i] != null ? loadout[i].icon : null, 1);
    }

    private void UpdateEquipVisuals(InventorySlot[] slots, ArmorItemData[] loadout)
    {
        if (slots == null || loadout == null) return;
        int len = Mathf.Min(slots.Length, loadout.Length);
        for (int i = 0; i < len; i++)
            UpdateEquipVisual(slots[i], loadout[i] != null ? loadout[i].icon : null, 1);
    }

    private int GetCurrentCrossIndex(EquipCrossFocus focus)
    {
        EnsurePlayerInventory();
        switch (focus)
        {
            case EquipCrossFocus.Right:
                return playerInventory != null ? Mathf.Clamp(playerInventory.currentRightIndex, 0, 2) : 0;
            case EquipCrossFocus.Left:
                return playerInventory != null ? Mathf.Clamp(playerInventory.currentLeftIndex, 0, 2) : 0;
            case EquipCrossFocus.Bottom:
                return playerInventory != null ? Mathf.Clamp(playerInventory.currentUsableIndex, 0, 2) : 0;
            case EquipCrossFocus.Top:
                if (playerInventory != null)
                    return Mathf.Clamp(playerInventory.currentMagicIndex, 0, 2);
                return Mathf.Clamp(currentTopIndex, 0, 2);
            case EquipCrossFocus.Armor:
                return Mathf.Clamp(currentArmorIndex, 0, Mathf.Max(0, armorEquipSlots.Length - 1));
            default:
                return 0;
        }
    }

    private void MoveEquipmentFocus(Vector2 direction)
    {
        BuildEquipSlotsIfNeeded();

        if (TryMoveEquipmentFocusShortcut(direction))
            return;

        InventorySlot currentSlot = GetCurrentCrossSlot();
        if (currentSlot == null)
        {
            FocusEquipmentCrossDefault();
            return;
        }

        Vector2 dir = direction.normalized;
        Vector2 currentPos = GetSlotCenter(currentSlot);
        bool found = false;
        float bestScore = float.NegativeInfinity;
        CrossSlotRef best = default;

        foreach (var candidate in EnumerateCrossSlots())
        {
            if (candidate.slot == null) continue;

            int focusedIndex = GetCurrentCrossIndex(candidate.focus);
            if (candidate.focus == equipCrossFocus && candidate.index == focusedIndex) continue;

            Vector2 delta = GetSlotCenter(candidate.slot) - currentPos;
            if (delta.sqrMagnitude < 0.01f) continue;

            Vector2 deltaNorm = delta.normalized;
            float forward = Vector2.Dot(deltaNorm, dir);
            if (forward <= 0.15f) continue;

            float lateral = Mathf.Abs(Vector2.Dot(deltaNorm, new Vector2(-dir.y, dir.x)));
            float distance = delta.magnitude;
            float score = (forward * 3f) - lateral - (distance * 0.0025f);

            if (!found || score > bestScore)
            {
                found = true;
                bestScore = score;
                best = candidate;
            }
        }

        if (found)
            SetEquipmentCrossFocus(best.focus, best.index);
    }

    private bool TryMoveEquipmentFocusShortcut(Vector2 direction)
    {
        if (equipCrossFocus == EquipCrossFocus.Armor)
        {
            if (direction.y >= 0.5f)
            {
                SetEquipmentCrossFocus(EquipCrossFocus.Armor, Mathf.Max(0, GetCurrentCrossIndex(EquipCrossFocus.Armor) - 1));
                return true;
            }

            if (direction.y <= -0.5f)
            {
                SetEquipmentCrossFocus(EquipCrossFocus.Armor, Mathf.Min(armorEquipSlots.Length - 1, GetCurrentCrossIndex(EquipCrossFocus.Armor) + 1));
                return true;
            }
        }

        return false;
    }

    private InventorySlot GetCurrentCrossSlot()
    {
        int idx = GetCurrentCrossIndex(equipCrossFocus);
        switch (equipCrossFocus)
        {
            case EquipCrossFocus.Right: return idx >= 0 && idx < rightEquipSlots.Length ? rightEquipSlots[idx] : null;
            case EquipCrossFocus.Left: return idx >= 0 && idx < leftEquipSlots.Length ? leftEquipSlots[idx] : null;
            case EquipCrossFocus.Bottom: return idx >= 0 && idx < bottomEquipSlots.Length ? bottomEquipSlots[idx] : null;
            case EquipCrossFocus.Top: return idx >= 0 && idx < topEquipSlots.Length ? topEquipSlots[idx] : null;
            case EquipCrossFocus.Armor: return idx >= 0 && idx < armorEquipSlots.Length ? armorEquipSlots[idx] : null;
            default: return null;
        }
    }

    private IEnumerable<CrossSlotRef> EnumerateCrossSlots()
    {
        for (int i = 0; i < rightEquipSlots.Length; i++)
            yield return new CrossSlotRef(EquipCrossFocus.Right, i, rightEquipSlots[i]);
        for (int i = 0; i < leftEquipSlots.Length; i++)
            yield return new CrossSlotRef(EquipCrossFocus.Left, i, leftEquipSlots[i]);
        for (int i = 0; i < bottomEquipSlots.Length; i++)
            yield return new CrossSlotRef(EquipCrossFocus.Bottom, i, bottomEquipSlots[i]);
        for (int i = 0; i < topEquipSlots.Length; i++)
            yield return new CrossSlotRef(EquipCrossFocus.Top, i, topEquipSlots[i]);
        for (int i = 0; i < armorEquipSlots.Length; i++)
            yield return new CrossSlotRef(EquipCrossFocus.Armor, i, armorEquipSlots[i]);
    }

    private Vector2 GetSlotCenter(InventorySlot slot)
    {
        if (slot == null) return Vector2.zero;
        var rt = slot.GetComponent<RectTransform>();
        if (rt == null) return slot.transform.position;

        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        Vector3 center = (corners[0] + corners[2]) * 0.5f;
        return new Vector2(center.x, center.y);
    }

    private int FindClosestSlotIndexByVerticalDistance(InventorySlot referenceSlot, InventorySlot[] candidates)
    {
        if (referenceSlot == null || candidates == null || candidates.Length == 0)
            return -1;

        float referenceY = GetSlotCenter(referenceSlot).y;
        int bestIndex = -1;
        float bestDelta = float.PositiveInfinity;

        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] == null)
                continue;

            float delta = Mathf.Abs(GetSlotCenter(candidates[i]).y - referenceY);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private void SetEquipmentCrossFocus(EquipCrossFocus focus, int slotIndex)
    {
        BuildEquipSlotsIfNeeded();
        EnsurePlayerInventory();
        equipCrossFocus = focus;

        if (playerInventory != null)
        {
            switch (focus)
            {
                case EquipCrossFocus.Right:
                    playerInventory.currentRightIndex = Mathf.Clamp(slotIndex, 0, 2);
                    break;
                case EquipCrossFocus.Left:
                    playerInventory.currentLeftIndex = Mathf.Clamp(slotIndex, 0, 2);
                    break;
                case EquipCrossFocus.Bottom:
                    playerInventory.currentUsableIndex = Mathf.Clamp(slotIndex, 0, 2);
                    break;
                case EquipCrossFocus.Top:
                    playerInventory.currentMagicIndex = Mathf.Clamp(slotIndex, 0, 2);
                    currentTopIndex = Mathf.Clamp(slotIndex, 0, 2);
                    break;
                case EquipCrossFocus.Armor:
                    currentArmorIndex = Mathf.Clamp(slotIndex, 0, armorEquipSlots.Length - 1);
                    break;
            }
        }
        else if (focus == EquipCrossFocus.Top)
        {
            currentTopIndex = Mathf.Clamp(slotIndex, 0, 2);
        }
        else if (focus == EquipCrossFocus.Armor)
        {
            currentArmorIndex = Mathf.Clamp(slotIndex, 0, armorEquipSlots.Length - 1);
        }

        ApplyEquipmentCrossFocusVisual();
    }

    private void ApplyEquipmentCrossFocusVisual()
    {
        int rightIndex = GetCurrentCrossIndex(EquipCrossFocus.Right);
        int leftIndex = GetCurrentCrossIndex(EquipCrossFocus.Left);
        int bottomIndex = GetCurrentCrossIndex(EquipCrossFocus.Bottom);
        int topIndex = GetCurrentCrossIndex(EquipCrossFocus.Top);
        int armorIndex = GetCurrentCrossIndex(EquipCrossFocus.Armor);

        for (int i = 0; i < rightEquipSlots.Length; i++)
            if (rightEquipSlots[i] != null) rightEquipSlots[i].SetFocused(showPadFocus && equipCrossFocus == EquipCrossFocus.Right && i == rightIndex);
        for (int i = 0; i < leftEquipSlots.Length; i++)
            if (leftEquipSlots[i] != null) leftEquipSlots[i].SetFocused(showPadFocus && equipCrossFocus == EquipCrossFocus.Left && i == leftIndex);
        for (int i = 0; i < bottomEquipSlots.Length; i++)
            if (bottomEquipSlots[i] != null) bottomEquipSlots[i].SetFocused(showPadFocus && equipCrossFocus == EquipCrossFocus.Bottom && i == bottomIndex);
        for (int i = 0; i < topEquipSlots.Length; i++)
            if (topEquipSlots[i] != null) topEquipSlots[i].SetFocused(showPadFocus && equipCrossFocus == EquipCrossFocus.Top && i == topIndex);
        for (int i = 0; i < armorEquipSlots.Length; i++)
            if (armorEquipSlots[i] != null) armorEquipSlots[i].SetFocused(showPadFocus && equipCrossFocus == EquipCrossFocus.Armor && i == armorIndex);
    }

    private void EnsurePlayerInventory()
    {
    }

    private static Transform FindNamedChild(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != null && child.name == childName)
                return child;
        }

        return null;
    }

    private Transform ResolveArmorContainer(Transform current, string childName)
    {
        if (current != null)
            return current;

        Transform found = FindNamedChild(transform, childName);
        if (found != null)
            return found;

        if (equipmentBackground != null)
        {
            found = FindNamedChild(equipmentBackground.transform, childName);
            if (found != null)
                return found;
        }

        if (inventoryBackground != null)
        {
            found = FindNamedChild(inventoryBackground.transform, childName);
            if (found != null)
                return found;
        }

        return null;
    }

    private void LogMissingArmorContainer(Transform container, string slotName)
    {
        if (container != null)
            return;

        Debug.LogWarning($"[EquipmentManager] Armor container '{slotName}' non trovato. Assegna il riferimento in Inspector oppure usa esattamente il nome '{slotName}' nella UI.");
    }

    public void HandleSlotPointerDown(int index)
    {
        BeginEquipFromEncodedSlot(index);
    }

    public void HandleSlotBeginDrag(int index, PointerEventData eventData) { }
    public void HandleSlotDrag(PointerEventData eventData) { }
    public void HandleSlotEndDrag() { }
    public void HandleSlotDrop(int targetIndex) { }
    public void HandleSlotSelected(int index) { }

    public void HandleSlotSubmit(int index)
    {
        BeginEquipFromEncodedSlot(index);
    }

    private void BeginEquipFromEncodedSlot(int encodedSlot)
    {
        if (TryDecodeLoadoutSlot(encodedSlot, RightSlotBase, out int rightIndex))
        {
            SetEquipmentCrossFocus(EquipCrossFocus.Right, rightIndex);
            BeginEquipRight(rightIndex);
            return;
        }

        if (TryDecodeLoadoutSlot(encodedSlot, LeftSlotBase, out int leftIndex))
        {
            SetEquipmentCrossFocus(EquipCrossFocus.Left, leftIndex);
            BeginEquipLeft(leftIndex);
            return;
        }

        if (TryDecodeLoadoutSlot(encodedSlot, BottomSlotBase, out int bottomIndex))
        {
            SetEquipmentCrossFocus(EquipCrossFocus.Bottom, bottomIndex);
            BeginEquipBottom(bottomIndex);
            return;
        }

        if (TryDecodeLoadoutSlot(encodedSlot, TopSlotBase, out int topIndex))
        {
            SetEquipmentCrossFocus(EquipCrossFocus.Top, topIndex);
            BeginEquipTop(topIndex);
            return;
        }

        int armorIndex = encodedSlot - ArmorSlotBase;
        if (armorIndex < 0 || armorIndex >= armorEquipSlots.Length)
            return;

        SetEquipmentCrossFocus(EquipCrossFocus.Armor, armorIndex);
        BeginEquipArmor((ArmorItemData.ArmorSlot)armorIndex);
    }

    private static bool TryDecodeLoadoutSlot(int encodedSlot, int slotBase, out int localIndex)
    {
        localIndex = encodedSlot - slotBase;
        return localIndex >= 0 && localIndex < 3;
    }

    private readonly struct CrossSlotRef
    {
        public readonly EquipCrossFocus focus;
        public readonly int index;
        public readonly InventorySlot slot;

        public CrossSlotRef(EquipCrossFocus focus, int index, InventorySlot slot)
        {
            this.focus = focus;
            this.index = index;
            this.slot = slot;
        }
    }
}
