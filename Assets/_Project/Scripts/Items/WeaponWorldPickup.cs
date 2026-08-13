using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WeaponWorldPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private WeaponItem weapon;
    [SerializeField] private string instanceId;
    [SerializeField, Min(0)] private int upgradeLevel;
    [SerializeField] private string prompt = "Raccogli arma";

    public void Initialize(WeaponItem item, string id, int level = 0)
    {
        weapon = item;
        instanceId = id;
        upgradeLevel = Mathf.Max(0, level);
    }

    public void Interact(GameObject player)
    {
        TryPickup(player);
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

        if (inventory.TryAddWeaponInstance(weapon, instanceId, upgradeLevel, save: true))
            Destroy(gameObject);
    }

    public string GetPrompt()
    {
        return prompt;
    }
}
