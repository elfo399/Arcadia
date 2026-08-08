using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DialogueConversation", menuName = "Arcadia/Dialogue/Conversation")]
public sealed class DialogueConversation : ScriptableObject
{
    public string conversationId;
    public string startNodeId;
    public List<DialogueNode> nodes = new List<DialogueNode>();

    [NonSerialized] private Dictionary<string, DialogueNode> nodeLookup;

    private void OnEnable()
    {
        nodeLookup = null;
    }

    public bool TryGetNode(string nodeId, out DialogueNode node)
    {
        node = null;
        EnsureLookup();
        return !string.IsNullOrWhiteSpace(nodeId) && nodeLookup.TryGetValue(nodeId.Trim(), out node);
    }

    public List<string> GetValidationMessages()
    {
        var messages = new List<string>();
        if (string.IsNullOrWhiteSpace(conversationId))
            messages.Add("conversationId vuoto.");
        if (string.IsNullOrWhiteSpace(startNodeId))
            messages.Add("startNodeId vuoto.");

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (nodes == null || nodes.Count == 0)
        {
            messages.Add("La conversazione non contiene node.");
            return messages;
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            DialogueNode node = nodes[i];
            if (node == null)
            {
                messages.Add($"nodes[{i}] e null.");
                continue;
            }

            string nodePath = $"node[{i}]";
            if (string.IsNullOrWhiteSpace(node.nodeId))
                messages.Add(nodePath + ": nodeId vuoto.");
            else if (!ids.Add(node.nodeId.Trim()))
                messages.Add(nodePath + $": nodeId duplicato '{node.nodeId}'.");

            if (node.speaker == null && !string.IsNullOrWhiteSpace(node.text))
                messages.Add(nodePath + ": speaker mancante.");

            DialogueValidationUtility.ValidateConditionGroup(node.conditions, nodePath + ".conditions", messages);
            DialogueValidationUtility.ValidateActions(node.actionsOnEnter, nodePath + ".actionsOnEnter", messages);
            DialogueValidationUtility.ValidateActions(node.actionsOnExit, nodePath + ".actionsOnExit", messages);

            var choiceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (node.choices == null)
                continue;

            for (int choiceIndex = 0; choiceIndex < node.choices.Count; choiceIndex++)
            {
                DialogueChoice choice = node.choices[choiceIndex];
                string choicePath = $"{nodePath}.choices[{choiceIndex}]";
                if (choice == null)
                {
                    messages.Add(choicePath + ": choice null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(choice.choiceId))
                    messages.Add(choicePath + ": choiceId vuoto.");
                else if (!choiceIds.Add(choice.choiceId.Trim()))
                    messages.Add(choicePath + $": choiceId duplicato '{choice.choiceId}'.");

                if (string.IsNullOrWhiteSpace(choice.text))
                    messages.Add(choicePath + ": testo vuoto.");

                DialogueValidationUtility.ValidateConditionGroup(choice.conditions, choicePath + ".conditions", messages);
                DialogueValidationUtility.ValidateActions(choice.actions, choicePath + ".actions", messages);
            }
        }

        if (!string.IsNullOrWhiteSpace(startNodeId) && !ids.Contains(startNodeId.Trim()))
            messages.Add($"startNodeId inesistente: '{startNodeId}'.");

        for (int i = 0; i < nodes.Count; i++)
        {
            DialogueNode node = nodes[i];
            if (node == null)
                continue;

            ValidateNodeReference(node.nextNodeId, $"node '{node.nodeId}' nextNodeId", ids, messages);
            if (node.choices == null)
                continue;

            for (int j = 0; j < node.choices.Count; j++)
            {
                DialogueChoice choice = node.choices[j];
                if (choice == null)
                    continue;

                string prefix = $"node '{node.nodeId}' choice '{choice.choiceId}'";
                ValidateNodeReference(choice.nextNodeId, prefix + " nextNodeId", ids, messages);
                ValidateNodeReference(choice.returnNodeId, prefix + " returnNodeId", ids, messages);
            }
        }

        return messages;
    }

    private static void ValidateNodeReference(string targetId, string path, HashSet<string> ids, List<string> messages)
    {
        if (!string.IsNullOrWhiteSpace(targetId) && !ids.Contains(targetId.Trim()))
            messages.Add($"{path} inesistente: '{targetId}'.");
    }

    private void EnsureLookup()
    {
        if (nodeLookup != null)
            return;

        nodeLookup = new Dictionary<string, DialogueNode>(StringComparer.OrdinalIgnoreCase);
        if (nodes == null)
            return;

        for (int i = 0; i < nodes.Count; i++)
        {
            DialogueNode node = nodes[i];
            if (node == null || string.IsNullOrWhiteSpace(node.nodeId))
                continue;

            string id = node.nodeId.Trim();
            if (!nodeLookup.ContainsKey(id))
                nodeLookup.Add(id, node);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        nodeLookup = null;
    }
#endif
}
