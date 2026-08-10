using UnityEngine;

[DisallowMultipleComponent]
public sealed class NPCInteractable : MonoBehaviour, IInteractable
{
    [Header("Dialogue")]
    [SerializeField] private string promptOverride;
    [SerializeField] private NpcProfile npcProfile;
    [Tooltip("Attore principale usato per speaker, animazioni e orientamento.")]
    [SerializeField] private DialogueActor mainSpeakerActor;
    [Tooltip("Punto verso cui ruota il player. Se non assegnato usa il focus del main speaker.")]
    [SerializeField] private Transform lookTarget;

    [Header("Orientation")]
    [SerializeField] private bool rotatePlayerTowardsNpc = true;
    [SerializeField] private bool rotateNpcTowardsPlayer = true;
    [Min(0f)]
    [SerializeField] private float rotationSpeed = 360f;

    [Header("Input")]
    [SerializeField] private bool allowCancel = true;

    private bool warnedMissingManager;
    private bool warnedMissingProfile;

    public DialogueProfile Profile => npcProfile != null ? npcProfile.dialogueProfile : null;
    public NpcProfile NpcProfile => npcProfile;
    public DialogueActor MainSpeakerActor => mainSpeakerActor;
    public DialogueSpeakerData MainSpeaker => mainSpeakerActor != null ? mainSpeakerActor.Speaker : null;
    public Transform LookTarget => lookTarget != null
        ? lookTarget
        : mainSpeakerActor != null ? mainSpeakerActor.FocusTransform : transform;
    public Transform NpcTransform => mainSpeakerActor != null ? mainSpeakerActor.transform : transform;
    public bool RotatePlayerTowardsNpc => rotatePlayerTowardsNpc;
    public bool RotateNpcTowardsPlayer => rotateNpcTowardsPlayer;
    public float RotationSpeed => rotationSpeed;
    public bool AllowCancel => allowCancel;

    public string GetPrompt()
    {
        if (!string.IsNullOrWhiteSpace(promptOverride))
            return promptOverride.Trim();
        string displayName = npcProfile != null ? npcProfile.displayName : null;
        return string.IsNullOrWhiteSpace(displayName) ? "Parla" : $"Parla con {displayName.Trim()}";
    }

    public void Interact(GameObject player)
    {
        if (!isActiveAndEnabled || player == null)
            return;

        DialogueManager manager = DialogueManager.Instance;
        if (manager == null)
        {
            if (!warnedMissingManager)
            {
                Debug.LogWarning("[NPCInteractable] Nessun DialogueManager attivo nella scena.", this);
                warnedMissingManager = true;
            }

            return;
        }

        if (manager.IsDialogueActive)
            return;

        if (Profile == null)
        {
            if (!warnedMissingProfile)
            {
                Debug.LogWarning($"[NPCInteractable] DialogueProfile mancante su '{name}'.", this);
                warnedMissingProfile = true;
            }

            return;
        }

        manager.TryStartDialogue(this, player);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        mainSpeakerActor = GetComponentInChildren<DialogueActor>(true);
    }

    private void OnValidate()
    {
        rotationSpeed = Mathf.Max(0f, rotationSpeed);

        if (Profile == null)
            Debug.LogWarning($"[NPCInteractable] DialogueProfile mancante su '{name}'.", this);
        if (mainSpeakerActor == null)
            Debug.LogWarning($"[NPCInteractable] Main Speaker Actor mancante su '{name}'.", this);
    }
#endif
}
