using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 4.5f;
    public float rotationSpeed = 360f;
    public float gravity = -9.81f;

    [HideInInspector] public bool canMove = true;
    [HideInInspector] public float moveAmount; // 0 = fermo, 1 = input pieno

    private CharacterController controller;
    private Vector3 velocity;
    private Transform cam;
    private PlayerControls controls;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        cam = Camera.main.transform;

        controls = new PlayerControls(); // usa lo stesso asset delle input actions
    }

    void OnEnable()
    {
        controls.Player.Enable();
    }

    void OnDisable()
    {
        controls.Player.Disable();
    }

    void Update()
    {
        Vector2 moveInput = Vector2.zero;

        if (canMove)
        {
            // Legge l'azione "Move" (WASD + analogico sinistro)
            moveInput = controls.Player.Move.ReadValue<Vector2>();
        }

        // Salviamo quanto è "forte" l'input (serve per le animazioni)
        moveAmount = moveInput.magnitude; // tra 0 e 1

        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);

        if (inputDir.sqrMagnitude > 0.01f)
        {
            // Direzioni base della camera sul piano XZ
            Vector3 camForward = cam.forward;
            camForward.y = 0f;
            camForward.Normalize();

            Vector3 camRight = cam.right;
            camRight.y = 0f;
            camRight.Normalize();

            // Movimento relativo alla camera
            Vector3 moveDir = camForward * moveInput.y + camRight * moveInput.x;
            moveDir.y = 0f;
            moveDir.Normalize();

            // Muovi il personaggio
            controller.Move(moveDir * moveSpeed * Time.deltaTime);

            // Ruota il corpo solo se non stai andando chiaramente indietro
            float dot = Vector3.Dot(moveDir, transform.forward);

            if (dot > -0.2f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRot,
                    rotationSpeed * Time.deltaTime
                );
            }
        }

        // Gravità
        if (controller.isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
