using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public enum EquipLoadTier
    {
        Fast,
        Normal,
        Slow
    }

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float sprintMultiplier = 1.5f;
    public float rotationSpeed = 720f;
    public float gravity = -20f;
    [Header("Equip Load Speed")]
    [Range(0f, 1f)] public float lightLoadThreshold = 0.20f;
    [Range(0f, 1f)] public float heavyLoadThreshold = 0.80f;
    [Min(0.1f)] public float lightLoadSpeedMultiplier = 1.15f;
    [Min(0.1f)] public float heavyLoadSpeedMultiplier = 0.75f;

    [Header("Jump")]
    public float jumpHeight = 1.2f;
    public float coyoteTime = 0.15f;

    [Header("Dodge / Roll")]
    public float dodgeDistance = 4f;     
    public float dodgeDuration = 0.6f;   
    public float dodgeCooldown = 0.8f;   
    public float rollStartDelay = 0.05f; 
    public AnimationCurve dodgeSpeedCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0));
    [Range(0f, 1f)] public float rollIFrameStartNormalized = 0.15f;
    [Range(0f, 1f)] public float rollIFrameEndNormalized = 0.65f;

    [Header("Stamina Costs")]
    public float rollStaminaCost = 25f;
    public float jumpStaminaCost = 15f;
    public float sprintStaminaCostPerSecond = 10f;

    [Header("Falling")]
    public float fallingSpeedThreshold = -2.0f;

    [Header("UI")]
    public MenuManager menuManager;

    // Flags
    [HideInInspector] public bool canMove = true;
    [HideInInspector] public float moveAmount;
    [HideInInspector] public bool isSprinting = false;
    [HideInInspector] public bool isDodging = false; // Letto dal LockSystem
    [HideInInspector] public bool isFalling = false;
    
    public bool IsInventoryOpen => menuManager != null && menuManager.IsMenuOpen;
    public static Transform CurrentPlayerTransform { get; private set; }

    [SerializeField] private Animator animator;
    private CharacterController controller;
    public PlayerControls Controls { get; private set; }
    private PlayerCombat combat;
    private PlayerInventory playerInventory;
    private PlayerStats playerStats;
    private Transform cam;
    private InputAction cycleRightEquipAction;
    private InputAction cycleLeftEquipAction;
    private InputAction cycleUsableAction;
    private InputAction cycleMagicAction;
    private Coroutine sceneEquipmentRefreshRoutine;

    private Vector3 velocity;
    private float lastDodgeTime = -999f;
    private float lastGroundedTime = -999f;
    private float actionButtonDownTime = 0f;
    private bool actionButtonHeld = false;
    private float sprintThreshold = 0.25f;
    private bool controlsInitialized = false;
    private float inventoryInputReadyTime;
    private const float InventoryInputEnableDelay = 0.25f;

    public bool IsGrounded => controller != null && controller.isGrounded;

    public bool IsRolling
    {
        get
        {
            if (animator == null) return isDodging;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            bool inMainRollAnim = state.IsName("Roll") && state.normalizedTime < 0.9f;
            return isDodging || inMainRollAnim;
        }
    }

    void Awake()
    {
        CurrentPlayerTransform = transform;
        controller = GetComponent<CharacterController>();
        cam = Camera.main != null ? Camera.main.transform : null;
        EnsureControlsInitialized();
        canMove = true;
        isSprinting = false;
        isDodging = false;
        isFalling = false;

        if (animator == null) animator = GetComponentInChildren<Animator>();
        playerStats = GetComponent<PlayerStats>();
        playerInventory = GetComponent<PlayerInventory>();
        combat = GetComponent<PlayerCombat>();

        ResolveMenuManager();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnEnable()
    {
        CurrentPlayerTransform = transform;
        EnsureControlsInitialized();
        if (Controls != null) Controls.Player.Enable();
        inventoryInputReadyTime = Time.unscaledTime + InventoryInputEnableDelay;
        ResolveMenuManager();
        canMove = menuManager == null || !menuManager.IsMenuOpen;
        RequestSceneEquipmentUIRefresh();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (sceneEquipmentRefreshRoutine != null)
        {
            StopCoroutine(sceneEquipmentRefreshRoutine);
            sceneEquipmentRefreshRoutine = null;
        }
        if (Controls != null) Controls.Player.Disable();
        if (playerStats != null) playerStats.SetInvulnerable(false);
    }

    private void OnDestroy()
    {
        if (CurrentPlayerTransform == transform)
            CurrentPlayerTransform = null;

        if (controlsInitialized && Controls != null)
            Controls.Player.Inventory.performed -= OnInventoryPerformed;
    }

    void Update()
    {
        if (Controls == null || controller == null) return;
        if (cam == null && Camera.main != null) cam = Camera.main.transform;

        if (IsInventoryOpen)
        {
            canMove = false;
        }
        else if (!isDodging)
        {
            canMove = true;
        }

        if (controller.isGrounded)
        {
            lastGroundedTime = Time.time;
            if (velocity.y < 0f) velocity.y = -2f; 
            isFalling = false;
        }

        if (!IsInventoryOpen)
        {
            bool isAttacking = combat != null && combat.isAttacking;
            bool isRolling = IsRolling;
            
            Vector2 moveInput = Vector2.zero;
            if (canMove && !isRolling && !isAttacking)
            {
                moveInput = Controls.Player.Move.ReadValue<Vector2>();
            }
            moveAmount = moveInput.magnitude;

            HandleSprintAndDodgeInput(moveInput);

            if (moveAmount > 0.01f && !isRolling && !isAttacking)
            {
                HandleMovement(moveInput);
            }
            else
            {
                if (!isRolling && !isAttacking)
                {
                    animator.SetFloat("Speed", 0f);
                    animator.SetBool("IsSprinting", false);
                }
            }

            HandleJump();
            HandleQuickSlotCycleInput();
        }
        else
        {
            // Se l'inventario è aperto, azzera l'input di movimento
            moveAmount = 0;
            isSprinting = false;
            animator.SetFloat("Speed", 0f);
            animator.SetBool("IsSprinting", false);

            if (menuManager != null)
                menuManager.HandleMenuInput(Controls);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        UpdateFallingAnimator();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        cam = Camera.main != null ? Camera.main.transform : null;
        playerInventory = GetComponent<PlayerInventory>();
        playerStats = GetComponent<PlayerStats>();
        combat = GetComponent<PlayerCombat>();
        ResolveMenuManager();
        RequestSceneEquipmentUIRefresh();

        inventoryInputReadyTime = Time.unscaledTime + InventoryInputEnableDelay;
        canMove = menuManager == null || !menuManager.IsMenuOpen;
    }

    private void RequestSceneEquipmentUIRefresh()
    {
        if (!isActiveAndEnabled)
        {
            BindAndRefreshEquipmentUI();
            return;
        }

        if (sceneEquipmentRefreshRoutine != null)
            StopCoroutine(sceneEquipmentRefreshRoutine);

        sceneEquipmentRefreshRoutine = StartCoroutine(RefreshSceneEquipmentUIRoutine());
    }

    private IEnumerator RefreshSceneEquipmentUIRoutine()
    {
        for (int i = 0; i < 5; i++)
        {
            BindAndRefreshEquipmentUI();
            yield return null;
        }

        sceneEquipmentRefreshRoutine = null;
    }

    private void BindAndRefreshEquipmentUI()
    {
        if (playerInventory == null)
            playerInventory = GetComponent<PlayerInventory>();

        ResolveMenuManager();

        if (menuManager != null)
            menuManager.BindPlayerInventory(playerInventory);
    }

    private void EnsureControlsInitialized()
    {
        if (controlsInitialized && Controls != null) return;
        Controls = new PlayerControls();
        cycleRightEquipAction = Controls.asset.FindAction("Player/CycleRightEquip", throwIfNotFound: false);
        cycleLeftEquipAction = Controls.asset.FindAction("Player/CycleLeftEquip", throwIfNotFound: false);
        cycleUsableAction = Controls.asset.FindAction("Player/CycleUsable", throwIfNotFound: false);
        cycleMagicAction = Controls.asset.FindAction("Player/CycleMagic", throwIfNotFound: false);
        Controls.Player.Inventory.performed -= OnInventoryPerformed;
        Controls.Player.Inventory.performed += OnInventoryPerformed;
        controlsInitialized = true;
    }

    private void HandleQuickSlotCycleInput()
    {
        if (playerInventory == null) return;

        bool changed = false;
        if (cycleRightEquipAction != null && cycleRightEquipAction.WasPerformedThisFrame())
        {
            changed |= playerInventory.CycleRightWeapon(1);
        }
        if (cycleLeftEquipAction != null && cycleLeftEquipAction.WasPerformedThisFrame())
        {
            changed |= playerInventory.CycleLeftWeapon(1);
        }
        if (cycleUsableAction != null && cycleUsableAction.WasPerformedThisFrame())
        {
            changed |= playerInventory.CycleUsable(1);
        }
        if (cycleMagicAction != null && cycleMagicAction.WasPerformedThisFrame())
        {
            changed |= playerInventory.CycleMagic(1);
        }
        else if (cycleMagicAction == null && Gamepad.current != null && Gamepad.current.dpad.up.wasPressedThisFrame)
        {
            // Fallback: se l'InputAction non è ancora configurata, usa D-Pad Up.
            changed |= playerInventory.CycleMagic(1);
        }

        if (changed)
            menuManager?.RefreshEquipmentUI();
    }

    void HandleMovement(Vector2 moveInput)
    {
        float equipLoadMultiplier = GetEquipLoadSpeedMultiplier();
        float targetSpeed = moveSpeed * equipLoadMultiplier * (isSprinting ? sprintMultiplier : 1f);
        
        Vector3 camForward = cam != null ? cam.forward : transform.forward;
        camForward.y = 0f;
        camForward.Normalize();

        Vector3 camRight = cam != null ? cam.right : transform.right;
        camRight.y = 0f;
        camRight.Normalize();
        Vector3 moveDir = camForward * moveInput.y + camRight * moveInput.x;
        moveDir.Normalize();

        controller.Move(moveDir * targetSpeed * Time.deltaTime);

        // Rotazione
        if (moveDir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        animator.SetFloat("Speed", moveAmount); 
        animator.SetBool("IsSprinting", isSprinting);
    }

    private float GetEquipLoadSpeedMultiplier()
    {
        switch (GetEquipLoadTier())
        {
            case EquipLoadTier.Fast:
                return Mathf.Max(0.1f, lightLoadSpeedMultiplier);
            case EquipLoadTier.Slow:
                return Mathf.Max(0.1f, heavyLoadSpeedMultiplier);
            default:
                return 1f;
        }
    }

    public float GetEquipLoadRatio()
    {
        if (playerStats == null)
            playerStats = GetComponent<PlayerStats>();
        if (playerStats == null)
            return 0f;

        float maxLoad = Mathf.Max(0.001f, playerStats.GetMaxEquipLoad());
        float currentLoad = Mathf.Max(0f, playerStats.GetCurrentEquipLoad());
        return currentLoad / maxLoad;
    }

    public EquipLoadTier GetEquipLoadTier()
    {
        float loadRatio = GetEquipLoadRatio();

        if (loadRatio < lightLoadThreshold)
            return EquipLoadTier.Fast;
        if (loadRatio > heavyLoadThreshold)
            return EquipLoadTier.Slow;
        return EquipLoadTier.Normal;
    }

    public string GetEquipLoadTierLabel()
    {
        switch (GetEquipLoadTier())
        {
            case EquipLoadTier.Fast:
                return "Fast";
            case EquipLoadTier.Slow:
                return "Slow";
            default:
                return "Normal";
        }
    }

    void HandleSprintAndDodgeInput(Vector2 moveInput)
    {
        bool pressed = Controls.Player.SprintOrDodge.WasPerformedThisFrame();
        bool released = Controls.Player.SprintOrDodge.WasReleasedThisFrame();
        bool isAttacking = combat != null && combat.isAttacking;

        if (pressed)
        {
            actionButtonDownTime = Time.time;
            actionButtonHeld = true;
        }

        if (actionButtonHeld && !isSprinting && moveAmount > 0.01f && !isAttacking && !IsRolling)
        {
            if (Time.time - actionButtonDownTime >= sprintThreshold)
            {
                if (playerStats == null || playerStats.HasStamina(1f)) isSprinting = true;
            }
        }

        if (released)
        {
            float holdTime = Time.time - actionButtonDownTime;
            if (holdTime < sprintThreshold)
            {
                if (!isAttacking && !IsRolling) TryDodge(moveInput);
            }
            isSprinting = false;
            actionButtonHeld = false;
        }

        if (isSprinting && moveAmount > 0.01f && playerStats != null)
        {
            playerStats.SpendStaminaPerSecond(sprintStaminaCostPerSecond);
            if (!playerStats.HasStamina(1f)) isSprinting = false;
        }
    }

    void HandleJump()
    {
        if (Controls.Player.Jump.WasPerformedThisFrame() && !IsRolling && combat != null && !combat.isAttacking)
        {
            if ((Time.time - lastGroundedTime) <= coyoteTime)
            {
                if (playerStats == null || playerStats.HasStamina(jumpStaminaCost))
                {
                    if (playerStats != null) playerStats.SpendStamina(jumpStaminaCost);
                    animator.CrossFadeInFixedTime("Jump", 0.1f);
                    velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }
        }
    }

    void UpdateFallingAnimator()
    {
        if (animator != null)
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            bool isAirAttacking = combat != null && combat.isAttacking && !controller.isGrounded;
            bool fallingCondition = !controller.isGrounded
                                    && !IsRolling
                                    && !isAirAttacking
                                    && !state.IsName("Jump")
                                    && velocity.y < fallingSpeedThreshold;
            animator.SetBool("IsFalling", fallingCondition);
        }
    }

    private void TryDodge(Vector2 moveInput)
    {
        if (isDodging) return;
        if (Time.time < lastDodgeTime + dodgeCooldown) return;
        
        if (playerStats != null && !playerStats.HasStamina(rollStaminaCost)) return;
        if (playerStats != null) playerStats.SpendStamina(rollStaminaCost);

        StartCoroutine(DodgeCoroutine(moveInput));
    }

    private IEnumerator DodgeCoroutine(Vector2 moveInput)
    {
        isDodging = true;
        canMove = false; 
        lastDodgeTime = Time.time;

        if (combat != null) combat.canAttack = false;

        // --- CALCOLO DIREZIONE INTELLIGENTE ---
        Vector3 dodgeDir;

        if (moveInput.sqrMagnitude > 0.01f)
        {
            // Se premo una direzione, vado LÌ (anche se sono lockato)
            Vector3 camForward = cam.forward; camForward.y = 0f; camForward.Normalize();
            Vector3 camRight = cam.right; camRight.y = 0f; camRight.Normalize();
            dodgeDir = camForward * moveInput.y + camRight * moveInput.x;
            dodgeDir.Normalize();
        }
        else
        {
            // Se non premo nulla, vado all'INDIETRO rispetto al personaggio
            dodgeDir = -transform.forward; 
        }

        // --- ROTAZIONE FORZATA ---
        // Mi giro verso la direzione di fuga.
        // Poiché TargetLockSystem vede che isDodging=true, non mi forzerà a guardare il nemico!
        transform.rotation = Quaternion.LookRotation(dodgeDir);

        animator.CrossFadeInFixedTime("Roll", 0.05f, 0, 0.2f);

        yield return new WaitForSeconds(rollStartDelay);

        float elapsed = 0f;
        bool iframeActive = false;
        float iframeStart = Mathf.Clamp01(rollIFrameStartNormalized);
        float iframeEnd = Mathf.Clamp(rollIFrameEndNormalized, iframeStart, 1f);
        while (elapsed < dodgeDuration)
        {
            float t = elapsed / dodgeDuration;
            float curveValue = dodgeSpeedCurve.Evaluate(t);
            float currentSpeed = (dodgeDistance / dodgeDuration) * curveValue;

            bool shouldBeInvulnerable = t >= iframeStart && t <= iframeEnd;
            if (playerStats != null && shouldBeInvulnerable != iframeActive)
            {
                iframeActive = shouldBeInvulnerable;
                playerStats.SetInvulnerable(iframeActive);
            }

            controller.Move(dodgeDir * currentSpeed * Time.deltaTime);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (playerStats != null) playerStats.SetInvulnerable(false);
        isDodging = false;
        canMove = true;
        if (combat != null) combat.canAttack = true;
    }

    public void StopMovementImmediate()
    {
        moveAmount = 0f;
        isSprinting = false;
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.SetBool("IsSprinting", false);
        }
    }

    private void OnInventoryPerformed(InputAction.CallbackContext ctx)
    {
        if (Time.unscaledTime < inventoryInputReadyTime)
            return;

        ToggleInventory();
    }

    private void ToggleInventory()
    {
        ResolveMenuManager();
        if (menuManager == null)
        {
            Debug.LogWarning("[PlayerController] MenuManager non assegnato.");
            return;
        }

        if (!menuManager.IsMenuOpen && !CanOpenMenuInCurrentRoom())
            return;

        bool menuOpen = menuManager.ToggleMenu(Controls, playerInventory);
        canMove = !menuOpen;
        if (!canMove)
            StopMovementImmediate();
    }

    private bool CanOpenMenuInCurrentRoom()
    {
        Room currentRoom = Room.CurrentPlayerRoom;
        return currentRoom == null || currentRoom.CanOpenMenuHere();
    }

    private void ResolveMenuManager()
    {
        if (menuManager != null)
            return;

#if UNITY_2023_1_OR_NEWER
        menuManager = FindFirstObjectByType<MenuManager>();
#else
        menuManager = FindObjectOfType<MenuManager>();
#endif
    }

}
