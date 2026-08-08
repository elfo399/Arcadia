using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DialogueTeleportTarget : MonoBehaviour
{
    [SerializeField] private string targetId;
    public string TargetId => targetId;

    private static readonly Dictionary<string, List<DialogueTeleportTarget>> Targets =
        new Dictionary<string, List<DialogueTeleportTarget>>(StringComparer.OrdinalIgnoreCase);

    private void OnEnable()
    {
        if (string.IsNullOrWhiteSpace(targetId))
            return;

        string id = targetId.Trim();
        if (!Targets.TryGetValue(id, out List<DialogueTeleportTarget> targets))
        {
            targets = new List<DialogueTeleportTarget>();
            Targets.Add(id, targets);
        }

        targets.RemoveAll(candidate => candidate == null || candidate == this);
        if (targets.Count > 0)
            Debug.LogWarning($"[DialogueTeleportTarget] targetId duplicato '{id}'.", this);
        targets.Add(this);
    }

    private void OnDisable()
    {
        if (string.IsNullOrWhiteSpace(targetId))
            return;

        string id = targetId.Trim();
        if (!Targets.TryGetValue(id, out List<DialogueTeleportTarget> targets))
            return;

        targets.RemoveAll(candidate => candidate == null || candidate == this);
        if (targets.Count == 0)
            Targets.Remove(id);
    }

    public static bool TryResolve(string targetId, out DialogueTeleportTarget target)
    {
        target = null;
        if (string.IsNullOrWhiteSpace(targetId))
            return false;

        string id = targetId.Trim();
        if (!Targets.TryGetValue(id, out List<DialogueTeleportTarget> targets))
            return false;

        for (int i = targets.Count - 1; i >= 0; i--)
        {
            DialogueTeleportTarget candidate = targets[i];
            if (candidate == null || !candidate.isActiveAndEnabled)
            {
                targets.RemoveAt(i);
                continue;
            }

            target = candidate;
            return true;
        }

        Targets.Remove(id);
        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        Targets.Clear();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(targetId))
            Debug.LogWarning($"[DialogueTeleportTarget] '{name}' senza targetId.", this);
    }
#endif
}
