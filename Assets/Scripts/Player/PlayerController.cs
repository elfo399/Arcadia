using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 4.5f;
    public float sprintMultiplier = 1.6f;
    public float rotationSpeed = 360f;
    public float gravity = -20f;

    [Header("Jump")]
    public float jumpHeight = 1.5f;
    public float coyoteTime = 0.12f;

    [Header("Dodge / Roll")]
    public float dodgeDistance = 2f;
    public float dodgeDuration = 0.25f;
    public float dodgeCooldown = 0.6f;

    // Ritardo per allineare movimento e animazione
    public float rollStartDelay = 0.1f;

    // Curva di velocità della roll (morbida)
    public AnimationCurve dodgeSpeedCurve =
        AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Stamina Costs")]
    public float rollStaminaCost = 30f;
    public float jumpStaminaCost = 15f;
    public float sprintStaminaCostPerSecond = 12f;

    [Header("Falling")]
    // Più vicino a 0 => anim di caduta parte prima
    public float fallingSpeedThreshold = -1.0f;

    [HideInInspector] public bool canMove = true;
    [HideInInspector] public float moveAmount;
    [HideInInspector] public bool isSprinting = false;

    [SerializeField] private Animator animator;

    private CharacterController controller;
    private Vector3 velocity;
    private Transform cam;
    private PlayerControls controls;
    private PlayerCombat combat;

    [HideInInspector] public bool isDodging = false;
    [HideInInspector] public bool isFalling = false;

    public bool IsGrounded => controller != null && controller.isGrounded;
    private float lastDodgeTime = -999f;

    // SprintOrDodge timing
    private float actionButtonDownTime = 0f;
    private bool actionButtonHeld = false;
    private float sprintThreshold = 0.20f;

    // Jump helper
    private float lastGroundedTime = -999f;

    // riferimento alle stats
    private PlayerStats playerStats;

    /// <summary>
    /// Vero se il player sta facendo il roll:
    /// - durante il coroutine (isDodging)
    /// - oppure finché l'animazione "Roll" non ha passato l'80% circa.
    /// </summary>
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

    public bool IsFalling => isFalling;

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

    void OnEnable() => controls.Player.Enable();
    void OnDisable() => controls.Player.Disable();

    void Update()
    {
        bool isAttacking = combat != null && combat.isAttacking;
        bool isRolling   = IsRolling;

        //--------------------------------------------------
        // GROUND CHECK + COYOTE TIME
        //--------------------------------------------------
        if (controller.isGrounded)
        {
            lastGroundedTime = Time.time;

            if (velocity.y < 0f)
                velocity.y = -2f;

            // appena tocchi terra, non sei più in falling
            isFalling = false;
        }

        //--------------------------------------------------
        // INPUT MOVIMENTO (PUOI MUOVERTI ANCHE IN CADUTA)
        //--------------------------------------------------
        Vector2 moveInput = canMove && !isRolling && !isAttacking
            ? controls.Player.Move.ReadValue<Vector2>()
            : Vector2.zero;

        moveAmount = moveInput.magnitude;

        //--------------------------------------------------
        // INPUT SprintOrDodge
        //--------------------------------------------------
        bool pressed  = controls.Player.SprintOrDodge.WasPerformedThisFrame();
        bool released = controls.Player.SprintOrDodge.WasReleasedThisFrame();

        if (pressed)
        {
            actionButtonDownTime = Time.time;
            actionButtonHeld = true;
        }

        // SPRINT: solo terra/coyote + movimento + hold + no attacco/roll/falling
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
                // TAP → roll (ma non rollare se stai attaccando, rollando o cadendo)
                if (!isAttacking && !isRolling && !isFalling)
                    TryDodge(moveInput);
            }

            isSprinting = false;
            actionButtonHeld = false;
        }

        //--------------------------------------------------
        // CONSUMO STAMINA PER SPRINT
        //--------------------------------------------------
        if (isSprinting && moveAmount > 0.01f && playerStats != null && !isRolling && !isFalling)
        {
            playerStats.SpendStaminaPerSecond(sprintStaminaCostPerSecond);

            if (!playerStats.HasStamina(1f))
                isSprinting = false;
        }

        //--------------------------------------------------
        // NO SPRINT SE CHIARAMENTE IN ARIA
        //--------------------------------------------------
        if (!controller.isGrounded && velocity.y < -0.1f)
            isSprinting = false;

        //--------------------------------------------------
        // MOVIMENTO + ROTAZIONE (DISABILITATI SOLO SE ROLL O ATTACCO)
        //--------------------------------------------------
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

        //--------------------------------------------------
        // JUMP (bloccato se roll, attacco o falling)
        //--------------------------------------------------
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

        //--------------------------------------------------
        // GRAVITY
        //--------------------------------------------------
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        //--------------------------------------------------
        // FALLING DETECTION (DOPO LA GRAVITY)
        //--------------------------------------------------
        if (animator != null)
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            bool isInJumpAnim = state.IsName("Jump");

            // 🔴 QUI LA FIX:
            // Sei in aria, non stai rollando, NON sei in Jump, NON stai attaccando
            // e stai cadendo abbastanza veloce → falling
            if (!controller.isGrounded &&
                !isRolling &&
                !isInJumpAnim &&
                !isAttacking &&
                velocity.y < fallingSpeedThreshold)
            {
                isFalling = true;
            }

            //--------------------------------------------------
            // PARAMETRI ANIMATOR
            //--------------------------------------------------
            animator.SetBool("IsFalling", isFalling);
        }
    }

    //--------------------------------------------------
    // ROLL / DODGE
    //--------------------------------------------------
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

    private IEnumerator DodgeCoroutine(Vector2 moveInput)
    {
        isDodging = true;
        canMove = false;
        lastDodgeTime = Time.time;

        // blocca attacchi durante il roll
        if (combat != null)
            combat.canAttack = false;

        //--------------------------------------------------
        // DIREZIONE ROLL
        //--------------------------------------------------
        Vector3 dodgeDir;

        if (moveInput.sqrMagnitude > 0.01f)
        {
            Vector3 camForward = cam.forward; camForward.y = 0f; camForward.Normalize();
            Vector3 camRight   = cam.right;   camRight.y   = 0f; camRight.Normalize();
            dodgeDir = camForward * moveInput.y + camRight * moveInput.x;
        }
        else
        {
            dodgeDir = transform.forward;
        }

        dodgeDir.y = 0f;
        dodgeDir.Normalize();

        //--------------------------------------------------
        // ANIMAZIONE ROLL
        //--------------------------------------------------
        animator.CrossFadeInFixedTime("Roll", 0.05f, 0, 0.2f);

        //--------------------------------------------------
        // ASPETTA micro delay per sincronizzare col tuffo
        //--------------------------------------------------
        yield return new WaitForSeconds(rollStartDelay);

        //--------------------------------------------------
        // MOVIMENTO ROLL (con curva)
        //--------------------------------------------------
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

        // riabilita gli attacchi solo a roll finito
        if (combat != null)
            combat.canAttack = true;
    }
}
