using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Presentation data for one already-filtered dialogue choice.
/// Hidden choices must be omitted by the caller; unavailable visible choices
/// are included with <see cref="Enabled"/> set to false.
/// </summary>
public sealed class DialogueChoiceViewModel
{
    public DialogueChoice Choice { get; }
    public bool Enabled { get; }
    public bool AlreadySeen { get; }

    public DialogueChoiceViewModel(DialogueChoice choice, bool enabled, bool alreadySeen)
    {
        Choice = choice;
        Enabled = enabled;
        AlreadySeen = alreadySeen;
    }
}

/// <summary>
/// Pure presentation layer for dialogue lines and choices. Dialogue flow,
/// conditions, history, input subscriptions, and voice playback belong to the
/// DialogueManager and its collaborators.
/// </summary>
public sealed class DialogueUI : MonoBehaviour
{
    [Header("Hierarchy")]
    [SerializeField] private GameObject dialogueRoot;
    [SerializeField] private GameObject portraitContainer;
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text speakerNameText;
    [SerializeField] private TMP_Text dialogueBodyText;
    [SerializeField] private Transform choicesRoot;
    [SerializeField] private ScrollRect choicesScrollRect;
    [SerializeField] private Button choiceButtonPrefab;
    [SerializeField] private GameObject continueIndicator;

    [Header("Typewriter")]
    [SerializeField, Min(0f)] private float charactersPerSecond = 45f;

    [Header("Choice Presentation")]
    [SerializeField] private string lockedChoicePrefix = "\uD83D\uDD12 ";
    [SerializeField] private string seenChoicePrefix = "\u2713 ";

    private sealed class RuntimeChoiceButton
    {
        public Button Button;
        public DialogueChoiceViewModel ViewModel;
    }

    private readonly List<RuntimeChoiceButton> runtimeChoiceButtons = new();

    private Coroutine typewriterRoutine;
    private Action currentLineCompletedCallback;
    private Action<DialogueChoice> choiceSelectedCallback;
    private bool currentLinePendingCompletion;
    private bool isTyping;
    private int selectedChoiceIndex = -1;
    private int totalLineCharacters;
    private bool warnedAboutChoiceConfiguration;
    private bool warnedAboutChoiceLabel;
    private GameObject lastObservedChoiceSelection;

    /// <summary>Raised once when the current line finishes, naturally or through CompleteLine.</summary>
    public event Action LineCompleted;

    public bool IsTyping => isTyping;
    public bool HasChoices => runtimeChoiceButtons.Count > 0;
    public int ChoiceCount => runtimeChoiceButtons.Count;
    public bool IsVisible => dialogueRoot != null && dialogueRoot.activeSelf;

    public float CharactersPerSecond
    {
        get => charactersPerSecond;
        set => charactersPerSecond = Mathf.Max(0f, value);
    }

    private void LateUpdate()
    {
        // With InputSystemUIInputModule the EventSystem moves Button focus
        // directly, bypassing MoveSelection. Observe that focus here so a
        // controller/keyboard selection never disappears below the viewport.
        if (choicesScrollRect == null || runtimeChoiceButtons.Count == 0 || EventSystem.current == null)
        {
            lastObservedChoiceSelection = null;
            return;
        }

        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
        if (selectedObject == null)
        {
            // A later re-selection of the same Button is still a focus change
            // and must be allowed to bring a manually scrolled row back into view.
            lastObservedChoiceSelection = null;
            return;
        }

        if (selectedObject == lastObservedChoiceSelection)
            return;

        for (int i = 0; i < runtimeChoiceButtons.Count; i++)
        {
            Button button = runtimeChoiceButtons[i].Button;
            if (button == null || selectedObject != button.gameObject || !IsChoiceEnabled(i))
                continue;

            selectedChoiceIndex = i;
            lastObservedChoiceSelection = selectedObject;
            EnsureChoiceVisible(button);
            return;
        }

        lastObservedChoiceSelection = null;
    }

    private void Awake()
    {
        // A scene-local button can be used as an inactive template. Never
        // destroy or present that authored object as if it were a runtime row.
        HideSceneChoiceTemplate();
        SetPortrait(null);
        SetContinueIndicator(false);

        if (choicesRoot != null)
            choicesRoot.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        CancelCurrentLine();
        ClearChoices();
        SetContinueIndicator(false);
    }

    private void OnDestroy()
    {
        CancelCurrentLine();
        currentLineCompletedCallback = null;
        choiceSelectedCallback = null;
        LineCompleted = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        charactersPerSecond = Mathf.Max(0f, charactersPerSecond);
    }
#endif

    public void Show()
    {
        if (dialogueRoot != null && !dialogueRoot.activeSelf)
            dialogueRoot.SetActive(true);
    }

    public void Hide()
    {
        CancelCurrentLine();
        ClearChoices();
        SetContinueIndicator(false);
        SetPortrait(null);

        if (speakerNameText != null)
            speakerNameText.text = string.Empty;

        if (dialogueBodyText != null)
        {
            dialogueBodyText.text = string.Empty;
            dialogueBodyText.maxVisibleCharacters = int.MaxValue;
        }

        if (dialogueRoot != null && dialogueRoot.activeSelf)
            dialogueRoot.SetActive(false);
    }

    /// <summary>
    /// Displays a line and begins its typewriter animation. Voice playback is
    /// intentionally owned by DialogueManager so replacing a line can stop the
    /// corresponding AudioSource in the same place that started it.
    /// </summary>
    public void ShowLine(
        string speakerName,
        Sprite portrait,
        string text,
        Action onCompleted = null)
    {
        Show();
        CancelCurrentLine();
        ClearChoices();
        SetContinueIndicator(false);

        currentLineCompletedCallback = onCompleted;
        currentLinePendingCompletion = true;

        if (speakerNameText != null)
            speakerNameText.text = speakerName ?? string.Empty;

        SetPortrait(portrait);

        if (dialogueBodyText == null)
        {
            FinishCurrentLine();
            return;
        }

        dialogueBodyText.text = text ?? string.Empty;
        dialogueBodyText.maxVisibleCharacters = int.MaxValue;
        // ignoreActiveState keeps this deterministic if a caller presents a
        // line while an ancestor canvas is being activated in the same frame.
        dialogueBodyText.ForceMeshUpdate(true, true);
        totalLineCharacters = dialogueBodyText.textInfo.characterCount;

        if (totalLineCharacters <= 0 || charactersPerSecond <= 0f)
        {
            FinishCurrentLine();
            return;
        }

        dialogueBodyText.maxVisibleCharacters = 0;
        isTyping = true;
        typewriterRoutine = StartCoroutine(TypeCurrentLine());
    }

    /// <summary>
    /// Reveals the current line immediately. Returns false when no typewriter
    /// animation was active, allowing the manager to distinguish reveal from
    /// advance with one Confirm input.
    /// </summary>
    public bool CompleteLine()
    {
        if (!isTyping)
            return false;

        FinishCurrentLine();
        return true;
    }

    /// <summary>
    /// Builds buttons for choices already filtered for visibility by the
    /// manager. Disabled entries remain visible and receive the lock prefix.
    /// </summary>
    public void ShowChoices(
        IReadOnlyList<DialogueChoiceViewModel> choices,
        Action<DialogueChoice> onChoiceSelected)
    {
        ClearChoices();
        SetContinueIndicator(false);

        if (choices == null || choices.Count == 0)
        {
            SetContinueIndicator(!isTyping && !currentLinePendingCompletion);
            return;
        }

        if (choicesRoot == null || choiceButtonPrefab == null)
        {
            WarnAboutMissingChoiceConfiguration();
            return;
        }

        HideSceneChoiceTemplate();
        choicesRoot.gameObject.SetActive(true);
        choiceSelectedCallback = onChoiceSelected;

        for (int i = 0; i < choices.Count; i++)
        {
            DialogueChoiceViewModel viewModel = choices[i];
            if (viewModel == null || viewModel.Choice == null)
                continue;

            Button button = Instantiate(choiceButtonPrefab, choicesRoot);
            if (button == null)
                continue;

            button.gameObject.SetActive(true);
            button.interactable = viewModel.Enabled;

            var runtimeButton = new RuntimeChoiceButton
            {
                Button = button,
                ViewModel = viewModel
            };
            runtimeChoiceButtons.Add(runtimeButton);

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = BuildChoiceLabel(viewModel);
            else
                WarnAboutMissingChoiceLabel();

            // Keep any persistent feedback configured on the prefab. This
            // runtime listener is discarded together with the cloned button.
            button.onClick.AddListener(() => ActivateChoice(runtimeButton));
        }

        if (runtimeChoiceButtons.Count == 0)
        {
            choiceSelectedCallback = null;
            choicesRoot.gameObject.SetActive(false);
            SetContinueIndicator(!isTyping && !currentLinePendingCompletion);
            return;
        }

        ResetChoiceScroll();
        ConfigureVerticalNavigation();
        SelectFirstEnabledChoice();
    }

    /// <summary>
    /// Moves to the next enabled choice with wraparound. Positive direction is
    /// down/next; negative direction is up/previous.
    /// </summary>
    public bool MoveSelection(int direction)
    {
        if (direction == 0 || runtimeChoiceButtons.Count == 0)
            return false;

        SynchronizeSelectionFromEventSystem();

        int step = direction > 0 ? 1 : -1;
        int index = selectedChoiceIndex;
        if (index < 0 || index >= runtimeChoiceButtons.Count)
            index = step > 0 ? runtimeChoiceButtons.Count - 1 : 0;

        for (int checkedCount = 0; checkedCount < runtimeChoiceButtons.Count; checkedCount++)
        {
            index = WrapIndex(index + step, runtimeChoiceButtons.Count);
            if (SelectChoiceAt(index))
                return true;
        }

        return false;
    }

    /// <summary>Selects the currently focused enabled choice.</summary>
    public bool ConfirmSelection()
    {
        if (runtimeChoiceButtons.Count == 0)
            return false;

        SynchronizeSelectionFromEventSystem();
        if (!IsChoiceEnabled(selectedChoiceIndex))
            return false;

        RuntimeChoiceButton runtimeButton = runtimeChoiceButtons[selectedChoiceIndex];
        ActivateChoice(runtimeButton);
        return true;
    }

    /// <summary>
    /// Removes only buttons instantiated by this component. Authored children
    /// and the serialized template/prefab are never destroyed.
    /// </summary>
    public void ClearChoices()
    {
        ClearOwnedEventSystemSelection();

        for (int i = 0; i < runtimeChoiceButtons.Count; i++)
        {
            Button button = runtimeChoiceButtons[i].Button;
            if (button == null)
                continue;

            button.gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(button.gameObject);
            else
                DestroyImmediate(button.gameObject);
        }

        runtimeChoiceButtons.Clear();
        selectedChoiceIndex = -1;
        lastObservedChoiceSelection = null;
        choiceSelectedCallback = null;

        if (choicesRoot != null)
            choicesRoot.gameObject.SetActive(false);
    }

    public void SetContinueAvailable(bool visible)
    {
        SetContinueIndicator(visible && !isTyping);
    }

    private IEnumerator TypeCurrentLine()
    {
        float visibleCharacterProgress = 0f;
        int visibleCharacterCount = 0;

        while (visibleCharacterCount < totalLineCharacters)
        {
            float currentSpeed = Mathf.Max(0f, charactersPerSecond);
            if (currentSpeed <= 0f)
                break;

            visibleCharacterProgress += currentSpeed * Time.unscaledDeltaTime;
            int nextVisibleCount = Mathf.Min(
                totalLineCharacters,
                Mathf.FloorToInt(visibleCharacterProgress));

            if (nextVisibleCount != visibleCharacterCount)
            {
                visibleCharacterCount = nextVisibleCount;
                if (dialogueBodyText != null)
                    dialogueBodyText.maxVisibleCharacters = visibleCharacterCount;
            }

            yield return null;
        }

        typewriterRoutine = null;
        FinishCurrentLine();
    }

    private void FinishCurrentLine()
    {
        if (!currentLinePendingCompletion)
            return;

        if (typewriterRoutine != null)
        {
            StopCoroutine(typewriterRoutine);
            typewriterRoutine = null;
        }

        if (dialogueBodyText != null)
            dialogueBodyText.maxVisibleCharacters = int.MaxValue;

        isTyping = false;
        currentLinePendingCompletion = false;
        SetContinueIndicator(!HasChoices);

        Action callback = currentLineCompletedCallback;
        currentLineCompletedCallback = null;

        LineCompleted?.Invoke();
        callback?.Invoke();
    }

    private void CancelCurrentLine()
    {
        if (typewriterRoutine != null)
        {
            StopCoroutine(typewriterRoutine);
            typewriterRoutine = null;
        }

        isTyping = false;
        currentLinePendingCompletion = false;
        currentLineCompletedCallback = null;
    }

    private void SetPortrait(Sprite portrait)
    {
        bool hasPortrait = portrait != null;

        if (portraitImage != null)
        {
            portraitImage.sprite = portrait;
            portraitImage.enabled = hasPortrait;
        }

        if (portraitContainer != null)
            portraitContainer.SetActive(hasPortrait);
    }

    private void SetContinueIndicator(bool visible)
    {
        if (continueIndicator != null && continueIndicator.activeSelf != visible)
            continueIndicator.SetActive(visible);
    }

    private string BuildChoiceLabel(DialogueChoiceViewModel viewModel)
    {
        string prefix = string.Empty;
        if (!viewModel.Enabled)
            prefix += lockedChoicePrefix ?? string.Empty;

        if (viewModel.AlreadySeen && viewModel.Choice.showReadIndicator)
            prefix += seenChoicePrefix ?? string.Empty;

        return prefix + (viewModel.Choice.text ?? string.Empty);
    }

    private void ActivateChoice(RuntimeChoiceButton runtimeButton)
    {
        int index = runtimeChoiceButtons.IndexOf(runtimeButton);
        if (!IsChoiceEnabled(index))
            return;

        DialogueChoice choice = runtimeButton.ViewModel.Choice;
        Action<DialogueChoice> callback = choiceSelectedCallback;

        // Clear first so repeated submit/click events in the same frame cannot
        // select the same branch twice while the manager changes node.
        ClearChoices();
        callback?.Invoke(choice);
    }

    private void ConfigureVerticalNavigation()
    {
        var enabledIndices = new List<int>();
        for (int i = 0; i < runtimeChoiceButtons.Count; i++)
        {
            if (IsChoiceEnabled(i))
                enabledIndices.Add(i);
        }

        for (int i = 0; i < runtimeChoiceButtons.Count; i++)
        {
            Button button = runtimeChoiceButtons[i].Button;
            if (button == null)
                continue;

            Navigation navigation = button.navigation;
            if (!IsChoiceEnabled(i) || enabledIndices.Count == 0)
            {
                navigation.mode = Navigation.Mode.None;
                button.navigation = navigation;
                continue;
            }

            int enabledPosition = enabledIndices.IndexOf(i);
            int previousIndex = enabledIndices[WrapIndex(enabledPosition - 1, enabledIndices.Count)];
            int nextIndex = enabledIndices[WrapIndex(enabledPosition + 1, enabledIndices.Count)];

            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = runtimeChoiceButtons[previousIndex].Button;
            navigation.selectOnDown = runtimeChoiceButtons[nextIndex].Button;
            navigation.selectOnLeft = null;
            navigation.selectOnRight = null;
            button.navigation = navigation;
        }
    }

    private bool SelectFirstEnabledChoice()
    {
        for (int i = 0; i < runtimeChoiceButtons.Count; i++)
        {
            if (SelectChoiceAt(i))
                return true;
        }

        return false;
    }

    private bool SelectChoiceAt(int index)
    {
        if (!IsChoiceEnabled(index))
            return false;

        Button button = runtimeChoiceButtons[index].Button;
        selectedChoiceIndex = index;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(button.gameObject);

        button.Select();
        EnsureChoiceVisible(button);
        return true;
    }

    private void ResetChoiceScroll()
    {
        if (choicesScrollRect == null)
            return;

        RebuildChoiceLayout();
        choicesScrollRect.StopMovement();
        choicesScrollRect.verticalNormalizedPosition = 1f;
    }

    private void EnsureChoiceVisible(Button button)
    {
        if (choicesScrollRect == null || button == null)
            return;

        RectTransform viewport = choicesScrollRect.viewport;
        RectTransform content = choicesScrollRect.content;
        RectTransform buttonRect = button.transform as RectTransform;
        if (viewport == null || content == null || buttonRect == null)
            return;

        RebuildChoiceLayout();

        Bounds contentBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, content);
        float hiddenHeight = contentBounds.size.y - viewport.rect.height;
        if (hiddenHeight <= 0.01f)
            return;

        Bounds buttonBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, buttonRect);
        const float edgePadding = 2f;
        float normalizedPosition = choicesScrollRect.verticalNormalizedPosition;

        float overflowBelow = viewport.rect.yMin + edgePadding - buttonBounds.min.y;
        if (overflowBelow > 0f)
            normalizedPosition -= overflowBelow / hiddenHeight;
        else
        {
            float overflowAbove = buttonBounds.max.y - (viewport.rect.yMax - edgePadding);
            if (overflowAbove > 0f)
                normalizedPosition += overflowAbove / hiddenHeight;
        }

        choicesScrollRect.StopMovement();
        choicesScrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalizedPosition);
    }

    private void RebuildChoiceLayout()
    {
        Canvas.ForceUpdateCanvases();
        if (choicesRoot is RectTransform choicesRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(choicesRect);
    }

    private bool IsChoiceEnabled(int index)
    {
        if (index < 0 || index >= runtimeChoiceButtons.Count)
            return false;

        RuntimeChoiceButton runtimeButton = runtimeChoiceButtons[index];
        return runtimeButton != null
               && runtimeButton.ViewModel != null
               && runtimeButton.ViewModel.Enabled
               && runtimeButton.Button != null
               && runtimeButton.Button.interactable
               && runtimeButton.Button.gameObject.activeInHierarchy;
    }

    private void SynchronizeSelectionFromEventSystem()
    {
        if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null)
            return;

        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
        for (int i = 0; i < runtimeChoiceButtons.Count; i++)
        {
            Button button = runtimeChoiceButtons[i].Button;
            if (button != null && selectedObject == button.gameObject && IsChoiceEnabled(i))
            {
                selectedChoiceIndex = i;
                return;
            }
        }
    }

    private void ClearOwnedEventSystemSelection()
    {
        if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null)
            return;

        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
        for (int i = 0; i < runtimeChoiceButtons.Count; i++)
        {
            Button button = runtimeChoiceButtons[i].Button;
            if (button != null && selectedObject == button.gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
                return;
            }
        }
    }

    private void HideSceneChoiceTemplate()
    {
        if (choiceButtonPrefab == null || choicesRoot == null)
            return;

        Transform templateTransform = choiceButtonPrefab.transform;
        if (templateTransform == choicesRoot || templateTransform.IsChildOf(choicesRoot))
            choiceButtonPrefab.gameObject.SetActive(false);
    }

    private void WarnAboutMissingChoiceConfiguration()
    {
        if (warnedAboutChoiceConfiguration)
            return;

        warnedAboutChoiceConfiguration = true;
        Debug.LogWarning(
            "[DialogueUI] Choices cannot be shown: assign both Choices Root and Choice Button Prefab.",
            this);
    }

    private void WarnAboutMissingChoiceLabel()
    {
        if (warnedAboutChoiceLabel)
            return;

        warnedAboutChoiceLabel = true;
        Debug.LogWarning(
            "[DialogueUI] Choice Button Prefab has no TMP_Text child; the choice remains selectable but has no label.",
            this);
    }

    private static int WrapIndex(int value, int count)
    {
        if (count <= 0)
            return -1;

        int result = value % count;
        return result < 0 ? result + count : result;
    }
}
