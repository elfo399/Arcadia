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

    [HideInInspector] public bool canMove = true;
    [HideInInspector] public float moveAmount;
    [HideInInspector] public bool isSprinting = false;

    [SerializeField] private Animator animator;

    private CharacterController controller;
    private Vector3 velocity;
    private Transform cam;
    private PlayerControls controls;
    private PlayerCombat combat;          // rif. al combat per sapere se stai attaccando

    [HideInInspector] public bool isDodging = false;
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
        //--------------------------------------------------
        // STATO ATTACCO
        //--------------------------------------------------
        bool isAttacking = combat != null && combat.isAttacking;

        //--------------------------------------------------
        // GROUND CHECK + COYOTE TIME
        //--------------------------------------------------
        if (controller.isGrounded)
        {
            lastGroundedTime = Time.time;

            if (velocity.y < 0f)
                velocity.y = -2f;
        }

        //--------------------------------------------------
        // INPUT MOVIMENTO
        //--------------------------------------------------
        Vector2 moveInput = canMove && !isDodging && !isAttacking
            ? controls.Player.Move.ReadValue<Vector2>()
            : Vector2.zero;

        moveAmount = moveInput.magnitude;

        //--------------------------------------------------
        // INPUT SprintOrDodge
        //--------------------------------------------------
        bool pressed = controls.Player.SprintOrDodge.WasPerformedThisFrame();
        bool released = controls.Player.SprintOrDodge.WasReleasedThisFrame();

        if (pressed)
        {
            actionButtonDownTime = Time.time;
            actionButtonHeld = true;
        }

        // SPRINT solo se: a terra o coyote + ti muovi + tieni premuto + NON stai attaccando
        bool canSprint = (Time.time - lastGroundedTime) <= coyoteTime;

        if (actionButtonHeld && !isSprinting && canSprint && moveAmount > 0.01f && !isAttacking)
        {
            if (Time.time - actionButtonDownTime >= sprintThreshold)
            {
                // controlla che ci sia almeno un minimo di stamina
                if (playerStats == null || playerStats.HasStamina(1f))
                {
                    isSprinting = true;
                }
            }
        }

        // RILASCIO tasto
        if (released)
        {
            float holdTime = Time.time - actionButtonDownTime;

            if (holdTime < sprintThreshold)
            {
                // TAP → roll (ma non rollare se stai attaccando)
                if (!isAttacking)
                    TryDodge(moveInput);
            }

            isSprinting = false;
            actionButtonHeld = false;
        }

        //--------------------------------------------------
        // CONSUMO STAMINA PER SPRINT
        //--------------------------------------------------
        if (isSprinting && moveAmount > 0.01f && playerStats != null)
        {
            playerStats.SpendStaminaPerSecond(sprintStaminaCostPerSecond);

            // se finisce la stamina, stop sprint
            if (!playerStats.HasStamina(1f))
            {
                isSprinting = false;
            }
        }

        //--------------------------------------------------
        // NO SPRINT SE CHIARAMENTE IN ARIA
        //--------------------------------------------------
        if (!controller.isGrounded && velocity.y < -0.1f)
            isSprinting = false;

        //--------------------------------------------------
        // MOVIMENTO + ROTAZIONE
        //--------------------------------------------------
        float currentSpeed = moveSpeed * (isSprinting ? sprintMultiplier : 1f);

        if (moveAmount > 0.01f && !isDodging && !isAttacking)
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
        // JUMP (con coyote time + costo stamina)
        // NIENTE JUMP SE ATTACCHI
        //--------------------------------------------------
        bool jumpPressed = controls.Player.Jump.WasPerformedThisFrame();

        if (jumpPressed && !isDodging && !isAttacking)
        {
            if (Time.time - lastGroundedTime <= coyoteTime)
            {
                // richiede stamina per saltare
                if (playerStats == null || playerStats.HasStamina(jumpStaminaCost))
                {
                    if (playerStats != null)
                        playerStats.SpendStamina(jumpStaminaCost);

                    // ANIMAZIONE
                    animator.CrossFadeInFixedTime("Jump", 0.05f, 0, 0.2f);

                    velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
                // se non ha stamina, non salta
            }
        }

        //--------------------------------------------------
        // GRAVITY
        //--------------------------------------------------
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }


    //--------------------------------------------------
    // ROLL / DODGE
    //--------------------------------------------------
    private void TryDodge(Vector2 moveInput)
    {  
        if (isDodging) return;
        if (combat != null && combat.isAttacking) return; // niente roll durante attacco
        if (Time.time < lastDodgeTime + dodgeCooldown) return;

        // Roll SOLO a terra (o coyote)
        bool canRoll = (Time.time - lastGroundedTime) <= coyoteTime;
        if (!canRoll) return;

        // serve stamina per rollare
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
    
        // 👇 DISABILITA ATTACCHI DURANTE TUTTO IL ROLL
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
        // ANIMAZIONE ROLL (istantanea)
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
    
        // 👇 RIABILITA GLI ATTACCHI A ROLL FINITO
        if (combat != null)
            combat.canAttack = true;
    }

}
