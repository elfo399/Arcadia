
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class QuestJournalUI : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private MenuManager menuManager;
    [SerializeField] private InventoryUIManager inventoryUIManager;
    [SerializeField] private MagicInventoryManager magicInventoryManager;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private PlayerStats playerStats;

    [Header("Quest UI")]
    [SerializeField] private bool useQuestManager = true;
    [SerializeField] private Transform questListContainer;
    [SerializeField] private QuestItemUI questItemPrefab;
    [SerializeField] private List<QuestEntryData> startingQuests = new();

    [Header("Quest Detail UI")]
    [SerializeField] private TextMeshProUGUI questDetailTypeText;
    [SerializeField] private TextMeshProUGUI questDetailRecommendedText;
    [SerializeField] private Image questDetailImage;
    [SerializeField] private TextMeshProUGUI questDetailTitleText;
    [SerializeField] private TextMeshProUGUI questDetailLocationText;
    [SerializeField] private TextMeshProUGUI questDetailLoreTitleText;
    [SerializeField] private TextMeshProUGUI questDetailLoreDescriptionText;
    [SerializeField] private TextMeshProUGUI questDetailLoreAuthorText;
    [SerializeField] private RectTransform questDetailPanelRoot;
    [SerializeField] private bool showQuestDetailOnlyOnSelection = true;
    [SerializeField] private Transform questObjectivesContainer;
    [SerializeField] private QuestObjectiveItemUI questObjectivePrefab;
    [SerializeField] private Transform questRewardsContainer;
    [SerializeField] private QuestRewardItemUI questRewardPrefab;
    [SerializeField] private Button questClaimRewardButton;
    [SerializeField] private int questRewardInventoryCapacity = -1;
    [SerializeField] private int questRewardMagicCapacity = -1;
    [SerializeField] private Color questPadFocusBorderColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Vector2 questPadFocusBorderThickness = new Vector2(3f, 3f);

    private QuestManager questManager;
    private bool initialized;
    private bool questManagerSubscribed;
    private Transform questFocusedTransform;
    private Outline questFocusOutline;
    private readonly List<QuestItemUI> spawnedQuestRows = new();
    private readonly List<QuestObjectiveItemUI> spawnedObjectiveRows = new();
    private readonly List<QuestRewardItemUI> spawnedRewardRows = new();
    private float suppressQuestRowClickUntil;

    public bool UseQuestManager { get => useQuestManager; set => useQuestManager = value; }
    public Transform QuestListContainer { get => questListContainer; set => questListContainer = value; }
    public QuestItemUI QuestItemPrefab { get => questItemPrefab; set => questItemPrefab = value; }
    public List<QuestEntryData> StartingQuests => startingQuests;
    public TextMeshProUGUI QuestDetailTypeText { get => questDetailTypeText; set => questDetailTypeText = value; }
    public TextMeshProUGUI QuestDetailRecommendedText { get => questDetailRecommendedText; set => questDetailRecommendedText = value; }
    public Image QuestDetailImage { get => questDetailImage; set => questDetailImage = value; }
    public TextMeshProUGUI QuestDetailTitleText { get => questDetailTitleText; set => questDetailTitleText = value; }
    public TextMeshProUGUI QuestDetailLocationText { get => questDetailLocationText; set => questDetailLocationText = value; }
    public TextMeshProUGUI QuestDetailLoreTitleText { get => questDetailLoreTitleText; set => questDetailLoreTitleText = value; }
    public TextMeshProUGUI QuestDetailLoreDescriptionText { get => questDetailLoreDescriptionText; set => questDetailLoreDescriptionText = value; }
    public TextMeshProUGUI QuestDetailLoreAuthorText { get => questDetailLoreAuthorText; set => questDetailLoreAuthorText = value; }
    public RectTransform QuestDetailPanelRoot { get => questDetailPanelRoot; set => questDetailPanelRoot = value; }
    public bool ShowQuestDetailOnlyOnSelection { get => showQuestDetailOnlyOnSelection; set => showQuestDetailOnlyOnSelection = value; }
    public Transform QuestObjectivesContainer { get => questObjectivesContainer; set => questObjectivesContainer = value; }
    public QuestObjectiveItemUI QuestObjectivePrefab { get => questObjectivePrefab; set => questObjectivePrefab = value; }
    public Transform QuestRewardsContainer { get => questRewardsContainer; set => questRewardsContainer = value; }
    public QuestRewardItemUI QuestRewardPrefab { get => questRewardPrefab; set => questRewardPrefab = value; }
    public Button QuestClaimRewardButton { get => questClaimRewardButton; set => questClaimRewardButton = value; }
    public int QuestRewardInventoryCapacity { get => questRewardInventoryCapacity; set => questRewardInventoryCapacity = value; }
    public int QuestRewardMagicCapacity { get => questRewardMagicCapacity; set => questRewardMagicCapacity = value; }
    public Color QuestPadFocusBorderColor { get => questPadFocusBorderColor; set => questPadFocusBorderColor = value; }
    public Vector2 QuestPadFocusBorderThickness { get => questPadFocusBorderThickness; set => questPadFocusBorderThickness = value; }

    private void OnDestroy()
    {
        UnbindQuestManager();
    }

    public void InitializeIfNeeded()
    {
        if (initialized)
            return;

        initialized = true;

        ResolveDependencies();

        if (questListContainer != null)
        {
            if (questItemPrefab != null && questItemPrefab.transform.parent == questListContainer)
                questItemPrefab.gameObject.SetActive(false);
        }

        if (questObjectivesContainer != null && questObjectivePrefab != null && questObjectivePrefab.transform.parent == questObjectivesContainer)
            questObjectivePrefab.gameObject.SetActive(false);
        if (questRewardsContainer != null && questRewardPrefab != null && questRewardPrefab.transform.parent == questRewardsContainer)
            questRewardPrefab.gameObject.SetActive(false);

        TryBindQuestManager();

        if (useQuestManager && questManager != null)
        {
            questManager.SeedFromInventoryEntriesIfEmpty(startingQuests);
            questManager.MergeMissingDetailsFromInventoryEntries(startingQuests);
        }
    }

    public void RefreshUI(bool showPadFocus)
    {
        InitializeIfNeeded();
        TryBindQuestManager();
        RebuildQuestRows(showPadFocus);
        RefreshSelectedQuestDetails();
        if (showPadFocus && IsQuestTabVisualActive())
            ApplyPadFocusVisual(showPadFocus);
    }

    public void HideActiveDetailForMenuClose()
    {
        UpdateQuestDetailPanel(null);
        ClearPadFocusVisual();
    }

    public void SetQuestFilterAll() { RefreshUI(IsPadFocusVisible()); }
    public void SetQuestFilterActive() { SetQuestFilterAll(); }
    public void SetQuestFilterCompleted() { SetQuestFilterAll(); }

    public void AddOrUpdateQuest(string questId, string title, string location, bool completed)
    {
        TryBindQuestManager();
        if (questManager == null) return;
        questManager.AddOrUpdateQuest(questId, title, location, completed);
    }

    public bool SetQuestCompleted(string questId, bool completed = true)
    {
        TryBindQuestManager();
        return questManager != null && questManager.SetQuestCompleted(questId, completed);
    }

    public bool SetQuestObjectiveCompleted(string questId, int objectiveIndex, bool completed = true)
    {
        TryBindQuestManager();
        return questManager != null && questManager.SetQuestObjectiveCompleted(questId, objectiveIndex, completed);
    }

    public bool SetQuestObjectiveCompleted(string questId, string objectiveTitle, bool completed = true)
    {
        TryBindQuestManager();
        return questManager != null && questManager.SetQuestObjectiveCompleted(questId, objectiveTitle, completed);
    }

    public void FocusPadDefault(bool showPadFocus)
    {
        TryBindQuestManager();
        if (questManager == null) return;
        questManager.FocusJournalPadDefault();
        ApplyPadFocusVisual(showPadFocus);
    }

    public void FocusPadFilters(bool showPadFocus)
    {
        FocusPadDefault(showPadFocus);
    }

    public void MovePadFocusHorizontal(int direction, bool showPadFocus)
    {
        TryBindQuestManager();
        if (questManager == null) return;
        questManager.MoveJournalPadFocusHorizontal(direction);
        ApplyPadFocusVisual(showPadFocus);
    }

    public void MovePadFocusVertical(int direction, bool showPadFocus)
    {
        TryBindQuestManager();
        if (questManager == null) return;
        questManager.MoveJournalPadFocusVertical(direction);
        ApplyPadFocusVisual(showPadFocus);
    }
    public void ConfirmPadSelection(bool showPadFocus)
    {
        TryBindQuestManager();
        if (questManager == null) return;

        string previousQuestId = questManager.SelectedJournalQuestId;
        EnsurePlayerStats();
        bool claimed = questManager.ConfirmJournalSelection(playerInventory, playerStats, GetQuestRewardNormalCapacityValue(), GetQuestRewardMagicCapacityValue());
        if (claimed)
        {
            RefreshQuestSourcesFromPlayer();
            RefreshUI(showPadFocus);
            return;
        }

        if (!string.Equals(previousQuestId, questManager.SelectedJournalQuestId, StringComparison.OrdinalIgnoreCase))
            LogSelectedQuestCompletionState();

        RefreshQuestRowsSelection();
        RefreshSelectedQuestDetails();
        if (showPadFocus && IsQuestTabVisualActive())
            ApplyPadFocusVisual(showPadFocus);
    }

    public bool HandlePadBack(bool showPadFocus)
    {
        TryBindQuestManager();
        if (questManager == null || !questManager.HandleJournalBack())
            return false;

        ApplyPadFocusVisual(showPadFocus);
        return true;
    }

    public void ApplyPadFocusVisual(bool showPadFocus)
    {
        if (!IsQuestTabVisualActive())
        {
            ClearPadFocusVisual();
            return;
        }

        GameObject selectedTarget = null;
        GameObject visualTarget = null;
        switch (questManager != null ? questManager.CurrentJournalPadSection : QuestManager.JournalPadSection.List)
        {
            case QuestManager.JournalPadSection.List:
                var rowButton = GetVisibleQuestRowAt(questManager != null ? questManager.JournalPadListIndex : 0);
                selectedTarget = rowButton != null ? rowButton.gameObject : null;
                visualTarget = selectedTarget;
                break;
            case QuestManager.JournalPadSection.Detail:
                selectedTarget = questClaimRewardButton != null && questClaimRewardButton.gameObject.activeInHierarchy ? questClaimRewardButton.gameObject : null;
                visualTarget = selectedTarget;
                break;
        }

        if (selectedTarget == null) selectedTarget = visualTarget;
        if (visualTarget == null) visualTarget = selectedTarget;

        if (EventSystem.current != null)
        {
            if (selectedTarget != null)
                EventSystem.current.SetSelectedGameObject(selectedTarget);
            else
                EventSystem.current.SetSelectedGameObject(null);
        }

        SetQuestPadFocusVisualTarget(showPadFocus ? visualTarget : null);
    }

    public void ClearPadFocusVisual()
    {
        if (questFocusOutline != null)
            questFocusOutline.enabled = false;
        questFocusedTransform = null;
        questFocusOutline = null;
    }

    private void TryBindQuestManager()
    {
        if (!useQuestManager) return;
        if (questManager == null)
            questManager = QuestManager.Instance;
        if (questManager == null || questManagerSubscribed)
            return;

        questManager.OnQuestListChanged += HandleQuestManagerListChanged;
        questManagerSubscribed = true;
    }

    private void UnbindQuestManager()
    {
        if (!questManagerSubscribed || questManager == null)
            return;
        questManager.OnQuestListChanged -= HandleQuestManagerListChanged;
        questManagerSubscribed = false;
    }

    private void HandleQuestManagerListChanged(List<QuestManager.QuestData> _)
    {
        if (!isActiveAndEnabled || !IsQuestTabVisualActive())
            return;

        RefreshUI(IsPadFocusVisible());
    }

    private void OnQuestRowClicked(string questId)
    {
        if (Time.unscaledTime < suppressQuestRowClickUntil)
            return;
        if (string.IsNullOrWhiteSpace(questId) || questManager == null)
            return;
        if (questManager.IsJournalQuestSelected(questId))
            return;

        questManager.SelectJournalQuest(questId);
        LogSelectedQuestCompletionState();
        RefreshQuestRowsSelection();
        RefreshSelectedQuestDetails();

        bool showPadFocus = IsPadFocusVisible();
        if (showPadFocus && IsQuestTabVisualActive())
            ApplyPadFocusVisual(showPadFocus);
    }

    private void RefreshSelectedQuestDetails()
    {
        UpdateQuestDetailPanel(questManager != null ? questManager.GetSelectedVisibleJournalQuest() : null);
    }

    private void LogSelectedQuestCompletionState()
    {
        if (questManager == null)
            return;

        var quest = questManager.GetSelectedVisibleJournalQuest();
        if (quest == null)
            return;

        bool readyToClaim = questManager.IsQuestReadyToClaim(quest);
        EnsurePlayerStats();

        bool canClaim = questManager.CanClaimJournalQuest(quest, playerInventory, playerStats, GetQuestRewardNormalCapacityValue(), GetQuestRewardMagicCapacityValue(), out string failureReason);
        string claimDetails = canClaim ? "claim=si" : $"claim=no, reason={failureReason}";
        Debug.Log($"[QuestJournalUI] Quest selezionata '{quest.questId}' ({quest.title}): completedFlag={(quest.completed ? "si" : "no")}, readyToClaim={(readyToClaim ? "si" : "no")}, {claimDetails}.", this);
    }

    private void UpdateQuestDetailPanel(QuestEntryData quest)
    {
        if (questDetailPanelRoot != null && showQuestDetailOnlyOnSelection)
            questDetailPanelRoot.gameObject.SetActive(quest != null);

        if (questDetailTypeText != null) questDetailTypeText.text = quest != null ? (quest.questTypeLabel ?? string.Empty) : string.Empty;
        if (questDetailRecommendedText != null) questDetailRecommendedText.text = quest != null ? (quest.recommendedLabel ?? string.Empty) : string.Empty;
        if (questDetailImage != null)
        {
            Sprite image = quest != null ? quest.questImage : null;
            questDetailImage.sprite = image;
            questDetailImage.enabled = image != null;
        }
        if (questDetailTitleText != null) questDetailTitleText.text = quest != null ? (quest.title ?? string.Empty) : string.Empty;
        if (questDetailLocationText != null) questDetailLocationText.text = quest != null ? (quest.location ?? string.Empty) : string.Empty;
        if (questDetailLoreTitleText != null) questDetailLoreTitleText.text = quest != null ? (quest.loreTitle ?? string.Empty) : string.Empty;
        if (questDetailLoreDescriptionText != null) questDetailLoreDescriptionText.text = quest != null ? (quest.loreDescription ?? string.Empty) : string.Empty;
        if (questDetailLoreAuthorText != null) questDetailLoreAuthorText.text = quest != null ? (quest.loreAuthor ?? string.Empty) : string.Empty;

        UpdateQuestClaimButtonState(quest);
        RebuildQuestObjectiveRows(quest);
        RebuildQuestRewardRows(quest);
    }

    private void UpdateQuestClaimButtonState(QuestEntryData quest)
    {
        if (questClaimRewardButton == null) return;
        bool shouldShowButton = quest != null && !quest.rewardClaimed;
        if (questClaimRewardButton.gameObject.activeSelf != shouldShowButton)
            questClaimRewardButton.gameObject.SetActive(shouldShowButton);

        if (!shouldShowButton)
        {
            questClaimRewardButton.interactable = false;
            return;
        }

        if (questManager == null)
        {
            questClaimRewardButton.interactable = false;
            return;
        }

        EnsurePlayerStats();
        bool canClaim = questManager.CanClaimJournalQuest(quest, playerInventory, playerStats, GetQuestRewardNormalCapacityValue(), GetQuestRewardMagicCapacityValue(), out string failureReason);
        questClaimRewardButton.interactable = canClaim;

        string questId = quest != null ? quest.questId : "none";
        Debug.Log($"[QuestJournalUI] Claim button state: quest='{questId}', canClaim={(canClaim ? "si" : "no")}, interactable={(questClaimRewardButton.interactable ? "si" : "no")}, isInteractable={(questClaimRewardButton.IsInteractable() ? "si" : "no")}, activeInHierarchy={(questClaimRewardButton.gameObject.activeInHierarchy ? "si" : "no")}{(canClaim ? string.Empty : ", reason=" + failureReason)}.", this);
    }

    public void OnQuestClaimRewardButtonClicked()
    {
        Debug.Log("[QuestJournalUI] Claim button clicked.", this);

        if (questManager == null)
        {
            Debug.LogWarning("[QuestJournalUI] Claim reward failed: QuestManager non assegnato.", this);
            return;
        }

        EnsurePlayerStats();
        var selectedQuest = questManager.GetSelectedVisibleJournalQuest();
        if (!questManager.TryClaimSelectedQuestRewards(playerInventory, playerStats, GetQuestRewardNormalCapacityValue(), GetQuestRewardMagicCapacityValue()))
        {
            if (!questManager.CanClaimJournalQuest(selectedQuest, playerInventory, playerStats, GetQuestRewardNormalCapacityValue(), GetQuestRewardMagicCapacityValue(), out string failureReason))
                Debug.LogWarning($"[QuestJournalUI] Claim reward failed for quest '{(selectedQuest != null ? selectedQuest.questId : "none")}': {failureReason}", this);
            else
                Debug.LogWarning($"[QuestJournalUI] Claim reward failed for quest '{selectedQuest.questId}': reward apply failed after validation.", this);
            RefreshUI(IsPadFocusVisible());
            return;
        }

        RefreshQuestSourcesFromPlayer();
        RefreshUI(IsPadFocusVisible());
        Debug.Log("[QuestJournalUI] Claim reward completed.", this);
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
            row.gameObject.SetActive(true);
            row.SetData(obj.title, FormatQuestObjectiveDescription(obj), obj.completed);
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
            row.gameObject.SetActive(true);
            row.SetData(ResolveRewardIcon(reward), ResolveRewardTypeText(reward), reward.amount, ResolveRewardItemName(reward));
            spawnedRewardRows.Add(row);
        }
    }

    private void RebuildQuestRows(bool showPadFocus)
    {
        if (questListContainer == null || questItemPrefab == null || questManager == null) return;
        ClearSpawnedQuestRows();

        var visible = questManager.GetVisibleJournalQuestEntriesSnapshot();
        for (int i = 0; i < visible.Count; i++)
        {
            var quest = visible[i];
            if (quest == null) continue;
            var row = Instantiate(questItemPrefab, questListContainer);
            row.gameObject.SetActive(true);
            row.SetData(quest.title, quest.location, quest.completed || questManager.IsQuestReadyToClaim(quest));
            row.SetSelected(questManager.IsJournalQuestSelected(quest.questId));

            string capturedQuestId = quest.questId;
            var rowButton = EnsureButton(row.gameObject);
            if (rowButton != null)
            {
                rowButton.onClick.RemoveAllListeners();
                rowButton.onClick.AddListener(() => OnQuestRowClicked(capturedQuestId));
            }

            spawnedQuestRows.Add(row);
        }

        if (showPadFocus)
            ApplyPadFocusVisual(true);
    }

    private void RefreshQuestRowsSelection()
    {
        if (questManager == null)
            return;

        var visible = questManager.GetVisibleJournalQuestEntriesSnapshot();
        int count = Mathf.Min(spawnedQuestRows.Count, visible.Count);
        for (int i = 0; i < count; i++)
        {
            var row = spawnedQuestRows[i];
            var quest = visible[i];
            if (row == null || quest == null)
                continue;

            row.SetSelected(questManager.IsJournalQuestSelected(quest.questId));
        }
    }

    private void ClearSpawnedQuestRows()
    {
        for (int i = 0; i < spawnedQuestRows.Count; i++)
        {
            if (spawnedQuestRows[i] != null)
                Destroy(spawnedQuestRows[i].gameObject);
        }

        if (questListContainer == null)
        {
            spawnedQuestRows.Clear();
            return;
        }

        for (int i = questListContainer.childCount - 1; i >= 0; i--)
        {
            var child = questListContainer.GetChild(i).gameObject;
            if (questItemPrefab != null && child == questItemPrefab.gameObject) continue;
            Destroy(child);
        }

        spawnedQuestRows.Clear();
    }

    private void SetQuestPadFocusVisualTarget(GameObject target)
    {
        target = ResolveQuestFocusGraphicTarget(target);
        if (target != null && target.transform == questFocusedTransform && questFocusOutline != null)
        {
            questFocusOutline.enabled = true;
            questFocusOutline.effectColor = questPadFocusBorderColor;
            questFocusOutline.effectDistance = questPadFocusBorderThickness;
            return;
        }

        ClearPadFocusVisual();
        if (target == null) return;

        var outline = target.GetComponent<Outline>();
        bool created = false;
        if (outline == null)
        {
            outline = target.AddComponent<Outline>();
            created = true;
        }

        outline.effectColor = questPadFocusBorderColor;
        outline.effectDistance = questPadFocusBorderThickness;
        outline.enabled = true;
        questFocusedTransform = target.transform;
        questFocusOutline = outline;
        if (created && outline.useGraphicAlpha == false)
            outline.useGraphicAlpha = true;
    }
    private Button GetVisibleQuestRowAt(int index)
    {
        if (index < 0) return null;
        int cursor = 0;
        for (int i = 0; i < spawnedQuestRows.Count; i++)
        {
            var row = spawnedQuestRows[i];
            if (row == null || !row.gameObject.activeInHierarchy) continue;
            if (cursor == index) return row.GetComponent<Button>();
            cursor++;
        }
        return null;
    }

    public void SetPadFocusVisible(bool visible)
    {
        if (!IsQuestTabVisualActive())
        {
            ClearPadFocusVisual();
            return;
        }

        ApplyPadFocusVisual(visible);
    }

    public bool IsQuestTabVisualActive()
    {
        if (menuManager == null)
            return false;

        string tabKey = menuManager.CurrentTabKey;
        return string.Equals(tabKey, "Quest", StringComparison.OrdinalIgnoreCase)
               || string.Equals(tabKey, "Quests", StringComparison.OrdinalIgnoreCase)
               || string.Equals(tabKey, "Journal", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsPadFocusVisible()
    {
        return menuManager != null && menuManager.IsPadFocusVisible;
    }

    private int GetQuestRewardNormalCapacityValue()
    {
        if (questRewardInventoryCapacity > 0)
            return questRewardInventoryCapacity;
        if (inventoryUIManager != null && inventoryUIManager.GetCapacity() > 0)
            return inventoryUIManager.GetCapacity();
        return 0;
    }

    private int GetQuestRewardMagicCapacityValue()
    {
        if (questRewardMagicCapacity > 0)
            return questRewardMagicCapacity;
        if (magicInventoryManager != null && magicInventoryManager.GetCapacity() > 0)
            return magicInventoryManager.GetCapacity();
        return 0;
    }

    private void RefreshQuestSourcesFromPlayer()
    {
        inventoryUIManager?.RefreshSourceItemsFromPlayer();
    }

    private void EnsurePlayerStats()
    {
        if (playerStats == null)
            playerStats = PlayerStats.instance;
    }

    private void ResolveDependencies()
    {
        EnsurePlayerStats();
    }

    private static void ClearSpawnedRows<T>(List<T> spawned, Transform container, T prefabRef) where T : Component
    {
        if (spawned != null)
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null) Destroy(spawned[i].gameObject);
            }
            spawned.Clear();
        }

        if (container == null) return;
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            var child = container.GetChild(i).gameObject;
            if (prefabRef != null && child == prefabRef.gameObject) continue;
            Destroy(child);
        }
    }

    private static Button EnsureButton(GameObject target)
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

    private static GameObject ResolveQuestFocusGraphicTarget(GameObject target)
    {
        if (target == null) return null;
        var button = target.GetComponent<Button>();
        if (button != null && button.targetGraphic != null) return button.targetGraphic.gameObject;
        var ownGraphic = target.GetComponent<Graphic>();
        if (ownGraphic != null) return ownGraphic.gameObject;
        var nestedGraphic = target.GetComponentInChildren<Graphic>(true);
        if (nestedGraphic != null) return nestedGraphic.gameObject;
        return target;
    }

    private static string NormalizeQuestId(string questId, string title, string location)
    {
        if (!string.IsNullOrWhiteSpace(questId)) return questId.Trim();
        string safeTitle = string.IsNullOrWhiteSpace(title) ? "Quest" : title.Trim();
        string safeLocation = string.IsNullOrWhiteSpace(location) ? "Unknown" : location.Trim();
        return safeTitle + "|" + safeLocation;
    }

    private static Sprite ResolveRewardIcon(QuestRewardEntryData reward)
    {
        if (reward == null)
            return null;
        return reward.rewardType switch
        {
            QuestRewardType.Weapon => reward.weaponAsset != null ? reward.weaponAsset.icon : null,
            QuestRewardType.Usable => reward.usableAsset != null ? reward.usableAsset.icon : null,
            QuestRewardType.Item => reward.itemAsset != null ? reward.itemAsset.icon : null,
            QuestRewardType.Magic => reward.magicAsset != null ? reward.magicAsset.icon : null,
            QuestRewardType.Armor => reward.armorAsset != null ? reward.armorAsset.icon : null,
            _ => null
        };
    }

    private static string ResolveRewardTypeText(QuestRewardEntryData reward)
    {
        if (reward == null)
            return string.Empty;
        return !string.IsNullOrWhiteSpace(reward.type) ? reward.type : reward.rewardType.ToString();
    }

    private static string ResolveRewardItemName(QuestRewardEntryData reward)
    {
        if (reward == null)
            return string.Empty;
        if (!string.IsNullOrWhiteSpace(reward.itemName))
            return reward.itemName;

        return reward.rewardType switch
        {
            QuestRewardType.Weapon => reward.weaponAsset != null ? reward.weaponAsset.weaponName : string.Empty,
            QuestRewardType.Usable => reward.usableAsset != null ? reward.usableAsset.itemName : string.Empty,
            QuestRewardType.Item => reward.itemAsset != null ? reward.itemAsset.itemName : string.Empty,
            QuestRewardType.Magic => reward.magicAsset != null ? reward.magicAsset.magicName : string.Empty,
            QuestRewardType.Armor => reward.armorAsset != null ? reward.armorAsset.itemName : string.Empty,
            _ => string.Empty
        };
    }

    private static string FormatQuestObjectiveDescription(QuestObjectiveEntryData objective)
    {
        if (objective == null)
            return string.Empty;

        string description = objective.description ?? string.Empty;
        if (objective.requiredAmount <= 1)
            return description;

        string progress = $"{Mathf.Clamp(objective.currentAmount, 0, objective.requiredAmount)}/{objective.requiredAmount}";
        return string.IsNullOrWhiteSpace(description) ? progress : description + " (" + progress + ")";
    }

}

