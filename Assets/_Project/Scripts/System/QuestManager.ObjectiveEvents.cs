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

        string normalizedEventTargetId = NormalizeQuestTargetValue(questEvent.TargetId);
        string normalizedEventTargetTag = NormalizeQuestTargetValue(questEvent.TargetTag);
        var activePhaseByQuest = new Dictionary<QuestData, int>();
        bool changed = false;
        for (int i = 0; i < handles.Count; i++)
        {
            var handle = handles[i];
            if (handle == null || handle.Quest == null || handle.Objective == null)
                continue;

            var objective = handle.Objective;
            if (objective.completed)
                continue;

            if (!activePhaseByQuest.TryGetValue(handle.Quest, out int activePhase))
            {
                activePhase = GetCurrentPhaseNumber(handle.Quest);
                activePhaseByQuest.Add(handle.Quest, activePhase);
            }

            if (objective.phase != activePhase)
                continue;

            if (!QuestObjectiveMatchesEvent(objective, questEvent.Type, normalizedEventTargetId, normalizedEventTargetTag))
                continue;

            objective.requiredAmount = Mathf.Max(1, objective.requiredAmount);
            int previousAmount = objective.currentAmount;
            bool wasCompleted = objective.completed;
            long increasedAmount = (long)Mathf.Max(0, objective.currentAmount) + questEvent.Amount;
            objective.currentAmount = (int)Math.Min(objective.requiredAmount, increasedAmount);
            objective.completed = objective.currentAmount >= objective.requiredAmount;

            if (previousAmount == objective.currentAmount && wasCompleted == objective.completed)
                continue;

            SyncQuestCompletionFromObjectives(handle.Quest);
            if (!handle.Quest.completed)
                handle.Quest.rewardClaimed = false;

            changed = true;
        }

        if (changed)
            NotifyChanged();
    }

    private static bool QuestObjectiveMatchesEvent(QuestObjectiveData objective, QuestObjectiveEventType eventType, string normalizedEventTargetId, string normalizedEventTargetTag)
    {
        if (objective == null || objective.eventType != eventType)
            return false;

        string resolvedTargetId = ResolveQuestObjectiveTargetId(objective);
        bool hasTargetId = !string.IsNullOrWhiteSpace(resolvedTargetId);
        bool hasTargetTag = !string.IsNullOrWhiteSpace(objective.targetTag);

        if (!hasTargetId && !hasTargetTag)
            return true;

        if (hasTargetId && QuestTargetEqualsNormalized(resolvedTargetId, normalizedEventTargetId))
            return true;

        return hasTargetTag && QuestTargetEqualsNormalized(objective.targetTag, normalizedEventTargetTag);
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

    private static bool QuestTargetEqualsNormalized(string configuredValue, string normalizedEventValue)
    {
        string configured = NormalizeQuestTargetValue(configuredValue);
        return !string.IsNullOrEmpty(configured)
               && !string.IsNullOrEmpty(normalizedEventValue)
               && string.Equals(configured, normalizedEventValue, StringComparison.OrdinalIgnoreCase);
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
