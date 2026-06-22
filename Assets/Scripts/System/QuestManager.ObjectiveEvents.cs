using System;
using System.Collections.Generic;
using UnityEngine;

public partial class QuestManager
{
    private readonly Dictionary<QuestObjectiveEventType, List<QuestObjectiveHandle>> objectiveHandlesByEventType = new();
    private bool questEventBusSubscribed;

    private sealed class QuestObjectiveHandle
    {
        public QuestData Quest;
        public QuestObjectiveData Objective;
    }

    private void OnEnable()
    {
        SubscribeQuestEvents();
    }

    private void OnDisable()
    {
        UnsubscribeQuestEvents();
    }

    private void SubscribeQuestEvents()
    {
        if (questEventBusSubscribed)
            return;

        QuestEvents.Raised += HandleQuestEventRaised;
        questEventBusSubscribed = true;
    }

    private void UnsubscribeQuestEvents()
    {
        if (!questEventBusSubscribed)
            return;

        QuestEvents.Raised -= HandleQuestEventRaised;
        questEventBusSubscribed = false;
    }

    private void RebuildQuestObjectiveEventIndex()
    {
        objectiveHandlesByEventType.Clear();

        for (int i = 0; i < quests.Count; i++)
        {
            var quest = quests[i];
            if (quest == null || quest.objectives == null)
                continue;

            for (int j = 0; j < quest.objectives.Count; j++)
            {
                var objective = quest.objectives[j];
                if (objective == null || objective.eventType == QuestObjectiveEventType.None)
                    continue;

                if (!objectiveHandlesByEventType.TryGetValue(objective.eventType, out var handles))
                {
                    handles = new List<QuestObjectiveHandle>();
                    objectiveHandlesByEventType.Add(objective.eventType, handles);
                }

                handles.Add(new QuestObjectiveHandle { Quest = quest, Objective = objective });
            }
        }
    }

    private void HandleQuestEventRaised(QuestEvent questEvent)
    {
        if (questEvent.Type == QuestObjectiveEventType.None)
            return;

        if (!objectiveHandlesByEventType.TryGetValue(questEvent.Type, out var handles) || handles == null || handles.Count == 0)
            return;

        bool changed = false;
        for (int i = 0; i < handles.Count; i++)
        {
            var handle = handles[i];
            if (handle == null || handle.Quest == null || handle.Objective == null)
                continue;

            var objective = handle.Objective;
            if (objective.completed)
                continue;

            if (!QuestObjectiveMatchesEvent(objective, questEvent))
                continue;

            objective.requiredAmount = Mathf.Max(1, objective.requiredAmount);
            objective.currentAmount = Mathf.Min(objective.requiredAmount, Mathf.Max(0, objective.currentAmount) + questEvent.Amount);
            if (objective.currentAmount >= objective.requiredAmount)
                objective.completed = true;

            SyncQuestCompletionFromObjectives(handle.Quest);
            if (!handle.Quest.completed)
                handle.Quest.rewardClaimed = false;

            changed = true;
        }

        if (changed)
            NotifyChanged();
    }

    private static bool QuestObjectiveMatchesEvent(QuestObjectiveData objective, QuestEvent questEvent)
    {
        if (objective == null || objective.eventType != questEvent.Type)
            return false;

        string resolvedTargetId = ResolveQuestObjectiveTargetId(objective);
        bool hasTargetId = !string.IsNullOrWhiteSpace(resolvedTargetId);
        bool hasTargetTag = !string.IsNullOrWhiteSpace(objective.targetTag);

        if (!hasTargetId && !hasTargetTag)
            return true;

        if (hasTargetId && QuestTargetEquals(resolvedTargetId, questEvent.TargetId))
            return true;

        return hasTargetTag && QuestTargetEquals(objective.targetTag, questEvent.TargetTag);
    }

    private static string ResolveQuestObjectiveTargetId(QuestObjectiveData objective)
    {
        if (objective == null)
            return string.Empty;

        if (objective.targetObject != null)
            return ResolveQuestTargetObjectId(objective.targetObject);

        return objective.targetId;
    }

    private static string ResolveQuestTargetObjectId(UnityEngine.Object targetObject)
    {
        if (targetObject == null)
            return string.Empty;

        switch (targetObject)
        {
            case EnemyData enemy:
                return !string.IsNullOrWhiteSpace(enemy.enemyName) ? enemy.enemyName : enemy.name;
            case RoomData room:
                return !string.IsNullOrWhiteSpace(room.roomName) ? room.roomName : room.name;
            case WeaponItem weapon:
                return weapon.name;
            case ItemData item:
                return item.name;
            case UsableItemData usable:
                return usable.name;
            case MagicItemData magic:
                return magic.name;
            case ArmorItemData armor:
                return armor.name;
            default:
                return targetObject.name;
        }
    }

    private static bool QuestTargetEquals(string configuredValue, string eventValue)
    {
        string configured = NormalizeQuestTargetValue(configuredValue);
        string raised = NormalizeQuestTargetValue(eventValue);
        return !string.IsNullOrEmpty(configured)
               && !string.IsNullOrEmpty(raised)
               && string.Equals(configured, raised, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeQuestTargetValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string normalized = value.Trim();
        const string cloneSuffix = "(Clone)";
        if (normalized.EndsWith(cloneSuffix, StringComparison.OrdinalIgnoreCase))
            normalized = normalized.Substring(0, normalized.Length - cloneSuffix.Length).Trim();
        return normalized;
    }
}
