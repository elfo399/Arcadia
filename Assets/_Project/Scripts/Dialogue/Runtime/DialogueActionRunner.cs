using System.Collections.Generic;
using UnityEngine;

public readonly struct DialogueActionBatchResult
{
    public bool TransitionAllowed { get; }
    public bool RetrySafe { get; }

    public DialogueActionBatchResult(bool transitionAllowed, bool retrySafe)
    {
        TransitionAllowed = transitionAllowed;
        RetrySafe = retrySafe;
    }
}

public sealed class DialogueActionRunner
{
    public bool Run(IReadOnlyList<DialogueAction> actions, DialogueRuntimeContext context)
    {
        return RunBatch(actions, context).TransitionAllowed;
    }

    public DialogueActionBatchResult RunBatch(
        IReadOnlyList<DialogueAction> actions,
        DialogueRuntimeContext context)
    {
        if (actions == null || actions.Count == 0)
            return new DialogueActionBatchResult(transitionAllowed: true, retrySafe: true);

        if (context != null && context.PlayerStats != null
            && !context.PlayerStats.TryEnsurePersistentStateReady())
        {
            Debug.LogWarning(
                "[DialogueActionRunner] Batch rimandato: lo stato persistente quest/inventory non e ancora applicato.");
            return new DialogueActionBatchResult(transitionAllowed: false, retrySafe: true);
        }

        // Validate every blocking operation before applying earlier mutations.
        // This keeps common purchase/cost batches atomic when an authored
        // resource, quest, item, service, or teleport target is unavailable.
        for (int i = 0; i < actions.Count; i++)
        {
            DialogueAction action = actions[i];
            if (action == null || !action.stopOnFailure || CanRunSingle(action, context))
                continue;

            Debug.LogWarning($"[DialogueActionRunner] Pre-check action {action.type} fallito (indice {i}).");
            return new DialogueActionBatchResult(transitionAllowed: false, retrySafe: true);
        }

        bool transitionAllowed = true;
        bool retrySafe = true;
        bool potentialSideEffectRan = false;
        bool persistentStateChanged = false;
        for (int i = 0; i < actions.Count; i++)
        {
            DialogueAction action = actions[i];
            if (action == null)
                continue;

            bool succeeded = RunSingle(action, context, out bool changed);
            persistentStateChanged |= changed;
            if (!succeeded)
            {
                Debug.LogWarning($"[DialogueActionRunner] Action {action.type} fallita (indice {i}).");
                if (action.stopOnFailure)
                {
                    transitionAllowed = false;
                    retrySafe = !potentialSideEffectRan && IsFailureSideEffectFree(action.type);
                    break;
                }

                if (!IsFailureSideEffectFree(action.type))
                    potentialSideEffectRan = true;
            }
            else
            {
                potentialSideEffectRan = true;
            }
        }

        if (persistentStateChanged && context != null && context.PlayerStats != null)
            context.PlayerStats.SaveStats();
        return new DialogueActionBatchResult(transitionAllowed, retrySafe);
    }

    private static bool CanRunSingle(DialogueAction action, DialogueRuntimeContext context)
    {
        if (action == null)
            return true;

        PlayerStats stats = context != null ? context.PlayerStats : null;
        PlayerInventory inventory = context != null ? context.PlayerInventory : null;
        int positiveAmount = ToPositiveAmount(action.amount);

        switch (action.type)
        {
            case DialogueActionType.ModifyKarma:
            case DialogueActionType.ModifyBenedetto:
            case DialogueActionType.ModifyMalefico:
                return stats != null;

            case DialogueActionType.GiveAttributePoint:
                return stats != null && positiveAmount > 0 && stats.unspentAttributePoints < int.MaxValue;

            case DialogueActionType.AddCoins:
                return stats != null && positiveAmount > 0 && stats.runCoins < int.MaxValue;

            case DialogueActionType.RemoveCoins:
                return stats != null && positiveAmount > 0 && stats.HasCoins(positiveAmount);

            case DialogueActionType.AddItem:
                return inventory != null && action.item != null && action.item.IsValid
                       && inventory.CanAddItem(action.item.Asset, positiveAmount);

            case DialogueActionType.RemoveItem:
                return inventory != null && action.item != null && action.item.IsValid
                       && positiveAmount > 0 && inventory.HasItem(action.item.Asset, positiveAmount);

            case DialogueActionType.StartQuest:
            {
                QuestManager quests = QuestManager.Instance;
                string questId = action.questDefinition != null ? action.questDefinition.questId : string.Empty;
                return quests != null && action.questDefinition != null && !string.IsNullOrWhiteSpace(questId)
                       && quests.TryGetQuestDefinition(questId, out _)
                       && !quests.HasQuest(questId);
            }

            case DialogueActionType.CompleteQuest:
                return QuestManager.Instance != null && QuestManager.Instance.HasQuest(action.id);

            case DialogueActionType.FailQuest:
                return false;

            case DialogueActionType.SetStoryFlag:
            case DialogueActionType.ClearStoryFlag:
                return stats != null && !string.IsNullOrWhiteSpace(action.id);

            case DialogueActionType.RestoreHealth:
            case DialogueActionType.RestoreMana:
            case DialogueActionType.RestoreStamina:
            case DialogueActionType.RestoreFlasks:
                return stats != null && positiveAmount > 0;

            case DialogueActionType.OpenService:
                return NpcServiceRegistry.HasService(action.serviceId);

            case DialogueActionType.Teleport:
                return context != null && context.Manager != null
                       && context.Manager.CanRequestTeleport(action.teleportTargetId, action.teleportSceneName);

            default:
                return false;
        }
    }

    private static bool IsFailureSideEffectFree(DialogueActionType type)
    {
        // External service implementations may mutate UI/game state before
        // returning false. Built-in adapters fail before applying their change.
        return type != DialogueActionType.OpenService;
    }

    private static bool RunSingle(DialogueAction action, DialogueRuntimeContext context, out bool persistentStateChanged)
    {
        persistentStateChanged = false;
        PlayerStats stats = context != null ? context.PlayerStats : null;
        PlayerInventory inventory = context != null ? context.PlayerInventory : null;
        int positiveAmount = ToPositiveAmount(action.amount);

        switch (action.type)
        {
            case DialogueActionType.ModifyKarma:
                if (stats == null) return false;
                persistentStateChanged = stats.ModifyKarma(action.amount, save: false);
                return true;

            case DialogueActionType.ModifyBenedetto:
                if (stats == null) return false;
                persistentStateChanged = stats.ModifyBenedetto(action.amount, save: false);
                return true;

            case DialogueActionType.ModifyMalefico:
                if (stats == null) return false;
                persistentStateChanged = stats.ModifyMalefico(action.amount, save: false);
                return true;

            case DialogueActionType.GiveAttributePoint:
                if (stats == null || positiveAmount <= 0) return false;
                persistentStateChanged = stats.AddAttributePoints(positiveAmount, save: false);
                return persistentStateChanged;

            case DialogueActionType.AddCoins:
                if (stats == null || positiveAmount <= 0) return false;
                int previousCoins = stats.runCoins;
                stats.AddCoins(positiveAmount, save: false);
                persistentStateChanged = stats.runCoins != previousCoins;
                return persistentStateChanged;

            case DialogueActionType.RemoveCoins:
                if (stats == null || positiveAmount <= 0) return false;
                persistentStateChanged = stats.TryRemoveCoins(positiveAmount, save: false);
                return persistentStateChanged;

            case DialogueActionType.AddItem:
                if (inventory == null || action.item == null || !action.item.IsValid || positiveAmount <= 0) return false;
                persistentStateChanged = inventory.TryAddItem(action.item.Asset, positiveAmount, save: false);
                return persistentStateChanged;

            case DialogueActionType.RemoveItem:
                if (inventory == null || action.item == null || !action.item.IsValid || positiveAmount <= 0) return false;
                persistentStateChanged = inventory.TryRemoveItem(action.item.Asset, positiveAmount, out _, save: false);
                return persistentStateChanged;

            case DialogueActionType.StartQuest:
            {
                QuestManager quests = QuestManager.Instance;
                if (quests == null || action.questDefinition == null) return false;
                persistentStateChanged = quests.TryStartQuest(action.questDefinition);
                return persistentStateChanged;
            }

            case DialogueActionType.CompleteQuest:
            {
                QuestManager quests = QuestManager.Instance;
                if (quests == null || string.IsNullOrWhiteSpace(action.id)) return false;
                persistentStateChanged = quests.SetQuestCompleted(action.id, true);
                return persistentStateChanged;
            }

            case DialogueActionType.FailQuest:
                Debug.LogWarning("[DialogueActionRunner] FailQuest non e supportata dal QuestManager attuale; action ignorata.");
                return false;

            case DialogueActionType.SetStoryFlag:
                if (stats == null) return false;
                persistentStateChanged = stats.SetStoryFlag(action.id, save: false);
                return !string.IsNullOrWhiteSpace(action.id);

            case DialogueActionType.ClearStoryFlag:
                if (stats == null) return false;
                persistentStateChanged = stats.ClearStoryFlag(action.id, save: false);
                return !string.IsNullOrWhiteSpace(action.id);

            case DialogueActionType.RestoreHealth:
                if (stats == null || positiveAmount <= 0) return false;
                stats.RestoreHealth(positiveAmount);
                return true;

            case DialogueActionType.RestoreMana:
                if (stats == null || positiveAmount <= 0) return false;
                stats.RestoreMana(positiveAmount);
                return true;

            case DialogueActionType.RestoreStamina:
                if (stats == null || positiveAmount <= 0) return false;
                stats.RestoreStamina(positiveAmount);
                return true;

            case DialogueActionType.RestoreFlasks:
                if (stats == null || positiveAmount <= 0) return false;
                stats.RestoreFlasks(positiveAmount);
                return true;

            case DialogueActionType.OpenService:
                return OpenService(action.serviceId, context);

            case DialogueActionType.Teleport:
                return context != null && context.Manager != null
                       && context.Manager.RequestTeleport(action.teleportTargetId, action.teleportSceneName, action.useTeleportTargetRotation);

            default:
                return false;
        }
    }

    private static bool OpenService(string serviceId, DialogueRuntimeContext context)
    {
        if (context == null)
            return false;

        return NpcServiceRegistry.TryOpen(serviceId, new NpcServiceContext
        {
            DialogueManager = context.Manager,
            Interactable = context.Interactable,
            Player = context.Player,
            PlayerStats = context.PlayerStats,
            PlayerInventory = context.PlayerInventory
        });
    }

    private static int ToPositiveAmount(int amount)
    {
        if (amount == int.MinValue)
            return int.MaxValue;
        return Mathf.Abs(amount);
    }
}
