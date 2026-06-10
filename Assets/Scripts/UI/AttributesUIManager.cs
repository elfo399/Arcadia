using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AttributesUIManager : MonoBehaviour
{
    [System.Serializable]
    private class AttributeRowBinding
    {
        public string key;
        public Transform root;
        public TextMeshProUGUI labelText;
        public TextMeshProUGUI valueText;
        public TextMeshProUGUI descText;
        public Button addButton;
    }

    [Header("Attributes UI")]
    [SerializeField] private bool autoWireAttributesUI = false;
    [SerializeField] private Transform attributesRoot;
    [SerializeField] private TextMeshProUGUI attributesLevelLabelText;
    [SerializeField] private TextMeshProUGUI attributesLevelValueText;
    [SerializeField] private TextMeshProUGUI attributesXpValueText;
    [SerializeField] private Scrollbar attributesXpScrollbar;
    [SerializeField] private TextMeshProUGUI attributesHpValueText;
    [SerializeField] private TextMeshProUGUI attributesManaValueText;
    [SerializeField] private TextMeshProUGUI attributesStaminaValueText;
    [SerializeField] private TextMeshProUGUI attributesBasePhyDamageValueText;
    [SerializeField] private TextMeshProUGUI attributesMagicDamageValueText;
    [SerializeField] private TextMeshProUGUI attributesPhyDefValueText;
    [SerializeField] private TextMeshProUGUI attributesMagicDefValueText;
    [SerializeField] private TextMeshProUGUI attributesLoadValueText;
    [SerializeField] private TextMeshProUGUI attributesLoadTierValueText;
    [SerializeField] private Scrollbar attributesLoadScrollbar;
    [SerializeField] private Color attributesSelectedColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color attributesNormalColor = Color.white;
    [SerializeField] private List<AttributeRowBinding> attributeRows = new();
    [SerializeField] private MenuManager menuManager;
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private PlayerController playerController;

    private string selectedAttributeKey;
    private bool attributesUiInitialized;
    private int attributesPadIndex;
    private bool showPadFocus;

    public void Initialize()
    {
        if (attributesUiInitialized) return;

        if (autoWireAttributesUI)
            AutoWireAttributesUIReferences();

        if (attributeRows == null) attributeRows = new List<AttributeRowBinding>();

        for (int i = 0; i < attributeRows.Count; i++)
        {
            var row = attributeRows[i];
            if (row == null) continue;
            if (autoWireAttributesUI && row.root == null && !string.IsNullOrWhiteSpace(row.key) && attributesRoot != null)
                row.root = FindDeepChildByName(attributesRoot, row.key);
            if (row.root == null) continue;

            if (string.IsNullOrWhiteSpace(row.key)) row.key = row.root.name;
            if (autoWireAttributesUI && row.labelText == null) row.labelText = FindDeepTextByName(row.root, "Txt");
            if (autoWireAttributesUI && row.valueText == null) row.valueText = FindDeepTextByName(row.root, "Value");
            if (autoWireAttributesUI && row.descText == null) row.descText = FindDeepTextByName(row.root, "Desc");
            if (autoWireAttributesUI && row.addButton == null)
            {
                var btnTf = FindDeepChildByName(row.root, "Button");
                if (btnTf != null) row.addButton = btnTf.GetComponent<Button>();
            }
        }

        BindAttributeButtons();
        if (string.IsNullOrWhiteSpace(selectedAttributeKey))
            selectedAttributeKey = GetFirstAttributeKey();

        attributesUiInitialized = true;
    }

    public void Cleanup()
    {
        if (attributeRows == null) return;
        for (int i = 0; i < attributeRows.Count; i++)
        {
            var row = attributeRows[i];
            if (row == null || row.addButton == null) continue;
            row.addButton.onClick.RemoveAllListeners();
        }
    }

    public void SetPadFocusVisible(bool visible)
    {
        showPadFocus = visible;
        RefreshAttributeSelectionVisual();
    }

    public void RefreshUI()
    {
        Initialize();
        CachePlayerStats();
        CachePlayerController();
        if (playerStats == null) return;

        SetReadOnlyScrollbar(attributesXpScrollbar);
        SetReadOnlyScrollbar(attributesLoadScrollbar);
        RefreshAttributeRowsValues();
        RefreshAttributeDerivedPanel();
        RefreshAttributeSelectionVisual();
    }

    public bool HasAttributePointsToSpend()
    {
        CachePlayerStats();
        if (playerStats == null || playerStats.unspentAttributePoints <= 0) return false;
        if (attributeRows == null || attributeRows.Count == 0) return false;

        for (int i = 0; i < attributeRows.Count; i++)
        {
            var row = attributeRows[i];
            if (row == null || string.IsNullOrWhiteSpace(row.key)) continue;
            if (CanAllocateAttributeKey(row.key)) return true;
        }

        return false;
    }

    public void FocusPadDefault(float lockDuration)
    {
        Initialize();

        if (!HasAttributePointsToSpend())
        {
            menuManager?.SetPadFocusVisible(false);
            RefreshUI();
            return;
        }

        menuManager?.ForcePadFocusMode(lockDuration);
        attributesPadIndex = GetSelectedAttributeIndex();
        if (attributesPadIndex < 0) attributesPadIndex = 0;
        SyncAttributeSelectionFromPadIndex();
        RefreshUI();
    }

    public void MovePadFocusVertical(int direction)
    {
        Initialize();
        if (!HasAttributePointsToSpend()) return;
        if (attributeRows == null || attributeRows.Count == 0) return;

        int dir = direction >= 0 ? 1 : -1;
        int count = attributeRows.Count;
        int guard = 0;
        int idx = Mathf.Clamp(attributesPadIndex, 0, count - 1);

        do
        {
            idx = (idx + dir + count) % count;
            guard++;
        }
        while (guard <= count && !IsAttributeRowNavigable(idx));

        attributesPadIndex = Mathf.Clamp(idx, 0, count - 1);
        SyncAttributeSelectionFromPadIndex();
        RefreshUI();
    }

    public void ConfirmPadSelection()
    {
        Initialize();
        if (!HasAttributePointsToSpend()) return;
        SyncAttributeSelectionFromPadIndex();
        if (attributeRows == null || attributeRows.Count == 0) return;
        if (attributesPadIndex < 0 || attributesPadIndex >= attributeRows.Count) return;

        var row = attributeRows[attributesPadIndex];
        if (row == null || string.IsNullOrWhiteSpace(row.key)) return;
        OnAttributeAddClicked(row.key.Trim().ToLowerInvariant());
    }

    private void CachePlayerStats()
    {
        if (playerStats != null) return;
        playerStats = PlayerStats.instance;
    }

    private void CachePlayerController()
    {
    }

    private void AutoWireAttributesUIReferences()
    {
        Transform skillRoot = null;
        var menuTabs = menuManager != null ? menuManager.GetTabs() : null;
        if (menuTabs != null)
        {
            for (int i = 0; i < menuTabs.Length; i++)
            {
                if (menuTabs[i] == null || menuTabs[i].background == null) continue;
                if (!string.Equals(menuTabs[i].key, "Skill", System.StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(menuTabs[i].key, "Attributes", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                skillRoot = menuTabs[i].background.transform;
                break;
            }
        }

        if (skillRoot == null)
            skillRoot = FindDeepChildByName(transform.root, "SkillBackground");
        if (skillRoot == null) return;

        if (attributesRoot == null)
            attributesRoot = FindDescendantByPath(skillRoot, "Right/Attributes") ?? FindDeepChildByName(skillRoot, "Attributes");

        Transform centerPanel = FindDescendantByPath(skillRoot, "Center/Panel");
        if (attributesLevelLabelText == null && centerPanel != null) attributesLevelLabelText = FindDeepTextByName(centerPanel, "LevelTxt");
        if (attributesLevelValueText == null && centerPanel != null) attributesLevelValueText = FindDeepTextByName(centerPanel, "LevelValue");
        if (attributesXpValueText == null && centerPanel != null) attributesXpValueText = FindDeepTextByName(centerPanel, "XpValue");
        if (attributesXpScrollbar == null && centerPanel != null) attributesXpScrollbar = centerPanel.GetComponentInChildren<Scrollbar>(true);

        Transform leftRoot = FindDescendantByPath(skillRoot, "Left");
        if (attributesHpValueText == null && leftRoot != null) attributesHpValueText = FindDeepTextByName(leftRoot, "HPValue");
        if (attributesManaValueText == null && leftRoot != null) attributesManaValueText = FindDeepTextByName(leftRoot, "ManaValue");
        if (attributesStaminaValueText == null && leftRoot != null) attributesStaminaValueText = FindDeepTextByName(leftRoot, "StaminaValue");
        if (attributesBasePhyDamageValueText == null && leftRoot != null) attributesBasePhyDamageValueText = FindDeepTextByName(leftRoot, "BasePhyDamageValue");
        if (attributesMagicDamageValueText == null && leftRoot != null) attributesMagicDamageValueText = FindDeepTextByName(leftRoot, "MagicDamageValue");
        if (attributesPhyDefValueText == null && leftRoot != null) attributesPhyDefValueText = FindDeepTextByName(leftRoot, "PhyDefValue");
        if (attributesMagicDefValueText == null && leftRoot != null) attributesMagicDefValueText = FindDeepTextByName(leftRoot, "MagicDefValue");
        if (attributesLoadValueText == null && leftRoot != null) attributesLoadValueText = FindDeepTextByName(leftRoot, "LoadValue");
        if (attributesLoadScrollbar == null)
        {
            var loadRoot = leftRoot != null ? FindDeepChildByName(leftRoot, "Load") : null;
            if (loadRoot != null) attributesLoadScrollbar = loadRoot.GetComponentInChildren<Scrollbar>(true);
        }

        if (attributeRows == null) attributeRows = new List<AttributeRowBinding>();
        if (attributesRoot == null) return;

        if (attributeRows.Count == 0)
        {
            string[] keys = { "Vigor", "Mind", "Endurance", "Strength", "Dexterity", "Intelligence", "Faith", "Evil", "Karma" };
            for (int i = 0; i < keys.Length; i++)
            {
                var rowTf = FindDeepChildByName(attributesRoot, keys[i]);
                if (rowTf == null) continue;

                attributeRows.Add(new AttributeRowBinding
                {
                    key = keys[i],
                    root = rowTf,
                    labelText = FindDeepTextByName(rowTf, "Txt"),
                    valueText = FindDeepTextByName(rowTf, "Value"),
                    descText = FindDeepTextByName(rowTf, "Desc"),
                    addButton = FindDeepChildByName(rowTf, "Button") != null ? FindDeepChildByName(rowTf, "Button").GetComponent<Button>() : null
                });
            }
        }
    }

    private void BindAttributeButtons()
    {
        if (attributeRows == null) return;

        for (int i = 0; i < attributeRows.Count; i++)
        {
            var row = attributeRows[i];
            if (row == null || row.addButton == null) continue;

            string key = string.IsNullOrWhiteSpace(row.key) ? row.root != null ? row.root.name : string.Empty : row.key;
            if (string.IsNullOrWhiteSpace(key)) continue;
            string capturedKey = key.Trim().ToLowerInvariant();

            row.addButton.onClick.RemoveAllListeners();
            row.addButton.onClick.AddListener(() => OnAttributeAddClicked(capturedKey));
        }
    }

    private void RefreshAttributeRowsValues()
    {
        if (attributeRows == null || playerStats == null) return;
        bool canSpend = HasAttributePointsToSpend();

        for (int i = 0; i < attributeRows.Count; i++)
        {
            var row = attributeRows[i];
            if (row == null || string.IsNullOrWhiteSpace(row.key)) continue;

            string statName = MapAttributeKeyToStatName(row.key);
            int value = playerStats.GetPersistentStat(statName);
            if (row.valueText != null) row.valueText.text = value.ToString();
            if (row.descText != null && string.IsNullOrWhiteSpace(row.descText.text))
                row.descText.text = GetDefaultAttributeDescription(row.key);
            if (row.addButton != null) row.addButton.gameObject.SetActive(canSpend && CanAllocateAttributeKey(row.key));
        }
    }

    private void RefreshAttributeDerivedPanel()
    {
        if (playerStats == null) return;

        playerStats.RefreshArmorTotals();

        int level = Mathf.Max(1, playerStats.playerLevel);
        float xpProgress = playerStats.GetLevelProgress01();

        if (attributesLevelLabelText != null)
            attributesLevelLabelText.text = "Level";
        if (attributesLevelValueText != null) attributesLevelValueText.text = level.ToString();
        if (attributesXpValueText != null) attributesXpValueText.text = $"{playerStats.levelExperience}/{playerStats.experienceToNextLevel}";
        ApplyReadOnlyProgressToScrollbar(attributesXpScrollbar, xpProgress);

        int hp = Mathf.RoundToInt(playerStats.maxHealth);
        int mana = Mathf.RoundToInt(playerStats.maxMana);
        int stamina = Mathf.RoundToInt(playerStats.maxStamina);
        int basePhyDamage = playerStats.GetBasePhysicalDamage();
        int magicDamage = playerStats.GetBaseMagicDamage();
        int phyDef = Mathf.Max(0, playerStats.endurance + Mathf.RoundToInt(playerStats.vigor * 0.5f)) + playerStats.TotalArmorPhysicalDefense;
        int magicDef = Mathf.Max(0, playerStats.intelligence + playerStats.faith) + playerStats.TotalArmorMagicDefense;

        float equipWeight = playerStats.GetCurrentEquipLoad();
        float maxLoad = playerStats.GetMaxEquipLoad();
        float loadRatio = Mathf.Clamp01(maxLoad > 0f ? equipWeight / maxLoad : 0f);
        string loadTierLabel = playerController != null ? playerController.GetEquipLoadTierLabel() : ResolveLoadTierLabelFallback(loadRatio);

        if (attributesHpValueText != null) attributesHpValueText.text = hp.ToString();
        if (attributesManaValueText != null) attributesManaValueText.text = mana.ToString();
        if (attributesStaminaValueText != null) attributesStaminaValueText.text = stamina.ToString();
        if (attributesBasePhyDamageValueText != null) attributesBasePhyDamageValueText.text = basePhyDamage.ToString();
        if (attributesMagicDamageValueText != null) attributesMagicDamageValueText.text = magicDamage.ToString();
        if (attributesPhyDefValueText != null) attributesPhyDefValueText.text = phyDef.ToString();
        if (attributesMagicDefValueText != null) attributesMagicDefValueText.text = magicDef.ToString();
        if (attributesLoadValueText != null) attributesLoadValueText.text = equipWeight.ToString("0.0") + " / " + maxLoad.ToString("0.0");
        if (attributesLoadTierValueText != null) attributesLoadTierValueText.text = loadTierLabel;
        ApplyReadOnlyProgressToScrollbar(attributesLoadScrollbar, loadRatio);
    }

    private static string ResolveLoadTierLabelFallback(float loadRatio)
    {
        if (loadRatio < 0.20f) return "Fast";
        if (loadRatio > 0.80f) return "Slow";
        return "Normal";
    }

    private static void SetReadOnlyScrollbar(Scrollbar bar)
    {
        if (bar == null) return;
        bar.interactable = false;
        var nav = bar.navigation;
        nav.mode = Navigation.Mode.None;
        bar.navigation = nav;
    }

    private static void ApplyReadOnlyProgressToScrollbar(Scrollbar bar, float normalized)
    {
        if (bar == null) return;

        normalized = Mathf.Clamp01(normalized);
        bar.size = normalized;

        switch (bar.direction)
        {
            case Scrollbar.Direction.LeftToRight:
            case Scrollbar.Direction.BottomToTop:
                bar.value = 0f;
                break;
            case Scrollbar.Direction.RightToLeft:
            case Scrollbar.Direction.TopToBottom:
                bar.value = 1f;
                break;
        }
    }

    private void RefreshAttributeSelectionVisual()
    {
        if (attributeRows == null) return;
        if (string.IsNullOrWhiteSpace(selectedAttributeKey))
            selectedAttributeKey = GetFirstAttributeKey();
        bool canSpend = HasAttributePointsToSpend();

        for (int i = 0; i < attributeRows.Count; i++)
        {
            var row = attributeRows[i];
            if (row == null || string.IsNullOrWhiteSpace(row.key)) continue;

            bool selected = canSpend && string.Equals(row.key, selectedAttributeKey, System.StringComparison.OrdinalIgnoreCase);
            Color color = selected && showPadFocus ? attributesSelectedColor : attributesNormalColor;
            if (row.labelText != null) row.labelText.color = color;
            if (row.valueText != null) row.valueText.color = color;
        }
    }

    private void OnAttributeAddClicked(string key)
    {
        CachePlayerStats();
        if (playerStats == null || string.IsNullOrWhiteSpace(key)) return;

        string statName = MapAttributeKeyToStatName(key);
        if (string.IsNullOrWhiteSpace(statName)) return;
        if (!IsAllocatableAttribute(statName)) return;
        if (!playerStats.TrySpendAttributePoint(statName)) return;

        selectedAttributeKey = key;
        RefreshUI();
    }

    private int GetSelectedAttributeIndex()
    {
        if (attributeRows == null || attributeRows.Count == 0) return -1;
        if (string.IsNullOrWhiteSpace(selectedAttributeKey))
        {
            for (int i = 0; i < attributeRows.Count; i++)
                if (IsAttributeRowNavigable(i)) return i;
            return 0;
        }

        for (int i = 0; i < attributeRows.Count; i++)
        {
            var row = attributeRows[i];
            if (row == null || string.IsNullOrWhiteSpace(row.key)) continue;
            if (string.Equals(row.key.Trim(), selectedAttributeKey.Trim(), System.StringComparison.OrdinalIgnoreCase))
                return i;
        }

        for (int i = 0; i < attributeRows.Count; i++)
            if (IsAttributeRowNavigable(i)) return i;

        return 0;
    }

    private void SyncAttributeSelectionFromPadIndex()
    {
        if (attributeRows == null || attributeRows.Count == 0) return;
        attributesPadIndex = Mathf.Clamp(attributesPadIndex, 0, attributeRows.Count - 1);
        var row = attributeRows[attributesPadIndex];
        if (row == null || string.IsNullOrWhiteSpace(row.key)) return;
        selectedAttributeKey = row.key.Trim().ToLowerInvariant();
    }

    private bool IsAttributeRowNavigable(int index)
    {
        if (attributeRows == null || index < 0 || index >= attributeRows.Count) return false;
        var row = attributeRows[index];
        return row != null && row.root != null && row.root.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(row.key);
    }

    private string GetFirstAttributeKey()
    {
        if (attributeRows == null || attributeRows.Count == 0) return "vigor";
        for (int i = 0; i < attributeRows.Count; i++)
        {
            if (attributeRows[i] == null || string.IsNullOrWhiteSpace(attributeRows[i].key)) continue;
            return attributeRows[i].key.Trim().ToLowerInvariant();
        }
        return "vigor";
    }

    private static string MapAttributeKeyToStatName(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;
        string k = key.Trim().ToLowerInvariant();
        if (k == "evil") return "malefico";
        return k;
    }

    private static bool IsAllocatableAttribute(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        switch (key.Trim().ToLowerInvariant())
        {
            case "vigor":
            case "mind":
            case "endurance":
            case "strength":
            case "dexterity":
            case "intelligence":
                return true;
            default:
                return false;
        }
    }

    private bool CanAllocateAttributeKey(string key)
    {
        if (!IsAllocatableAttribute(key)) return false;
        CachePlayerStats();
        if (playerStats == null) return false;

        string statName = MapAttributeKeyToStatName(key);
        int current = playerStats.GetPersistentStat(statName);
        return current < PlayerStats.MaxAllocatableAttributeLevel;
    }

    private static string GetDefaultAttributeDescription(string key)
    {
        string k = string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim().ToLowerInvariant();
        switch (k)
        {
            case "vigor": return "Increases max HP and base physical damage.";
            case "mind": return "Increases mana and base magic damage.";
            case "endurance": return "Increases stamina, defense and equip load.";
            case "strength": return "Increases base physical damage and physical scaling.";
            case "dexterity": return "Increases ranged (bow) damage.";
            case "intelligence": return "Increases base magic damage and magic scaling.";
            case "faith": return "Increases holy power and magic defense.";
            case "evil": return "Represents your dark alignment.";
            case "karma": return "Represents your moral balance.";
            default: return string.Empty;
        }
    }

    private static Transform FindDescendantByPath(Transform root, string path)
    {
        if (root == null || string.IsNullOrWhiteSpace(path)) return null;
        string[] parts = path.Split('/');
        Transform current = root;
        for (int i = 0; i < parts.Length; i++)
        {
            if (current == null) return null;
            current = current.Find(parts[i]);
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

    private static TextMeshProUGUI FindDeepTextByName(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName)) return null;

        Transform t = FindDeepChildByName(root, objectName);
        if (t == null) return null;
        var own = t.GetComponent<TextMeshProUGUI>();
        if (own != null) return own;
        return t.GetComponentInChildren<TextMeshProUGUI>(true);
    }
}
