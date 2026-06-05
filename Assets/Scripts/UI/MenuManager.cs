using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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

    [Header("Book Animation")]
    [SerializeField] private Animator menuAnimator;
    [SerializeField] private string menuOpenStateName = "BookOpen";
    [SerializeField] private string menuPostOpenStateName = "";
    [SerializeField] private string menuCloseStateName = "CloseBook";
    [SerializeField] private string menuFlipRightStateName = "FlipRightPage";
    [SerializeField] private string menuFlipLeftStateName = "FlipLeftPage";
    [SerializeField] [Min(0f)] private float menuOpenStartDelay = 0.5f;
    [SerializeField] [Min(0f)] private float menuPostOpenFallbackDuration = 0.25f;
    [SerializeField] [Min(0f)] private float menuCloseEndDelay = 0.5f;
    [SerializeField] [Min(0f)] private float menuPageFlipFallbackDuration = 0.25f;
    [SerializeField] [Min(0f)] private float menuCloseFallbackDuration = 0.25f;
    [SerializeField] [Min(1f)] private float multiPageFlipSpeedMultiplier = 1.25f;

    [Header("Content Animation")]
    [SerializeField] private Animator menuContentAnimator;
    [SerializeField] private string menuContentAppearStateName = "";
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
            inventoryPanel.SetActive(false);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public MenuTabEntry[] GetTabs()
    {
        return tabs;
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

        int targetIndex = FindTabIndex(tabKey);
        if (!IsValidTabIndex(targetIndex))
            targetIndex = FindFirstValidTabIndex();
        if (!IsValidTabIndex(targetIndex))
            return;

        RequestShowTabAtIndex(targetIndex);
    }

    public void ShowTabByIndex(int targetIndex)
    {
        ResolveReferences();

        if (!IsValidTabIndex(targetIndex))
            return;

        RequestShowTabAtIndex(targetIndex);
    }

    private void RequestShowTabAtIndex(int targetIndex)
    {
        if (!IsValidTabIndex(targetIndex) || isMenuClosing || isMenuPageFlipping)
            return;

        if (targetIndex == currentTabIndex || !isMenuOpen || isMenuOpening || !IsValidTabIndex(currentTabIndex))
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
            inventoryUIManager?.RefreshWalletUI();
            inventoryUIManager?.ResetFilterToAll();
            inventoryUIManager?.FocusDefaultPadSlot();
        }

        if (isEquipmentTab)
        {
            inventoryUIManager?.RefreshSourceItemsFromPlayer();
            equipmentManager?.CloseEquipGrid();
            equipmentManager?.FocusEquipmentCrossDefault();
        }

        if (isMagicTab)
            magicInventoryManager?.ShowMagicTab();

        if (isSkillTab)
        {
            attributesUIManager?.Initialize();
            attributesUIManager?.RefreshUI();
            attributesUIManager?.FocusPadDefault(padFocusLockDuration);
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

        for (int offset = 1; offset <= tabs.Length; offset++)
        {
            int next = (startIndex + offset) % tabs.Length;
            if (!IsValidTabIndex(next)) continue;
            ChangeTabAfterPageFlip(next, menuFlipRightStateName, offset);
            return;
        }
    }

    public void PreviousTab()
    {
        if (IsMenuTransitioning || tabs == null || tabs.Length == 0) return;

        int startIndex = IsValidTabIndex(currentTabIndex) ? currentTabIndex : FindFirstValidTabIndex();
        if (!IsValidTabIndex(startIndex)) return;

        for (int offset = 1; offset <= tabs.Length; offset++)
        {
            int prev = (startIndex - offset + tabs.Length) % tabs.Length;
            if (!IsValidTabIndex(prev)) continue;
            ChangeTabAfterPageFlip(prev, menuFlipLeftStateName, offset);
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
        string animationStateName = signedDistance > 0 ? menuFlipRightStateName : menuFlipLeftStateName;
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

        ResolveReferences();
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
        bool downPressed = Keyboard.current != null && Keyboard.current.downArrowKey.wasPressedThisFrame;
        bool upPressed = (Keyboard.current != null && Keyboard.current.upArrowKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.dpad.up.wasPressedThisFrame);

        bool inEquipmentCross = equipmentManager.IsEquipmentCrossModeActive();
        bool inAttributesTab = IsAttributesTabActive();

        if (inAttributesTab)
        {
            if (!attributesUIManager.HasAttributePointsToSpend())
                return;

            ForcePadFocusMode();

            if (downPressed) attributesUIManager.MovePadFocusVertical(1);
            if (upPressed) attributesUIManager.MovePadFocusVertical(-1);

            Vector2 attrNav = controls.Player.Move.ReadValue<Vector2>();
            if (Time.time >= lastNavigationMoveTime + navigationRepeatCooldown)
            {
                if (attrNav.y > 0.5f)
                {
                    attributesUIManager.MovePadFocusVertical(-1);
                    lastNavigationMoveTime = Time.time;
                }
                else if (attrNav.y < -0.5f)
                {
                    attributesUIManager.MovePadFocusVertical(1);
                    lastNavigationMoveTime = Time.time;
                }
            }

            if (controls.Player.Jump.WasPerformedThisFrame())
                attributesUIManager.ConfirmPadSelection();

            return;
        }

        if (inQuestTab)
        {
            bool trianglePressed = controls.Player.Interact.WasPerformedThisFrame()
                || (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame);
            if (trianglePressed)
            {
                ForcePadFocusMode();
                questJournalUI?.FocusPadFilters(showPadFocus);
                lastNavigationMoveTime = Time.time;
                return;
            }

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
                else if (questNav.y > 0.5f)
                {
                    questJournalUI?.MovePadFocusVertical(-1, showPadFocus);
                    lastNavigationMoveTime = Time.time;
                }
                else if (questNav.y < -0.5f)
                {
                    questJournalUI?.MovePadFocusVertical(1, showPadFocus);
                    lastNavigationMoveTime = Time.time;
                }
            }

            if (controls.Player.Jump.WasPerformedThisFrame())
                questJournalUI?.ConfirmPadSelection(showPadFocus);

            if (Gamepad.current != null)
            {
                Vector2 rightStick = Gamepad.current.rightStick.ReadValue();
                questJournalUI?.ScrollDetailByPad(rightStick.y, Time.unscaledDeltaTime, true);
            }

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
        currentPlayerInventory = playerInventory;
        isMenuOpen = true;

        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(true);
            StartOpenMenuAnimation();
        }
        if (playerHudPanel != null)
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

        ShowTab(GetDefaultOpenTabKey());
        inventoryUIManager.FocusDefaultPadSlot();
        lastNavigationMoveTime = Time.time;

        CameraInputBlocker.SetAllCinemachineInput(false);
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

        ApplyPadFocusVisible(false);
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

        // Il menu non deve attraversare il cambio scena in stato aperto.
        isMenuOpen = false;
        currentTabIndex = -1;
        currentPlayerInventory = FindObjectOfType<PlayerInventory>(true);
        ApplyPadFocusVisible(false);

        if (inventoryPanel != null)
            inventoryPanel.SetActive(false);
        if (playerHudPanel != null)
            playerHudPanel.SetActive(true);

        CameraInputBlocker.SetAllCinemachineInput(true);
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
        if (inventoryPanel == null)
            inventoryPanel = GameObject.Find("HUD_Inventory");
        if (playerHudPanel == null)
            playerHudPanel = GameObject.Find("HUD_Canvas");
        if (menuAnimator == null && inventoryPanel != null)
        {
            menuAnimator = inventoryPanel.GetComponent<Animator>();
            if (menuAnimator == null)
                menuAnimator = inventoryPanel.GetComponentInChildren<Animator>(true);
        }
        ResolveTabBackgroundImage();

        if (inventoryUIManager == null && inventoryPanel != null)
            inventoryUIManager = inventoryPanel.GetComponentInChildren<InventoryUIManager>(true);
        if (inventoryUIManager == null)
            inventoryUIManager = FindObjectOfType<InventoryUIManager>(true);

        if (magicInventoryManager == null && inventoryPanel != null)
            magicInventoryManager = inventoryPanel.GetComponentInChildren<MagicInventoryManager>(true);
        if (magicInventoryManager == null)
            magicInventoryManager = FindObjectOfType<MagicInventoryManager>(true);

        if (equipmentManager == null && inventoryPanel != null)
            equipmentManager = inventoryPanel.GetComponentInChildren<EquipmentManager>(true);
        if (equipmentManager == null)
            equipmentManager = FindObjectOfType<EquipmentManager>(true);

        if (attributesUIManager == null && inventoryPanel != null)
            attributesUIManager = inventoryPanel.GetComponentInChildren<AttributesUIManager>(true);
        if (attributesUIManager == null)
            attributesUIManager = FindObjectOfType<AttributesUIManager>(true);

        if (questJournalUI == null && inventoryPanel != null)
        {
            questJournalUI = inventoryPanel.GetComponentInChildren<QuestJournalUI>(true);
            if (questJournalUI == null)
                questJournalUI = inventoryPanel.GetComponentInParent<QuestJournalUI>(true);
        }

        if (questJournalUI == null)
            questJournalUI = FindObjectOfType<QuestJournalUI>(true);
        if (currentPlayerInventory == null)
            currentPlayerInventory = FindObjectOfType<PlayerInventory>(true);

        equipmentManager?.Initialize(currentPlayerInventory, inventoryUIManager, magicInventoryManager);
        inventoryUIManager?.Initialize(currentPlayerInventory, equipmentManager);
        magicInventoryManager?.Initialize(currentPlayerInventory, equipmentManager);
        attributesUIManager?.Initialize();
    }

    private void ResolveTabBackgroundImage()
    {
        if (tabBackgroundImage != null)
            return;

        if (menuAnimator != null)
            tabBackgroundImage = menuAnimator.GetComponent<Image>();
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

        if (!PlayMenuAnimationState(menuCloseStateName))
            return false;

        float closeAnimationDuration = GetMenuAnimationDuration(menuCloseStateName, menuCloseFallbackDuration);
        if (closeAnimationDuration <= 0f)
            return false;

        isMenuClosing = true;
        closeMenuRoutine = StartCoroutine(CloseMenuAfterAnimation(controls, closeAnimationDuration, Mathf.Max(0f, menuCloseEndDelay)));
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

        float playbackSpeed = GetPageFlipPlaybackSpeed(flipCount);
        SetMenuAnimatorPlaybackSpeed(playbackSpeed);

        if (!PlayMenuAnimationState(stateName))
        {
            ResetMenuAnimatorPlayback();
            isMenuPageFlipping = false;
            return false;
        }

        float flipDuration = GetMenuAnimationDuration(stateName, menuPageFlipFallbackDuration) / playbackSpeed;
        if (flipDuration <= 0f)
        {
            ResetMenuAnimatorPlayback();
            isMenuPageFlipping = false;
            return false;
        }

        pendingTabIndex = targetTabIndex;
        isMenuPageFlipping = true;
        pageFlipRoutine = StartCoroutine(CompletePageFlipAfterDelay(stateName, flipDuration, flipCount));
        return true;
    }

    private void StartOpenMenuAnimation()
    {
        StopPendingOpenMenuRoutine();
        ResetMenuAnimatorPlayback();

        if (!PlayMenuAnimationState(menuOpenStateName))
        {
            isMenuOpening = false;
            return;
        }

        float delay = Mathf.Max(0f, menuOpenStartDelay);
        float openAnimationDuration = Mathf.Max(0f, GetMenuAnimationDuration(menuOpenStateName, 0f));
        if (delay <= 0f && openAnimationDuration <= 0f && !HasPostOpenAnimation() && !HasContentAppearAnimation())
        {
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

        float contentAppearAnimationDuration = PlayContentAppearAnimation();
        if (contentAppearAnimationDuration > 0f)
            yield return new WaitForSecondsRealtime(contentAppearAnimationDuration);

        openMenuRoutine = null;
        if (!isMenuOpen || isMenuClosing)
        {
            isMenuOpening = false;
            yield break;
        }

        HoldMenuAnimatorOnCurrentTabSprite();
        isMenuOpening = false;
    }

    private bool HasPostOpenAnimation()
    {
        return !string.IsNullOrWhiteSpace(menuPostOpenStateName);
    }

    private bool HasContentAppearAnimation()
    {
        return !string.IsNullOrWhiteSpace(menuContentAppearStateName);
    }

    private float PlayPostOpenAnimation()
    {
        if (!HasPostOpenAnimation())
            return 0f;

        if (!PlayMenuAnimationState(menuPostOpenStateName))
            return 0f;

        return GetMenuAnimationDuration(menuPostOpenStateName, menuPostOpenFallbackDuration);
    }

    private float PlayContentAppearAnimation()
    {
        if (!HasContentAppearAnimation())
            return 0f;

        if (!PlayAnimatorState(menuContentAnimator, menuContentAppearStateName))
            return 0f;

        return GetAnimatorAnimationDuration(menuContentAnimator, menuContentAppearStateName, menuContentAppearFallbackDuration);
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

    private IEnumerator CloseMenuAfterAnimation(PlayerControls controls, float closeAnimationDuration, float endDelay)
    {
        yield return new WaitForSecondsRealtime(closeAnimationDuration + endDelay);
        closeMenuRoutine = null;
        CompleteMenuClose(controls);
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
        ResetMenuAnimatorPlayback();
        ApplyPadFocusVisible(false);

        if (inventoryPanel != null)
            inventoryPanel.SetActive(false);
        if (playerHudPanel != null)
            playerHudPanel.SetActive(true);

        CameraInputBlocker.SetAllCinemachineInput(true);
        if (controls != null)
            controls.Player.Look.Enable();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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

        string[] candidates = BuildAnimatorStateCandidates(stateName);
        for (int i = 0; i < candidates.Length; i++)
        {
            string candidate = candidates[i];
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            int stateHash = Animator.StringToHash(GetAnimatorStatePath(candidate));
            if (!animator.HasState(0, stateHash))
                continue;

            animator.Play(stateHash, 0, 0f);
            animator.Update(0f);
            return true;
        }

        return false;
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
