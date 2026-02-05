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
    public List<InventoryItem> GetCurrentItemsSnapshot() => new List<InventoryItem>(currentItems);

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

    void Start()
    {
        // fallback: se non assegnato, usa il proprio transform come parent
        if (slotParent == null) slotParent = transform;

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
        if (inventoryData == null)
        {
            ClearAllSlots();
            return;
        }

        currentItems = new List<InventoryItem>(inventoryData);

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
    }

    /// <summary>
    /// Attiva una tab specifica: colora il titolo e mostra l'eventuale background associato.
    /// </summary>
    public void SetActiveTab(string tabKey)
    {
        if (tabs == null || tabs.Length == 0) return;

        bool tabFound = false;

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
        RefreshSlot(a);
        RefreshSlot(b);
        RefreshDetailSelection();
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
            return;
        }
        selectedPadIndex = index;
        ShowItemDetailsByIndex(index);
    }

    public void HandleSlotBeginDrag(int index, PointerEventData eventData)
    {
        if (!HasItem(index)) return;
        dragOriginIndex = index;
        CreateDragPreview(currentItems[index]?.icon, eventData);
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
        }
        else
        {
            ClearDetailPanel();
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

    private void CreateDragPreview(Sprite icon, PointerEventData eventData)
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
        activeDragPreview.rectTransform.sizeDelta = new Vector2(48, 48);
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
}
