using System;
using System.Collections.Generic;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Serializable]
    public class QuestData
    {
        public string questId;
        public string title;
        public string location;
        public bool completed;
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
        public Sprite icon;
        public string type;
        public int amount = 1;
        public string itemName;
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
            DontDestroyOnLoad(gameObject);

        if (quests.Count == 0 && initialQuests != null && initialQuests.Count > 0)
            ReplaceAllQuests(initialQuests, false);
    }

    private void Start()
    {
        if (autoNotifyOnStart)
            NotifyChanged();
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
        }
        else
        {
            quests.Add(new QuestData
            {
                questId = normalizedId,
                title = title,
                location = location,
                completed = completed
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
                icon = entry.icon,
                type = entry.type,
                amount = entry.amount,
                itemName = entry.itemName
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
}
