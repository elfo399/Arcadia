using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EquipmentManager : MonoBehaviour
{
    public enum EquipTarget { None, Right, Left, Bottom, Top }
    private enum EquipCrossFocus { Right, Left, Bottom, Top }

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
    private bool equipSlotsBuilt;
    private bool hudSlotsBuilt;
    private bool showPadFocus;
    private EquipCrossFocus equipCrossFocus = EquipCrossFocus.Right;
    private int currentTopIndex;

    public EquipTarget CurrentEquipTarget { get; private set; } = EquipTarget.None;
    public int CurrentEquipSlot { get; private set; }
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

        rightEquipSlots[0] = CreateEquipSlot(rightEquipContainer);
        rightEquipSlots[1] = CreateEquipSlot(rightEquipContainer2);
        rightEquipSlots[2] = CreateEquipSlot(rightEquipContainer3);

        leftEquipSlots[0] = CreateEquipSlot(leftEquipContainer);
        leftEquipSlots[1] = CreateEquipSlot(leftEquipContainer2);
        leftEquipSlots[2] = CreateEquipSlot(leftEquipContainer3);

        bottomEquipSlots[0] = CreateEquipSlot(bottomEquipContainer);
        bottomEquipSlots[1] = CreateEquipSlot(bottomEquipContainer2);
        bottomEquipSlots[2] = CreateEquipSlot(bottomEquipContainer3);

        topEquipSlots[0] = CreateEquipSlot(topEquipContainer);
        topEquipSlots[1] = CreateEquipSlot(topEquipContainer2);
        topEquipSlots[2] = CreateEquipSlot(topEquipContainer3);

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

    private InventorySlot CreateEquipSlot(Transform parent)
    {
        if (slotPrefab == null || parent == null) return null;

        var existing = parent.GetComponentInChildren<InventorySlot>();
        if (existing != null) return existing;

        var slot = Instantiate(slotPrefab, parent);
        slot.Init(-1, null);
        slot.SetDisplayOnly(true);
        slot.gameObject.SetActive(true);
        var img = slot.GetComponent<Image>();
        if (img != null) img.raycastTarget = false;
        return slot;
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
            default:
                return 0;
        }
    }

    private void MoveEquipmentFocus(Vector2 direction)
    {
        BuildEquipSlotsIfNeeded();

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

    private InventorySlot GetCurrentCrossSlot()
    {
        int idx = GetCurrentCrossIndex(equipCrossFocus);
        switch (equipCrossFocus)
        {
            case EquipCrossFocus.Right: return idx >= 0 && idx < rightEquipSlots.Length ? rightEquipSlots[idx] : null;
            case EquipCrossFocus.Left: return idx >= 0 && idx < leftEquipSlots.Length ? leftEquipSlots[idx] : null;
            case EquipCrossFocus.Bottom: return idx >= 0 && idx < bottomEquipSlots.Length ? bottomEquipSlots[idx] : null;
            case EquipCrossFocus.Top: return idx >= 0 && idx < topEquipSlots.Length ? topEquipSlots[idx] : null;
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
            }
        }
        else if (focus == EquipCrossFocus.Top)
        {
            currentTopIndex = Mathf.Clamp(slotIndex, 0, 2);
        }

        ApplyEquipmentCrossFocusVisual();
    }

    private void ApplyEquipmentCrossFocusVisual()
    {
        int rightIndex = GetCurrentCrossIndex(EquipCrossFocus.Right);
        int leftIndex = GetCurrentCrossIndex(EquipCrossFocus.Left);
        int bottomIndex = GetCurrentCrossIndex(EquipCrossFocus.Bottom);
        int topIndex = GetCurrentCrossIndex(EquipCrossFocus.Top);

        for (int i = 0; i < rightEquipSlots.Length; i++)
            if (rightEquipSlots[i] != null) rightEquipSlots[i].SetFocused(showPadFocus && equipCrossFocus == EquipCrossFocus.Right && i == rightIndex);
        for (int i = 0; i < leftEquipSlots.Length; i++)
            if (leftEquipSlots[i] != null) leftEquipSlots[i].SetFocused(showPadFocus && equipCrossFocus == EquipCrossFocus.Left && i == leftIndex);
        for (int i = 0; i < bottomEquipSlots.Length; i++)
            if (bottomEquipSlots[i] != null) bottomEquipSlots[i].SetFocused(showPadFocus && equipCrossFocus == EquipCrossFocus.Bottom && i == bottomIndex);
        for (int i = 0; i < topEquipSlots.Length; i++)
            if (topEquipSlots[i] != null) topEquipSlots[i].SetFocused(showPadFocus && equipCrossFocus == EquipCrossFocus.Top && i == topIndex);
    }

    private void EnsurePlayerInventory()
    {
        if (playerInventory == null)
            playerInventory = FindObjectOfType<PlayerInventory>();
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
