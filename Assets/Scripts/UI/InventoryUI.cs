using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Events;

public class InventoryUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private InventorySlot slotPrefab;
    [SerializeField] private Transform slotParent;
    [SerializeField] private int initialSlotCount = 0; // facoltativo: crea slot all'avvio
    private readonly List<InventorySlot> slots = new();

    [Header("Tabs")]
    [SerializeField] private TabEntry[] tabs;
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color inactiveColor = new Color(0.8f, 0.8f, 0.8f);
    [SerializeField] private string defaultTabKey = "Inventory";
    private int currentTabIndex = -1;

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
        }

        // genera slot iniziali se richiesto
        if (slotPrefab != null && initialSlotCount > 0 && slots.Count == 0)
        {
            EnsureSlots(initialSlotCount);
        }

        ClearAllSlots();

        // Evidenzia tab di default
        if (!string.IsNullOrEmpty(defaultTabKey))
        {
            SetActiveTab(defaultTabKey);
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

        EnsureSlots(inventoryData.Count);

        ClearAllSlots();

        for (int i = 0; i < slots.Count; i++)
        {
            if (i < inventoryData.Count && inventoryData[i] != null)
            {
                slots[i].Setup(inventoryData[i].icon, inventoryData[i].amount);
                slots[i].gameObject.SetActive(true);
            }
            else
            {
                slots[i].Clear();
                // nasconde eventuali slot in eccesso
                slots[i].gameObject.SetActive(false);
            }
        }
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

        if (slotPrefab == null || slotParent == null) return;

        while (slots.Count < required)
        {
            var slot = Instantiate(slotPrefab, slotParent);
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
}
