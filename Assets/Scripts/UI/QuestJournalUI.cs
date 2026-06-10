
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
    [SerializeField] private bool autoWireQuestUI = false;
    [SerializeField] private bool editorAutoAssignQuestPrefabs = false;
    [SerializeField] private Transform questListContainer;
    [SerializeField] private QuestItemUI questItemPrefab;
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
    [SerializeField] private RectTransform questDetailPanelRoot;
    [SerializeField] private bool showQuestDetailOnlyOnSelection = true;
    [SerializeField] private bool collapseQuestLoreWhenEmpty = true;
    [SerializeField] private Transform questObjectivesContainer;
    [SerializeField] private QuestObjectiveItemUI questObjectivePrefab;
    [SerializeField] private Transform questRewardsContainer;
    [SerializeField] private QuestRewardItemUI questRewardPrefab;
    [SerializeField] private Button questClaimRewardButton;
    [SerializeField] private int questRewardInventoryCapacity = -1;
    [SerializeField] private int questRewardMagicCapacity = -1;
    [SerializeField] private ScrollRect questDetailScrollRect;
    [SerializeField] private bool smoothQuestMouseWheel = true;
    [SerializeField] private float questMouseWheelStepNormalized = 0.10f;
    [SerializeField] private float questMouseWheelSmoothSpeed = 14f;
    [SerializeField] private float questPadRightStickScrollSpeed = 0.65f;
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
    private float questTargetScrollNormalized = 1f;
    private bool questTargetScrollInitialized;
    private float suppressQuestRowClickUntil;

    public bool UseQuestManager { get => useQuestManager; set => useQuestManager = value; }
    public bool AutoWireQuestUI { get => autoWireQuestUI; set => autoWireQuestUI = value; }
    public Transform QuestListContainer { get => questListContainer; set => questListContainer = value; }
    public QuestItemUI QuestItemPrefab { get => questItemPrefab; set => questItemPrefab = value; }
    public List<QuestEntryData> StartingQuests => startingQuests;
    public TextMeshProUGUI QuestDetailTypeText { get => questDetailTypeText; set => questDetailTypeText = value; }
    public TextMeshProUGUI QuestDetailRecommendedText { get => questDetailRecommendedText; set => questDetailRecommendedText = value; }
    public TextMeshProUGUI QuestDetailTitleText { get => questDetailTitleText; set => questDetailTitleText = value; }
    public TextMeshProUGUI QuestDetailLocationText { get => questDetailLocationText; set => questDetailLocationText = value; }
    public TextMeshProUGUI QuestDetailLoreTitleText { get => questDetailLoreTitleText; set => questDetailLoreTitleText = value; }
    public TextMeshProUGUI QuestDetailLoreDescriptionText { get => questDetailLoreDescriptionText; set => questDetailLoreDescriptionText = value; }
    public TextMeshProUGUI QuestDetailLoreAuthorText { get => questDetailLoreAuthorText; set => questDetailLoreAuthorText = value; }
    public RectTransform QuestDetailLoreRoot { get => questDetailLoreRoot; set => questDetailLoreRoot = value; }
    public RectTransform QuestDetailPanelRoot { get => questDetailPanelRoot; set => questDetailPanelRoot = value; }
    public bool ShowQuestDetailOnlyOnSelection { get => showQuestDetailOnlyOnSelection; set => showQuestDetailOnlyOnSelection = value; }
    public bool CollapseQuestLoreWhenEmpty { get => collapseQuestLoreWhenEmpty; set => collapseQuestLoreWhenEmpty = value; }
    public Transform QuestObjectivesContainer { get => questObjectivesContainer; set => questObjectivesContainer = value; }
    public QuestObjectiveItemUI QuestObjectivePrefab { get => questObjectivePrefab; set => questObjectivePrefab = value; }
    public Transform QuestRewardsContainer { get => questRewardsContainer; set => questRewardsContainer = value; }
    public QuestRewardItemUI QuestRewardPrefab { get => questRewardPrefab; set => questRewardPrefab = value; }
    public Button QuestClaimRewardButton { get => questClaimRewardButton; set => questClaimRewardButton = value; }
    public int QuestRewardInventoryCapacity { get => questRewardInventoryCapacity; set => questRewardInventoryCapacity = value; }
    public int QuestRewardMagicCapacity { get => questRewardMagicCapacity; set => questRewardMagicCapacity = value; }
    public ScrollRect QuestDetailScrollRect { get => questDetailScrollRect; set => questDetailScrollRect = value; }
    public bool SmoothQuestMouseWheel { get => smoothQuestMouseWheel; set => smoothQuestMouseWheel = value; }
    public float QuestMouseWheelStepNormalized { get => questMouseWheelStepNormalized; set => questMouseWheelStepNormalized = value; }
    public float QuestMouseWheelSmoothSpeed { get => questMouseWheelSmoothSpeed; set => questMouseWheelSmoothSpeed = value; }
    public float QuestPadRightStickScrollSpeed { get => questPadRightStickScrollSpeed; set => questPadRightStickScrollSpeed = value; }
    public Color QuestPadFocusBorderColor { get => questPadFocusBorderColor; set => questPadFocusBorderColor = value; }
    public Vector2 QuestPadFocusBorderThickness { get => questPadFocusBorderThickness; set => questPadFocusBorderThickness = value; }

    private void OnDestroy()
    {
        UnbindQuestManager();
    }

    private void Update()
    {
        if (!initialized)
            return;

        UpdateMouseWheelSmoothScroll(IsQuestTabVisualActive());
    }

    public void InitializeIfNeeded()
    {
        if (initialized)
            return;

        initialized = true;

        ResolveDependencies();

        if (autoWireQuestUI)
            AutoWireQuestUIReferences();

        RepairQuestDetailPanelRootReference();

        if (editorAutoAssignQuestPrefabs)
            TryEditorAutoAssignQuestPrefabs();

        if (questListContainer != null)
        {
            if (questItemPrefab == null && questListContainer.childCount > 0)
                questItemPrefab = questListContainer.GetChild(0).GetComponent<QuestItemUI>();
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

    public void UpdateMouseWheelSmoothScroll(bool isQuestTabActive)
    {
        if (!smoothQuestMouseWheel || !isQuestTabActive) return;
        if (questDetailScrollRect == null || !questDetailScrollRect.vertical) return;
        if (Mouse.current == null) return;

        if (!questTargetScrollInitialized)
        {
            questTargetScrollNormalized = questDetailScrollRect.verticalNormalizedPosition;
            questTargetScrollInitialized = true;
        }

        if (questDetailScrollRect.scrollSensitivity != 0f)
            questDetailScrollRect.scrollSensitivity = 0f;

        var viewport = questDetailScrollRect.viewport != null ? questDetailScrollRect.viewport : questDetailScrollRect.transform as RectTransform;
        if (viewport == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        if (!RectTransformUtility.RectangleContainsScreenPoint(viewport, mousePos, null))
            return;

        float wheel = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(wheel) > 0.01f)
            questTargetScrollNormalized = Mathf.Clamp01(questTargetScrollNormalized + (wheel * questMouseWheelStepNormalized / 120f));

        float current = questDetailScrollRect.verticalNormalizedPosition;
        float t = 1f - Mathf.Exp(-questMouseWheelSmoothSpeed * Time.unscaledDeltaTime);
        questDetailScrollRect.verticalNormalizedPosition = Mathf.Lerp(current, questTargetScrollNormalized, t);
    }

    public void SetQuestFilterAll() { RefreshUI(IsPadFocusVisible()); }
    public void SetQuestFilterActive() { SetQuestFilterAll(); }
    public void SetQuestFilterCompleted() { SetQuestFilterAll(); }

    public void SetQuests(List<QuestEntryData> quests)
    {
        TryBindQuestManager();
        if (!useQuestManager || questManager == null)
            return;

        var mapped = new List<QuestManager.QuestData>();
        if (quests != null)
        {
            for (int i = 0; i < quests.Count; i++)
            {
                var entry = quests[i];
                if (entry == null) continue;
                mapped.Add(new QuestManager.QuestData
                {
                    questId = string.IsNullOrWhiteSpace(entry.questId) ? NormalizeQuestId(entry.questId, entry.title, entry.location) : entry.questId.Trim(),
                    title = entry.title,
                    location = entry.location,
                    completed = entry.completed,
                    rewardClaimed = entry.rewardClaimed,
                    questTypeLabel = entry.questTypeLabel,
                    recommendedLabel = entry.recommendedLabel,
                    loreTitle = entry.loreTitle,
                    loreDescription = entry.loreDescription,
                    loreAuthor = entry.loreAuthor,
                    objectives = MapObjectives(entry.objectives),
                    rewards = MapRewards(entry.rewards)
                });
            }
        }

        questManager.ReplaceAllQuests(mapped);
    }

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
        EnsurePlayerInventory();
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

    public void ScrollDetailByPad(float axisY, float deltaTime, bool isQuestTabActive)
    {
        if (!isQuestTabActive) return;
        if (questManager == null || questManager.CurrentJournalPadSection != QuestManager.JournalPadSection.Detail) return;
        if (questDetailScrollRect == null || !questDetailScrollRect.vertical) return;
        if (Mathf.Abs(axisY) < 0.15f) return;

        float delta = axisY * questPadRightStickScrollSpeed * Mathf.Max(0.001f, deltaTime);
        questDetailScrollRect.verticalNormalizedPosition = Mathf.Clamp01(questDetailScrollRect.verticalNormalizedPosition + delta);
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
        EnsurePlayerInventory();
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
        if (questDetailTitleText != null) questDetailTitleText.text = quest != null ? (quest.title ?? string.Empty) : string.Empty;
        if (questDetailLocationText != null) questDetailLocationText.text = quest != null ? (quest.location ?? string.Empty) : string.Empty;
        if (questDetailLoreTitleText != null) questDetailLoreTitleText.text = quest != null ? (quest.loreTitle ?? string.Empty) : string.Empty;
        if (questDetailLoreDescriptionText != null) questDetailLoreDescriptionText.text = quest != null ? (quest.loreDescription ?? string.Empty) : string.Empty;
        if (questDetailLoreAuthorText != null) questDetailLoreAuthorText.text = quest != null ? (quest.loreAuthor ?? string.Empty) : string.Empty;

        UpdateQuestLoreVisibility(quest);
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

        EnsurePlayerInventory();
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

        EnsurePlayerInventory();
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
    private void UpdateQuestLoreVisibility(QuestEntryData quest)
    {
        bool hasLore = quest != null && (!string.IsNullOrWhiteSpace(quest.loreTitle) || !string.IsNullOrWhiteSpace(quest.loreDescription) || !string.IsNullOrWhiteSpace(quest.loreAuthor));
        if (collapseQuestLoreWhenEmpty && questDetailLoreRoot != null)
            questDetailLoreRoot.gameObject.SetActive(hasLore);
        else if (questDetailLoreRoot != null)
            questDetailLoreRoot.gameObject.SetActive(true);
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
            row.SetData(obj.title, obj.description, obj.completed);
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

    private void EnsurePlayerInventory()
    {
    }

    private void EnsurePlayerStats()
    {
        if (playerStats == null)
            playerStats = PlayerStats.instance;
    }

    private void ResolveDependencies()
    {
        EnsurePlayerInventory();
        EnsurePlayerStats();
    }

    private void AutoWireQuestUIReferences()
    {
        ResolveDependencies();
        Transform questRoot = null;
        var tabs = menuManager != null ? menuManager.GetTabs() : null;
        if (tabs != null)
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                var tab = tabs[i];
                if (tab == null || tab.background == null) continue;
                if (!string.Equals(tab.key, "Quest", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(tab.key, "Quests", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(tab.key, "Journal", StringComparison.OrdinalIgnoreCase))
                    continue;

                questRoot = tab.background.transform;
                break;
            }
        }

        if (questRoot == null)
            questRoot = FindDeepChildByName(transform.root, "QuestBackground");
        if (questRoot == null) return;

        if (questListContainer == null)
            questListContainer = FindDescendantByPath(questRoot, "LeftSide/QuestPanel") ?? FindDescendantByPath(questRoot, "LeftSide/Quest") ?? FindDeepChildByName(questRoot, "QuestPanel");

        Transform rightSide = FindDescendantByPath(questRoot, "RightSide");
        if (rightSide != null)
        {
            if (questDetailScrollRect == null) questDetailScrollRect = rightSide.GetComponent<ScrollRect>();
            if (questDetailPanelRoot == null) questDetailPanelRoot = rightSide as RectTransform;

            Transform body = FindDeepChildByName(rightSide, "Body");
            Transform lore = body != null ? FindDeepChildByName(body, "Lore") : FindDeepChildByName(rightSide, "Lore");
            if (questDetailLoreRoot == null) questDetailLoreRoot = lore as RectTransform;
            if (questDetailTypeText == null) questDetailTypeText = FindDeepTextByName(rightSide, "Type");
            if (questDetailRecommendedText == null) questDetailRecommendedText = FindDeepTextByName(rightSide, "Lvl");
            if (questDetailTitleText == null) questDetailTitleText = FindDeepTextByName(rightSide, "Title");
            if (questDetailLocationText == null) questDetailLocationText = FindDeepTextByName(rightSide, "Location");
            if (questDetailLoreTitleText == null) questDetailLoreTitleText = FindDeepTextByName(lore, "Title desc") ?? FindDeepTextByName(lore, "Title");
            if (questDetailLoreDescriptionText == null) questDetailLoreDescriptionText = FindDeepTextByName(lore, "Desc");
            if (questDetailLoreAuthorText == null) questDetailLoreAuthorText = FindDeepTextByName(lore, "Cit");
            if (questObjectivesContainer == null)
                questObjectivesContainer = FindDeepChildByName(rightSide, "BG_objective") ?? FindDeepChildByName(rightSide, "CurrentObjectives") ?? FindDeepChildByName(rightSide, "Objectives");
            if (questRewardsContainer == null)
                questRewardsContainer = FindDeepChildByName(rightSide, "BG_Rewards") ?? FindDeepChildByName(rightSide, "RewardContainer") ?? FindDeepChildByName(rightSide, "Rewards");
            if (questClaimRewardButton == null)
            {
                var claim = FindDeepChildByName(rightSide, "Claim");
                if (claim != null) questClaimRewardButton = claim.GetComponent<Button>();
            }
        }

        if (questObjectivesContainer != null && questObjectivePrefab == null && questObjectivesContainer.childCount > 0)
            questObjectivePrefab = questObjectivesContainer.GetChild(0).GetComponent<QuestObjectiveItemUI>();
        if (questRewardsContainer != null && questRewardPrefab == null && questRewardsContainer.childCount > 0)
            questRewardPrefab = questRewardsContainer.GetChild(0).GetComponent<QuestRewardItemUI>();
    }

    private void RepairQuestDetailPanelRootReference()
    {
        if (questListContainer == null)
            return;

        if (questDetailPanelRoot != null && !ContainsTransform(questDetailPanelRoot, questListContainer))
            return;

        Transform start = questDetailPanelRoot != null ? questDetailPanelRoot : questListContainer;
        RectTransform repairedRoot = FindSiblingQuestDetailPanel(start, questListContainer);
        if (repairedRoot != null)
            questDetailPanelRoot = repairedRoot;
    }

    private static RectTransform FindSiblingQuestDetailPanel(Transform start, Transform listContainer)
    {
        for (Transform current = start; current != null; current = current.parent)
        {
            Transform parent = current.parent;
            if (parent == null)
                continue;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform sibling = parent.GetChild(i);
                if (sibling == null || sibling == current)
                    continue;
                if (ContainsTransform(sibling, listContainer))
                    continue;
                if (!LooksLikeQuestDetailPanel(sibling))
                    continue;

                return sibling as RectTransform;
            }
        }

        return null;
    }

    private static bool LooksLikeQuestDetailPanel(Transform candidate)
    {
        if (candidate == null)
            return false;

        string n = candidate.name.ToLowerInvariant();
        if (n.Contains("side") || n.Contains("detail"))
            return true;

        return FindDeepChildByName(candidate, "QuestDetail") != null
               || FindDeepChildByName(candidate, "Lore") != null
               || FindDeepChildByName(candidate, "Objectives") != null
               || FindDeepChildByName(candidate, "Rewards") != null;
    }

    private static bool ContainsTransform(Transform root, Transform target)
    {
        if (root == null || target == null)
            return false;

        for (Transform current = target; current != null; current = current.parent)
        {
            if (current == root)
                return true;
        }

        return false;
    }

    private void TryEditorAutoAssignQuestPrefabs()
    {
#if UNITY_EDITOR
        if (questItemPrefab == null)
            questItemPrefab = LoadPrefabComponentAtPath<QuestItemUI>("Assets/Prefabs/UI/Quest.prefab");
        if (questObjectivePrefab == null)
        {
            var byName = AssetDatabase.FindAssets("t:prefab *Objective*");
            if (byName != null && byName.Length > 0)
                questObjectivePrefab = LoadPrefabComponentAtPath<QuestObjectiveItemUI>(AssetDatabase.GUIDToAssetPath(byName[0]));
        }
        if (questRewardPrefab == null)
        {
            var byName = AssetDatabase.FindAssets("t:prefab *Reward*");
            if (byName != null && byName.Length > 0)
                questRewardPrefab = LoadPrefabComponentAtPath<QuestRewardItemUI>(AssetDatabase.GUIDToAssetPath(byName[0]));
        }
#endif
    }

#if UNITY_EDITOR
    private static T LoadPrefabComponentAtPath<T>(string path) where T : Component
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return prefab != null ? prefab.GetComponent<T>() : null;
    }
#endif

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
            if (string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase)) return child;
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
        return t.GetComponent<TextMeshProUGUI>() ?? t.GetComponentInChildren<TextMeshProUGUI>(true);
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

    private static List<QuestManager.QuestObjectiveData> MapObjectives(List<QuestObjectiveEntryData> source)
    {
        var result = new List<QuestManager.QuestObjectiveData>();
        if (source == null) return result;
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] == null) continue;
            result.Add(new QuestManager.QuestObjectiveData { title = source[i].title, description = source[i].description, completed = source[i].completed });
        }
        return result;
    }

    private static List<QuestManager.QuestRewardData> MapRewards(List<QuestRewardEntryData> source)
    {
        var result = new List<QuestManager.QuestRewardData>();
        if (source == null) return result;
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] == null) continue;
            result.Add(new QuestManager.QuestRewardData
            {
                rewardType = source[i].rewardType,
                type = string.IsNullOrWhiteSpace(source[i].type) ? source[i].rewardType.ToString() : source[i].type,
                amount = source[i].amount,
                itemName = source[i].itemName,
                weaponAsset = source[i].weaponAsset,
                usableAsset = source[i].usableAsset,
                itemAsset = source[i].itemAsset,
                magicAsset = source[i].magicAsset,
                armorAsset = source[i].armorAsset
            });
        }
        return result;
    }
}

