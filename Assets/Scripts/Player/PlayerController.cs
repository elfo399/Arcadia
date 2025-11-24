using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    // Base movement speed
    public float moveSpeed = 4.5f;
    // Multiplier applied when sprinting
    public float sprintMultiplier = 1.6f;
    // Rotation speed in degrees per second
    public float rotationSpeed = 360f;
    // Gravity applied to the character
    public float gravity = -20f;

    [Header("Jump")]
    // Jump height in world units
    public float jumpHeight = 1.5f;
    // Grace period to allow jump after leaving the ground
    public float coyoteTime = 0.12f;

    [Header("Dodge / Roll")]
    // Distance covered during a dodge
    public float dodgeDistance = 2f;
    // Duration of the dodge movement
    public float dodgeDuration = 0.25f;
    // Cooldown between dodges
    public float dodgeCooldown = 0.6f;
    // Delay to sync movement with roll animation
    public float rollStartDelay = 0.1f;
    // Speed curve for dodge movement
    public AnimationCurve dodgeSpeedCurve =
        AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Stamina Costs")]
    // Stamina spent per roll
    public float rollStaminaCost = 30f;
    // Stamina spent per jump
    public float jumpStaminaCost = 15f;
    // Stamina spent per second while sprinting
    public float sprintStaminaCostPerSecond = 12f;

    [Header("Falling")]
    // Threshold velocity to consider the player falling
    public float fallingSpeedThreshold = -1.0f;

    // Flag to allow movement processing
    [HideInInspector] public bool canMove = true;
    // Magnitude of current movement input
    [HideInInspector] public float moveAmount;
    // Whether the player is currently sprinting
    [HideInInspector] public bool isSprinting = false;

    // Animator controlling player animations
    [SerializeField] private Animator animator;

    // Character controller handling movement
    private CharacterController controller;
    // Current velocity used for gravity and jump
    private Vector3 velocity;
    // Cached camera transform
    private Transform cam;
    // Input controls instance
    private PlayerControls controls;
    // Combat component reference
    private PlayerCombat combat;

    // Whether the player is in a dodge coroutine
    [HideInInspector] public bool isDodging = false;
    // Whether the player is flagged as falling
    [HideInInspector] public bool isFalling = false;

    // Grounded check using the character controller
    public bool IsGrounded => controller != null && controller.isGrounded;
    // Timestamp of the last dodge
    private float lastDodgeTime = -999f;

    // Timestamp tracking action button hold duration
    private float actionButtonDownTime = 0f;
    // Whether the sprint/dodge button is held
    private bool actionButtonHeld = false;
    // Time threshold to distinguish sprint from dodge
    private float sprintThreshold = 0.20f;

    // Timestamp of the last grounded frame for coyote time
    private float lastGroundedTime = -999f;

    // Player stats reference for stamina checks
    private PlayerStats playerStats;

    // True while the roll animation or coroutine is active
    public bool IsRolling
    {
        get
        {
            if (animator == null)
                return isDodging;

            var state = animator.GetCurrentAnimatorStateInfo(0);
            bool inMainRollAnim = state.IsName("Roll") && state.normalizedTime < 0.8f;

            return isDodging || inMainRollAnim;
        }
    }

    // Exposes whether the player is flagged as falling
    public bool IsFalling => isFalling;

    // Initialize components and references
    void Awake()
    {
        controller = GetComponent<CharacterController>();
        cam = Camera.main.transform;

        controls = new PlayerControls();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        playerStats = GetComponent<PlayerStats>();
        combat = GetComponent<PlayerCombat>();
    }

    // Enable input actions
    void OnEnable() => controls.Player.Enable();
    // Disable input actions
    void OnDisable() => controls.Player.Disable();

    // Handle movement, sprinting, jumping, and falling each frame
    void Update()
    {
        bool isAttacking = combat != null && combat.isAttacking;
        bool isRolling = IsRolling;

        if (controller.isGrounded)
        {
            lastGroundedTime = Time.time;

            if (velocity.y < 0f)
                velocity.y = -2f;

            isFalling = false;
        }

        Vector2 moveInput = canMove && !isRolling && !isAttacking
            ? controls.Player.Move.ReadValue<Vector2>()
            : Vector2.zero;

        moveAmount = moveInput.magnitude;

        bool pressed = controls.Player.SprintOrDodge.WasPerformedThisFrame();
        bool released = controls.Player.SprintOrDodge.WasReleasedThisFrame();

        if (pressed)
        {
            actionButtonDownTime = Time.time;
            actionButtonHeld = true;
        }

        bool canSprint = (Time.time - lastGroundedTime) <= coyoteTime;

        if (actionButtonHeld && !isSprinting && canSprint && moveAmount > 0.01f && !isAttacking && !isRolling && !isFalling)
        {
            if (Time.time - actionButtonDownTime >= sprintThreshold)
            {
                if (playerStats == null || playerStats.HasStamina(1f))
                {
                    isSprinting = true;
                }
            }
        }

        if (released)
        {
            float holdTime = Time.time - actionButtonDownTime;

            if (holdTime < sprintThreshold)
            {
                if (!isAttacking && !isRolling && !isFalling)
                    TryDodge(moveInput);
            }

            isSprinting = false;
            actionButtonHeld = false;
        }

        if (isSprinting && moveAmount > 0.01f && playerStats != null && !isRolling && !isFalling)
        {
            playerStats.SpendStaminaPerSecond(sprintStaminaCostPerSecond);

            if (!playerStats.HasStamina(1f))
                isSprinting = false;
        }

        if (!controller.isGrounded && velocity.y < -0.1f)
            isSprinting = false;

        float currentSpeed = moveSpeed * (isSprinting ? sprintMultiplier : 1f);

        if (moveAmount > 0.01f && !isRolling && !isAttacking)
        {
            Vector3 camForward = cam.forward;
            camForward.y = 0f;
            camForward.Normalize();

            Vector3 camRight = cam.right;
            camRight.y = 0f;
            camRight.Normalize();

            Vector3 moveDir = camForward * moveInput.y + camRight * moveInput.x;
            moveDir.Normalize();

            controller.Move(moveDir * currentSpeed * Time.deltaTime);

            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                rotationSpeed * Time.deltaTime
            );
        }

        bool jumpPressed = controls.Player.Jump.WasPerformedThisFrame();

        if (jumpPressed && !isRolling && !isAttacking && !isFalling)
        {
            if (Time.time - lastGroundedTime <= coyoteTime)
            {
                if (playerStats == null || playerStats.HasStamina(jumpStaminaCost))
                {
                    if (playerStats != null)
                        playerStats.SpendStamina(jumpStaminaCost);

                    animator.CrossFadeInFixedTime("Jump", 0.05f, 0, 0.2f);

                    velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        if (animator != null)
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            bool isInJumpAnim = state.IsName("Jump");

            if (!controller.isGrounded &&
                !isRolling &&
                !isInJumpAnim &&
                !isAttacking &&
                velocity.y < fallingSpeedThreshold)
            {
                isFalling = true;
            }

            animator.SetBool("IsFalling", isFalling);
        }
    }

    // Attempt to start a dodge based on input direction
    private void TryDodge(Vector2 moveInput)
    {
        if (isDodging) return;
        if (combat != null && combat.isAttacking) return;
        if (Time.time < lastDodgeTime + dodgeCooldown) return;

        bool canRoll = (Time.time - lastGroundedTime) <= coyoteTime;
        if (!canRoll) return;

        if (playerStats != null && !playerStats.HasStamina(rollStaminaCost))
            return;

        if (playerStats != null)
            playerStats.SpendStamina(rollStaminaCost);

        StartCoroutine(DodgeCoroutine(moveInput));
    }

    // Coroutine executing the dodge movement and timing
    private IEnumerator DodgeCoroutine(Vector2 moveInput)
    {
        isDodging = true;
        canMove = false;
        lastDodgeTime = Time.time;

        if (combat != null)
            combat.canAttack = false;

        Vector3 dodgeDir;

        if (moveInput.sqrMagnitude > 0.01f)
        {
            Vector3 camForward = cam.forward; camForward.y = 0f; camForward.Normalize();
            Vector3 camRight = cam.right; camRight.y = 0f; camRight.Normalize();
            dodgeDir = camForward * moveInput.y + camRight * moveInput.x;
        }
        else
        {
            dodgeDir = transform.forward;
        }

        dodgeDir.y = 0f;
        dodgeDir.Normalize();

        animator.CrossFadeInFixedTime("Roll", 0.05f, 0, 0.2f);

        yield return new WaitForSeconds(rollStartDelay);

        float elapsed = 0f;

        while (elapsed < dodgeDuration)
        {
            float t = elapsed / dodgeDuration;
            float curveValue = dodgeSpeedCurve.Evaluate(t);
            float currentSpeed = (dodgeDistance / dodgeDuration) * curveValue;

            controller.Move(dodgeDir * currentSpeed * Time.deltaTime);

            elapsed += Time.deltaTime;
            yield return null;
        }

        isDodging = false;
        canMove = true;

        if (combat != null)
            combat.canAttack = true;
    }
}
