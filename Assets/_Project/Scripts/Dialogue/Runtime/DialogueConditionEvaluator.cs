using System.Collections.Generic;
using UnityEngine;

public sealed class DialogueConditionEvaluator
{
    public bool Evaluate(DialogueConditionGroup group, DialogueRuntimeContext context)
    {
        return EvaluateGroup(group, context, new HashSet<DialogueConditionGroup>());
    }

    private bool EvaluateGroup(
        DialogueConditionGroup group,
        DialogueRuntimeContext context,
        HashSet<DialogueConditionGroup> visited)
    {
        if (group == null)
            return true;
        if (group.IsEmpty)
            return !group.negate;
        if (!visited.Add(group))
        {
            Debug.LogWarning("[DialogueConditionEvaluator] Gruppo ciclico rilevato: condizione considerata falsa.");
            return false;
        }

        bool hasElements = false;
        bool result = group.logic == DialogueLogicalOperator.And;

        if (group.conditions != null)
        {
            for (int i = 0; i < group.conditions.Count; i++)
            {
                DialogueCondition condition = group.conditions[i];
                if (condition == null)
                    continue;

                hasElements = true;
                bool value = EvaluateCondition(condition, context);
                result = Combine(group.logic, result, value);
                if (CanShortCircuit(group.logic, result))
                    break;
            }
        }

        if (!CanShortCircuit(group.logic, result) && group.groups != null)
        {
            for (int i = 0; i < group.groups.Count; i++)
            {
                DialogueConditionGroup child = group.groups[i];
                if (child == null)
                    continue;

                hasElements = true;
                bool value = EvaluateGroup(child, context, visited);
                result = Combine(group.logic, result, value);
                if (CanShortCircuit(group.logic, result))
                    break;
            }
        }

        visited.Remove(group);
        if (!hasElements)
            result = true;
        return group.negate ? !result : result;
    }

    private bool EvaluateCondition(DialogueCondition condition, DialogueRuntimeContext context)
    {
        bool result;
        PlayerStats stats = context != null ? context.PlayerStats : null;

        switch (condition.type)
        {
            case DialogueConditionType.PlayerAttribute:
                result = stats != null && Compare(GetPlayerAttribute(stats, condition.playerAttribute), condition.value, condition.comparison);
                break;

            case DialogueConditionType.PlayerLevel:
                result = stats != null && Compare(stats.playerLevel, condition.value, condition.comparison);
                break;

            case DialogueConditionType.Karma:
                result = stats != null && Compare(stats.karma, condition.value, condition.comparison);
                break;

            case DialogueConditionType.QuestState:
                result = EvaluateQuestState(condition.id, condition.questState);
                break;

            case DialogueConditionType.StoryFlag:
                result = stats != null && stats.HasStoryFlag(condition.id) == condition.expected;
                break;

            case DialogueConditionType.HasItem:
            {
                int amount = GetItemAmount(context, condition.item);
                result = (amount > 0) == condition.expected;
                break;
            }

            case DialogueConditionType.ItemAmount:
                result = Compare(GetItemAmount(context, condition.item), condition.value, condition.comparison);
                break;

            case DialogueConditionType.HasCoins:
                result = stats != null && Compare(stats.runCoins, condition.value, condition.comparison);
                break;

            case DialogueConditionType.DungeonFloor:
                result = Compare(GetDungeonFloor(context), condition.value, condition.comparison);
                break;

            case DialogueConditionType.DialogueNodeRead:
            {
                string conversationId = ResolveConversationId(condition, context);
                result = stats != null
                         && stats.HasReadDialogueNode(conversationId, condition.nodeId) == condition.expected;
                break;
            }

            case DialogueConditionType.DialogueChoiceSeen:
            {
                string conversationId = ResolveConversationId(condition, context);
                result = stats != null
                         && stats.HasSelectedDialogueChoice(conversationId, condition.nodeId, condition.choiceId) == condition.expected;
                break;
            }

            default:
                result = false;
                break;
        }

        return condition.negate ? !result : result;
    }

    private static int GetPlayerAttribute(PlayerStats stats, DialoguePlayerAttribute attribute)
    {
        switch (attribute)
        {
            case DialoguePlayerAttribute.Vigor: return stats.vigor;
            case DialoguePlayerAttribute.Mind: return stats.mind;
            case DialoguePlayerAttribute.Endurance: return stats.endurance;
            case DialoguePlayerAttribute.Strength: return stats.strength;
            case DialoguePlayerAttribute.Dexterity: return stats.dexterity;
            case DialoguePlayerAttribute.Intelligence: return stats.intelligence;
            case DialoguePlayerAttribute.Faith: return stats.faith;
            default: return 0;
        }
    }

    private static bool EvaluateQuestState(string questId, DialogueQuestState desiredState)
    {
        QuestManager manager = QuestManager.Instance;
        QuestManager.QuestData quest = null;
        bool exists = manager != null && manager.TryGetQuestSnapshot(questId, out quest);

        if (desiredState == DialogueQuestState.NotStarted)
            return !exists;
        if (!exists || quest == null)
            return false;

        switch (desiredState)
        {
            case DialogueQuestState.Active:
                return !quest.completed;
            case DialogueQuestState.ReadyToComplete:
                return quest.completed && !quest.rewardClaimed;
            case DialogueQuestState.Completed:
                return quest.completed;
            case DialogueQuestState.RewardClaimed:
                return quest.rewardClaimed;
            default:
                return false;
        }
    }

    private static int GetItemAmount(DialogueRuntimeContext context, DialogueItemReference item)
    {
        if (context == null || context.PlayerInventory == null || item == null || !item.IsValid)
            return 0;
        return context.PlayerInventory.GetTotalItemAmount(item.Asset);
    }

    private static int GetDungeonFloor(DialogueRuntimeContext context)
    {
        if (context != null && context.DungeonGenerator != null)
            return Mathf.Max(1, context.DungeonGenerator.CurrentFloor);

        if (context != null && context.PlayerStats != null
            && context.PlayerStats.TryGetDungeonCheckpoint(out int floor, out _))
            return Mathf.Max(1, floor);

        return 1;
    }

    private static string ResolveConversationId(DialogueCondition condition, DialogueRuntimeContext context)
    {
        return !string.IsNullOrWhiteSpace(condition.conversationId)
            ? condition.conversationId.Trim()
            : context != null ? context.ConversationId : string.Empty;
    }

    private static bool Compare(int left, int right, DialogueComparisonOperator comparison)
    {
        switch (comparison)
        {
            case DialogueComparisonOperator.Equal: return left == right;
            case DialogueComparisonOperator.NotEqual: return left != right;
            case DialogueComparisonOperator.Greater: return left > right;
            case DialogueComparisonOperator.GreaterOrEqual: return left >= right;
            case DialogueComparisonOperator.Less: return left < right;
            case DialogueComparisonOperator.LessOrEqual: return left <= right;
            default: return false;
        }
    }

    private static bool Combine(DialogueLogicalOperator op, bool accumulated, bool next)
    {
        return op == DialogueLogicalOperator.And ? accumulated && next : accumulated || next;
    }

    private static bool CanShortCircuit(DialogueLogicalOperator op, bool result)
    {
        return op == DialogueLogicalOperator.And ? !result : result;
    }
}
