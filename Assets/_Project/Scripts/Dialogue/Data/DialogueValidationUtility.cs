using System.Collections.Generic;

public static class DialogueValidationUtility
{
    public static void ValidateConditionGroup(
        DialogueConditionGroup group,
        string path,
        List<string> messages,
        HashSet<DialogueConditionGroup> visited = null)
    {
        if (group == null)
            return;

        if (visited == null)
            visited = new HashSet<DialogueConditionGroup>();
        if (!visited.Add(group))
        {
            messages.Add(path + ": riferimento ciclico tra gruppi di condizioni.");
            return;
        }

        if (group.conditions != null)
        {
            for (int i = 0; i < group.conditions.Count; i++)
            {
                DialogueCondition condition = group.conditions[i];
                if (condition == null)
                {
                    messages.Add($"{path}.conditions[{i}]: elemento null.");
                    continue;
                }

                string error = condition.GetConfigurationError();
                if (!string.IsNullOrEmpty(error))
                    messages.Add(path + ": " + error);
            }
        }

        if (group.groups != null)
        {
            for (int i = 0; i < group.groups.Count; i++)
            {
                DialogueConditionGroup child = group.groups[i];
                if (child == null)
                {
                    messages.Add($"{path}.groups[{i}]: gruppo null.");
                    continue;
                }

                ValidateConditionGroup(child, $"{path}.groups[{i}]", messages, visited);
            }
        }

        visited.Remove(group);
    }

    public static void ValidateActions(IReadOnlyList<DialogueAction> actions, string path, List<string> messages)
    {
        if (actions == null)
            return;

        for (int i = 0; i < actions.Count; i++)
        {
            DialogueAction action = actions[i];
            if (action == null)
            {
                messages.Add($"{path}[{i}]: action null.");
                continue;
            }

            string error = action.GetConfigurationError();
            if (!string.IsNullOrEmpty(error))
                messages.Add($"{path}[{i}]: {error}");

            if (i > 0 && action.stopOnFailure
                && (action.type == DialogueActionType.OpenService
                    || action.type == DialogueActionType.Teleport))
            {
                messages.Add(
                    $"{path}[{i}]: {action.type} bloccante dopo altre action puo lasciare effetti parziali; " +
                    "preferire un singolo servizio atomico o spostarla prima delle mutazioni.");
            }
        }
    }
}
