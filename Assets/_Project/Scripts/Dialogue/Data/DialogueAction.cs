using System;
using UnityEngine;

public enum DialogueActionType
{
    ModifyKarma,
    ModifyBenedetto,
    ModifyMalefico,
    GiveAttributePoint,
    AddCoins,
    RemoveCoins,
    AddItem,
    RemoveItem,
    StartQuest,
    CompleteQuest,
    FailQuest,
    SetStoryFlag,
    ClearStoryFlag,
    RestoreHealth,
    RestoreMana,
    RestoreStamina,
    RestoreFlasks,
    OpenService,
    Teleport
}

[Serializable]
public sealed class DialogueAction
{
    public DialogueActionType type;
    [Tooltip("Quantita o delta. I valori narrativi possono essere negativi; reward e costi vengono normalizzati dal runner.")]
    public int amount = 1;
    [Tooltip("Se attivo, interrompe il batch quando questa action fallisce.")]
    public bool stopOnFailure;

    [Header("ID")]
    [Tooltip("Story flag o quest ID, secondo il tipo selezionato.")]
    public string id;

    [Header("Item")]
    public DialogueItemReference item = new DialogueItemReference();

    [Header("Quest")]
    public QuestDefinition questDefinition;

    [Header("NPC Service")]
    public string serviceId;

    [Header("Teleport")]
    public string teleportTargetId;
    [Tooltip("Vuoto = scena corrente.")]
    public string teleportSceneName;
    public bool useTeleportTargetRotation = true;

    public string GetConfigurationError()
    {
        switch (type)
        {
            case DialogueActionType.ModifyKarma:
            case DialogueActionType.ModifyBenedetto:
            case DialogueActionType.ModifyMalefico:
            case DialogueActionType.GiveAttributePoint:
            case DialogueActionType.AddCoins:
            case DialogueActionType.RemoveCoins:
            case DialogueActionType.AddItem:
            case DialogueActionType.RemoveItem:
            case DialogueActionType.RestoreHealth:
            case DialogueActionType.RestoreMana:
            case DialogueActionType.RestoreStamina:
            case DialogueActionType.RestoreFlasks:
                if (amount == 0)
                    return $"{type}: amount non puo essere zero.";
                break;
        }

        switch (type)
        {
            case DialogueActionType.SetStoryFlag:
            case DialogueActionType.ClearStoryFlag:
            case DialogueActionType.CompleteQuest:
            case DialogueActionType.FailQuest:
                return string.IsNullOrWhiteSpace(id) ? $"{type}: ID mancante." : string.Empty;

            case DialogueActionType.AddItem:
            case DialogueActionType.RemoveItem:
                return item == null || !item.IsValid ? $"{type}: item mancante." : string.Empty;

            case DialogueActionType.StartQuest:
                if (questDefinition == null)
                    return "StartQuest: QuestDefinition mancante.";
                return string.IsNullOrWhiteSpace(questDefinition.questId)
                    ? "StartQuest: la QuestDefinition non ha un questId stabile."
                    : string.Empty;

            case DialogueActionType.OpenService:
                return string.IsNullOrWhiteSpace(serviceId) ? "OpenService: serviceId mancante." : string.Empty;

            case DialogueActionType.Teleport:
                return string.IsNullOrWhiteSpace(teleportTargetId) ? "Teleport: teleportTargetId mancante." : string.Empty;

            default:
                return string.Empty;
        }
    }
}
