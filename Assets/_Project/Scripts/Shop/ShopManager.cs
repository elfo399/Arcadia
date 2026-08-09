using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum ShopMode
{
    Buy,
    Sell
}

public sealed class ShopManager : MonoBehaviour, IInventorySlotHandler
{
    [Header("Scene References")]
    [SerializeField] private GameObject shopHud;
    [SerializeField] private GameObject initialFocus;
    [SerializeField] private Animator bookAnimator;
    [SerializeField] private Animator contentAppearAnimator;
    [SerializeField] private CanvasGroup contentGroup;
    [SerializeField] private InventorySlot slotPrefab;
    [SerializeField] private Transform slotParent;
    [SerializeField] [Min(0)] private int initialSlotCount = 30;

    [Header("Initial State")]
    [SerializeField] private ShopMode initialMode = ShopMode.Buy;
    [SerializeField] private string contentAppearStateName = "Transition";
    [SerializeField] [Min(0f)] private float contentAppearDelay = 0.5833333f;
    [SerializeField] [Min(0f)] private float contentAppearDuration = 1.8f;
    [SerializeField] private string bookCloseStateName = "CloseBook";
    [SerializeField] [Min(0f)] private float bookCloseDuration = 0.6666666f;

    private readonly object gameplayLockOwner = new object();
    private PlayerController playerController;
    private PlayerControls controls;
    private Action<InputAction.CallbackContext> confirmCallback;
    private Action<InputAction.CallbackContext> cancelCallback;
    private int openingFrame = -1;
    private Coroutine contentAppearRoutine;
    private Coroutine closeRoutine;
    private bool isClosing;
    private readonly List<InventorySlot> shopSlots = new List<InventorySlot>();
    private readonly List<InventoryItem> shopItems = new List<InventoryItem>();
    [SerializeField] private GameObject weaponSection;
    [SerializeField] private GameObject shieldSection;
    [SerializeField] private GameObject armorSection;
    [SerializeField] private GameObject itemSection;
    [SerializeField] private GameObject commonTitle;
    [SerializeField] private GameObject commonImage;
    private int shopFocusIndex;
    private float lastNavigationTime = -999f;
    private const float NavigationRepeatCooldown = 0.20f;

    public bool IsOpen { get; private set; }
    public ShopMode CurrentMode { get; private set; }

    public event Action<ShopMode> ConfirmRequested;
    public event Action Closed;

    private void Awake()
    {
        confirmCallback = OnConfirmPerformed;
        cancelCallback = OnCancelPerformed;

        if (shopHud != null)
            shopHud.SetActive(false);
        EnsureShopSlots();
        HideContentAppearAnimation();
        HideContentGroup();
    }

    private void EnsureShopSlots()
    {
        if (slotPrefab == null || slotParent == null || initialSlotCount <= 0)
            return;

        int existing = slotParent.GetComponentsInChildren<InventorySlot>(true).Length;
        for (int i = existing; i < initialSlotCount; i++)
        {
            InventorySlot slot = Instantiate(slotPrefab, slotParent);
            slot.name = $"InvSlot_{i:00}";
            slot.SetDisplayOnly(false);
            slot.Init(i, this);
            slot.Clear();
        }

        shopSlots.Clear();
        shopSlots.AddRange(slotParent.GetComponentsInChildren<InventorySlot>(true));
        for (int i = 0; i < shopSlots.Count; i++)
        {
            shopSlots[i].SetDisplayOnly(false);
            shopSlots[i].Init(i, this);
            shopSlots[i].SetFocused(false);
        }
    }

    public void SetShopItems(IReadOnlyList<InventoryItem> items)
    {
        shopItems.Clear();
        if (items != null) shopItems.AddRange(items);
        for (int i = 0; i < shopSlots.Count; i++)
        {
            InventoryItem item = i < shopItems.Count ? shopItems[i] : null;
            if (item != null) shopSlots[i].Setup(GetItemIcon(item), item.amount);
            else shopSlots[i].Clear();
        }
        SetShopFocus(0);
    }

    private void ShowSelectedItem(int index)
    {
        HideDetailSections();
        if (index < 0 || index >= shopItems.Count || shopItems[index] == null) return;
        InventoryItem item = shopItems[index];
        if (commonTitle != null) commonTitle.SetActive(true);
        if (commonImage != null) commonImage.SetActive(true);
        if (item.weaponData != null)
            (item.weaponData.category == WeaponCategory.Shield ? shieldSection : weaponSection)?.SetActive(true);
        else if (item.armorData != null)
            armorSection?.SetActive(true);
        else
            itemSection?.SetActive(true);
    }

    private void HideDetailSections()
    {
        commonTitle?.SetActive(false);
        commonImage?.SetActive(false);
        weaponSection?.SetActive(false);
        shieldSection?.SetActive(false);
        armorSection?.SetActive(false);
        itemSection?.SetActive(false);
    }

    private Sprite GetItemIcon(InventoryItem item)
    {
        if (item == null) return null;
        return item.icon ?? item.weaponData?.icon ?? item.armorData?.icon ?? item.usableData?.icon ?? item.itemData?.icon;
    }

    private void OnDisable()
    {
        if (IsOpen)
            CloseShopInternal(notifyClosed: false);
        else if (isClosing)
            FinishClose(notifyClosed: false);
    }

    public bool OpenShop()
    {
        return OpenShop(FindObjectOfType<PlayerController>(), initialMode);
    }

    public bool OpenShop(PlayerController controller, ShopMode mode = ShopMode.Buy)
    {
        if (isClosing)
            return false;

        if (shopHud == null)
        {
            Debug.LogWarning("[ShopManager] HUD Market non assegnata.", this);
            return false;
        }

        if (controller == null || controller.Controls == null)
        {
            Debug.LogWarning("[ShopManager] PlayerController o PlayerControls non disponibili.", this);
            return false;
        }

        if (IsOpen)
        {
            CurrentMode = mode;
            FocusInitialTarget();
            return true;
        }

        playerController = controller;
        controls = controller.Controls;
        CurrentMode = mode;
        IsOpen = true;
        openingFrame = Time.frameCount;

        playerController.AcquireGameplayInputLock(gameplayLockOwner);
        SubscribeInput();

        shopHud.SetActive(true);
        Canvas.ForceUpdateCanvases();
        FocusInitialTarget();
        SetShopFocus(0);
        contentAppearRoutine = StartCoroutine(PlayContentAppearAnimation());
        return true;
    }

    private void Update()
    {
        if (!IsOpen || controls == null || Time.unscaledTime < lastNavigationTime + NavigationRepeatCooldown)
            return;

        Vector2 navigation = controls.Player.Move.ReadValue<Vector2>();
        if (navigation.x > 0.5f)
            MoveShopFocusHorizontal(1);
        else if (navigation.x < -0.5f)
            MoveShopFocusHorizontal(-1);
        else if (navigation.y > 0.5f)
            MoveShopFocusVertical(-1);
        else if (navigation.y < -0.5f)
            MoveShopFocusVertical(1);
        else
            return;

        lastNavigationTime = Time.unscaledTime;
    }

    private void MoveShopFocusHorizontal(int direction)
    {
        if (shopSlots.Count == 0) return;
        SetShopFocus((shopFocusIndex + (direction >= 0 ? 1 : -1) + shopSlots.Count) % shopSlots.Count);
    }

    private void MoveShopFocusVertical(int direction)
    {
        if (shopSlots.Count == 0) return;
        int columns = 5;
        GridLayoutGroup grid = slotParent != null ? slotParent.GetComponent<GridLayoutGroup>() : null;
        if (grid != null && grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            columns = Mathf.Max(1, grid.constraintCount);
        int next = shopFocusIndex + direction * columns;
        next %= shopSlots.Count;
        if (next < 0) next += shopSlots.Count;
        SetShopFocus(next);
    }

    private void SetShopFocus(int index)
    {
        if (shopSlots.Count == 0) return;
        shopFocusIndex = Mathf.Clamp(index, 0, shopSlots.Count - 1);
        for (int i = 0; i < shopSlots.Count; i++)
            shopSlots[i].SetFocused(i == shopFocusIndex);
        ShowSelectedItem(shopFocusIndex);
    }

    public void HandleSlotSelected(int index) => ShowSelectedItem(index);
    public void HandleSlotSubmit(int index) => ConfirmRequested?.Invoke(CurrentMode);
    public void HandleSlotPointerDown(int index) => SetShopFocus(index);
    public void HandleSlotBeginDrag(int index, PointerEventData eventData) { }
    public void HandleSlotDrag(PointerEventData eventData) { }
    public void HandleSlotEndDrag() { }
    public void HandleSlotDrop(int targetIndex) { }

    public void SetMode(ShopMode mode)
    {
        CurrentMode = mode;
    }

    public void CloseShop()
    {
        CloseShopInternal(notifyClosed: true);
    }

    private void CloseShopInternal(bool notifyClosed)
    {
        if (!IsOpen || isClosing)
            return;

        IsOpen = false;
        isClosing = true;
        UnsubscribeInput();
        StopContentAppearRoutineOnly();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        if (notifyClosed)
        {
            closeRoutine = StartCoroutine(RunCloseAnimations());
            return;
        }

        FinishClose(notifyClosed);
    }

    private System.Collections.IEnumerator RunCloseAnimations()
    {
        if (contentAppearAnimator != null && contentAppearDuration > 0f)
        {
            contentAppearAnimator.gameObject.SetActive(true);
            contentAppearAnimator.enabled = true;
            contentAppearAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;

            float elapsed = 0f;
            while (elapsed < contentAppearDuration)
            {
                float normalizedTime = 1f - Mathf.Clamp01(elapsed / contentAppearDuration);
                contentAppearAnimator.Play(contentAppearStateName, 0, normalizedTime);
                contentAppearAnimator.Update(0f);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            contentAppearAnimator.Play(contentAppearStateName, 0, 0f);
            contentAppearAnimator.Update(0f);
            HideContentAppearAnimation();
        }

        HideContentGroup();

        if (bookAnimator != null && bookCloseDuration > 0f)
        {
            bookAnimator.enabled = true;
            bookAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            bookAnimator.Play(bookCloseStateName, 0, 0f);
            bookAnimator.Update(0f);
            yield return new WaitForSecondsRealtime(bookCloseDuration);
        }

        FinishClose(notifyClosed: true);
    }

    private void FinishClose(bool notifyClosed)
    {
        if (shopHud != null)
            shopHud.SetActive(false);

        isClosing = false;
        closeRoutine = null;

        // The service reopens the dialogue from Closed while this lock is still
        // held, so gameplay never becomes active between the two modal states.
        if (notifyClosed)
            Closed?.Invoke();

        if (playerController != null)
            playerController.ReleaseGameplayInputLock(gameplayLockOwner);

        controls = null;
        playerController = null;
        openingFrame = -1;
    }

    private System.Collections.IEnumerator PlayContentAppearAnimation()
    {
        if (contentAppearAnimator == null || string.IsNullOrWhiteSpace(contentAppearStateName))
            yield break;

        if (contentAppearDelay > 0f)
            yield return new WaitForSecondsRealtime(contentAppearDelay);
        if (!IsOpen)
            yield break;

        GameObject animationObject = contentAppearAnimator.gameObject;
        animationObject.SetActive(true);
        ShowContentGroup();
        contentAppearAnimator.enabled = true;
        contentAppearAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        contentAppearAnimator.Play(contentAppearStateName, 0, 0f);
        contentAppearAnimator.Update(0f);

        if (contentAppearDuration > 0f)
            yield return new WaitForSecondsRealtime(contentAppearDuration);

        HideContentAppearAnimation();
        contentAppearRoutine = null;
    }

    private void StopContentAppearRoutineOnly()
    {
        if (contentAppearRoutine != null)
            StopCoroutine(contentAppearRoutine);
        contentAppearRoutine = null;
    }

    private void HideContentAppearAnimation()
    {
        if (contentAppearAnimator != null)
        {
            contentAppearAnimator.enabled = false;
            contentAppearAnimator.gameObject.SetActive(false);
        }

    }

    private void HideContentGroup()
    {
        if (contentGroup == null)
            return;

        contentGroup.alpha = 0f;
        contentGroup.interactable = false;
        contentGroup.blocksRaycasts = false;
    }

    private void ShowContentGroup()
    {
        if (contentGroup == null)
            return;

        contentGroup.alpha = 1f;
        contentGroup.interactable = true;
        contentGroup.blocksRaycasts = true;
    }

    private void SubscribeInput()
    {
        if (controls == null)
            return;

        // Same actions used by the player inventory/menu: Cross confirms,
        // Circle/Back closes. UI navigation remains owned by the EventSystem.
        controls.Player.Jump.performed -= confirmCallback;
        controls.Player.Jump.performed += confirmCallback;
        controls.Player.SprintOrDodge.performed -= cancelCallback;
        controls.Player.SprintOrDodge.performed += cancelCallback;
    }

    private void UnsubscribeInput()
    {
        if (controls == null)
            return;

        controls.Player.Jump.performed -= confirmCallback;
        controls.Player.SprintOrDodge.performed -= cancelCallback;
    }

    private void OnConfirmPerformed(InputAction.CallbackContext _)
    {
        if (!IsOpen || openingFrame == Time.frameCount)
            return;

        ConfirmRequested?.Invoke(CurrentMode);
    }

    private void OnCancelPerformed(InputAction.CallbackContext _)
    {
        if (!IsOpen || openingFrame == Time.frameCount)
            return;

        CloseShop();
    }

    private void FocusInitialTarget()
    {
        if (EventSystem.current == null)
            return;

        GameObject focusTarget = initialFocus;
        Selectable selectable = initialFocus != null
            ? initialFocus.GetComponentInChildren<Selectable>(includeInactive: false)
            : null;
        if (selectable == null && shopHud != null)
            selectable = shopHud.GetComponentInChildren<Selectable>(includeInactive: false);
        if (selectable != null)
            focusTarget = selectable.gameObject;

        if (focusTarget != null)
            EventSystem.current.SetSelectedGameObject(focusTarget);
    }
}
