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
    [SerializeField] private GameObject upgradeModeRoot;
    [SerializeField] private GameObject craftModeRoot;
    [SerializeField] private CanvasGroup craftContentGroup;
    [SerializeField] private Transform slotParent;
    [SerializeField] private InventorySlot upgradeItemSlotPrefab;
    [SerializeField, Min(1)] private int upgradeGridSlotCount = 30;
    [SerializeField] private ScrollableVerticalListUI upgradeListScroll;

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
    [SerializeField] private TextMeshProUGUI weaponLevelText;
    [SerializeField] private TextMeshProUGUI shieldDamageText;
    [SerializeField] private TextMeshProUGUI shieldCriticalText;
    [SerializeField] private TextMeshProUGUI shieldWeightText;
    [SerializeField] private TextMeshProUGUI shieldScalingText;
    [SerializeField] private TextMeshProUGUI shieldLevelText;
    [SerializeField] private TextMeshProUGUI shieldPhysicalDefenseText;
    [SerializeField] private TextMeshProUGUI shieldMagicDefenseText;
    [SerializeField] private Color upgradePreviewColor = new Color(0.3647059f, 0.73333335f, 0.3882353f, 1f);

    [Header("Upgrade Requirements")]
    [SerializeField] private Transform materialsRoot;
    [SerializeField] private QuestRewardItemUI materialRowPrefab;
    [SerializeField] private GameObject priceRoot;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private SegmentedButtonSelectionUI upgradeButtonSelection;

    [Header("Craft List")]
    [SerializeField] private Transform craftListRoot;
    [SerializeField] private DialogueChoiceUI craftRecipeRowPrefab;
    [SerializeField] private ScrollableVerticalListUI craftListScroll;

    [Header("Craft Detail")]
    [SerializeField] private GameObject craftDetailRoot;
    [SerializeField] private Image craftDetailImage;
    [SerializeField] private TextMeshProUGUI craftDetailTitle;
    [SerializeField] private GameObject craftWeaponSection;
    [SerializeField] private GameObject craftShieldSection;
    [SerializeField] private TextMeshProUGUI craftWeaponDamageText;
    [SerializeField] private TextMeshProUGUI craftWeaponCriticalText;
    [SerializeField] private TextMeshProUGUI craftWeaponWeightText;
    [SerializeField] private TextMeshProUGUI craftWeaponScalingText;
    [SerializeField] private TextMeshProUGUI craftWeaponRequirementText;
    [SerializeField] private TextMeshProUGUI craftShieldDamageText;
    [SerializeField] private TextMeshProUGUI craftShieldCriticalText;
    [SerializeField] private TextMeshProUGUI craftShieldWeightText;
    [SerializeField] private TextMeshProUGUI craftShieldScalingText;
    [SerializeField] private TextMeshProUGUI craftShieldRequirementText;
    [SerializeField] private TextMeshProUGUI craftShieldPhysicalDefenseText;
    [SerializeField] private TextMeshProUGUI craftShieldMagicDefenseText;

    [Header("Craft Requirements")]
    [SerializeField] private Transform craftMaterialsRoot;
    [SerializeField] private QuestRewardItemUI craftMaterialRowPrefab;
    [SerializeField] private GameObject craftPriceRoot;
    [SerializeField] private TextMeshProUGUI craftPriceText;
    [SerializeField] private Button craftButton;
    [SerializeField] private SegmentedButtonSelectionUI craftButtonSelection;

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
    private readonly List<InventorySlot> upgradeItemSlots = new List<InventorySlot>();
    private readonly List<InventoryItem> upgradeItems = new List<InventoryItem>();
    private readonly List<QuestRewardItemUI> materialRows = new List<QuestRewardItemUI>();
    private readonly List<CraftingRecipeData> visibleCraftRecipes = new List<CraftingRecipeData>();
    private readonly List<DialogueChoiceUI> craftRecipeRows = new List<DialogueChoiceUI>();
    private readonly List<QuestRewardItemUI> craftMaterialRows = new List<QuestRewardItemUI>();
    private int selectedUpgradeIndex = -1;
    private int selectedCraftIndex = -1;
    private int openingFrame = -1;
    private float lastNavigationTime = -999f;
    private const float NavigationRepeatCooldown = 0.20f;
    private Coroutine contentAppearRoutine;
    private Coroutine closeRoutine;
    private Coroutine upgradeButtonFocusRoutine;
    private Coroutine craftButtonFocusRoutine;
    private int lastUpgradeActivationFrame = -1;
    private int upgradeButtonFocusEnteredFrame = -1;
    private int lastCraftActivationFrame = -1;
    private int craftButtonFocusEnteredFrame = -1;
    private FocusArea focusArea = FocusArea.Grid;
    private bool isClosing;
    private bool isInteractive;

    private void Awake()
    {
        confirmCallback = OnConfirmPerformed;
        cancelCallback = OnCancelPerformed;

        if (blacksmithHud != null)
            blacksmithHud.SetActive(false);
        ResolveUpgradeRequirementReferences();
        ResolveCraftReferences();
        HideContentAppearAnimation();
        HideAllContentGroups();
        HideUpgradeSections();
        HideCraftSections();
    }

    private void OnDestroy()
    {
        if (upgradeButton != null)
            upgradeButton.onClick.RemoveListener(OnUpgradeButtonClicked);
        if (craftButton != null)
            craftButton.onClick.RemoveListener(OnCraftButtonClicked);
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
        ClearActionFocusVisuals();
        openingFrame = Time.frameCount;
        playerController.AcquireGameplayInputLock(gameplayLockOwner);
        SubscribeInput();
        ApplyModeRoots();
        HideContentGroup();
        if (blacksmithHud != null)
            blacksmithHud.SetActive(true);
        RefreshCurrentMode();
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
        ClearActionFocusVisuals();
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
        if (craftButtonFocusRoutine != null)
            StopCoroutine(craftButtonFocusRoutine);
        craftButtonFocusRoutine = null;
        focusArea = FocusArea.Grid;
        ClearActionFocusVisuals();
        ClearUpgradeItemRows();
        ClearCraftRecipeRows();
        ClearGeneratedCraftMaterialRows();
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
        if (CurrentMode == BlacksmithMode.Craft)
        {
            if (navigation.y > 0.5f)
                MoveCraftFocus(-1);
            else if (navigation.y < -0.5f)
                MoveCraftFocus(1);
            else
                return;

            lastNavigationTime = Time.unscaledTime;
            return;
        }

        if (navigation.x > 0.5f)
            MoveUpgradeFocusHorizontal(1);
        else if (navigation.x < -0.5f)
            MoveUpgradeFocusHorizontal(-1);
        else if (navigation.y > 0.5f)
            MoveUpgradeFocusVertical(-1);
        else if (navigation.y < -0.5f)
            MoveUpgradeFocusVertical(1);
        else
            return;

        lastNavigationTime = Time.unscaledTime;
    }

    private void ApplyModeRoots()
    {
        if (upgradeModeRoot != null)
            upgradeModeRoot.SetActive(CurrentMode == BlacksmithMode.Upgrade);
        if (craftModeRoot != null)
            craftModeRoot.SetActive(CurrentMode == BlacksmithMode.Craft);
    }

    private void RefreshCurrentMode()
    {
        if (CurrentMode == BlacksmithMode.Craft)
            RefreshCraftingList();
        else
            RefreshBlacksmithGrid();
    }

    private void RefreshCraftingList()
    {
        int previousIndex = selectedCraftIndex;
        ClearCraftRecipeRows();
        visibleCraftRecipes.Clear();

        for (int i = 0; i < recipes.Count; i++)
        {
            CraftingRecipeData recipe = recipes[i];
            if (recipe == null || recipe.resultWeapon == null || !recipe.resultWeapon.canCraft
                || recipe.resultWeapon.category == WeaponCategory.Unarmed || !IsRecipeUnlocked(recipe))
                continue;

            visibleCraftRecipes.Add(recipe);
        }

        for (int i = 0; i < visibleCraftRecipes.Count; i++)
        {
            if (craftListRoot == null || craftRecipeRowPrefab == null)
                break;

            int recipeIndex = i;
            CraftingRecipeData recipe = visibleCraftRecipes[i];
            DialogueChoiceUI row = Instantiate(craftRecipeRowPrefab, craftListRoot, false);
            row.name = $"CraftRecipe_{i:00}";
            row.gameObject.SetActive(true);
            row.Bind(GetCraftRecipeDisplayName(recipe), true, false);

            Navigation navigation = row.Button.navigation;
            navigation.mode = Navigation.Mode.None;
            row.Button.navigation = navigation;
            row.Button.onClick.AddListener(() => HandleCraftRecipeSubmit(recipeIndex));
            craftRecipeRows.Add(row);
        }

        int nextIndex = visibleCraftRecipes.Count > 0
            ? Mathf.Clamp(previousIndex < 0 ? 0 : previousIndex, 0, visibleCraftRecipes.Count - 1)
            : -1;
        craftListScroll?.Refresh(previousIndex < 0);
        SetCraftFocus(nextIndex);
    }

    private static string GetCraftRecipeDisplayName(CraftingRecipeData recipe)
    {
        return recipe != null && recipe.resultWeapon != null
            ? WeaponUpgradeCalculator.GetDisplayName(recipe.resultWeapon, recipe.startingUpgradeLevel)
            : string.Empty;
    }

    private void ClearCraftRecipeRows()
    {
        for (int i = 0; i < craftRecipeRows.Count; i++)
        {
            if (craftRecipeRows[i] != null)
            {
                craftRecipeRows[i].gameObject.SetActive(false);
                Destroy(craftRecipeRows[i].gameObject);
            }
        }

        craftRecipeRows.Clear();
        craftListScroll?.Refresh(false);
    }

    private CraftingRecipeData SelectedCraftRecipe => selectedCraftIndex >= 0
        && selectedCraftIndex < visibleCraftRecipes.Count
            ? visibleCraftRecipes[selectedCraftIndex]
            : null;

    private void HandleCraftRecipeSubmit(int index)
    {
        if (!isInteractive || index < 0 || index >= visibleCraftRecipes.Count)
            return;

        if (selectedCraftIndex != index)
            SetCraftFocus(index);
        TryFocusCraftButton();
    }

    private void SetCraftFocus(int index)
    {
        focusArea = FocusArea.Grid;
        ClearActionFocusVisuals();
        selectedCraftIndex = index >= 0 && index < visibleCraftRecipes.Count ? index : -1;
        RefreshCraftDetails();

        if (EventSystem.current == null || selectedCraftIndex < 0
            || selectedCraftIndex >= craftRecipeRows.Count)
            return;

        GameObject target = craftRecipeRows[selectedCraftIndex].gameObject;
        if (EventSystem.current.currentSelectedGameObject != target)
            EventSystem.current.SetSelectedGameObject(target);
        craftListScroll?.EnsureVisible(craftRecipeRows[selectedCraftIndex].transform as RectTransform);
    }

    private void MoveCraftFocus(int direction)
    {
        if (visibleCraftRecipes.Count == 0)
            return;

        int next = selectedCraftIndex + (direction >= 0 ? 1 : -1);
        if (next >= visibleCraftRecipes.Count) next = 0;
        if (next < 0) next = visibleCraftRecipes.Count - 1;
        SetCraftFocus(next);
    }

    private void RefreshBlacksmithGrid()
    {
        int previousIndex = selectedUpgradeIndex;
        ClearUpgradeItemRows();
        upgradeItems.Clear();
        int slotCount = Mathf.Max(1, upgradeGridSlotCount);
        if (playerInventory != null)
        {
            IReadOnlyList<InventoryItem> inventoryItems = playerInventory.Items;
            for (int i = 0; i < inventoryItems.Count; i++)
            {
                InventoryItem item = inventoryItems[i];
                if (item != null && item.weaponData != null
                    && item.weaponData.canUpgrade
                    && item.weaponData.category != WeaponCategory.Unarmed)
                {
                    upgradeItems.Add(item);
                    if (upgradeItems.Count >= slotCount)
                        break;
                }
            }
        }

        for (int i = 0; i < slotCount; i++)
        {
            if (slotParent == null || upgradeItemSlotPrefab == null)
                break;

            InventorySlot slot = Instantiate(upgradeItemSlotPrefab, slotParent, false);
            slot.name = $"UpgradeWeaponSlot_{i:00}";
            slot.gameObject.SetActive(true);
            bool hasWeapon = i < upgradeItems.Count;
            slot.Init(i, hasWeapon ? this : null);
            slot.SetDisplayOnly(!hasWeapon);
            if (hasWeapon)
                slot.Setup(GetItemIcon(upgradeItems[i]), 1, false);
            else
                slot.Clear();
            upgradeItemSlots.Add(slot);
        }

        selectedUpgradeIndex = upgradeItems.Count > 0
            ? Mathf.Clamp(previousIndex < 0 ? 0 : previousIndex, 0, upgradeItems.Count - 1)
            : -1;
        upgradeListScroll?.Refresh(previousIndex < 0);
        SetBlacksmithFocus(selectedUpgradeIndex);
    }

    private void ClearUpgradeItemRows()
    {
        for (int i = 0; i < upgradeItemSlots.Count; i++)
        {
            if (upgradeItemSlots[i] == null)
                continue;
            upgradeItemSlots[i].gameObject.SetActive(false);
            Destroy(upgradeItemSlots[i].gameObject);
        }

        upgradeItemSlots.Clear();
        upgradeListScroll?.Refresh(false);
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

        RefreshUpgradeDetails();

        for (int i = 0; i < upgradeItemSlots.Count; i++)
            upgradeItemSlots[i]?.SetFocused(i == selectedUpgradeIndex);

        if (EventSystem.current != null && selectedUpgradeIndex >= 0
            && selectedUpgradeIndex < upgradeItemSlots.Count)
        {
            GameObject target = upgradeItemSlots[selectedUpgradeIndex].gameObject;
            if (EventSystem.current.currentSelectedGameObject != target)
                EventSystem.current.SetSelectedGameObject(target);
            upgradeListScroll?.EnsureVisible(upgradeItemSlots[selectedUpgradeIndex].transform as RectTransform);
        }
    }

    private void MoveUpgradeFocusHorizontal(int direction)
    {
        if (upgradeItems.Count == 0)
            return;

        int next = selectedUpgradeIndex + (direction >= 0 ? 1 : -1);
        if (next >= upgradeItems.Count) next = 0;
        if (next < 0) next = upgradeItems.Count - 1;
        SetBlacksmithFocus(next);
    }

    private void MoveUpgradeFocusVertical(int direction)
    {
        if (upgradeItems.Count == 0)
            return;

        int columns = GetUpgradeGridColumnCount();
        int start = selectedUpgradeIndex;
        if (start < 0 || start >= upgradeItems.Count) start = 0;
        int next = (start + (direction >= 0 ? columns : -columns)) % upgradeItems.Count;
        if (next < 0) next += upgradeItems.Count;
        SetBlacksmithFocus(next);
    }

    private int GetUpgradeGridColumnCount()
    {
        GridLayoutGroup grid = slotParent != null ? slotParent.GetComponent<GridLayoutGroup>() : null;
        return grid != null && grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount
            ? Mathf.Max(1, grid.constraintCount)
            : Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(upgradeItems.Count)));
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

        if (item.weaponData == null)
        {
            RefreshUpgradeRequirements(item);
            return;
        }

        WeaponItem weapon = item.weaponData;
        int currentLevel = WeaponUpgradeRules.ClampLevel(weapon, item.upgradeLevel);
        int maxLevel = WeaponUpgradeRules.GetMaxLevel(weapon.rarity);
        bool hasNextLevel = weapon != null
            && weapon.canUpgrade
            && weapon.category != WeaponCategory.Unarmed
            && currentLevel < maxLevel;
        int nextLevel = hasNextLevel ? currentLevel + 1 : currentLevel;
        EffectiveWeaponStats currentStats = WeaponUpgradeCalculator.GetStats(weapon, currentLevel);
        EffectiveWeaponStats nextStats = hasNextLevel
            ? WeaponUpgradeCalculator.GetStats(weapon, nextLevel)
            : currentStats;

        if (weapon.category == WeaponCategory.Shield)
        {
            if (shieldSection != null)
                shieldSection.SetActive(true);

            SetDetailText(shieldDamageText,
                FormatPreviewInt(currentStats.PhysicalDamage, nextStats.PhysicalDamage, hasNextLevel));
            SetDetailText(shieldCriticalText,
                FormatPreviewFloat(currentStats.CriticalHit, nextStats.CriticalHit, hasNextLevel));
            SetDetailText(shieldWeightText, weapon.weight.ToString("0.##"));
            SetDetailText(shieldScalingText, GetScalingLabel(currentStats, nextStats, hasNextLevel));
            SetDetailText(shieldLevelText, GetUpgradeLevelPreviewLabel(item));
            SetDetailText(shieldPhysicalDefenseText,
                FormatPreviewPercent(currentStats.PhysicalBlockPercent, nextStats.PhysicalBlockPercent, hasNextLevel));
            SetDetailText(shieldMagicDefenseText,
                FormatPreviewPercent(currentStats.MagicBlockPercent, nextStats.MagicBlockPercent, hasNextLevel));
        }
        else
        {
            if (weaponSection != null)
                weaponSection.SetActive(true);

            SetDetailText(weaponDamageText,
                FormatPreviewInt(currentStats.PhysicalDamage, nextStats.PhysicalDamage, hasNextLevel));
            SetDetailText(weaponCriticalText,
                FormatPreviewFloat(currentStats.CriticalHit, nextStats.CriticalHit, hasNextLevel));
            SetDetailText(weaponWeightText, weapon.weight.ToString("0.##"));
            SetDetailText(weaponScalingText, GetScalingLabel(currentStats, nextStats, hasNextLevel));
            SetDetailText(weaponLevelText, GetUpgradeLevelPreviewLabel(item));
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

    private void ResolveCraftReferences()
    {
        if (craftButton != null)
        {
            craftButton.onClick.RemoveListener(OnCraftButtonClicked);
            craftButton.onClick.AddListener(OnCraftButtonClicked);
        }
    }

    private void RefreshCraftDetails()
    {
        if (craftDetailRoot != null)
            craftDetailRoot.SetActive(true);

        HideCraftSections();

        CraftingRecipeData recipe = SelectedCraftRecipe;
        if (recipe == null || recipe.resultWeapon == null)
            return;

        WeaponItem weapon = recipe.resultWeapon;
        int level = WeaponUpgradeRules.ClampLevel(weapon, recipe.startingUpgradeLevel);
        EffectiveWeaponStats stats = WeaponUpgradeCalculator.GetStats(weapon, level);

        if (craftDetailImage != null)
        {
            craftDetailImage.sprite = weapon.icon;
            craftDetailImage.enabled = weapon.icon != null;
            craftDetailImage.preserveAspect = true;
        }
        SetDetailText(craftDetailTitle, WeaponUpgradeCalculator.GetDisplayName(weapon, level));

        if (weapon.category == WeaponCategory.Shield)
        {
            if (craftShieldSection != null)
                craftShieldSection.SetActive(true);

            SetDetailText(craftShieldDamageText, stats.PhysicalDamage.ToString());
            SetDetailText(craftShieldCriticalText, stats.CriticalHit.ToString("0.##"));
            SetDetailText(craftShieldWeightText, weapon.weight.ToString("0.##"));
            SetDetailText(craftShieldScalingText, GetScalingLabel(stats, stats, false));
            SetDetailText(craftShieldRequirementText, weapon.GetRequirementsLabel());
            SetDetailText(craftShieldPhysicalDefenseText,
                Mathf.RoundToInt(stats.PhysicalBlockPercent * 100f) + "%");
            SetDetailText(craftShieldMagicDefenseText,
                Mathf.RoundToInt(stats.MagicBlockPercent * 100f) + "%");
        }
        else
        {
            if (craftWeaponSection != null)
                craftWeaponSection.SetActive(true);

            SetDetailText(craftWeaponDamageText, stats.PhysicalDamage.ToString());
            SetDetailText(craftWeaponCriticalText, stats.CriticalHit.ToString("0.##"));
            SetDetailText(craftWeaponWeightText, weapon.weight.ToString("0.##"));
            SetDetailText(craftWeaponScalingText, GetScalingLabel(stats, stats, false));
            SetDetailText(craftWeaponRequirementText, weapon.GetRequirementsLabel());
        }

        RefreshCraftRequirements(recipe);
    }

    private void HideCraftSections()
    {
        if (craftWeaponSection != null) craftWeaponSection.SetActive(false);
        if (craftShieldSection != null) craftShieldSection.SetActive(false);
        if (craftDetailImage != null)
        {
            craftDetailImage.sprite = null;
            craftDetailImage.enabled = false;
        }
        SetDetailText(craftDetailTitle, string.Empty);

        TextMeshProUGUI[] fields =
        {
            craftWeaponDamageText, craftWeaponCriticalText, craftWeaponWeightText,
            craftWeaponScalingText, craftWeaponRequirementText, craftShieldDamageText,
            craftShieldCriticalText, craftShieldWeightText, craftShieldScalingText,
            craftShieldRequirementText, craftShieldPhysicalDefenseText,
            craftShieldMagicDefenseText
        };
        for (int i = 0; i < fields.Length; i++)
            SetDetailText(fields[i], string.Empty);

        HideCraftRequirements();
    }

    private void RefreshCraftRequirements(CraftingRecipeData recipe)
    {
        BlacksmithCraftCheck check = recipe != null ? CanCraft(recipe) : new BlacksmithCraftCheck();
        List<BlacksmithRequirementStatus> requirements = check.Materials
            ?? new List<BlacksmithRequirementStatus>();

        if (craftPriceRoot != null)
            craftPriceRoot.SetActive(check.CoinCost > 0);
        SetDetailText(craftPriceText, check.CoinCost > 0 ? check.CoinCost.ToString("N0") : string.Empty);

        ClearGeneratedCraftMaterialRows();
        for (int i = 0; i < requirements.Count; i++)
        {
            if (craftMaterialsRoot == null || craftMaterialRowPrefab == null)
                break;

            QuestRewardItemUI row = Instantiate(craftMaterialRowPrefab, craftMaterialsRoot, false);
            row.name = $"CraftMaterial_{i:00}";
            row.gameObject.SetActive(true);
            BlacksmithRequirementStatus requirement = requirements[i];
            row.SetRequirementData(
                requirement.item != null ? requirement.item.icon : null,
                requirement.item != null ? requirement.item.itemName : string.Empty,
                requirement.owned,
                requirement.required);
            craftMaterialRows.Add(row);
        }

        if (craftMaterialsRoot != null)
            craftMaterialsRoot.gameObject.SetActive(requirements.Count > 0
                && craftMaterialRows.Count == requirements.Count);

        if (craftButton != null)
        {
            craftButton.gameObject.SetActive(recipe != null);
            craftButton.interactable = recipe != null && check.IsValid;
        }
    }

    private void ClearGeneratedCraftMaterialRows()
    {
        for (int i = 0; i < craftMaterialRows.Count; i++)
        {
            if (craftMaterialRows[i] != null)
                Destroy(craftMaterialRows[i].gameObject);
        }

        craftMaterialRows.Clear();
    }

    private void HideCraftRequirements()
    {
        if (craftMaterialsRoot != null)
            craftMaterialsRoot.gameObject.SetActive(false);
        if (craftPriceRoot != null)
            craftPriceRoot.SetActive(false);
        SetDetailText(craftPriceText, string.Empty);
        if (craftButton != null)
        {
            craftButton.gameObject.SetActive(false);
            craftButton.interactable = false;
        }

        ClearGeneratedCraftMaterialRows();
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

    private void OnCraftButtonClicked()
    {
        CraftingRecipeData recipe = SelectedCraftRecipe;
        if (recipe == null || lastCraftActivationFrame == Time.frameCount
            || craftButtonFocusEnteredFrame == Time.frameCount)
            return;

        lastCraftActivationFrame = Time.frameCount;
        focusArea = FocusArea.Grid;
        ClearCraftButtonFocusVisual();

        if (TryCraft(recipe, out _))
            RefreshCraftingList();
        else
            RefreshCraftDetails();
    }

    private void ClearDetailTextFields()
    {
        TextMeshProUGUI[] fields =
        {
            weaponDamageText, weaponCriticalText, weaponWeightText, weaponScalingText,
            weaponLevelText, shieldDamageText, shieldCriticalText, shieldWeightText,
            shieldScalingText, shieldLevelText, shieldPhysicalDefenseText,
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

    private string GetScalingLabel(
        EffectiveWeaponStats currentStats,
        EffectiveWeaponStats nextStats,
        bool hasNextLevel)
    {
        var parts = new List<string>();
        AddScalingPreview(parts, "STR", currentStats.StrengthScalingRank, nextStats.StrengthScalingRank, hasNextLevel);
        AddScalingPreview(parts, "DEX", currentStats.DexterityScalingRank, nextStats.DexterityScalingRank, hasNextLevel);
        AddScalingPreview(parts, "INT", currentStats.IntelligenceScalingRank, nextStats.IntelligenceScalingRank, hasNextLevel);
        AddScalingPreview(parts, "FAI", currentStats.FaithScalingRank, nextStats.FaithScalingRank, hasNextLevel);
        return string.Join(" / ", parts);
    }

    private void AddScalingPreview(
        List<string> parts,
        string label,
        WeaponItem.ScalingRank currentRank,
        WeaponItem.ScalingRank nextRank,
        bool hasNextLevel)
    {
        if (currentRank == WeaponItem.ScalingRank.None)
            return;

        string value = label + " " + currentRank;
        if (hasNextLevel && nextRank != currentRank && nextRank != WeaponItem.ScalingRank.None)
        {
            string color = ColorUtility.ToHtmlStringRGB(upgradePreviewColor);
            value += $" <color=#{color}>(\u2192 {nextRank})</color>";
        }

        parts.Add(value);
    }

    private string FormatPreviewInt(int currentValue, int nextValue, bool hasNextLevel)
    {
        return currentValue + (hasNextLevel
            ? FormatPreviewDelta(nextValue - currentValue, "0")
            : string.Empty);
    }

    private string FormatPreviewFloat(float currentValue, float nextValue, bool hasNextLevel)
    {
        return currentValue.ToString("0.##") + (hasNextLevel
            ? FormatPreviewDelta(nextValue - currentValue, "0.##")
            : string.Empty);
    }

    private string FormatPreviewPercent(float currentValue, float nextValue, bool hasNextLevel)
    {
        int currentPercent = Mathf.RoundToInt(currentValue * 100f);
        int nextPercent = Mathf.RoundToInt(nextValue * 100f);
        return currentPercent + "%" + (hasNextLevel
            ? FormatPreviewDelta(nextPercent - currentPercent, "0", "%")
            : string.Empty);
    }

    private string FormatPreviewDelta(float delta, string numberFormat, string suffix = "")
    {
        if (Mathf.Approximately(delta, 0f))
            return string.Empty;

        string sign = delta > 0f ? "+" : string.Empty;
        string color = ColorUtility.ToHtmlStringRGB(upgradePreviewColor);
        return $" <color=#{color}>({sign}{delta.ToString(numberFormat)}{suffix})</color>";
    }

    private static string GetUpgradeLevelLabel(InventoryItem item)
    {
        if (item == null || item.weaponData == null)
            return string.Empty;

        int level = WeaponUpgradeRules.ClampLevel(item.weaponData, item.upgradeLevel);
        int maxLevel = WeaponUpgradeRules.GetMaxLevel(item.weaponData.rarity);
        return level >= maxLevel ? "MAX" : level.ToString();
    }

    private string GetUpgradeLevelPreviewLabel(InventoryItem item)
    {
        string currentLabel = GetUpgradeLevelLabel(item);
        if (currentLabel == "MAX" || item == null || item.weaponData == null)
            return currentLabel;

        WeaponItem weapon = item.weaponData;
        int currentLevel = WeaponUpgradeRules.ClampLevel(weapon, item.upgradeLevel);
        int maxLevel = WeaponUpgradeRules.GetMaxLevel(weapon.rarity);
        if (!weapon.canUpgrade || weapon.category == WeaponCategory.Unarmed || currentLevel >= maxLevel)
            return currentLabel;

        return currentLabel + FormatPreviewDelta(1f, "0");
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
        if (lastUpgradeActivationFrame == Time.frameCount || lastCraftActivationFrame == Time.frameCount)
            return;

        if (CurrentMode == BlacksmithMode.Craft)
        {
            if (IsCraftButtonFocused())
            {
                if (craftButtonFocusEnteredFrame == Time.frameCount)
                    return;
                OnCraftButtonClicked();
                return;
            }

            if (SelectedCraftRecipe != null)
                TryFocusCraftButton();
            return;
        }

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
        return CurrentMode == BlacksmithMode.Upgrade && focusArea == FocusArea.Action;
    }

    private void ClearUpgradeButtonFocusVisual()
    {
        upgradeButtonSelection?.SetFocused(false);
    }

    private bool TryFocusCraftButton()
    {
        if (craftButton == null || !craftButton.gameObject.activeInHierarchy || !craftButton.interactable)
            return false;
        if (EventSystem.current == null)
            return false;

        if (focusArea != FocusArea.Action)
            craftButtonFocusEnteredFrame = Time.frameCount;
        focusArea = FocusArea.Action;
        craftButtonSelection?.SetFocused(true);

        if (craftButtonFocusRoutine != null)
            StopCoroutine(craftButtonFocusRoutine);
        craftButtonFocusRoutine = StartCoroutine(KeepCraftButtonFocusedAfterSubmit());
        return true;
    }

    private System.Collections.IEnumerator KeepCraftButtonFocusedAfterSubmit()
    {
        yield return null;
        craftButtonFocusRoutine = null;
        if (IsOpen && isInteractive && IsCraftButtonFocused() && craftButton != null
            && craftButton.gameObject.activeInHierarchy && craftButton.interactable
            && EventSystem.current != null)
        {
            craftButton.Select();
            if (EventSystem.current.currentSelectedGameObject != craftButton.gameObject)
                EventSystem.current.SetSelectedGameObject(craftButton.gameObject);
        }
    }

    private bool IsCraftButtonFocused()
    {
        return CurrentMode == BlacksmithMode.Craft && focusArea == FocusArea.Action;
    }

    private void ClearCraftButtonFocusVisual()
    {
        craftButtonSelection?.SetFocused(false);
    }

    private void ClearActionFocusVisuals()
    {
        ClearUpgradeButtonFocusVisual();
        ClearCraftButtonFocusVisual();
    }

    private void ReturnFocusToGrid()
    {
        focusArea = FocusArea.Grid;
        ClearActionFocusVisuals();
        if (EventSystem.current == null)
            return;

        if (CurrentMode == BlacksmithMode.Craft)
        {
            if (selectedCraftIndex < 0 || selectedCraftIndex >= craftRecipeRows.Count)
                return;
            GameObject craftTarget = craftRecipeRows[selectedCraftIndex].gameObject;
            if (EventSystem.current.currentSelectedGameObject != craftTarget)
                EventSystem.current.SetSelectedGameObject(craftTarget);
            return;
        }

        if (selectedUpgradeIndex < 0 || selectedUpgradeIndex >= upgradeItemSlots.Count)
            return;

        GameObject target = upgradeItemSlots[selectedUpgradeIndex].gameObject;
        if (EventSystem.current.currentSelectedGameObject != target)
            EventSystem.current.SetSelectedGameObject(target);
        upgradeListScroll?.EnsureVisible(upgradeItemSlots[selectedUpgradeIndex].transform as RectTransform);
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
        if (CurrentMode == BlacksmithMode.Craft)
        {
            if (visibleCraftRecipes.Count > 0)
                SetCraftFocus(0);
            return;
        }

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
        SetContentGroupState(GetActiveContentGroup(), 0f, false);
    }

    private void HideAllContentGroups()
    {
        SetContentGroupState(contentGroup, 0f, false);
        SetContentGroupState(craftContentGroup, 0f, false);
    }

    private void ShowContentGroup()
    {
        SetContentGroupState(GetActiveContentGroup(), 1f, false);
        SetContentInteraction(false);
    }

    private void SetContentInteraction(bool enabled)
    {
        CanvasGroup activeGroup = GetActiveContentGroup();
        if (activeGroup == null)
            return;
        activeGroup.interactable = enabled;
        activeGroup.blocksRaycasts = enabled;
    }

    private CanvasGroup GetActiveContentGroup()
    {
        return CurrentMode == BlacksmithMode.Craft ? craftContentGroup : contentGroup;
    }

    private static void SetContentGroupState(CanvasGroup group, float alpha, bool interactive)
    {
        if (group == null)
            return;
        group.alpha = alpha;
        group.interactable = interactive;
        group.blocksRaycasts = interactive;
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
        check.MaxLevel = WeaponUpgradeRules.GetMaxLevel(weapon.rarity);
        check.IsMaxLevel = check.CurrentLevel >= check.MaxLevel;
        check.TargetLevel = check.IsMaxLevel ? check.CurrentLevel : check.CurrentLevel + 1;

        if (!weapon.canUpgrade || weapon.category == WeaponCategory.Unarmed)
        {
            check.FailureReason = "Arma non upgradeabile.";
            return check;
        }

        if (check.IsMaxLevel)
        {
            check.FailureReason = "Livello massimo raggiunto.";
            return check;
        }

        if (inventory == null || stats == null)
        {
            check.FailureReason = "Dipendenze player mancanti.";
            return check;
        }

        check.CoinCost = WeaponUpgradeCalculator.GetUpgradeCoinCost(weapon, check.TargetLevel);
        if (!stats.HasCoins(check.CoinCost))
            check.FailureReason = "Monete insufficienti.";

        List<UpgradeMaterialRequirement> requirements = WeaponUpgradeCalculator.GetUpgradeMaterialRequirements(weapon, check.TargetLevel);
        for (int i = 0; i < requirements.Count; i++)
        {
            UpgradeMaterialRequirement requirement = requirements[i];
            int owned = stats != null ? stats.MaterialStorage.GetAmount(requirement.item) : 0;
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
            if (stats.MaterialStorage.TryRemove(requirement.item, requirement.required))
            {
                removed.Add(requirement);
                continue;
            }

            if (result.CoinCost > 0) stats.AddCoins(result.CoinCost, false);
            for (int j = 0; j < removed.Count; j++)
                stats.MaterialStorage.TryAdd(removed[j].item, removed[j].required);
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
            check.FailureReason = "InventoryFull";
        else if (!stats.HasCoins(recipe.coinCost))
            check.FailureReason = "Monete insufficienti.";

        check.CoinCost = Mathf.Max(0, recipe.coinCost);
        if (inventory != null && recipe.materialRequirements != null)
        {
            for (int i = 0; i < recipe.materialRequirements.Count; i++)
            {
                UpgradeMaterialRequirement requirement = recipe.materialRequirements[i];
                if (requirement == null || requirement.item == null || requirement.amount <= 0) continue;
                int owned = stats.MaterialStorage.GetAmount(requirement.item);
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
            if (stats.MaterialStorage.TryRemove(requirement.item, requirement.required))
            {
                removed.Add(requirement);
                continue;
            }

            if (result.CoinCost > 0) stats.AddCoins(result.CoinCost, false);
            for (int j = 0; j < removed.Count; j++)
                stats.MaterialStorage.TryAdd(removed[j].item, removed[j].required);
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
                stats.MaterialStorage.TryAdd(removed[i].item, removed[i].required);
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

    public bool CanConvertWeaponToBlueprintFragment(string instanceId)
    {
        if (ActiveContext == null || ActiveContext.PlayerInventory == null || ActiveContext.PlayerStats == null
            || !ActiveContext.PlayerInventory.TryGetItemByInstanceId(instanceId, out InventoryItem item)
            || item == null || item.weaponData == null)
            return false;

        CraftingRecipeData recipe = FindRecipeForWeapon(item.weaponData);
        return recipe != null
            && (recipe.unlockType == RecipeUnlockType.Blueprint || recipe.unlockType == RecipeUnlockType.BlueprintAndStory)
            && !ActiveContext.PlayerStats.KnowsBlacksmithRecipe(recipe.recipeId)
            && ActiveContext.PlayerStats.GetBlacksmithBlueprintFragments(recipe.recipeId) < recipe.blueprintFragmentsRequired;
    }

    public bool TryConvertWeaponToBlueprintFragment(string instanceId)
    {
        if (!CanConvertWeaponToBlueprintFragment(instanceId)) return false;
        PlayerInventory inventory = ActiveContext.PlayerInventory;
        PlayerStats stats = ActiveContext.PlayerStats;
        inventory.TryGetItemByInstanceId(instanceId, out InventoryItem item);
        CraftingRecipeData recipe = FindRecipeForWeapon(item.weaponData);

        if (!inventory.TryRemoveInstance(instanceId, 1, out int remaining, save: false))
            return false;

        if (stats.TryAddBlacksmithBlueprintFragment(recipe.recipeId, recipe.blueprintFragmentsRequired, save: false))
        {
            stats.SaveStatsImmediate();
            return true;
        }

        if (remaining > 0)
            inventory.TryAdjustInstanceAmount(instanceId, 1, out _, save: false);
        else
        {
            InventoryItem restored = new InventoryItem(item.weaponData, 1);
            restored.instanceId = instanceId;
            restored.upgradeLevel = item.upgradeLevel;
            inventory.TryAddItemInstance(restored, save: false);
        }
        return false;
    }

    private CraftingRecipeData FindRecipeForWeapon(WeaponItem weapon)
    {
        if (weapon == null || recipes == null) return null;
        for (int i = 0; i < recipes.Count; i++)
            if (recipes[i] != null && recipes[i].resultWeapon == weapon)
                return recipes[i];
        return null;
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
