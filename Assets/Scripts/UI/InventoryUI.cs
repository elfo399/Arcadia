using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class InventoryUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private InventorySlot slotPrefab;
    [SerializeField] private Transform slotParent;
    [SerializeField] private int initialSlotCount = 0; // facoltativo: crea slot all'avvio
    private readonly List<InventorySlot> slots = new();
    private List<InventoryItem> currentItems = new();
    private List<InventoryItem> sourceItems = new(); // lista completa, non filtrata
    public List<InventoryItem> GetCurrentItemsSnapshot() => new List<InventoryItem>(currentItems);
    public List<InventoryItem> GetSourceItemsSnapshot() => new List<InventoryItem>(sourceItems);

    [Header("Tabs")]
    [SerializeField] private TabEntry[] tabs;
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color inactiveColor = new Color(0.8f, 0.8f, 0.8f);
    [SerializeField] private string defaultTabKey = "Inventory";
    private int currentTabIndex = -1;

    public enum WalletSource { Run, Bank }

    [System.Serializable]
    public class QuestObjectiveEntryData
    {
        public string title;
        public string description;
        public bool completed;
    }

    [System.Serializable]
    public class QuestRewardEntryData
    {
        public Sprite icon;
        public string type;
        public int amount = 1;
        public string itemName;
    }

    [System.Serializable]
    public class QuestEntryData
    {
        public string questId;
        public string title;
        public string location;
        public bool completed;
        public string questTypeLabel = "Main Quest";
        public string recommendedLabel = "";
        public string loreTitle = "";
        [TextArea(2, 6)] public string loreDescription = "";
        public string loreAuthor = "";
        public List<QuestObjectiveEntryData> objectives = new();
        public List<QuestRewardEntryData> rewards = new();
    }

    [Header("Wallet UI")]
    [SerializeField] private TextMeshProUGUI goldValueText;
    [SerializeField] private TextMeshProUGUI silverValueText;
    [SerializeField] private TextMeshProUGUI copperValueText;
    [SerializeField] private WalletSource walletSource = WalletSource.Run;
    [SerializeField] private bool autoRefreshWallet = true;
    private PlayerStats playerStats;

    [Header("Quest UI")]
    [SerializeField] private bool useQuestManager = true;
    [SerializeField] private bool autoWireQuestUI = true;
    [SerializeField] private Transform questListContainer;
    [SerializeField] private GameObject questItemPrefab;
    [SerializeField] private Button questActiveFilterButton;
    [SerializeField] private Button questCompletedFilterButton;
    [SerializeField] private TextMeshProUGUI questActiveCountText;
    [SerializeField] private TextMeshProUGUI questCompletedCountText;
    [SerializeField] private TextMeshProUGUI questActiveFilterLabelText;
    [SerializeField] private TextMeshProUGUI questCompletedFilterLabelText;
    [SerializeField] private Color questFilterSelectedColor = new Color(1f, 0.35f, 0.35f, 1f);
    [SerializeField] private List<QuestEntryData> startingQuests = new();
    [Header("Quest Detail UI")]
    [SerializeField] private TextMeshProUGUI questDetailTypeText;
    [SerializeField] private TextMeshProUGUI questDetailRecommendedText;
    [SerializeField] private TextMeshProUGUI questDetailTitleText;
    [SerializeField] private TextMeshProUGUI questDetailLocationText;
    [SerializeField] private TextMeshProUGUI questDetailLoreTitleText;
    [SerializeField] private TextMeshProUGUI questDetailLoreDescriptionText;
    [SerializeField] private TextMeshProUGUI questDetailLoreAuthorText;
    [SerializeField] private RectTransform questDetailLoreRoot;
    [SerializeField] private RectTransform questObjectivesSectionRoot;
    [SerializeField] private GameObject questDetailPanelRoot;
    [SerializeField] private bool showQuestDetailOnlyOnSelection = true;
    [SerializeField] private bool collapseQuestLoreWhenEmpty = true;
    [SerializeField] private float questObjectivesLiftWhenNoLore = -1f; // < 0 => auto (lore height + padding)
    [SerializeField] private Transform questObjectivesContainer;
    [SerializeField] private GameObject questObjectivePrefab;
    [SerializeField] private Transform questRewardsContainer;
    [SerializeField] private GameObject questRewardPrefab;
    [SerializeField] private ScrollRect questDetailScrollRect;
    [SerializeField] private bool preserveQuestScrollPosition = false;
    [SerializeField] private bool smoothQuestMouseWheel = true;
    [SerializeField] private float questMouseWheelStepNormalized = 0.10f;
    [SerializeField] private float questMouseWheelSmoothSpeed = 14f;

    private enum QuestFilter { All, Active, Completed }
    private QuestFilter currentQuestFilter = QuestFilter.All;
    private readonly List<QuestEntryData> questEntries = new();
    private readonly List<GameObject> spawnedQuestRows = new();
    private readonly List<GameObject> spawnedObjectiveRows = new();
    private readonly List<GameObject> spawnedRewardRows = new();
    private bool questUiInitialized = false;
    private QuestManager questManager;
    private bool questManagerSubscribed = false;
    private bool questFilterBaseColorsCached = false;
    private Color questActiveFilterBaseColor = Color.white;
    private Color questCompletedFilterBaseColor = Color.white;
    private string selectedQuestId = null;
    private float questTargetScrollNormalized = 1f;
    private bool questTargetScrollInitialized = false;
    private bool questObjectivesDefaultPosCached = false;
    private Vector2 questObjectivesDefaultAnchoredPos;

    [Header("Drag & Drop")]
    [SerializeField] private Canvas dragCanvas; // opzionale: se null usa quello più alto trovato
    [SerializeField] private Image dragPreviewTemplate;
    private Image activeDragPreview;
    private int dragOriginIndex = -1;
    private int selectedPadIndex = -1;
    private int currentSelectedIndex = -1;
    private int padFocusIndex = -1;
    [SerializeField] private float gamepadAxisDetectThreshold = 0.35f;
    private bool showPadFocus = false;
    private enum EquipCrossFocus { Right, Left, Bottom, Top }
    private EquipCrossFocus equipCrossFocus = EquipCrossFocus.Right;

    [Header("Detail Panel - Common")]
    [SerializeField] private Image detailIcon;
    [SerializeField] private TextMeshProUGUI detailTitle;
    [SerializeField] private TextMeshProUGUI detailDescription;
    [SerializeField] private GameObject detailRoot;

    [Header("Detail Panel - Weapon Stats")]
    [SerializeField] private GameObject weaponStatsRoot;
    [SerializeField] private TextMeshProUGUI weaponDamageText;
    [SerializeField] private TextMeshProUGUI weaponCriticalText;
    [SerializeField] private TextMeshProUGUI weaponWeightText;
    [SerializeField] private TextMeshProUGUI weaponScalingText;
    [SerializeField] private TextMeshProUGUI weaponRequirementsText;

    [Header("Detail Panel - Weapon")]
    [SerializeField] private GameObject weaponDetailRoot;   // DescWeapon
    [SerializeField] private Image weaponImage;             // DescWeapon/Image
    [SerializeField] private TextMeshProUGUI weaponTitle;   // DescWeapon/Title
    [SerializeField] private TextMeshProUGUI weaponDesc;    // DescWeapon/Desc

    [Header("Detail Panel - Item")]
    [SerializeField] private GameObject itemDetailRoot;     // DescItem
    [SerializeField] private Image itemImage;               // DescItem/Image
    [SerializeField] private TextMeshProUGUI itemTitle;     // DescItem/Title
    [SerializeField] private TextMeshProUGUI itemDesc;      // DescItem/Desc

    [Header("Events")]
    public UnityEvent<string> onTabChanged;

    private enum Filter { All, Weapons, Usables, Magic }
    private Filter currentFilter = Filter.All;
    private Filter lastFilter = Filter.All;

    [Header("HUD Cross Icons (solo overlay esterno)")]
    [SerializeField] private Image hudCrossTop;
    [SerializeField] private Image hudCrossRight;
    [SerializeField] private Image hudCrossBottom;
    [SerializeField] private Image hudCrossLeft;
    [Header("HUD Cross Containers (instanzia invSlot prefab)")]
    [SerializeField] private Transform hudRightContainer;
    [SerializeField] private Transform hudLeftContainer;
    [SerializeField] private Transform hudBottomContainer;
    [SerializeField] private Transform hudTopContainer;
    [Header("Equipment Slot Containers (instanzia invSlot prefab)")]
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
    [SerializeField] private GameObject equipmentBackground;
    [SerializeField] private GameObject inventoryBackground;
    [SerializeField] private Button equipWeaponButton;
    [SerializeField] private Button equipUsableButton;

    private PlayerInventory playerInventory;
    private enum EquipTarget { None, Right, Left, Bottom, Top }
    private EquipTarget currentEquipTarget = EquipTarget.None;
    private int currentEquipSlot = 0; // 0-2
    private InventorySlot[] rightEquipSlots = new InventorySlot[3];
    private InventorySlot[] leftEquipSlots = new InventorySlot[3];
    private InventorySlot[] bottomEquipSlots = new InventorySlot[3];
    private InventorySlot[] topEquipSlots = new InventorySlot[3];
    private int currentTopIndex = 0;
    private InventorySlot hudRightSlot;
    private InventorySlot hudLeftSlot;
    private InventorySlot hudBottomSlot;
    private InventorySlot hudTopSlot;
    private bool equipSlotsBuilt = false;
    private bool hudSlotsBuilt = false;

    void Awake()
    {
        // costruisci subito le croci HUD/equip anche se il menu è disattivato all'avvio
        BuildEquipSlotsIfNeeded();
        BuildHudSlotsIfNeeded();
        RefreshEquipmentCross();
    }

    void Start()
    {
        // fallback: se non assegnato, usa il proprio transform come parent
        if (slotParent == null) slotParent = transform;
        playerInventory = FindObjectOfType<PlayerInventory>();

        // se non usiamo prefab, conserva eventuali slot già presenti come figli
        if (slotPrefab == null && slotParent != null)
        {
            slots.AddRange(slotParent.GetComponentsInChildren<InventorySlot>(true));
            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].Init(i, this);
            }
        }

        // genera slot iniziali se richiesto
        if (slotPrefab != null && initialSlotCount > 0 && slots.Count == 0)
        {
            EnsureSlots(initialSlotCount);
        }

        ClearAllSlots();
        ClearDetailPanel();

        // Evidenzia tab di default
        if (!string.IsNullOrEmpty(defaultTabKey))
        {
            SetActiveTab(defaultTabKey);
        }

        CachePlayerStats();
        if (autoRefreshWallet) RefreshWalletUI();

        ResetEquipTarget();

        // istanzia i visual dei quattro slot equip usando lo stesso prefab della griglia
        BuildEquipSlotsIfNeeded();
        BuildHudSlotsIfNeeded();

        // mostra subito gli equip correnti
        RefreshEquipmentCross();

        TryBindQuestManager();
        InitializeQuestUIIfNeeded();
    }

    void OnEnable()
    {
        // quando il pannello viene riaperto, riallinea subito le icone HUD/equip
        RefreshEquipmentCross();
        TryBindQuestManager();
        RefreshQuestUI();
        RefreshFocusVisualState();
    }

    void Update()
    {
        UpdateFocusInputMode();
        UpdateQuestMouseWheelSmoothScroll();
    }

    void OnDestroy()
    {
        ClearDragPreview();
        ClearSpawnedRows(spawnedObjectiveRows, questObjectivesContainer, questObjectivePrefab);
        ClearSpawnedRows(spawnedRewardRows, questRewardsContainer, questRewardPrefab);
        if (questActiveFilterButton != null)
            questActiveFilterButton.onClick.RemoveListener(SetQuestFilterActive);
        if (questCompletedFilterButton != null)
            questCompletedFilterButton.onClick.RemoveListener(SetQuestFilterCompleted);
        UnbindQuestManager();
        if (playerStats != null)
        {
            playerStats.OnBankChanged -= HandleBankChanged;
            playerStats.OnRunWalletChanged -= HandleRunWalletChanged;
        }
    }

    /// <summary>
    /// Aggiorna gli slot con i dati inventario.
    /// </summary>
    public void UpdateUI(List<InventoryItem> inventoryData)
    {
        // se viene chiamato direttamente, consideriamo questi dati come fonte e attiviamo filtro corrente
        sourceItems = NormalizeSourceItems(inventoryData);
        ApplyFilterInternal(currentFilter);
    }

    /// <summary>
    /// Imposta la lista sorgente (completa) e applica il filtro corrente.
    /// </summary>
    public void SetSourceItems(List<InventoryItem> inventoryData)
    {
        sourceItems = NormalizeSourceItems(inventoryData);
        ApplyFilterInternal(currentFilter);
    }

    public void ShowWeaponsFilter() { lastFilter = Filter.Weapons; ApplyFilterInternal(Filter.Weapons); }
    public void ShowUsablesFilter() { lastFilter = Filter.Usables; ApplyFilterInternal(Filter.Usables); }
    public void ShowMagicFilter()   { lastFilter = Filter.Magic; ApplyFilterInternal(Filter.Magic); }
    public void ShowAllFilter()     { lastFilter = Filter.All; ApplyFilterInternal(Filter.All); }

    public void ApplyLastFilter() => ApplyFilterInternal(lastFilter);

    public void ResetFilterToAll()
    {
        lastFilter = Filter.All;
        ApplyFilterInternal(Filter.All);
        ResetEquipTarget();
    }

    // ---- Equipment cross button handlers ----
    public void OnEquipRight(int slot = 0)
    {
        EnsurePlayerInventory();
        ShowEquipmentInventory(true);
        currentEquipTarget = EquipTarget.Right;
        currentEquipSlot = Mathf.Clamp(slot, 0, 2);
        playerInventory.currentRightIndex = currentEquipSlot;
        SetSourceItemsFromPlayer();
        ShowWeaponsFilter();
        UpdateEquipButtonState();
    }
    public void OnEquipLeft(int slot = 0)
    {
        EnsurePlayerInventory();
        ShowEquipmentInventory(true);
        currentEquipTarget = EquipTarget.Left;
        currentEquipSlot = Mathf.Clamp(slot, 0, 2);
        playerInventory.currentLeftIndex = currentEquipSlot;
        SetSourceItemsFromPlayer();
        ShowWeaponsFilter();
        UpdateEquipButtonState();
    }
    public void OnEquipBottom(int slot = 0)
    {
        EnsurePlayerInventory();
        ShowEquipmentInventory(true);
        currentEquipTarget = EquipTarget.Bottom;
        currentEquipSlot = Mathf.Clamp(slot, 0, 2);
        playerInventory.currentUsableIndex = currentEquipSlot;
        SetSourceItemsFromPlayer();
        ShowUsablesFilter();
        UpdateEquipButtonState();
    }
    public void OnEquipTop(int slot = 0)
    {
        EnsurePlayerInventory();
        ShowEquipmentInventory(true);
        currentEquipTarget = EquipTarget.Top;
        currentTopIndex = Mathf.Clamp(slot, 0, 2);
        SetSourceItemsFromPlayer();
        ShowMagicFilter(); // placeholder
        UpdateEquipButtonState();
    }

    private void ApplyFilterInternal(Filter filter)
    {
        currentFilter = filter;

        // crea lista filtrata mantenendo la lunghezza per non rompere gli indici degli slot
        currentItems = new List<InventoryItem>(sourceItems.Count);
        for (int i = 0; i < sourceItems.Count; i++)
        {
            var item = sourceItems[i];
            if (MatchesFilter(item, filter))
                currentItems.Add(item);
            else
                currentItems.Add(null);
        }

        // Garantisce almeno gli slot richiesti dagli item o dal valore iniziale configurato
        int neededSlots = Mathf.Max(currentItems.Count, initialSlotCount);
        EnsureSlots(neededSlots);

        ClearAllSlots();

        for (int i = 0; i < slots.Count; i++)
        {
            InventoryItem item = i < currentItems.Count ? currentItems[i] : null;
            if (item != null)
            {
                slots[i].Setup(GetItemIcon(item), item.amount, IsItemEquipped(item));
            }
            else
            {
                slots[i].Clear();
            }
            // mantieni attivo lo slot anche se vuoto, cosï¿½ si vede la griglia di placeholder
            slots[i].gameObject.SetActive(true);
        }

        selectedPadIndex = -1;
        currentSelectedIndex = -1;
        ApplyPadFocusVisual(-1);
        ClearDetailPanel();
        RefreshEquipmentCross();
        UpdateEquipButtonState();
    }

    private bool MatchesFilter(InventoryItem item, Filter filter)
    {
        if (filter == Filter.All || item == null) return true;
        switch (filter)
        {
            case Filter.Weapons: return item.weaponData != null;
            case Filter.Usables: return item.usableData != null;
            case Filter.Magic:   return false; // placeholder per future magie
            default: return true;
        }
    }

    private void EnsurePlayerInventory()
    {
        if (playerInventory == null)
            playerInventory = FindObjectOfType<PlayerInventory>();
    }

    private void SetSourceItemsFromPlayer()
    {
        EnsurePlayerInventory();
        if (playerInventory == null) return;
        var list = new List<InventoryItem>(playerInventory.Items);
        SetSourceItems(list);
    }

    /// <summary>
    /// Per le armi non stackabili: se amount > 1 vengono splittate in item separati.
    /// </summary>
    private List<InventoryItem> NormalizeSourceItems(List<InventoryItem> data)
    {
        var result = new List<InventoryItem>();
        if (data == null) return result;

        foreach (var it in data)
        {
            if (it == null)
            {
                result.Add(null);
                continue;
            }
            // le armi sono già istanze uniche (amount=1) con instanceId: non duplicare
            result.Add(it);
        }
        return result;
    }

    private void ShowEquipmentInventory(bool showInventoryPanel)
    {
        // quando apri la griglia inventario per scegliere un equip, nascondi lo sfondo degli slot equip
        if (equipmentBackground != null) equipmentBackground.SetActive(!showInventoryPanel);
        if (inventoryBackground != null) inventoryBackground.SetActive(showInventoryPanel);
        // opzionale: potresti spegnere altri background (magic/skill/quest etc) se sono presenti
    }

    /// <summary>
    /// Attiva una tab specifica: colora il titolo e mostra l'eventuale background associato.
    /// </summary>
    public void SetActiveTab(string tabKey)
    {
        if (tabs == null || tabs.Length == 0) return;

        bool tabFound = false;
        bool isInventoryTab = string.Equals(tabKey, "Inventory", System.StringComparison.OrdinalIgnoreCase);
        bool isEquipmentTab = string.Equals(tabKey, "Equipment", System.StringComparison.OrdinalIgnoreCase);
        bool isQuestTab = string.Equals(tabKey, "Quest", System.StringComparison.OrdinalIgnoreCase)
                          || string.Equals(tabKey, "Quests", System.StringComparison.OrdinalIgnoreCase);

        foreach (var tab in tabs)
        {
            if (tab == null || tab.label == null) continue;

            bool isActive = string.Equals(tab.key, tabKey, System.StringComparison.OrdinalIgnoreCase);
            tab.label.color = isActive ? activeColor : inactiveColor;

            if (tab.background != null)
            {
                tab.background.SetActive(isActive);
            }

            if (isActive)
            {
                // salva l'indice corrente
                currentTabIndex = System.Array.IndexOf(tabs, tab);
                tabFound = true;
            }
        }
        // se non trovata, punta alla prima tab valida
        if (currentTabIndex < 0 && tabs.Length > 0)
        {
            currentTabIndex = 0;
            if (tabs[0].background != null) tabs[0].background.SetActive(true);
            if (tabs[0].label != null) tabs[0].label.color = activeColor;
            tabFound = true;
        }

        if (tabFound && onTabChanged != null)
        {
            onTabChanged.Invoke(tabKey);
        }

        // Se la tab è Inventory, forza il filtro a "tutti"
        if (isInventoryTab) ResetFilterToAll();

        if (isEquipmentTab)
        {
            ShowEquipmentInventory(false);
            FocusEquipmentCrossDefault();
        }

        if (isQuestTab)
        {
            InitializeQuestUIIfNeeded();
            RefreshQuestUI();
        }
    }

    /// <summary>
    /// Richiamabile dai pulsanti UI (OnClick) con la chiave della tab.
    /// </summary>
    public void ShowTab(string tabKey)
    {
        SetActiveTab(tabKey);
    }

    public void NextTab()
    {
        if (tabs == null || tabs.Length == 0) return;
        int next = (currentTabIndex + 1 + tabs.Length) % tabs.Length;
        SetActiveTab(tabs[next].key);
    }

    public void PreviousTab()
    {
        if (tabs == null || tabs.Length == 0) return;
        int prev = (currentTabIndex - 1 + tabs.Length) % tabs.Length;
        SetActiveTab(tabs[prev].key);
    }

    private void CachePlayerStats()
    {
        if (playerStats != null) return;
        playerStats = PlayerStats.instance != null ? PlayerStats.instance : FindObjectOfType<PlayerStats>();
        if (playerStats != null)
        {
            playerStats.OnBankChanged += HandleBankChanged;
            playerStats.OnRunWalletChanged += HandleRunWalletChanged;
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
    }

    public void RefreshWalletUI()
    {
        CachePlayerStats();
        if (playerStats != null)
        {
            if (walletSource == WalletSource.Bank)
                SetWalletValues(playerStats.bankGold, playerStats.bankSilver, playerStats.bankCopper);
            else
                SetWalletValues(playerStats.runGold, playerStats.runSilver, playerStats.runCopper);
        }
    }

    public void SetWalletValues(int gold, int silver, int copper)
    {
        if (goldValueText != null) goldValueText.text = gold.ToString();
        if (silverValueText != null) silverValueText.text = silver.ToString();
        if (copperValueText != null) copperValueText.text = copper.ToString();
    }

    // ------- QUEST UI --------
    public void SetQuestFilterAll()
    {
        InitializeQuestUIIfNeeded();
        currentQuestFilter = QuestFilter.All;
        RefreshQuestUI();
    }

    public void SetQuestFilterActive()
    {
        InitializeQuestUIIfNeeded();
        currentQuestFilter = currentQuestFilter == QuestFilter.Active ? QuestFilter.All : QuestFilter.Active;
        RefreshQuestUI();
    }

    public void SetQuestFilterCompleted()
    {
        InitializeQuestUIIfNeeded();
        currentQuestFilter = currentQuestFilter == QuestFilter.Completed ? QuestFilter.All : QuestFilter.Completed;
        RefreshQuestUI();
    }

    public void SetQuests(List<QuestEntryData> quests)
    {
        string previousSelected = selectedQuestId;
        questEntries.Clear();
        if (quests != null)
        {
            for (int i = 0; i < quests.Count; i++)
            {
                var copy = CloneQuest(quests[i]);
                if (copy != null) questEntries.Add(copy);
            }
        }

        selectedQuestId = previousSelected;
        RefreshQuestUI();
    }

    public void AddOrUpdateQuest(string questId, string title, string location, bool completed)
    {
        TryBindQuestManager();
        if (useQuestManager && questManager != null)
        {
            questManager.AddOrUpdateQuest(questId, title, location, completed);
            return;
        }

        string normalizedId = NormalizeQuestId(questId, title, location);
        int index = FindQuestIndexById(normalizedId);

        if (index >= 0)
        {
            questEntries[index].title = title;
            questEntries[index].location = location;
            questEntries[index].completed = completed;
        }
        else
        {
            questEntries.Add(new QuestEntryData
            {
                questId = normalizedId,
                title = title,
                location = location,
                completed = completed
            });
        }

        RefreshQuestUI();
    }

    public bool SetQuestCompleted(string questId, bool completed = true)
    {
        TryBindQuestManager();
        if (useQuestManager && questManager != null)
            return questManager.SetQuestCompleted(questId, completed);

        int index = FindQuestIndexById(questId);
        if (index < 0) return false;

        questEntries[index].completed = completed;
        RefreshQuestUI();
        return true;
    }

    private void InitializeQuestUIIfNeeded()
    {
        if (questUiInitialized) return;
        questUiInitialized = true;

        bool needQuestListWiring = questListContainer == null || questItemPrefab == null;
        if (autoWireQuestUI || needQuestListWiring)
            AutoWireQuestUIReferences();

        TryEditorAutoAssignQuestPrefabs();

        if (questListContainer != null)
        {
            if (questItemPrefab == null && questListContainer.childCount > 0)
            {
                questItemPrefab = questListContainer.GetChild(0).gameObject;
            }

            if (questItemPrefab != null && questItemPrefab.transform.parent == questListContainer)
            {
                questItemPrefab.SetActive(false);
            }
        }

        if (questObjectivesContainer != null && questObjectivePrefab != null && questObjectivePrefab.transform.parent == questObjectivesContainer)
            questObjectivePrefab.SetActive(false);

        if (questRewardsContainer != null && questRewardPrefab != null && questRewardPrefab.transform.parent == questRewardsContainer)
            questRewardPrefab.SetActive(false);

        WireQuestFilterButtons();
        CacheQuestFilterBaseColors();

        TryBindQuestManager();

        bool loadedFromManager = false;
        if (useQuestManager && questManager != null)
        {
            if (questManager.QuestCount == 0 && startingQuests != null && startingQuests.Count > 0)
            {
                var initial = new List<QuestManager.QuestData>(startingQuests.Count);
                for (int i = 0; i < startingQuests.Count; i++)
                {
                    if (startingQuests[i] == null) continue;
                    var src = startingQuests[i];
                    var mappedObjectives = new List<QuestManager.QuestObjectiveData>();
                    if (src.objectives != null)
                    {
                        for (int j = 0; j < src.objectives.Count; j++)
                        {
                            var o = src.objectives[j];
                            if (o == null) continue;
                            mappedObjectives.Add(new QuestManager.QuestObjectiveData
                            {
                                title = o.title,
                                description = o.description,
                                completed = o.completed
                            });
                        }
                    }

                    var mappedRewards = new List<QuestManager.QuestRewardData>();
                    if (src.rewards != null)
                    {
                        for (int j = 0; j < src.rewards.Count; j++)
                        {
                            var r = src.rewards[j];
                            if (r == null) continue;
                            mappedRewards.Add(new QuestManager.QuestRewardData
                            {
                                icon = r.icon,
                                type = r.type,
                                amount = r.amount,
                                itemName = r.itemName
                            });
                        }
                    }

                    initial.Add(new QuestManager.QuestData
                    {
                        questId = src.questId,
                        title = src.title,
                        location = src.location,
                        completed = src.completed,
                        questTypeLabel = src.questTypeLabel,
                        recommendedLabel = src.recommendedLabel,
                        loreTitle = src.loreTitle,
                        loreDescription = src.loreDescription,
                        loreAuthor = src.loreAuthor,
                        objectives = mappedObjectives,
                        rewards = mappedRewards
                    });
                }
                questManager.ReplaceAllQuests(initial);
            }
            else if (startingQuests != null && startingQuests.Count > 0)
            {
                MergeStartingQuestDetailsIntoManager();
            }

            HandleQuestManagerListChanged(questManager.GetQuestsSnapshot());
            loadedFromManager = true;
        }

        if (!loadedFromManager && questEntries.Count == 0 && startingQuests != null && startingQuests.Count > 0)
        {
            for (int i = 0; i < startingQuests.Count; i++)
            {
                var copy = CloneQuest(startingQuests[i]);
                if (copy != null) questEntries.Add(copy);
            }
        }

        currentQuestFilter = QuestFilter.All;
        UpdateQuestFilterVisuals();
    }

    private void RefreshQuestUI()
    {
        InitializeQuestUIIfNeeded();
        UpdateQuestFilterVisuals();
        UpdateQuestCounters();
        RebuildQuestRows();
        RefreshSelectedQuestDetails();
    }

    private void TryBindQuestManager()
    {
        if (!useQuestManager) return;

        if (questManager == null)
        {
            questManager = QuestManager.Instance != null ? QuestManager.Instance : FindObjectOfType<QuestManager>();
        }

        if (questManager == null || questManagerSubscribed) return;

        questManager.OnQuestListChanged += HandleQuestManagerListChanged;
        questManagerSubscribed = true;
        HandleQuestManagerListChanged(questManager.GetQuestsSnapshot());
    }

    private void UnbindQuestManager()
    {
        if (!questManagerSubscribed || questManager == null) return;

        questManager.OnQuestListChanged -= HandleQuestManagerListChanged;
        questManagerSubscribed = false;
    }

    private void HandleQuestManagerListChanged(List<QuestManager.QuestData> managerData)
    {
        var mapped = new List<QuestEntryData>();
        if (managerData != null)
        {
            for (int i = 0; i < managerData.Count; i++)
            {
                var q = managerData[i];
                if (q == null) continue;
                mapped.Add(new QuestEntryData
                {
                    questId = q.questId,
                    title = q.title,
                    location = q.location,
                    completed = q.completed,
                    questTypeLabel = q.questTypeLabel,
                    recommendedLabel = q.recommendedLabel,
                    loreTitle = q.loreTitle,
                    loreDescription = q.loreDescription,
                    loreAuthor = q.loreAuthor,
                    objectives = MapObjectives(q.objectives),
                    rewards = MapRewards(q.rewards)
                });
            }
        }

        SetQuests(mapped);
    }

    private void UpdateQuestCounters()
    {
        int activeCount = 0;
        int completedCount = 0;

        for (int i = 0; i < questEntries.Count; i++)
        {
            if (questEntries[i] == null) continue;
            if (questEntries[i].completed) completedCount++;
            else activeCount++;
        }

        if (questActiveCountText != null) questActiveCountText.text = activeCount.ToString();
        if (questCompletedCountText != null) questCompletedCountText.text = completedCount.ToString();
    }

    private void OnQuestRowClicked(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId)) return;
        selectedQuestId = questId.Trim();
        RebuildQuestRows();
        RefreshSelectedQuestDetails();
    }

    private bool IsQuestSelected(string questId)
    {
        return !string.IsNullOrWhiteSpace(selectedQuestId)
               && !string.IsNullOrWhiteSpace(questId)
               && string.Equals(selectedQuestId, questId, System.StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshSelectedQuestDetails()
    {
        var selected = GetSelectedVisibleQuest();
        UpdateQuestDetailPanel(selected);
    }

    private QuestEntryData GetSelectedVisibleQuest()
    {
        if (string.IsNullOrWhiteSpace(selectedQuestId)) return null;

        for (int i = 0; i < questEntries.Count; i++)
        {
            var q = questEntries[i];
            if (q == null) continue;
            if (!MatchesQuestFilter(q)) continue;
            if (IsQuestSelected(q.questId))
            {
                return q;
            }
        }

        return null;
    }

    private void UpdateQuestDetailPanel(QuestEntryData quest)
    {
        if (questDetailPanelRoot != null && showQuestDetailOnlyOnSelection)
            questDetailPanelRoot.SetActive(quest != null);

        if (questDetailTypeText != null) questDetailTypeText.text = quest != null ? (quest.questTypeLabel ?? string.Empty) : string.Empty;
        if (questDetailRecommendedText != null) questDetailRecommendedText.text = quest != null ? (quest.recommendedLabel ?? string.Empty) : string.Empty;
        if (questDetailTitleText != null) questDetailTitleText.text = quest != null ? (quest.title ?? string.Empty) : string.Empty;
        if (questDetailLocationText != null) questDetailLocationText.text = quest != null ? (quest.location ?? string.Empty) : string.Empty;
        if (questDetailLoreTitleText != null) questDetailLoreTitleText.text = quest != null ? (quest.loreTitle ?? string.Empty) : string.Empty;
        if (questDetailLoreDescriptionText != null) questDetailLoreDescriptionText.text = quest != null ? (quest.loreDescription ?? string.Empty) : string.Empty;
        if (questDetailLoreAuthorText != null) questDetailLoreAuthorText.text = quest != null ? (quest.loreAuthor ?? string.Empty) : string.Empty;
        UpdateQuestLoreVisibilityAndLayout(quest);

        RebuildQuestObjectiveRows(quest);
        RebuildQuestRewardRows(quest);
    }

    private void UpdateQuestLoreVisibilityAndLayout(QuestEntryData quest)
    {
        bool hasLore = quest != null &&
                       (!string.IsNullOrWhiteSpace(quest.loreTitle)
                        || !string.IsNullOrWhiteSpace(quest.loreDescription)
                        || !string.IsNullOrWhiteSpace(quest.loreAuthor));

        if (collapseQuestLoreWhenEmpty && questDetailLoreRoot != null)
            questDetailLoreRoot.gameObject.SetActive(hasLore);
        else if (questDetailLoreRoot != null)
            questDetailLoreRoot.gameObject.SetActive(true);

        EnsureQuestObjectivesLayoutCache();

        if (!collapseQuestLoreWhenEmpty || questObjectivesSectionRoot == null || !questObjectivesDefaultPosCached) return;

        Vector2 targetPos = questObjectivesDefaultAnchoredPos;
        if (!hasLore)
        {
            float lift = questObjectivesLiftWhenNoLore;
            if (lift < 0f)
            {
                float loreHeight = questDetailLoreRoot != null ? questDetailLoreRoot.rect.height : 0f;
                lift = loreHeight + 16f;
            }
            targetPos.y += lift;
        }

        questObjectivesSectionRoot.anchoredPosition = targetPos;
    }

    private void EnsureQuestObjectivesLayoutCache()
    {
        if (questObjectivesSectionRoot == null)
        {
            if (questObjectivesContainer != null && questObjectivesContainer.parent != null)
                questObjectivesSectionRoot = questObjectivesContainer.parent as RectTransform;
        }

        if (questObjectivesSectionRoot != null && !questObjectivesDefaultPosCached)
        {
            questObjectivesDefaultAnchoredPos = questObjectivesSectionRoot.anchoredPosition;
            questObjectivesDefaultPosCached = true;
        }
    }

    private void RebuildQuestObjectiveRows(QuestEntryData quest)
    {
        ClearSpawnedRows(spawnedObjectiveRows, questObjectivesContainer, questObjectivePrefab);
        if (questObjectivesContainer == null || questObjectivePrefab == null || quest == null || quest.objectives == null) return;

        for (int i = 0; i < quest.objectives.Count; i++)
        {
            var obj = quest.objectives[i];
            if (obj == null) continue;

            var row = Instantiate(questObjectivePrefab, questObjectivesContainer);
            row.SetActive(true);

            var rowUi = row.GetComponent<QuestObjectiveItemUI>();
            if (rowUi == null) rowUi = row.AddComponent<QuestObjectiveItemUI>();
            rowUi.SetData(obj.title, obj.description, obj.completed);

            spawnedObjectiveRows.Add(row);
        }
    }

    private void RebuildQuestRewardRows(QuestEntryData quest)
    {
        ClearSpawnedRows(spawnedRewardRows, questRewardsContainer, questRewardPrefab);
        if (questRewardsContainer == null || questRewardPrefab == null || quest == null || quest.rewards == null) return;

        for (int i = 0; i < quest.rewards.Count; i++)
        {
            var reward = quest.rewards[i];
            if (reward == null) continue;

            var row = Instantiate(questRewardPrefab, questRewardsContainer);
            row.SetActive(true);

            var rowUi = row.GetComponent<QuestRewardItemUI>();
            if (rowUi == null) rowUi = row.AddComponent<QuestRewardItemUI>();
            rowUi.SetData(reward.icon, reward.type, reward.amount, reward.itemName);

            spawnedRewardRows.Add(row);
        }
    }

    private void RebuildQuestRows()
    {
        if (questListContainer == null || questItemPrefab == null) return;

        ClearSpawnedQuestRows();

        for (int i = 0; i < questEntries.Count; i++)
        {
            var quest = questEntries[i];
            if (quest == null) continue;
            if (!MatchesQuestFilter(quest)) continue;

            var row = Instantiate(questItemPrefab, questListContainer);
            row.SetActive(true);

            var rowUI = row.GetComponent<QuestItemUI>();
            if (rowUI == null) rowUI = row.AddComponent<QuestItemUI>();
            bool selected = IsQuestSelected(quest.questId);
            rowUI.SetData(quest.title, quest.location, quest.completed);
            rowUI.SetSelected(selected);

            string capturedQuestId = quest.questId;
            var rowButton = EnsureFilterButton(row);
            if (rowButton != null)
            {
                rowButton.onClick.RemoveAllListeners();
                rowButton.onClick.AddListener(() => OnQuestRowClicked(capturedQuestId));
            }

            spawnedQuestRows.Add(row);
        }
    }

    private void ClearSpawnedQuestRows()
    {
        for (int i = 0; i < spawnedQuestRows.Count; i++)
        {
            if (spawnedQuestRows[i] != null)
                Destroy(spawnedQuestRows[i]);
        }

        if (questListContainer == null)
        {
            spawnedQuestRows.Clear();
            return;
        }

        for (int i = questListContainer.childCount - 1; i >= 0; i--)
        {
            var child = questListContainer.GetChild(i).gameObject;
            if (child == questItemPrefab) continue;

            bool alreadyTracked = false;
            for (int j = 0; j < spawnedQuestRows.Count; j++)
            {
                if (spawnedQuestRows[j] == child)
                {
                    alreadyTracked = true;
                    break;
                }
            }
            if (alreadyTracked) continue;

            Destroy(child);
        }

        spawnedQuestRows.Clear();
    }

    private static void ClearSpawnedRows(List<GameObject> spawned, Transform container, GameObject prefabRef)
    {
        if (spawned != null)
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null) Object.Destroy(spawned[i]);
            }
            spawned.Clear();
        }

        if (container == null) return;

        for (int i = container.childCount - 1; i >= 0; i--)
        {
            var child = container.GetChild(i).gameObject;
            if (prefabRef != null && child == prefabRef) continue;
            Object.Destroy(child);
        }
    }

    private bool MatchesQuestFilter(QuestEntryData quest)
    {
        if (quest == null) return false;
        switch (currentQuestFilter)
        {
            case QuestFilter.Active: return !quest.completed;
            case QuestFilter.Completed: return quest.completed;
            default: return true;
        }
    }

    private void AutoWireQuestUIReferences()
    {
        Transform questRoot = null;

        if (tabs != null)
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] == null || tabs[i].background == null) continue;
                if (!string.Equals(tabs[i].key, "Quest", System.StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(tabs[i].key, "Quests", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                questRoot = tabs[i].background.transform;
                break;
            }
        }

        if (questRoot == null)
            questRoot = FindDeepChildByName(transform, "QuestBackground");

        if (questRoot == null) return;

        if (questListContainer == null)
            questListContainer = FindDescendantByPath(questRoot, "LeftSide/Quest");

        Transform filterRoot = FindDescendantByPath(questRoot, "LeftSide/Filter");
        if (filterRoot == null)
            filterRoot = FindDeepChildByName(questRoot, "Filter");

        if (filterRoot == null) return;

        var activeRow = FindFilterRowByLabel(filterRoot, "ACTIVE");
        var completedRow = FindFilterRowByLabel(filterRoot, "COMPLETED");

        if (questActiveFilterButton == null && activeRow != null)
            questActiveFilterButton = EnsureFilterButton(activeRow.gameObject);
        if (questCompletedFilterButton == null && completedRow != null)
            questCompletedFilterButton = EnsureFilterButton(completedRow.gameObject);

        if (questActiveCountText == null && activeRow != null)
            questActiveCountText = FindCounterTextOnFilterRow(activeRow, "ACTIVE");
        if (questCompletedCountText == null && completedRow != null)
            questCompletedCountText = FindCounterTextOnFilterRow(completedRow, "COMPLETED");

        if (questActiveFilterLabelText == null && activeRow != null)
            questActiveFilterLabelText = FindFilterLabelText(activeRow, "ACTIVE");
        if (questCompletedFilterLabelText == null && completedRow != null)
            questCompletedFilterLabelText = FindFilterLabelText(completedRow, "COMPLETED");

        if ((questActiveCountText == null || questCompletedCountText == null))
        {
            var valueTexts = new List<TextMeshProUGUI>();
            var allTexts = filterRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < allTexts.Length; i++)
            {
                if (allTexts[i] == null) continue;
                string n = allTexts[i].gameObject.name.ToLowerInvariant();
                if (n.Contains("value")) valueTexts.Add(allTexts[i]);
            }

            if (questActiveCountText == null && valueTexts.Count > 0) questActiveCountText = valueTexts[0];
            if (questCompletedCountText == null && valueTexts.Count > 1) questCompletedCountText = valueTexts[1];
        }

        Transform rightSide = FindDescendantByPath(questRoot, "RightSide");
        if (rightSide != null)
        {
            if (questDetailScrollRect == null)
                questDetailScrollRect = rightSide.GetComponent<ScrollRect>();

            if (questDetailPanelRoot == null) questDetailPanelRoot = rightSide.gameObject;

            Transform body = FindDeepChildByName(rightSide, "Body");
            Transform lore = body != null ? FindDeepChildByName(body, "Lore") : FindDeepChildByName(rightSide, "Lore");
            if (questDetailLoreRoot == null)
                questDetailLoreRoot = lore as RectTransform;

            if (questDetailTypeText == null)
                questDetailTypeText = FindDeepTextByName(rightSide, "Type") ?? FindDeepTextByContains(rightSide, "main quest");
            if (questDetailRecommendedText == null)
                questDetailRecommendedText = FindDeepTextByName(rightSide, "Lvl") ?? FindDeepTextByContains(rightSide, "recommended");
            if (questDetailTitleText == null)
                questDetailTitleText = FindDeepTextByName(rightSide, "Title") ?? FindDeepTextByContains(rightSide, "ashen ritual");
            if (questDetailLocationText == null)
                questDetailLocationText = FindDeepTextByName(rightSide, "Location") ?? FindDeepTextByContains(rightSide, "hub");

            if (questDetailLoreTitleText == null)
                questDetailLoreTitleText = FindDeepTextByName(lore, "Title desc")
                                           ?? FindDeepTextByObjectNameContains(lore, "title desc")
                                           ?? FindDeepTextByName(lore, "Title")
                                           ?? FindDeepTextByObjectNameContains(lore, "title")
                                           ?? FindDeepTextByContains(rightSide, "legend");
            if (questDetailLoreDescriptionText == null)
                questDetailLoreDescriptionText = FindDeepTextByName(lore, "Desc")
                                                 ?? FindDeepTextByObjectNameContains(lore, "desc")
                                                 ?? FindDeepTextByContains(rightSide, "bells toll");
            if (questDetailLoreAuthorText == null)
                questDetailLoreAuthorText = FindDeepTextByName(lore, "Cit")
                                            ?? FindDeepTextByObjectNameContains(lore, "cit", "author")
                                            ?? FindDeepTextByContains(rightSide, "rosario");

            if (questObjectivesContainer == null)
                questObjectivesContainer = FindDeepChildByName(rightSide, "CurrentObjectivesContainer")
                                          ?? FindDeepChildByName(rightSide, "CurrentObjectives")
                                          ?? FindDeepChildByName(rightSide, "Objectives")
                                          ?? FindDeepChildByName(rightSide, "ObjectiveContainer")
                                          ?? FindDeepChildByName(rightSide, "ObjectiveList");

            if (questObjectivesSectionRoot == null)
            {
                var objectivesRoot = FindDeepChildByName(rightSide, "Objectives");
                if (objectivesRoot != null) questObjectivesSectionRoot = objectivesRoot as RectTransform;
                else if (questObjectivesContainer != null && questObjectivesContainer.parent != null)
                    questObjectivesSectionRoot = questObjectivesContainer.parent as RectTransform;
            }

            if (questRewardsContainer == null)
                questRewardsContainer = FindDeepChildByName(rightSide, "RewardContainer")
                                       ?? FindDeepChildByName(rightSide, "Rewards")
                                       ?? FindDeepChildByName(rightSide, "RewardList");
        }

        if (questObjectivesContainer != null && questObjectivePrefab == null && questObjectivesContainer.childCount > 0)
            questObjectivePrefab = questObjectivesContainer.GetChild(0).gameObject;

        if (questRewardsContainer != null && questRewardPrefab == null && questRewardsContainer.childCount > 0)
            questRewardPrefab = questRewardsContainer.GetChild(0).gameObject;
    }

    private void RestoreQuestScrollPosition(float normalizedY)
    {
        if (questDetailScrollRect == null || !questDetailScrollRect.vertical) return;
        if (questDetailScrollRect.content == null) return;

        normalizedY = Mathf.Clamp01(normalizedY);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(questDetailScrollRect.content);
        Canvas.ForceUpdateCanvases();
        questDetailScrollRect.StopMovement();
        questDetailScrollRect.verticalNormalizedPosition = normalizedY;
        questTargetScrollNormalized = questDetailScrollRect.verticalNormalizedPosition;
        questTargetScrollInitialized = true;
    }

    private void UpdateQuestMouseWheelSmoothScroll()
    {
        if (!smoothQuestMouseWheel) return;
        if (questDetailScrollRect == null || !questDetailScrollRect.vertical) return;
        if (Mouse.current == null) return;
        if (!IsQuestTabActive()) return;

        if (!questTargetScrollInitialized)
        {
            questTargetScrollNormalized = questDetailScrollRect.verticalNormalizedPosition;
            questTargetScrollInitialized = true;
        }

        // Evita doppio effetto con lo scroll built-in a step.
        if (questDetailScrollRect.scrollSensitivity != 0f)
            questDetailScrollRect.scrollSensitivity = 0f;

        var viewport = questDetailScrollRect.viewport != null ? questDetailScrollRect.viewport : questDetailScrollRect.transform as RectTransform;
        if (viewport == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        bool pointerOverViewport = RectTransformUtility.RectangleContainsScreenPoint(viewport, mousePos, null);
        if (!pointerOverViewport) return;

        float wheel = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(wheel) > 0.01f)
        {
            // wheel > 0 = su, wheel < 0 = giu
            questTargetScrollNormalized = Mathf.Clamp01(questTargetScrollNormalized + (wheel * questMouseWheelStepNormalized / 120f));
        }

        float current = questDetailScrollRect.verticalNormalizedPosition;
        float t = 1f - Mathf.Exp(-questMouseWheelSmoothSpeed * Time.unscaledDeltaTime);
        float next = Mathf.Lerp(current, questTargetScrollNormalized, t);
        questDetailScrollRect.verticalNormalizedPosition = next;
    }

    private bool IsQuestTabActive()
    {
        if (tabs == null || tabs.Length == 0) return false;
        for (int i = 0; i < tabs.Length; i++)
        {
            var tab = tabs[i];
            if (tab == null || tab.background == null) continue;
            if (!string.Equals(tab.key, "Quest", System.StringComparison.OrdinalIgnoreCase)
                && !string.Equals(tab.key, "Quests", System.StringComparison.OrdinalIgnoreCase))
                continue;

            return tab.background.activeInHierarchy;
        }

        return false;
    }

    private void WireQuestFilterButtons()
    {
        if (questActiveFilterButton != null)
        {
            questActiveFilterButton.onClick.RemoveListener(SetQuestFilterActive);
            questActiveFilterButton.onClick.AddListener(SetQuestFilterActive);
        }

        if (questCompletedFilterButton != null)
        {
            questCompletedFilterButton.onClick.RemoveListener(SetQuestFilterCompleted);
            questCompletedFilterButton.onClick.AddListener(SetQuestFilterCompleted);
        }
    }

    private void CacheQuestFilterBaseColors()
    {
        if (questFilterBaseColorsCached) return;

        if (questActiveFilterLabelText != null)
            questActiveFilterBaseColor = questActiveFilterLabelText.color;
        if (questCompletedFilterLabelText != null)
            questCompletedFilterBaseColor = questCompletedFilterLabelText.color;

        questFilterBaseColorsCached = true;
    }

    private void UpdateQuestFilterVisuals()
    {
        CacheQuestFilterBaseColors();

        if (questActiveFilterLabelText != null)
        {
            questActiveFilterLabelText.color = currentQuestFilter == QuestFilter.Active
                ? questFilterSelectedColor
                : questActiveFilterBaseColor;
        }

        if (questCompletedFilterLabelText != null)
        {
            questCompletedFilterLabelText.color = currentQuestFilter == QuestFilter.Completed
                ? questFilterSelectedColor
                : questCompletedFilterBaseColor;
        }
    }

    private static Button EnsureFilterButton(GameObject target)
    {
        if (target == null) return null;

        var button = target.GetComponent<Button>();
        if (button == null) button = target.AddComponent<Button>();

        if (button.targetGraphic == null)
        {
            var graphic = target.GetComponent<Graphic>();
            if (graphic == null) graphic = target.GetComponentInChildren<Graphic>(true);
            button.targetGraphic = graphic;
        }

        return button;
    }

    private static Transform FindFilterRowByLabel(Transform filterRoot, string label)
    {
        if (filterRoot == null || string.IsNullOrEmpty(label)) return null;

        var texts = filterRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null) continue;
            if (!string.Equals(texts[i].text.Trim(), label, System.StringComparison.OrdinalIgnoreCase)) continue;
            return texts[i].transform.parent != null ? texts[i].transform.parent : texts[i].transform;
        }

        return null;
    }

    private static TextMeshProUGUI FindCounterTextOnFilterRow(Transform row, string rowLabel)
    {
        if (row == null) return null;

        var texts = row.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null) continue;
            if (!string.Equals(texts[i].text.Trim(), rowLabel, System.StringComparison.OrdinalIgnoreCase))
                return texts[i];
        }

        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null) continue;
            if (texts[i].gameObject.name.ToLowerInvariant().Contains("value"))
                return texts[i];
        }

        return null;
    }

    private static TextMeshProUGUI FindFilterLabelText(Transform row, string label)
    {
        if (row == null || string.IsNullOrEmpty(label)) return null;

        var texts = row.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null) continue;
            if (string.Equals(texts[i].text.Trim(), label, System.StringComparison.OrdinalIgnoreCase))
                return texts[i];
        }

        return null;
    }

    private static Transform FindDescendantByPath(Transform root, string path)
    {
        if (root == null || string.IsNullOrEmpty(path)) return null;

        var parts = path.Split('/');
        var current = root;
        for (int i = 0; i < parts.Length; i++)
        {
            current = current.Find(parts[i]);
            if (current == null) return null;
        }

        return current;
    }

    private static Transform FindDeepChildByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;

        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (string.Equals(child.name, name, System.StringComparison.OrdinalIgnoreCase))
                return child;

            var nested = FindDeepChildByName(child, name);
            if (nested != null) return nested;
        }

        return null;
    }

    private static TextMeshProUGUI FindDeepTextByContains(Transform root, string value)
    {
        if (root == null || string.IsNullOrWhiteSpace(value)) return null;

        string pattern = value.Trim();
        var texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null) continue;
            if (texts[i].text != null && texts[i].text.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return texts[i];
        }

        return null;
    }

    private static TextMeshProUGUI FindDeepTextByName(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName)) return null;

        Transform t = FindDeepChildByName(root, objectName);
        if (t == null) return null;
        var own = t.GetComponent<TextMeshProUGUI>();
        if (own != null) return own;
        return t.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private static TextMeshProUGUI FindDeepTextByObjectNameContains(Transform root, params string[] objectNameParts)
    {
        if (root == null || objectNameParts == null || objectNameParts.Length == 0) return null;

        var allTransforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            var t = allTransforms[i];
            if (t == null) continue;
            string name = t.name ?? string.Empty;

            bool matches = true;
            for (int j = 0; j < objectNameParts.Length; j++)
            {
                string part = objectNameParts[j];
                if (string.IsNullOrWhiteSpace(part)) continue;
                if (name.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    matches = false;
                    break;
                }
            }

            if (!matches) continue;

            var own = t.GetComponent<TextMeshProUGUI>();
            if (own != null) return own;

            var nested = t.GetComponentInChildren<TextMeshProUGUI>(true);
            if (nested != null) return nested;
        }

        return null;
    }

    private static QuestEntryData CloneQuest(QuestEntryData source)
    {
        if (source == null) return null;
        return new QuestEntryData
        {
            questId = source.questId,
            title = source.title,
            location = source.location,
            completed = source.completed,
            questTypeLabel = source.questTypeLabel,
            recommendedLabel = source.recommendedLabel,
            loreTitle = source.loreTitle,
            loreDescription = source.loreDescription,
            loreAuthor = source.loreAuthor,
            objectives = CloneObjectives(source.objectives),
            rewards = CloneRewards(source.rewards)
        };
    }

    private static List<QuestObjectiveEntryData> CloneObjectives(List<QuestObjectiveEntryData> source)
    {
        var result = new List<QuestObjectiveEntryData>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] == null) continue;
            result.Add(new QuestObjectiveEntryData
            {
                title = source[i].title,
                description = source[i].description,
                completed = source[i].completed
            });
        }

        return result;
    }

    private static List<QuestRewardEntryData> CloneRewards(List<QuestRewardEntryData> source)
    {
        var result = new List<QuestRewardEntryData>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] == null) continue;
            result.Add(new QuestRewardEntryData
            {
                icon = source[i].icon,
                type = source[i].type,
                amount = source[i].amount,
                itemName = source[i].itemName
            });
        }

        return result;
    }

    private static List<QuestObjectiveEntryData> MapObjectives(List<QuestManager.QuestObjectiveData> source)
    {
        var result = new List<QuestObjectiveEntryData>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] == null) continue;
            result.Add(new QuestObjectiveEntryData
            {
                title = source[i].title,
                description = source[i].description,
                completed = source[i].completed
            });
        }

        return result;
    }

    private static List<QuestRewardEntryData> MapRewards(List<QuestManager.QuestRewardData> source)
    {
        var result = new List<QuestRewardEntryData>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] == null) continue;
            result.Add(new QuestRewardEntryData
            {
                icon = source[i].icon,
                type = source[i].type,
                amount = source[i].amount,
                itemName = source[i].itemName
            });
        }

        return result;
    }

    private int FindQuestIndexById(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return -1;

        for (int i = 0; i < questEntries.Count; i++)
        {
            if (questEntries[i] == null) continue;
            if (string.Equals(questEntries[i].questId, questId, System.StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static string NormalizeQuestId(string questId, string title, string location)
    {
        if (!string.IsNullOrWhiteSpace(questId))
            return questId.Trim();

        string safeTitle = string.IsNullOrWhiteSpace(title) ? "Quest" : title.Trim();
        string safeLocation = string.IsNullOrWhiteSpace(location) ? "Unknown" : location.Trim();
        return safeTitle + "|" + safeLocation;
    }

    private void MergeStartingQuestDetailsIntoManager()
    {
        if (questManager == null || startingQuests == null || startingQuests.Count == 0) return;

        var managerList = questManager.GetQuestsSnapshot();
        if (managerList == null || managerList.Count == 0) return;

        var changed = false;

        for (int i = 0; i < managerList.Count; i++)
        {
            var m = managerList[i];
            if (m == null) continue;

            string managerId = NormalizeQuestId(m.questId, m.title, m.location);
            QuestEntryData source = null;

            for (int j = 0; j < startingQuests.Count; j++)
            {
                var s = startingQuests[j];
                if (s == null) continue;
                string startId = NormalizeQuestId(s.questId, s.title, s.location);
                if (string.Equals(startId, managerId, System.StringComparison.OrdinalIgnoreCase))
                {
                    source = s;
                    break;
                }
            }

            if (source == null) continue;

            bool managerHasObjectives = m.objectives != null && m.objectives.Count > 0;
            bool managerHasRewards = m.rewards != null && m.rewards.Count > 0;

            if (!managerHasObjectives && source.objectives != null && source.objectives.Count > 0)
            {
                m.objectives = new List<QuestManager.QuestObjectiveData>();
                for (int k = 0; k < source.objectives.Count; k++)
                {
                    var o = source.objectives[k];
                    if (o == null) continue;
                    m.objectives.Add(new QuestManager.QuestObjectiveData
                    {
                        title = o.title,
                        description = o.description,
                        completed = o.completed
                    });
                }
                changed = true;
            }

            if (!managerHasRewards && source.rewards != null && source.rewards.Count > 0)
            {
                m.rewards = new List<QuestManager.QuestRewardData>();
                for (int k = 0; k < source.rewards.Count; k++)
                {
                    var r = source.rewards[k];
                    if (r == null) continue;
                    m.rewards.Add(new QuestManager.QuestRewardData
                    {
                        icon = r.icon,
                        type = r.type,
                        amount = r.amount,
                        itemName = r.itemName
                    });
                }
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(m.questTypeLabel) && !string.IsNullOrWhiteSpace(source.questTypeLabel))
            {
                m.questTypeLabel = source.questTypeLabel;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(m.recommendedLabel) && !string.IsNullOrWhiteSpace(source.recommendedLabel))
            {
                m.recommendedLabel = source.recommendedLabel;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(m.loreTitle) && !string.IsNullOrWhiteSpace(source.loreTitle))
            {
                m.loreTitle = source.loreTitle;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(m.loreDescription) && !string.IsNullOrWhiteSpace(source.loreDescription))
            {
                m.loreDescription = source.loreDescription;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(m.loreAuthor) && !string.IsNullOrWhiteSpace(source.loreAuthor))
            {
                m.loreAuthor = source.loreAuthor;
                changed = true;
            }
        }

        if (changed)
            questManager.ReplaceAllQuests(managerList);
    }

    private void TryEditorAutoAssignQuestPrefabs()
    {
#if UNITY_EDITOR
        if (questItemPrefab == null)
            questItemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Quest.prefab");

        if (questObjectivePrefab == null)
        {
            var byName = AssetDatabase.FindAssets("t:prefab *Objective*");
            if (byName != null && byName.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(byName[0]);
                questObjectivePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
        }

        if (questRewardPrefab == null)
        {
            var byName = AssetDatabase.FindAssets("t:prefab *Reward*");
            if (byName != null && byName.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(byName[0]);
                questRewardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
        }
#endif
    }

    /// <summary>
    /// Costruisce da zero un numero esatto di slot usando il prefab.
    /// Utile quando conosci già la capacità massima dell'inventario.
    /// </summary>
    public void BuildSlots(int count)
    {
        if (slotParent == null) slotParent = transform;

        if (slotPrefab == null || slotParent == null)
        {
            Debug.LogWarning("InventoryUI: slotPrefab o slotParent non assegnato.");
            return;
        }

        // elimina i vecchi slot presenti come figli
        for (int i = slotParent.childCount - 1; i >= 0; i--)
        {
            Destroy(slotParent.GetChild(i).gameObject);
        }
        slots.Clear();

        EnsureSlots(count);
        ClearAllSlots();
    }

    /// <summary>
    /// Pulisce tutti gli slot inventario.
    /// </summary>
    private void ClearAllSlots()
    {
        if (slots == null) return;
        foreach (var slot in slots)
        {
            if (slot != null)
            {
                slot.Clear();
            }
        }
    }

    /// <summary>
    /// Garantisce che esistano almeno 'required' slot, istanziando i prefab mancanti.
    /// </summary>
    private void EnsureSlots(int required)
    {
        if (slotParent == null) slotParent = transform;

        if (slotParent == null) return;
        if (slotPrefab == null)
        {
            if (slots.Count < required)
            {
                // Se manca il prefab, prova a clonare il primo slot esistente come template
                if (slots.Count > 0)
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
                else
                {
                    Debug.LogWarning($"InventoryUI: servono {required} slot ma slotPrefab non Ã¨ assegnato e non ci sono slot da clonare.");
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

    // --------- EQUIP SLOT VISUALS (usa invSlot prefab) ---------
    private void BuildEquipSlotsIfNeeded()
    {
        if (equipSlotsBuilt) return;
        // Right side (3)
        rightEquipSlots[0] = CreateEquipSlot(rightEquipContainer);
        rightEquipSlots[1] = CreateEquipSlot(rightEquipContainer2);
        rightEquipSlots[2] = CreateEquipSlot(rightEquipContainer3);

        // Left side (3)
        leftEquipSlots[0] = CreateEquipSlot(leftEquipContainer);
        leftEquipSlots[1] = CreateEquipSlot(leftEquipContainer2);
        leftEquipSlots[2] = CreateEquipSlot(leftEquipContainer3);

        // Bottom (3 usables)
        bottomEquipSlots[0] = CreateEquipSlot(bottomEquipContainer);
        bottomEquipSlots[1] = CreateEquipSlot(bottomEquipContainer2);
        bottomEquipSlots[2] = CreateEquipSlot(bottomEquipContainer3);

        // Top (3 slot, placeholder magie)
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
        // lasciali attivi anche se vuoti, così si vede lo sfondo
        if (hudRightSlot) hudRightSlot.gameObject.SetActive(true);
        if (hudLeftSlot) hudLeftSlot.gameObject.SetActive(true);
        if (hudBottomSlot) hudBottomSlot.gameObject.SetActive(true);
        if (hudTopSlot) hudTopSlot.gameObject.SetActive(true);
        hudSlotsBuilt = true;
    }

    private InventorySlot CreateEquipSlot(Transform parent)
    {
        if (slotPrefab == null || parent == null) return null;
        // evita doppioni se già presente
        var existing = parent.GetComponentInChildren<InventorySlot>();
        if (existing != null) return existing;

        var slot = Instantiate(slotPrefab, parent);
        slot.Init(-1, this);
        slot.SetDisplayOnly(true);
        slot.gameObject.SetActive(true);
        // disattiva raycast sul background per evitare selezioni
        var img = slot.GetComponent<Image>();
        if (img != null) img.raycastTarget = false;
        return slot;
    }

    [System.Serializable]
    private class TabEntry
    {
        public string key;
        public TextMeshProUGUI label;
        public GameObject background;
    }

    // --------- DRAG & DROP / SWAP LOGIC ----------

    private bool IsValidIndex(int index) => index >= 0 && index < slots.Count;

    private bool HasItem(int index) => index >= 0 && index < currentItems.Count && currentItems[index] != null;

    private void SwapItems(int a, int b)
    {
        if (!IsValidIndex(a) || !IsValidIndex(b) || a == b) return;

        // Estende currentItems se si droppa su slot oltre la lista corrente
        int maxIndex = Mathf.Max(a, b);
        while (currentItems.Count <= maxIndex)
        {
            currentItems.Add(null);
        }

        var temp = currentItems[a];
        currentItems[a] = currentItems[b];
        currentItems[b] = temp;

        // Mantieni sincronizzata anche la lista sorgente
        if (sourceItems.Count <= a) ExtendSourceToIndex(a);
        if (sourceItems.Count <= b) ExtendSourceToIndex(b);
        var tempSrc = sourceItems[a];
        sourceItems[a] = sourceItems[b];
        sourceItems[b] = tempSrc;

        RefreshSlot(a);
        RefreshSlot(b);
        RefreshDetailSelection();
    }

    private void ExtendSourceToIndex(int index)
    {
        while (sourceItems.Count <= index) sourceItems.Add(null);
    }

    private void RefreshSlot(int index)
    {
        if (!IsValidIndex(index) || index >= slots.Count) return;
        var item = currentItems[index];
        if (item != null)
        {
            slots[index].Setup(GetItemIcon(item), item.amount, IsItemEquipped(item));
        }
        else
        {
            slots[index].Clear();
        }
        // Mantieni lo slot attivo anche se vuoto, cos\u00ec la griglia non collassa
        slots[index].gameObject.SetActive(true);
        UpdateEquipButtonState();
    }

    // ------- DETAIL PANEL --------
    private void ClearDetailPanel()
    {
        currentSelectedIndex = -1;

        if (weaponDetailRoot != null) weaponDetailRoot.SetActive(false);
        if (itemDetailRoot != null) itemDetailRoot.SetActive(false);

        if (detailIcon != null)
        {
            detailIcon.enabled = false;
            detailIcon.sprite = null;
        }

        if (detailTitle != null) detailTitle.text = string.Empty;
        if (detailDescription != null) detailDescription.text = string.Empty;

        if (weaponStatsRoot != null) weaponStatsRoot.SetActive(false);
        if (weaponDamageText != null) weaponDamageText.text = string.Empty;
        if (weaponCriticalText != null) weaponCriticalText.text = string.Empty;
        if (weaponWeightText != null) weaponWeightText.text = string.Empty;
        if (weaponScalingText != null) weaponScalingText.text = string.Empty;
        if (weaponRequirementsText != null) weaponRequirementsText.text = string.Empty;

        if (detailRoot != null) detailRoot.SetActive(false);
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

        // disattiva entrambi i pannelli specifici, poi attiva quello corretto
        if (weaponDetailRoot != null) weaponDetailRoot.SetActive(false);
        if (itemDetailRoot != null) itemDetailRoot.SetActive(false);

        // Preferisce i dati dell'arma se presenti, altrimenti quelli degli usabili o degli item generici
        Sprite icon = GetItemIcon(item);
        string title = item.title;
        string description = item.description;

        var weapon = item.weaponData;
        var usable = item.usableData;
        var itemData = item.itemData;

        if (weapon != null)
        {
            if (weapon.icon != null) icon = weapon.icon;
            if (!string.IsNullOrEmpty(weapon.weaponName)) title = weapon.weaponName;
            if (!string.IsNullOrEmpty(weapon.description)) description = weapon.description;

            if (weaponDetailRoot != null) weaponDetailRoot.SetActive(true);
            if (weaponImage != null) weaponImage.sprite = icon;
            if (weaponTitle != null) weaponTitle.text = title ?? string.Empty;
            if (weaponDesc != null) weaponDesc.text = description ?? string.Empty;
            if (weaponDamageText != null) weaponDamageText.text = weapon.physicalDamage.ToString();
            if (weaponCriticalText != null) weaponCriticalText.text = weapon.criticalHit.ToString("0.##");
            if (weaponWeightText != null) weaponWeightText.text = weapon.weight.ToString("0.##");
            if (weaponScalingText != null) weaponScalingText.text = weapon.scaling ?? string.Empty;
            if (weaponRequirementsText != null) weaponRequirementsText.text = weapon.requirements ?? string.Empty;

            // weaponStatsRoot è la sezione stat arma: lasciala attiva
            if (weaponStatsRoot != null) weaponStatsRoot.SetActive(true);

            // Aggiorna anche il blocco comune (fallback per sicurezza)
            if (detailIcon != null) { detailIcon.enabled = icon != null; detailIcon.sprite = icon; }
            if (detailTitle != null) detailTitle.text = title ?? string.Empty;
            if (detailDescription != null) detailDescription.text = description ?? string.Empty;
            return;
        }

        // Item o Usable
        if (usable != null)
        {
            if (usable.icon != null) icon = usable.icon;
            if (!string.IsNullOrEmpty(usable.itemName)) title = usable.itemName;
            if (!string.IsNullOrEmpty(usable.description)) description = usable.description;
        }
        else if (itemData != null)
        {
            if (itemData.icon != null) icon = itemData.icon;
            if (!string.IsNullOrEmpty(itemData.itemName)) title = itemData.itemName;
            if (!string.IsNullOrEmpty(itemData.description)) description = itemData.description;
        }

        if (itemDetailRoot != null) itemDetailRoot.SetActive(true);
        if (itemImage != null) itemImage.sprite = icon;
        if (itemTitle != null) itemTitle.text = title ?? string.Empty;
        if (itemDesc != null) itemDesc.text = description ?? string.Empty;

        // pannello item: nascondi la sezione stat arma
        if (weaponStatsRoot != null) weaponStatsRoot.SetActive(false);

        // Aggiorna anche il blocco comune (se presente) per garantire visibilità
        if (detailIcon != null) { detailIcon.enabled = icon != null; detailIcon.sprite = icon; }
        if (detailTitle != null) detailTitle.text = title ?? string.Empty;
        if (detailDescription != null) detailDescription.text = description ?? string.Empty;

        // Aggiorna la croce se l'Equipment è visibile
        RefreshEquipmentCross();

        // Aggiorna lo stato del pulsante equip
        UpdateEquipButtonState();
    }


    // ------ EQUIPMENT CROSS SYNC ------
    public void RefreshEquipmentCross()
    {
        BuildEquipSlotsIfNeeded();
        BuildHudSlotsIfNeeded();

        if (playerInventory == null)
            playerInventory = FindObjectOfType<PlayerInventory>();

        // right/left hands
        if (playerInventory != null)
        {
            var rightEquipped = playerInventory.GetWeaponForHand(Hand.Right);
            var leftEquipped = playerInventory.GetWeaponForHand(Hand.Left);
            var rightFrontIcon = rightEquipped != null ? rightEquipped.icon : null;
            var leftFrontIcon = leftEquipped != null ? leftEquipped.icon : null;

            // Back layer = solo sfondo.
            SetBackLayerIcon(hudCrossRight);
            SetBackLayerIcon(hudCrossLeft);

            UpdateEquipVisuals(rightEquipSlots, playerInventory.rightLoadout);
            UpdateEquipVisuals(leftEquipSlots, playerInventory.leftLoadout);

            // Front layer (invSlot) = icona equipaggiata, inclusa default/unarmed.
            UpdateEquipVisual(hudRightSlot, rightFrontIcon, 1);
            UpdateEquipVisual(hudLeftSlot, leftFrontIcon, 1);
        }

        // bottom: mostra solo l'usabile equipaggiato, niente fallback da inventario
        Sprite usableIcon = null;
        if (playerInventory != null && playerInventory.GetCurrentUsable() != null)
        {
            usableIcon = playerInventory.GetCurrentUsable().icon;
        }
        SetBackLayerIcon(hudCrossBottom);
        UpdateEquipVisuals(bottomEquipSlots, playerInventory.usableLoadout);
        UpdateHudVisual(hudBottomSlot, usableIcon);

        // top: placeholder magie (3 slot per coerenza con gli altri lati)
        SetBackLayerIcon(hudCrossTop);
        for (int i = 0; i < topEquipSlots.Length; i++)
            UpdateEquipVisual(topEquipSlots[i], null, 0);
        UpdateHudVisual(hudTopSlot, null);

        ApplyEquipmentCrossFocusVisual();
    }

    private void SetBackLayerIcon(Image target)
    {
        if (target == null) return;

        // Layer dietro sempre vuoto: l'icona deve essere solo nel prefab InventorySlot frontale.
        target.sprite = null;
        target.enabled = false;
    }

    private void UpdateEquipVisual(InventorySlot slot, Sprite icon, int amount)
    {
        if (slot == null) return;
        if (icon != null)
            slot.Setup(icon, amount);
        else
            slot.Clear();
    }

    private void UpdateHudVisual(InventorySlot slot, Sprite icon)
    {
        if (slot == null) return;
        slot.gameObject.SetActive(true); // mostra il fondo anche se vuoto
        if (icon != null)
        {
            slot.Setup(icon, 1);
        }
        else
        {
            slot.Clear();
        }
    }

    private void UpdateEquipVisuals(InventorySlot[] slots, WeaponItem[] loadout)
    {
        if (slots == null || loadout == null) return;
        int len = Mathf.Min(slots.Length, loadout.Length);
        for (int i = 0; i < len; i++)
        {
            var icon = loadout[i] != null ? loadout[i].icon : null;
            UpdateEquipVisual(slots[i], icon, 1);
        }
    }

    private void UpdateEquipVisuals(InventorySlot[] slots, UsableItemData[] loadout)
    {
        if (slots == null || loadout == null) return;
        int len = Mathf.Min(slots.Length, loadout.Length);
        for (int i = 0; i < len; i++)
        {
            var icon = loadout[i] != null ? loadout[i].icon : null;
            UpdateEquipVisual(slots[i], icon, 1);
        }
    }

    private void UpdateEquipButtonState()
    {
        bool hasSelection = currentSelectedIndex >= 0 && HasItem(currentSelectedIndex);

        if (equipWeaponButton != null)
        {
            bool showW = (currentEquipTarget == EquipTarget.Right || currentEquipTarget == EquipTarget.Left) && currentFilter == Filter.Weapons;
            equipWeaponButton.gameObject.SetActive(showW);
            equipWeaponButton.interactable = showW && hasSelection && currentSelectedIndex < currentItems.Count && currentItems[currentSelectedIndex]?.weaponData != null;
        }

        if (equipUsableButton != null)
        {
            bool showU = (currentEquipTarget == EquipTarget.Bottom) && currentFilter == Filter.Usables;
            equipUsableButton.gameObject.SetActive(showU);
            equipUsableButton.interactable = showU && hasSelection && currentSelectedIndex < currentItems.Count && currentItems[currentSelectedIndex]?.usableData != null;
        }
    }

    private void ResetEquipTarget()
    {
        currentEquipTarget = EquipTarget.None;
        if (equipWeaponButton != null)
        {
            equipWeaponButton.gameObject.SetActive(false);
            equipWeaponButton.interactable = false;
        }
        if (equipUsableButton != null)
        {
            equipUsableButton.gameObject.SetActive(false);
            equipUsableButton.interactable = false;
        }
    }

    public bool IsEquipmentCrossModeActive()
    {
        if (equipmentBackground == null) return false;
        bool equipVisible = equipmentBackground.activeInHierarchy;
        bool invHidden = inventoryBackground == null || !inventoryBackground.activeInHierarchy;
        return equipVisible && invHidden;
    }

    public void FocusEquipmentCrossDefault()
    {
        EnsurePlayerInventory();
        int idx = playerInventory != null ? Mathf.Clamp(playerInventory.currentRightIndex, 0, 2) : 0;
        SetEquipmentCrossFocus(EquipCrossFocus.Right, idx);
    }

    public void NavigateEquipmentRight()
    {
        MoveEquipmentFocus(Vector2.right);
    }

    public void NavigateEquipmentLeft()
    {
        MoveEquipmentFocus(Vector2.left);
    }

    public void NavigateEquipmentDown()
    {
        MoveEquipmentFocus(Vector2.down);
    }

    public void NavigateEquipmentUp()
    {
        MoveEquipmentFocus(Vector2.up);
    }

    public void ConfirmEquipmentSelection()
    {
        switch (equipCrossFocus)
        {
            case EquipCrossFocus.Right:
                OnEquipRight(GetCurrentCrossIndex(EquipCrossFocus.Right));
                break;
            case EquipCrossFocus.Left:
                OnEquipLeft(GetCurrentCrossIndex(EquipCrossFocus.Left));
                break;
            case EquipCrossFocus.Bottom:
                OnEquipBottom(GetCurrentCrossIndex(EquipCrossFocus.Bottom));
                break;
            case EquipCrossFocus.Top:
                OnEquipTop(GetCurrentCrossIndex(EquipCrossFocus.Top));
                break;
        }
    }

    private Sprite GetItemIcon(InventoryItem item)
    {
        if (item == null) return null;
        if (item.icon != null) return item.icon;
        if (item.weaponData != null && item.weaponData.icon != null) return item.weaponData.icon;
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


    // Mouse: select origin on pointer down, start drag to move preview
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
        {
            iconSize = slots[index].GetIconSize();
        }
        CreateDragPreview(GetItemIcon(currentItems[index]), eventData, iconSize);
    }

    public void HandleSlotDrag(PointerEventData eventData)
    {
        if (activeDragPreview != null)
        {
            activeDragPreview.rectTransform.position = eventData.position;
        }
    }

    public void HandleSlotEndDrag()
    {
        ClearDragPreview();
        dragOriginIndex = -1;
    }

    public void HandleSlotDrop(int targetIndex)
    {
        if (dragOriginIndex >= 0)
        {
            SwapItems(dragOriginIndex, targetIndex);
        }
        ClearDragPreview();
        dragOriginIndex = -1;
        ShowItemDetailsByIndex(targetIndex);
    }

    // Gamepad: first Submit marks selection, second Submit on another slot swaps
    public void HandleSlotSelected(int index)
    {
        // highlight handled by EventSystem; we only track and show details
        if (HasItem(index))
        {
            ApplyPadFocusVisual(index);
            ShowItemDetailsByIndex(index);
            UpdateEquipButtonState();
        }
        else
        {
            ApplyPadFocusVisual(index);
            ClearDetailPanel();
            UpdateEquipButtonState();
        }
    }

    public void HandleSlotSubmit(int index)
    {
        if (selectedPadIndex < 0)
        {
            if (HasItem(index))
            {
                selectedPadIndex = index; // pick up
                ShowItemDetailsByIndex(index);
            }
        }
        else
        {
            SwapItems(selectedPadIndex, index);
            selectedPadIndex = -1;
            ShowItemDetailsByIndex(index);
        }
    }

    public void FocusDefaultPadSlot()
    {
        if (slots == null || slots.Count == 0) return;

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
        if (slots == null || slots.Count == 0) return;
        int dir = direction >= 0 ? 1 : -1;

        int start = padFocusIndex;
        if (start < 0 || start >= slots.Count) start = 0;

        int next = (start + dir + slots.Count) % slots.Count;
        SetPadFocus(next);
    }

    public void MovePadFocusVertical(int direction)
    {
        if (slots == null || slots.Count == 0) return;
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
        // Priorita' ai pulsanti equip quando visibili/interagibili.
        if (equipWeaponButton != null && equipWeaponButton.gameObject.activeInHierarchy && equipWeaponButton.interactable)
        {
            OnEquipWeaponButtonClick();
            return;
        }
        if (equipUsableButton != null && equipUsableButton.gameObject.activeInHierarchy && equipUsableButton.interactable)
        {
            OnEquipUsableButtonClick();
            return;
        }

        if (padFocusIndex < 0 || padFocusIndex >= slots.Count)
        {
            FocusDefaultPadSlot();
            if (padFocusIndex < 0 || padFocusIndex >= slots.Count) return;
        }

        HandleSlotSubmit(padFocusIndex);
        SetPadFocus(padFocusIndex);
    }

    private void SetPadFocus(int index)
    {
        if (slots == null || slots.Count == 0) return;
        if (index < 0 || index >= slots.Count) return;

        padFocusIndex = index;
        ApplyPadFocusVisual(index);
        HandleSlotSelected(index);

        if (EventSystem.current != null && slots[index] != null)
        {
            EventSystem.current.SetSelectedGameObject(slots[index].gameObject);
        }
    }

    private void ApplyPadFocusVisual(int focusedIndex)
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
            {
                slots[i].SetFocused(showPadFocus && i == focusedIndex);
            }
        }
    }

    private int GetCurrentCrossIndex(EquipCrossFocus focus)
    {
        EnsurePlayerInventory();
        if (focus == EquipCrossFocus.Top) return Mathf.Clamp(currentTopIndex, 0, 2);
        if (playerInventory == null) return 0;
        switch (focus)
        {
            case EquipCrossFocus.Right: return Mathf.Clamp(playerInventory.currentRightIndex, 0, 2);
            case EquipCrossFocus.Left: return Mathf.Clamp(playerInventory.currentLeftIndex, 0, 2);
            case EquipCrossFocus.Bottom: return Mathf.Clamp(playerInventory.currentUsableIndex, 0, 2);
            default: return 0;
        }
    }

    private struct CrossSlotRef
    {
        public EquipCrossFocus focus;
        public int index;
        public InventorySlot slot;

        public CrossSlotRef(EquipCrossFocus f, int i, InventorySlot s)
        {
            focus = f;
            index = i;
            slot = s;
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
        CrossSlotRef best = default(CrossSlotRef);

        foreach (var candidate in EnumerateCrossSlots())
        {
            if (candidate.slot == null) continue;

            int focusedIndex = GetCurrentCrossIndex(candidate.focus);
            if (candidate.focus == equipCrossFocus && candidate.index == focusedIndex) continue;

            Vector2 delta = GetSlotCenter(candidate.slot) - currentPos;
            if (delta.sqrMagnitude < 0.01f) continue;

            Vector2 deltaNorm = delta.normalized;
            float forward = Vector2.Dot(deltaNorm, dir);
            if (forward <= 0.15f) continue; // deve essere almeno un po' nella direzione richiesta

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
        {
            SetEquipmentCrossFocus(best.focus, best.index);
        }
    }

    private InventorySlot GetCurrentCrossSlot()
    {
        int idx = GetCurrentCrossIndex(equipCrossFocus);
        switch (equipCrossFocus)
        {
            case EquipCrossFocus.Right:
                return idx >= 0 && idx < rightEquipSlots.Length ? rightEquipSlots[idx] : null;
            case EquipCrossFocus.Left:
                return idx >= 0 && idx < leftEquipSlots.Length ? leftEquipSlots[idx] : null;
            case EquipCrossFocus.Bottom:
                return idx >= 0 && idx < bottomEquipSlots.Length ? bottomEquipSlots[idx] : null;
            case EquipCrossFocus.Top:
                return idx >= 0 && idx < topEquipSlots.Length ? topEquipSlots[idx] : null;
            default:
                return null;
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

        for (int i = 0; i < rightEquipSlots.Length; i++)
            if (rightEquipSlots[i] != null) rightEquipSlots[i].SetFocused(showPadFocus && equipCrossFocus == EquipCrossFocus.Right && i == rightIndex);

        for (int i = 0; i < leftEquipSlots.Length; i++)
            if (leftEquipSlots[i] != null) leftEquipSlots[i].SetFocused(showPadFocus && equipCrossFocus == EquipCrossFocus.Left && i == leftIndex);

        for (int i = 0; i < bottomEquipSlots.Length; i++)
            if (bottomEquipSlots[i] != null) bottomEquipSlots[i].SetFocused(showPadFocus && equipCrossFocus == EquipCrossFocus.Bottom && i == bottomIndex);

        int topIndex = GetCurrentCrossIndex(EquipCrossFocus.Top);
        for (int i = 0; i < topEquipSlots.Length; i++)
            if (topEquipSlots[i] != null) topEquipSlots[i].SetFocused(showPadFocus && equipCrossFocus == EquipCrossFocus.Top && i == topIndex);
    }

    private void UpdateFocusInputMode()
    {
        bool gamepadUsed = DetectGamepadInputThisFrame();
        bool kbMouseUsed = DetectKeyboardMouseInputThisFrame();

        bool newState = showPadFocus;
        if (gamepadUsed) newState = true;
        if (kbMouseUsed) newState = false;

        if (newState == showPadFocus) return;
        showPadFocus = newState;
        RefreshFocusVisualState();
    }

    private void RefreshFocusVisualState()
    {
        ApplyPadFocusVisual(showPadFocus ? padFocusIndex : -1);
        ApplyEquipmentCrossFocusVisual();
    }

    private bool DetectGamepadInputThisFrame()
    {
        var gp = Gamepad.current;
        if (gp == null) return false;

        if (gp.buttonSouth.wasPressedThisFrame || gp.buttonNorth.wasPressedThisFrame ||
            gp.buttonEast.wasPressedThisFrame || gp.buttonWest.wasPressedThisFrame ||
            gp.leftShoulder.wasPressedThisFrame || gp.rightShoulder.wasPressedThisFrame ||
            gp.startButton.wasPressedThisFrame || gp.selectButton.wasPressedThisFrame ||
            gp.dpad.up.wasPressedThisFrame || gp.dpad.down.wasPressedThisFrame ||
            gp.dpad.left.wasPressedThisFrame || gp.dpad.right.wasPressedThisFrame)
            return true;

        if (gp.leftStick.ReadValue().sqrMagnitude > gamepadAxisDetectThreshold * gamepadAxisDetectThreshold)
            return true;

        if (gp.rightStick.ReadValue().sqrMagnitude > gamepadAxisDetectThreshold * gamepadAxisDetectThreshold)
            return true;

        return false;
    }

    private bool DetectKeyboardMouseInputThisFrame()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.anyKey.wasPressedThisFrame)
            return true;

        var mouse = Mouse.current;
        if (mouse == null) return false;

        if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame)
            return true;

        if (mouse.delta.ReadValue().sqrMagnitude > 0.01f)
            return true;

        if (mouse.scroll.ReadValue().sqrMagnitude > 0.01f)
            return true;

        return false;
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
        // Prevent leaked previews if BeginDrag fires again without a matching EndDrag.
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
            // Build a simple image if no template is assigned
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

        // Keep preview the same size as the grid icon; fall back to template or a sane default.
        if (iconSize == Vector2.zero)
        {
            iconSize = activeDragPreview.rectTransform.sizeDelta;
            if (iconSize == Vector2.zero)
            {
                iconSize = new Vector2(48f, 48f);
            }
        }
        activeDragPreview.rectTransform.sizeDelta = iconSize;
        activeDragPreview.rectTransform.position = eventData.position;
        activeDragPreview.gameObject.SetActive(true);
    }

    private void ClearDragPreview()
    {
        if (activeDragPreview != null)
        {
            Destroy(activeDragPreview.gameObject);
            activeDragPreview = null;
        }
    }

    void OnDisable()
    {
        ClearDragPreview();
        dragOriginIndex = -1;
    }

    private void CloseEquipGrid()
    {
        // Nascondi la griglia inventario quando abbiamo equipaggiato
        ShowEquipmentInventory(false);
        ResetFilterToAll();
        ClearDetailPanel();
        currentSelectedIndex = -1;
        selectedPadIndex = -1;
    }

    // Equip selected item into the slot that opened the grid (weapon button)
    public void OnEquipWeaponButtonClick()
    {
        if (currentSelectedIndex < 0 || !HasItem(currentSelectedIndex)) return;
        EnsurePlayerInventory();
        if (playerInventory == null) return;

        var item = currentItems[currentSelectedIndex];
        if (item.weaponData == null) return;

        // arma nuova e arma precedente
        WeaponItem newWeapon = item.weaponData;

        if (currentEquipTarget == EquipTarget.Right)
        {
            playerInventory.SetRightAtSlot(currentEquipSlot, newWeapon, item.instanceId);
        }
        else if (currentEquipTarget == EquipTarget.Left)
        {
            playerInventory.SetLeftAtSlot(currentEquipSlot, newWeapon, item.instanceId);
        }
        else
        {
            return; // not a weapon target
        }

        RefreshSlot(currentSelectedIndex);
        RefreshDetailSelection();
        RefreshEquipmentCross();
        ResetEquipTarget();
        CloseEquipGrid();
    }

    // Equip usable into bottom slot
    public void OnEquipUsableButtonClick()
    {
        if (currentSelectedIndex < 0 || !HasItem(currentSelectedIndex)) return;
        EnsurePlayerInventory();
        if (playerInventory == null) return;

        var item = currentItems[currentSelectedIndex];
        if (item.usableData == null) return;

        if (currentEquipTarget == EquipTarget.Bottom)
        {
            playerInventory.SetUsableAtSlot(currentEquipSlot, item.usableData, item.instanceId);
        }
        else
            return;


        RefreshSlot(currentSelectedIndex);
        RefreshDetailSelection();
        RefreshEquipmentCross();
        ResetEquipTarget();
        CloseEquipGrid();
    }
}
