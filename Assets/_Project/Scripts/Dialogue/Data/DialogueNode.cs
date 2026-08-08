using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class DialogueNode
{
    public string nodeId;
    public DialogueSpeakerData speaker;
    [TextArea(3, 10)] public string text;
    public Sprite portraitOverride;
    public string animationTrigger;
    public AudioClip voiceClip;
    public DialogueConditionGroup conditions = new DialogueConditionGroup();
    public List<DialogueAction> actionsOnEnter = new List<DialogueAction>();
    public List<DialogueAction> actionsOnExit = new List<DialogueAction>();
    public string nextNodeId;
    public List<DialogueChoice> choices = new List<DialogueChoice>();
}
