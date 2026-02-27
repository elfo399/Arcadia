using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WeaponWorldPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private WeaponItem weapon;
    [SerializeField] private string instanceId;
    [SerializeField] private string prompt = "Raccogli arma";

    public void Initialize(WeaponItem item, string id)
    {
        weapon = item;
        instanceId = id;
    }

    public void Interact(GameObject player)
    {
        TryPickup(player);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || !other.CompareTag("Player")) return;
        TryPickup(other.gameObject);
    }

    private void TryPickup(GameObject player)
    {
        if (weapon == null || string.IsNullOrWhiteSpace(instanceId) || player == null) return;

        var inventory = player.GetComponent<PlayerInventory>();
        if (inventory == null) inventory = player.GetComponentInParent<PlayerInventory>();
        if (inventory == null) return;

        if (inventory.HasWeaponInstanceInInventoryPublic(instanceId, weapon))
        {
            Destroy(gameObject);
            return;
        }

        inventory.AddWeaponInstance(weapon, instanceId);
        Destroy(gameObject);
    }

    public string GetPrompt()
    {
        return prompt;
    }
}
