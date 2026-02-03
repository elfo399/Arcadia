using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class InventoryUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private InventorySlot[] slots;

    [Header("Tabs")]
    [SerializeField] private TabEntry[] tabs;
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color inactiveColor = new Color(0.8f, 0.8f, 0.8f);
    [SerializeField] private string defaultTabKey = "Inventory";
    private int currentTabIndex = -1;

    void Start()
    {
        // Se non assegnato in Inspector, popola automaticamente dai figli
        if (slots == null || slots.Length == 0)
        {
            slots = GetComponentsInChildren<InventorySlot>();
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
        ClearAllSlots();

        for (int i = 0; i < slots.Length; i++)
        {
            if (i < inventoryData.Count && inventoryData[i] != null)
            {
                slots[i].Setup(inventoryData[i].icon, inventoryData[i].amount);
            }
            else
            {
                slots[i].Clear();
            }
        }
    }

    /// <summary>
    /// Attiva una tab specifica: colora il titolo e mostra l'eventuale background associato.
    /// </summary>
    public void SetActiveTab(string tabKey)
    {
        if (tabs == null || tabs.Length == 0) return;

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
            }
        }
        // se non trovata, punta alla prima tab valida
        if (currentTabIndex < 0 && tabs.Length > 0)
        {
            currentTabIndex = 0;
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

    [System.Serializable]
    private class TabEntry
    {
        public string key;
        public TextMeshProUGUI label;
        public GameObject background;
    }
}
