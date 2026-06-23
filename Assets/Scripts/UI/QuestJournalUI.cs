
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class QuestJournalUI : MonoBehaviour
{
    private const float ObjectiveRowHeight = 34f;
    private const float ObjectiveRowSpacing = 2f;

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
    [SerializeField] private ScrollRect questListScrollRect;
    [SerializeField] private RectTransform questListViewport;
    [SerializeField] private VerticalLayoutGroup questListLayout;
    [SerializeField] private ContentSizeFitter questListContentSizeFitter;
    [SerializeField, Min(1f)] private float questListMouseWheelPixels = 48f;
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

    [Header("Quest Phase UI")]
    [FormerlySerializedAs("questPhaseText")]
    [SerializeField] private TextMeshProUGUI questDetailPhaseText;
    [SerializeField] private Button questPreviousPhaseButton;
    [SerializeField] private Button questNextPhaseButton;

    [SerializeField] private Color questPadFocusBorderColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Vector2 questPadFocusBorderThickness = new Vector2(3f, 3f);

    private QuestManager questManager;
    private bool initialized;
    private bool questManagerSubscribed;
    private Transform questFocusedTransform;
    private Outline questFocusOutline;
    private QuestItemUI questFocusedRow;
    private string viewedPhaseQuestId;
    private int viewedPhase = 1;
    private int lastCurrentPhase = 1;
    private readonly List<QuestItemUI> spawnedQuestRows = new();
    private readonly List<QuestObjectiveItemUI> spawnedObjectiveRows = new();
    private readonly List<QuestRewardItemUI> spawnedRewardRows = new();
    private float suppressQuestRowClickUntil;

    public bool UseQuestManager { get => useQuestManager; set => useQuestManager = value; }
    public Transform QuestListContainer { get => questListContainer; set => questListContainer = value; }
    public QuestItemUI QuestItemPrefab { get => questItemPrefab; set => questItemPrefab = value; }
    public ScrollRect QuestListScrollRect { get => questListScrollRect; set => questListScrollRect = value; }
    public RectTransform QuestListViewport { get => questListViewport; set => questListViewport = value; }
    public VerticalLayoutGroup QuestListLayout { get => questListLayout; set => questListLayout = value; }
    public ContentSizeFitter QuestListContentSizeFitter { get => questListContentSizeFitter; set => questListContentSizeFitter = value; }
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
    public TextMeshProUGUI QuestPhaseText { get => questDetailPhaseText; set => questDetailPhaseText = value; }
    public Button QuestPreviousPhaseButton { get => questPreviousPhaseButton; set => questPreviousPhaseButton = value; }
    public Button QuestNextPhaseButton { get => questNextPhaseButton; set => questNextPhaseButton = value; }
    public Color QuestPadFocusBorderColor { get => questPadFocusBorderColor; set => questPadFocusBorderColor = value; }
    public Vector2 QuestPadFocusBorderThickness { get => questPadFocusBorderThickness; set => questPadFocusBorderThickness = value; }

    private void OnDestroy()
    {
        UnbindQuestPhaseButtons();
        UnbindQuestManager();
    }

    private void Update()
    {
        HandleQuestListMouseWheel();
    }

    private void HandleQuestListMouseWheel()
    {
        if (questListScrollRect == null || questListViewport == null || Mouse.current == null
            || !(questListContainer is RectTransform content)
            || !questListViewport.gameObject.activeInHierarchy)
            return;

        Vector2 pointerPosition = Mouse.current.position.ReadValue();
        Canvas canvas = questListViewport.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        if (!RectTransformUtility.RectangleContainsScreenPoint(questListViewport, pointerPosition, eventCamera))
            return;

        float wheel = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(wheel) < 0.01f)
            return;

        float maxOffset = Mathf.Max(0f, content.rect.height - questListViewport.rect.height);
        Vector2 position = content.anchoredPosition;
        position.y = Mathf.Clamp(position.y + (wheel < 0f ? questListMouseWheelPixels : -questListMouseWheelPixels), 0f, maxOffset);
        content.anchoredPosition = position;
        questListScrollRect.StopMovement();
    }

    public void InitializeIfNeeded()
    {
        if (initialized)
            return;

        initialized = true;

        ResolveDependencies();
        BindQuestPhaseButtons();
        EnsureQuestListScrolling();

        if (questListContainer != null)
        {
            if (questItemPrefab != null && questItemPrefab.transform.parent == questListContainer)
                questItemPrefab.gameObject.SetActive(false);
        }

        if (questObjectivesContainer != null && questObjectivePrefab != null && questObjectivePrefab.transform.parent == questObjectivesContainer)
            questObjectivePrefab.gameObject.SetActive(false);
        EnsureObjectiveLayoutCapacity();
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

        if (ChangeViewedPhase(direction))
        {
            RefreshSelectedQuestDetails();
            ApplyPadFocusVisual(showPadFocus);
            return;
        }

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
                if (rowButton != null)
                    ScrollQuestRowIntoView(rowButton.transform as RectTransform);
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
        if (questFocusedRow != null)
            questFocusedRow.SetFocused(false);
        if (questFocusOutline != null)
            questFocusOutline.enabled = false;
        questFocusedRow = null;
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
        int displayedPhase = ResolveDisplayedPhase(quest);
        if (questDetailPanelRoot != null && showQuestDetailOnlyOnSelection)
            questDetailPanelRoot.gameObject.SetActive(quest != null);

        if (questDetailTypeText != null) questDetailTypeText.text = quest != null ? (quest.questTypeLabel ?? string.Empty) : string.Empty;
        if (questDetailRecommendedText != null)
            questDetailRecommendedText.text = quest != null ? quest.recommendedLabel ?? string.Empty : string.Empty;
        UpdateQuestPhaseUI(quest, displayedPhase);
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
        RebuildQuestObjectiveRows(quest, displayedPhase);
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
    private void RebuildQuestObjectiveRows(QuestEntryData quest, int displayedPhase)
    {
        ClearSpawnedRows(spawnedObjectiveRows, questObjectivesContainer, questObjectivePrefab);
        if (questObjectivesContainer == null || questObjectivePrefab == null || quest == null || quest.objectives == null) return;

        EnsureObjectiveLayoutCapacity();
        for (int i = 0; i < quest.objectives.Count; i++)
        {
            var obj = quest.objectives[i];
            if (obj == null || obj.phase != displayedPhase) continue;
            var row = Instantiate(questObjectivePrefab, questObjectivesContainer);
            row.gameObject.SetActive(true);
            row.SetData(obj.title, FormatQuestObjectiveDescription(obj), obj.completed);
            spawnedObjectiveRows.Add(row);
        }
    }

    private int ResolveDisplayedPhase(QuestEntryData quest)
    {
        if (quest == null)
        {
            viewedPhaseQuestId = null;
            viewedPhase = 1;
            lastCurrentPhase = 1;
            return 1;
        }

        int currentPhase = QuestManager.GetCurrentPhaseNumber(quest);
        bool questChanged = !string.Equals(viewedPhaseQuestId, quest.questId, StringComparison.OrdinalIgnoreCase);
        if (questChanged)
        {
            viewedPhaseQuestId = quest.questId;
            viewedPhase = currentPhase;
        }
        else if (currentPhase != lastCurrentPhase && viewedPhase == lastCurrentPhase)
        {
            viewedPhase = currentPhase;
        }

        lastCurrentPhase = currentPhase;
        viewedPhase = Mathf.Clamp(viewedPhase, 1, currentPhase);
        return viewedPhase;
    }

    private bool ChangeViewedPhase(int direction)
    {
        var quest = questManager != null ? questManager.GetSelectedVisibleJournalQuest() : null;
        if (quest == null)
            return false;

        int currentPhase = QuestManager.GetCurrentPhaseNumber(quest);
        ResolveDisplayedPhase(quest);
        int nextPhase = Mathf.Clamp(viewedPhase + (direction >= 0 ? 1 : -1), 1, currentPhase);
        if (nextPhase == viewedPhase)
            return false;

        viewedPhase = nextPhase;
        return true;
    }

    private void BindQuestPhaseButtons()
    {
        if (questPreviousPhaseButton != null)
        {
            questPreviousPhaseButton.onClick.RemoveListener(ShowPreviousQuestPhase);
            questPreviousPhaseButton.onClick.AddListener(ShowPreviousQuestPhase);
        }

        if (questNextPhaseButton != null)
        {
            questNextPhaseButton.onClick.RemoveListener(ShowNextQuestPhase);
            questNextPhaseButton.onClick.AddListener(ShowNextQuestPhase);
        }
    }

    private void UnbindQuestPhaseButtons()
    {
        if (questPreviousPhaseButton != null)
            questPreviousPhaseButton.onClick.RemoveListener(ShowPreviousQuestPhase);
        if (questNextPhaseButton != null)
            questNextPhaseButton.onClick.RemoveListener(ShowNextQuestPhase);
    }

    private void ShowPreviousQuestPhase()
    {
        if (ChangeViewedPhase(-1))
            RefreshSelectedQuestDetails();
    }

    private void ShowNextQuestPhase()
    {
        if (ChangeViewedPhase(1))
            RefreshSelectedQuestDetails();
    }

    private void UpdateQuestPhaseUI(QuestEntryData quest, int displayedPhase)
    {
        bool hasQuest = quest != null && quest.objectives != null && quest.objectives.Count > 0;
        int currentPhase = hasQuest ? QuestManager.GetCurrentPhaseNumber(quest) : 1;
        int phaseCount = hasQuest ? QuestManager.GetPhaseCount(quest) : 1;

        if (questDetailPhaseText != null)
        {
            questDetailPhaseText.text = hasQuest ? FormatPhaseLabel(displayedPhase, phaseCount) : string.Empty;
            questDetailPhaseText.gameObject.SetActive(hasQuest);
        }

        if (questPreviousPhaseButton != null)
        {
            questPreviousPhaseButton.gameObject.SetActive(hasQuest && phaseCount > 1);
            questPreviousPhaseButton.interactable = hasQuest && displayedPhase > 1;
        }

        if (questNextPhaseButton != null)
        {
            questNextPhaseButton.gameObject.SetActive(hasQuest && phaseCount > 1);
            questNextPhaseButton.interactable = hasQuest && displayedPhase < currentPhase;
        }
    }

    private void EnsureObjectiveLayoutCapacity()
    {
        if (questObjectivesContainer == null || questObjectivePrefab == null)
            return;

        if (questObjectivePrefab.transform is RectTransform prefabRect)
            prefabRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, ObjectiveRowHeight);

        var layout = questObjectivesContainer.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.spacing = ObjectiveRowSpacing;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
        }

        if (!(questObjectivesContainer is RectTransform containerRect))
            return;

        float minimumHeight = QuestManager.MaxObjectivesPerPhase * ObjectiveRowHeight
                              + (QuestManager.MaxObjectivesPerPhase - 1) * ObjectiveRowSpacing;
        if (containerRect.rect.height < minimumHeight)
            containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, minimumHeight);
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
                rowButton.targetGraphic = row.SelectionGraphic;
                rowButton.transition = Selectable.Transition.None;
                rowButton.onClick.RemoveAllListeners();
                rowButton.onClick.AddListener(() => OnQuestRowClicked(capturedQuestId));
            }

            spawnedQuestRows.Add(row);
        }

        RefreshQuestListScrollLayout();

        if (showPadFocus)
            ApplyPadFocusVisual(true);
    }

    private void RefreshQuestListScrollLayout()
    {
        if (questListScrollRect == null || questListViewport == null
            || !(questListContainer is RectTransform content))
            return;

        if (questListContentSizeFitter != null)
            questListContentSizeFitter.enabled = false;

        float requiredHeight = 0f;
        int activeRows = 0;
        for (int i = 0; i < spawnedQuestRows.Count; i++)
        {
            var row = spawnedQuestRows[i];
            if (row == null || !row.gameObject.activeSelf || !(row.transform is RectTransform rowRect))
                continue;

            requiredHeight += Mathf.Max(1f, rowRect.rect.height);
            activeRows++;
        }

        if (questListLayout != null)
        {
            requiredHeight += questListLayout.padding.top + questListLayout.padding.bottom;
            if (activeRows > 1)
                requiredHeight += questListLayout.spacing * (activeRows - 1);
        }

        requiredHeight = Mathf.Max(questListViewport.rect.height, requiredHeight);
        content.anchoredPosition = Vector2.zero;
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, requiredHeight);

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        Canvas.ForceUpdateCanvases();
        questListScrollRect.verticalNormalizedPosition = 1f;
        questListScrollRect.StopMovement();
    }

    private void EnsureQuestListScrolling()
    {
        if (!(questListContainer is RectTransform content))
            return;

        bool hasScrollConfiguration = questListScrollRect != null
                                      || questListViewport != null
                                      || questListLayout != null
                                      || questListContentSizeFitter != null;
        if (!hasScrollConfiguration)
            return;

        if (questListScrollRect == null || questListViewport == null)
        {
            Debug.LogWarning("[QuestJournalUI] Collega Quest List Scroll Rect e Quest List Viewport nell'Inspector.", this);
            return;
        }

        if (questListViewport.GetComponent<RectMask2D>() == null)
            Debug.LogWarning("[QuestJournalUI] Il Quest List Viewport deve avere un componente Rect Mask 2D.", questListViewport);

        if (questListLayout == null)
            Debug.LogWarning("[QuestJournalUI] Collega il Vertical Layout Group del Content.", this);
        if (questListContentSizeFitter == null)
            Debug.LogWarning("[QuestJournalUI] Collega il Content Size Fitter del Content.", this);

        questListScrollRect.content = content;
        questListScrollRect.viewport = questListViewport;
        questListScrollRect.horizontal = false;
        questListScrollRect.vertical = true;
        questListScrollRect.movementType = ScrollRect.MovementType.Clamped;
        questListScrollRect.scrollSensitivity = 24f;
    }

    private void ScrollQuestRowIntoView(RectTransform row)
    {
        if (row == null || questListScrollRect == null || questListViewport == null
            || !(questListContainer is RectTransform content))
            return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        Bounds rowBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(questListViewport, row);
        Rect viewportRect = questListViewport.rect;
        Vector2 position = content.anchoredPosition;

        if (rowBounds.min.y < viewportRect.yMin)
            position.y += viewportRect.yMin - rowBounds.min.y;
        else if (rowBounds.max.y > viewportRect.yMax)
            position.y -= rowBounds.max.y - viewportRect.yMax;

        content.anchoredPosition = position;
        questListScrollRect.StopMovement();
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
            {
                spawnedQuestRows[i].gameObject.SetActive(false);
                Destroy(spawnedQuestRows[i].gameObject);
            }
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
            child.SetActive(false);
            Destroy(child);
        }

        spawnedQuestRows.Clear();
    }

    private void SetQuestPadFocusVisualTarget(GameObject target)
    {
        var focusedRow = target != null ? target.GetComponentInParent<QuestItemUI>() : null;
        if (focusedRow != null)
        {
            if (questFocusedRow != focusedRow)
                ClearPadFocusVisual();

            questFocusedRow = focusedRow;
            questFocusedTransform = focusedRow.transform;
            focusedRow.SetFocused(true);
            return;
        }

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
        if (outline == null)
        {
            Debug.LogWarning("[QuestJournalUI] Il prefab Quest deve avere un componente Outline sul root.", target);
            return;
        }

        outline.effectColor = questPadFocusBorderColor;
        outline.effectDistance = questPadFocusBorderThickness;
        outline.enabled = true;
        questFocusedTransform = target.transform;
        questFocusOutline = outline;
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
                if (spawned[i] != null)
                {
                    spawned[i].gameObject.SetActive(false);
                    Destroy(spawned[i].gameObject);
                }
            }
            spawned.Clear();
        }

        if (container == null) return;
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            var child = container.GetChild(i).gameObject;
            if (prefabRef != null && child == prefabRef.gameObject) continue;
            child.SetActive(false);
            Destroy(child);
        }
    }

    private static Button EnsureButton(GameObject target)
    {
        if (target == null) return null;
        var button = target.GetComponent<Button>();
        if (button == null)
        {
            Debug.LogWarning("[QuestJournalUI] Il prefab Quest deve avere un componente Button sul root.", target);
            return null;
        }
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

    private static string FormatPhaseLabel(int displayedPhase, int phaseCount)
    {
        return $"{displayedPhase}/{phaseCount}";
    }

}

