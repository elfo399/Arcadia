using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FloorPortal : MonoBehaviour
{
    [Tooltip("Tag del player autorizzato a usare il portale.")]
    public string playerTag = "Player";
    [Tooltip("Se true, il portale si disattiva dopo l'uso per evitare doppi trigger.")]
    public bool disableAfterUse = true;
    [Header("Quest Events")]
    [SerializeField] private string questTargetId = "next_floor";
    [SerializeField] private string questTargetTag = "floor";

    private bool used = false;

    void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
        else
        {
            BoxCollider bc = gameObject.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(1f, 2f, 1f);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (used) return;
        if (!other.CompareTag(playerTag)) return;

        if (CoreGenerator.Instance != null)
        {
            used = true;
            QuestEvents.Raise(QuestObjectiveEventType.ReachFloor, ResolveQuestTargetId(), questTargetTag);
            CoreGenerator.Instance.NextFloor();
        }

        if (disableAfterUse) gameObject.SetActive(false);
    }

    private string ResolveQuestTargetId()
    {
        return string.IsNullOrWhiteSpace(questTargetId) ? "next_floor" : questTargetId.Trim();
    }
}
