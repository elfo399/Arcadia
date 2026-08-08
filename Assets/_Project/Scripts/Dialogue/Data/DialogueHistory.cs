using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Runtime history for dialogue nodes read and choices selected.
/// Persistence is handled through explicit string-array import/export because
/// Unity's JsonUtility does not serialize HashSet collections.
/// </summary>
public sealed class DialogueHistory
{
    private readonly HashSet<string> readNodeKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> selectedChoiceKeys = new(StringComparer.OrdinalIgnoreCase);

    public int ReadNodeCount => readNodeKeys.Count;
    public int SelectedChoiceCount => selectedChoiceKeys.Count;

    public bool HasReadNode(string conversationId, string nodeId)
    {
        return TryBuildNodeKey(conversationId, nodeId, out string key)
               && readNodeKeys.Contains(key);
    }

    public bool MarkNodeRead(string conversationId, string nodeId)
    {
        return TryBuildNodeKey(conversationId, nodeId, out string key)
               && readNodeKeys.Add(key);
    }

    public bool HasSelectedChoice(string conversationId, string nodeId, string choiceId)
    {
        return TryBuildChoiceKey(conversationId, nodeId, choiceId, out string key)
               && selectedChoiceKeys.Contains(key);
    }

    public bool MarkChoiceSelected(string conversationId, string nodeId, string choiceId)
    {
        return TryBuildChoiceKey(conversationId, nodeId, choiceId, out string key)
               && selectedChoiceKeys.Add(key);
    }

    public void Import(string[] savedReadNodeKeys, string[] savedSelectedChoiceKeys)
    {
        readNodeKeys.Clear();
        selectedChoiceKeys.Clear();
        ImportKeys(savedReadNodeKeys, readNodeKeys);
        ImportKeys(savedSelectedChoiceKeys, selectedChoiceKeys);
    }

    public string[] ExportReadNodeKeys()
    {
        return ExportSorted(readNodeKeys);
    }

    public string[] ExportSelectedChoiceKeys()
    {
        return ExportSorted(selectedChoiceKeys);
    }

    public void Clear()
    {
        readNodeKeys.Clear();
        selectedChoiceKeys.Clear();
    }

    public static string BuildNodeKey(string conversationId, string nodeId)
    {
        return TryBuildNodeKey(conversationId, nodeId, out string key) ? key : string.Empty;
    }

    public static string BuildChoiceKey(string conversationId, string nodeId, string choiceId)
    {
        return TryBuildChoiceKey(conversationId, nodeId, choiceId, out string key) ? key : string.Empty;
    }

    private static bool TryBuildNodeKey(string conversationId, string nodeId, out string key)
    {
        return TryBuildKey(out key, conversationId, nodeId);
    }

    private static bool TryBuildChoiceKey(string conversationId, string nodeId, string choiceId, out string key)
    {
        return TryBuildKey(out key, conversationId, nodeId, choiceId);
    }

    private static bool TryBuildKey(out string key, params string[] segments)
    {
        key = string.Empty;
        if (segments == null || segments.Length == 0)
            return false;

        var builder = new StringBuilder();
        for (int i = 0; i < segments.Length; i++)
        {
            string segment = NormalizeId(segments[i]);
            if (segment.Length == 0)
                return false;

            // Length-prefixing keeps keys unambiguous even when authored IDs
            // contain common separators such as '/', ':', or '|'.
            builder.Append(segment.Length);
            builder.Append(':');
            builder.Append(segment);
            builder.Append('|');
        }

        key = builder.ToString();
        return true;
    }

    private static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static void ImportKeys(string[] source, HashSet<string> destination)
    {
        if (source == null || destination == null)
            return;

        for (int i = 0; i < source.Length; i++)
        {
            string key = NormalizeId(source[i]);
            if (key.Length > 0)
                destination.Add(key);
        }
    }

    private static string[] ExportSorted(HashSet<string> source)
    {
        if (source == null || source.Count == 0)
            return Array.Empty<string>();

        var result = new string[source.Count];
        source.CopyTo(result);
        Array.Sort(result, StringComparer.OrdinalIgnoreCase);
        return result;
    }
}
