using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private GameObject playerHudPanel;
    [SerializeField] private InventoryUIManager inventoryUIManager;
    [SerializeField] private MagicInventoryManager magicInventoryManager;
    [SerializeField] private EquipmentManager equipmentManager;
    [SerializeField] private AttributesUIManager attributesUIManager;
    [SerializeField] private QuestJournalUI questJournalUI;
    [SerializeField] private PlayerInventory scenePlayerInventory;

    [Header("Camera Input")]
    [SerializeField] private CinemachineInputProvider[] cameraInputProviders;
    [SerializeField] private CinemachineFreeLook[] cameraInputFallbacks;

    [Header("Book Animation")]
    [SerializeField] private Animator menuAnimator;
    [SerializeField] private string menuOpenStateName = "BookOpen";
    [SerializeField] private string menuPostOpenStateName = "";
    [SerializeField] private string menuPreCloseStateName = "DisappearTabs";
    [SerializeField] private string menuCloseStateName = "CloseBook";
    [SerializeField] private string menuFlipRightStateName = "FlipRightPage";
    [SerializeField] private string menuFlipLeftStateName = "FlipLeftPage";
    [SerializeField] [Min(0f)] private float menuOpenStartDelay = 0.5f;
    [SerializeField] [Min(0f)] private float menuPostOpenFallbackDuration = 0.25f;
    [SerializeField] [Min(0f)] private float menuPreCloseFallbackDuration = 0.25f;
    [SerializeField] [Min(0f)] private float menuCloseEndDelay = 0.5f;
    [SerializeField] [Min(0f)] private float menuPageFlipFallbackDuration = 0.25f;
    [SerializeField] [Min(0f)] private float menuCloseFallbackDuration = 0.25f;
    [SerializeField] [Min(1f)] private float multiPageFlipSpeedMultiplier = 1.25f;

    [Header("Content Animation")]
    [SerializeField] private Animator menuContentAnimator;
    [SerializeField] private string menuContentAppearStateName = "";
    [SerializeField] private string menuContentPreCloseStateName = "";
    [SerializeField] [Min(0f)] private float menuContentAppearFallbackDuration = 0.25f;

    [Header("Tabs")]
    [SerializeField] private MenuTabEntry[] tabs;
    [SerializeField] private Image tabBackgroundImage;
    [SerializeField] private string defaultOpenTabKey = "Equipment";
    [SerializeField] private bool openPauseMenuByDefault = true;
    [SerializeField] private string pauseMenuTabKey = "Pause";

    [Header("Pad Focus")]
    [SerializeField] private float padFocusLockDuration = 0.35f;
    [SerializeField] private float gamepadAxisDetectThreshold = 0.35f;
    [SerializeField] private float navigationRepeatCooldown = 0.20f;

    private int currentTabIndex = -1;
    private bool isMenuOpen;
    private bool showPadFocus;
    private float padFocusLockUntil;
    private float lastNavigationMoveTime = -999f;
    private PlayerInventory currentPlayerInventory;
    private Coroutine openMenuRoutine;
    private Coroutine closeMenuRoutine;
    private Coroutine pageFlipRoutine;
    private bool isMenuOpening;
    private bool isMenuClosing;
    private bool isMenuPageFlipping;
    private int pendingTabIndex = -1;
    private int openingTabIndex = -1;

    private const string AnimatorBaseLayerPrefix = "Base Layer.";

    public bool IsMenuOpen => isMenuOpen;
    public bool IsPadFocusVisible => showPadFocus;
    public string CurrentTabKey => IsValidTabIndex(currentTabIndex) ? tabs[currentTabIndex].key : string.Empty;
    private bool IsMenuTransitioning => isMenuOpening || isMenuClosing || isMenuPageFlipping;

    private void Awake()
    {
        ResolveReferences();
        ApplyPadFocusVisible(false);
        StopPendingOpenMenuRoutine();
        StopPendingCloseMenuRoutine();
        StopPendingPageFlipRoutine();
        isMenuOpening = false;
        isMenuClosing = false;
        isMenuPageFlipping = false;
        ResetMenuAnimatorPlayback();

        if (inventoryPanel != null)
        {
            SetInventoryPanelInteraction(false);
            SetInventorySlotInputEnabled(false);
            inventoryPanel.SetActive(false);
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public void SetPadFocusVisible(bool visible)
    {
        ApplyPadFocusVisible(visible);
    }

    public void ForcePadFocusMode(float lockDuration = -1f)
    {
        ApplyPadFocusVisible(true);
        float duration = lockDuration >= 0f ? lockDuration : padFocusLockDuration;
        padFocusLockUntil = Mathf.Max(padFocusLockUntil, Time.unscaledTime + Mathf.Max(0f, duration));
    }

    public bool ToggleMenu(PlayerControls controls, PlayerInventory playerInventory)
    {
        if (IsMenuTransitioning)
            return isMenuOpen;

        if (isMenuOpen)
            CloseMenu(controls, playerInventory);
        else
            OpenMenu(controls, playerInventory);

        return isMenuOpen;
    }

    public void ShowTab(string tabKey)
    {
        ResolveReferences();

        if (tabs == null || tabs.Length == 0)
        {
            return;
        }

        int targetIndex = ResolveTabIndexOrFallback(tabKey);
        if (!IsValidTabIndex(targetIndex))
            return;

        RequestShowTabAtIndex(targetIndex);
    }

    private void RequestShowTabAtIndex(int targetIndex)
    {
        if (!IsValidTabIndex(targetIndex) || IsMenuTransitioning)
            return;

        if (targetIndex == currentTabIndex || !isMenuOpen || !IsValidTabIndex(currentTabIndex))
        {
            ShowTabAtIndex(targetIndex);
            return;
        }

        ChangeTabAfterPageFlip(targetIndex);
    }

    private void ShowTabAtIndex(int targetIndex)
    {
        if (!IsValidTabIndex(targetIndex))
            return;

        currentTabIndex = targetIndex;
        string resolvedKey = tabs[currentTabIndex].key;

        for (int i = 0; i < tabs.Length; i++)
        {
            var tab = tabs[i];
            if (tab == null) continue;

            if (tab.background != null)
                tab.background.SetActive(i == currentTabIndex);
        }

        ApplyTabBackgroundSprite(tabs[currentTabIndex]);
        ApplyMenuTab(resolvedKey);

        if (IsQuestTabKey(resolvedKey) && questJournalUI != null)
        {
            questJournalUI.InitializeIfNeeded();
            questJournalUI.RefreshUI(showPadFocus);
            questJournalUI.FocusPadDefault(showPadFocus);
        }
    }

    private void ApplyMenuTab(string tabKey)
    {
        bool isInventoryTab = string.Equals(tabKey, "Inventory", System.StringComparison.OrdinalIgnoreCase);
        bool isEquipmentTab = string.Equals(tabKey, "Equipment", System.StringComparison.OrdinalIgnoreCase);
        bool isMagicTab = string.Equals(tabKey, "Magic", System.StringComparison.OrdinalIgnoreCase);
        bool isSkillTab = string.Equals(tabKey, "Skill", System.StringComparison.OrdinalIgnoreCase)
                          || string.Equals(tabKey, "Attributes", System.StringComparison.OrdinalIgnoreCase);

        if (isInventoryTab)
        {
            inventoryUIManager?.RefreshSourceItemsFromPlayer();
            inventoryUIManager?.ResetFilterToAll();
            inventoryUIManager?.FocusDefaultPadSlot();
        }

        if (isEquipmentTab)
        {
            inventoryUIManager?.RefreshSourceItemsFromPlayer();
            equipmentManager?.CloseEquipGrid();
            equipmentManager?.RefreshEquipmentCross();
            equipmentManager?.FocusEquipmentCrossDefault();
        }

        if (isMagicTab)
            magicInventoryManager?.ShowMagicTab();

        if (isSkillTab)
        {
            attributesUIManager?.Initialize();
            attributesUIManager?.BeginAllocationSession();
            attributesUIManager?.FocusPadDefault(padFocusLockDuration);
        }
        else
        {
            attributesUIManager?.CancelPendingAllocation();
        }
    }

    private void ApplyTabBackgroundSprite(MenuTabEntry tab)
    {
        if (tabBackgroundImage == null)
            return;

        Sprite sprite = tab != null ? tab.backgroundSprite : null;
        tabBackgroundImage.sprite = sprite;
        tabBackgroundImage.enabled = sprite != null;
    }

    public void RefreshEquipmentUI()
    {
        equipmentManager?.RefreshEquipmentCross();
        if (IsAttributesTabActive())
            attributesUIManager?.RefreshUI();
    }

    public void BindPlayerInventory(PlayerInventory playerInventory)
    {
        if (playerInventory == null)
            return;

        currentPlayerInventory = playerInventory;
        scenePlayerInventory = playerInventory;

        inventoryUIManager?.SetPlayerInventory(currentPlayerInventory);
        magicInventoryManager?.SetPlayerInventory(currentPlayerInventory);
        equipmentManager?.SetPlayerInventory(currentPlayerInventory);
        InitializeLinkedManagers();
        inventoryUIManager?.RefreshSourceItemsFromPlayer();
        RefreshEquipmentUI();
    }

    private bool IsAttributesTabActive()
    {
        string tabKey = CurrentTabKey;
        return string.Equals(tabKey, "Skill", System.StringComparison.OrdinalIgnoreCase)
               || string.Equals(tabKey, "Attributes", System.StringComparison.OrdinalIgnoreCase);
    }

    private bool IsMagicGridContextActive()
    {
        if (string.Equals(CurrentTabKey, "Magic", System.StringComparison.OrdinalIgnoreCase))
            return true;

        return equipmentManager != null
               && equipmentManager.CurrentEquipTarget == EquipmentManager.EquipTarget.Top
               && equipmentManager.HasEquipGridOpen();
    }

    private void HandleGridMoveHorizontal(int direction)
    {
        if (IsMagicGridContextActive())
            magicInventoryManager?.MovePadFocusHorizontal(direction);
        else
            inventoryUIManager?.MovePadFocusHorizontal(direction);
    }

    private void HandleGridMoveVertical(int direction)
    {
        if (IsMagicGridContextActive())
            magicInventoryManager?.MovePadFocusVertical(direction);
        else
            inventoryUIManager?.MovePadFocusVertical(direction);
    }

    private void ConfirmGridSelection()
    {
        if (IsMagicGridContextActive())
            magicInventoryManager?.ConfirmPadSelection();
        else
            inventoryUIManager?.ConfirmPadSelection();
    }

    private bool HandlePadBack()
    {
        bool equipGridOpen = equipmentManager != null && equipmentManager.HasEquipGridOpen();
        if (equipmentManager != null && equipmentManager.CurrentEquipTarget != EquipmentManager.EquipTarget.None && equipGridOpen)
        {
            equipmentManager.CloseEquipGrid();
            equipmentManager.FocusEquipmentCrossDefault();
            return true;
        }

        return false;
    }

    public void NextTab()
    {
        if (IsMenuTransitioning || tabs == null || tabs.Length == 0) return;

        int startIndex = IsValidTabIndex(currentTabIndex) ? currentTabIndex : FindFirstValidTabIndex();
        if (!IsValidTabIndex(startIndex)) return;

        for (int offset = 1; startIndex + offset < tabs.Length; offset++)
        {
            int next = startIndex + offset;
            if (!IsValidTabIndex(next)) continue;
            ChangeTabAfterPageFlip(next, menuFlipLeftStateName, offset);
            return;
        }
    }

    public void PreviousTab()
    {
        if (IsMenuTransitioning || tabs == null || tabs.Length == 0) return;

        int startIndex = IsValidTabIndex(currentTabIndex) ? currentTabIndex : FindFirstValidTabIndex();
        if (!IsValidTabIndex(startIndex)) return;

        for (int offset = 1; startIndex - offset >= 0; offset++)
        {
            int prev = startIndex - offset;
            if (!IsValidTabIndex(prev)) continue;
            ChangeTabAfterPageFlip(prev, menuFlipRightStateName, offset);
            return;
        }
    }

    private void ChangeTabAfterPageFlip(int targetIndex)
    {
        if (!IsValidTabIndex(targetIndex))
            return;

        if (!IsValidTabIndex(currentTabIndex) || targetIndex == currentTabIndex)
        {
            ShowTabAtIndex(targetIndex);
            return;
        }

        int signedDistance = targetIndex - currentTabIndex;
        string animationStateName = signedDistance > 0 ? menuFlipLeftStateName : menuFlipRightStateName;
        ChangeTabAfterPageFlip(targetIndex, animationStateName, Mathf.Abs(signedDistance));
    }

    private void ChangeTabAfterPageFlip(int targetIndex, string animationStateName, int flipCount = 1)
    {
        if (!IsValidTabIndex(targetIndex))
            return;

        if (StartPageFlipAnimation(animationStateName, targetIndex, flipCount))
            return;

        ShowTabAtIndex(targetIndex);
    }

    public void HandleMenuInput(PlayerControls controls)
    {
        if (!isMenuOpen || IsMenuTransitioning || controls == null)
            return;

        if (equipmentManager == null || inventoryUIManager == null || magicInventoryManager == null || attributesUIManager == null)
            return;

        UpdateFocusInputMode();

        if (controls.Player.TabNext.WasPerformedThisFrame())
            NextTab();
        if (controls.Player.TabPrev.WasPerformedThisFrame())
            PreviousTab();

        bool inQuestTab = IsQuestTabActive();

        if (controls.Player.SprintOrDodge.WasPerformedThisFrame())
        {
            bool consumed = false;
            if (inQuestTab && questJournalUI != null)
                consumed = questJournalUI.HandlePadBack(showPadFocus);
            else if (HandlePadBack())
                consumed = true;
            if (!consumed)
            {
                CloseMenu(controls, currentPlayerInventory);
            }
            return;
        }

        // Left/Right/Down del pad arrivano gia' dalle InputAction dedicate
        // (CycleRightEquip/CycleLeftEquip/CycleUsable). Evitiamo di leggerli
        // anche direttamente dal D-Pad per non processare due volte la stessa pressione.
        bool rightPressed = Keyboard.current != null && Keyboard.current.rightArrowKey.wasPressedThisFrame;
        bool leftPressed = Keyboard.current != null && Keyboard.current.leftArrowKey.wasPressedThisFrame;
        bool downPressed = (Keyboard.current != null && Keyboard.current.downArrowKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.dpad.down.wasPressedThisFrame);
        bool upPressed = (Keyboard.current != null && Keyboard.current.upArrowKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.dpad.up.wasPressedThisFrame);

        bool inEquipmentCross = equipmentManager.IsEquipmentCrossModeActive();
        bool inAttributesTab = IsAttributesTabActive();

        if (inAttributesTab)
        {
            if (!attributesUIManager.HasAttributePointsToSpend() && !attributesUIManager.HasPendingAllocation())
                return;

            ForcePadFocusMode();

            InputAction increaseAttributeAction = controls.asset.FindAction("Player/CycleRightEquip", throwIfNotFound: false);
            InputAction decreaseAttributeAction = controls.asset.FindAction("Player/CycleLeftEquip", throwIfNotFound: false);
            bool increasePressed = rightPressed || (increaseAttributeAction != null && increaseAttributeAction.WasPerformedThisFrame());
            bool decreasePressed = leftPressed || (decreaseAttributeAction != null && decreaseAttributeAction.WasPerformedThisFrame());

            if (increasePressed) attributesUIManager.IncreasePadSelection();
            if (decreasePressed) attributesUIManager.DecreasePadSelection();

            if (downPressed) attributesUIManager.MovePadFocusVertical(1);
            if (upPressed) attributesUIManager.MovePadFocusVertical(-1);

            Vector2 attrNav = controls.Player.Move.ReadValue<Vector2>();
            if (Time.time >= lastNavigationMoveTime + navigationRepeatCooldown)
            {
                if (attrNav.y > 0.5f && !upPressed)
                {
                    attributesUIManager.MovePadFocusVertical(-1);
                    lastNavigationMoveTime = Time.time;
                }
                else if (attrNav.y < -0.5f && !downPressed)
                {
                    attributesUIManager.MovePadFocusVertical(1);
                    lastNavigationMoveTime = Time.time;
                }
                else if (attrNav.x > 0.5f && !increasePressed)
                {
                    attributesUIManager.IncreasePadSelection();
                    lastNavigationMoveTime = Time.time;
                }
                else if (attrNav.x < -0.5f && !decreasePressed)
                {
                    attributesUIManager.DecreasePadSelection();
                    lastNavigationMoveTime = Time.time;
                }
            }

            if (controls.Player.Jump.WasPerformedThisFrame())
                attributesUIManager.ConfirmPadSelection();

            return;
        }

        if (inQuestTab)
        {
            if (rightPressed) questJournalUI?.MovePadFocusHorizontal(1, showPadFocus);
            if (leftPressed) questJournalUI?.MovePadFocusHorizontal(-1, showPadFocus);
            if (downPressed) questJournalUI?.MovePadFocusVertical(1, showPadFocus);
            if (upPressed) questJournalUI?.MovePadFocusVertical(-1, showPadFocus);

            Vector2 questNav = controls.Player.Move.ReadValue<Vector2>();
            if (Time.time >= lastNavigationMoveTime + navigationRepeatCooldown)
            {
                if (questNav.x > 0.5f)
                {
                    questJournalUI?.MovePadFocusHorizontal(1, showPadFocus);
                    lastNavigationMoveTime = Time.time;
                }
                else if (questNav.x < -0.5f)
                {
                    questJournalUI?.MovePadFocusHorizontal(-1, showPadFocus);
                    lastNavigationMoveTime = Time.time;
                }
                else if (questNav.y > 0.5f && !upPressed)
                {
                    questJournalUI?.MovePadFocusVertical(-1, showPadFocus);
                    lastNavigationMoveTime = Time.time;
                }
                else if (questNav.y < -0.5f && !downPressed)
                {
                    questJournalUI?.MovePadFocusVertical(1, showPadFocus);
                    lastNavigationMoveTime = Time.time;
                }
            }

            if (controls.Player.Jump.WasPerformedThisFrame())
                questJournalUI?.ConfirmPadSelection(showPadFocus);

            return;
        }

        if (IsPauseMenuTabActive())
            return;

        InputAction cycleRightEquipAction = controls.asset.FindAction("Player/CycleRightEquip", throwIfNotFound: false);
        InputAction cycleLeftEquipAction = controls.asset.FindAction("Player/CycleLeftEquip", throwIfNotFound: false);
        InputAction cycleUsableAction = controls.asset.FindAction("Player/CycleUsable", throwIfNotFound: false);

        if ((cycleRightEquipAction != null && cycleRightEquipAction.WasPerformedThisFrame()) || rightPressed)
        {
            if (inEquipmentCross) equipmentManager.NavigateEquipmentRight();
            else HandleGridMoveHorizontal(1);
        }
        if ((cycleLeftEquipAction != null && cycleLeftEquipAction.WasPerformedThisFrame()) || leftPressed)
        {
            if (inEquipmentCross) equipmentManager.NavigateEquipmentLeft();
            else HandleGridMoveHorizontal(-1);
        }
        if ((cycleUsableAction != null && cycleUsableAction.WasPerformedThisFrame()) || downPressed)
        {
            if (inEquipmentCross) equipmentManager.NavigateEquipmentDown();
            else HandleGridMoveVertical(1);
        }
        if (upPressed)
        {
            if (inEquipmentCross) equipmentManager.NavigateEquipmentUp();
            else HandleGridMoveVertical(-1);
        }

        Vector2 nav = controls.Player.Move.ReadValue<Vector2>();
        if (!inEquipmentCross && Time.time >= lastNavigationMoveTime + navigationRepeatCooldown)
        {
            if (nav.x > 0.5f)
            {
                HandleGridMoveHorizontal(1);
                lastNavigationMoveTime = Time.time;
            }
            else if (nav.x < -0.5f)
            {
                HandleGridMoveHorizontal(-1);
                lastNavigationMoveTime = Time.time;
            }
            else if (nav.y > 0.5f)
            {
                HandleGridMoveVertical(-1);
                lastNavigationMoveTime = Time.time;
            }
            else if (nav.y < -0.5f)
            {
                HandleGridMoveVertical(1);
                lastNavigationMoveTime = Time.time;
            }
        }

        if (controls.Player.Jump.WasPerformedThisFrame())
        {
            if (inEquipmentCross) equipmentManager.ConfirmEquipmentSelection();
            else ConfirmGridSelection();
        }
    }

    private void OpenMenu(PlayerControls controls, PlayerInventory playerInventory)
    {
        ResolveReferences();
        StopPendingCloseMenuRoutine();
        StopPendingOpenMenuRoutine();
        StopPendingPageFlipRoutine();
        isMenuOpening = false;
        isMenuClosing = false;
        isMenuPageFlipping = false;
        currentPlayerInventory = playerInventory != null ? playerInventory : scenePlayerInventory;
        isMenuOpen = true;
        InitializeLinkedManagers();

        openingTabIndex = ResolveTabIndexOrFallback(GetDefaultOpenTabKey());
        currentTabIndex = -1;
        HideContentAppearAnimation();

        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(true);
            SetInventoryPanelInteraction(true);
            SetInventorySlotInputEnabled(true);
            HideAllTabBackgrounds();
            StartOpenMenuAnimation();
        }
        if (playerHudPanel != null && ShouldHidePlayerHudPanelWhenMenuOpen())
            playerHudPanel.SetActive(false);

        ApplyPadFocusVisible(false);

        inventoryUIManager.ResetFilterToAll();
        inventoryUIManager.SetPlayerInventory(playerInventory);
        magicInventoryManager.SetPlayerInventory(playerInventory);
        equipmentManager.SetPlayerInventory(playerInventory);
        attributesUIManager.RefreshUI();
        if (playerInventory != null)
        {
            var list = new List<InventoryItem>(playerInventory.Items);
            inventoryUIManager.SetSourceItems(list);
            StartCoroutine(RefreshInventoryNextFrame(list, false));
        }

        inventoryUIManager.FocusDefaultPadSlot();
        lastNavigationMoveTime = Time.time;

        SetMenuCameraInputActive(false);
        if (controls != null)
            controls.Player.Look.Disable();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void CloseMenu(PlayerControls controls, PlayerInventory playerInventory)
    {
        ResolveReferences();
        if (IsMenuTransitioning)
            return;

        StopPendingOpenMenuRoutine();
        StopPendingPageFlipRoutine();
        isMenuOpening = false;
        isMenuPageFlipping = false;
        ResetMenuAnimatorPlayback();
        SetInventoryPanelInteraction(false);
        SetInventorySlotInputEnabled(false);
        CancelActiveInventoryDrag();
        attributesUIManager?.CancelPendingAllocation();
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        PlayerInventory inventoryToUse = playerInventory != null ? playerInventory : currentPlayerInventory;

        if (inventoryToUse != null)
        {
            var snapshot = inventoryUIManager != null ? inventoryUIManager.GetSourceItemsSnapshot() : null;
            if (snapshot != null)
            {
                bool canOverwrite = snapshot.Count > 0 || inventoryToUse.Items.Count == 0;
                if (canOverwrite)
                    inventoryToUse.ReplaceAllItems(snapshot);
            }
            inventoryUIManager.ResetFilterToAll();
            equipmentManager.RefreshEquipmentCross();
        }

        HideActiveDetailPanelsForMenuClose();
        ApplyPadFocusVisible(false);
        HideContentAppearAnimation();
        if (!StartCloseMenuAnimation(controls))
            CompleteMenuClose(controls);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveReferences();
        StopPendingOpenMenuRoutine();
        StopPendingCloseMenuRoutine();
        StopPendingPageFlipRoutine();
        isMenuOpening = false;
        isMenuClosing = false;
        isMenuPageFlipping = false;
        ResetMenuAnimatorPlayback();
        CancelActiveInventoryDrag();
        attributesUIManager?.CancelPendingAllocation();

        // Il menu non deve attraversare il cambio scena in stato aperto.
        isMenuOpen = false;
        currentTabIndex = -1;
        openingTabIndex = -1;
        currentPlayerInventory = scenePlayerInventory != null ? scenePlayerInventory : currentPlayerInventory;
        ApplyPadFocusVisible(false);
        HideMenuContentPanels();

        if (inventoryPanel != null)
        {
            SetInventoryPanelInteraction(false);
            SetInventorySlotInputEnabled(false);
            inventoryPanel.SetActive(false);
        }
        if (playerHudPanel != null)
            playerHudPanel.SetActive(true);

        SetMenuCameraInputActive(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (currentPlayerInventory != null)
        {
            inventoryUIManager?.SetPlayerInventory(currentPlayerInventory);
            magicInventoryManager?.SetPlayerInventory(currentPlayerInventory);
            equipmentManager?.SetPlayerInventory(currentPlayerInventory);
            inventoryUIManager?.RefreshSourceItemsFromPlayer();
            equipmentManager?.RefreshEquipmentCross();
        }

        attributesUIManager?.RefreshUI();
        questJournalUI?.RefreshUI(false);
    }

    private IEnumerator RefreshInventoryNextFrame(List<InventoryItem> snapshot, bool weaponsOnly = false)
    {
        yield return null;

        if (inventoryUIManager == null)
            yield break;

        inventoryUIManager.SetSourceItems(snapshot);
        if (weaponsOnly)
            inventoryUIManager.ShowWeaponsFilter();
        else
            inventoryUIManager.ResetFilterToAll();
    }

    private void ApplyPadFocusVisible(bool visible)
    {
        if (showPadFocus == visible)
            return;

        showPadFocus = visible;
        inventoryUIManager?.SetPadFocusVisible(showPadFocus);
        magicInventoryManager?.SetPadFocusVisible(showPadFocus);
        equipmentManager?.SetPadFocusVisible(showPadFocus);
        attributesUIManager?.SetPadFocusVisible(showPadFocus);
        if (questJournalUI != null)
            questJournalUI.SetPadFocusVisible(showPadFocus);
    }

    private void UpdateFocusInputMode()
    {
        bool gamepadUsed = DetectGamepadInputThisFrame();
        bool kbMouseUsed = DetectKeyboardMouseInputThisFrame();

        bool newState = showPadFocus;
        if (gamepadUsed)
        {
            newState = true;
            padFocusLockUntil = Time.unscaledTime + padFocusLockDuration;
        }
        if (kbMouseUsed && Time.unscaledTime >= padFocusLockUntil)
            newState = false;

        ApplyPadFocusVisible(newState);
    }

    private bool DetectGamepadInputThisFrame()
    {
        var gp = Gamepad.current;
        if (gp == null) return false;

        if (gp.buttonSouth.wasPressedThisFrame || gp.buttonNorth.wasPressedThisFrame ||
            gp.buttonEast.wasPressedThisFrame || gp.buttonWest.wasPressedThisFrame ||
            gp.leftShoulder.wasPressedThisFrame || gp.rightShoulder.wasPressedThisFrame ||
            gp.startButton.wasPressedThisFrame || gp.selectButton.wasPressedThisFrame ||
            gp.dpad.up.wasPressedThisFrame || gp.dpad.down.wasPressedThisFrame ||
            gp.dpad.left.wasPressedThisFrame || gp.dpad.right.wasPressedThisFrame)
            return true;

        float thresholdSqr = gamepadAxisDetectThreshold * gamepadAxisDetectThreshold;
        if (gp.leftStick.ReadValue().sqrMagnitude > thresholdSqr)
            return true;
        if (gp.rightStick.ReadValue().sqrMagnitude > thresholdSqr)
            return true;

        return false;
    }

    private bool DetectKeyboardMouseInputThisFrame()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.anyKey.wasPressedThisFrame)
            return true;

        var mouse = Mouse.current;
        if (mouse == null) return false;

        if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame)
            return true;
        if (mouse.delta.ReadValue().sqrMagnitude > 0.01f)
            return true;
        if (mouse.scroll.ReadValue().sqrMagnitude > 0.01f)
            return true;

        return false;
    }

    private void ResolveReferences()
    {
        if (currentPlayerInventory == null)
            currentPlayerInventory = scenePlayerInventory;

        InitializeLinkedManagers();
    }

    private void InitializeLinkedManagers()
    {
        equipmentManager?.Initialize(currentPlayerInventory, inventoryUIManager, magicInventoryManager);
        inventoryUIManager?.Initialize(currentPlayerInventory, equipmentManager);
        magicInventoryManager?.Initialize(currentPlayerInventory, equipmentManager);
        attributesUIManager?.Initialize();
    }

    private void SetMenuCameraInputActive(bool active)
    {
        if (cameraInputProviders != null)
        {
            for (int i = 0; i < cameraInputProviders.Length; i++)
            {
                if (cameraInputProviders[i] != null)
                    cameraInputProviders[i].enabled = active;
            }
        }

        if (cameraInputFallbacks == null)
            return;

        for (int i = 0; i < cameraInputFallbacks.Length; i++)
        {
            var cam = cameraInputFallbacks[i];
            if (cam == null)
                continue;

            if (active)
            {
                cam.m_XAxis.m_InputAxisName = "Mouse X";
                cam.m_YAxis.m_InputAxisName = "Mouse Y";
            }
            else
            {
                cam.m_XAxis.m_InputAxisName = "";
                cam.m_YAxis.m_InputAxisName = "";
            }
        }
    }

    private bool IsQuestTabActive()
    {
        return IsQuestTabKey(CurrentTabKey);
    }

    private bool IsPauseMenuTabActive()
    {
        return !string.IsNullOrWhiteSpace(pauseMenuTabKey)
               && string.Equals(CurrentTabKey, pauseMenuTabKey, System.StringComparison.OrdinalIgnoreCase);
    }

    private bool ShouldHidePlayerHudPanelWhenMenuOpen()
    {
        return playerHudPanel != null && playerHudPanel.name != "HUD_Canvas";
    }

    private static bool IsQuestTabKey(string tabKey)
    {
        return string.Equals(tabKey, "Quest", System.StringComparison.OrdinalIgnoreCase)
               || string.Equals(tabKey, "Quests", System.StringComparison.OrdinalIgnoreCase)
               || string.Equals(tabKey, "Journal", System.StringComparison.OrdinalIgnoreCase);
    }

    private int FindTabIndex(string tabKey)
    {
        if (tabs == null || tabs.Length == 0)
            return -1;

        for (int i = 0; i < tabs.Length; i++)
        {
            if (!IsValidTabIndex(i)) continue;
            if (string.Equals(tabs[i].key, tabKey, System.StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private int FindFirstValidTabIndex()
    {
        if (tabs == null) return -1;

        for (int i = 0; i < tabs.Length; i++)
        {
            if (IsValidTabIndex(i))
                return i;
        }

        return -1;
    }

    private int ResolveTabIndexOrFallback(string tabKey)
    {
        int targetIndex = FindTabIndex(tabKey);
        if (!IsValidTabIndex(targetIndex))
            targetIndex = FindFirstValidTabIndex();

        return targetIndex;
    }

    private void HideAllTabBackgrounds()
    {
        if (tabs == null)
            return;

        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i] == null || tabs[i].background == null)
                continue;

            ResetTabContentCanvasGroup(tabs[i].background);
            tabs[i].background.SetActive(false);
        }
    }

    private void HideCurrentTabBackground()
    {
        if (!IsValidTabIndex(currentTabIndex))
            return;

        GameObject background = tabs[currentTabIndex].background;
        if (background == null)
            return;

        ResetTabContentCanvasGroup(background);
        background.SetActive(false);
    }

    private void ResetTabContentCanvasGroup(GameObject tabBackground)
    {
        CanvasGroup contentGroup = tabBackground != null ? tabBackground.GetComponent<CanvasGroup>() : null;
        if (contentGroup == null)
            return;

        contentGroup.alpha = 1f;
        contentGroup.interactable = true;
        contentGroup.blocksRaycasts = true;
    }

    private void ApplyOpeningTab()
    {
        if (!IsValidTabIndex(openingTabIndex))
            openingTabIndex = ResolveTabIndexOrFallback(GetDefaultOpenTabKey());

        if (IsValidTabIndex(openingTabIndex))
            ShowTabAtIndex(openingTabIndex);

        openingTabIndex = -1;
    }

    private bool IsValidTabIndex(int index)
    {
        return tabs != null
               && index >= 0
               && index < tabs.Length
               && tabs[index] != null
               && !string.IsNullOrWhiteSpace(tabs[index].key);
    }

    private string GetDefaultOpenTabKey()
    {
        if (openPauseMenuByDefault && !string.IsNullOrWhiteSpace(pauseMenuTabKey) && FindTabIndex(pauseMenuTabKey) >= 0)
            return pauseMenuTabKey;

        return string.IsNullOrWhiteSpace(defaultOpenTabKey) ? "Equipment" : defaultOpenTabKey;
    }

    private bool StartCloseMenuAnimation(PlayerControls controls)
    {
        if (inventoryPanel == null || !inventoryPanel.activeInHierarchy)
            return false;

        isMenuClosing = true;
        closeMenuRoutine = StartCoroutine(RunCloseMenuAnimation(controls));
        return true;
    }

    private bool StartPageFlipAnimation(string stateName, int targetTabIndex, int flipCount)
    {
        StopPendingPageFlipRoutine();
        pendingTabIndex = -1;
        flipCount = Mathf.Max(1, flipCount);

        if (string.IsNullOrWhiteSpace(stateName))
        {
            isMenuPageFlipping = false;
            return false;
        }

        string animationStateName = ResolveCurrentTabPageFlipStateName(stateName);
        float playbackSpeed = GetPageFlipPlaybackSpeed(flipCount);
        bool hasAnimationState = HasMenuAnimationState(animationStateName);
        if (!hasAnimationState)
        {
            Sprite[] manualFlipSprites = GetCurrentTabPageFlipSprites(stateName);
            if (tabBackgroundImage != null && HasPageFlipSprites(manualFlipSprites))
            {
                float manualFlipDuration = GetMenuAnimationDuration(stateName, menuPageFlipFallbackDuration) / playbackSpeed;
                if (manualFlipDuration > 0f)
                {
                    HideAllTabBackgrounds();
                    HideContentAppearAnimation();
                    DisableMenuAnimator();
                    tabBackgroundImage.enabled = true;
                    pendingTabIndex = targetTabIndex;
                    isMenuPageFlipping = true;
                    pageFlipRoutine = StartCoroutine(CompleteManualPageFlipAfterDelay(manualFlipSprites, manualFlipDuration, flipCount));
                    return true;
                }
            }
        }

        if (!PlayMenuAnimationState(animationStateName))
        {
            ResetMenuAnimatorPlayback();
            isMenuPageFlipping = false;
            return false;
        }

        SetMenuAnimatorPlaybackSpeed(playbackSpeed);

        float baseFlipDuration = GetMenuAnimationDuration(animationStateName, menuPageFlipFallbackDuration);
        float flipDuration = baseFlipDuration / playbackSpeed;
        if (flipDuration <= 0f)
        {
            ResetMenuAnimatorPlayback();
            isMenuPageFlipping = false;
            return false;
        }

        HideAllTabBackgrounds();
        HideContentAppearAnimation();
        pendingTabIndex = targetTabIndex;
        isMenuPageFlipping = true;
        pageFlipRoutine = StartCoroutine(CompletePageFlipAfterDelay(animationStateName, flipDuration, flipCount));
        return true;
    }

    private string ResolveCurrentTabPageFlipStateName(string baseStateName)
    {
        if (!IsValidTabIndex(currentTabIndex))
            return baseStateName;

        string tabAnimationKey = GetPageFlipAnimationKey(tabs[currentTabIndex].key);
        if (string.IsNullOrWhiteSpace(tabAnimationKey))
            return baseStateName;

        string tabStateName = baseStateName + "_" + tabAnimationKey;
        return HasMenuAnimationState(tabStateName) ? tabStateName : baseStateName;
    }

    private bool HasMenuAnimationState(string stateName)
    {
        return TryGetAnimatorStateHash(menuAnimator, stateName, out _);
    }

    private Sprite[] GetCurrentTabPageFlipSprites(string stateName)
    {
        if (!IsValidTabIndex(currentTabIndex))
            return null;

        MenuTabEntry tab = tabs[currentTabIndex];
        return IsFlipLeftAnimationState(stateName) ? tab.flipLeftSprites : tab.flipRightSprites;
    }

    private bool IsFlipLeftAnimationState(string stateName)
    {
        return string.Equals(stateName, menuFlipLeftStateName, System.StringComparison.Ordinal)
               || string.Equals(stateName, "FlipLeftPage", System.StringComparison.Ordinal)
               || (!string.IsNullOrWhiteSpace(stateName)
                   && stateName.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static string GetPageFlipAnimationKey(string tabKey)
    {
        if (string.Equals(tabKey, "Maps", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(tabKey, "Map", System.StringComparison.OrdinalIgnoreCase))
            return "Map";

        if (string.Equals(tabKey, "Equipment", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(tabKey, "Equip", System.StringComparison.OrdinalIgnoreCase))
            return "Equip";

        if (string.Equals(tabKey, "Inventory", System.StringComparison.OrdinalIgnoreCase))
            return "Inventory";

        if (string.Equals(tabKey, "Magic", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(tabKey, "MagicInventory", System.StringComparison.OrdinalIgnoreCase))
            return "MagicInventory";

        if (string.Equals(tabKey, "Attributes", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(tabKey, "Skill", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(tabKey, "Stats", System.StringComparison.OrdinalIgnoreCase))
            return "Stats";

        if (string.Equals(tabKey, "Journal", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(tabKey, "Quest", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(tabKey, "Quests", System.StringComparison.OrdinalIgnoreCase))
            return "Quest";

        if (string.Equals(tabKey, "Setting", System.StringComparison.OrdinalIgnoreCase))
            return "Setting";

        return tabKey;
    }

    private static bool HasPageFlipSprites(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
            return false;

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
                return true;
        }

        return false;
    }

    private void StartOpenMenuAnimation()
    {
        StopPendingOpenMenuRoutine();
        ResetMenuAnimatorPlayback();

        if (!PlayMenuAnimationState(menuOpenStateName))
        {
            ApplyOpeningTab();
            isMenuOpening = false;
            return;
        }

        float delay = Mathf.Max(0f, menuOpenStartDelay);
        float openAnimationDuration = Mathf.Max(0f, GetMenuAnimationDuration(menuOpenStateName, 0f));
        if (delay <= 0f && openAnimationDuration <= 0f && !HasPostOpenAnimation() && !HasContentAppearAnimation())
        {
            ApplyOpeningTab();
            HoldMenuAnimatorOnCurrentTabSprite();
            isMenuOpening = false;
            return;
        }

        isMenuOpening = true;
        openMenuRoutine = StartCoroutine(RunOpenMenuAnimation(delay, openAnimationDuration));
    }

    private IEnumerator RunOpenMenuAnimation(float delay, float openAnimationDuration)
    {
        if (delay > 0f)
        {
            SetMenuAnimatorPlaybackSpeed(0f);
            yield return new WaitForSecondsRealtime(delay);
        }

        if (!isMenuOpen || isMenuClosing)
        {
            openMenuRoutine = null;
            isMenuOpening = false;
            yield break;
        }

        SetMenuAnimatorPlaybackSpeed(1f);

        if (openAnimationDuration > 0f)
            yield return new WaitForSecondsRealtime(openAnimationDuration);

        if (!isMenuOpen || isMenuClosing)
        {
            openMenuRoutine = null;
            isMenuOpening = false;
            yield break;
        }

        float postOpenAnimationDuration = PlayPostOpenAnimation();
        if (postOpenAnimationDuration > 0f)
            yield return new WaitForSecondsRealtime(postOpenAnimationDuration);

        ApplyOpeningTab();
        CanvasGroup openingContentGroup = PrepareCurrentTabContentForAppear();
        HoldMenuAnimatorOnCurrentTabSprite();

        float contentAppearAnimationDuration = PlayContentAppearAnimation();
        if (contentAppearAnimationDuration > 0f)
        {
            RevealTabContent(openingContentGroup);
            yield return new WaitForSecondsRealtime(contentAppearAnimationDuration);
            HideContentAppearAnimation();
        }
        else
        {
            HideContentAppearAnimation();
            yield return FadeCurrentTabContentIn(menuContentAppearFallbackDuration);
        }

        openMenuRoutine = null;
        if (!isMenuOpen || isMenuClosing)
        {
            isMenuOpening = false;
            yield break;
        }

        isMenuOpening = false;
    }

    private bool HasPostOpenAnimation()
    {
        return !string.IsNullOrWhiteSpace(menuPostOpenStateName);
    }

    private bool HasPreCloseAnimation()
    {
        return !string.IsNullOrWhiteSpace(menuPreCloseStateName);
    }

    private bool HasContentAppearAnimation()
    {
        return !string.IsNullOrWhiteSpace(menuContentAppearStateName);
    }

    private bool HasContentPreCloseAnimation()
    {
        return !string.IsNullOrWhiteSpace(menuContentPreCloseStateName);
    }

    private float PlayPostOpenAnimation()
    {
        if (!HasPostOpenAnimation())
            return 0f;

        if (!PlayMenuAnimationState(menuPostOpenStateName))
            return 0f;

        return GetMenuAnimationDuration(menuPostOpenStateName, menuPostOpenFallbackDuration);
    }

    private float PlayPreCloseAnimation()
    {
        if (!HasPreCloseAnimation())
            return 0f;

        if (!PlayMenuAnimationState(menuPreCloseStateName))
            return 0f;

        return GetMenuAnimationDuration(menuPreCloseStateName, menuPreCloseFallbackDuration);
    }

    private float PlayContentAppearAnimation()
    {
        if (!HasContentAppearAnimation())
            return 0f;

        PrepareContentAppearAnimationObject();

        if (!PlayAnimatorState(menuContentAnimator, menuContentAppearStateName))
        {
            HideContentAppearAnimation();
            return 0f;
        }

        return GetAnimatorAnimationDuration(menuContentAnimator, menuContentAppearStateName, menuContentAppearFallbackDuration);
    }

    private IEnumerator RunContentPreCloseAnimation()
    {
        if (!HasContentPreCloseAnimation())
            yield break;

        PrepareContentAppearAnimationObject();

        if (!TryGetAnimatorStateHash(menuContentAnimator, menuContentPreCloseStateName, out int stateHash))
        {
            HideContentAppearAnimation();
            yield break;
        }

        float duration = GetAnimatorAnimationDuration(menuContentAnimator, menuContentPreCloseStateName, menuContentAppearFallbackDuration);
        if (duration <= 0f)
        {
            menuContentAnimator.Play(stateHash, 0, 0f);
            menuContentAnimator.Update(0f);
            HideContentAppearAnimation();
            yield break;
        }

        menuContentAnimator.enabled = true;
        menuContentAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        menuContentAnimator.speed = 0f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float normalizedTime = 1f - Mathf.Clamp01(elapsed / duration);
            menuContentAnimator.Play(stateHash, 0, normalizedTime);
            menuContentAnimator.Update(0f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        menuContentAnimator.Play(stateHash, 0, 0f);
        menuContentAnimator.Update(0f);
        HideContentAppearAnimation();
    }

    private void PrepareContentAppearAnimationObject()
    {
        if (menuContentAnimator == null)
            return;

        if (!menuContentAnimator.gameObject.activeSelf)
            menuContentAnimator.gameObject.SetActive(true);

        menuContentAnimator.transform.SetAsLastSibling();

        Image contentAppearImage = menuContentAnimator.GetComponent<Image>();
        if (contentAppearImage == null)
            return;

        contentAppearImage.enabled = true;
        contentAppearImage.raycastTarget = false;

        Color color = contentAppearImage.color;
        color.a = 1f;
        contentAppearImage.color = color;
    }

    private void HideContentAppearAnimation()
    {
        if (menuContentAnimator == null)
            return;

        Image contentAppearImage = menuContentAnimator.GetComponent<Image>();
        if (contentAppearImage != null)
            contentAppearImage.enabled = false;

        menuContentAnimator.speed = 1f;
        menuContentAnimator.enabled = false;
    }

    private CanvasGroup PrepareCurrentTabContentForAppear()
    {
        CanvasGroup contentGroup = GetCurrentTabContentCanvasGroup();
        if (contentGroup == null)
            return null;

        contentGroup.alpha = 0f;
        contentGroup.interactable = false;
        contentGroup.blocksRaycasts = false;
        return contentGroup;
    }

    private static void RevealTabContent(CanvasGroup contentGroup)
    {
        if (contentGroup == null)
            return;

        contentGroup.alpha = 1f;
        contentGroup.interactable = true;
        contentGroup.blocksRaycasts = true;
    }

    private IEnumerator FadeCurrentTabContentIn(float duration)
    {
        CanvasGroup contentGroup = PrepareCurrentTabContentForAppear();
        if (contentGroup == null)
            yield break;

        duration = Mathf.Max(0f, duration);

        if (duration <= 0f)
        {
            RevealTabContent(contentGroup);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!isMenuOpen || isMenuClosing)
                yield break;

            elapsed += Time.unscaledDeltaTime;
            contentGroup.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        RevealTabContent(contentGroup);
    }

    private CanvasGroup GetCurrentTabContentCanvasGroup()
    {
        if (!IsValidTabIndex(currentTabIndex) || tabs[currentTabIndex].background == null)
            return null;

        CanvasGroup contentGroup = tabs[currentTabIndex].background.GetComponent<CanvasGroup>();
        if (contentGroup == null)
            contentGroup = tabs[currentTabIndex].background.AddComponent<CanvasGroup>();

        return contentGroup;
    }

    private IEnumerator CompletePageFlipAfterDelay(string stateName, float flipDuration, int flipCount)
    {
        for (int playedFlips = 1; playedFlips <= flipCount; playedFlips++)
        {
            yield return new WaitForSecondsRealtime(flipDuration);

            if (playedFlips >= flipCount)
                break;

            if (!isMenuOpen || isMenuClosing)
            {
                ApplyCurrentTabBackgroundSprite();
                pendingTabIndex = -1;
                pageFlipRoutine = null;
                isMenuPageFlipping = false;
                ResetMenuAnimatorPlayback();
                yield break;
            }

            if (!PlayMenuAnimationState(stateName))
                break;

            SetMenuAnimatorPlaybackSpeed(GetPageFlipPlaybackSpeed(flipCount));
        }

        ResetMenuAnimatorPlayback();
        DisableMenuAnimator();
        if (IsValidTabIndex(pendingTabIndex))
            ShowTabAtIndex(pendingTabIndex);
        else
            ApplyCurrentTabBackgroundSprite();

        pendingTabIndex = -1;
        pageFlipRoutine = null;
        isMenuPageFlipping = false;
    }

    private IEnumerator CompleteManualPageFlipAfterDelay(Sprite[] flipSprites, float flipDuration, int flipCount)
    {
        for (int playedFlips = 1; playedFlips <= flipCount; playedFlips++)
        {
            yield return PlayManualPageFlipFrames(flipSprites, flipDuration);

            if (playedFlips >= flipCount)
                break;

            if (!isMenuOpen || isMenuClosing)
            {
                ApplyCurrentTabBackgroundSprite();
                pendingTabIndex = -1;
                pageFlipRoutine = null;
                isMenuPageFlipping = false;
                yield break;
            }
        }

        DisableMenuAnimator();
        if (IsValidTabIndex(pendingTabIndex))
            ShowTabAtIndex(pendingTabIndex);
        else
            ApplyCurrentTabBackgroundSprite();

        pendingTabIndex = -1;
        pageFlipRoutine = null;
        isMenuPageFlipping = false;
    }

    private IEnumerator PlayManualPageFlipFrames(Sprite[] flipSprites, float flipDuration)
    {
        if (tabBackgroundImage == null || !HasPageFlipSprites(flipSprites))
        {
            yield return new WaitForSecondsRealtime(flipDuration);
            yield break;
        }

        flipDuration = Mathf.Max(0f, flipDuration);
        float frameDuration = flipSprites.Length > 0 ? flipDuration / flipSprites.Length : flipDuration;

        for (int i = 0; i < flipSprites.Length; i++)
        {
            Sprite sprite = flipSprites[i];
            if (sprite != null)
            {
                tabBackgroundImage.sprite = sprite;
                tabBackgroundImage.enabled = true;
            }

            if (frameDuration > 0f)
                yield return new WaitForSecondsRealtime(frameDuration);
        }
    }

    private IEnumerator RunCloseMenuAnimation(PlayerControls controls)
    {
        yield return RunContentPreCloseAnimation();
        HideMenuContentPanels();
        HideCurrentTabBackground();

        float preCloseAnimationDuration = PlayPreCloseAnimation();
        if (preCloseAnimationDuration > 0f)
            yield return new WaitForSecondsRealtime(preCloseAnimationDuration);

        if (inventoryPanel == null || !inventoryPanel.activeInHierarchy)
        {
            closeMenuRoutine = null;
            CompleteMenuClose(controls);
            yield break;
        }

        if (!PlayMenuAnimationState(menuCloseStateName))
        {
            yield return FadeMenuPanelOut();
            closeMenuRoutine = null;
            CompleteMenuClose(controls);
            yield break;
        }

        float closeAnimationDuration = GetMenuAnimationDuration(menuCloseStateName, menuCloseFallbackDuration);
        float endDelay = Mathf.Max(0f, menuCloseEndDelay);
        if (closeAnimationDuration > 0f || endDelay > 0f)
            yield return new WaitForSecondsRealtime(closeAnimationDuration + endDelay);

        yield return FadeMenuPanelOut();

        closeMenuRoutine = null;
        CompleteMenuClose(controls);
    }

    private IEnumerator FadeMenuPanelOut()
    {
        if (inventoryPanel == null)
            yield break;

        CanvasGroupFadeInOnEnable fade = inventoryPanel.GetComponentInChildren<CanvasGroupFadeInOnEnable>();
        if (fade != null && fade.isActiveAndEnabled)
            yield return fade.FadeOut();
    }

    private void CompleteMenuClose(PlayerControls controls)
    {
        StopPendingOpenMenuRoutine();
        StopPendingCloseMenuRoutine();
        StopPendingPageFlipRoutine();
        isMenuOpening = false;
        isMenuClosing = false;
        isMenuPageFlipping = false;
        isMenuOpen = false;
        openingTabIndex = -1;
        ResetMenuAnimatorPlayback();
        CancelActiveInventoryDrag();
        ApplyPadFocusVisible(false);
        HideMenuContentPanels();

        if (inventoryPanel != null)
        {
            SetInventoryPanelInteraction(false);
            SetInventorySlotInputEnabled(false);
            inventoryPanel.SetActive(false);
        }
        if (playerHudPanel != null)
            playerHudPanel.SetActive(true);

        SetMenuCameraInputActive(true);
        if (controls != null)
            controls.Player.Look.Enable();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void CancelActiveInventoryDrag()
    {
        inventoryUIManager?.CancelActiveDrag();
        magicInventoryManager?.CancelActiveDrag();
    }

    private void HideActiveDetailPanelsForMenuClose()
    {
        inventoryUIManager?.HideActiveDetailForMenuClose();
        magicInventoryManager?.HideActiveDetailForMenuClose();
        questJournalUI?.HideActiveDetailForMenuClose();
    }

    private void HideMenuContentPanels()
    {
        equipmentManager?.HideMenuContentPanels();
    }

    private void SetInventorySlotInputEnabled(bool enabled)
    {
        inventoryUIManager?.SetSlotInputEnabled(enabled);
        magicInventoryManager?.SetSlotInputEnabled(enabled);
    }

    private void SetInventoryPanelInteraction(bool enabled)
    {
        if (inventoryPanel == null)
            return;

        CanvasGroup canvasGroup = inventoryPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = inventoryPanel.AddComponent<CanvasGroup>();

        canvasGroup.interactable = enabled;
        canvasGroup.blocksRaycasts = enabled;
    }

    private void StopPendingCloseMenuRoutine()
    {
        if (closeMenuRoutine == null)
            return;

        StopCoroutine(closeMenuRoutine);
        closeMenuRoutine = null;
    }

    private void StopPendingPageFlipRoutine()
    {
        if (pageFlipRoutine == null)
            return;

        StopCoroutine(pageFlipRoutine);
        pageFlipRoutine = null;
        pendingTabIndex = -1;
        ResetMenuAnimatorPlayback();
    }

    private void StopPendingOpenMenuRoutine()
    {
        if (openMenuRoutine == null)
            return;

        StopCoroutine(openMenuRoutine);
        openMenuRoutine = null;
    }

    private bool PlayMenuAnimationState(string stateName)
    {
        if (tabBackgroundImage != null)
            tabBackgroundImage.enabled = true;

        return PlayAnimatorState(menuAnimator, stateName);
    }

    private bool PlayAnimatorState(Animator animator, string stateName)
    {
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(stateName))
            return false;

        animator.enabled = true;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animator.speed = 1f;

        if (!TryGetAnimatorStateHash(animator, stateName, out int stateHash))
            return false;

        animator.Play(stateHash, 0, 0f);
        animator.Update(0f);
        return true;
    }

    private float GetMenuAnimationDuration(string clipName, float fallbackDuration)
    {
        return GetAnimatorAnimationDuration(menuAnimator, clipName, fallbackDuration);
    }

    private float GetAnimatorAnimationDuration(Animator animator, string clipName, float fallbackDuration)
    {
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(clipName))
            return Mathf.Max(0f, fallbackDuration);

        var clips = animator.runtimeAnimatorController.animationClips;
        string[] candidates = BuildAnimatorStateCandidates(clipName);
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null)
                continue;

            for (int j = 0; j < candidates.Length; j++)
            {
                if (clip.name == candidates[j])
                    return clip.length;
            }
        }

        return Mathf.Max(0f, fallbackDuration);
    }

    private static bool TryGetAnimatorStateHash(Animator animator, string stateName, out int stateHash)
    {
        stateHash = 0;
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(stateName))
            return false;

        string[] candidates = BuildAnimatorStateCandidates(stateName);
        for (int i = 0; i < candidates.Length; i++)
        {
            string candidate = candidates[i];
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            int candidateHash = Animator.StringToHash(GetAnimatorStatePath(candidate));
            if (!animator.HasState(0, candidateHash))
                continue;

            stateHash = candidateHash;
            return true;
        }

        return false;
    }

    private static string GetAnimatorStatePath(string stateName)
    {
        if (string.IsNullOrWhiteSpace(stateName))
            return string.Empty;

        return stateName.Contains(".") ? stateName : AnimatorBaseLayerPrefix + stateName;
    }

    private void ResetMenuAnimatorPlayback()
    {
        SetMenuAnimatorPlaybackSpeed(1f);
    }

    private void HoldMenuAnimatorOnCurrentTabSprite()
    {
        DisableMenuAnimator();
        ApplyCurrentTabBackgroundSprite();
    }

    private void DisableMenuAnimator()
    {
        if (menuAnimator != null)
            menuAnimator.enabled = false;
    }

    private void ApplyCurrentTabBackgroundSprite()
    {
        if (IsValidTabIndex(currentTabIndex))
            ApplyTabBackgroundSprite(tabs[currentTabIndex]);
    }

    private void SetMenuAnimatorPlaybackSpeed(float speed)
    {
        if (menuAnimator != null)
            menuAnimator.speed = speed;
    }

    private float GetPageFlipPlaybackSpeed(int flipCount)
    {
        return flipCount > 1 ? Mathf.Max(1f, multiPageFlipSpeedMultiplier) : 1f;
    }

    private static string[] BuildAnimatorStateCandidates(string primaryName)
    {
        if (string.IsNullOrWhiteSpace(primaryName))
            return System.Array.Empty<string>();

        if (string.Equals(primaryName, "BookClose", System.StringComparison.Ordinal))
            return new[] { "BookClose", "CloseBook" };

        if (string.Equals(primaryName, "CloseBook", System.StringComparison.Ordinal))
            return new[] { "CloseBook", "BookClose" };

        if (string.Equals(primaryName, "BookOpen", System.StringComparison.Ordinal))
            return new[] { "BookOpen", "OpenBook" };

        if (string.Equals(primaryName, "OpenBook", System.StringComparison.Ordinal))
            return new[] { "OpenBook", "BookOpen" };

        return new[] { primaryName };
    }
}
