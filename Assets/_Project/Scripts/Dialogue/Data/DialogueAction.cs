using System;
using UnityEngine;

public enum DialogueActionType
{
    ModifyKarma = 0,
    GiveAttributePoint = 3,
    AddCoins = 4,
    RemoveCoins = 5,
    AddItem = 6,
    RemoveItem = 7,
    StartQuest = 8,
    CompleteQuest = 9,
    FailQuest = 10,
    SetStoryFlag = 11,
    ClearStoryFlag = 12,
    RestoreHealth = 13,
    RestoreMana = 14,
    RestoreStamina = 15,
    RestoreFlasks = 16,
    OpenService = 17,
    Teleport = 18
}

[Serializable]
public sealed class DialogueAction
{
    public DialogueActionType type;
    [Tooltip("Quantita o delta. I valori narrativi possono essere negativi; reward e costi vengono normalizzati dal runner.")]
    public int amount = 1;
    [Tooltip("Se attivo, interrompe il batch quando questa action fallisce.")]
    public bool stopOnFailure;

    [Tooltip("Story flag o quest ID, secondo il tipo selezionato.")]
    public string id;

    public DialogueItemReference item = new DialogueItemReference();

    public QuestDefinition questDefinition;

    public string serviceId;

    public string teleportTargetId;
    [Tooltip("Vuoto = scena corrente.")]
    public string teleportSceneName;
    public bool useTeleportTargetRotation = true;

    public string GetConfigurationError()
    {
        switch (type)
        {
            case DialogueActionType.ModifyKarma:
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
