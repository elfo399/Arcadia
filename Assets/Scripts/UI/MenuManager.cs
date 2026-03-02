using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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

    [Header("Tabs")]
    [SerializeField] private MenuTabEntry[] tabs;
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color inactiveColor = new Color(0.8f, 0.8f, 0.8f);
    [SerializeField] private string defaultOpenTabKey = "Equipment";

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

    public bool IsMenuOpen => isMenuOpen;
    public bool IsPadFocusVisible => showPadFocus;
    public string CurrentTabKey => IsValidTabIndex(currentTabIndex) ? tabs[currentTabIndex].key : string.Empty;

    private void Awake()
    {
        ResolveReferences();
        ApplyPadFocusVisible(false);

        if (inventoryPanel != null)
            inventoryPanel.SetActive(false);
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

        currentTabIndex = targetIndex;
        string resolvedKey = tabs[currentTabIndex].key;

        for (int i = 0; i < tabs.Length; i++)
        {
            var tab = tabs[i];
            if (tab == null) continue;

            bool isActive = i == currentTabIndex;
            if (tab.label != null)
                tab.label.color = isActive ? activeColor : inactiveColor;
            if (tab.background != null)
                tab.background.SetActive(isActive);
        }

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
        if (tabs == null || tabs.Length == 0) return;

        int startIndex = IsValidTabIndex(currentTabIndex) ? currentTabIndex : FindFirstValidTabIndex();
        if (!IsValidTabIndex(startIndex)) return;

        for (int offset = 1; offset <= tabs.Length; offset++)
        {
            int next = (startIndex + offset) % tabs.Length;
            if (!IsValidTabIndex(next)) continue;
            ShowTab(tabs[next].key);
            return;
        }
    }

    public void PreviousTab()
    {
        if (tabs == null || tabs.Length == 0) return;

        int startIndex = IsValidTabIndex(currentTabIndex) ? currentTabIndex : FindFirstValidTabIndex();
        if (!IsValidTabIndex(startIndex)) return;

        for (int offset = 1; offset <= tabs.Length; offset++)
        {
            int prev = (startIndex - offset + tabs.Length) % tabs.Length;
            if (!IsValidTabIndex(prev)) continue;
            ShowTab(tabs[prev].key);
            return;
        }
    }

    public void HandleMenuInput(PlayerControls controls)
    {
        if (!isMenuOpen || controls == null)
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

        bool rightPressed = (Keyboard.current != null && Keyboard.current.rightArrowKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.dpad.right.wasPressedThisFrame);
        bool leftPressed = (Keyboard.current != null && Keyboard.current.leftArrowKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.dpad.left.wasPressedThisFrame);
        bool downPressed = (Keyboard.current != null && Keyboard.current.downArrowKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.dpad.down.wasPressedThisFrame);
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
        currentPlayerInventory = playerInventory;
        isMenuOpen = true;

        if (inventoryPanel != null)
            inventoryPanel.SetActive(true);
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

        ShowTab(string.IsNullOrWhiteSpace(defaultOpenTabKey) ? "Equipment" : defaultOpenTabKey);
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

        isMenuOpen = false;
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

    private bool IsQuestTabActive()
    {
        return IsQuestTabKey(CurrentTabKey);
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
}
