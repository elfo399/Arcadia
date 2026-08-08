using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DialogueConversation))]
[CanEditMultipleObjects]
public sealed class DialogueConversationEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();

        DrawValidationMessages();

        if (GUILayout.Button("Validate"))
            LogValidationResults();
    }

    private void DrawValidationMessages()
    {
        for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
        {
            var conversation = targets[targetIndex] as DialogueConversation;
            if (conversation == null)
                continue;

            List<string> messages = conversation.GetValidationMessages();
            if (targets.Length > 1)
                EditorGUILayout.LabelField(conversation.name, EditorStyles.boldLabel);

            if (messages.Count == 0)
            {
                EditorGUILayout.HelpBox("Validation OK: nessun problema rilevato.", MessageType.Info);
                continue;
            }

            for (int i = 0; i < messages.Count; i++)
                EditorGUILayout.HelpBox(messages[i], MessageType.Warning);
        }
    }

    private void LogValidationResults()
    {
        for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
        {
            var conversation = targets[targetIndex] as DialogueConversation;
            if (conversation == null)
                continue;

            List<string> messages = conversation.GetValidationMessages();
            if (messages.Count == 0)
            {
                Debug.Log($"[Dialogue Validation] '{conversation.name}' e valida.", conversation);
                continue;
            }

            for (int i = 0; i < messages.Count; i++)
                Debug.LogWarning($"[Dialogue Validation:{conversation.name}] {messages[i]}", conversation);
        }
    }
}

[CustomEditor(typeof(DialogueProfile))]
[CanEditMultipleObjects]
public sealed class DialogueProfileEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();

        DrawValidationMessages();

        if (GUILayout.Button("Validate"))
            LogValidationResults();
    }

    private void DrawValidationMessages()
    {
        for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
        {
            var profile = targets[targetIndex] as DialogueProfile;
            if (profile == null)
                continue;

            List<string> messages = profile.GetValidationMessages();
            if (targets.Length > 1)
                EditorGUILayout.LabelField(profile.name, EditorStyles.boldLabel);

            if (messages.Count == 0)
            {
                EditorGUILayout.HelpBox("Validation OK: nessun problema rilevato.", MessageType.Info);
                continue;
            }

            for (int i = 0; i < messages.Count; i++)
                EditorGUILayout.HelpBox(messages[i], MessageType.Warning);
        }
    }

    private void LogValidationResults()
    {
        for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
        {
            var profile = targets[targetIndex] as DialogueProfile;
            if (profile == null)
                continue;

            List<string> messages = profile.GetValidationMessages();
            if (messages.Count == 0)
            {
                Debug.Log($"[Dialogue Validation] '{profile.name}' e valido.", profile);
                continue;
            }

            for (int i = 0; i < messages.Count; i++)
                Debug.LogWarning($"[Dialogue Validation:{profile.name}] {messages[i]}", profile);
        }
    }
}
