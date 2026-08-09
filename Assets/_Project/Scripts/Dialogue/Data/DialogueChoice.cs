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
    [Tooltip("Testo breve mostrato sul pulsante della scelta.")]
    [TextArea(2, 5)] public string text;
    [Tooltip("Frase pronunciata dal giocatore dopo la selezione. Se vuota, usa Text.")]
    [TextArea(2, 5)] public string playerSpokenText;
    public DialogueConditionGroup conditions = new DialogueConditionGroup();
    public List<DialogueAction> actions = new List<DialogueAction>();
    public string nextNodeId;
    [Tooltip("Quando il ramo termina, torna a questo node (es. service_menu).")]
    public string returnNodeId;
    public bool playerSpeaksChoice = true;
    public DialogueUnavailableChoiceDisplay unavailableDisplay = DialogueUnavailableChoiceDisplay.Disabled;
    public bool showReadIndicator = false;

    public string ResolvePlayerSpokenText()
    {
        return string.IsNullOrWhiteSpace(playerSpokenText) ? text ?? string.Empty : playerSpokenText;
    }
}
