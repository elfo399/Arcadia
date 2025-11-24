using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    // Target transform the camera follows
    public Transform target;
    // Distance behind the target
    public float distance = 6f;
    // Height offset above the target
    public float height = 3f;
    // Horizontal look sensitivity
    public float sensitivityX = 120f;
    // Vertical look sensitivity
    public float sensitivityY = 80f;
    // Minimum pitch angle
    public float minPitch = -20f;
    // Maximum pitch angle
    public float maxPitch = 60f;

    // Current yaw rotation
    private float yaw;
    // Current pitch rotation
    private float pitch;

    // Input controls for camera movement
    private PlayerControls controls;

    // Initialize input bindings
    void Awake()
    {
        controls = new PlayerControls();
    }

    // Enable camera input
    void OnEnable()
    {
        controls.Player.Enable();
    }

    // Disable camera input
    void OnDisable()
    {
        controls.Player.Disable();
    }

    // Initialize orientation based on the current transform
    void Start()
    {
        if (target == null) return;

        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;
    }

    // Apply camera rotation and follow the target
    void LateUpdate()
    {
        if (target == null) return;

        Vector2 lookInput = controls.Player.Look.ReadValue<Vector2>();

        float inputX = lookInput.x;
        float inputY = lookInput.y;

        yaw   += inputX * sensitivityX * Time.deltaTime;
        pitch -= inputY * sensitivityY * Time.deltaTime;
        pitch  = Mathf.Clamp(pitch, minPitch, maxPitch);

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 offset = rot * new Vector3(0f, 0f, -distance);
        Vector3 desiredPos = target.position + Vector3.up * height + offset;

        transform.position = desiredPos;
        transform.LookAt(target.position + Vector3.up * 1.5f);
    }
}
