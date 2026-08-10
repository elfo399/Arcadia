using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public enum ShopMode
{
    Buy,
    Sell
}

public sealed class ShopManager : MonoBehaviour, IInventorySlotHandler
{
    private enum FocusArea { Grid, Action }

    [Header("Scene References")]
    [SerializeField] private GameObject shopHud;
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
    [SerializeField] [Min(0)] private int initialSlotCount = 30;
    private MerchantData currentMerchant;

    [Header("Initial State")]
    [SerializeField] private ShopMode initialMode = ShopMode.Buy;
    [SerializeField] private string contentAppearStateName = "Transition";
    [SerializeField] [Min(0f)] private float contentAppearDelay = 0.5833333f;
    [SerializeField] [Min(0f)] private float contentAppearDuration = 1.8f;
    [SerializeField] private string bookCloseStateName = "CloseBook";
    [SerializeField] [Min(0f)] private float bookCloseDuration = 0.6666666f;

    private readonly object gameplayLockOwner = new object();
    private PlayerControls controls;
    private Action<InputAction.CallbackContext> confirmCallback;
    private Action<InputAction.CallbackContext> cancelCallback;
    private int openingFrame = -1;
    private Coroutine contentAppearRoutine;
    private Coroutine closeRoutine;
    private bool isClosing;
    private bool isInteractive;
    private readonly List<InventorySlot> shopSlots = new List<InventorySlot>();
    private readonly List<InventoryItem> shopItems = new List<InventoryItem>();
    private readonly List<MerchantData.StockEntry> buyEntries = new List<MerchantData.StockEntry>();
    private readonly Dictionary<MerchantData.StockEntry, int> remainingStock = new Dictionary<MerchantData.StockEntry, int>();
    private bool stockLoaded;
    [SerializeField] private GameObject weaponSection;
    [SerializeField] private GameObject shieldSection;
    [SerializeField] private GameObject armorSection;
    [SerializeField] private GameObject itemSection;
    [SerializeField] private GameObject commonTitle;
    [SerializeField] private GameObject commonImage;
    [SerializeField] private Button weaponBuyButton;
    [SerializeField] private Button weaponSellButton;
    [SerializeField] private Button shieldBuyButton;
    [SerializeField] private Button shieldSellButton;
    [SerializeField] private Button armorBuyButton;
    [SerializeField] private Button armorSellButton;
    [SerializeField] private Button itemBuyButton;
    [SerializeField] private Button itemSellButton;
    [SerializeField] private SegmentedButtonSelectionUI weaponBuySelection;
    [SerializeField] private SegmentedButtonSelectionUI weaponSellSelection;
    [SerializeField] private SegmentedButtonSelectionUI shieldBuySelection;
    [SerializeField] private SegmentedButtonSelectionUI shieldSellSelection;
    [SerializeField] private SegmentedButtonSelectionUI armorBuySelection;
    [SerializeField] private SegmentedButtonSelectionUI armorSellSelection;
    [SerializeField] private SegmentedButtonSelectionUI itemBuySelection;
    [SerializeField] private SegmentedButtonSelectionUI itemSellSelection;

    [Header("Detail Content")]
    [SerializeField] private Image detailImage;
    [SerializeField] private TextMeshProUGUI detailTitle;
    [Header("Prices")]
    [SerializeField] private TextMeshProUGUI weaponBuyPriceText;
    [SerializeField] private TextMeshProUGUI weaponSellPriceText;
    [SerializeField] private TextMeshProUGUI shieldBuyPriceText;
    [SerializeField] private TextMeshProUGUI shieldSellPriceText;
    [SerializeField] private TextMeshProUGUI armorBuyPriceText;
    [SerializeField] private TextMeshProUGUI armorSellPriceText;
    [SerializeField] private TextMeshProUGUI itemBuyPriceText;
    [SerializeField] private TextMeshProUGUI itemSellPriceText;
    [SerializeField] private TextMeshProUGUI weaponDescription;
    [SerializeField] private TextMeshProUGUI weaponDamageText;
    [SerializeField] private TextMeshProUGUI weaponCriticalText;
    [SerializeField] private TextMeshProUGUI weaponWeightText;
    [SerializeField] private TextMeshProUGUI weaponScalingText;
    [SerializeField] private TextMeshProUGUI weaponRequirementsText;
    [SerializeField] private TextMeshProUGUI shieldDescription;
    [SerializeField] private TextMeshProUGUI shieldDamageText;
    [SerializeField] private TextMeshProUGUI shieldCriticalText;
    [SerializeField] private TextMeshProUGUI shieldWeightText;
    [SerializeField] private TextMeshProUGUI shieldScalingText;
    [SerializeField] private TextMeshProUGUI shieldRequirementsText;
    [SerializeField] private TextMeshProUGUI shieldPhysicalDefenseText;
    [SerializeField] private TextMeshProUGUI shieldMagicDefenseText;
    [SerializeField] private TextMeshProUGUI armorDescription;
    [SerializeField] private TextMeshProUGUI armorWeightText;
    [SerializeField] private TextMeshProUGUI armorPhysicalDefenseText;
    [SerializeField] private TextMeshProUGUI armorMagicDefenseText;
    [SerializeField] private TextMeshProUGUI itemDescription;
    private int shopFocusIndex;
    private FocusArea focusArea = FocusArea.Grid;
    private SegmentedButtonSelectionUI activeActionSelection;
    private float lastNavigationTime = -999f;
    private const float NavigationRepeatCooldown = 0.20f;

    public bool IsOpen { get; private set; }
    public ShopMode CurrentMode { get; private set; }

    public event Action<ShopMode> ConfirmRequested;
    public event Action Closed;

    private void Awake()
    {
        confirmCallback = OnConfirmPerformed;
        cancelCallback = OnCancelPerformed;
        PlayerStats.MerchantStockSnapshotProvider = CreateMerchantStockSave;

        if (shopHud != null)
            shopHud.SetActive(false);
        EnsureShopSlots();
        HideContentAppearAnimation();
        HideContentGroup();
    }

    private void EnsureShopSlots()
    {
        if (slotPrefab == null || slotParent == null || initialSlotCount <= 0)
            return;

        shopSlots.Clear();
        for (int i = 0; i < initialSlotCount; i++)
        {
            InventorySlot slot = Instantiate(slotPrefab, slotParent);
            slot.name = $"InvSlot_{i:00}";
            slot.SetDisplayOnly(false);
            slot.Init(i, this);
            slot.Clear();
            slot.SetFocused(false);
            shopSlots.Add(slot);
        }
    }

    public void SetShopItems(IReadOnlyList<InventoryItem> items)
    {
        if (!ReferenceEquals(items, shopItems))
        {
            shopItems.Clear();
            if (items != null) shopItems.AddRange(items);
        }
        for (int i = 0; i < shopSlots.Count; i++)
        {
            InventoryItem item = i < shopItems.Count ? shopItems[i] : null;
            if (item != null)
            {
                bool equipped = CurrentMode == ShopMode.Sell
                    && playerInventory != null
                    && playerInventory.IsInstanceEquipped(item.instanceId);
                shopSlots[i].Setup(GetItemIcon(item), item.amount, equipped);
            }
            else shopSlots[i].Clear();
        }
        SetShopFocus(0);
    }

    private void ShowSelectedItem(int index)
    {
        HideDetailSections();
        if (index < 0 || index >= shopItems.Count || shopItems[index] == null) { RefreshSelectedPrice(null); return; }
        InventoryItem item = shopItems[index];
        RefreshSelectedPrice(GetItemAsset(item));
        Sprite icon = GetItemIcon(item);
        if (detailImage != null)
        {
            detailImage.sprite = icon;
            detailImage.enabled = icon != null;
            detailImage.preserveAspect = true;
        }
        if (detailTitle != null) detailTitle.text = item.title ?? string.Empty;
        if (commonTitle != null) commonTitle.SetActive(true);
        if (commonImage != null) commonImage.SetActive(true);
        if (item.weaponData != null)
        {
            WeaponItem weapon = item.weaponData;
            if (detailTitle != null && !string.IsNullOrEmpty(weapon.weaponName))
                detailTitle.text = weapon.weaponName;
            if (weapon.category == WeaponCategory.Shield)
            {
                shieldSection?.SetActive(true);
                SetText(shieldDescription, weapon.description);
                SetText(shieldDamageText, weapon.physicalDamage.ToString());
                SetText(shieldCriticalText, weapon.criticalHit.ToString("0.##"));
                SetText(shieldWeightText, weapon.weight.ToString("0.##"));
                SetText(shieldScalingText, weapon.GetScalingLabel());
                SetText(shieldRequirementsText, weapon.GetRequirementsLabel());
                SetText(shieldPhysicalDefenseText, Mathf.RoundToInt(Mathf.Clamp01(weapon.physicalBlockPercent) * 100f).ToString());
                SetText(shieldMagicDefenseText, Mathf.RoundToInt(Mathf.Clamp01(weapon.magicBlockPercent) * 100f).ToString());
            }
            else
            {
                weaponSection?.SetActive(true);
                SetText(weaponDescription, weapon.description);
                SetText(weaponDamageText, weapon.physicalDamage.ToString());
                SetText(weaponCriticalText, weapon.criticalHit.ToString("0.##"));
                SetText(weaponWeightText, weapon.weight.ToString("0.##"));
                SetText(weaponScalingText, weapon.GetScalingLabel());
                SetText(weaponRequirementsText, weapon.GetRequirementsLabel());
            }
        }
        else if (item.armorData != null)
        {
            armorSection?.SetActive(true);
            ArmorItemData armor = item.armorData;
            if (detailTitle != null && !string.IsNullOrEmpty(armor.itemName)) detailTitle.text = armor.itemName;
            SetText(armorDescription, armor.description);
            SetText(armorWeightText, armor.weight.ToString("0.##"));
            SetText(armorPhysicalDefenseText, armor.physicalDefense.ToString());
            SetText(armorMagicDefenseText, armor.magicDefense.ToString());
        }
        else
        {
            itemSection?.SetActive(true);
            SetText(itemDescription, item.description);
        }
    }

    private void HideDetailSections()
    {
        commonTitle?.SetActive(false);
        commonImage?.SetActive(false);
        weaponSection?.SetActive(false);
        shieldSection?.SetActive(false);
        armorSection?.SetActive(false);
        itemSection?.SetActive(false);
        if (detailImage != null)
        {
            detailImage.sprite = null;
            detailImage.enabled = false;
        }
        if (detailTitle != null) detailTitle.text = string.Empty;
        ClearDetailTexts();
        RefreshSelectedPrice(null);
    }

    private void RefreshSelectedPrice(ScriptableObject asset)
    {
        if (weaponBuyPriceText != null) weaponBuyPriceText.text = string.Empty;
        if (weaponSellPriceText != null) weaponSellPriceText.text = string.Empty;
        if (shieldBuyPriceText != null) shieldBuyPriceText.text = string.Empty;
        if (shieldSellPriceText != null) shieldSellPriceText.text = string.Empty;
        if (armorBuyPriceText != null) armorBuyPriceText.text = string.Empty;
        if (armorSellPriceText != null) armorSellPriceText.text = string.Empty;
        if (itemBuyPriceText != null) itemBuyPriceText.text = string.Empty;
        if (itemSellPriceText != null) itemSellPriceText.text = string.Empty;
        if (asset == null) return;
        float buyMultiplier = currentMerchant != null ? currentMerchant.buyMultiplier : 1f;
        float sellMultiplier = currentMerchant != null ? currentMerchant.sellMultiplier : 0.5f;
        TextMeshProUGUI buyTarget = GetPriceText(asset, true);
        TextMeshProUGUI sellTarget = GetPriceText(asset, false);
        if (buyTarget != null) buyTarget.text = GetPrice(asset, buyMultiplier).ToString();
        if (sellTarget != null) sellTarget.text = GetPrice(asset, sellMultiplier).ToString();
    }

    private TextMeshProUGUI GetPriceText(ScriptableObject asset, bool buy)
    {
        if (asset is WeaponItem weapon)
            return weapon.category == WeaponCategory.Shield
                ? (buy ? shieldBuyPriceText : shieldSellPriceText)
                : (buy ? weaponBuyPriceText : weaponSellPriceText);
        if (asset is ArmorItemData) return buy ? armorBuyPriceText : armorSellPriceText;
        return buy ? itemBuyPriceText : itemSellPriceText;
    }

    private static void SetText(TextMeshProUGUI target, string value)
    {
        if (target != null) target.text = value ?? string.Empty;
    }

    private void ClearDetailTexts()
    {
        TextMeshProUGUI[] fields =
        {
            weaponDescription, weaponDamageText, weaponCriticalText, weaponWeightText,
            weaponScalingText, weaponRequirementsText, shieldDescription, shieldDamageText,
            shieldCriticalText, shieldWeightText, shieldScalingText, shieldRequirementsText,
            shieldPhysicalDefenseText, shieldMagicDefenseText, armorDescription, armorWeightText,
            armorPhysicalDefenseText, armorMagicDefenseText, itemDescription
        };
        for (int i = 0; i < fields.Length; i++)
            if (fields[i] != null) fields[i].text = string.Empty;
    }

    private Sprite GetItemIcon(InventoryItem item)
    {
        if (item == null) return null;
        return item.icon ?? item.weaponData?.icon ?? item.armorData?.icon ?? item.usableData?.icon ?? item.itemData?.icon ?? item.magicData?.icon;
    }

    private void OnDisable()
    {
        if (IsOpen)
            CloseShopInternal(notifyClosed: false);
        else if (isClosing)
            FinishClose(notifyClosed: false);
    }

    private void OnDestroy()
    {
        if (PlayerStats.MerchantStockSnapshotProvider != null)
            PlayerStats.MerchantStockSnapshotProvider = null;
    }

    public bool OpenShop()
    {
        return OpenShop(initialMode, currentMerchant);
    }

    public bool OpenShop(ShopMode mode = ShopMode.Buy)
    {
        return OpenShop(mode, currentMerchant);
    }

    public bool OpenShop(ShopMode mode, MerchantData merchantData)
    {
        if (isClosing)
            return false;

        if (merchantData == null)
        {
            Debug.LogWarning("[ShopManager] MerchantData mancante: apertura annullata.", this);
            return false;
        }

        if (shopHud == null)
        {
            Debug.LogWarning("[ShopManager] HUD Market non assegnata.", this);
            return false;
        }

        if (playerController == null || playerController.Controls == null)
        {
            Debug.LogWarning("[ShopManager] PlayerController o PlayerControls non disponibili.", this);
            return false;
        }

        if (IsOpen)
        {
            CurrentMode = mode;
            currentMerchant = merchantData;
            ApplyModeVisuals();
            RefreshPlayerCoins();
            RefreshShopContents();
            FocusInitialTarget();
            return true;
        }

        controls = playerController.Controls;
        CurrentMode = mode;
        currentMerchant = merchantData;
        remainingStock.Clear();
        stockLoaded = false;
        IsOpen = true;
        isInteractive = false;
        openingFrame = Time.frameCount;

        playerController.AcquireGameplayInputLock(gameplayLockOwner);
        SubscribeInput();

        shopHud.SetActive(true);
        ApplyModeVisuals();
        RefreshPlayerCoins();
        RefreshShopContents();
        Canvas.ForceUpdateCanvases();
        FocusInitialTarget();
        SetShopFocus(0);
        contentAppearRoutine = StartCoroutine(PlayContentAppearAnimation());
        return true;
    }

    private void Update()
    {
        if (!IsOpen || !isInteractive || focusArea != FocusArea.Grid || controls == null
            || Time.unscaledTime < lastNavigationTime + NavigationRepeatCooldown)
            return;

        Vector2 navigation = controls.Player.Move.ReadValue<Vector2>();
        if (navigation.x > 0.5f)
            MoveShopFocusHorizontal(1);
        else if (navigation.x < -0.5f)
            MoveShopFocusHorizontal(-1);
        else if (navigation.y > 0.5f)
            MoveShopFocusVertical(-1);
        else if (navigation.y < -0.5f)
            MoveShopFocusVertical(1);
        else
            return;

        lastNavigationTime = Time.unscaledTime;
    }

    private void MoveShopFocusHorizontal(int direction)
    {
        if (shopSlots.Count == 0) return;
        SetShopFocus((shopFocusIndex + (direction >= 0 ? 1 : -1) + shopSlots.Count) % shopSlots.Count);
    }

    private void MoveShopFocusVertical(int direction)
    {
        if (shopSlots.Count == 0) return;
        int columns = 5;
        if (slotGrid != null && slotGrid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            columns = Mathf.Max(1, slotGrid.constraintCount);
        int next = shopFocusIndex + direction * columns;
        next %= shopSlots.Count;
        if (next < 0) next += shopSlots.Count;
        SetShopFocus(next);
    }

    private void SetShopFocus(int index)
    {
        if (shopSlots.Count == 0) return;
        focusArea = FocusArea.Grid;
        shopFocusIndex = Mathf.Clamp(index, 0, shopSlots.Count - 1);
        for (int i = 0; i < shopSlots.Count; i++)
            shopSlots[i].SetFocused(i == shopFocusIndex);
        ShowSelectedItem(shopFocusIndex);
        UpdateActionInteractable();
    }

    public void HandleSlotSelected(int index)
    {
        if (isInteractive) { ShowSelectedItem(index); UpdateActionInteractable(); }
    }

    public void HandleSlotSubmit(int index)
    {
        if (isInteractive) FocusActionButton(index);
    }

    public void HandleSlotPointerDown(int index)
    {
        if (isInteractive) SetShopFocus(index);
    }
    public void HandleSlotBeginDrag(int index, PointerEventData eventData) { }
    public void HandleSlotDrag(PointerEventData eventData) { }
    public void HandleSlotEndDrag() { }
    public void HandleSlotDrop(int targetIndex) { }

    public void SetMode(ShopMode mode)
    {
        CurrentMode = mode;
        ApplyModeVisuals();
        RefreshPlayerCoins();
        RefreshShopContents();
    }

    public void RefreshPlayerCoins()
    {
        if (playerCoinsText != null)
            playerCoinsText.text = playerStats != null ? Mathf.Max(0, playerStats.runCoins).ToString() : "0";
    }

    private void RefreshShopContents()
    {
        LoadMerchantStockOnce();
        if (CurrentMode == ShopMode.Sell)
        {
            SetShopItems(playerInventory != null ? playerInventory.Items : null);
            return;
        }
        shopItems.Clear();
        buyEntries.Clear();
        if (currentMerchant != null && currentMerchant.stock != null)
        {
            foreach (MerchantData.StockEntry entry in currentMerchant.stock)
            {
                if (entry == null || entry.item == null) continue;
                if (!remainingStock.ContainsKey(entry)) remainingStock.Add(entry, Mathf.Max(0, entry.quantity));
                if (!entry.infiniteStock && remainingStock[entry] <= 0) continue;
                InventoryItem display = CreateInventoryItem(entry.item, entry.infiniteStock ? 1 : remainingStock[entry]);
                if (display != null) { shopItems.Add(display); buyEntries.Add(entry); }
            }
        }
        SetShopItems(shopItems);
    }

    public bool TryBuy()
    {
        if (!IsOpen || CurrentMode != ShopMode.Buy || currentMerchant == null || playerInventory == null || playerStats == null || shopFocusIndex >= buyEntries.Count) return false;
        MerchantData.StockEntry entry = buyEntries[shopFocusIndex];
        if (entry == null || entry.item == null || (!entry.infiniteStock && remainingStock[entry] <= 0)) return false;
        int price = GetPrice(entry.item, currentMerchant.buyMultiplier);
        if (!playerStats.HasCoins(price) || !playerInventory.CanAddItem(entry.item, 1)) return false;
        if (!playerStats.TryRemoveCoins(price, false)) return false;
        if (!playerInventory.TryAddItem(entry.item, 1, false)) { playerStats.AddCoins(price, false); return false; }
        if (!entry.infiniteStock) remainingStock[entry] = Mathf.Max(0, remainingStock[entry] - 1);
        SaveTransaction(); RefreshAfterTransaction(); return true;
    }

    public bool TrySell()
    {
        if (!IsOpen || CurrentMode != ShopMode.Sell || playerInventory == null || playerStats == null || shopFocusIndex >= shopItems.Count) return false;
        InventoryItem selected = shopItems[shopFocusIndex];
        ScriptableObject asset = GetItemAsset(selected);
        if (asset == null || playerInventory.IsInstanceEquipped(selected.instanceId)) return false;
        int price = GetPrice(asset, currentMerchant != null ? currentMerchant.sellMultiplier : 0.5f);
        if (playerStats.runCoins > int.MaxValue - price) return false;
        bool removed = (selected.weaponData != null || selected.armorData != null)
            ? playerInventory.TryRemoveInstance(selected.instanceId, 1, out _, false)
            : playerInventory.TryRemoveItem(asset, 1, out _, false);
        if (!removed) return false;
        playerStats.AddCoins(price, false); SaveTransaction(); RefreshAfterTransaction(); return true;
    }

    private void LoadMerchantStockOnce()
    {
        if (stockLoaded) return;
        stockLoaded = true;
        if (currentMerchant == null || currentMerchant.stock == null) return;
        SavedMerchantStockData[] saved = playerStats != null && playerStats.LoadedDataSnapshot != null
            ? playerStats.LoadedDataSnapshot.merchantStocks : null;
        for (int i = 0; i < currentMerchant.stock.Count; i++)
        {
            MerchantData.StockEntry entry = currentMerchant.stock[i];
            if (entry == null || entry.infiniteStock) continue;
            int quantity = Mathf.Max(0, entry.quantity);
            if (saved != null)
                for (int j = 0; j < saved.Length; j++)
                    if (saved[j] != null && saved[j].merchantId == currentMerchant.merchantId && saved[j].entryId == GetEntryId(entry, i)) { quantity = Mathf.Max(0, saved[j].remainingQuantity); break; }
            remainingStock[entry] = quantity;
        }
    }

    private SavedMerchantStockData[] CreateMerchantStockSave()
    {
        if (currentMerchant == null || currentMerchant.stock == null) return System.Array.Empty<SavedMerchantStockData>();
        List<SavedMerchantStockData> result = new List<SavedMerchantStockData>();
        for (int i = 0; i < currentMerchant.stock.Count; i++)
        {
            MerchantData.StockEntry entry = currentMerchant.stock[i];
            if (entry == null || entry.infiniteStock) continue;
            int remaining = remainingStock.ContainsKey(entry) ? remainingStock[entry] : entry.quantity;
            result.Add(new SavedMerchantStockData { merchantId = currentMerchant.merchantId, entryId = GetEntryId(entry, i), remainingQuantity = Mathf.Max(0, remaining) });
        }
        return result.ToArray();
    }

    private static string GetEntryId(MerchantData.StockEntry entry, int index)
    {
        return !string.IsNullOrWhiteSpace(entry.entryId) ? entry.entryId : "entry_" + index.ToString();
    }

    private void RefreshAfterTransaction()
    {
        int oldIndex = shopFocusIndex; RefreshPlayerCoins(); RefreshShopContents();
        if (shopItems.Count > 0) SetShopFocus(Mathf.Clamp(oldIndex, 0, shopItems.Count - 1)); else HideDetailSections();
        UpdateActionInteractable();
    }

    private void SaveTransaction() { if (playerStats != null) playerStats.SaveStats(); }

    private void UpdateActionInteractable()
    {
        if (shopFocusIndex < 0 || shopFocusIndex >= shopItems.Count) return;
        Button button = GetActionButton(shopItems[shopFocusIndex]);
        ScriptableObject asset = GetItemAsset(shopItems[shopFocusIndex]);
        if (button == null || asset == null) return;
        button.interactable = CurrentMode == ShopMode.Buy
            ? playerStats != null && currentMerchant != null && playerStats.HasCoins(GetPrice(asset, currentMerchant.buyMultiplier)) && playerInventory != null && playerInventory.CanAddItem(asset, 1)
            : playerInventory != null && !playerInventory.IsInstanceEquipped(shopItems[shopFocusIndex].instanceId);
    }

    private static ScriptableObject GetItemAsset(InventoryItem item)
    {
        if (item == null) return null;
        if (item.weaponData != null) return item.weaponData;
        if (item.armorData != null) return item.armorData;
        if (item.usableData != null) return item.usableData;
        if (item.itemData != null) return item.itemData;
        return item.magicData;
    }

    private static InventoryItem CreateInventoryItem(ScriptableObject asset, int quantity)
    {
        if (asset is WeaponItem weapon) return new InventoryItem(weapon, quantity);
        if (asset is ArmorItemData armor) return new InventoryItem(armor, quantity);
        if (asset is UsableItemData usable) return new InventoryItem(usable, quantity);
        if (asset is MagicItemData magic) return new InventoryItem(magic, quantity);
        if (asset is ItemData item) return new InventoryItem(item, quantity);
        return null;
    }

    private static int GetPrice(ScriptableObject asset, float multiplier)
    {
        int value = asset is WeaponItem weapon ? weapon.baseValue : asset is ArmorItemData armor ? armor.baseValue : asset is UsableItemData usable ? usable.baseValue : asset is MagicItemData magic ? magic.baseValue : asset is ItemData item ? item.baseValue : 0;
        return Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0f, value) * Mathf.Max(0f, multiplier)));
    }

    private void ApplyModeVisuals()
    {
        bool buy = CurrentMode == ShopMode.Buy;
        SetButtonActive(weaponBuyButton, buy);
        SetButtonActive(shieldBuyButton, buy);
        SetButtonActive(armorBuyButton, buy);
        SetButtonActive(itemBuyButton, buy);
        SetButtonActive(weaponSellButton, !buy);
        SetButtonActive(shieldSellButton, !buy);
        SetButtonActive(armorSellButton, !buy);
        SetButtonActive(itemSellButton, !buy);
    }

    private static void SetButtonActive(Button button, bool active)
    {
        if (button != null)
            button.gameObject.SetActive(active);
    }

    public void CloseShop()
    {
        if (!isInteractive)
            return;

        CloseShopInternal(notifyClosed: true);
    }

    private void CloseShopInternal(bool notifyClosed)
    {
        if (!IsOpen || isClosing)
            return;

        IsOpen = false;
        isClosing = true;
        isInteractive = false;
        ClearActionFocusVisual();
        UnsubscribeInput();
        StopContentAppearRoutineOnly();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        if (notifyClosed)
        {
            closeRoutine = StartCoroutine(RunCloseAnimations());
            return;
        }

        FinishClose(notifyClosed);
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

        FinishClose(notifyClosed: true);
    }

    private void FinishClose(bool notifyClosed)
    {
        if (shopHud != null)
            shopHud.SetActive(false);

        isClosing = false;
        isInteractive = false;
        closeRoutine = null;

        // The service reopens the dialogue from Closed while this lock is still
        // held, so gameplay never becomes active between the two modal states.
        if (notifyClosed)
            Closed?.Invoke();

        if (playerController != null)
            playerController.ReleaseGameplayInputLock(gameplayLockOwner);

        controls = null;
        openingFrame = -1;
    }

    private System.Collections.IEnumerator PlayContentAppearAnimation()
    {
        if (contentAppearDelay > 0f)
            yield return new WaitForSecondsRealtime(contentAppearDelay);
        if (!IsOpen)
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

    private void SubscribeInput()
    {
        if (controls == null)
            return;

        // Same actions used by the player inventory/menu: Cross confirms,
        // Circle/Back closes. UI navigation remains owned by the EventSystem.
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

        if (focusArea == FocusArea.Grid)
            FocusActionButton(shopFocusIndex);
        else
            if (CurrentMode == ShopMode.Buy) TryBuy(); else TrySell();
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

        CloseShop();
    }

    private void FocusActionButton(int index)
    {
        if (index < 0 || index >= shopItems.Count || shopItems[index] == null)
            return;

        Button button = GetActionButton(shopItems[index]);
        if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
            return;

        focusArea = FocusArea.Action;
        ShowSelectedItem(index);
        ClearActionFocusVisual();
        activeActionSelection = GetActionSelection(shopItems[index]);
        activeActionSelection?.SetFocused(true);
        button.Select();
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(button.gameObject);
    }

    private Button GetActionButton(InventoryItem item)
    {
        bool buy = CurrentMode == ShopMode.Buy;
        if (item.weaponData != null)
            return item.weaponData.category == WeaponCategory.Shield
                ? (buy ? shieldBuyButton : shieldSellButton)
                : (buy ? weaponBuyButton : weaponSellButton);
        if (item.armorData != null)
            return buy ? armorBuyButton : armorSellButton;
        return buy ? itemBuyButton : itemSellButton;
    }

    private SegmentedButtonSelectionUI GetActionSelection(InventoryItem item)
    {
        bool buy = CurrentMode == ShopMode.Buy;
        if (item.weaponData != null)
            return item.weaponData.category == WeaponCategory.Shield
                ? (buy ? shieldBuySelection : shieldSellSelection)
                : (buy ? weaponBuySelection : weaponSellSelection);
        if (item.armorData != null)
            return buy ? armorBuySelection : armorSellSelection;
        return buy ? itemBuySelection : itemSellSelection;
    }

    private void ClearActionFocusVisual()
    {
        if (activeActionSelection != null)
            activeActionSelection.SetFocused(false);
        activeActionSelection = null;
    }

    private void ReturnFocusToGrid()
    {
        ClearActionFocusVisual();
        focusArea = FocusArea.Grid;
        ShowSelectedItem(shopFocusIndex);
        if (EventSystem.current != null && shopFocusIndex >= 0 && shopFocusIndex < shopSlots.Count)
            EventSystem.current.SetSelectedGameObject(shopSlots[shopFocusIndex].gameObject);
    }

    private void FocusInitialTarget()
    {
        if (EventSystem.current == null)
            return;

        if (initialFocus != null)
            EventSystem.current.SetSelectedGameObject(initialFocus);
    }
}
