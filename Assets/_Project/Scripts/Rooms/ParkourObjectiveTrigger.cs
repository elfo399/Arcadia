using UnityEngine;

/// <summary>Reusable trigger or interaction that completes a ParkourRoomRule.</summary>
[DisallowMultipleComponent]
public sealed class ParkourObjectiveTrigger : MonoBehaviour, IInteractable
{
    [SerializeField] private ParkourRoomRule parkourRule;
    [SerializeField] private bool completeOnPlayerEnter = true;
    [SerializeField] private bool completeOnInteract = true;
    [SerializeField] private string prompt = "Activate";

    private void Reset()
    {
        if (parkourRule == null)
            parkourRule = GetComponentInParent<ParkourRoomRule>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (completeOnPlayerEnter && other.CompareTag("Player"))
            CompleteObjective();
    }

    public void Interact(GameObject player)
    {
        if (completeOnInteract)
            CompleteObjective();
    }

    public string GetPrompt()
    {
        ParkourRoomRule rule = ResolveRule();
        return completeOnInteract && rule != null && !rule.IsResolved ? prompt : string.Empty;
    }

    /// <summary>Can also be invoked by an authored puzzle, animation event, or UnityEvent.</summary>
    public void CompleteObjective()
    {
        ResolveRule()?.CompleteTraversal();
    }

    private ParkourRoomRule ResolveRule()
    {
        if (parkourRule == null)
            parkourRule = GetComponentInParent<ParkourRoomRule>();
        return parkourRule;
    }
}
