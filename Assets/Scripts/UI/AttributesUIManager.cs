using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
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
        [FormerlySerializedAs("increaseButton")]
        [InspectorName("Decrease Button")]
        [Tooltip("Freccia sinistra. Rimuove soltanto i livelli preparati e non ancora confermati.")]
        public Button decreaseButtonReference;
        [FormerlySerializedAs("decreaseButton")]
        [FormerlySerializedAs("addButton")]
        [InspectorName("Increase Button")]
        [Tooltip("Freccia destra usata per preparare un livello da assegnare.")]
        public Button increaseButtonReference;
    }

    [Header("Attributes UI")]
    [SerializeField] private TextMeshProUGUI attributesLevelLabelText;
    [SerializeField] private TextMeshProUGUI attributesLevelValueText;
    [SerializeField] private TextMeshProUGUI attributesXpValueText;
    [FormerlySerializedAs("attributesXpScrollbar")]
    [SerializeField] private ProgressBarUI attributesXpProgressBar;
    [SerializeField] private TextMeshProUGUI attributesHpValueText;
    [SerializeField] private TextMeshProUGUI attributesManaValueText;
    [SerializeField] private TextMeshProUGUI attributesStaminaValueText;
    [SerializeField] private TextMeshProUGUI attributesBasePhyDamageValueText;
    [SerializeField] private TextMeshProUGUI attributesMagicDamageValueText;
    [SerializeField] private TextMeshProUGUI attributesPhyDefValueText;
    [SerializeField] private TextMeshProUGUI attributesMagicDefValueText;
    [SerializeField] private TextMeshProUGUI attributesLoadValueText;
    [SerializeField] private TextMeshProUGUI attributesLoadTierValueText;
    [Header("Unspent Attribute Points")]
    [Tooltip("Parent che contiene la scritta Point e il relativo valore. Viene nascosto quando non restano punti da assegnare.")]
    [SerializeField] private GameObject txtPointParent;
    [Tooltip("Testo che mostra il numero di punti attributo ancora disponibili.")]
    [SerializeField] private TextMeshProUGUI txtPointValue;
    [SerializeField] private Image playerPortraitImage;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private string playerNameFormat = "{0}";
    [FormerlySerializedAs("attributesLoadScrollbar")]
    [SerializeField] private ProgressBarUI attributesLoadProgressBar;
    [SerializeField] private PlayerCharacterDatabase playerCharacterDatabase;
    [SerializeField] private bool hidePlayerPortraitWhenMissing = true;
    [SerializeField] private Color attributesSelectedColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color attributesNormalColor = Color.white;
    [Tooltip("Colore del delta mostrato nei valori derivati durante l'assegnazione non ancora confermata.")]
    [SerializeField] private Color pendingPreviewColor = new Color(1f, 0.82f, 0.15f, 1f);
    [SerializeField] private List<AttributeRowBinding> attributeRows = new();
    [Tooltip("Pulsante che applica e salva tutte le modifiche preparate.")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private MenuManager menuManager;
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private PlayerController playerController;

    private string selectedAttributeKey;
    private bool attributesUiInitialized;
    private int attributesPadIndex;
    private bool showPadFocus;
    private string lastDisplayedCharacterId;
    private Sprite lastDisplayedPlayerPortrait;
    private string lastDisplayedPlayerName;
    private readonly Dictionary<string, int> pendingAttributeLevels = new();
    private bool allocationSessionActive;
    private bool confirmPadFocused;

    public void Initialize()
    {
        if (attributesUiInitialized) return;

        if (attributeRows == null) attributeRows = new List<AttributeRowBinding>();

        for (int i = 0; i < attributeRows.Count; i++)
        {
            var row = attributeRows[i];
            if (row == null) continue;
            if (row.root == null) continue;

            if (string.IsNullOrWhiteSpace(row.key)) row.key = row.root.name;
        }

        BindAttributeButtons();
        BindConfirmButton();
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
            if (row == null) continue;
            row.decreaseButtonReference?.onClick.RemoveAllListeners();
            row.increaseButtonReference?.onClick.RemoveAllListeners();
        }

        confirmButton?.onClick.RemoveAllListeners();
        pendingAttributeLevels.Clear();
        allocationSessionActive = false;
        confirmPadFocused = false;
    }

    public void BeginAllocationSession()
    {
        pendingAttributeLevels.Clear();
        allocationSessionActive = true;
        confirmPadFocused = false;
        RefreshUI();
    }

    public void CancelPendingAllocation()
    {
        if (!allocationSessionActive && pendingAttributeLevels.Count == 0) return;

        pendingAttributeLevels.Clear();
        allocationSessionActive = false;
        confirmPadFocused = false;
        RefreshUI();
    }

    public bool HasPendingAllocation()
    {
        return GetPendingAttributeLevelCount() > 0;
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
        if (playerStats == null) return;

        RefreshUnspentAttributePoints();
        RefreshAttributeRowsValues();
        RefreshAttributeDerivedPanel();
        RefreshPlayerPortrait();
        RefreshPlayerName();
        RefreshAttributeSelectionVisual();
    }

    public bool HasAttributePointsToSpend()
    {
        CachePlayerStats();
        if (playerStats == null || GetRemainingAttributePoints() <= 0) return false;
        if (attributeRows == null || attributeRows.Count == 0) return false;

        for (int i = 0; i < attributeRows.Count; i++)
        {
            var row = attributeRows[i];
            if (row == null || string.IsNullOrWhiteSpace(row.key)) continue;
            if (CanIncreaseAttributeKey(row.key)) return true;
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
        confirmPadFocused = false;
        attributesPadIndex = GetSelectedAttributeIndex();
        if (attributesPadIndex < 0) attributesPadIndex = 0;
        SyncAttributeSelectionFromPadIndex();
        RefreshUI();
    }

    public void MovePadFocusVertical(int direction)
    {
        Initialize();
        if (!HasAttributePointsToSpend() && !HasPendingAllocation()) return;
        if (attributeRows == null || attributeRows.Count == 0) return;

        int dir = direction >= 0 ? 1 : -1;
        if (confirmPadFocused)
        {
            if (dir < 0)
            {
                int lastIndex = GetLastNavigableAttributeIndex();
                if (lastIndex >= 0)
                {
                    confirmPadFocused = false;
                    attributesPadIndex = lastIndex;
                    SyncAttributeSelectionFromPadIndex();
                }
            }

            RefreshUI();
            return;
        }

        int count = attributeRows.Count;
        int guard = 0;
        int idx = IsAttributeRowNavigable(attributesPadIndex)
            ? attributesPadIndex
            : GetSelectedAttributeIndex();
        if (idx < 0) return;

        int lastNavigableIndex = GetLastNavigableAttributeIndex();
        if (dir > 0 && idx == lastNavigableIndex)
        {
            if (HasPendingAllocation() && confirmButton != null)
                confirmPadFocused = true;

            RefreshUI();
            return;
        }

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
        if (confirmPadFocused)
            ConfirmPendingAllocation();
    }

    public void IncreasePadSelection()
    {
        Initialize();
        if (confirmPadFocused) return;
        SyncAttributeSelectionFromPadIndex();
        if (attributeRows == null || attributeRows.Count == 0) return;
        if (attributesPadIndex < 0 || attributesPadIndex >= attributeRows.Count) return;

        var row = attributeRows[attributesPadIndex];
        if (row == null || string.IsNullOrWhiteSpace(row.key)) return;
        OnAttributeIncreaseClicked(row.key.Trim().ToLowerInvariant());
    }

    public void DecreasePadSelection()
    {
        Initialize();
        if (confirmPadFocused) return;
        SyncAttributeSelectionFromPadIndex();
        if (attributeRows == null || attributeRows.Count == 0) return;
        if (attributesPadIndex < 0 || attributesPadIndex >= attributeRows.Count) return;

        var row = attributeRows[attributesPadIndex];
        if (row == null || string.IsNullOrWhiteSpace(row.key)) return;
        OnAttributeDecreaseClicked(row.key.Trim().ToLowerInvariant());
    }

    private void CachePlayerStats()
    {
        if (playerStats != null) return;
        playerStats = PlayerStats.instance;
    }

    private void RefreshPlayerPortrait()
    {
        if (playerPortraitImage == null)
            return;

        PlayerCharacterData character = ResolveSelectedCharacter();
        string characterId = character != null ? character.GetCharacterId() : string.Empty;
        Sprite portrait = character != null ? character.portrait : null;

        if (characterId == lastDisplayedCharacterId && portrait == lastDisplayedPlayerPortrait)
            return;

        playerPortraitImage.sprite = portrait;
        playerPortraitImage.enabled = portrait != null || !hidePlayerPortraitWhenMissing;
        lastDisplayedCharacterId = characterId;
        lastDisplayedPlayerPortrait = portrait;
    }

    private void RefreshPlayerName()
    {
        if (playerNameText == null)
            return;

        PlayerCharacterData character = ResolveSelectedCharacter();
        string resolvedName = ResolveSelectedCharacterName(character);
        string formattedName = FormatText(playerNameFormat, resolvedName);

        if (formattedName == lastDisplayedPlayerName)
            return;

        playerNameText.text = formattedName;
        lastDisplayedPlayerName = formattedName;
    }

    private string ResolveSelectedCharacterName(PlayerCharacterData character)
    {
        CachePlayerStats();
        string selectedCharacterId = playerStats != null ? playerStats.SelectedCharacterId : string.Empty;
        if (string.IsNullOrWhiteSpace(selectedCharacterId))
            selectedCharacterId = PlayerCharacterSelection.GetSelectedCharacterId();

        PlayerCharacterData selectedCharacter = ResolveCharacterById(selectedCharacterId);
        if (selectedCharacter != null)
            character = selectedCharacter;

        if (character != null)
        {
            if (!string.IsNullOrWhiteSpace(character.displayName))
                return character.displayName.Trim();

            return character.GetCharacterId();
        }

        if (playerStats != null && !string.IsNullOrWhiteSpace(playerStats.CharacterName))
            return playerStats.CharacterName.Trim();

        return string.IsNullOrWhiteSpace(selectedCharacterId) ? "Player" : selectedCharacterId.Trim();
    }

    private PlayerCharacterData ResolveCharacterById(string characterId)
    {
        if (playerCharacterDatabase == null)
            playerCharacterDatabase = Resources.Load<PlayerCharacterDatabase>("PlayerCharacterDatabase");

        if (playerCharacterDatabase == null || string.IsNullOrWhiteSpace(characterId))
            return null;

        var characters = playerCharacterDatabase.Characters;
        if (characters == null)
            return null;

        string normalizedId = characterId.Trim();
        for (int i = 0; i < characters.Length; i++)
        {
            var candidate = characters[i];
            if (candidate == null)
                continue;

            if (string.Equals(candidate.GetCharacterId(), normalizedId, System.StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        return null;
    }

    private static string FormatText(string format, object value)
    {
        if (string.IsNullOrEmpty(format))
            return value?.ToString() ?? string.Empty;

        try
        {
            return string.Format(format, value);
        }
        catch (System.FormatException)
        {
            return value?.ToString() ?? string.Empty;
        }
    }

    private PlayerCharacterData ResolveSelectedCharacter()
    {
        if (playerCharacterDatabase == null)
            playerCharacterDatabase = Resources.Load<PlayerCharacterDatabase>("PlayerCharacterDatabase");

        if (playerCharacterDatabase == null)
            return null;

        CachePlayerStats();
        string selectedCharacterId = playerStats != null ? playerStats.SelectedCharacterId : string.Empty;
        if (string.IsNullOrWhiteSpace(selectedCharacterId))
            selectedCharacterId = PlayerCharacterSelection.GetSelectedCharacterId();

        return playerCharacterDatabase.GetById(selectedCharacterId);
    }

    private void BindAttributeButtons()
    {
        if (attributeRows == null) return;

        for (int i = 0; i < attributeRows.Count; i++)
        {
            var row = attributeRows[i];
            if (row == null) continue;

            string key = string.IsNullOrWhiteSpace(row.key) ? row.root != null ? row.root.name : string.Empty : row.key;
            if (string.IsNullOrWhiteSpace(key)) continue;
            string capturedKey = key.Trim().ToLowerInvariant();

            if (row.decreaseButtonReference != null)
            {
                row.decreaseButtonReference.onClick.RemoveAllListeners();
                row.decreaseButtonReference.onClick.AddListener(() => OnAttributeDecreaseClicked(capturedKey));
            }

            if (row.increaseButtonReference != null)
            {
                row.increaseButtonReference.onClick.RemoveAllListeners();
                row.increaseButtonReference.onClick.AddListener(() => OnAttributeIncreaseClicked(capturedKey));
            }
        }
    }

    private void BindConfirmButton()
    {
        if (confirmButton == null) return;

        confirmButton.onClick.RemoveAllListeners();
        confirmButton.onClick.AddListener(ConfirmPendingAllocation);
    }

    private void RefreshAttributeRowsValues()
    {
        if (attributeRows == null || playerStats == null) return;
        for (int i = 0; i < attributeRows.Count; i++)
        {
            var row = attributeRows[i];
            if (row == null || string.IsNullOrWhiteSpace(row.key)) continue;

            string statName = MapAttributeKeyToStatName(row.key);
            int value = playerStats.GetPersistentStat(statName) + GetPendingAttributeLevels(statName);
            if (row.valueText != null) row.valueText.text = value.ToString();
            if (row.descText != null)
            {
                string description = GetDefaultAttributeDescription(row.key);
                if (!string.IsNullOrWhiteSpace(description))
                    row.descText.text = description;
            }

            if (row.decreaseButtonReference != null)
            {
                row.decreaseButtonReference.gameObject.SetActive(true);
                row.decreaseButtonReference.interactable = GetPendingAttributeLevels(statName) > 0;
            }

            if (row.increaseButtonReference != null)
            {
                row.increaseButtonReference.gameObject.SetActive(true);
                row.increaseButtonReference.interactable = CanIncreaseAttributeKey(row.key);
            }
        }

        if (confirmButton != null)
        {
            bool hasPendingAllocation = HasPendingAllocation();
            if (!hasPendingAllocation)
                confirmPadFocused = false;

            confirmButton.gameObject.SetActive(hasPendingAllocation);
            confirmButton.interactable = hasPendingAllocation;
        }
    }

    private void RefreshUnspentAttributePoints()
    {
        int remainingPoints = GetRemainingAttributePoints();
        bool hasUnspentPoints = remainingPoints > 0;

        if (txtPointParent != null)
            txtPointParent.SetActive(hasUnspentPoints);

        if (txtPointValue != null)
            txtPointValue.text = remainingPoints.ToString();
    }

    private void RefreshAttributeDerivedPanel()
    {
        if (playerStats == null) return;

        playerStats.RefreshArmorTotals();

        int level = Mathf.Max(1, playerStats.playerLevel);
        float xpProgress = playerStats.GetLevelProgress01();

        if (attributesLevelLabelText != null)
            attributesLevelLabelText.text = "Level";
        string xpText = $"{playerStats.levelExperience}/{playerStats.experienceToNextLevel}";
        if (attributesLevelValueText != null) attributesLevelValueText.text = level.ToString();
        if (attributesXpValueText != null) attributesXpValueText.text = xpText;
        ApplyProgress(attributesXpProgressBar, xpProgress, xpText);

        int hp = Mathf.RoundToInt(playerStats.maxHealth);
        int mana = Mathf.RoundToInt(playerStats.maxMana);
        int stamina = Mathf.RoundToInt(playerStats.maxStamina);
        int basePhyDamage = playerStats.GetBasePhysicalDamage();
        int magicDamage = playerStats.GetBaseMagicDamage();
        int phyDef = playerStats.GetPhysicalDefense();
        int magicDef = playerStats.GetMagicDefense();

        int previewVigor = playerStats.vigor + GetPendingAttributeLevels("vigor");
        int previewMind = playerStats.mind + GetPendingAttributeLevels("mind");
        int previewEndurance = playerStats.endurance + GetPendingAttributeLevels("endurance");
        int previewStrength = playerStats.strength + GetPendingAttributeLevels("strength");
        int previewIntelligence = playerStats.intelligence + GetPendingAttributeLevels("intelligence");

        int previewHp = Mathf.RoundToInt(playerStats.GetMaxHealth(previewVigor));
        int previewMana = Mathf.RoundToInt(playerStats.GetMaxMana(previewMind));
        int previewStamina = Mathf.RoundToInt(playerStats.GetMaxStamina(previewEndurance));
        int previewBasePhyDamage = playerStats.GetBasePhysicalDamage(previewStrength);
        int previewMagicDamage = playerStats.GetBaseMagicDamage(previewIntelligence);
        int previewPhyDef = playerStats.GetPhysicalDefense(previewEndurance);
        int previewMagicDef = playerStats.GetMagicDefense(previewMind);

        float equipWeight = playerStats.GetCurrentEquipLoad();
        float maxLoad = playerStats.GetMaxEquipLoad();
        float previewMaxLoad = playerStats.GetMaxEquipLoad(previewEndurance);
        float loadRatio = Mathf.Clamp01(maxLoad > 0f ? equipWeight / maxLoad : 0f);
        string loadTierLabel = playerController != null ? playerController.GetEquipLoadTierLabel() : ResolveLoadTierLabelFallback(loadRatio);

        if (attributesHpValueText != null) attributesHpValueText.text = FormatPreviewValue(hp, previewHp);
        if (attributesManaValueText != null) attributesManaValueText.text = FormatPreviewValue(mana, previewMana);
        if (attributesStaminaValueText != null) attributesStaminaValueText.text = FormatPreviewValue(stamina, previewStamina);
        if (attributesBasePhyDamageValueText != null) attributesBasePhyDamageValueText.text = FormatPreviewValue(basePhyDamage, previewBasePhyDamage);
        if (attributesMagicDamageValueText != null) attributesMagicDamageValueText.text = FormatPreviewValue(magicDamage, previewMagicDamage);
        if (attributesPhyDefValueText != null) attributesPhyDefValueText.text = FormatPreviewValue(phyDef, previewPhyDef);
        if (attributesMagicDefValueText != null) attributesMagicDefValueText.text = FormatPreviewValue(magicDef, previewMagicDef);
        string loadText = equipWeight.ToString("0.0") + " / " + maxLoad.ToString("0.0")
                          + FormatPreviewDelta(previewMaxLoad - maxLoad, "0.0");
        if (attributesLoadValueText != null) attributesLoadValueText.text = loadText;
        if (attributesLoadTierValueText != null) attributesLoadTierValueText.text = loadTierLabel;
        ApplyProgress(attributesLoadProgressBar, loadRatio, loadText);
    }

    private string FormatPreviewValue(int currentValue, int previewValue)
    {
        return currentValue + FormatPreviewDelta(previewValue - currentValue, "0");
    }

    private string FormatPreviewDelta(float delta, string numberFormat)
    {
        if (Mathf.Approximately(delta, 0f))
            return string.Empty;

        string sign = delta > 0f ? "+" : string.Empty;
        string color = ColorUtility.ToHtmlStringRGB(pendingPreviewColor);
        return $" <color=#{color}>({sign}{delta.ToString(numberFormat)})</color>";
    }

    private static string ResolveLoadTierLabelFallback(float loadRatio)
    {
        if (loadRatio < 0.20f) return "Fast";
        if (loadRatio > 0.80f) return "Slow";
        return "Normal";
    }

    private static void ApplyProgress(ProgressBarUI bar, float normalized, string displayText)
    {
        if (bar == null) return;
        bar.SetProgress(normalized, displayText);
    }

    private void RefreshAttributeSelectionVisual()
    {
        if (attributeRows == null) return;
        if (string.IsNullOrWhiteSpace(selectedAttributeKey))
            selectedAttributeKey = GetFirstAttributeKey();
        bool canInteract = HasAttributePointsToSpend() || HasPendingAllocation();

        for (int i = 0; i < attributeRows.Count; i++)
        {
            var row = attributeRows[i];
            if (row == null || string.IsNullOrWhiteSpace(row.key)) continue;

            bool selected = !confirmPadFocused
                            && canInteract
                            && string.Equals(row.key, selectedAttributeKey, System.StringComparison.OrdinalIgnoreCase);
            Color color = selected && showPadFocus ? attributesSelectedColor : attributesNormalColor;
            if (row.labelText != null) row.labelText.color = color;
            if (row.valueText != null) row.valueText.color = color;
        }

        RefreshConfirmPadFocusVisual();
    }

    private void OnAttributeIncreaseClicked(string key)
    {
        CachePlayerStats();
        if (playerStats == null || string.IsNullOrWhiteSpace(key)) return;

        string statName = MapAttributeKeyToStatName(key);
        if (string.IsNullOrWhiteSpace(statName)) return;
        if (!IsAllocatableAttribute(statName)) return;
        if (!CanIncreaseAttributeKey(statName)) return;

        if (!allocationSessionActive)
            allocationSessionActive = true;

        confirmPadFocused = false;
        pendingAttributeLevels.TryGetValue(statName, out int pendingLevels);
        pendingAttributeLevels[statName] = pendingLevels + 1;

        selectedAttributeKey = key;
        RefreshUI();
    }

    private void OnAttributeDecreaseClicked(string key)
    {
        string statName = MapAttributeKeyToStatName(key);
        if (string.IsNullOrWhiteSpace(statName)) return;
        if (!pendingAttributeLevels.TryGetValue(statName, out int pendingLevels) || pendingLevels <= 0) return;

        confirmPadFocused = false;
        if (pendingLevels == 1)
            pendingAttributeLevels.Remove(statName);
        else
            pendingAttributeLevels[statName] = pendingLevels - 1;

        selectedAttributeKey = key;
        RefreshUI();
    }

    private void ConfirmPendingAllocation()
    {
        CachePlayerStats();
        if (playerStats == null || !HasPendingAllocation()) return;
        if (!playerStats.TrySpendAttributePoints(pendingAttributeLevels))
        {
            RefreshUI();
            return;
        }

        pendingAttributeLevels.Clear();
        allocationSessionActive = true;
        confirmPadFocused = false;
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
            if (IsAttributeRowNavigable(i)
                && string.Equals(row.key.Trim(), selectedAttributeKey.Trim(), System.StringComparison.OrdinalIgnoreCase))
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
        return row != null
               && row.root != null
               && row.root.gameObject.activeInHierarchy
               && !string.IsNullOrWhiteSpace(row.key)
               && IsAllocatableAttribute(row.key);
    }

    private int GetLastNavigableAttributeIndex()
    {
        if (attributeRows == null) return -1;

        for (int i = attributeRows.Count - 1; i >= 0; i--)
            if (IsAttributeRowNavigable(i)) return i;

        return -1;
    }

    private void RefreshConfirmPadFocusVisual()
    {
        if (confirmButton == null) return;

        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        if (eventSystem == null) return;

        bool shouldSelectConfirm = confirmPadFocused
                                   && showPadFocus
                                   && confirmButton.gameObject.activeInHierarchy
                                   && confirmButton.interactable;
        if (shouldSelectConfirm)
        {
            if (eventSystem.currentSelectedGameObject != confirmButton.gameObject)
                eventSystem.SetSelectedGameObject(confirmButton.gameObject);
        }
        else if (eventSystem.currentSelectedGameObject == confirmButton.gameObject)
        {
            eventSystem.SetSelectedGameObject(null);
        }
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

    private bool CanIncreaseAttributeKey(string key)
    {
        if (GetRemainingAttributePoints() <= 0 || !CanAllocateAttributeKey(key)) return false;

        string statName = MapAttributeKeyToStatName(key);
        int previewValue = playerStats.GetPersistentStat(statName) + GetPendingAttributeLevels(statName);
        return previewValue < PlayerStats.MaxAllocatableAttributeLevel;
    }

    private int GetPendingAttributeLevels(string statName)
    {
        if (string.IsNullOrWhiteSpace(statName)) return 0;
        return pendingAttributeLevels.TryGetValue(statName.Trim().ToLowerInvariant(), out int levels)
            ? Mathf.Max(0, levels)
            : 0;
    }

    private int GetPendingAttributeLevelCount()
    {
        int total = 0;
        foreach (var entry in pendingAttributeLevels)
            total += Mathf.Max(0, entry.Value);
        return total;
    }

    private int GetRemainingAttributePoints()
    {
        CachePlayerStats();
        if (playerStats == null) return 0;
        return Mathf.Max(0, playerStats.unspentAttributePoints - GetPendingAttributeLevelCount());
    }

    private static string GetDefaultAttributeDescription(string key)
    {
        string k = string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim().ToLowerInvariant();
        switch (k)
        {
            case "vigor": return "Increases HP.";
            case "mind": return "Increases Mana & Magic Resist.";
            case "endurance": return "Increases Stamina, Load & Phy Def.";
            case "strength": return "Increases Physical & STR Damage.";
            case "dexterity": return "Increases Ranged & DEX Damage.";
            case "intelligence": return "Increases Magic & INT Damage.";
            case "faith": return "Increases Magic Resistance.";
            case "evil": return "Represents your dark alignment.";
            case "karma": return "Represents your moral balance.";
            default: return string.Empty;
        }
    }

}
