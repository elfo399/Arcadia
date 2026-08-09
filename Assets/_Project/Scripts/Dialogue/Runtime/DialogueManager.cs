using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public sealed class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("Runtime")]
    [SerializeField] private bool persistAcrossScenes = true;
    [SerializeField] private DialogueUI dialogueUI;
    [SerializeField] private DialogueSpeakerData playerSpeaker;
    [SerializeField] private AudioSource voiceAudioSource;

    [Header("Input")]
    [SerializeField, Min(0f)] private float openingInputGuard = 0.12f;
    [SerializeField, Range(0.1f, 1f)] private float navigationThreshold = 0.55f;
    [SerializeField, Min(0.05f)] private float navigationInitialDelay = 0.35f;
    [SerializeField, Min(0.03f)] private float navigationRepeatDelay = 0.12f;

    private readonly DialogueConditionEvaluator conditionEvaluator = new DialogueConditionEvaluator();
    private readonly DialogueActionRunner actionRunner = new DialogueActionRunner();
    private readonly HashSet<DialogueConversation> validatedConversations = new HashSet<DialogueConversation>();
    private readonly HashSet<DialogueProfile> validatedProfiles = new HashSet<DialogueProfile>();

    private DialogueRuntimeContext context;
    private DialogueNode currentNode;
    private DialogueChoice pendingPlayerChoice;
    private string pendingReturnNodeId;
    private bool choicesPresented;
    private bool hasSelectableChoices;
    private float inputGuardUntil;
    private float nextNavigationTime;
    private int heldNavigationDirection;
    private int suppressConfirmFrame = -1;
    private bool useManualChoiceNavigation;
    private InputAction suppressedUiNavigateAction;
    private bool suppressedUiNavigateWasEnabled;
    private Action<InputAction.CallbackContext> confirmCallback;
    private Action<InputAction.CallbackContext> cancelCallback;
    private Coroutine playerFacingRoutine;
    private Coroutine npcFacingRoutine;
    private Coroutine pendingReleaseRoutine;
    private PlayerController pendingReleaseController;
    private object pendingReleaseOwner;
    private object activeGameplayLockOwner;

    public bool IsDialogueActive { get; private set; }
    public DialogueConversation CurrentConversation => context != null ? context.Conversation : null;
    public DialogueNode CurrentNode => currentNode;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (persistAcrossScenes)
        {
            if (transform.parent != null)
                transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);
        }

        if (voiceAudioSource == null)
            voiceAudioSource = GetComponent<AudioSource>();
        if (voiceAudioSource == null)
            voiceAudioSource = gameObject.AddComponent<AudioSource>();
        voiceAudioSource.playOnAwake = false;
        voiceAudioSource.loop = false;

        confirmCallback = OnConfirmPerformed;
        cancelCallback = OnCancelPerformed;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        RebindDialogueUI(SceneManager.GetActiveScene());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeInput();
        RestoreSceneUiNavigation();
        IsDialogueActive = false;
        StopFacing();
        StopVoice();
        if (dialogueUI != null)
            dialogueUI.Hide();
        BindDialogueUI(null);
        ReleaseAllGameplayLocksImmediately();
    }

    private void OnDestroy()
    {
        BindDialogueUI(null);
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!IsDialogueActive || context == null || context.PlayerController == null)
            return;

        HandleChoiceNavigation();
    }

    public bool TryStartDialogue(NPCInteractable interactable, GameObject player)
    {
        if (IsDialogueActive || interactable == null || dialogueUI == null)
            return false;

        DialogueProfile profile = interactable.Profile;
        if (profile == null)
        {
            Debug.LogWarning($"[DialogueManager] NPC '{interactable.name}' senza DialogueProfile.", interactable);
            return false;
        }

        LogProfileValidation(profile);

        DialogueRuntimeContext newContext = DialogueRuntimeContext.Create(player, interactable, this);
        if (newContext.PlayerStats != null && !newContext.PlayerStats.TryEnsurePersistentStateReady())
        {
            Debug.LogWarning(
                "[DialogueManager] Dialogo rimandato: quest/inventory del save non sono ancora applicabili.",
                interactable);
            return false;
        }

        if (newContext.PlayerController == null)
        {
            Debug.LogWarning(
                "[DialogueManager] Impossibile aprire il dialogo: il player non ha un PlayerController valido.",
                interactable);
            return false;
        }

        DialogueConversation conversation = profile.SelectConversation(conditionEvaluator, newContext);
        if (conversation == null)
        {
            Debug.LogWarning($"[DialogueManager] Nessuna conversazione valida nel profilo '{profile.name}'.", profile);
            return false;
        }

        if (!conversation.TryGetNode(conversation.startNodeId, out _))
        {
            Debug.LogWarning($"[DialogueManager] startNodeId non valido nella conversazione '{conversation.name}'.", conversation);
            LogConversationValidation(conversation);
            return false;
        }

        newContext.Conversation = conversation;
        context = newContext;
        currentNode = null;
        pendingPlayerChoice = null;
        pendingReturnNodeId = string.Empty;
        choicesPresented = false;
        hasSelectableChoices = false;
        heldNavigationDirection = 0;
        suppressConfirmFrame = -1;
        inputGuardUntil = Time.unscaledTime + openingInputGuard;
        IsDialogueActive = true;

        ReleasePendingGameplayLockImmediately();
        activeGameplayLockOwner = new object();
        if (context.PlayerController != null)
            context.PlayerController.AcquireGameplayInputLock(activeGameplayLockOwner);

        SuppressSceneUiNavigation();
        SubscribeInput();
        dialogueUI.Show();
        BeginFacing(interactable);
        LogConversationValidation(conversation);
        ShowNode(conversation.startNodeId);
        return IsDialogueActive;
    }

    public void CloseDialogue()
    {
        CloseDialogue(runCurrentExitActions: true);
    }

    public bool CanRequestTeleport(string targetId, string sceneName)
    {
        if (string.IsNullOrWhiteSpace(targetId))
            return false;

        Transform playerTransform = context != null && context.Player != null
            ? context.Player.transform
            : PlayerStats.instance != null ? PlayerStats.instance.transform : null;
        if (playerTransform == null)
            return false;

        string requestedScene = string.IsNullOrWhiteSpace(sceneName) ? string.Empty : sceneName.Trim();
        string activeScene = SceneManager.GetActiveScene().name;
        if (requestedScene.Length == 0 || string.Equals(requestedScene, activeScene, StringComparison.OrdinalIgnoreCase))
            return DialogueTeleportTarget.TryResolve(targetId, out _);

        return persistAcrossScenes && Application.CanStreamedLevelBeLoaded(requestedScene);
    }

    public bool RequestTeleport(string targetId, string sceneName, bool useTargetRotation)
    {
        if (string.IsNullOrWhiteSpace(targetId))
            return false;

        Transform playerTransform = context != null && context.Player != null
            ? context.Player.transform
            : PlayerStats.instance != null ? PlayerStats.instance.transform : null;
        if (playerTransform == null)
            return false;

        string requestedScene = string.IsNullOrWhiteSpace(sceneName) ? string.Empty : sceneName.Trim();
        string activeScene = SceneManager.GetActiveScene().name;
        if (requestedScene.Length == 0 || string.Equals(requestedScene, activeScene, StringComparison.OrdinalIgnoreCase))
            return TryWarpToRegisteredTarget(playerTransform, targetId, useTargetRotation);

        if (!persistAcrossScenes)
        {
            Debug.LogWarning("[DialogueManager] Teleport tra scene richiede persistAcrossScenes.", this);
            return false;
        }
        if (!Application.CanStreamedLevelBeLoaded(requestedScene))
        {
            Debug.LogWarning($"[DialogueManager] Scena teleport non caricabile o assente dai Build Settings: '{requestedScene}'.", this);
            return false;
        }

        StartCoroutine(LoadSceneAndTeleport(playerTransform, requestedScene, targetId, useTargetRotation));
        CloseDialogue(runCurrentExitActions: false);
        return true;
    }

    private void ShowNode(string requestedNodeId)
    {
        if (!IsDialogueActive || context == null || context.Conversation == null)
            return;

        string nodeId = requestedNodeId;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        DialogueNode node = null;
        while (!string.IsNullOrWhiteSpace(nodeId))
        {
            string normalizedId = nodeId.Trim();
            if (!visited.Add(normalizedId))
            {
                Debug.LogWarning($"[DialogueManager] Ciclo di node non validi rilevato da '{normalizedId}'.", context.Conversation);
                CompleteCurrentPath();
                return;
            }
            if (!context.Conversation.TryGetNode(normalizedId, out node) || node == null)
            {
                Debug.LogWarning($"[DialogueManager] Node inesistente '{normalizedId}'.", context.Conversation);
                CompleteCurrentPath();
                return;
            }
            if (conditionEvaluator.Evaluate(node.conditions, context))
                break;
            nodeId = node.nextNodeId;
            node = null;
        }

        if (node == null)
        {
            CompleteCurrentPath();
            return;
        }

        currentNode = node;
        pendingPlayerChoice = null;
        choicesPresented = false;
        hasSelectableChoices = false;

        bool canEnter = actionRunner.Run(node.actionsOnEnter, context);
        if (!IsDialogueActive)
            return;
        if (!canEnter)
        {
            Debug.LogWarning($"[DialogueManager] Ingresso nel node '{node.nodeId}' interrotto da una action bloccante.", context.Conversation);
            CloseDialogue(runCurrentExitActions: false);
            return;
        }

        string conversationId = context.ConversationId;
        PlayerStats stats = context.PlayerStats;
        TriggerNodeAnimation(node);
        PlayVoice(node.voiceClip);

        DialogueSpeakerData speaker = node.speaker;
        string speakerName = speaker != null ? speaker.ResolveDisplayName(stats) : string.Empty;
        Sprite portrait = node.portraitOverride != null
            ? node.portraitOverride
            : speaker != null ? speaker.ResolvePortrait(stats) : null;
        dialogueUI.ShowLine(speakerName, portrait, node.text ?? string.Empty);
    }

    private void OnLineCompleted()
    {
        if (!IsDialogueActive || currentNode == null || pendingPlayerChoice != null)
            return;

        context.PlayerStats?.MarkDialogueNodeRead(context.ConversationId, currentNode.nodeId);
        if (choicesPresented)
            return;

        PresentChoices();
    }

    private void PresentChoices()
    {
        if (currentNode == null || currentNode.choices == null || currentNode.choices.Count == 0)
            return;

        var presentations = new List<DialogueChoiceViewModel>();
        for (int i = 0; i < currentNode.choices.Count; i++)
        {
            DialogueChoice choice = currentNode.choices[i];
            if (choice == null)
                continue;

            bool available = conditionEvaluator.Evaluate(choice.conditions, context);
            if (!available && choice.unavailableDisplay == DialogueUnavailableChoiceDisplay.Hidden)
                continue;

            bool alreadySeen = context.PlayerStats != null
                               && context.PlayerStats.HasSelectedDialogueChoice(
                                   context.ConversationId,
                                   currentNode.nodeId,
                                   choice.choiceId);
            presentations.Add(new DialogueChoiceViewModel(choice, available, alreadySeen));
        }

        if (presentations.Count == 0)
            return;

        choicesPresented = true;
        dialogueUI.ShowChoices(presentations, OnChoiceSelected);
        bool anyEnabled = false;
        for (int i = 0; i < presentations.Count; i++)
        {
            if (presentations[i].Enabled)
            {
                anyEnabled = true;
                break;
            }
        }

        if (anyEnabled && !dialogueUI.HasChoices)
        {
            Debug.LogError(
                "[DialogueManager] La UI non ha creato i pulsanti per una scelta obbligatoria. " +
                "Il dialogo viene chiuso per non saltare silenziosamente il ramo.",
                dialogueUI);
            CloseDialogue(runCurrentExitActions: false);
            return;
        }

        hasSelectableChoices = anyEnabled;
        dialogueUI.SetContinueAvailable(!hasSelectableChoices);
    }

    private void OnChoiceSelected(DialogueChoice choice)
    {
        if (!IsDialogueActive || currentNode == null || choice == null)
            return;
        if (Time.unscaledTime < inputGuardUntil)
        {
            // A UI Submit can be delivered by the scene EventSystem even when
            // the gameplay Interact callback is guarded. Rebuild the current
            // line instead of accepting an opening-frame choice accidentally.
            RedisplayCurrentNode();
            return;
        }
        if (!conditionEvaluator.Evaluate(choice.conditions, context))
        {
            // The UI clears its runtime buttons before invoking the callback.
            // If state changed since presentation, rebuild instead of leaving
            // the manager convinced that a now-missing selection still exists.
            RedisplayCurrentNode();
            return;
        }

        suppressConfirmFrame = Time.frameCount;

        dialogueUI.ClearChoices();
        choicesPresented = false;
        hasSelectableChoices = false;
        StopVoice();

        if (choice.playerSpeaksChoice)
        {
            pendingPlayerChoice = choice;
            PlayerStats stats = context.PlayerStats;
            string playerName = playerSpeaker != null
                ? playerSpeaker.ResolveDisplayName(stats)
                : stats != null ? stats.PlayerName : SaveSystem.DefaultPlayerName;
            Sprite portrait = playerSpeaker != null
                ? playerSpeaker.ResolvePortrait(stats)
                : stats != null ? stats.PlayerPortrait : null;
            dialogueUI.ShowLine(playerName, portrait, choice.ResolvePlayerSpokenText());
            return;
        }

        ExecuteChoiceAndContinue(choice);
    }

    private void ExecuteChoiceAndContinue(DialogueChoice choice)
    {
        // A spoken player choice is committed only after its synthetic line was
        // completed. Cancelling during that line therefore does not mark it as
        // heard; non-spoken choices reach this point immediately on selection.
        if (currentNode != null)
        {
            context.PlayerStats?.MarkDialogueChoiceSelected(
                context.ConversationId,
                currentNode.nodeId,
                choice.choiceId,
                save: true);
        }

        pendingPlayerChoice = null;
        DialogueActionBatchResult actionResult = actionRunner.RunBatch(choice.actions, context);
        if (!IsDialogueActive)
            return;
        if (!actionResult.TransitionAllowed)
        {
            if (actionResult.RetrySafe)
            {
                RedisplayCurrentNode();
            }
            else
            {
                Debug.LogWarning(
                    "[DialogueManager] Batch choice interrotto dopo possibili effetti parziali: " +
                    "il dialogo viene chiuso per impedire retry/duplicazioni.",
                    this);
                CloseDialogue(runCurrentExitActions: false);
            }
            return;
        }

        // Commit the return target only after the choice action batch succeeds.
        // A failed paid/service choice must not leave a stale return target that
        // later traps an otherwise terminal "exit" choice in the menu node.
        if (!string.IsNullOrWhiteSpace(choice.returnNodeId))
            pendingReturnNodeId = choice.returnNodeId.Trim();

        LeaveCurrentNode(choice.nextNodeId);
    }

    private void AdvanceFromCurrentNode()
    {
        if (currentNode == null)
        {
            CompleteCurrentPath();
            return;
        }

        LeaveCurrentNode(currentNode.nextNodeId);
    }

    private void LeaveCurrentNode(string nextNodeId)
    {
        DialogueNode leavingNode = currentNode;
        choicesPresented = false;
        hasSelectableChoices = false;
        dialogueUI.ClearChoices();
        StopVoice();

        if (leavingNode != null)
        {
            DialogueActionBatchResult exitResult = actionRunner.RunBatch(leavingNode.actionsOnExit, context);
            if (!exitResult.TransitionAllowed)
            {
                // This method may run after a choice batch already changed
                // state. Closing is safer than presenting the choice again and
                // allowing rewards/costs to be repeated.
                Debug.LogWarning(
                    $"[DialogueManager] Uscita dal node '{leavingNode.nodeId}' bloccata; dialogo chiuso senza retry.",
                    context != null ? context.Conversation : this);
                CloseDialogue(runCurrentExitActions: false);
                return;
            }
        }
        if (!IsDialogueActive)
            return;

        currentNode = null;

        if (string.IsNullOrWhiteSpace(nextNodeId))
        {
            CompleteCurrentPath();
            return;
        }

        ShowNode(nextNodeId);
    }

    private void CompleteCurrentPath()
    {
        if (!string.IsNullOrWhiteSpace(pendingReturnNodeId))
        {
            string returnNodeId = pendingReturnNodeId;
            pendingReturnNodeId = string.Empty;
            ShowNode(returnNodeId);
            return;
        }

        CloseDialogue(runCurrentExitActions: false);
    }

    private void OnConfirmPerformed(InputAction.CallbackContext _)
    {
        if (!IsDialogueActive || Time.unscaledTime < inputGuardUntil || dialogueUI == null)
            return;
        if (suppressConfirmFrame == Time.frameCount)
            return;

        if (dialogueUI.IsTyping)
        {
            dialogueUI.CompleteLine();
            return;
        }

        if (pendingPlayerChoice != null)
        {
            DialogueChoice choice = pendingPlayerChoice;
            ExecuteChoiceAndContinue(choice);
            return;
        }

        if (choicesPresented || dialogueUI.HasChoices)
        {
            if (!hasSelectableChoices)
            {
                dialogueUI.ClearChoices();
                choicesPresented = false;
                AdvanceFromCurrentNode();
                return;
            }

            dialogueUI.ConfirmSelection();
            return;
        }

        AdvanceFromCurrentNode();
    }

    private void OnCancelPerformed(InputAction.CallbackContext _)
    {
        if (!IsDialogueActive || Time.unscaledTime < inputGuardUntil)
            return;
        if (suppressConfirmFrame == Time.frameCount)
            return;
        if (context != null && context.Interactable != null && context.Interactable.AllowCancel)
            CloseDialogue(runCurrentExitActions: true);
    }

    private void HandleChoiceNavigation()
    {
        if (!choicesPresented || dialogueUI == null || context.PlayerController.Controls == null)
        {
            heldNavigationDirection = 0;
            return;
        }

        // The project scenes already provide an InputSystemUIInputModule. In
        // that case it owns UI navigation; reading Player.Move as well would
        // move twice for one stick/key press. Keep the explicit PlayerControls
        // path as a fallback for scenes without an EventSystem/input module.
        if (!useManualChoiceNavigation
            && EventSystem.current != null
            && EventSystem.current.currentInputModule != null)
        {
            heldNavigationDirection = 0;
            return;
        }

        float vertical = context.PlayerController.Controls.Player.Move.ReadValue<Vector2>().y;
        int direction = vertical >= navigationThreshold ? -1 : vertical <= -navigationThreshold ? 1 : 0;
        if (direction == 0)
        {
            heldNavigationDirection = 0;
            return;
        }

        float now = Time.unscaledTime;
        if (direction != heldNavigationDirection)
        {
            heldNavigationDirection = direction;
            dialogueUI.MoveSelection(direction);
            nextNavigationTime = now + navigationInitialDelay;
        }
        else if (now >= nextNavigationTime)
        {
            dialogueUI.MoveSelection(direction);
            nextNavigationTime = now + navigationRepeatDelay;
        }
    }

    private void SubscribeInput()
    {
        PlayerControls controls = context != null && context.PlayerController != null
            ? context.PlayerController.Controls
            : null;
        if (controls == null)
            return;

        // Dialogue confirm uses the south face button (X/Cross on PlayStation),
        // already mapped to Jump. The gameplay lock prevents an actual jump.
        controls.Player.Jump.performed -= confirmCallback;
        controls.Player.Jump.performed += confirmCallback;
        controls.Player.SprintOrDodge.performed -= cancelCallback;
        controls.Player.SprintOrDodge.performed += cancelCallback;
    }

    private void UnsubscribeInput()
    {
        PlayerControls controls = context != null && context.PlayerController != null
            ? context.PlayerController.Controls
            : null;
        if (controls == null)
            return;

        controls.Player.Jump.performed -= confirmCallback;
        controls.Player.SprintOrDodge.performed -= cancelCallback;
    }

    private void SuppressSceneUiNavigation()
    {
        RestoreSceneUiNavigation();

        InputSystemUIInputModule inputModule = EventSystem.current != null
            ? EventSystem.current.currentInputModule as InputSystemUIInputModule
            : null;
        if (inputModule == null)
            return;

        // The scenes use the package DefaultInputActions, whose UI/Navigate is
        // also bound to rightStick. Disable only that UI action while a
        // dialogue is active: Player.Move (left stick/WASD) owns choices and
        // Player.Look/rightStick remains available for the gameplay camera.
        useManualChoiceNavigation = true;
        InputActionReference moveReference = inputModule.move;
        suppressedUiNavigateAction = moveReference != null ? moveReference.action : null;
        suppressedUiNavigateWasEnabled = suppressedUiNavigateAction != null
                                         && suppressedUiNavigateAction.enabled;
        if (suppressedUiNavigateWasEnabled)
            suppressedUiNavigateAction.Disable();
    }

    private void RestoreSceneUiNavigation()
    {
        if (suppressedUiNavigateAction != null
            && suppressedUiNavigateWasEnabled
            && !suppressedUiNavigateAction.enabled)
        {
            suppressedUiNavigateAction.Enable();
        }

        useManualChoiceNavigation = false;
        suppressedUiNavigateAction = null;
        suppressedUiNavigateWasEnabled = false;
    }

    private void CloseDialogue(bool runCurrentExitActions)
    {
        if (!IsDialogueActive)
            return;

        if (runCurrentExitActions && currentNode != null)
        {
            DialogueActionBatchResult closeResult = actionRunner.RunBatch(currentNode.actionsOnExit, context);
            if (!IsDialogueActive)
                return;
            if (!closeResult.TransitionAllowed)
            {
                Debug.LogWarning(
                    "[DialogueManager] Action exit bloccante fallita durante la chiusura; " +
                    "il dialogo viene chiuso comunque per evitare un modal soft-lock.",
                    this);
            }
        }

        PlayerController controller = context != null ? context.PlayerController : null;
        PlayerControls controls = controller != null ? controller.Controls : null;
        object lockOwner = activeGameplayLockOwner;
        activeGameplayLockOwner = null;

        IsDialogueActive = false;
        UnsubscribeInput();
        RestoreSceneUiNavigation();
        StopFacing();
        StopVoice();
        if (dialogueUI != null)
            dialogueUI.Hide();
        context?.PlayerStats?.SaveStats();

        currentNode = null;
        pendingPlayerChoice = null;
        pendingReturnNodeId = string.Empty;
        choicesPresented = false;
        hasSelectableChoices = false;
        heldNavigationDirection = 0;
        suppressConfirmFrame = -1;
        context = null;

        if (controller != null && lockOwner != null && isActiveAndEnabled)
        {
            pendingReleaseController = controller;
            pendingReleaseOwner = lockOwner;
            pendingReleaseRoutine = StartCoroutine(ReleaseGameplayLockWhenInputIsSafe(controller, controls, lockOwner));
        }
        else if (controller != null && lockOwner != null)
        {
            controller.ReleaseGameplayInputLock(lockOwner);
        }
    }

    private IEnumerator ReleaseGameplayLockWhenInputIsSafe(
        PlayerController controller,
        PlayerControls controls,
        object lockOwner)
    {
        yield return null;
        while (controls != null && IsAnyBlockedActionPressed(controls))
            yield return null;

        if (controller != null)
            controller.ReleaseGameplayInputLock(lockOwner);
        if (ReferenceEquals(pendingReleaseOwner, lockOwner))
        {
            pendingReleaseController = null;
            pendingReleaseOwner = null;
            pendingReleaseRoutine = null;
        }
    }

    private static bool IsAnyBlockedActionPressed(PlayerControls controls)
    {
        return controls.Player.Interact.IsPressed()
               || controls.Player.SprintOrDodge.IsPressed()
               || controls.Player.Jump.IsPressed()
               || controls.Player.UseFlask.IsPressed()
               || controls.Player.Inventory.IsPressed();
    }

    private void BeginFacing(NPCInteractable interactable)
    {
        if (interactable == null || context == null)
            return;

        Transform lookTarget = interactable.LookTarget != null
            ? interactable.LookTarget
            : interactable.MainSpeakerActor != null ? interactable.MainSpeakerActor.FocusTransform : interactable.transform;

        if (interactable.RotatePlayerTowardsNpc && context.Player != null && lookTarget != null)
            playerFacingRoutine = StartCoroutine(RotateTowards(context.Player.transform, lookTarget, interactable.RotationSpeed));

        Transform npcTransform = interactable.MainSpeakerActor != null
            ? interactable.MainSpeakerActor.transform
            : interactable.transform;
        if (interactable.RotateNpcTowardsPlayer && context.Player != null && npcTransform != null)
            npcFacingRoutine = StartCoroutine(RotateTowards(npcTransform, context.Player.transform, interactable.RotationSpeed));
    }

    private IEnumerator RotateTowards(Transform actor, Transform target, float degreesPerSecond)
    {
        float speed = Mathf.Max(1f, degreesPerSecond);
        float timeout = 3f;
        while (IsDialogueActive && actor != null && target != null && timeout > 0f)
        {
            Vector3 direction = target.position - actor.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                yield break;

            Quaternion desired = Quaternion.LookRotation(direction.normalized, Vector3.up);
            actor.rotation = Quaternion.RotateTowards(actor.rotation, desired, speed * Time.unscaledDeltaTime);
            if (Quaternion.Angle(actor.rotation, desired) <= 0.5f)
                yield break;

            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void StopFacing()
    {
        if (playerFacingRoutine != null)
            StopCoroutine(playerFacingRoutine);
        if (npcFacingRoutine != null)
            StopCoroutine(npcFacingRoutine);
        playerFacingRoutine = null;
        npcFacingRoutine = null;
    }

    private void TriggerNodeAnimation(DialogueNode node)
    {
        if (node == null || string.IsNullOrWhiteSpace(node.animationTrigger) || node.speaker == null)
            return;

        if (node.speaker.isPlayer)
        {
            Animator animator = context != null && context.Player != null
                ? context.Player.GetComponentInChildren<Animator>()
                : null;
            if (!TrySetAnimatorTrigger(animator, node.animationTrigger))
                Debug.LogWarning($"[DialogueManager] Trigger '{node.animationTrigger}' non disponibile sul player.", this);
            return;
        }

        DialogueActor actor = null;
        if (context != null && context.Interactable != null
            && context.Interactable.MainSpeaker == node.speaker)
            actor = context.Interactable.MainSpeakerActor;
        if (actor == null)
            DialogueActor.TryResolve(node.speaker, out actor);
        if (actor == null)
        {
            Debug.LogWarning($"[DialogueManager] Nessun actor/trigger valido per speaker '{node.speaker.speakerId}'.", this);
            return;
        }

        actor.TrySetTrigger(node.animationTrigger);
    }

    private static bool TrySetAnimatorTrigger(Animator animator, string triggerName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(triggerName))
            return false;
        string normalized = triggerName.Trim();
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == normalized)
            {
                animator.SetTrigger(normalized);
                return true;
            }
        }
        return false;
    }

    private void PlayVoice(AudioClip clip)
    {
        StopVoice();
        if (clip == null || voiceAudioSource == null)
            return;
        voiceAudioSource.clip = clip;
        voiceAudioSource.Play();
    }

    private void StopVoice()
    {
        if (voiceAudioSource == null)
            return;
        voiceAudioSource.Stop();
        voiceAudioSource.clip = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode _)
    {
        if (IsDialogueActive)
            CloseDialogue(runCurrentExitActions: false);

        RebindDialogueUI(scene);
    }

    private void RebindDialogueUI(Scene scene)
    {
        DialogueUI sceneUi = null;
        DialogueUI[] candidates = FindObjectsOfType<DialogueUI>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            DialogueUI candidate = candidates[i];
            if (candidate == null || candidate.gameObject.scene != scene)
                continue;

            if (sceneUi == null)
            {
                sceneUi = candidate;
                continue;
            }

            Debug.LogWarning(
                $"[DialogueManager] Piu DialogueUI nella scena '{scene.name}'; uso '{sceneUi.name}'.",
                sceneUi);
            break;
        }

        BindDialogueUI(sceneUi);
        if (sceneUi == null)
            Debug.LogWarning($"[DialogueManager] Nessuna DialogueUI trovata nella scena '{scene.name}'.", this);
    }

    private void BindDialogueUI(DialogueUI nextUi)
    {
        if (dialogueUI == nextUi)
        {
            if (dialogueUI != null)
            {
                dialogueUI.LineCompleted -= OnLineCompleted;
                dialogueUI.LineCompleted += OnLineCompleted;
                if (!IsDialogueActive)
                    dialogueUI.Hide();
            }
            return;
        }

        if (dialogueUI != null)
            dialogueUI.LineCompleted -= OnLineCompleted;

        dialogueUI = nextUi;
        if (dialogueUI == null)
            return;

        dialogueUI.LineCompleted -= OnLineCompleted;
        dialogueUI.LineCompleted += OnLineCompleted;
        if (!IsDialogueActive)
            dialogueUI.Hide();
    }

    private IEnumerator LoadSceneAndTeleport(Transform originalPlayer, string sceneName, string targetId, bool useTargetRotation)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (operation == null)
            yield break;
        while (!operation.isDone)
            yield return null;

        for (int i = 0; i < 10 && !DialogueTeleportTarget.TryResolve(targetId, out _); i++)
            yield return null;

        Transform player = PlayerStats.instance != null ? PlayerStats.instance.transform : originalPlayer;
        if (!TryWarpToRegisteredTarget(player, targetId, useTargetRotation))
            Debug.LogWarning($"[DialogueManager] Target teleport '{targetId}' non trovato nella scena '{sceneName}'.", this);
    }

    private static bool TryWarpToRegisteredTarget(Transform player, string targetId, bool useTargetRotation)
    {
        if (player == null || !DialogueTeleportTarget.TryResolve(targetId, out DialogueTeleportTarget target))
        {
            Debug.LogWarning($"[DialogueManager] Target teleport non trovato: '{targetId}'.");
            return false;
        }

        CharacterController characterController = player.GetComponent<CharacterController>();
        bool wasEnabled = characterController != null && characterController.enabled;
        if (wasEnabled)
            characterController.enabled = false;
        if (useTargetRotation)
            player.SetPositionAndRotation(target.transform.position, target.transform.rotation);
        else
            player.position = target.transform.position;
        if (wasEnabled && characterController != null)
            characterController.enabled = true;
        return true;
    }

    private void LogConversationValidation(DialogueConversation conversation)
    {
        if (conversation == null || !validatedConversations.Add(conversation))
            return;

        List<string> messages = conversation.GetValidationMessages();
        for (int i = 0; i < messages.Count; i++)
            Debug.LogWarning($"[DialogueConversation:{conversation.name}] {messages[i]}", conversation);
    }

    private void LogProfileValidation(DialogueProfile profile)
    {
        if (profile == null || !validatedProfiles.Add(profile))
            return;

        List<string> messages = profile.GetValidationMessages();
        for (int i = 0; i < messages.Count; i++)
            Debug.LogWarning($"[DialogueProfile:{profile.name}] {messages[i]}", profile);
    }

    private void RedisplayCurrentNode()
    {
        if (!IsDialogueActive || currentNode == null || context == null || dialogueUI == null)
            return;

        pendingPlayerChoice = null;
        choicesPresented = false;
        hasSelectableChoices = false;
        dialogueUI.ClearChoices();
        StopVoice();

        PlayerStats stats = context.PlayerStats;
        DialogueSpeakerData speaker = currentNode.speaker;
        string speakerName = speaker != null ? speaker.ResolveDisplayName(stats) : string.Empty;
        Sprite portrait = currentNode.portraitOverride != null
            ? currentNode.portraitOverride
            : speaker != null ? speaker.ResolvePortrait(stats) : null;
        PlayVoice(currentNode.voiceClip);
        dialogueUI.ShowLine(speakerName, portrait, currentNode.text ?? string.Empty);
    }

    private void ReleaseAllGameplayLocksImmediately()
    {
        if (context != null && context.PlayerController != null && activeGameplayLockOwner != null)
            context.PlayerController.ReleaseGameplayInputLock(activeGameplayLockOwner);
        if (pendingReleaseController != null && pendingReleaseOwner != null)
            pendingReleaseController.ReleaseGameplayInputLock(pendingReleaseOwner);

        activeGameplayLockOwner = null;
        pendingReleaseController = null;
        pendingReleaseOwner = null;
        if (pendingReleaseRoutine != null)
            StopCoroutine(pendingReleaseRoutine);
        pendingReleaseRoutine = null;
    }

    private void ReleasePendingGameplayLockImmediately()
    {
        if (pendingReleaseRoutine != null)
            StopCoroutine(pendingReleaseRoutine);
        if (pendingReleaseController != null && pendingReleaseOwner != null)
            pendingReleaseController.ReleaseGameplayInputLock(pendingReleaseOwner);

        pendingReleaseController = null;
        pendingReleaseOwner = null;
        pendingReleaseRoutine = null;
    }
}
