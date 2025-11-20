using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 6f;
    public float height = 3f;
    public float sensitivityX = 120f;
    public float sensitivityY = 80f;
    public float minPitch = -20f;
    public float maxPitch = 60f;

    private float yaw;
    private float pitch;

    private PlayerControls controls;

    void Awake()
    {
        controls = new PlayerControls();
    }

    void OnEnable()
    {
        controls.Player.Enable();
    }

    void OnDisable()
    {
        controls.Player.Disable();
    }

    void Start()
    {
        if (target == null) return;

        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // INPUT DELLA CAMERA (mouse + stick destro)
        Vector2 lookInput = controls.Player.Look.ReadValue<Vector2>();

        float inputX = lookInput.x;
        float inputY = lookInput.y;

        // Applica movimento
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
