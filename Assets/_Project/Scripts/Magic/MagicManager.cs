using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class MagicManager : MonoBehaviour
{
    private enum FocusArea { List, Action }

    [Header("Magic UI")]
    [SerializeField] private GameObject magicHud;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private PlayerStats playerStats;

    [Header("Recipe List")]
    [SerializeField] private Transform recipeListRoot;
    [SerializeField] private DialogueChoiceUI recipeRowPrefab;
    [SerializeField] private List<MagicRecipeData> recipes = new List<MagicRecipeData>();

    [Header("Detail")]
    [SerializeField] private GameObject detailRoot;
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

    private readonly List<MagicRecipeData> visibleRecipes = new List<MagicRecipeData>();
    private readonly List<DialogueChoiceUI> recipeRows = new List<DialogueChoiceUI>();
    private readonly List<QuestRewardItemUI> materialRows = new List<QuestRewardItemUI>();
    private readonly object gameplayLockOwner = new object();
    private PlayerControls controls;
    private Action<InputAction.CallbackContext> confirmCallback;
    private Action<InputAction.CallbackContext> cancelCallback;
    private int selectedRecipeIndex = -1;
    private int openingFrame = -1;
    private int lastActionActivationFrame = -1;
    private int actionFocusEnteredFrame = -1;
    private float lastNavigationTime = -999f;
    private FocusArea focusArea = FocusArea.List;
    private bool isInteractive;
    private bool isClosing;

    public bool IsOpen { get; private set; }
    public NpcServiceContext ActiveContext { get; private set; }
    public IReadOnlyList<MagicRecipeData> Recipes => recipes;
    public event Action Closed;

    private MagicRecipeData SelectedRecipe => selectedRecipeIndex >= 0 && selectedRecipeIndex < visibleRecipes.Count
        ? visibleRecipes[selectedRecipeIndex] : null;

    private void Awake()
    {
        confirmCallback = OnConfirmPerformed;
        cancelCallback = OnCancelPerformed;
        if (magicHud != null) magicHud.SetActive(false);
        if (actionButton != null) actionButton.onClick.AddListener(OnActionButtonClicked);
    }

    private void OnDestroy()
    {
        if (actionButton != null) actionButton.onClick.RemoveListener(OnActionButtonClicked);
    }

    private void OnDisable()
    {
        UnsubscribeInput();
        if (playerController != null) playerController.ReleaseGameplayInputLock(gameplayLockOwner);
        IsOpen = false;
        isInteractive = false;
        isClosing = false;
        ActiveContext = null;
        ClearRows();
        if (magicHud != null) magicHud.SetActive(false);
    }

    public bool OpenMagic(NpcServiceContext context)
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
        focusArea = FocusArea.List;
        openingFrame = Time.frameCount;
        playerController.AcquireGameplayInputLock(gameplayLockOwner);
        SubscribeInput();
        if (magicHud != null) magicHud.SetActive(true);
        RefreshRecipeList();
        isInteractive = true;
        return true;
    }

    public void CloseMagic()
    {
        if (!IsOpen || isClosing) return;
        IsOpen = false;
        isInteractive = false;
        isClosing = true;
        UnsubscribeInput();
        if (EventSystem.current != null && IsOwnedSelection(EventSystem.current.currentSelectedGameObject))
            EventSystem.current.SetSelectedGameObject(null);
        ClearRows();
        if (magicHud != null) magicHud.SetActive(false);
        if (playerController != null) playerController.ReleaseGameplayInputLock(gameplayLockOwner);
        ActiveContext = null;
        isClosing = false;
        Closed?.Invoke();
    }

    private void Update()
    {
        if (!IsOpen || !isInteractive || controls == null || Time.unscaledTime < lastNavigationTime + 0.20f) return;
        Vector2 move = controls.Player.Move.ReadValue<Vector2>();
        if (Mathf.Abs(move.y) <= 0.5f) return;
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
        if (playerStats == null || recipes == null) return result;
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < recipes.Count; i++)
        {
            MagicRecipeData recipe = recipes[i];
            if (recipe == null || recipe.resultMagic == null || string.IsNullOrWhiteSpace(recipe.recipeId)
                || !playerStats.KnowsMagicRecipe(recipe.recipeId) || !ids.Add(recipe.recipeId.Trim()))
                continue;
            result.Add(recipe.resultMagic);
        }
        return result;
    }

    public bool SetRunMagicAtSlot(int slot, MagicRecipeData recipe)
    {
        return recipe != null && playerStats != null && recipe.resultMagic != null
            && playerStats.KnowsMagicRecipe(recipe.recipeId)
            && playerStats.SetRunMagicAtSlot(slot, recipe.recipeId);
    }

    public bool RemoveRunMagicAtSlot(int slot) => playerStats != null && playerStats.RemoveRunMagicAtSlot(slot);
    public string[] GetRunMagicSelection() => playerStats != null ? playerStats.GetRunMagicSelection() : Array.Empty<string>();
    public void ClearRunMagicSelection() => playerStats?.ClearRunMagicSelection();

    public bool CanConvertMagicToBlueprintFragment(string instanceId)
    {
        if (playerInventory == null || playerStats == null || !playerInventory.TryGetItemByInstanceId(instanceId, out InventoryItem item)
            || item == null || item.magicData == null)
            return false;
        MagicRecipeData recipe = FindRecipeForMagic(item.magicData);
        return recipe != null && recipe.unlockType == MagicRecipeUnlockType.Blueprint
            && !playerStats.IsMagicRecipeUnlocked(recipe.recipeId)
            && !playerStats.KnowsMagicRecipe(recipe.recipeId)
            && playerStats.GetMagicBlueprintFragments(recipe.recipeId) < recipe.blueprintFragmentsRequired;
    }

    public bool TryConvertMagicToBlueprintFragment(string instanceId)
    {
        if (!CanConvertMagicToBlueprintFragment(instanceId)) return false;
        playerInventory.TryGetItemByInstanceId(instanceId, out InventoryItem item);
        MagicRecipeData recipe = FindRecipeForMagic(item.magicData);
        if (!playerInventory.TryRemoveInstance(instanceId, 1, out int remaining, save: false)) return false;

        if (playerStats.TryAddMagicBlueprintFragment(recipe.recipeId, recipe.blueprintFragmentsRequired, save: false))
        {
            playerStats.SaveStatsImmediate();
            return true;
        }

        if (remaining > 0)
            playerInventory.TryAdjustInstanceAmount(instanceId, 1, out _, save: false);
        else
        {
            InventoryItem restored = new InventoryItem(item.magicData, 1);
            restored.instanceId = instanceId;
            playerInventory.TryAddItemInstance(restored, save: false);
        }
        return false;
    }

    private MagicRecipeData FindRecipeForMagic(MagicItemData magic)
    {
        if (magic == null || recipes == null) return null;
        for (int i = 0; i < recipes.Count; i++)
            if (recipes[i] != null && recipes[i].resultMagic == magic)
                return recipes[i];
        return null;
    }

    private bool IsRecipeAvailable(MagicRecipeData recipe)
    {
        return recipe != null && recipe.resultMagic != null && !string.IsNullOrWhiteSpace(recipe.recipeId)
               && (recipe.unlockType == MagicRecipeUnlockType.Default
                   || (playerStats != null && playerStats.IsMagicRecipeUnlocked(recipe.recipeId)));
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
        for (int i = 0; i < recipes.Count; i++)
        {
            MagicRecipeData recipe = recipes[i];
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
        RefreshSelectedRecipe();
    }

    private bool IsRecipeVisible(MagicRecipeData recipe)
    {
        return recipe != null && recipe.resultMagic != null && !string.IsNullOrWhiteSpace(recipe.recipeId) && IsRecipeAvailable(recipe);
    }

    private void SetRecipeFocus(int index)
    {
        if (visibleRecipes.Count == 0) { selectedRecipeIndex = -1; RefreshSelectedRecipe(); return; }
        selectedRecipeIndex = (index % visibleRecipes.Count + visibleRecipes.Count) % visibleRecipes.Count;
        focusArea = FocusArea.List;
        actionFocusEnteredFrame = -1;
        RefreshSelectedRecipe();
        if (EventSystem.current != null && selectedRecipeIndex < recipeRows.Count)
            EventSystem.current.SetSelectedGameObject(recipeRows[selectedRecipeIndex].gameObject);
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
        if (!IsOpen || Time.frameCount == openingFrame || Time.frameCount == lastActionActivationFrame) return;
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
        if (focusArea == FocusArea.Action)
        {
            if (actionButton != null && actionButton.interactable) OnActionButtonClicked();
            return;
        }
        if (actionButton != null && actionButton.gameObject.activeInHierarchy)
        {
            focusArea = FocusArea.Action;
            actionFocusEnteredFrame = Time.frameCount;
            EventSystem.current?.SetSelectedGameObject(actionButton.gameObject);
        }
    }

    private void OnCancelPerformed(InputAction.CallbackContext _)
    {
        if (!IsOpen || !isInteractive) return;
        if (focusArea == FocusArea.Action && Time.frameCount != actionFocusEnteredFrame)
        {
            SetRecipeFocus(selectedRecipeIndex);
            return;
        }
        CloseMagic();
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
            if (recipeRows[i] != null) Destroy(recipeRows[i].gameObject);
        recipeRows.Clear();
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
        return false;
    }

    private static string GetRecipeName(MagicRecipeData recipe)
    {
        return recipe != null && recipe.resultMagic != null
            ? (!string.IsNullOrWhiteSpace(recipe.resultMagic.magicName) ? recipe.resultMagic.magicName : recipe.resultMagic.name)
            : string.Empty;
    }
}
