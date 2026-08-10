using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public sealed class BlacksmithManager : MonoBehaviour, IInventorySlotHandler
{
    [Header("Blacksmith UI")]
    [SerializeField] private GameObject blacksmithHud;
    [SerializeField] private GameObject initialFocus;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private TextMeshProUGUI playerCoinsText;
    [SerializeField] private Animator bookAnimator;
    [SerializeField] private Animator contentAppearAnimator;
    [SerializeField] private CanvasGroup contentGroup;
    [SerializeField] private InventorySlot slotPrefab;
    [SerializeField] private Transform slotParent;
    [SerializeField] private GridLayoutGroup slotGrid;
    [SerializeField, Min(0)] private int initialSlotCount = 30;

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
    public event Action Closed;

    private readonly List<InventorySlot> blacksmithSlots = new List<InventorySlot>();
    private readonly List<InventoryItem> upgradeItems = new List<InventoryItem>();
    private int selectedUpgradeIndex = -1;
    private Coroutine contentAppearRoutine;
    private Coroutine closeRoutine;
    private bool isClosing;

    private void Awake()
    {
        if (blacksmithHud != null)
            blacksmithHud.SetActive(false);
        EnsureBlacksmithSlots();
        HideContentAppearAnimation();
        HideContentGroup();
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
        playerInventory = context.PlayerInventory;
        playerStats = context.PlayerStats;
        IsOpen = true;
        isClosing = false;
        RefreshBlacksmithGrid();
        if (blacksmithHud != null)
            blacksmithHud.SetActive(true);
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
        StopContentAppearRoutineOnly();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        closeRoutine = StartCoroutine(RunCloseAnimations());
    }

    private void OnDisable()
    {
        StopContentAppearRoutineOnly();
        if (closeRoutine != null)
            StopCoroutine(closeRoutine);
        closeRoutine = null;
        isClosing = false;
        if (blacksmithHud != null)
            blacksmithHud.SetActive(false);
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
        if (playerInventory != null && CurrentMode == BlacksmithMode.Upgrade)
        {
            IReadOnlyList<InventoryItem> inventoryItems = playerInventory.Items;
            for (int i = 0; i < inventoryItems.Count; i++)
            {
                InventoryItem item = inventoryItems[i];
                if (item != null && item.weaponData != null && item.weaponData.canUpgrade
                    && item.weaponData.category != WeaponCategory.Unarmed)
                    upgradeItems.Add(item);
            }
        }

        for (int i = 0; i < blacksmithSlots.Count; i++)
        {
            InventoryItem item = i < upgradeItems.Count ? upgradeItems[i] : null;
            if (item != null)
                blacksmithSlots[i].Setup(GetItemIcon(item), item.amount, playerInventory != null && playerInventory.IsInstanceEquipped(item.instanceId));
            else
                blacksmithSlots[i].Clear();
        }

        SetBlacksmithFocus(upgradeItems.Count > 0 ? 0 : -1);
        RefreshPlayerCoins();
    }

    public InventoryItem SelectedUpgradeItem => selectedUpgradeIndex >= 0 && selectedUpgradeIndex < upgradeItems.Count
        ? upgradeItems[selectedUpgradeIndex]
        : null;

    public void HandleSlotPointerDown(int index) { SelectUpgradeItem(index); }
    public void HandleSlotBeginDrag(int index, PointerEventData eventData) { }
    public void HandleSlotDrag(PointerEventData eventData) { }
    public void HandleSlotEndDrag() { }
    public void HandleSlotDrop(int targetIndex) { }
    public void HandleSlotSelected(int index) { SelectUpgradeItem(index); }
    public void HandleSlotSubmit(int index) { SelectUpgradeItem(index); }

    private void SelectUpgradeItem(int index)
    {
        if (index < 0 || index >= upgradeItems.Count)
            return;
        SetBlacksmithFocus(index);
    }

    private void SetBlacksmithFocus(int index)
    {
        selectedUpgradeIndex = index >= 0 && index < upgradeItems.Count ? index : -1;
        for (int i = 0; i < blacksmithSlots.Count; i++)
            blacksmithSlots[i].SetFocused(i == selectedUpgradeIndex);

        if (EventSystem.current != null && selectedUpgradeIndex >= 0
            && selectedUpgradeIndex < blacksmithSlots.Count)
            EventSystem.current.SetSelectedGameObject(blacksmithSlots[selectedUpgradeIndex].gameObject);
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

    private void RefreshPlayerCoins()
    {
        if (playerCoinsText != null)
            playerCoinsText.text = playerStats != null ? Mathf.Max(0, playerStats.runCoins).ToString() : "0";
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
            SetContentInteraction(true);
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
        closeRoutine = null;
        Closed?.Invoke();
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
