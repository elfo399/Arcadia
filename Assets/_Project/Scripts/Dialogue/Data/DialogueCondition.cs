using System;
using System.Collections.Generic;
using UnityEngine;

public enum DialogueConditionType
{
    PlayerAttribute,
    PlayerLevel,
    Karma,
    Benedetto,
    Malefico,
    QuestState,
    StoryFlag,
    HasItem,
    ItemAmount,
    HasCoins,
    DungeonFloor,
    DialogueNodeRead,
    DialogueChoiceSeen
}

public enum DialogueComparisonOperator
{
    Equal,
    NotEqual,
    Greater,
    GreaterOrEqual,
    Less,
    LessOrEqual
}

public enum DialogueLogicalOperator
{
    And,
    Or
}

public enum DialoguePlayerAttribute
{
    Vigor,
    Mind,
    Endurance,
    Strength,
    Dexterity,
    Intelligence,
    Faith
}

public enum DialogueQuestState
{
    NotStarted,
    Active,
    ReadyToComplete,
    Completed,
    RewardClaimed
}

public enum DialogueItemType
{
    Generic,
    Weapon,
    Armor,
    Magic,
    Usable
}

[Serializable]
public sealed class DialogueItemReference
{
    public DialogueItemType itemType = DialogueItemType.Generic;
    public ItemData item;
    public WeaponItem weapon;
    public ArmorItemData armor;
    public MagicItemData magic;
    public UsableItemData usable;

    public ScriptableObject Asset
    {
        get
        {
            switch (itemType)
            {
                case DialogueItemType.Weapon: return weapon;
                case DialogueItemType.Armor: return armor;
                case DialogueItemType.Magic: return magic;
                case DialogueItemType.Usable: return usable;
                default: return item;
            }
        }
    }

    public bool IsValid => Asset != null;
}

[Serializable]
public sealed class DialogueCondition
{
    public DialogueConditionType type;
    [Tooltip("Inverte il risultato finale di questa condizione.")]
    public bool negate;

    public DialogueComparisonOperator comparison = DialogueComparisonOperator.GreaterOrEqual;
    public int value = 1;
    public DialoguePlayerAttribute playerAttribute;

    [Tooltip("Story flag o quest ID, secondo il tipo selezionato.")]
    public string id;
    public DialogueQuestState questState = DialogueQuestState.Active;
    public bool expected = true;

    public DialogueItemReference item = new DialogueItemReference();

    [Tooltip("Vuoto = conversazione corrente.")]
    public string conversationId;
    public string nodeId;
    public string choiceId;

    public string GetConfigurationError()
    {
        switch (type)
        {
            case DialogueConditionType.QuestState:
            case DialogueConditionType.StoryFlag:
                return string.IsNullOrWhiteSpace(id) ? $"{type}: ID mancante." : string.Empty;

            case DialogueConditionType.HasItem:
            case DialogueConditionType.ItemAmount:
                return item == null || !item.IsValid ? $"{type}: item mancante." : string.Empty;

            case DialogueConditionType.DialogueNodeRead:
                return string.IsNullOrWhiteSpace(nodeId) ? "DialogueNodeRead: nodeId mancante." : string.Empty;

            case DialogueConditionType.DialogueChoiceSeen:
                if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(choiceId))
                    return "DialogueChoiceSeen: nodeId/choiceId mancanti.";
                return string.Empty;

            default:
                return string.Empty;
        }
    }
}

[Serializable]
public sealed class DialogueConditionGroup
{
    public DialogueLogicalOperator logic = DialogueLogicalOperator.And;
    [Tooltip("Inverte il risultato dell'intero gruppo.")]
    public bool negate;
    public List<DialogueCondition> conditions = new List<DialogueCondition>();
    [SerializeReference] public List<DialogueConditionGroup> groups = new List<DialogueConditionGroup>();

    public bool IsEmpty => (conditions == null || conditions.Count == 0)
                           && (groups == null || groups.Count == 0);
}
