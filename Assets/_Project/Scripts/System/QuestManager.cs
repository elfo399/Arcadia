using System;
using System.Collections.Generic;
using UnityEngine;

public partial class QuestManager : MonoBehaviour
{
    public const int MaxObjectivesPerPhase = 5;

    public static QuestManager Instance { get; private set; }

    [Serializable]
    public class QuestData
    {
        public string questId;
        public string title;
        public string location;
        public bool completed;
        public bool rewardClaimed;
        public string questTypeLabel = "Main Quest";
        public string recommendedLabel = "";
        public Sprite questImage;
        public string loreTitle = "";
        [TextArea(2, 6)] public string loreDescription = "";
        public string loreAuthor = "";
        public List<QuestObjectiveData> objectives = new();
        public List<QuestRewardData> rewards = new();
    }

    [Serializable]
    public class QuestObjectiveData
    {
        [Min(1)] public int phase = 1;
        public string title;
        public string description;
        public QuestObjectiveEventType eventType = QuestObjectiveEventType.None;
        public UnityEngine.Object targetObject;
        public string targetId;
        public string targetTag;
        [Min(1)] public int requiredAmount = 1;
        [Min(0)] public int currentAmount = 0;
        public bool completed;
    }

    [Serializable]
    public class QuestRewardData
    {
        public QuestRewardType rewardType = QuestRewardType.Item;
        public string type;
        public int amount = 1;
        public string itemName;
        public WeaponItem weaponAsset;
        public UsableItemData usableAsset;
        public ItemData itemAsset;
        public MagicItemData magicAsset;
        public MagicBlueprintData magicBlueprintAsset;
        public string magicBlueprintRecipeId;
        public ArmorItemData armorAsset;
    }

    [Header("Settings")]
    [SerializeField] private bool persistAcrossScenes = true;
    [SerializeField] private bool autoNotifyOnStart = true;
    [Tooltip("Quest attive immediatamente in una nuova partita.")]
    [SerializeField] private List<QuestDefinition> initialQuestDefinitions = new();
    [Tooltip("Definizioni note ma non attive. Inserire qui le quest avviabili da dialoghi/servizi per ripristinare correttamente asset e reward dopo il load.")]
    [SerializeField] private List<QuestDefinition> questDefinitionCatalog = new();

    private readonly List<QuestData> quests = new();
    private readonly HashSet<string> warnedMissingCatalogQuestIds = new(StringComparer.OrdinalIgnoreCase);

    public event Action<List<QuestData>> OnQuestListChanged;

    public int QuestCount => quests.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (persistAcrossScenes)
            MarkPersistentRoot();

        if (quests.Count == 0)
            ReplaceAllQuests(BuildInitialQuests(), false);
    }

    private void Start()
    {
        if (autoNotifyOnStart)
            NotifyChanged();
    }

    private void MarkPersistentRoot()
    {
        if (transform.parent != null)
            transform.SetParent(null, true);

        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public List<QuestData> GetQuestsSnapshot()
    {
        return CloneQuestList(quests);
    }

    public List<QuestData> GetInitialQuestsSnapshot()
    {
        return CloneQuestList(BuildInitialQuests());
    }

    /// <summary>
    /// Builds definition-backed runtime data for loading a save. Initial quests
    /// are always included; catalog-only quests are materialized only when the
    /// save says that they were started, so catalog registration never makes a
    /// quest active by itself.
    /// </summary>
    public List<QuestData> GetQuestLoadDefinitionsSnapshot(IReadOnlyList<QuestData> savedQuests)
    {
        List<QuestData> result = BuildInitialQuests();
        var includedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < result.Count; i++)
        {
            QuestData quest = result[i];
            if (quest == null)
                continue;

            includedIds.Add(NormalizeQuestId(quest.questId, quest.title, quest.location));
        }

        if (savedQuests == null)
            return result;

        for (int i = 0; i < savedQuests.Count; i++)
        {
            QuestData savedQuest = savedQuests[i];
            if (savedQuest == null)
                continue;

            string questId = NormalizeQuestId(savedQuest.questId, savedQuest.title, savedQuest.location);
            if (includedIds.Contains(questId))
                continue;

            if (!TryGetQuestDefinition(questId, out QuestDefinition definition))
            {
                if (warnedMissingCatalogQuestIds.Add(questId))
                {
                    Debug.LogWarning(
                        $"[QuestManager] La quest salvata '{questId}' non ha una QuestDefinition nel catalogo. "
                        + "Lo stato testuale verra caricato, ma i riferimenti asset delle reward non possono essere ripristinati.",
                        this);
                }

                continue;
            }

            QuestData definitionData = definition.CreateRuntimeData();
            if (definitionData == null)
                continue;

            definitionData.questId = questId;
            result.Add(definitionData);
            includedIds.Add(questId);
        }

        return result;
    }

    public bool TryGetQuestDefinition(string questId, out QuestDefinition definition)
    {
        definition = null;
        if (string.IsNullOrWhiteSpace(questId))
            return false;

        string normalizedId = questId.Trim();
        return TryFindQuestDefinition(initialQuestDefinitions, normalizedId, out definition)
               || TryFindQuestDefinition(questDefinitionCatalog, normalizedId, out definition);
    }

    public bool HasQuest(string questId)
    {
        return FindQuestIndex(questId) >= 0;
    }

    public bool TryGetQuestSnapshot(string questId, out QuestData quest)
    {
        quest = null;

        int index = FindQuestIndex(questId);
        if (index < 0)
            return false;

        quest = CloneQuestData(quests[index]);
        return quest != null;
    }

    public List<QuestEntryData> GetQuestEntriesSnapshot()
    {
        var result = new List<QuestEntryData>();
        for (int i = 0; i < quests.Count; i++)
        {
            var quest = quests[i];
            if (quest == null) continue;
            result.Add(MapToInventoryEntry(quest));
        }

        return result;
    }

    public void ReplaceAllQuests(List<QuestData> newQuests, bool notify = true)
    {
        quests.Clear();
        if (newQuests != null)
        {
            var seenQuestIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < newQuests.Count; i++)
            {
                var q = newQuests[i];
                if (q == null) continue;

                string normalizedId = NormalizeQuestId(q.questId, q.title, q.location);
                if (!seenQuestIds.Add(normalizedId))
                {
                    MergeDuplicateQuestState(FindQuestIndex(normalizedId), q);
                    continue;
                }

                var clone = CloneQuestData(q);
                clone.questId = normalizedId;
                quests.Add(clone);
            }
        }

        RebuildQuestObjectiveEventIndex();

        if (notify)
            NotifyChanged();
    }

    public void ResetToInitialQuests(bool notify = true)
    {
        ReplaceAllQuests(BuildInitialQuests(), notify);
    }

    private List<QuestData> BuildInitialQuests()
    {
        var result = new List<QuestData>();
        var seenQuestIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (initialQuestDefinitions != null)
        {
            for (int i = 0; i < initialQuestDefinitions.Count; i++)
            {
                var definition = initialQuestDefinitions[i];
                if (definition == null) continue;

                var quest = definition.CreateRuntimeData();
                string normalizedId = NormalizeQuestId(quest.questId, quest.title, quest.location);
                if (!seenQuestIds.Add(normalizedId))
                {
                    Debug.LogWarning($"[QuestManager] Quest iniziale duplicata ignorata: {normalizedId}.", this);
                    continue;
                }

                quest.questId = normalizedId;
                result.Add(quest);
            }
        }

        return result;
    }

    public bool TryStartQuest(QuestDefinition definition, bool notify = true)
    {
        if (definition == null)
            return false;
        if (string.IsNullOrWhiteSpace(definition.questId))
        {
            Debug.LogWarning("[QuestManager] StartQuest richiede una QuestDefinition con questId stabile.", definition);
            return false;
        }
        if (!TryGetQuestDefinition(definition.questId, out QuestDefinition registeredDefinition))
        {
            Debug.LogWarning(
                $"[QuestManager] QuestDefinition '{definition.questId.Trim()}' non registrata. "
                + "Aggiungerla al Quest Definition Catalog prima di usarla in StartQuest.",
                definition);
            return false;
        }
        if (!EnsureLoadedQuestStateBeforeMutation())
            return false;

        QuestData runtimeQuest = registeredDefinition.CreateRuntimeData();
        if (runtimeQuest == null)
            return false;

        string normalizedId = NormalizeQuestId(runtimeQuest.questId, runtimeQuest.title, runtimeQuest.location);
        if (FindQuestIndex(normalizedId) >= 0)
            return false;

        QuestData quest = CloneQuestData(runtimeQuest);
        quest.questId = normalizedId;
        quests.Add(quest);

        RebuildQuestObjectiveEventIndex();
        if (notify)
            NotifyChanged();

        return true;
    }

    public bool TryStartQuest(string questId, bool notify = true)
    {
        if (!TryGetQuestDefinition(questId, out QuestDefinition definition))
        {
            Debug.LogWarning($"[QuestManager] QuestDefinition non trovata nel catalogo: '{questId}'.", this);
            return false;
        }

        return TryStartQuest(definition, notify);
    }

    public void AddOrUpdateQuest(string questId, string title, string location, bool completed = false, bool notify = true)
    {
        if (!EnsureLoadedQuestStateBeforeMutation())
            return;

        string normalizedId = NormalizeQuestId(questId, title, location);
        int index = FindQuestIndex(normalizedId);

        if (index >= 0)
        {
            quests[index].title = title;
            quests[index].location = location;
            quests[index].completed = completed;
            if (!completed)
                quests[index].rewardClaimed = false;
        }
        else
        {
            quests.Add(new QuestData
            {
                questId = normalizedId,
                title = title,
                location = location,
                completed = completed,
                rewardClaimed = false
            });
        }

        if (notify)
            NotifyChanged();
    }

    public bool SetQuestCompleted(string questId, bool completed = true, bool notify = true)
    {
        if (!EnsureLoadedQuestStateBeforeMutation())
            return false;

        int index = FindQuestIndex(questId);
        if (index < 0) return false;

        quests[index].completed = completed;
        if (!completed)
            quests[index].rewardClaimed = false;
        if (notify)
            NotifyChanged();
        return true;
    }

    public bool SetQuestObjectiveCompleted(string questId, int objectiveIndex, bool completed = true, bool notify = true)
    {
        if (!EnsureLoadedQuestStateBeforeMutation())
            return false;

        int questIndex = FindQuestIndex(questId);
        if (questIndex < 0) return false;

        var quest = quests[questIndex];
        if (quest == null || quest.objectives == null || objectiveIndex < 0 || objectiveIndex >= quest.objectives.Count)
            return false;

        var objective = quest.objectives[objectiveIndex];
        if (objective == null)
            return false;

        if (completed && objective.phase != GetCurrentPhaseNumber(quest))
            return false;

        objective.completed = completed;
        objective.requiredAmount = Mathf.Max(1, objective.requiredAmount);
        objective.currentAmount = completed ? objective.requiredAmount : Mathf.Min(objective.currentAmount, objective.requiredAmount - 1);
        SyncQuestCompletionFromObjectives(quest);

        if (!quest.completed)
            quest.rewardClaimed = false;

        if (notify)
            NotifyChanged();
        return true;
    }

    public bool SetQuestObjectiveCompleted(string questId, string objectiveTitle, bool completed = true, bool notify = true)
    {
        if (!EnsureLoadedQuestStateBeforeMutation())
            return false;

        int questIndex = FindQuestIndex(questId);
        if (questIndex < 0 || string.IsNullOrWhiteSpace(objectiveTitle)) return false;

        var quest = quests[questIndex];
        if (quest == null || quest.objectives == null)
            return false;

        string normalizedTitle = objectiveTitle.Trim();
        for (int i = 0; i < quest.objectives.Count; i++)
        {
            var objective = quest.objectives[i];
            if (objective == null || string.IsNullOrWhiteSpace(objective.title))
                continue;

            if (!string.Equals(objective.title.Trim(), normalizedTitle, StringComparison.OrdinalIgnoreCase))
                continue;

            if (completed && objective.phase != GetCurrentPhaseNumber(quest))
                return false;

            objective.completed = completed;
            objective.requiredAmount = Mathf.Max(1, objective.requiredAmount);
            objective.currentAmount = completed ? objective.requiredAmount : Mathf.Min(objective.currentAmount, objective.requiredAmount - 1);
            SyncQuestCompletionFromObjectives(quest);

            if (!quest.completed)
                quest.rewardClaimed = false;

            if (notify)
                NotifyChanged();
            return true;
        }

        return false;
    }

    public void SeedFromInventoryEntriesIfEmpty(List<QuestEntryData> sourceEntries, bool notify = true)
    {
        if (quests.Count > 0 || sourceEntries == null || sourceEntries.Count == 0)
            return;

        var mapped = new List<QuestData>(sourceEntries.Count);
        for (int i = 0; i < sourceEntries.Count; i++)
        {
            var source = sourceEntries[i];
            if (source == null) continue;
            mapped.Add(MapFromInventoryEntry(source));
        }

        ReplaceAllQuests(mapped, notify);
    }

    public void MergeMissingDetailsFromInventoryEntries(List<QuestEntryData> sourceEntries, bool notify = true)
    {
        if (sourceEntries == null || sourceEntries.Count == 0 || quests.Count == 0)
            return;

        bool changed = false;
        for (int i = 0; i < quests.Count; i++)
        {
            var target = quests[i];
            if (target == null) continue;

            string targetId = NormalizeQuestId(target.questId, target.title, target.location);
            QuestEntryData source = null;
            for (int j = 0; j < sourceEntries.Count; j++)
            {
                var candidate = sourceEntries[j];
                if (candidate == null) continue;
                string sourceId = NormalizeQuestId(candidate.questId, candidate.title, candidate.location);
                if (!string.Equals(targetId, sourceId, StringComparison.OrdinalIgnoreCase))
                    continue;

                source = candidate;
                break;
            }

            if (source == null) continue;

            if ((target.objectives == null || target.objectives.Count == 0) && source.objectives != null && source.objectives.Count > 0)
            {
                target.objectives = MapObjectivesFromInventory(source.objectives);
                changed = true;
            }

            if ((target.rewards == null || target.rewards.Count == 0) && source.rewards != null && source.rewards.Count > 0)
            {
                target.rewards = MapRewardsFromInventory(source.rewards);
                changed = true;
            }

            changed |= FillIfEmpty(ref target.questTypeLabel, source.questTypeLabel);
            changed |= FillIfEmpty(ref target.recommendedLabel, source.recommendedLabel);
            changed |= FillIfEmpty(ref target.loreTitle, source.loreTitle);
            changed |= FillIfEmpty(ref target.loreDescription, source.loreDescription);
            changed |= FillIfEmpty(ref target.loreAuthor, source.loreAuthor);
        }

        if (changed)
        {
            RebuildQuestObjectiveEventIndex();
            if (notify)
                NotifyChanged();
        }
    }

    private int FindQuestIndex(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId)) return -1;

        string normalized = questId.Trim();
        for (int i = 0; i < quests.Count; i++)
        {
            if (quests[i] == null) continue;
            if (string.Equals(quests[i].questId, normalized, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private bool EnsureLoadedQuestStateBeforeMutation()
    {
        PlayerStats stats = PlayerStats.instance;
        if (stats == null)
            return true;

        if (stats.EnsureLoadedQuestStateApplied())
            return true;

        Debug.LogWarning(
            "[QuestManager] Mutazione quest rimandata: lo stato del save non e ancora applicabile.",
            this);
        return false;
    }

    private static bool TryFindQuestDefinition(
        IReadOnlyList<QuestDefinition> definitions,
        string questId,
        out QuestDefinition definition)
    {
        definition = null;
        if (definitions == null || string.IsNullOrWhiteSpace(questId))
            return false;

        for (int i = 0; i < definitions.Count; i++)
        {
            QuestDefinition candidate = definitions[i];
            if (candidate == null)
                continue;

            // Catalog lookup intentionally requires a real persistent ID. The
            // title/location fallback is retained only for legacy active quest
            // data and must never make a new dialogue quest save-dependent on
            // mutable display text.
            if (string.IsNullOrWhiteSpace(candidate.questId))
                continue;

            string candidateId = candidate.questId.Trim();
            if (!string.Equals(candidateId, questId, StringComparison.OrdinalIgnoreCase))
                continue;

            definition = candidate;
            return true;
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        var knownIds = new Dictionary<string, QuestDefinition>(StringComparer.OrdinalIgnoreCase);
        ValidateQuestDefinitionList(initialQuestDefinitions, "Initial Quest Definitions", knownIds);
        ValidateQuestDefinitionList(questDefinitionCatalog, "Quest Definition Catalog", knownIds);
    }

    private void ValidateQuestDefinitionList(
        IReadOnlyList<QuestDefinition> definitions,
        string listName,
        Dictionary<string, QuestDefinition> knownIds)
    {
        if (definitions == null)
            return;

        for (int i = 0; i < definitions.Count; i++)
        {
            QuestDefinition definition = definitions[i];
            if (definition == null)
                continue;

            if (string.IsNullOrWhiteSpace(definition.questId))
            {
                Debug.LogWarning($"[QuestManager] {listName}[{i}] non ha un questId stabile.", this);
                continue;
            }

            string questId = definition.questId.Trim();
            if (!knownIds.TryGetValue(questId, out QuestDefinition existing))
            {
                knownIds.Add(questId, definition);
                continue;
            }

            if (existing != definition)
            {
                Debug.LogWarning(
                    $"[QuestManager] questId duplicato '{questId}' tra '{existing.name}' e '{definition.name}'.",
                    this);
            }
        }
    }
#endif

    private void MergeDuplicateQuestState(int targetIndex, QuestData duplicate)
    {
        if (targetIndex < 0 || targetIndex >= quests.Count || duplicate == null)
            return;

        var target = quests[targetIndex];
        if (target == null)
            return;

        target.completed |= duplicate.completed || duplicate.rewardClaimed;
        target.rewardClaimed |= duplicate.rewardClaimed;
        MergeDuplicateObjectiveState(target.objectives, duplicate.objectives);
        SyncQuestCompletionFromObjectives(target);
    }

    private static void MergeDuplicateObjectiveState(List<QuestObjectiveData> target, List<QuestObjectiveData> duplicate)
    {
        if (target == null || duplicate == null)
            return;

        int count = Mathf.Min(target.Count, duplicate.Count);
        for (int i = 0; i < count; i++)
        {
            var targetObjective = target[i];
            var duplicateObjective = duplicate[i];
            if (targetObjective == null || duplicateObjective == null)
                continue;

            targetObjective.requiredAmount = Mathf.Max(1, Mathf.Max(targetObjective.requiredAmount, duplicateObjective.requiredAmount));
            targetObjective.currentAmount = Mathf.Max(targetObjective.currentAmount, duplicateObjective.currentAmount);
            targetObjective.completed |= duplicateObjective.completed;
        }
    }

    private static void SyncQuestCompletionFromObjectives(QuestData quest)
    {
        if (quest == null || quest.objectives == null || quest.objectives.Count == 0)
            return;

        bool allCompleted = true;
        for (int i = 0; i < quest.objectives.Count; i++)
        {
            var objective = quest.objectives[i];
            if (objective == null || objective.completed)
                continue;

            allCompleted = false;
            break;
        }

        quest.completed = allCompleted;
    }

    public static int GetCurrentPhaseNumber(QuestData quest)
    {
        if (quest == null || quest.objectives == null || quest.objectives.Count == 0)
            return 1;

        int lastPhase = 1;
        for (int i = 0; i < quest.objectives.Count; i++)
        {
            var objective = quest.objectives[i];
            if (objective == null) continue;
            lastPhase = Mathf.Max(lastPhase, objective.phase);
            if (!objective.completed)
                return Mathf.Max(1, objective.phase);
        }

        return lastPhase;
    }

    public static int GetCurrentPhaseNumber(QuestEntryData quest)
    {
        if (quest == null || quest.objectives == null || quest.objectives.Count == 0)
            return 1;

        int lastPhase = 1;
        for (int i = 0; i < quest.objectives.Count; i++)
        {
            var objective = quest.objectives[i];
            if (objective == null) continue;
            lastPhase = Mathf.Max(lastPhase, objective.phase);
            if (!objective.completed)
                return Mathf.Max(1, objective.phase);
        }

        return lastPhase;
    }

    public static int GetPhaseCount(QuestEntryData quest)
    {
        int count = 1;
        if (quest == null || quest.objectives == null)
            return count;

        for (int i = 0; i < quest.objectives.Count; i++)
        {
            var objective = quest.objectives[i];
            if (objective != null)
                count = Mathf.Max(count, objective.phase);
        }

        return count;
    }

    private void NotifyChanged()
    {
        OnQuestListChanged?.Invoke(GetQuestsSnapshot());
    }

    private static List<QuestData> CloneQuestList(List<QuestData> source)
    {
        var result = new List<QuestData>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] == null) continue;
            result.Add(CloneQuestData(source[i]));
        }

        return result;
    }

    private static QuestData CloneQuestData(QuestData source)
    {
        if (source == null) return null;
        return new QuestData
        {
            questId = source.questId,
            title = source.title,
            location = source.location,
            completed = source.completed,
            rewardClaimed = source.rewardClaimed,
            questTypeLabel = source.questTypeLabel,
            recommendedLabel = source.recommendedLabel,
            questImage = source.questImage,
            loreTitle = source.loreTitle,
            loreDescription = source.loreDescription,
            loreAuthor = source.loreAuthor,
            objectives = CloneObjectives(source.objectives),
            rewards = CloneRewards(source.rewards)
        };
    }

    private static List<QuestObjectiveData> CloneObjectives(List<QuestObjectiveData> source)
    {
        var result = new List<QuestObjectiveData>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            var entry = source[i];
            if (entry == null) continue;
            result.Add(new QuestObjectiveData
            {
                phase = Mathf.Max(1, entry.phase),
                title = entry.title,
                description = entry.description,
                eventType = entry.eventType,
                targetObject = entry.targetObject,
                targetId = entry.targetId,
                targetTag = entry.targetTag,
                requiredAmount = Mathf.Max(1, entry.requiredAmount),
                currentAmount = Mathf.Max(0, entry.currentAmount),
                completed = entry.completed
            });
        }

        NormalizeObjectivePhases(result);
        return result;
    }

    private static void NormalizeObjectivePhases(List<QuestObjectiveData> objectives)
    {
        if (objectives == null || objectives.Count == 0)
            return;

        int normalizedPhase = 1;
        int objectivesInPhase = 0;
        int previousDeclaredPhase = 1;
        bool hasPrevious = false;

        for (int i = 0; i < objectives.Count; i++)
        {
            var objective = objectives[i];
            if (objective == null) continue;

            int declaredPhase = Mathf.Max(1, objective.phase);
            if (hasPrevious && declaredPhase != previousDeclaredPhase)
            {
                normalizedPhase++;
                objectivesInPhase = 0;
            }

            if (objectivesInPhase >= MaxObjectivesPerPhase)
            {
                normalizedPhase++;
                objectivesInPhase = 0;
            }

            objective.phase = normalizedPhase;
            objectivesInPhase++;
            previousDeclaredPhase = declaredPhase;
            hasPrevious = true;
        }
    }

    private static List<QuestRewardData> CloneRewards(List<QuestRewardData> source)
    {
        var result = new List<QuestRewardData>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            var entry = source[i];
            if (entry == null) continue;
            result.Add(new QuestRewardData
            {
                rewardType = entry.rewardType,
                type = entry.type,
                amount = entry.amount,
                itemName = entry.itemName,
                weaponAsset = entry.weaponAsset,
                usableAsset = entry.usableAsset,
                itemAsset = entry.itemAsset,
                magicAsset = entry.magicAsset,
                magicBlueprintAsset = entry.magicBlueprintAsset,
                magicBlueprintRecipeId = entry.magicBlueprintRecipeId,
                armorAsset = entry.armorAsset
            });
        }

        return result;
    }

    private static string NormalizeQuestId(string questId, string title, string location)
    {
        if (!string.IsNullOrWhiteSpace(questId))
            return questId.Trim();

        string safeTitle = string.IsNullOrWhiteSpace(title) ? "Quest" : title.Trim();
        string safeLocation = string.IsNullOrWhiteSpace(location) ? "Unknown" : location.Trim();
        return safeTitle + "|" + safeLocation;
    }

    private static QuestEntryData MapToInventoryEntry(QuestData source)
    {
        if (source == null) return null;
        return new QuestEntryData
        {
            questId = source.questId,
            title = source.title,
            location = source.location,
            completed = source.completed,
            rewardClaimed = source.rewardClaimed,
            questTypeLabel = source.questTypeLabel,
            recommendedLabel = source.recommendedLabel,
            questImage = source.questImage,
            loreTitle = source.loreTitle,
            loreDescription = source.loreDescription,
            loreAuthor = source.loreAuthor,
            objectives = MapObjectivesToInventory(source.objectives),
            rewards = MapRewardsToInventory(source.rewards)
        };
    }

    private static QuestData MapFromInventoryEntry(QuestEntryData source)
    {
        if (source == null) return null;
        return new QuestData
        {
            questId = source.questId,
            title = source.title,
            location = source.location,
            completed = source.completed,
            rewardClaimed = source.rewardClaimed,
            questTypeLabel = source.questTypeLabel,
            recommendedLabel = source.recommendedLabel,
            questImage = source.questImage,
            loreTitle = source.loreTitle,
            loreDescription = source.loreDescription,
            loreAuthor = source.loreAuthor,
            objectives = MapObjectivesFromInventory(source.objectives),
            rewards = MapRewardsFromInventory(source.rewards)
        };
    }

    private static List<QuestObjectiveEntryData> MapObjectivesToInventory(List<QuestObjectiveData> source)
    {
        var result = new List<QuestObjectiveEntryData>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            var objective = source[i];
            if (objective == null) continue;
            result.Add(new QuestObjectiveEntryData
            {
                phase = Mathf.Max(1, objective.phase),
                title = objective.title,
                description = objective.description,
                eventType = objective.eventType,
                targetObject = objective.targetObject,
                targetId = objective.targetId,
                targetTag = objective.targetTag,
                requiredAmount = Mathf.Max(1, objective.requiredAmount),
                currentAmount = Mathf.Max(0, objective.currentAmount),
                completed = objective.completed
            });
        }

        return result;
    }

    private static List<QuestRewardEntryData> MapRewardsToInventory(List<QuestRewardData> source)
    {
        var result = new List<QuestRewardEntryData>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            var reward = source[i];
            if (reward == null) continue;
            result.Add(new QuestRewardEntryData
            {
                rewardType = reward.rewardType,
                type = ResolveRewardTypeString(reward),
                amount = reward.amount,
                itemName = ResolveRewardItemName(reward),
                weaponAsset = reward.weaponAsset,
                usableAsset = reward.usableAsset,
                itemAsset = reward.itemAsset,
                magicAsset = reward.magicAsset,
                magicBlueprintAsset = reward.magicBlueprintAsset,
                magicBlueprintRecipeId = ResolveMagicBlueprintRecipeId(reward),
                armorAsset = reward.armorAsset
            });
        }

        return result;
    }

    private static List<QuestObjectiveData> MapObjectivesFromInventory(List<QuestObjectiveEntryData> source)
    {
        var result = new List<QuestObjectiveData>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            var objective = source[i];
            if (objective == null) continue;
            result.Add(new QuestObjectiveData
            {
                phase = Mathf.Max(1, objective.phase),
                title = objective.title,
                description = objective.description,
                eventType = objective.eventType,
                targetObject = objective.targetObject,
                targetId = objective.targetId,
                targetTag = objective.targetTag,
                requiredAmount = Mathf.Max(1, objective.requiredAmount),
                currentAmount = Mathf.Max(0, objective.currentAmount),
                completed = objective.completed
            });
        }

        NormalizeObjectivePhases(result);
        return result;
    }

    private static List<QuestRewardData> MapRewardsFromInventory(List<QuestRewardEntryData> source)
    {
        var result = new List<QuestRewardData>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            var reward = source[i];
            if (reward == null) continue;
            result.Add(new QuestRewardData
            {
                rewardType = reward.rewardType,
                type = string.IsNullOrWhiteSpace(reward.type) ? reward.rewardType.ToString() : reward.type,
                amount = reward.amount,
                itemName = reward.itemName,
                weaponAsset = reward.weaponAsset,
                usableAsset = reward.usableAsset,
                itemAsset = reward.itemAsset,
                magicAsset = reward.magicAsset,
                magicBlueprintAsset = reward.magicBlueprintAsset,
                magicBlueprintRecipeId = reward.magicBlueprintRecipeId,
                armorAsset = reward.armorAsset
            });
        }

        return result;
    }

    public static Sprite ResolveRewardIcon(QuestRewardData reward)
    {
        if (reward == null)
            return null;
        return reward.rewardType switch
        {
            QuestRewardType.Weapon => reward.weaponAsset != null ? reward.weaponAsset.icon : null,
            QuestRewardType.Usable => reward.usableAsset != null ? reward.usableAsset.icon : null,
            QuestRewardType.Item => reward.itemAsset != null ? reward.itemAsset.icon : null,
            QuestRewardType.Magic => reward.magicAsset != null ? reward.magicAsset.icon : null,
            QuestRewardType.MagicBlueprint => reward.magicBlueprintAsset != null && reward.magicBlueprintAsset.recipe != null
                && reward.magicBlueprintAsset.recipe.resultMagic != null ? reward.magicBlueprintAsset.recipe.resultMagic.icon : null,
            QuestRewardType.Armor => reward.armorAsset != null ? reward.armorAsset.icon : null,
            _ => null
        };
    }

    private static string ResolveRewardTypeString(QuestRewardData reward)
    {
        if (reward == null)
            return string.Empty;
        if (!string.IsNullOrWhiteSpace(reward.type))
            return reward.type;
        return reward.rewardType.ToString();
    }

    private static string ResolveRewardItemName(QuestRewardData reward)
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
            QuestRewardType.MagicBlueprint => reward.magicBlueprintAsset != null && reward.magicBlueprintAsset.recipe != null
                ? reward.magicBlueprintAsset.recipe.resultMagic != null ? reward.magicBlueprintAsset.recipe.resultMagic.magicName : reward.magicBlueprintAsset.recipe.recipeId
                : reward.magicBlueprintRecipeId ?? string.Empty,
            QuestRewardType.Armor => reward.armorAsset != null ? reward.armorAsset.itemName : string.Empty,
            _ => string.Empty
        };
    }

    private static string ResolveMagicBlueprintRecipeId(QuestRewardData reward)
    {
        if (reward == null) return string.Empty;
        if (!string.IsNullOrWhiteSpace(reward.magicBlueprintRecipeId))
            return reward.magicBlueprintRecipeId;
        return reward.magicBlueprintAsset != null && reward.magicBlueprintAsset.recipe != null
            ? reward.magicBlueprintAsset.recipe.recipeId
            : string.Empty;
    }

    private static bool FillIfEmpty(ref string target, string source)
    {
        if (!string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(source))
            return false;

        target = source;
        return true;
    }
}

