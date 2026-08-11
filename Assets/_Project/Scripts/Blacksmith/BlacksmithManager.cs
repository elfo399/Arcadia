using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public sealed class BlacksmithManager : MonoBehaviour, IInventorySlotHandler
{
    private enum FocusArea { Grid, Action }

    [Header("Blacksmith UI")]
    [SerializeField] private GameObject blacksmithHud;
    [SerializeField] private GameObject initialFocus;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private Animator bookAnimator;
    [SerializeField] private Animator contentAppearAnimator;
    [SerializeField] private CanvasGroup contentGroup;
    [SerializeField] private InventorySlot slotPrefab;
    [SerializeField] private Transform slotParent;
    [SerializeField] private GridLayoutGroup slotGrid;
    [SerializeField, Min(0)] private int initialSlotCount = 30;

    [Header("Upgrade Detail")]
    [SerializeField] private GameObject detailRoot;
    [SerializeField] private Image detailImage;
    [SerializeField] private TextMeshProUGUI detailTitle;
    [SerializeField] private GameObject weaponSection;
    [SerializeField] private GameObject shieldSection;
    [SerializeField] private TextMeshProUGUI weaponDamageText;
    [SerializeField] private TextMeshProUGUI weaponCriticalText;
    [SerializeField] private TextMeshProUGUI weaponWeightText;
    [SerializeField] private TextMeshProUGUI weaponScalingText;
    [SerializeField] private TextMeshProUGUI weaponRequirementsText;
    [SerializeField] private TextMeshProUGUI shieldDamageText;
    [SerializeField] private TextMeshProUGUI shieldCriticalText;
    [SerializeField] private TextMeshProUGUI shieldWeightText;
    [SerializeField] private TextMeshProUGUI shieldScalingText;
    [SerializeField] private TextMeshProUGUI shieldRequirementsText;
    [SerializeField] private TextMeshProUGUI shieldPhysicalDefenseText;
    [SerializeField] private TextMeshProUGUI shieldMagicDefenseText;

    [Header("Upgrade Requirements")]
    [SerializeField] private Transform materialsRoot;
    [SerializeField] private QuestRewardItemUI materialRowPrefab;
    [SerializeField] private GameObject priceRoot;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private SegmentedButtonSelectionUI upgradeButtonSelection;

    [Header("Initial State")]
    [SerializeField] private string contentAppearStateName = "Transition";
    [SerializeField, Min(0f)] private float contentAppearDelay = 0.5833333f;
    [SerializeField, Min(0f)] private float contentAppearDuration = 1.8f;
    [SerializeField] private string bookCloseStateName = "CloseBook";
    [SerializeField, Min(0f)] private float bookCloseDuration = 0.6666666f;

    [SerializeField] private List<CraftingRecipeData> recipes = new List<CraftingRecipeData>();
    public BlacksmithMode CurrentMode { get; private set; }
    public bool IsOpen { get; private set; }
    public NpcServiceContext ActiveContext { get; private set; }
    public IReadOnlyList<CraftingRecipeData> Recipes => recipes;
    public event Action<InventoryItem> ConfirmRequested;
    public event Action Closed;

    private readonly object gameplayLockOwner = new object();
    private PlayerControls controls;
    private Action<InputAction.CallbackContext> confirmCallback;
    private Action<InputAction.CallbackContext> cancelCallback;
    private readonly List<InventorySlot> blacksmithSlots = new List<InventorySlot>();
    private readonly List<InventoryItem> upgradeItems = new List<InventoryItem>();
    private readonly List<QuestRewardItemUI> materialRows = new List<QuestRewardItemUI>();
    private int selectedUpgradeIndex = -1;
    private int blacksmithFocusIndex = -1;
    private int openingFrame = -1;
    private float lastNavigationTime = -999f;
    private const float NavigationRepeatCooldown = 0.20f;
    private Coroutine contentAppearRoutine;
    private Coroutine closeRoutine;
    private Coroutine upgradeButtonFocusRoutine;
    private int lastUpgradeActivationFrame = -1;
    private int upgradeButtonFocusEnteredFrame = -1;
    private FocusArea focusArea = FocusArea.Grid;
    private bool isClosing;
    private bool isInteractive;

    private void Awake()
    {
        confirmCallback = OnConfirmPerformed;
        cancelCallback = OnCancelPerformed;

        if (blacksmithHud != null)
            blacksmithHud.SetActive(false);
        EnsureBlacksmithSlots();
        ResolveUpgradeRequirementReferences();
        HideContentAppearAnimation();
        HideContentGroup();
        HideUpgradeSections();
    }

    private void OnDestroy()
    {
        if (upgradeButton != null)
            upgradeButton.onClick.RemoveListener(OnUpgradeButtonClicked);
    }

    public bool OpenBlacksmith(BlacksmithMode mode, NpcServiceContext context)
    {
        if (context == null || context.Player == null || context.PlayerInventory == null || context.PlayerStats == null)
            return false;
        if (isClosing)
            return false;

        CurrentMode = mode;
        ActiveContext = context;
        playerController = context.Player.GetComponent<PlayerController>() ?? playerController;
        if (playerController == null || playerController.Controls == null)
        {
            Debug.LogWarning("[BlacksmithManager] PlayerController o PlayerControls non disponibili.", this);
            ActiveContext = null;
            return false;
        }

        controls = playerController.Controls;
        playerInventory = context.PlayerInventory;
        playerStats = context.PlayerStats;
        playerStats.TryEnsurePersistentStateReady();
        IsOpen = true;
        isInteractive = false;
        isClosing = false;
        focusArea = FocusArea.Grid;
        ClearUpgradeButtonFocusVisual();
        openingFrame = Time.frameCount;
        playerController.AcquireGameplayInputLock(gameplayLockOwner);
        SubscribeInput();
        if (blacksmithHud != null)
            blacksmithHud.SetActive(true);
        RefreshBlacksmithGrid();
        Canvas.ForceUpdateCanvases();
        FocusInitialTarget();
        contentAppearRoutine = StartCoroutine(PlayContentAppearAnimation());
        return true;
    }

    public void CloseBlacksmith()
    {
        if (!IsOpen || isClosing)
            return;

        IsOpen = false;
        ActiveContext = null;
        isClosing = true;
        isInteractive = false;
        focusArea = FocusArea.Grid;
        ClearUpgradeButtonFocusVisual();
        UnsubscribeInput();
        StopContentAppearRoutineOnly();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        if (!isActiveAndEnabled)
        {
            isClosing = false;
            if (blacksmithHud != null)
                blacksmithHud.SetActive(false);
            ReleaseGameplayInputLock();
            return;
        }

        closeRoutine = StartCoroutine(RunCloseAnimations());
    }

    private void OnDisable()
    {
        UnsubscribeInput();
        StopContentAppearRoutineOnly();
        if (upgradeButtonFocusRoutine != null)
            StopCoroutine(upgradeButtonFocusRoutine);
        upgradeButtonFocusRoutine = null;
        focusArea = FocusArea.Grid;
        ClearUpgradeButtonFocusVisual();
        if (closeRoutine != null)
            StopCoroutine(closeRoutine);
        closeRoutine = null;
        isInteractive = false;
        isClosing = false;
        if (blacksmithHud != null)
            blacksmithHud.SetActive(false);
        ReleaseGameplayInputLock();
    }

    private void Update()
    {
        if (!IsOpen || !isInteractive || focusArea != FocusArea.Grid || controls == null
            || Time.unscaledTime < lastNavigationTime + NavigationRepeatCooldown)
            return;

        Vector2 navigation = controls.Player.Move.ReadValue<Vector2>();
        if (navigation.x > 0.5f)
            MoveBlacksmithFocusHorizontal(1);
        else if (navigation.x < -0.5f)
            MoveBlacksmithFocusHorizontal(-1);
        else if (navigation.y > 0.5f)
            MoveBlacksmithFocusVertical(-1);
        else if (navigation.y < -0.5f)
            MoveBlacksmithFocusVertical(1);
        else
            return;

        lastNavigationTime = Time.unscaledTime;
    }

    private void EnsureBlacksmithSlots()
    {
        if (slotPrefab == null || slotParent == null || initialSlotCount <= 0)
            return;

        blacksmithSlots.Clear();
        for (int i = 0; i < initialSlotCount; i++)
        {
            InventorySlot slot = Instantiate(slotPrefab, slotParent);
            slot.name = $"BlacksmithSlot_{i:00}";
            slot.SetDisplayOnly(false);
            slot.Init(i, this);
            slot.Clear();
            slot.SetFocused(false);
            blacksmithSlots.Add(slot);
        }
    }

    private void RefreshBlacksmithGrid()
    {
        upgradeItems.Clear();
        selectedUpgradeIndex = -1;
        if (playerInventory != null)
        {
            IReadOnlyList<InventoryItem> inventoryItems = playerInventory.Items;
            for (int i = 0; i < inventoryItems.Count; i++)
            {
                InventoryItem item = inventoryItems[i];
                if (item != null && item.weaponData != null
                    && item.weaponData.category != WeaponCategory.Unarmed)
                    upgradeItems.Add(item);
            }
        }

        for (int i = 0; i < blacksmithSlots.Count; i++)
        {
            InventoryItem item = i < upgradeItems.Count ? upgradeItems[i] : null;
            if (item != null)
            {
                Sprite icon = GetItemIcon(item);
                if (icon == null)
                    Debug.LogWarning($"[BlacksmithManager] Icona mancante per '{item.weaponData.name}' instance '{item.instanceId}'.", this);
                blacksmithSlots[i].Setup(icon, item.amount, playerInventory != null && playerInventory.IsInstanceEquipped(item.instanceId));
            }
            else
                blacksmithSlots[i].Clear();
        }

        Debug.Log($"[BlacksmithManager] Refresh mode={CurrentMode}, inventory={playerInventory?.Items.Count ?? 0}, weapons={upgradeItems.Count}, slots={blacksmithSlots.Count}.", this);

        SetBlacksmithFocus(upgradeItems.Count > 0 ? 0 : -1);
    }

    public InventoryItem SelectedUpgradeItem => selectedUpgradeIndex >= 0 && selectedUpgradeIndex < upgradeItems.Count
        ? upgradeItems[selectedUpgradeIndex]
        : null;

    public void HandleSlotPointerDown(int index) { if (isInteractive) SetBlacksmithFocus(index); }
    public void HandleSlotBeginDrag(int index, PointerEventData eventData) { }
    public void HandleSlotDrag(PointerEventData eventData) { }
    public void HandleSlotEndDrag() { }
    public void HandleSlotDrop(int targetIndex) { }
    public void HandleSlotSelected(int index)
    {
        if (isInteractive && focusArea == FocusArea.Grid)
            SelectUpgradeItem(index);
    }

    public void HandleSlotSubmit(int index)
    {
        if (!isInteractive || index < 0 || index >= upgradeItems.Count)
            return;

        if (selectedUpgradeIndex != index)
            SetBlacksmithFocus(index);
        TryFocusUpgradeButton();
    }

    private void SelectUpgradeItem(int index)
    {
        if (index < 0 || index >= upgradeItems.Count)
            return;
        SetBlacksmithFocus(index);
    }

    private void SetBlacksmithFocus(int index)
    {
        focusArea = FocusArea.Grid;
        ClearUpgradeButtonFocusVisual();
        selectedUpgradeIndex = index >= 0 && index < upgradeItems.Count ? index : -1;
        blacksmithFocusIndex = selectedUpgradeIndex;
        for (int i = 0; i < blacksmithSlots.Count; i++)
            blacksmithSlots[i].SetFocused(i == selectedUpgradeIndex);

        RefreshUpgradeDetails();

        if (EventSystem.current != null && selectedUpgradeIndex >= 0
            && selectedUpgradeIndex < blacksmithSlots.Count)
        {
            GameObject target = blacksmithSlots[selectedUpgradeIndex].gameObject;
            if (EventSystem.current.currentSelectedGameObject != target)
                EventSystem.current.SetSelectedGameObject(target);
        }
    }

    private void MoveBlacksmithFocusHorizontal(int direction)
    {
        if (upgradeItems.Count == 0)
            return;

        int next = blacksmithFocusIndex + (direction >= 0 ? 1 : -1);
        if (next >= upgradeItems.Count) next = 0;
        if (next < 0) next = upgradeItems.Count - 1;
        SetBlacksmithFocus(next);
    }

    private void MoveBlacksmithFocusVertical(int direction)
    {
        if (upgradeItems.Count == 0)
            return;

        int columns = 5;
        if (slotGrid != null && slotGrid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            columns = Mathf.Max(1, slotGrid.constraintCount);

        int next = Mathf.Clamp(blacksmithFocusIndex + direction * columns, 0, upgradeItems.Count - 1);
        SetBlacksmithFocus(next);
    }

    private void RefreshUpgradeDetails()
    {
        if (detailRoot != null)
            detailRoot.SetActive(true);

        HideUpgradeSections();

        InventoryItem item = SelectedUpgradeItem;
        if (item == null)
            return;

        Sprite icon = GetItemIcon(item);
        if (detailImage != null)
        {
            detailImage.sprite = icon;
            detailImage.enabled = icon != null;
            detailImage.preserveAspect = true;
        }

        if (detailTitle != null)
            detailTitle.text = item.weaponData != null
                ? WeaponUpgradeCalculator.GetDisplayName(item)
                : item.title ?? string.Empty;

        if (item.weaponData != null && item.weaponData.category == WeaponCategory.Shield)
        {
            if (shieldSection != null)
                shieldSection.SetActive(true);

            WeaponItem shield = item.weaponData;
            EffectiveWeaponStats effective = WeaponUpgradeCalculator.GetStats(item);
            SetDetailText(shieldDamageText, effective.PhysicalDamage.ToString());
            SetDetailText(shieldCriticalText, effective.CriticalHit.ToString("0.##"));
            SetDetailText(shieldWeightText, shield.weight.ToString("0.##"));
            SetDetailText(shieldScalingText, GetScalingLabel(effective));
            SetDetailText(shieldRequirementsText, shield.GetRequirementsLabel());
            SetDetailText(shieldPhysicalDefenseText,
                Mathf.RoundToInt(effective.PhysicalBlockPercent * 100f).ToString());
            SetDetailText(shieldMagicDefenseText,
                Mathf.RoundToInt(effective.MagicBlockPercent * 100f).ToString());
        }
        else if (item.weaponData != null)
        {
            if (weaponSection != null)
                weaponSection.SetActive(true);

            WeaponItem weapon = item.weaponData;
            EffectiveWeaponStats effective = WeaponUpgradeCalculator.GetStats(item);
            SetDetailText(weaponDamageText, effective.PhysicalDamage.ToString());
            SetDetailText(weaponCriticalText, effective.CriticalHit.ToString("0.##"));
            SetDetailText(weaponWeightText, weapon.weight.ToString("0.##"));
            SetDetailText(weaponScalingText, GetScalingLabel(effective));
            SetDetailText(weaponRequirementsText, weapon.GetRequirementsLabel());
        }

        RefreshUpgradeRequirements(item);
    }

    private void HideUpgradeSections()
    {
        if (weaponSection != null) weaponSection.SetActive(false);
        if (shieldSection != null) shieldSection.SetActive(false);
        if (detailImage != null)
        {
            detailImage.sprite = null;
            detailImage.enabled = false;
        }
        if (detailTitle != null)
            detailTitle.text = string.Empty;

        ClearDetailTextFields();
        HideUpgradeRequirements();
    }

    private void ResolveUpgradeRequirementReferences()
    {
        if (upgradeButtonSelection == null && upgradeButton != null)
            upgradeButtonSelection = upgradeButton.GetComponent<SegmentedButtonSelectionUI>();

        if (materialsRoot != null)
        {
            QuestRewardItemUI[] authoredRows = materialsRoot.GetComponentsInChildren<QuestRewardItemUI>(true);
            for (int i = 0; i < authoredRows.Length; i++)
            {
                authoredRows[i].gameObject.SetActive(false);
                Destroy(authoredRows[i].gameObject);
            }
        }

        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveListener(OnUpgradeButtonClicked);
            upgradeButton.onClick.AddListener(OnUpgradeButtonClicked);
        }
    }

    private void RefreshUpgradeRequirements(InventoryItem item)
    {
        BlacksmithUpgradeCheck check = item != null ? CanUpgrade(item) : new BlacksmithUpgradeCheck();
        List<BlacksmithRequirementStatus> requirements = check.Materials ?? new List<BlacksmithRequirementStatus>();

        if (priceRoot != null)
            priceRoot.SetActive(check.CoinCost > 0);
        if (priceText != null)
            priceText.text = check.CoinCost > 0 ? check.CoinCost.ToString("N0") : string.Empty;

        ClearGeneratedMaterialRows();
        for (int i = 0; i < requirements.Count; i++)
        {
            if (materialsRoot == null || materialRowPrefab == null)
                break;

            QuestRewardItemUI row = Instantiate(materialRowPrefab, materialsRoot, false);
            row.name = $"Material_{i:00}";
            row.gameObject.SetActive(true);
            materialRows.Add(row);
        }

        if (materialsRoot != null)
            materialsRoot.gameObject.SetActive(requirements.Count > 0 && materialRows.Count == requirements.Count);

        for (int i = 0; i < materialRows.Count; i++)
        {
            bool visible = i < requirements.Count;
            materialRows[i].gameObject.SetActive(visible);
            if (!visible)
                continue;

            BlacksmithRequirementStatus requirement = requirements[i];
            materialRows[i].SetRequirementData(
                requirement.item != null ? requirement.item.icon : null,
                requirement.item != null ? requirement.item.itemName : string.Empty,
                requirement.owned,
                requirement.required);
        }

        if (upgradeButton != null)
        {
            upgradeButton.gameObject.SetActive(item != null);
            upgradeButton.interactable = item != null && check.IsValid;
        }
    }

    private void ClearGeneratedMaterialRows()
    {
        for (int i = 0; i < materialRows.Count; i++)
        {
            if (materialRows[i] != null)
                Destroy(materialRows[i].gameObject);
        }

        materialRows.Clear();
    }

    private void HideUpgradeRequirements()
    {
        if (materialsRoot != null)
            materialsRoot.gameObject.SetActive(false);
        if (priceRoot != null)
            priceRoot.SetActive(false);
        if (priceText != null)
            priceText.text = string.Empty;
        if (upgradeButton != null)
        {
            upgradeButton.gameObject.SetActive(false);
            upgradeButton.interactable = false;
        }

        ClearGeneratedMaterialRows();
    }

    private void OnUpgradeButtonClicked()
    {
        InventoryItem item = SelectedUpgradeItem;
        if (item == null || lastUpgradeActivationFrame == Time.frameCount
            || upgradeButtonFocusEnteredFrame == Time.frameCount)
            return;

        lastUpgradeActivationFrame = Time.frameCount;
        focusArea = FocusArea.Grid;
        ClearUpgradeButtonFocusVisual();

        if (TryUpgrade(item, out _))
            RefreshBlacksmithGrid();
        else
            RefreshUpgradeDetails();
    }

    private void ClearDetailTextFields()
    {
        TextMeshProUGUI[] fields =
        {
            weaponDamageText, weaponCriticalText, weaponWeightText, weaponScalingText,
            weaponRequirementsText, shieldDamageText, shieldCriticalText, shieldWeightText,
            shieldScalingText, shieldRequirementsText, shieldPhysicalDefenseText,
            shieldMagicDefenseText
        };
        for (int i = 0; i < fields.Length; i++)
            SetDetailText(fields[i], string.Empty);
    }

    private static void SetDetailText(TextMeshProUGUI target, string value)
    {
        if (target != null)
            target.text = value ?? string.Empty;
    }

    private static string GetScalingLabel(EffectiveWeaponStats stats)
    {
        var parts = new List<string>();
        if (stats.StrengthScalingRank != WeaponItem.ScalingRank.None)
            parts.Add("STR " + stats.StrengthScalingRank);
        if (stats.DexterityScalingRank != WeaponItem.ScalingRank.None)
            parts.Add("DEX " + stats.DexterityScalingRank);
        if (stats.IntelligenceScalingRank != WeaponItem.ScalingRank.None)
            parts.Add("INT " + stats.IntelligenceScalingRank);
        if (stats.FaithScalingRank != WeaponItem.ScalingRank.None)
            parts.Add("FAI " + stats.FaithScalingRank);
        return string.Join(" / ", parts);
    }

    private void SubscribeInput()
    {
        if (controls == null)
            return;

        controls.Player.Jump.performed -= confirmCallback;
        controls.Player.Jump.performed += confirmCallback;
        controls.Player.SprintOrDodge.performed -= cancelCallback;
        controls.Player.SprintOrDodge.performed += cancelCallback;
    }

    private void UnsubscribeInput()
    {
        if (controls == null)
            return;

        controls.Player.Jump.performed -= confirmCallback;
        controls.Player.SprintOrDodge.performed -= cancelCallback;
    }

    private void OnConfirmPerformed(InputAction.CallbackContext _)
    {
        if (!IsOpen || !isInteractive || openingFrame == Time.frameCount)
            return;
        if (lastUpgradeActivationFrame == Time.frameCount)
            return;

        if (IsUpgradeButtonFocused())
        {
            if (upgradeButtonFocusEnteredFrame == Time.frameCount)
                return;
            OnUpgradeButtonClicked();
            return;
        }

        InventoryItem selected = SelectedUpgradeItem;
        if (selected == null)
            return;

        if (TryFocusUpgradeButton())
            return;

        ConfirmRequested?.Invoke(selected);
    }

    private bool TryFocusUpgradeButton()
    {
        if (upgradeButton == null || !upgradeButton.gameObject.activeInHierarchy || !upgradeButton.interactable)
            return false;
        if (EventSystem.current == null)
            return false;

        if (focusArea != FocusArea.Action)
            upgradeButtonFocusEnteredFrame = Time.frameCount;
        focusArea = FocusArea.Action;
        upgradeButtonSelection?.SetFocused(true);

        if (upgradeButtonFocusRoutine != null)
            StopCoroutine(upgradeButtonFocusRoutine);
        upgradeButtonFocusRoutine = StartCoroutine(KeepUpgradeButtonFocusedAfterSubmit());
        return true;
    }

    private System.Collections.IEnumerator KeepUpgradeButtonFocusedAfterSubmit()
    {
        yield return null;
        upgradeButtonFocusRoutine = null;
        if (IsOpen && isInteractive && focusArea == FocusArea.Action && upgradeButton != null
            && upgradeButton.gameObject.activeInHierarchy && upgradeButton.interactable
            && EventSystem.current != null)
        {
            upgradeButton.Select();
            if (EventSystem.current.currentSelectedGameObject != upgradeButton.gameObject)
                EventSystem.current.SetSelectedGameObject(upgradeButton.gameObject);
        }
    }

    private bool IsUpgradeButtonFocused()
    {
        return focusArea == FocusArea.Action;
    }

    private void ClearUpgradeButtonFocusVisual()
    {
        upgradeButtonSelection?.SetFocused(false);
    }

    private void ReturnFocusToGrid()
    {
        focusArea = FocusArea.Grid;
        ClearUpgradeButtonFocusVisual();
        if (EventSystem.current == null || selectedUpgradeIndex < 0
            || selectedUpgradeIndex >= blacksmithSlots.Count)
            return;

        GameObject target = blacksmithSlots[selectedUpgradeIndex].gameObject;
        if (EventSystem.current.currentSelectedGameObject != target)
            EventSystem.current.SetSelectedGameObject(target);
    }

    private void OnCancelPerformed(InputAction.CallbackContext _)
    {
        if (!IsOpen || !isInteractive || openingFrame == Time.frameCount)
            return;

        if (focusArea == FocusArea.Action)
        {
            ReturnFocusToGrid();
            return;
        }

        CloseBlacksmith();
    }

    private void FocusInitialTarget()
    {
        if (upgradeItems.Count > 0)
        {
            SetBlacksmithFocus(0);
            return;
        }

        if (EventSystem.current != null && initialFocus != null)
            EventSystem.current.SetSelectedGameObject(initialFocus);
    }

    private static Sprite GetItemIcon(InventoryItem item)
    {
        if (item == null) return null;
        return item.icon ?? item.weaponData?.icon ?? item.armorData?.icon
            ?? item.usableData?.icon ?? item.itemData?.icon ?? item.magicData?.icon;
    }

    private System.Collections.IEnumerator PlayContentAppearAnimation()
    {
        if (contentAppearDelay > 0f)
            yield return new WaitForSecondsRealtime(contentAppearDelay);
        if (!IsOpen || isClosing)
            yield break;

        ShowContentGroup();
        if (contentAppearAnimator != null && !string.IsNullOrWhiteSpace(contentAppearStateName))
        {
            GameObject animationObject = contentAppearAnimator.gameObject;
            animationObject.SetActive(true);
            contentAppearAnimator.enabled = true;
            contentAppearAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            contentAppearAnimator.Play(contentAppearStateName, 0, 0f);
            contentAppearAnimator.Update(0f);

            if (contentAppearDuration > 0f)
                yield return new WaitForSecondsRealtime(contentAppearDuration);

            HideContentAppearAnimation();
        }

        contentAppearRoutine = null;
        if (IsOpen && !isClosing)
        {
            isInteractive = true;
            SetContentInteraction(true);
            lastNavigationTime = Time.unscaledTime;
            FocusInitialTarget();
        }
    }

    private System.Collections.IEnumerator RunCloseAnimations()
    {
        if (contentAppearAnimator != null && contentAppearDuration > 0f)
        {
            contentAppearAnimator.gameObject.SetActive(true);
            contentAppearAnimator.enabled = true;
            contentAppearAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;

            float elapsed = 0f;
            while (elapsed < contentAppearDuration)
            {
                float normalizedTime = 1f - Mathf.Clamp01(elapsed / contentAppearDuration);
                contentAppearAnimator.Play(contentAppearStateName, 0, normalizedTime);
                contentAppearAnimator.Update(0f);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            contentAppearAnimator.Play(contentAppearStateName, 0, 0f);
            contentAppearAnimator.Update(0f);
            HideContentAppearAnimation();
        }

        HideContentGroup();
        if (bookAnimator != null && bookCloseDuration > 0f)
        {
            bookAnimator.enabled = true;
            bookAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            bookAnimator.Play(bookCloseStateName, 0, 0f);
            bookAnimator.Update(0f);
            yield return new WaitForSecondsRealtime(bookCloseDuration);
        }

        FinishClose();
    }

    private void FinishClose()
    {
        if (blacksmithHud != null)
            blacksmithHud.SetActive(false);
        isClosing = false;
        isInteractive = false;
        closeRoutine = null;
        Closed?.Invoke();
        ReleaseGameplayInputLock();
        controls = null;
        openingFrame = -1;
    }

    private void ReleaseGameplayInputLock()
    {
        if (playerController != null)
            playerController.ReleaseGameplayInputLock(gameplayLockOwner);
        controls = null;
        openingFrame = -1;
    }

    private void StopContentAppearRoutineOnly()
    {
        if (contentAppearRoutine != null)
            StopCoroutine(contentAppearRoutine);
        contentAppearRoutine = null;
    }

    private void HideContentAppearAnimation()
    {
        if (contentAppearAnimator != null)
        {
            contentAppearAnimator.enabled = false;
            contentAppearAnimator.gameObject.SetActive(false);
        }
    }

    private void HideContentGroup()
    {
        if (contentGroup == null)
            return;
        contentGroup.alpha = 0f;
        contentGroup.interactable = false;
        contentGroup.blocksRaycasts = false;
    }

    private void ShowContentGroup()
    {
        if (contentGroup == null)
            return;
        contentGroup.alpha = 1f;
        SetContentInteraction(false);
    }

    private void SetContentInteraction(bool enabled)
    {
        if (contentGroup == null)
            return;
        contentGroup.interactable = enabled;
        contentGroup.blocksRaycasts = enabled;
    }

    public BlacksmithUpgradeCheck CanUpgrade(InventoryItem item)
    {
        BlacksmithUpgradeCheck check = new BlacksmithUpgradeCheck();
        PlayerInventory inventory = ActiveContext != null ? ActiveContext.PlayerInventory : null;
        PlayerStats stats = ActiveContext != null ? ActiveContext.PlayerStats : null;
        if (item == null || item.weaponData == null)
        {
            check.FailureReason = "InventoryItem weaponData mancante.";
            return check;
        }

        WeaponItem weapon = item.weaponData;
        check.CurrentLevel = WeaponUpgradeRules.ClampLevel(weapon, item.upgradeLevel);
        check.TargetLevel = check.CurrentLevel + 1;
        check.MaxLevel = WeaponUpgradeRules.GetMaxLevel(weapon.rarity);
        check.IsMaxLevel = check.CurrentLevel >= check.MaxLevel;
        check.CoinCost = WeaponUpgradeCalculator.GetUpgradeCoinCost(weapon, check.TargetLevel);

        if (inventory == null || stats == null)
            check.FailureReason = "Dipendenze player mancanti.";
        else if (!weapon.canUpgrade || weapon.category == WeaponCategory.Unarmed)
            check.FailureReason = "Arma non upgradeabile.";
        else if (check.IsMaxLevel)
            check.FailureReason = "Livello massimo raggiunto.";
        else if (!stats.HasCoins(check.CoinCost))
            check.FailureReason = "Monete insufficienti.";

        List<UpgradeMaterialRequirement> requirements = WeaponUpgradeCalculator.GetUpgradeMaterialRequirements(weapon, check.TargetLevel);
        for (int i = 0; i < requirements.Count; i++)
        {
            UpgradeMaterialRequirement requirement = requirements[i];
            int owned = inventory != null ? inventory.GetTotalItemAmount(requirement.item) : 0;
            check.Materials.Add(new BlacksmithRequirementStatus
            {
                item = requirement.item,
                required = requirement.amount,
                owned = owned,
                missing = Mathf.Max(0, requirement.amount - owned),
                met = owned >= requirement.amount
            });
        }

        for (int i = 0; i < check.Materials.Count; i++)
        {
            if (!check.Materials[i].met)
            {
                check.FailureReason = "Materiali insufficienti.";
                break;
            }
        }

        check.IsValid = string.IsNullOrEmpty(check.FailureReason);
        return check;
    }

    public bool TryUpgrade(InventoryItem item, out BlacksmithUpgradeCheck result)
    {
        result = CanUpgrade(item);
        if (!result.IsValid) return false;

        PlayerInventory inventory = ActiveContext.PlayerInventory;
        PlayerStats stats = ActiveContext.PlayerStats;
        if (result.CoinCost > 0 && !stats.TryRemoveCoins(result.CoinCost, false))
            return false;

        var removed = new List<BlacksmithRequirementStatus>();
        for (int i = 0; i < result.Materials.Count; i++)
        {
            BlacksmithRequirementStatus requirement = result.Materials[i];
            if (inventory.TryRemoveItem(requirement.item, requirement.required, out _, false))
            {
                removed.Add(requirement);
                continue;
            }

            if (result.CoinCost > 0) stats.AddCoins(result.CoinCost, false);
            for (int j = 0; j < removed.Count; j++)
                inventory.TryAddItem(removed[j].item, removed[j].required, false);
            result.IsValid = false;
            result.FailureReason = "Transazione materiali fallita.";
            return false;
        }

        item.upgradeLevel = result.TargetLevel;
        stats.SaveStatsImmediate();
        return true;
    }

    public BlacksmithCraftCheck CanCraft(CraftingRecipeData recipe)
    {
        BlacksmithCraftCheck check = new BlacksmithCraftCheck();
        PlayerInventory inventory = ActiveContext != null ? ActiveContext.PlayerInventory : null;
        PlayerStats stats = ActiveContext != null ? ActiveContext.PlayerStats : null;
        if (recipe == null || string.IsNullOrWhiteSpace(recipe.recipeId))
        {
            check.FailureReason = "Ricetta non valida.";
            return check;
        }
        if (inventory == null || stats == null)
            check.FailureReason = "Dipendenze player mancanti.";
        else if (!IsRecipeUnlocked(recipe))
            check.FailureReason = "Ricetta non sbloccata.";
        else if (recipe.resultWeapon == null || !recipe.resultWeapon.canCraft)
            check.FailureReason = "Risultato non forgiabile.";
        else if (!inventory.CanAddItem(recipe.resultWeapon, 1))
            check.FailureReason = "Inventario pieno.";
        else if (!stats.HasCoins(recipe.coinCost))
            check.FailureReason = "Monete insufficienti.";

        check.CoinCost = Mathf.Max(0, recipe.coinCost);
        if (inventory != null && recipe.materialRequirements != null)
        {
            for (int i = 0; i < recipe.materialRequirements.Count; i++)
            {
                UpgradeMaterialRequirement requirement = recipe.materialRequirements[i];
                if (requirement == null || requirement.item == null || requirement.amount <= 0) continue;
                int owned = inventory.GetTotalItemAmount(requirement.item);
                check.Materials.Add(new BlacksmithRequirementStatus
                {
                    item = requirement.item,
                    required = requirement.amount,
                    owned = owned,
                    missing = Mathf.Max(0, requirement.amount - owned),
                    met = owned >= requirement.amount
                });
                if (owned < requirement.amount)
                    check.FailureReason = "Materiali insufficienti.";
            }
        }

        check.IsValid = string.IsNullOrEmpty(check.FailureReason);
        return check;
    }

    public bool TryCraft(CraftingRecipeData recipe, out BlacksmithCraftCheck result)
    {
        result = CanCraft(recipe);
        if (!result.IsValid) return false;

        PlayerInventory inventory = ActiveContext.PlayerInventory;
        PlayerStats stats = ActiveContext.PlayerStats;
        if (result.CoinCost > 0 && !stats.TryRemoveCoins(result.CoinCost, false)) return false;

        var removed = new List<BlacksmithRequirementStatus>();
        for (int i = 0; i < result.Materials.Count; i++)
        {
            BlacksmithRequirementStatus requirement = result.Materials[i];
            if (inventory.TryRemoveItem(requirement.item, requirement.required, out _, false))
            {
                removed.Add(requirement);
                continue;
            }

            if (result.CoinCost > 0) stats.AddCoins(result.CoinCost, false);
            for (int j = 0; j < removed.Count; j++)
                inventory.TryAddItem(removed[j].item, removed[j].required, false);
            result.IsValid = false;
            result.FailureReason = "Transazione materiali fallita.";
            return false;
        }

        InventoryItem crafted = new InventoryItem(recipe.resultWeapon, 1);
        crafted.upgradeLevel = WeaponUpgradeRules.ClampLevel(recipe.resultWeapon, recipe.startingUpgradeLevel);
        if (!inventory.TryAddItemInstance(crafted, false))
        {
            if (result.CoinCost > 0) stats.AddCoins(result.CoinCost, false);
            for (int i = 0; i < removed.Count; i++)
                inventory.TryAddItem(removed[i].item, removed[i].required, false);
            result.IsValid = false;
            result.FailureReason = "Impossibile aggiungere l'arma creata.";
            return false;
        }

        stats.SaveStatsImmediate();
        return true;
    }

    public bool LearnRecipe(string recipeId)
    {
        return ActiveContext != null && ActiveContext.PlayerStats != null
            && ActiveContext.PlayerStats.LearnBlacksmithRecipe(recipeId);
    }

    public bool KnowsRecipe(string recipeId)
    {
        return ActiveContext != null && ActiveContext.PlayerStats != null
            && ActiveContext.PlayerStats.KnowsBlacksmithRecipe(recipeId);
    }

    private bool IsRecipeUnlocked(CraftingRecipeData recipe)
    {
        PlayerStats stats = ActiveContext != null ? ActiveContext.PlayerStats : null;
        if (recipe == null || stats == null) return false;
        bool blueprint = stats.KnowsBlacksmithRecipe(recipe.recipeId);
        bool story = !string.IsNullOrWhiteSpace(recipe.storyFlagId) && stats.HasStoryFlag(recipe.storyFlagId);
        switch (recipe.unlockType)
        {
            case RecipeUnlockType.Blueprint: return blueprint;
            case RecipeUnlockType.Story: return story;
            case RecipeUnlockType.BlueprintAndStory: return blueprint && story;
            default: return true;
        }
    }
}
