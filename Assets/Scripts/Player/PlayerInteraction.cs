using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Configurazione")]
    public float interactRange = 2f;
    public LayerMask interactLayer; // Importante: metti i lock/casse su un layer specifico o Default

    private PlayerControls controls;
    private Transform cam;
    private System.Action<InputAction.CallbackContext> interactCallback;

    void Awake()
    {
        controls = new PlayerControls();
        cam = Camera.main != null ? Camera.main.transform : null;
        interactCallback = _ => TryInteract();
    }

    void OnEnable()
    {
        if (controls == null)
            controls = new PlayerControls();
        controls.Player.Enable();
        controls.Player.Interact.performed += interactCallback;
    }

    void OnDisable()
    {
        if (controls != null)
        {
            controls.Player.Interact.performed -= interactCallback;
            controls.Player.Disable();
        }
    }

    void TryInteract()
    {
        // Cerca oggetti interagibili davanti al player
        Collider[] colliders = Physics.OverlapSphere(transform.position + transform.forward * 0.5f, interactRange, interactLayer);

        IInteractable closestInteractable = null;
        float closestDist = Mathf.Infinity;

        foreach (var col in colliders)
        {
            IInteractable interactable = col.GetComponent<IInteractable>();
            if (interactable == null) interactable = col.GetComponentInParent<IInteractable>();

            if (interactable != null)
            {
                float dist = Vector3.Distance(transform.position, col.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestInteractable = interactable;
                }
            }
        }

        // Se abbiamo trovato qualcosa, interagiamo!
        if (closestInteractable != null)
        {
            closestInteractable.Interact(gameObject);
        }
    }

    // Debug visivo in Editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position + transform.forward * 0.5f, interactRange);
    }
}
