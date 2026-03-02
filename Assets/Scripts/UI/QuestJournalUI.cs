
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
    [SerializeField] private float questObjectivesLiftWhenNoLore = -1f;
    [SerializeField] private Transform questObjectivesContainer;
    [SerializeField] private GameObject questObjectivePrefab;
    [SerializeField] private Transform questRewardsContainer;
    [SerializeField] private GameObject questRewardPrefab;
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
    private bool questFilterBaseColorsCached;
    private Color questActiveFilterBaseColor = Color.white;
    private Color questCompletedFilterBaseColor = Color.white;
    private GameObject questFocusedObject;
    private Outline questFocusOutline;
    private readonly List<GameObject> spawnedQuestRows = new();
    private readonly List<GameObject> spawnedObjectiveRows = new();
    private readonly List<GameObject> spawnedRewardRows = new();
    private float questTargetScrollNormalized = 1f;
    private bool questTargetScrollInitialized;
    private bool questObjectivesDefaultPosCached;
    private Vector2 questObjectivesDefaultAnchoredPos;
    private float suppressQuestRowClickUntil;

    public bool UseQuestManager { get => useQuestManager; set => useQuestManager = value; }
    public bool AutoWireQuestUI { get => autoWireQuestUI; set => autoWireQuestUI = value; }
    public Transform QuestListContainer { get => questListContainer; set => questListContainer = value; }
    public GameObject QuestItemPrefab { get => questItemPrefab; set => questItemPrefab = value; }
    public Button QuestActiveFilterButton { get => questActiveFilterButton; set => questActiveFilterButton = value; }
    public Button QuestCompletedFilterButton { get => questCompletedFilterButton; set => questCompletedFilterButton = value; }
    public TextMeshProUGUI QuestActiveCountText { get => questActiveCountText; set => questActiveCountText = value; }
    public TextMeshProUGUI QuestCompletedCountText { get => questCompletedCountText; set => questCompletedCountText = value; }
    public TextMeshProUGUI QuestActiveFilterLabelText { get => questActiveFilterLabelText; set => questActiveFilterLabelText = value; }
    public TextMeshProUGUI QuestCompletedFilterLabelText { get => questCompletedFilterLabelText; set => questCompletedFilterLabelText = value; }
    public Color QuestFilterSelectedColor { get => questFilterSelectedColor; set => questFilterSelectedColor = value; }
    public List<QuestEntryData> StartingQuests => startingQuests;
    public TextMeshProUGUI QuestDetailTypeText { get => questDetailTypeText; set => questDetailTypeText = value; }
    public TextMeshProUGUI QuestDetailRecommendedText { get => questDetailRecommendedText; set => questDetailRecommendedText = value; }
    public TextMeshProUGUI QuestDetailTitleText { get => questDetailTitleText; set => questDetailTitleText = value; }
    public TextMeshProUGUI QuestDetailLocationText { get => questDetailLocationText; set => questDetailLocationText = value; }
    public TextMeshProUGUI QuestDetailLoreTitleText { get => questDetailLoreTitleText; set => questDetailLoreTitleText = value; }
    public TextMeshProUGUI QuestDetailLoreDescriptionText { get => questDetailLoreDescriptionText; set => questDetailLoreDescriptionText = value; }
    public TextMeshProUGUI QuestDetailLoreAuthorText { get => questDetailLoreAuthorText; set => questDetailLoreAuthorText = value; }
    public RectTransform QuestDetailLoreRoot { get => questDetailLoreRoot; set => questDetailLoreRoot = value; }
    public RectTransform QuestObjectivesSectionRoot { get => questObjectivesSectionRoot; set => questObjectivesSectionRoot = value; }
    public GameObject QuestDetailPanelRoot { get => questDetailPanelRoot; set => questDetailPanelRoot = value; }
    public bool ShowQuestDetailOnlyOnSelection { get => showQuestDetailOnlyOnSelection; set => showQuestDetailOnlyOnSelection = value; }
    public bool CollapseQuestLoreWhenEmpty { get => collapseQuestLoreWhenEmpty; set => collapseQuestLoreWhenEmpty = value; }
    public float QuestObjectivesLiftWhenNoLore { get => questObjectivesLiftWhenNoLore; set => questObjectivesLiftWhenNoLore = value; }
    public Transform QuestObjectivesContainer { get => questObjectivesContainer; set => questObjectivesContainer = value; }
    public GameObject QuestObjectivePrefab { get => questObjectivePrefab; set => questObjectivePrefab = value; }
    public Transform QuestRewardsContainer { get => questRewardsContainer; set => questRewardsContainer = value; }
    public GameObject QuestRewardPrefab { get => questRewardPrefab; set => questRewardPrefab = value; }
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
        if (questActiveFilterButton != null)
            questActiveFilterButton.onClick.RemoveListener(SetQuestFilterActive);
        if (questCompletedFilterButton != null)
            questCompletedFilterButton.onClick.RemoveListener(SetQuestFilterCompleted);
        if (questClaimRewardButton != null)
            questClaimRewardButton.onClick.RemoveListener(OnQuestClaimRewardButtonClicked);
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

        bool needQuestListWiring = questListContainer == null || questItemPrefab == null;
        if (autoWireQuestUI || needQuestListWiring)
            AutoWireQuestUIReferences();

        TryEditorAutoAssignQuestPrefabs();

        if (questListContainer != null)
        {
            if (questItemPrefab == null && questListContainer.childCount > 0)
                questItemPrefab = questListContainer.GetChild(0).gameObject;
            if (questItemPrefab != null && questItemPrefab.transform.parent == questListContainer)
                questItemPrefab.SetActive(false);
        }

        if (questObjectivesContainer != null && questObjectivePrefab != null && questObjectivePrefab.transform.parent == questObjectivesContainer)
            questObjectivePrefab.SetActive(false);
        if (questRewardsContainer != null && questRewardPrefab != null && questRewardPrefab.transform.parent == questRewardsContainer)
            questRewardPrefab.SetActive(false);

        WireQuestFilterButtons();
        WireQuestClaimButton();
        CacheQuestFilterBaseColors();
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
        UpdateQuestFilterVisuals(showPadFocus);
        UpdateQuestCounters();
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

    public void SetQuestFilterAll() { if (questManager != null) questManager.SetJournalQuestFilterAll(); RefreshUI(IsPadFocusVisible()); }
    public void SetQuestFilterActive() { if (questManager != null) questManager.SetJournalQuestFilterActive(); RefreshUI(IsPadFocusVisible()); }
    public void SetQuestFilterCompleted() { if (questManager != null) questManager.SetJournalQuestFilterCompleted(); RefreshUI(IsPadFocusVisible()); }

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
        TryBindQuestManager();
        if (questManager == null) return;
        questManager.FocusJournalPadFilters();
        ApplyPadFocusVisual(showPadFocus);
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

        EnsurePlayerInventory();
        EnsurePlayerStats();
        bool claimed = questManager.ConfirmJournalSelection(playerInventory, playerStats, GetQuestRewardNormalCapacityValue(), GetQuestRewardMagicCapacityValue());
        if (claimed)
            RefreshQuestSourcesFromPlayer();

        RefreshUI(showPadFocus);
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
            ApplyQuestFilterPadFocusHighlight(showPadFocus);
            return;
        }

        GameObject selectedTarget = null;
        GameObject visualTarget = null;
        switch (questManager != null ? questManager.CurrentJournalPadSection : QuestManager.JournalPadSection.Filters)
        {
            case QuestManager.JournalPadSection.Filters:
                selectedTarget = GetQuestFilterButtonObject(questManager != null ? questManager.JournalPadFilterIndex : 0);
                visualTarget = GetQuestFilterVisualObject(questManager != null ? questManager.JournalPadFilterIndex : 0);
                break;
            case QuestManager.JournalPadSection.List:
                var rowButton = GetVisibleQuestRowAt(questManager != null ? questManager.JournalPadListIndex : 0);
                selectedTarget = rowButton != null ? rowButton.gameObject : null;
                visualTarget = selectedTarget;
                break;
            case QuestManager.JournalPadSection.Detail:
                selectedTarget = questClaimRewardButton != null ? questClaimRewardButton.gameObject : null;
                visualTarget = selectedTarget;
                break;
        }

        bool allowVisualAsSelection = questManager == null || questManager.CurrentJournalPadSection != QuestManager.JournalPadSection.Filters;
        if (selectedTarget == null && allowVisualAsSelection) selectedTarget = visualTarget;
        if (visualTarget == null) visualTarget = selectedTarget;

        if (EventSystem.current != null)
        {
            if (selectedTarget != null)
                EventSystem.current.SetSelectedGameObject(selectedTarget);
            else
                EventSystem.current.SetSelectedGameObject(null);
        }

        SetQuestPadFocusVisualTarget(showPadFocus ? visualTarget : null);
        ApplyQuestFilterPadFocusHighlight(showPadFocus);
    }

    public void ClearPadFocusVisual()
    {
        if (questFocusOutline != null)
            questFocusOutline.enabled = false;
        questFocusedObject = null;
        questFocusOutline = null;
    }

    private void TryBindQuestManager()
    {
        if (!useQuestManager) return;
        if (questManager == null)
            questManager = QuestManager.Instance != null ? QuestManager.Instance : FindObjectOfType<QuestManager>();
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

    private void UpdateQuestCounters()
    {
        var quests = questManager != null ? questManager.GetQuestEntriesSnapshot() : new List<QuestEntryData>();
        int activeCount = 0;
        int completedCount = 0;
        for (int i = 0; i < quests.Count; i++)
        {
            var quest = quests[i];
            if (quest == null) continue;
            if (quest.completed) completedCount++;
            else activeCount++;
        }

        if (questActiveCountText != null) questActiveCountText.text = activeCount.ToString();
        if (questCompletedCountText != null) questCompletedCountText.text = completedCount.ToString();
    }

    private void OnQuestRowClicked(string questId)
    {
        if (Time.unscaledTime < suppressQuestRowClickUntil)
            return;
        if (IsQuestTabVisualActive() && IsPadFocusVisible() && questManager != null && questManager.CurrentJournalPadSection == QuestManager.JournalPadSection.Filters)
            return;
        if (string.IsNullOrWhiteSpace(questId) || questManager == null)
            return;

        questManager.SelectJournalQuest(questId);
        RefreshUI(IsPadFocusVisible());
    }

    private void RefreshSelectedQuestDetails()
    {
        UpdateQuestDetailPanel(questManager != null ? questManager.GetSelectedVisibleJournalQuest() : null);
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
        UpdateQuestClaimButtonState(quest);
        RebuildQuestObjectiveRows(quest);
        RebuildQuestRewardRows(quest);
    }

    private void UpdateQuestClaimButtonState(QuestEntryData quest)
    {
        if (questClaimRewardButton == null) return;
        if (questManager == null)
        {
            questClaimRewardButton.interactable = false;
            return;
        }

        EnsurePlayerInventory();
        EnsurePlayerStats();
        bool canClaim = quest != null
            && questManager.IsQuestReadyToClaim(quest)
            && quest.rewards != null
            && quest.rewards.Count > 0
            && questManager.CanClaimSelectedQuestRewards(playerInventory, playerStats, GetQuestRewardNormalCapacityValue(), GetQuestRewardMagicCapacityValue());
        questClaimRewardButton.interactable = canClaim;
    }

    private void OnQuestClaimRewardButtonClicked()
    {
        if (questManager == null)
            return;

        EnsurePlayerInventory();
        EnsurePlayerStats();
        if (!questManager.TryClaimSelectedQuestRewards(playerInventory, playerStats, GetQuestRewardNormalCapacityValue(), GetQuestRewardMagicCapacityValue()))
        {
            RefreshUI(IsPadFocusVisible());
            return;
        }

        RefreshQuestSourcesFromPlayer();
        RefreshUI(IsPadFocusVisible());
    }
    private void UpdateQuestLoreVisibilityAndLayout(QuestEntryData quest)
    {
        bool hasLore = quest != null && (!string.IsNullOrWhiteSpace(quest.loreTitle) || !string.IsNullOrWhiteSpace(quest.loreDescription) || !string.IsNullOrWhiteSpace(quest.loreAuthor));
        if (collapseQuestLoreWhenEmpty && questDetailLoreRoot != null)
            questDetailLoreRoot.gameObject.SetActive(hasLore);
        else if (questDetailLoreRoot != null)
            questDetailLoreRoot.gameObject.SetActive(true);

        EnsureQuestObjectivesLayoutCache();
        if (!collapseQuestLoreWhenEmpty || questObjectivesSectionRoot == null || !questObjectivesDefaultPosCached)
            return;

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
            rowUi.SetData(ResolveRewardIcon(reward), ResolveRewardTypeText(reward), reward.amount, ResolveRewardItemName(reward));
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
            row.SetActive(true);
            var rowUi = row.GetComponent<QuestItemUI>();
            if (rowUi == null) rowUi = row.AddComponent<QuestItemUI>();
            rowUi.SetData(quest.title, quest.location, quest.completed);
            rowUi.SetSelected(questManager.IsJournalQuestSelected(quest.questId));

            string capturedQuestId = quest.questId;
            var rowButton = EnsureFilterButton(row);
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
            Destroy(child);
        }

        spawnedQuestRows.Clear();
    }

    private void UpdateQuestFilterVisuals(bool showPadFocus)
    {
        if (questActiveFilterLabelText != null)
            questActiveFilterLabelText.color = questManager != null && questManager.CurrentJournalFilter == QuestManager.JournalQuestFilter.Active ? questFilterSelectedColor : questActiveFilterBaseColor;

        if (questCompletedFilterLabelText != null)
            questCompletedFilterLabelText.color = questManager != null && questManager.CurrentJournalFilter == QuestManager.JournalQuestFilter.Completed ? questFilterSelectedColor : questCompletedFilterBaseColor;

        ApplyQuestFilterPadFocusHighlight(showPadFocus);
    }

    private void ApplyQuestFilterPadFocusHighlight(bool showPadFocus)
    {
        if (questActiveFilterLabelText == null && questCompletedFilterLabelText == null) return;
        if (!showPadFocus || !IsQuestTabVisualActive() || questManager == null || questManager.CurrentJournalPadSection != QuestManager.JournalPadSection.Filters)
            return;

        if (questManager.JournalPadFilterIndex <= 0)
        {
            if (questActiveFilterLabelText != null) questActiveFilterLabelText.color = questPadFocusBorderColor;
        }
        else
        {
            if (questCompletedFilterLabelText != null) questCompletedFilterLabelText.color = questPadFocusBorderColor;
        }
    }

    private void SetQuestPadFocusVisualTarget(GameObject target)
    {
        target = ResolveQuestFocusGraphicTarget(target);
        if (target == questFocusedObject && questFocusOutline != null)
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
        questFocusedObject = target;
        questFocusOutline = outline;
        if (created && outline.useGraphicAlpha == false)
            outline.useGraphicAlpha = true;
    }
    private GameObject GetQuestFilterButtonObject(int index)
    {
        InitializeIfNeeded();
        if (index <= 0)
        {
            if (questActiveFilterButton == null)
            {
                questActiveFilterButton = EnsureFilterButtonFromLabelOrCount(questActiveFilterLabelText, questActiveCountText);
                if (questActiveFilterButton != null)
                {
                    questActiveFilterButton.onClick.RemoveListener(SetQuestFilterActive);
                    questActiveFilterButton.onClick.AddListener(SetQuestFilterActive);
                }
            }
            return questActiveFilterButton != null ? questActiveFilterButton.gameObject : null;
        }

        if (questCompletedFilterButton == null)
        {
            questCompletedFilterButton = EnsureFilterButtonFromLabelOrCount(questCompletedFilterLabelText, questCompletedCountText);
            if (questCompletedFilterButton != null)
            {
                questCompletedFilterButton.onClick.RemoveListener(SetQuestFilterCompleted);
                questCompletedFilterButton.onClick.AddListener(SetQuestFilterCompleted);
            }
        }

        return questCompletedFilterButton != null ? questCompletedFilterButton.gameObject : null;
    }

    private GameObject GetQuestFilterVisualObject(int index)
    {
        if (index <= 0)
        {
            if (questActiveFilterLabelText != null) return questActiveFilterLabelText.gameObject;
            if (questActiveFilterButton != null) return questActiveFilterButton.gameObject;
            return questActiveCountText != null ? questActiveCountText.gameObject : null;
        }

        if (questCompletedFilterLabelText != null) return questCompletedFilterLabelText.gameObject;
        if (questCompletedFilterButton != null) return questCompletedFilterButton.gameObject;
        return questCompletedCountText != null ? questCompletedCountText.gameObject : null;
    }

    private Button GetVisibleQuestRowAt(int index)
    {
        if (index < 0) return null;
        int cursor = 0;
        for (int i = 0; i < spawnedQuestRows.Count; i++)
        {
            var row = spawnedQuestRows[i];
            if (row == null || !row.activeInHierarchy) continue;
            if (cursor == index) return row.GetComponent<Button>();
            cursor++;
        }
        return null;
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

    private void WireQuestClaimButton()
    {
        if (questClaimRewardButton == null) return;
        questClaimRewardButton.onClick.RemoveListener(OnQuestClaimRewardButtonClicked);
        questClaimRewardButton.onClick.AddListener(OnQuestClaimRewardButtonClicked);
    }

    private void CacheQuestFilterBaseColors()
    {
        if (questFilterBaseColorsCached) return;
        if (questActiveFilterLabelText != null) questActiveFilterBaseColor = questActiveFilterLabelText.color;
        if (questCompletedFilterLabelText != null) questCompletedFilterBaseColor = questCompletedFilterLabelText.color;
        questFilterBaseColorsCached = true;
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
        if (playerInventory == null)
            playerInventory = FindObjectOfType<PlayerInventory>();
    }

    private void EnsurePlayerStats()
    {
        if (playerStats == null)
            playerStats = PlayerStats.instance != null ? PlayerStats.instance : FindObjectOfType<PlayerStats>();
    }

    private void ResolveDependencies()
    {
        if (menuManager == null)
            menuManager = FindObjectOfType<MenuManager>(true);
        if (inventoryUIManager == null)
            inventoryUIManager = FindObjectOfType<InventoryUIManager>(true);
        if (magicInventoryManager == null)
            magicInventoryManager = FindObjectOfType<MagicInventoryManager>(true);
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

        Transform filterRoot = FindDescendantByPath(questRoot, "LeftSide/Filter") ?? FindDeepChildByName(questRoot, "Filter");
        if (filterRoot == null) return;

        var activeRow = FindFilterRowByLabel(filterRoot, "ACTIVE");
        var completedRow = FindFilterRowByLabel(filterRoot, "COMPLETED");
        if (questActiveFilterButton == null && activeRow != null) questActiveFilterButton = EnsureFilterButton(activeRow.gameObject);
        if (questCompletedFilterButton == null && completedRow != null) questCompletedFilterButton = EnsureFilterButton(completedRow.gameObject);
        if (questActiveCountText == null && activeRow != null) questActiveCountText = FindCounterTextOnFilterRow(activeRow, "ACTIVE");
        if (questCompletedCountText == null && completedRow != null) questCompletedCountText = FindCounterTextOnFilterRow(completedRow, "COMPLETED");
        if (questActiveFilterLabelText == null && activeRow != null) questActiveFilterLabelText = FindFilterLabelText(activeRow, "ACTIVE");
        if (questCompletedFilterLabelText == null && completedRow != null) questCompletedFilterLabelText = FindFilterLabelText(completedRow, "COMPLETED");
        Transform rightSide = FindDescendantByPath(questRoot, "RightSide");
        if (rightSide != null)
        {
            if (questDetailScrollRect == null) questDetailScrollRect = rightSide.GetComponent<ScrollRect>();
            if (questDetailPanelRoot == null) questDetailPanelRoot = rightSide.gameObject;

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
            if (questObjectivesSectionRoot == null)
            {
                var objectivesRoot = FindDeepChildByName(rightSide, "Objectives");
                if (objectivesRoot != null) questObjectivesSectionRoot = objectivesRoot as RectTransform;
                else if (questObjectivesContainer != null && questObjectivesContainer.parent != null) questObjectivesSectionRoot = questObjectivesContainer.parent as RectTransform;
            }
            if (questRewardsContainer == null)
                questRewardsContainer = FindDeepChildByName(rightSide, "BG_Rewards") ?? FindDeepChildByName(rightSide, "RewardContainer") ?? FindDeepChildByName(rightSide, "Rewards");
            if (questClaimRewardButton == null)
            {
                var claim = FindDeepChildByName(rightSide, "Claim");
                if (claim != null) questClaimRewardButton = claim.GetComponent<Button>();
            }
        }

        if (questObjectivesContainer != null && questObjectivePrefab == null && questObjectivesContainer.childCount > 0)
            questObjectivePrefab = questObjectivesContainer.GetChild(0).gameObject;
        if (questRewardsContainer != null && questRewardPrefab == null && questRewardsContainer.childCount > 0)
            questRewardPrefab = questRewardsContainer.GetChild(0).gameObject;
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
                questObjectivePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(byName[0]));
        }
        if (questRewardPrefab == null)
        {
            var byName = AssetDatabase.FindAssets("t:prefab *Reward*");
            if (byName != null && byName.Length > 0)
                questRewardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(byName[0]));
        }
#endif
    }

    private static void ClearSpawnedRows(List<GameObject> spawned, Transform container, GameObject prefabRef)
    {
        if (spawned != null)
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null) Destroy(spawned[i]);
            }
            spawned.Clear();
        }

        if (container == null) return;
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            var child = container.GetChild(i).gameObject;
            if (prefabRef != null && child == prefabRef) continue;
            Destroy(child);
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

    private static Button EnsureFilterButtonFromLabelOrCount(TextMeshProUGUI label, TextMeshProUGUI count)
    {
        Transform row = null;
        if (label != null) row = label.transform.parent != null ? label.transform.parent : label.transform;
        else if (count != null) row = count.transform.parent != null ? count.transform.parent : count.transform;
        return row != null ? EnsureFilterButton(row.gameObject) : null;
    }
    private static Transform FindFilterRowByLabel(Transform filterRoot, string label)
    {
        if (filterRoot == null || string.IsNullOrEmpty(label)) return null;
        var texts = filterRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null) continue;
            if (!string.Equals(texts[i].text.Trim(), label, StringComparison.OrdinalIgnoreCase)) continue;
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
            if (!string.Equals(texts[i].text.Trim(), rowLabel, StringComparison.OrdinalIgnoreCase)) return texts[i];
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
            if (string.Equals(texts[i].text.Trim(), label, StringComparison.OrdinalIgnoreCase)) return texts[i];
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

