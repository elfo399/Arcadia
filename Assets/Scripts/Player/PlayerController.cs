using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float sprintMultiplier = 1.5f;
    public float rotationSpeed = 720f;
    public float gravity = -20f;

    [Header("Jump")]
    public float jumpHeight = 1.2f;
    public float coyoteTime = 0.15f;

    [Header("Dodge / Roll")]
    public float dodgeDistance = 4f;     
    public float dodgeDuration = 0.6f;   
    public float dodgeCooldown = 0.8f;   
    public float rollStartDelay = 0.05f; 
    public AnimationCurve dodgeSpeedCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0));

    [Header("Stamina Costs")]
    public float rollStaminaCost = 25f;
    public float jumpStaminaCost = 15f;
    public float sprintStaminaCostPerSecond = 10f;

    [Header("Falling")]
    public float fallingSpeedThreshold = -2.0f;

    // Flags
    [HideInInspector] public bool canMove = true;
    [HideInInspector] public float moveAmount;
    [HideInInspector] public bool isSprinting = false;
    [HideInInspector] public bool isDodging = false; // Letto dal LockSystem
    [HideInInspector] public bool isFalling = false;

    [SerializeField] private Animator animator;
    private CharacterController controller;
    private PlayerControls controls;
    private PlayerCombat combat;
    private PlayerStats playerStats;
    private Transform cam;

    private Vector3 velocity;
    private float lastDodgeTime = -999f;
    private float lastGroundedTime = -999f;
    private float actionButtonDownTime = 0f;
    private bool actionButtonHeld = false;
    private float sprintThreshold = 0.25f;

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
        controller = GetComponent<CharacterController>();
        cam = Camera.main.transform;
        controls = new PlayerControls();

        if (animator == null) animator = GetComponentInChildren<Animator>();
        playerStats = GetComponent<PlayerStats>();
        combat = GetComponent<PlayerCombat>();
    }

    void OnEnable() => controls.Player.Enable();
    void OnDisable() => controls.Player.Disable();

    void Update()
    {
        bool isAttacking = combat != null && combat.isAttacking;
        bool isRolling = IsRolling;

        if (controller.isGrounded)
        {
            lastGroundedTime = Time.time;
            if (velocity.y < 0f) velocity.y = -2f; 
            isFalling = false;
        }

        Vector2 moveInput = Vector2.zero;
        if (canMove && !isRolling && !isAttacking)
        {
            moveInput = controls.Player.Move.ReadValue<Vector2>();
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

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        UpdateFallingAnimator();
    }

    void HandleMovement(Vector2 moveInput)
    {
        float targetSpeed = moveSpeed * (isSprinting ? sprintMultiplier : 1f);
        
        Vector3 camForward = cam.forward; camForward.y = 0f; camForward.Normalize();
        Vector3 camRight = cam.right; camRight.y = 0f; camRight.Normalize();
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

    void HandleSprintAndDodgeInput(Vector2 moveInput)
    {
        bool pressed = controls.Player.SprintOrDodge.WasPerformedThisFrame();
        bool released = controls.Player.SprintOrDodge.WasReleasedThisFrame();
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
        if (controls.Player.Jump.WasPerformedThisFrame() && !IsRolling && combat != null && !combat.isAttacking)
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
            bool fallingCondition = !controller.isGrounded && !IsRolling && !state.IsName("Jump") && velocity.y < fallingSpeedThreshold;
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
}