using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class MagicManager : MonoBehaviour, IInventorySlotHandler
{
    private enum MagicView { Learn, Prepare }
    private enum FocusArea { LearnList, LearnAction, PrepareList, PrepareSlots }

    [Header("Magic UI")]
    [SerializeField] private GameObject magicHud;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private Animator bookAnimator;
    [SerializeField] private Animator contentAppearAnimator;
    [SerializeField] private CanvasGroup learnContentGroup;
    [SerializeField] private CanvasGroup equipContentGroup;

    [Header("Recipe List")]
    [SerializeField] private Transform recipeListRoot;
    [SerializeField] private DialogueChoiceUI recipeRowPrefab;
    [SerializeField] private ScrollableVerticalListUI recipeListScroll;

    [Header("Detail")]
    [SerializeField] private GameObject detailRoot;
    [SerializeField] private Image detailImage;
    [SerializeField] private TextMeshProUGUI detailTitle;
    [SerializeField] private TextMeshProUGUI scalingText;
    [SerializeField] private TextMeshProUGUI requirementsText;

    [Header("Attack")]
    [SerializeField] private GameObject attackRoot;
    [SerializeField] private TextMeshProUGUI damageText;
    [SerializeField] private TextMeshProUGUI criticalText;
    [SerializeField] private TextMeshProUGUI attackManaCostText;

    [Header("Boost")]
    [SerializeField] private GameObject boostRoot;
    [SerializeField] private TextMeshProUGUI boostAttributeText;
    [SerializeField] private TextMeshProUGUI boostAmountText;
    [SerializeField] private TextMeshProUGUI boostDurationText;
    [SerializeField] private TextMeshProUGUI boostManaCostText;

    [Header("Healing")]
    [SerializeField] private GameObject healingRoot;
    [SerializeField] private TextMeshProUGUI healingTypeText;
    [SerializeField] private TextMeshProUGUI healingAmountText;
    [SerializeField] private TextMeshProUGUI healingManaCostText;

    [Header("Requirements")]
    [SerializeField] private Transform materialsRoot;
    [SerializeField] private QuestRewardItemUI materialRowPrefab;
    [SerializeField] private GameObject priceRoot;
    [SerializeField] private TextMeshProUGUI priceText;

    [Header("Action")]
    [SerializeField] private Button actionButton;
    [SerializeField] private TextMeshProUGUI actionButtonLabel;

    [Header("Equip Magic Layout")]
    [SerializeField] private GameObject learnRoot;
    [SerializeField] private GameObject equipRoot;
    [SerializeField] private Transform equipMagicListRoot;
    [SerializeField] private DialogueChoiceUI equipMagicRowPrefab;
    [SerializeField] private ScrollableVerticalListUI preparedMagicListScroll;
    [SerializeField] private Transform equipSlotRoot;
    [SerializeField] private GridLayoutGroup equipSlotGrid;
    [SerializeField] private InventorySlot equipSlotPrefab;
    // Editor/bootstrap fallback only; once PlayerStats is available the domain
    // RunMagicCapacity is the sole gameplay capacity source.
    [SerializeField, Min(0)] private int equipSlotCount = 6;

    [Header("Initial State")]
    [SerializeField] private string bookOpenStateName = "BookOpen";
    [SerializeField] private string contentAppearStateName = "Transition";
    [SerializeField, Min(0f)] private float contentAppearDelay = 0.5833333f;
    [SerializeField, Min(0f)] private float contentAppearDuration = 1.8f;
    [SerializeField] private string bookCloseStateName = "CloseBook";
    [SerializeField, Min(0f)] private float bookCloseDuration = 0.6666666f;

    private readonly List<MagicRecipeData> visibleRecipes = new List<MagicRecipeData>();
    private readonly List<DialogueChoiceUI> recipeRows = new List<DialogueChoiceUI>();
    private readonly List<QuestRewardItemUI> materialRows = new List<QuestRewardItemUI>();
    private readonly List<MagicRecipeData> preparedRecipeRows = new List<MagicRecipeData>();
    private readonly List<DialogueChoiceUI> preparedRows = new List<DialogueChoiceUI>();
    private readonly List<InventorySlot> equipSlots = new List<InventorySlot>();
    private readonly object gameplayLockOwner = new object();
    private PlayerControls controls;
    private Action<InputAction.CallbackContext> confirmCallback;
    private Action<InputAction.CallbackContext> cancelCallback;
    private int selectedRecipeIndex = -1;
    private int selectedPreparedRecipeIndex = -1;
    private int armedPreparedRecipeIndex = -1;
    private int selectedPreparedSlotIndex;
    private int openingFrame = -1;
    private int lastActionActivationFrame = -1;
    private int lastPreparedSlotActivationFrame = -1;
    private int preparedSlotFocusEnteredFrame = -1;
    private int actionFocusEnteredFrame = -1;
    private float lastNavigationTime = -999f;
    private Coroutine contentAppearRoutine;
    private Coroutine closeRoutine;
    private MagicView currentView = MagicView.Learn;
    private FocusArea focusArea = FocusArea.LearnList;
    private bool isInteractive;
    private bool isClosing;

    public bool IsOpen { get; private set; }
    public NpcServiceContext ActiveContext { get; private set; }
    public IReadOnlyList<MagicRecipeData> Recipes => MagicRecipeCatalog;
    public event Action Closed;

    private MagicRecipeData SelectedRecipe => selectedRecipeIndex >= 0 && selectedRecipeIndex < visibleRecipes.Count
        ? visibleRecipes[selectedRecipeIndex] : null;
    private MagicRecipeData ArmedPreparedRecipe => armedPreparedRecipeIndex >= 0 && armedPreparedRecipeIndex < preparedRecipeRows.Count
        ? preparedRecipeRows[armedPreparedRecipeIndex] : null;

    private IReadOnlyList<MagicRecipeData> MagicRecipeCatalog
    {
        get
        {
            PlayerInventory inventory = ResolvePlayerInventory();
            return inventory != null ? inventory.MagicRecipes : Array.Empty<MagicRecipeData>();
        }
    }

    private void Awake()
    {
        confirmCallback = OnConfirmPerformed;
        cancelCallback = OnCancelPerformed;
        if (magicHud != null) magicHud.SetActive(false);
        if (actionButton != null) actionButton.onClick.AddListener(OnActionButtonClicked);
        EnsureEquipSlots();
        ResolveDetailReferences();
        HideContentAppearAnimation();
        HideAllContentGroups();
    }

    private void OnValidate()
    {
        ResolveDetailReferences();
    }

    private void ResolveDetailReferences()
    {
        if (detailRoot == null || (detailImage != null && detailTitle != null))
            return;

        Transform root = detailRoot.transform;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null)
                continue;

            if (detailImage == null && string.Equals(child.name, "Image", StringComparison.OrdinalIgnoreCase))
                detailImage = child.GetComponent<Image>();
            else if (detailTitle == null && string.Equals(child.name, "Title", StringComparison.OrdinalIgnoreCase))
                detailTitle = child.GetComponent<TextMeshProUGUI>();
        }
    }

    private void EnsureEquipSlots()
    {
        int capacity = GetRunMagicCapacity();
        if (equipSlotRoot == null || equipSlotPrefab == null || capacity <= 0)
            return;

        if (equipSlots.Count == 0)
            equipSlots.AddRange(equipSlotRoot.GetComponentsInChildren<InventorySlot>(true));

        while (equipSlots.Count < capacity)
        {
            InventorySlot slot = Instantiate(equipSlotPrefab, equipSlotRoot);
            slot.name = $"Magic Slot {equipSlots.Count + 1}";
            slot.Init(equipSlots.Count, this);
            slot.SetDisplayOnly(false);
            slot.Clear();
            slot.gameObject.SetActive(true);
            equipSlots.Add(slot);
        }

        for (int i = 0; i < equipSlots.Count; i++)
        {
            if (equipSlots[i] != null)
            {
                equipSlots[i].Init(i, this);
                equipSlots[i].SetDisplayOnly(false);
                equipSlots[i].gameObject.SetActive(i < capacity);
            }
        }
    }

    private void OnDestroy()
    {
        if (actionButton != null) actionButton.onClick.RemoveListener(OnActionButtonClicked);
    }

    private void Start()
    {
        // Enforce the run-loadout rule also for any legacy physical magic
        // restored before the magic UI is opened.
        RefreshPreparedMagicState();
    }

    private void OnDisable()
    {
        UnsubscribeInput();
        StopContentAppearRoutineOnly();
        if (closeRoutine != null)
            StopCoroutine(closeRoutine);
        closeRoutine = null;
        if (playerController != null) playerController.ReleaseGameplayInputLock(gameplayLockOwner);
        controls = null;
        openingFrame = -1;
        preparedSlotFocusEnteredFrame = -1;
        IsOpen = false;
        isInteractive = false;
        isClosing = false;
        ActiveContext = null;
        armedPreparedRecipeIndex = -1;
        ClearRows();
        ClearPreparedRows();
        if (magicHud != null) magicHud.SetActive(false);
    }

    public bool OpenMagic(NpcServiceContext context)
    {
        return OpenMagic(MagicServiceMode.Learn, context);
    }

    public bool OpenMagic(MagicServiceMode mode, NpcServiceContext context)
    {
        if (context == null || context.Player == null || context.PlayerStats == null || context.PlayerInventory == null || isClosing)
            return false;

        ActiveContext = context;
        playerStats = context.PlayerStats;
        playerInventory = context.PlayerInventory;
        playerController = context.Player.GetComponent<PlayerController>() ?? playerController;
        if (playerController == null || playerController.Controls == null)
        {
            ActiveContext = null;
            return false;
        }

        playerStats.TryEnsurePersistentStateReady();
        controls = playerController.Controls;
        IsOpen = true;
        isClosing = false;
        isInteractive = false;
        MagicView openingView = mode == MagicServiceMode.Equip ? MagicView.Prepare : MagicView.Learn;
        // Opening PREPARA always starts in list mode. A placement choice must
        // be confirmed during this opening, never carried over from a prior UI.
        armedPreparedRecipeIndex = -1;
        focusArea = openingView == MagicView.Prepare ? FocusArea.PrepareList : FocusArea.LearnList;
        openingFrame = Time.frameCount;
        preparedSlotFocusEnteredFrame = -1;
        playerController.AcquireGameplayInputLock(gameplayLockOwner);
        SubscribeInput();
        HideAllContentGroups();
        if (magicHud != null) magicHud.SetActive(true);
        SetMagicView(openingView, refresh: true, focus: true);
        DeactivateContentRoots();
        RestartBookOpenAnimation();
        Canvas.ForceUpdateCanvases();
        contentAppearRoutine = StartCoroutine(PlayContentAppearAnimation());
        return true;
    }

    public void CloseMagic()
    {
        if (!IsOpen || isClosing) return;
        IsOpen = false;
        isInteractive = false;
        isClosing = true;
        armedPreparedRecipeIndex = -1;
        UnsubscribeInput();
        StopContentAppearRoutineOnly();
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
        ActiveContext = null;

        if (!isActiveAndEnabled)
        {
            FinishClose();
            return;
        }

        closeRoutine = StartCoroutine(RunCloseAnimations());
    }

    private void Update()
    {
        if (!IsOpen || !isInteractive || controls == null || Time.unscaledTime < lastNavigationTime + 0.20f) return;
        Vector2 move = controls.Player.Move.ReadValue<Vector2>();
        if (Mathf.Abs(move.x) > 0.5f && currentView == MagicView.Prepare)
        {
            int direction = move.x > 0.5f ? 1 : -1;
            if (focusArea == FocusArea.PrepareSlots)
                MovePreparedSlotHorizontal(direction);
            else
                HandleHorizontalNavigation(direction);
            lastNavigationTime = Time.unscaledTime;
            return;
        }

        if (Mathf.Abs(move.y) <= 0.5f) return;
        if (focusArea == FocusArea.PrepareList)
            MovePreparedRecipeFocus(move.y > 0.5f ? -1 : 1);
        else if (focusArea == FocusArea.PrepareSlots)
            MovePreparedSlotVertical(move.y > 0.5f ? -1 : 1);
        else
            MoveRecipeFocus(move.y > 0.5f ? -1 : 1);
        lastNavigationTime = Time.unscaledTime;
    }

    public MagicLearnCheck CanLearnMagic(MagicRecipeData recipe)
    {
        var check = new MagicLearnCheck { CoinCost = recipe != null ? Mathf.Max(0, recipe.learnCoinCost) : 0 };
        if (recipe == null || recipe.resultMagic == null || string.IsNullOrWhiteSpace(recipe.recipeId))
            return Fail(check, MagicFailureReason.InvalidRecipe);
        if (!IsRecipeAvailable(recipe)) return Fail(check, MagicFailureReason.LockedRecipe);
        if (playerStats == null) return Fail(check, MagicFailureReason.InvalidRecipe);
        if (playerStats.KnowsMagicRecipe(recipe.recipeId)) return Fail(check, MagicFailureReason.AlreadyLearned);

        IReadOnlyList<MagicStatRequirement> stats = recipe.resultMagic.StatRequirements;
        if (stats != null)
        {
            for (int i = 0; i < stats.Count; i++)
            {
                MagicStatRequirement req = stats[i];
                if (req == null) continue;
                int required = Mathf.Max(1, req.requiredValue);
                int owned = MagicItemData.GetStatValue(playerStats, req.attribute);
                check.Stats.Add(new MagicStatRequirementStatus { Attribute = req.attribute, Required = required, Owned = owned, Satisfied = owned >= required });
            }
        }
        bool statsSatisfied = !check.Stats.Exists(x => !x.Satisfied);
        bool materialsSatisfied = BuildMaterialStatuses(recipe.learnMaterialRequirements, check.Materials);
        if (!statsSatisfied) return Fail(check, MagicFailureReason.MissingStats);
        if (!playerStats.HasCoins(check.CoinCost)) return Fail(check, MagicFailureReason.MissingCoins);
        if (!materialsSatisfied) return Fail(check, MagicFailureReason.MissingMaterials);
        check.IsValid = true;
        return check;
    }

    public bool TryLearnMagic(MagicRecipeData recipe, out MagicLearnCheck check)
    {
        check = CanLearnMagic(recipe);
        if (!check.IsValid) return false;
        if (!playerStats.TryRemoveCoins(check.CoinCost, false)) return false;
        var removedMaterials = new List<MagicRequirementStatus>();
        for (int i = 0; i < check.Materials.Count; i++)
        {
            MagicRequirementStatus status = check.Materials[i];
            if (!playerStats.MaterialStorage.TryRemove(status.Item, status.Required))
            {
                playerStats.AddCoins(check.CoinCost, false);
                RestoreMaterials(removedMaterials);
                return false;
            }
            removedMaterials.Add(status);
        }
        if (!playerStats.LearnMagicRecipe(recipe.recipeId, false))
        {
            playerStats.AddCoins(check.CoinCost, false);
            RestoreMaterials(removedMaterials);
            return false;
        }
        playerStats.SaveStats();
        return true;
    }

    public IReadOnlyList<MagicItemData> GetLearnedMagic()
    {
        var result = new List<MagicItemData>();
        PlayerStats stats = ResolvePlayerStats();
        if (stats == null) return result;
        IReadOnlyList<MagicRecipeData> catalog = MagicRecipeCatalog;
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < catalog.Count; i++)
        {
            MagicRecipeData recipe = catalog[i];
            if (recipe == null || recipe.resultMagic == null || string.IsNullOrWhiteSpace(recipe.recipeId))
                continue;
            if (!ids.Add(recipe.recipeId.Trim()))
                continue;
            if (!stats.KnowsMagicRecipe(recipe.recipeId))
                continue;
            result.Add(recipe.resultMagic);
        }
        return result;
    }

    /// <summary>Projection of the shared magic inventory; this manager never owns the layout.</summary>
    public MagicItemData[] GetMagicInventoryLayout()
    {
        PlayerInventory inventory = ResolvePlayerInventory();
        if (inventory == null || !inventory.TryGetMagicInventoryLayout(out MagicInventorySlotView[] layout))
            return Array.Empty<MagicItemData>();

        var result = new MagicItemData[layout.Length];
        for (int i = 0; i < layout.Length; i++)
            result[i] = layout[i].Magic;
        return result;
    }

    public int GetRunMagicCapacity()
    {
        PlayerInventory inventory = ResolvePlayerInventory();
        if (inventory != null)
            return inventory.MagicInventoryCapacity;
        PlayerStats stats = ResolvePlayerStats();
        return stats != null ? Mathf.Max(0, stats.RunMagicCapacity) : Mathf.Max(0, equipSlotCount);
    }

    public bool SetRunMagicAtSlot(int slot, MagicRecipeData recipe)
    {
        PlayerStats stats = ResolvePlayerStats();
        return recipe != null && stats != null && recipe.resultMagic != null
            && stats.KnowsMagicRecipe(recipe.recipeId)
            && stats.SetRunMagicAtSlot(slot, recipe.recipeId);
    }

    public bool RemoveRunMagicAtSlot(int slot)
    {
        PlayerStats stats = ResolvePlayerStats();
        bool changed = stats != null && stats.RemoveRunMagicAtSlot(slot);
        if (changed)
            RefreshPreparedMagicState();
        return changed;
    }

    public string[] GetRunMagicSelection()
    {
        PlayerStats stats = ResolvePlayerStats();
        return stats != null ? stats.GetRunMagicSelection() : Array.Empty<string>();
    }

    public void ClearRunMagicSelection()
    {
        ResolvePlayerStats()?.ClearRunMagicSelection();
        RefreshPreparedMagicState();
    }

    private PlayerStats ResolvePlayerStats()
    {
        return playerStats != null ? playerStats : PlayerStats.instance;
    }

    private PlayerInventory ResolvePlayerInventory()
    {
        if (playerInventory != null)
            return playerInventory;

        PlayerStats stats = ResolvePlayerStats();
        return stats != null ? stats.GetComponent<PlayerInventory>() : null;
    }

    private void RefreshPreparedMagicState()
    {
        PlayerInventory inventory = ResolvePlayerInventory();
        if (inventory != null)
            inventory.ValidateMagicLoadoutAgainstMagicInventory();

        if (IsOpen && currentView == MagicView.Prepare)
            RefreshPrepareView();
    }

    public bool CanConvertMagicToBlueprintFragment(string instanceId)
    {
        PlayerInventory inventory = ResolvePlayerInventory();
        PlayerStats stats = ResolvePlayerStats();
        if (inventory == null || stats == null || !inventory.TryGetItemByInstanceId(instanceId, out InventoryItem item)
            || item == null || item.magicData == null)
            return false;
        MagicRecipeData recipe = FindRecipeForMagic(item.magicData);
        return recipe != null && recipe.unlockType == MagicRecipeUnlockType.Blueprint
            && !stats.IsMagicRecipeUnlocked(recipe.recipeId)
            && !stats.KnowsMagicRecipe(recipe.recipeId)
            && stats.GetMagicBlueprintFragments(recipe.recipeId) < recipe.blueprintFragmentsRequired;
    }

    public bool TryConvertMagicToBlueprintFragment(string instanceId)
    {
        if (!CanConvertMagicToBlueprintFragment(instanceId)) return false;
        PlayerInventory inventory = ResolvePlayerInventory();
        PlayerStats stats = ResolvePlayerStats();
        if (inventory == null || stats == null || !inventory.TryGetItemByInstanceId(instanceId, out InventoryItem item))
            return false;
        MagicRecipeData recipe = FindRecipeForMagic(item.magicData);
        if (recipe == null || !inventory.TryRemoveInstance(instanceId, 1, out int remaining, save: false)) return false;

        if (stats.TryAddMagicBlueprintFragment(recipe.recipeId, recipe.blueprintFragmentsRequired, save: false))
        {
            stats.SaveStatsImmediate();
            return true;
        }

        if (remaining > 0)
            inventory.TryAdjustInstanceAmount(instanceId, 1, out _, save: false);
        else
        {
            InventoryItem restored = new InventoryItem(item.magicData, 1);
            restored.instanceId = instanceId;
            inventory.TryAddItemInstance(restored, save: false);
        }
        return false;
    }

    private MagicRecipeData FindRecipeForMagic(MagicItemData magic)
    {
        if (magic == null) return null;
        IReadOnlyList<MagicRecipeData> catalog = MagicRecipeCatalog;
        for (int i = 0; i < catalog.Count; i++)
            if (catalog[i] != null && !string.IsNullOrWhiteSpace(catalog[i].recipeId)
                && catalog[i].resultMagic == magic)
                return catalog[i];
        return null;
    }

    private bool IsRecipeAvailable(MagicRecipeData recipe)
    {
        PlayerStats stats = ResolvePlayerStats();
        return recipe != null && recipe.resultMagic != null && !string.IsNullOrWhiteSpace(recipe.recipeId)
               && (recipe.unlockType == MagicRecipeUnlockType.Default
                   || (stats != null && stats.IsMagicRecipeUnlocked(recipe.recipeId)));
    }

    private bool BuildMaterialStatuses(List<MagicMaterialRequirement> requirements, List<MagicRequirementStatus> output)
    {
        if (requirements == null) return true;
        bool valid = true;
        for (int i = 0; i < requirements.Count; i++)
        {
            MagicMaterialRequirement req = requirements[i];
            if (req == null || req.item == null) { valid = false; continue; }
            int required = Mathf.Max(1, req.amount);
            int owned = playerStats != null ? playerStats.MaterialStorage.GetAmount(req.item) : 0;
            output.Add(new MagicRequirementStatus { Item = req.item, Required = required, Owned = owned, Satisfied = owned >= required });
            if (owned < required) valid = false;
        }
        return valid;
    }

    private void RestoreMaterials(List<MagicRequirementStatus> removed)
    {
        for (int i = 0; i < removed.Count; i++)
            playerStats.MaterialStorage.TryAdd(removed[i].Item, removed[i].Required);
    }

    private static T Fail<T>(T check, MagicFailureReason reason) where T : class
    {
        if (check is MagicLearnCheck learn) { learn.FailureReason = reason; learn.IsValid = false; }
        return check;
    }

    private void RefreshRecipeList()
    {
        int previous = selectedRecipeIndex;
        ClearRows();
        visibleRecipes.Clear();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<MagicRecipeData> catalog = MagicRecipeCatalog;
        for (int i = 0; i < catalog.Count; i++)
        {
            MagicRecipeData recipe = catalog[i];
            if (!IsRecipeVisible(recipe)) continue;
            if (!ids.Add(recipe.recipeId.Trim()))
            {
                Debug.LogWarning($"[MagicManager] Recipe ID duplicato ignorato: '{recipe.recipeId}'.", this);
                continue;
            }
            visibleRecipes.Add(recipe);
        }

        for (int i = 0; i < visibleRecipes.Count && recipeListRoot != null && recipeRowPrefab != null; i++)
        {
            int index = i;
            DialogueChoiceUI row = Instantiate(recipeRowPrefab, recipeListRoot, false);
            row.name = "MagicRecipe_" + i.ToString("00");
            row.gameObject.SetActive(true);
            row.Bind(GetRecipeName(visibleRecipes[i]), true, false);
            Navigation navigation = row.Button.navigation;
            navigation.mode = Navigation.Mode.None;
            row.Button.navigation = navigation;
            row.Button.onClick.AddListener(() => SetRecipeFocus(index));
            recipeRows.Add(row);
        }

        selectedRecipeIndex = visibleRecipes.Count == 0 ? -1 : Mathf.Clamp(previous < 0 ? 0 : previous, 0, visibleRecipes.Count - 1);
        recipeListScroll?.Refresh(previous < 0);
        RefreshSelectedRecipe();
    }

    private bool IsRecipeVisible(MagicRecipeData recipe)
    {
        return recipe != null && recipe.resultMagic != null && !string.IsNullOrWhiteSpace(recipe.recipeId) && IsRecipeAvailable(recipe);
    }

    private void SetMagicView(MagicView view, bool refresh, bool focus)
    {
        if (EventSystem.current != null && IsOwnedSelection(EventSystem.current.currentSelectedGameObject))
            EventSystem.current.SetSelectedGameObject(null);

        currentView = view;
        if (learnRoot != null) learnRoot.SetActive(view == MagicView.Learn);
        if (equipRoot != null) equipRoot.SetActive(view == MagicView.Prepare);
        if (isInteractive)
            SetContentGroupState(GetActiveContentGroup(), 1f, true);

        if (view == MagicView.Learn)
        {
            ClearPreparedRows();
            preparedRecipeRows.Clear();
            armedPreparedRecipeIndex = -1;
            selectedPreparedRecipeIndex = -1;
            if (refresh) RefreshRecipeList();
            if (focus) SetRecipeFocus(selectedRecipeIndex < 0 ? 0 : selectedRecipeIndex);
            return;
        }

        ClearRows();
        // Entering PREPARA is never itself a placement confirmation.
        armedPreparedRecipeIndex = -1;
        if (refresh) RefreshPrepareView();
        if (focus) SetPreparedRecipeFocus(selectedPreparedRecipeIndex < 0 ? 0 : selectedPreparedRecipeIndex);
    }

    private void HandleHorizontalNavigation(int direction)
    {
        // Right may only enter slot focus after an explicit list confirmation.
        if (direction > 0 && ArmedPreparedRecipe != null)
            FocusPreparedSlots();
    }

    private void MovePreparedSlotHorizontal(int direction)
    {
        int capacity = GetRunMagicCapacity();
        int columns = GetPreparedSlotColumnCount(capacity);
        if (capacity <= 0 || columns <= 0)
            return;

        int rowStart = selectedPreparedSlotIndex / columns * columns;
        int rowEnd = Mathf.Min(rowStart + columns - 1, capacity - 1);
        if (direction < 0 && selectedPreparedSlotIndex <= rowStart)
        {
            FocusPreparedList();
            return;
        }

        int nextIndex = Mathf.Clamp(selectedPreparedSlotIndex + direction, rowStart, rowEnd);
        if (nextIndex != selectedPreparedSlotIndex)
            SetPreparedSlotFocus(nextIndex);
    }

    private void MovePreparedSlotVertical(int direction)
    {
        int capacity = GetRunMagicCapacity();
        int columns = GetPreparedSlotColumnCount(capacity);
        if (capacity <= 0 || columns <= 0)
            return;

        int nextIndex = selectedPreparedSlotIndex + direction * columns;
        if (nextIndex >= 0 && nextIndex < capacity)
            SetPreparedSlotFocus(nextIndex);
    }

    private int GetPreparedSlotColumnCount(int capacity)
    {
        if (equipSlotGrid == null)
            return 1;

        if (equipSlotGrid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            return Mathf.Max(1, equipSlotGrid.constraintCount);

        if (equipSlotGrid.constraint == GridLayoutGroup.Constraint.FixedRowCount)
            return Mathf.Max(1, Mathf.CeilToInt(capacity / (float)Mathf.Max(1, equipSlotGrid.constraintCount)));

        return 1;
    }

    private void RefreshPrepareView()
    {
        EnsureEquipSlots();
        RefreshPreparedRecipeRows();
        RefreshPreparedSlots();
    }

    private void RefreshPreparedRecipeRows()
    {
        int previousFocus = selectedPreparedRecipeIndex;
        ClearPreparedRows();
        preparedRecipeRows.Clear();

        PlayerStats stats = ResolvePlayerStats();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<MagicRecipeData> catalog = MagicRecipeCatalog;
        for (int i = 0; i < catalog.Count; i++)
        {
            MagicRecipeData recipe = catalog[i];
            if (recipe == null || recipe.resultMagic == null || string.IsNullOrWhiteSpace(recipe.recipeId))
                continue;

            if (!ids.Add(recipe.recipeId.Trim()))
            {
                Debug.LogWarning($"[MagicManager] Recipe ID duplicato ignorato in PREPARA: '{recipe.recipeId}'.", this);
                continue;
            }
            if (stats == null || !stats.KnowsMagicRecipe(recipe.recipeId))
                continue;

            preparedRecipeRows.Add(recipe);
        }

        for (int i = 0; i < preparedRecipeRows.Count && equipMagicListRoot != null && equipMagicRowPrefab != null; i++)
        {
            int index = i;
            DialogueChoiceUI row = Instantiate(equipMagicRowPrefab, equipMagicListRoot, false);
            row.name = "PreparedMagic_" + i.ToString("00");
            row.gameObject.SetActive(true);
            row.Bind(GetRecipeName(preparedRecipeRows[i]), true, false);
            Navigation navigation = row.Button.navigation;
            navigation.mode = Navigation.Mode.None;
            row.Button.navigation = navigation;
            row.Button.onClick.AddListener(() => HandlePreparedRecipeClicked(index));
            preparedRows.Add(row);
        }

        selectedPreparedRecipeIndex = preparedRecipeRows.Count == 0
            ? -1
            : Mathf.Clamp(previousFocus < 0 ? 0 : previousFocus, 0, preparedRecipeRows.Count - 1);
        if (armedPreparedRecipeIndex >= preparedRecipeRows.Count)
            armedPreparedRecipeIndex = -1;
        preparedMagicListScroll?.Refresh(previousFocus < 0);
    }

    private void RefreshPreparedSlots()
    {
        EnsureEquipSlots();
        MagicItemData[] layout = GetMagicInventoryLayout();
        int capacity = layout.Length;
        selectedPreparedSlotIndex = capacity == 0 ? -1 : Mathf.Clamp(selectedPreparedSlotIndex, 0, capacity - 1);

        for (int i = 0; i < equipSlots.Count; i++)
        {
            InventorySlot slot = equipSlots[i];
            if (slot == null) continue;

            bool active = i < capacity;
            slot.gameObject.SetActive(active);
            if (!active) continue;

            MagicItemData magic = layout[i];
            if (magic != null)
                slot.Setup(magic.icon, 1);
            else
                slot.Clear();
            slot.SetFocused(focusArea == FocusArea.PrepareSlots && i == selectedPreparedSlotIndex);
        }
    }

    private void SetPreparedRecipeFocus(int index)
    {
        if (preparedRecipeRows.Count == 0)
        {
            selectedPreparedRecipeIndex = -1;
            armedPreparedRecipeIndex = -1;
            return;
        }

        index = (index % preparedRecipeRows.Count + preparedRecipeRows.Count) % preparedRecipeRows.Count;
        selectedPreparedRecipeIndex = index;
        focusArea = FocusArea.PrepareList;
        if (index < preparedRows.Count)
            SelectInEventSystem(preparedRows[index].gameObject);
        if (index < preparedRows.Count)
            preparedMagicListScroll?.EnsureVisible(preparedRows[index].transform as RectTransform);
        RefreshPreparedSlots();
    }

    private void HandlePreparedRecipeClicked(int index)
    {
        if (!IsOpen || !isInteractive || index < 0 || index >= preparedRecipeRows.Count)
            return;

        SetPreparedRecipeFocus(index);
        armedPreparedRecipeIndex = index;
        RefreshPreparedSlots();
        FocusPreparedSlots();
    }

    private void MovePreparedRecipeFocus(int direction)
    {
        if (preparedRecipeRows.Count > 0)
            SetPreparedRecipeFocus(selectedPreparedRecipeIndex + direction);
    }

    private void FocusPreparedList()
    {
        // Returning to the list exits placement mode, so no previous choice
        // can be applied after further navigation.
        armedPreparedRecipeIndex = -1;
        preparedSlotFocusEnteredFrame = -1;
        focusArea = FocusArea.PrepareList;
        SetPreparedRecipeFocus(selectedPreparedRecipeIndex < 0 ? 0 : selectedPreparedRecipeIndex);
    }

    private void FocusPreparedSlots()
    {
        if (ArmedPreparedRecipe == null || equipSlots.Count == 0)
            return;
        preparedSlotFocusEnteredFrame = Time.frameCount;
        focusArea = FocusArea.PrepareSlots;
        SetPreparedSlotFocus(selectedPreparedSlotIndex < 0 ? 0 : selectedPreparedSlotIndex);
    }

    private void SetPreparedSlotFocus(int index, bool updateEventSelection = true)
    {
        int capacity = GetRunMagicCapacity();
        if (capacity <= 0)
            return;

        selectedPreparedSlotIndex = (index % capacity + capacity) % capacity;
        focusArea = FocusArea.PrepareSlots;
        for (int i = 0; i < equipSlots.Count; i++)
            if (equipSlots[i] != null)
                equipSlots[i].SetFocused(i == selectedPreparedSlotIndex);
        if (updateEventSelection && selectedPreparedSlotIndex < equipSlots.Count && equipSlots[selectedPreparedSlotIndex] != null)
            SelectInEventSystem(equipSlots[selectedPreparedSlotIndex].gameObject);
    }

    private static void SelectInEventSystem(GameObject target)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || target == null || eventSystem.alreadySelecting
            || eventSystem.currentSelectedGameObject == target)
            return;

        eventSystem.SetSelectedGameObject(target);
    }

    private void SetRecipeFocus(int index)
    {
        if (visibleRecipes.Count == 0) { selectedRecipeIndex = -1; RefreshSelectedRecipe(); return; }
        selectedRecipeIndex = (index % visibleRecipes.Count + visibleRecipes.Count) % visibleRecipes.Count;
        focusArea = FocusArea.LearnList;
        actionFocusEnteredFrame = -1;
        RefreshSelectedRecipe();
        if (EventSystem.current != null && selectedRecipeIndex < recipeRows.Count)
            EventSystem.current.SetSelectedGameObject(recipeRows[selectedRecipeIndex].gameObject);
        if (selectedRecipeIndex < recipeRows.Count)
            recipeListScroll?.EnsureVisible(recipeRows[selectedRecipeIndex].transform as RectTransform);
    }

    private void MoveRecipeFocus(int direction)
    {
        if (visibleRecipes.Count == 0) return;
        SetRecipeFocus(selectedRecipeIndex + direction);
    }

    private void RefreshSelectedRecipe()
    {
        MagicRecipeData recipe = SelectedRecipe;
        MagicItemData magic = recipe != null ? recipe.resultMagic : null;
        if (detailRoot != null) detailRoot.SetActive(magic != null);
        if (detailImage != null)
        {
            detailImage.sprite = magic != null ? magic.icon : null;
            detailImage.enabled = magic != null && magic.icon != null;
            detailImage.preserveAspect = true;
        }
        if (detailTitle != null)
            detailTitle.text = magic != null ? GetRecipeName(recipe) : string.Empty;
        if (scalingText != null) scalingText.text = magic != null ? magic.scaling : string.Empty;
        if (requirementsText != null) requirementsText.text = magic != null ? magic.GetRequirementsLabel() : string.Empty;
        bool attack = magic != null && magic.IsVisualCategory(MagicItemData.MagicCategory.Attack);
        bool boost = magic != null && magic.IsVisualCategory(MagicItemData.MagicCategory.Boost);
        bool healing = magic != null && magic.IsVisualCategory(MagicItemData.MagicCategory.Healing);
        if (attackRoot != null) attackRoot.SetActive(attack);
        if (boostRoot != null) boostRoot.SetActive(boost);
        if (healingRoot != null) healingRoot.SetActive(healing);
        if (damageText != null) damageText.text = attack ? magic.magicDamage.ToString() : string.Empty;
        if (criticalText != null) criticalText.text = attack ? MagicItemData.FormatCompact(magic.criticalHit) : string.Empty;
        if (attackManaCostText != null) attackManaCostText.text = attack ? MagicItemData.FormatCompact(magic.manaCost) : string.Empty;
        if (boostAttributeText != null) boostAttributeText.text = boost ? MagicItemData.FormatBoostAttribute(magic.boostAttribute) : string.Empty;
        if (boostAmountText != null) boostAmountText.text = boost ? MagicItemData.FormatSignedAmount(magic.boostAmount) : string.Empty;
        if (boostDurationText != null) boostDurationText.text = boost ? MagicItemData.FormatDuration(magic.boostDurationSeconds) : string.Empty;
        if (boostManaCostText != null) boostManaCostText.text = boost ? MagicItemData.FormatCompact(magic.manaCost) : string.Empty;
        if (healingTypeText != null) healingTypeText.text = healing ? MagicItemData.FormatHealingType(magic.effectType) : string.Empty;
        if (healingAmountText != null) healingAmountText.text = healing ? magic.healAmount.ToString() : string.Empty;
        if (healingManaCostText != null) healingManaCostText.text = healing ? MagicItemData.FormatCompact(magic.manaCost) : string.Empty;
        RefreshRequirementsAndAction();
    }

    private void RefreshRequirementsAndAction()
    {
        ClearMaterialRows();
        MagicRecipeData recipe = SelectedRecipe;
        bool learned = recipe != null && playerStats != null && playerStats.KnowsMagicRecipe(recipe.recipeId);
        if (learned)
        {
            if (priceRoot != null) priceRoot.SetActive(false);
            if (priceText != null) priceText.text = string.Empty;
            if (actionButtonLabel != null) actionButtonLabel.text = "GIÀ IMPARATA";
            if (actionButton != null) actionButton.interactable = false;
        }
        else
        {
            MagicLearnCheck check = CanLearnMagic(recipe);
            if (priceRoot != null) priceRoot.SetActive(true);
            if (priceText != null) priceText.text = recipe != null ? recipe.learnCoinCost.ToString() : string.Empty;
            CreateMaterialRows(check.Materials);
            if (actionButtonLabel != null) actionButtonLabel.text = "STUDIA";
            if (actionButton != null) actionButton.interactable = check.IsValid;
        }
    }

    private void CreateMaterialRows(List<MagicRequirementStatus> statuses)
    {
        if (materialsRoot == null || materialRowPrefab == null || statuses == null) return;
        for (int i = 0; i < statuses.Count; i++)
        {
            MagicRequirementStatus status = statuses[i];
            QuestRewardItemUI row = Instantiate(materialRowPrefab, materialsRoot, false);
            row.SetRequirementData(status.Item.icon, status.Item.itemName, status.Owned, status.Required);
            materialRows.Add(row);
        }
    }

    private void OnActionButtonClicked()
    {
        if (!IsOpen || !isInteractive || Time.frameCount == openingFrame || Time.frameCount == lastActionActivationFrame) return;
        lastActionActivationFrame = Time.frameCount;
        MagicRecipeData recipe = SelectedRecipe;
        if (recipe == null) return;
        if (!playerStats.KnowsMagicRecipe(recipe.recipeId))
            TryLearnMagic(recipe, out _);
        RefreshRecipeList();
        if (visibleRecipes.Count > 0) SetRecipeFocus(Mathf.Clamp(selectedRecipeIndex, 0, visibleRecipes.Count - 1));
    }

    private void OnConfirmPerformed(InputAction.CallbackContext _)
    {
        if (!IsOpen || !isInteractive || Time.frameCount == openingFrame) return;
        if (focusArea == FocusArea.LearnAction)
        {
            if (actionButton != null && actionButton.interactable) OnActionButtonClicked();
            return;
        }

        if (focusArea == FocusArea.PrepareList)
        {
            if (selectedPreparedRecipeIndex < 0 || selectedPreparedRecipeIndex >= preparedRecipeRows.Count)
                return;

            armedPreparedRecipeIndex = selectedPreparedRecipeIndex;
            RefreshPreparedSlots();
            FocusPreparedSlots();
            return;
        }

        if (focusArea == FocusArea.PrepareSlots)
        {
            ApplyPreparedSlotSelection(selectedPreparedSlotIndex);
            return;
        }

        if (actionButton != null && actionButton.gameObject.activeInHierarchy)
        {
            focusArea = FocusArea.LearnAction;
            actionFocusEnteredFrame = Time.frameCount;
            EventSystem.current?.SetSelectedGameObject(actionButton.gameObject);
        }
    }

    private void OnCancelPerformed(InputAction.CallbackContext _)
    {
        if (!IsOpen || !isInteractive) return;
        if (focusArea == FocusArea.LearnAction && Time.frameCount != actionFocusEnteredFrame)
        {
            SetRecipeFocus(selectedRecipeIndex);
            return;
        }

        if (focusArea == FocusArea.PrepareSlots)
        {
            armedPreparedRecipeIndex = -1;
            FocusPreparedList();
            return;
        }

        CloseMagic();
    }

    public void HandleSlotPointerDown(int index)
    {
        if (IsOpen && isInteractive && currentView == MagicView.Prepare)
            ApplyPreparedSlotSelection(index);
    }

    public void HandleSlotBeginDrag(int index, PointerEventData eventData) { }
    public void HandleSlotDrag(PointerEventData eventData) { }
    public void HandleSlotEndDrag() { }
    public void HandleSlotDrop(int targetIndex) { }

    public void HandleSlotSelected(int index)
    {
        if (IsOpen && isInteractive && currentView == MagicView.Prepare)
            SetPreparedSlotFocus(index, updateEventSelection: false);
    }

    public void HandleSlotSubmit(int index)
    {
        if (IsOpen && isInteractive && currentView == MagicView.Prepare)
            ApplyPreparedSlotSelection(index);
    }

    private System.Collections.IEnumerator PlayContentAppearAnimation()
    {
        if (contentAppearDelay > 0f)
            yield return new WaitForSecondsRealtime(contentAppearDelay);
        if (!IsOpen || isClosing)
            yield break;

        if (contentAppearAnimator != null && !string.IsNullOrWhiteSpace(contentAppearStateName))
        {
            GameObject animationObject = contentAppearAnimator.gameObject;
            animationObject.SetActive(true);
            contentAppearAnimator.enabled = true;
            contentAppearAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            contentAppearAnimator.Play(contentAppearStateName, 0, 0f);
            contentAppearAnimator.Update(0f);
        }

        // The transition is sampled before activating the actual UI content,
        // preventing a one-frame flash of the final layout.
        ShowContentGroup();

        if (contentAppearAnimator != null && !string.IsNullOrWhiteSpace(contentAppearStateName))
        {
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
            FocusCurrentView();
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
        }

        HideContentGroup();
        DeactivateContentRoots();
        HideContentAppearAnimation();
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
        ClearRows();
        ClearPreparedRows();
        armedPreparedRecipeIndex = -1;
        if (magicHud != null)
            magicHud.SetActive(false);
        isClosing = false;
        isInteractive = false;
        closeRoutine = null;
        Closed?.Invoke();
        if (playerController != null)
            playerController.ReleaseGameplayInputLock(gameplayLockOwner);
        controls = null;
        openingFrame = -1;
        preparedSlotFocusEnteredFrame = -1;
    }

    private void StopContentAppearRoutineOnly()
    {
        if (contentAppearRoutine != null)
            StopCoroutine(contentAppearRoutine);
        contentAppearRoutine = null;
    }

    private void HideContentAppearAnimation()
    {
        if (contentAppearAnimator == null)
            return;
        contentAppearAnimator.enabled = false;
        contentAppearAnimator.gameObject.SetActive(false);
    }

    private void HideContentGroup()
    {
        SetContentGroupState(GetActiveContentGroup(), 0f, false);
    }

    private void HideAllContentGroups()
    {
        SetContentGroupState(learnContentGroup, 0f, false);
        SetContentGroupState(equipContentGroup, 0f, false);
    }

    private void DeactivateContentRoots()
    {
        if (EventSystem.current != null && IsOwnedSelection(EventSystem.current.currentSelectedGameObject))
            EventSystem.current.SetSelectedGameObject(null);
        if (learnRoot != null)
            learnRoot.SetActive(false);
        if (equipRoot != null)
            equipRoot.SetActive(false);
    }

    private void RestartBookOpenAnimation()
    {
        if (bookAnimator == null || string.IsNullOrWhiteSpace(bookOpenStateName))
            return;
        bookAnimator.enabled = true;
        bookAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        bookAnimator.Play(bookOpenStateName, 0, 0f);
        bookAnimator.Update(0f);
    }

    private void ShowContentGroup()
    {
        GameObject activeRoot = GetActiveContentRoot();
        if (activeRoot != null)
            activeRoot.SetActive(true);
        SetContentGroupState(GetActiveContentGroup(), 1f, false);
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
        return currentView == MagicView.Prepare ? equipContentGroup : learnContentGroup;
    }

    private GameObject GetActiveContentRoot()
    {
        return currentView == MagicView.Prepare ? equipRoot : learnRoot;
    }

    private static void SetContentGroupState(CanvasGroup group, float alpha, bool interactive)
    {
        if (group == null)
            return;
        group.alpha = alpha;
        group.interactable = interactive;
        group.blocksRaycasts = interactive;
    }

    private void FocusCurrentView()
    {
        if (currentView == MagicView.Prepare)
            FocusPreparedList();
        else if (visibleRecipes.Count > 0)
            SetRecipeFocus(Mathf.Clamp(selectedRecipeIndex, 0, visibleRecipes.Count - 1));
    }

    private void ApplyPreparedSlotSelection(int slotIndex)
    {
        // The input that confirms/clicks a recipe may also be delivered by the
        // EventSystem to the slot selected during that same frame. Entering the
        // grid must only move focus; placement requires a subsequent input.
        if (preparedSlotFocusEnteredFrame == Time.frameCount
            || lastPreparedSlotActivationFrame == Time.frameCount)
            return;

        int capacity = GetRunMagicCapacity();
        if (slotIndex < 0 || slotIndex >= capacity)
            return;

        MagicRecipeData recipe = ArmedPreparedRecipe;
        if (recipe == null)
            return;

        lastPreparedSlotActivationFrame = Time.frameCount;
        SetPreparedSlotFocus(slotIndex);
        if (!SetRunMagicAtSlot(slotIndex, recipe))
            return;

        armedPreparedRecipeIndex = -1;
        RefreshPreparedMagicState();
        FocusPreparedList();
    }

    private void SubscribeInput()
    {
        controls.Player.Jump.performed -= confirmCallback;
        controls.Player.Jump.performed += confirmCallback;
        controls.Player.SprintOrDodge.performed -= cancelCallback;
        controls.Player.SprintOrDodge.performed += cancelCallback;
    }

    private void UnsubscribeInput()
    {
        if (controls == null) return;
        controls.Player.Jump.performed -= confirmCallback;
        controls.Player.SprintOrDodge.performed -= cancelCallback;
    }

    private void ClearRows()
    {
        ClearMaterialRows();
        for (int i = 0; i < recipeRows.Count; i++)
            if (recipeRows[i] != null)
            {
                recipeRows[i].gameObject.SetActive(false);
                Destroy(recipeRows[i].gameObject);
            }
        recipeRows.Clear();
        recipeListScroll?.Refresh(false);
    }

    private void ClearPreparedRows()
    {
        if (EventSystem.current != null)
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            for (int i = 0; i < preparedRows.Count; i++)
            {
                if (preparedRows[i] != null && selected == preparedRows[i].gameObject)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                    break;
                }
            }
        }

        for (int i = 0; i < preparedRows.Count; i++)
            if (preparedRows[i] != null)
            {
                preparedRows[i].gameObject.SetActive(false);
                Destroy(preparedRows[i].gameObject);
            }
        preparedRows.Clear();
        preparedMagicListScroll?.Refresh(false);
    }

    private void ClearMaterialRows()
    {
        for (int i = 0; i < materialRows.Count; i++)
            if (materialRows[i] != null) Destroy(materialRows[i].gameObject);
        materialRows.Clear();
    }

    private bool IsOwnedSelection(GameObject selected)
    {
        if (selected == null) return false;
        if (actionButton != null && selected == actionButton.gameObject) return true;
        for (int i = 0; i < recipeRows.Count; i++)
            if (recipeRows[i] != null && selected == recipeRows[i].gameObject) return true;
        for (int i = 0; i < preparedRows.Count; i++)
            if (preparedRows[i] != null && selected == preparedRows[i].gameObject) return true;
        for (int i = 0; i < equipSlots.Count; i++)
            if (equipSlots[i] != null && selected == equipSlots[i].gameObject) return true;
        return false;
    }

    private static string GetRecipeName(MagicRecipeData recipe)
    {
        return recipe != null && recipe.resultMagic != null
            ? (!string.IsNullOrWhiteSpace(recipe.resultMagic.magicName) ? recipe.resultMagic.magicName : recipe.resultMagic.name)
            : string.Empty;
    }
}
