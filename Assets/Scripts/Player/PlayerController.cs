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
    public float rollStartDelay = 0.1f;
    public AnimationCurve dodgeSpeedCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Stamina Costs")]
    public float rollStaminaCost = 30f;
    public float jumpStaminaCost = 15f;
    public float sprintStaminaCostPerSecond = 12f;

    [Header("Falling")]
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
    private float actionButtonDownTime = 0f;
    private bool actionButtonHeld = false;
    private float sprintThreshold = 0.20f;
    private float lastGroundedTime = -999f;
    private PlayerStats playerStats;

    public bool IsRolling
    {
        get
        {
            if (animator == null) return isDodging;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            // Consideriamo rolling se siamo nella coroutine o se l'animazione è in corso
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
        // Se stiamo attaccando, il movimento è gestito (bloccato)
        bool isAttacking = combat != null && combat.isAttacking;
        bool isRolling = IsRolling;

        // Gestione Grounded e Gravità
        if (controller.isGrounded)
        {
            lastGroundedTime = Time.time;
            if (velocity.y < 0f) velocity.y = -2f;
            isFalling = false;
        }

        // Input Movimento: Leggiamo solo se non stiamo attaccando o rollando
        Vector2 moveInput = canMove && !isRolling && !isAttacking
            ? controls.Player.Move.ReadValue<Vector2>()
            : Vector2.zero;

        moveAmount = moveInput.magnitude;

        // Gestione Tasto Sprint/Dodge
        bool pressed = controls.Player.SprintOrDodge.WasPerformedThisFrame();
        bool released = controls.Player.SprintOrDodge.WasReleasedThisFrame();

        if (pressed)
        {
            actionButtonDownTime = Time.time;
            actionButtonHeld = true;
        }

        bool canSprint = (Time.time - lastGroundedTime) <= coyoteTime;

        // Logica Sprint
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

        // Logica Dodge al rilascio
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

        // Consumo Stamina Sprint
        if (isSprinting && moveAmount > 0.01f && playerStats != null && !isRolling && !isFalling)
        {
            playerStats.SpendStaminaPerSecond(sprintStaminaCostPerSecond);
            if (!playerStats.HasStamina(1f)) isSprinting = false;
        }

        if (!controller.isGrounded && velocity.y < -0.1f) isSprinting = false;

        // Movimento Fisico
        float currentSpeed = moveSpeed * (isSprinting ? sprintMultiplier : 1f);

        if (moveAmount > 0.01f && !isRolling && !isAttacking)
        {
            // Direzione basata sulla telecamera
            Vector3 camForward = cam.forward; camForward.y = 0f; camForward.Normalize();
            Vector3 camRight = cam.right; camRight.y = 0f; camRight.Normalize();
            Vector3 moveDir = camForward * moveInput.y + camRight * moveInput.x;
            moveDir.Normalize();

            controller.Move(moveDir * currentSpeed * Time.deltaTime);

            // Rotazione personaggio
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        // Salto
        if (controls.Player.Jump.WasPerformedThisFrame() && !isRolling && !isAttacking && !isFalling)
        {
            if (Time.time - lastGroundedTime <= coyoteTime)
            {
                if (playerStats == null || playerStats.HasStamina(jumpStaminaCost))
                {
                    if (playerStats != null) playerStats.SpendStamina(jumpStaminaCost);
                    animator.CrossFadeInFixedTime("Jump", 0.05f, 0, 0.2f);
                    velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }
        }

        // Applicazione Gravità
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        // Animazioni Falling
        if (animator != null)
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (!controller.isGrounded && !isRolling && !state.IsName("Jump") && !isAttacking && velocity.y < fallingSpeedThreshold)
            {
                isFalling = true;
            }
            animator.SetBool("IsFalling", isFalling);
        }
    }

    private void TryDodge(Vector2 moveInput)
    {
        if (isDodging || (combat != null && combat.isAttacking)) return;
        if (Time.time < lastDodgeTime + dodgeCooldown) return;
        if ((Time.time - lastGroundedTime) > coyoteTime) return;

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

        // Calcolo direzione dodge
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
        dodgeDir.y = 0f; dodgeDir.Normalize();

        // Ruota subito verso la direzione della rollata
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

    // --- NUOVA FUNZIONE: Chiamata dal Combat System per frenare all'istante ---
    public void StopMovementImmediate()
    {
        moveAmount = 0f;
        isSprinting = false;
        
        if (animator != null)
        {
            // Azzera parametri animazione per transizione immediata a Idle/Attack
            animator.SetFloat("Speed", 0f);
            animator.SetBool("IsSprinting", false);
        }
    }
}