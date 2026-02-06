using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

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

    [Header("Wallet UI")]
    [SerializeField] private TextMeshProUGUI goldValueText;
    [SerializeField] private TextMeshProUGUI silverValueText;
    [SerializeField] private TextMeshProUGUI copperValueText;
    [SerializeField] private WalletSource walletSource = WalletSource.Run;
    [SerializeField] private bool autoRefreshWallet = true;
    private PlayerStats playerStats;

    [Header("Drag & Drop")]
    [SerializeField] private Canvas dragCanvas; // opzionale: se null usa quello più alto trovato
    [SerializeField] private Image dragPreviewTemplate;
    private Image activeDragPreview;
    private int dragOriginIndex = -1;
    private int selectedPadIndex = -1;
    private int currentSelectedIndex = -1;

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
    private InventorySlot topEquipSlot;
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
    }

    void OnEnable()
    {
        // quando il pannello viene riaperto, riallinea subito le icone HUD/equip
        RefreshEquipmentCross();
    }

    void OnDestroy()
    {
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
    public void OnEquipTop()
    {
        EnsurePlayerInventory();
        ShowEquipmentInventory(true);
        currentEquipTarget = EquipTarget.Top;
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
                slots[i].Setup(GetItemIcon(item), item.amount);
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

        // Top (per future magie, singolo)
        topEquipSlot = CreateEquipSlot(topEquipContainer);
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
            slots[index].Setup(GetItemIcon(item), item.amount);
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
            var right = playerInventory.rightHandWeapon ?? playerInventory.unarmedRight;
            var left = playerInventory.leftHandWeapon ?? playerInventory.unarmedLeft;
            var rightIcon = right != null ? right.icon : null;
            var leftIcon = left != null ? left.icon : null;
            SetCrossIcon(hudCrossRight, rightIcon);
            SetCrossIcon(hudCrossLeft, leftIcon);

            UpdateEquipVisuals(rightEquipSlots, playerInventory.rightLoadout);
            UpdateEquipVisuals(leftEquipSlots, playerInventory.leftLoadout);

            UpdateEquipVisual(hudRightSlot, rightIcon, 1);
            UpdateEquipVisual(hudLeftSlot, leftIcon, 1);
        }

        // bottom: mostra solo l'usabile equipaggiato, niente fallback da inventario
        Sprite usableIcon = null;
        if (playerInventory != null && playerInventory.GetCurrentUsable() != null)
        {
            usableIcon = playerInventory.GetCurrentUsable().icon;
        }
        SetCrossIcon(hudCrossBottom, usableIcon);
        UpdateEquipVisuals(bottomEquipSlots, playerInventory.usableLoadout);
        UpdateHudVisual(hudBottomSlot, usableIcon);

        // top: placeholder per future magie (per ora vuoto)
        SetCrossIcon(hudCrossTop, null);
        UpdateEquipVisual(topEquipSlot, null, 0);
        UpdateHudVisual(hudTopSlot, null);
    }

    private void SetCrossIcon(Image target, Sprite sprite)
    {
        if (target == null) return;
        target.sprite = sprite;
        target.enabled = sprite != null;
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

    private void SetSourceItemAt(int index, InventoryItem item)
    {
        while (sourceItems.Count <= index) sourceItems.Add(null);
        sourceItems[index] = item;
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


    // Mouse: select origin on pointer down, start drag to move preview
    public void HandleSlotPointerDown(int index)
    {
        if (!HasItem(index))
        {
            ClearDetailPanel();
            UpdateEquipButtonState();
            return;
        }
        selectedPadIndex = index;
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
        CreateDragPreview(currentItems[index]?.icon, eventData, iconSize);
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
            ShowItemDetailsByIndex(index);
            UpdateEquipButtonState();
        }
        else
        {
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

    private void CreateDragPreview(Sprite icon, PointerEventData eventData, Vector2 iconSize)
    {
        if (icon == null) return;

        Canvas targetCanvas = dragCanvas;
        if (targetCanvas == null)
            targetCanvas = GetComponentInParent<Canvas>();

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

        // Rimetti l'arma precedente nello slot inventario (se esiste e non è unarmed)
        currentItems[currentSelectedIndex] = null;
        SetSourceItemAt(currentSelectedIndex, null);

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

        currentItems[currentSelectedIndex] = null;
        SetSourceItemAt(currentSelectedIndex, null);

        RefreshSlot(currentSelectedIndex);
        RefreshDetailSelection();
        RefreshEquipmentCross();
        ResetEquipTarget();
        CloseEquipGrid();
    }
}
