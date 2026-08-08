using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DialogueActor : MonoBehaviour
{
    [Header("Dialogue Binding")]
    [SerializeField] private DialogueSpeakerData speaker;
    [SerializeField] private Animator animator;
    [Tooltip("Punto verso cui gli altri attori guardano. Se non assegnato viene usato il transform dell'attore.")]
    [SerializeField] private Transform focusTransform;

    private static readonly Dictionary<DialogueSpeakerData, List<DialogueActor>> ActorsBySpeaker =
        new Dictionary<DialogueSpeakerData, List<DialogueActor>>();

    private readonly HashSet<int> warnedTriggerHashes = new HashSet<int>();
    private DialogueSpeakerData registeredSpeaker;

    public DialogueSpeakerData Speaker => speaker;
    public DialogueSpeakerData SpeakerData => speaker;
    public Animator ActorAnimator => ResolveAnimator();
    public Transform FocusTransform => focusTransform != null ? focusTransform : transform;

    private void Awake()
    {
        ResolveAnimator();
    }

    private void OnEnable()
    {
        Register();
    }

    private void OnDisable()
    {
        Unregister();
    }

    /// <summary>
    /// Resolves an enabled scene actor bound to the supplied speaker asset.
    /// When more than one actor uses the same asset, the most recently enabled
    /// valid actor is returned.
    /// </summary>
    public static bool TryResolve(DialogueSpeakerData speakerData, out DialogueActor actor)
    {
        actor = null;
        if (speakerData == null || !ActorsBySpeaker.TryGetValue(speakerData, out List<DialogueActor> actors))
            return false;

        for (int i = actors.Count - 1; i >= 0; i--)
        {
            DialogueActor candidate = actors[i];
            if (candidate == null || !candidate.isActiveAndEnabled || !candidate.gameObject.activeInHierarchy)
            {
                actors.RemoveAt(i);
                continue;
            }

            actor = candidate;
            return true;
        }

        ActorsBySpeaker.Remove(speakerData);
        return false;
    }

    /// <summary>
    /// Fires an Animator trigger only when the actor has a controller containing
    /// a Trigger parameter with the requested name.
    /// </summary>
    public bool TrySetTrigger(string triggerName)
    {
        if (string.IsNullOrWhiteSpace(triggerName))
            return false;

        Animator targetAnimator = ResolveAnimator();
        if (targetAnimator == null || targetAnimator.runtimeAnimatorController == null)
            return false;

        string normalizedName = triggerName.Trim();
        int triggerHash = Animator.StringToHash(normalizedName);
        AnimatorControllerParameter[] parameters = targetAnimator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.nameHash != triggerHash || parameter.type != AnimatorControllerParameterType.Trigger)
                continue;

            targetAnimator.SetTrigger(triggerHash);
            return true;
        }

        if (warnedTriggerHashes.Add(triggerHash))
        {
            Debug.LogWarning(
                $"[DialogueActor] Il trigger Animator '{normalizedName}' non esiste su '{name}' o non e di tipo Trigger.",
                this);
        }

        return false;
    }

    private Animator ResolveAnimator()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
        return animator;
    }

    private void Register()
    {
        Unregister();
        if (speaker == null)
            return;

        if (!ActorsBySpeaker.TryGetValue(speaker, out List<DialogueActor> actors))
        {
            actors = new List<DialogueActor>();
            ActorsBySpeaker.Add(speaker, actors);
        }

        if (!actors.Contains(this))
            actors.Add(this);
        registeredSpeaker = speaker;
    }

    private void Unregister()
    {
        if (registeredSpeaker == null)
            return;

        if (ActorsBySpeaker.TryGetValue(registeredSpeaker, out List<DialogueActor> actors))
        {
            actors.Remove(this);
            if (actors.Count == 0)
                ActorsBySpeaker.Remove(registeredSpeaker);
        }

        registeredSpeaker = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        ActorsBySpeaker.Clear();
    }

#if UNITY_EDITOR
    private void Reset()
    {
        animator = GetComponentInChildren<Animator>(true);
        focusTransform = transform;
    }

    private void OnValidate()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (speaker == null)
            Debug.LogWarning($"[DialogueActor] Speaker mancante su '{name}'.", this);

        if (Application.isPlaying && isActiveAndEnabled && registeredSpeaker != speaker)
            Register();
    }
#endif
}
