using System;
using System.Collections.Generic;
using UnityEngine;

public enum DialogueUnavailableChoiceDisplay
{
    Disabled,
    Hidden
}

[Serializable]
public sealed class DialogueChoice
{
    public string choiceId;
    [TextArea(2, 5)] public string text;
    public DialogueConditionGroup conditions = new DialogueConditionGroup();
    public List<DialogueAction> actions = new List<DialogueAction>();
    public string nextNodeId;
    [Tooltip("Quando il ramo termina, torna a questo node (es. service_menu).")]
    public string returnNodeId;
    public bool playerSpeaksChoice = true;
    public DialogueUnavailableChoiceDisplay unavailableDisplay = DialogueUnavailableChoiceDisplay.Disabled;
    public bool showReadIndicator = false;
}
