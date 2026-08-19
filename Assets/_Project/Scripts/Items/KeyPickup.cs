using UnityEngine;

public class KeyPickup : MonoBehaviour
{
    [SerializeField] private ItemData keyItem;
    public float rotateSpeed = 100f;

    void Update()
    {
        // Rotazione estetica
        transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerStats stats = other.GetComponent<PlayerStats>();
            if (stats == null) stats = other.GetComponentInParent<PlayerStats>();

            PlayerInventory inventory=stats!=null?stats.GetComponent<PlayerInventory>():null;
            ItemData resolvedKey=keyItem;
            if(resolvedKey==null&&inventory!=null&&inventory.ItemDatabase!=null)
                inventory.ItemDatabase.TryGetItem("dungeon-key",out resolvedKey);
            if (stats != null && inventory!=null && resolvedKey!=null && inventory.TryAddItem(resolvedKey,1))
            {
                QuestEvents.Raise(QuestObjectiveEventType.CollectItem, gameObject.name, "key");
                // Qui puoi mettere un suono: AudioManager.Play("KeyPickup");
                Destroy(gameObject);
            }
        }
    }
}
