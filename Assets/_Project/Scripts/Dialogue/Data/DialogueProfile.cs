using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class DialogueProfileRule
{
    public string ruleId;
    public int priority;
    public DialogueConditionGroup conditions = new DialogueConditionGroup();
    public DialogueConversation conversation;
}

[CreateAssetMenu(fileName = "DialogueProfile", menuName = "Arcadia/Dialogue/Profile")]
public sealed class DialogueProfile : ScriptableObject
{
    public List<DialogueProfileRule> rules = new List<DialogueProfileRule>();
    public DialogueConversation fallbackConversation;

    public DialogueConversation SelectConversation(DialogueConditionEvaluator evaluator, DialogueRuntimeContext context)
    {
        DialogueProfileRule best = null;
        if (rules != null)
        {
            for (int i = 0; i < rules.Count; i++)
            {
                DialogueProfileRule candidate = rules[i];
                if (candidate == null || candidate.conversation == null)
                    continue;
                if (evaluator != null && !evaluator.Evaluate(candidate.conditions, context))
                    continue;
                if (best == null || candidate.priority > best.priority)
                    best = candidate;
            }
        }

        return best != null ? best.conversation : fallbackConversation;
    }

    public List<string> GetValidationMessages()
    {
        var messages = new List<string>();
        if (fallbackConversation == null)
            messages.Add("Fallback conversation mancante.");

        var ruleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var conversationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (rules == null)
            return messages;

        for (int i = 0; i < rules.Count; i++)
        {
            DialogueProfileRule rule = rules[i];
            if (rule == null)
            {
                messages.Add($"rules[{i}] e null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(rule.ruleId))
                messages.Add($"rules[{i}]: ruleId vuoto.");
            else if (!ruleIds.Add(rule.ruleId.Trim()))
                messages.Add($"rules[{i}]: ruleId duplicato '{rule.ruleId}'.");

            if (rule.conversation == null)
                messages.Add($"rules[{i}]: conversation mancante.");
            else if (!string.IsNullOrWhiteSpace(rule.conversation.conversationId)
                     && !conversationIds.Add(rule.conversation.conversationId.Trim()))
                messages.Add($"rules[{i}]: conversationId duplicato '{rule.conversation.conversationId}'.");

            DialogueValidationUtility.ValidateConditionGroup(rule.conditions, $"rules[{i}].conditions", messages);
        }

        return messages;
    }
}
