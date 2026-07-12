using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Configurazione")]
    public float interactRange = 2f;
    public LayerMask interactLayer; // Importante: metti i lock/casse su un layer specifico o Default

    private PlayerController playerController;
    private bool isSubscribed;
    private System.Action<InputAction.CallbackContext> interactCallback;

    void Awake()
    {
        playerController = GetComponent<PlayerController>();
        interactCallback = _ => TryInteract();
    }

    void OnEnable()
    {
        TrySubscribeToSharedControls();
    }

    void OnDisable()
    {
        UnsubscribeFromSharedControls();
    }

    void Update()
    {
        // Se l'ordine di inizializzazione fa arrivare PlayerController dopo, ci agganciamo qui.
        if (!isSubscribed)
            TrySubscribeToSharedControls();
    }

    private void TrySubscribeToSharedControls()
    {
        if (isSubscribed) return;
        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (playerController == null || playerController.Controls == null) return;

        playerController.Controls.Player.Interact.performed -= interactCallback;
        playerController.Controls.Player.Interact.performed += interactCallback;
        isSubscribed = true;
    }

    private void UnsubscribeFromSharedControls()
    {
        if (!isSubscribed) return;
        if (playerController != null && playerController.Controls != null)
            playerController.Controls.Player.Interact.performed -= interactCallback;
        isSubscribed = false;
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
            if (closestInteractable is Component component)
                QuestEvents.Raise(QuestObjectiveEventType.Interact, component.gameObject.name, component.gameObject.tag);
        }
    }

    // Debug visivo in Editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position + transform.forward * 0.5f, interactRange);
    }
}
