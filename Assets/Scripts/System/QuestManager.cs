using System;
using System.Collections.Generic;
using UnityEngine;

public partial class QuestManager : MonoBehaviour
{
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
        public string loreTitle = "";
        [TextArea(2, 6)] public string loreDescription = "";
        public string loreAuthor = "";
        public List<QuestObjectiveData> objectives = new();
        public List<QuestRewardData> rewards = new();
    }

    [Serializable]
    public class QuestObjectiveData
    {
        public string title;
        public string description;
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
        public ArmorItemData armorAsset;
    }

    [Header("Settings")]
    [SerializeField] private bool persistAcrossScenes = true;
    [SerializeField] private bool autoNotifyOnStart = true;
    [SerializeField] private List<QuestData> initialQuests = new();

    private readonly List<QuestData> quests = new();

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

        if (quests.Count == 0 && initialQuests != null && initialQuests.Count > 0)
            ReplaceAllQuests(initialQuests, false);
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
            for (int i = 0; i < newQuests.Count; i++)
            {
                var q = newQuests[i];
                if (q == null) continue;
                quests.Add(CloneQuestData(q));
            }
        }

        if (notify)
            NotifyChanged();
    }

    public void AddOrUpdateQuest(string questId, string title, string location, bool completed = false, bool notify = true)
    {
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
        int index = FindQuestIndex(questId);
        if (index < 0) return false;

        quests[index].completed = completed;
        if (!completed)
            quests[index].rewardClaimed = false;
        if (notify)
            NotifyChanged();
        return true;
    }

    public bool RemoveQuest(string questId, bool notify = true)
    {
        int index = FindQuestIndex(questId);
        if (index < 0) return false;

        quests.RemoveAt(index);
        if (notify)
            NotifyChanged();
        return true;
    }

    public void ClearAll(bool notify = true)
    {
        quests.Clear();
        if (notify)
            NotifyChanged();
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

        if (changed && notify)
            NotifyChanged();
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
                title = entry.title,
                description = entry.description,
                completed = entry.completed
            });
        }

        return result;
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
                title = objective.title,
                description = objective.description,
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
                title = objective.title,
                description = objective.description,
                completed = objective.completed
            });
        }

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
            QuestRewardType.Armor => reward.armorAsset != null ? reward.armorAsset.itemName : string.Empty,
            _ => string.Empty
        };
    }

    private static bool FillIfEmpty(ref string target, string source)
    {
        if (!string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(source))
            return false;

        target = source;
        return true;
    }
}

