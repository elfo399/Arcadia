using UnityEngine;

/// <summary>Reusable trigger that completes a ParkourRoomRule when the player reaches it.</summary>
[DisallowMultipleComponent]
public sealed class ParkourObjectiveTrigger : MonoBehaviour
{
    [SerializeField] private ParkourRoomRule parkourRule;
    [SerializeField] private bool completeOnPlayerEnter = true;

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

    /// <summary>Can also be invoked by an authored puzzle, animation event, or UnityEvent.</summary>
    public void CompleteObjective()
    {
        if (parkourRule == null)
            parkourRule = GetComponentInParent<ParkourRoomRule>();
        if (parkourRule != null)
            parkourRule.CompleteTraversal();
    }
}
