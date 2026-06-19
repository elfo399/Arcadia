using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MagicProjectile : MonoBehaviour
{
    [SerializeField] private int damage = 10;
    [SerializeField] private float speed = 18f;
    [SerializeField] private float lifetime = 4f;
    [SerializeField] private float hitEnableDelay = 0.05f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField] private bool forceIgnoreRaycastLayer = true;

    private Transform owner;
    private Vector3 direction;
    private bool initialized;
    private float spawnTime;
    private string sourceLabel = "Projectile";
    private bool isCriticalHit = false;

    public void Initialize(
        Transform ownerTransform,
        Vector3 fireDirection,
        int projectileDamage,
        float projectileSpeed,
        float projectileLifetime,
        LayerMask mask,
        string source = "Projectile",
        bool critical = false)
    {
        owner = ownerTransform;
        direction = fireDirection.sqrMagnitude > 0.0001f ? fireDirection.normalized : transform.forward;
        damage = Mathf.Max(0, projectileDamage);
        speed = Mathf.Max(0.1f, projectileSpeed);
        lifetime = Mathf.Max(0.1f, projectileLifetime);
        hitMask = mask;
        sourceLabel = string.IsNullOrWhiteSpace(source) ? "Projectile" : source;
        isCriticalHit = critical;
        spawnTime = Time.time;
        initialized = true;

        // Orienta il prefab verso la direzione di volo.
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    private void Awake()
    {
        if (forceIgnoreRaycastLayer)
        {
            int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreRaycastLayer >= 0)
                SetLayerRecursively(gameObject, ignoreRaycastLayer);
        }

        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null) return;
        root.layer = layer;
        Transform t = root.transform;
        for (int i = 0; i < t.childCount; i++)
        {
            var child = t.GetChild(i);
            if (child != null)
                SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void Update()
    {
        if (!initialized) return;

        transform.position += direction * speed * Time.deltaTime;

        if (Time.time >= spawnTime + lifetime)
            Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!initialized || other == null) return;
        if (Time.time < spawnTime + Mathf.Max(0f, hitEnableDelay)) return;

        Transform hitTransform = other.transform;
        if (owner != null && (hitTransform == owner || hitTransform.IsChildOf(owner) || owner.IsChildOf(hitTransform)))
            return;

        int otherLayerMask = 1 << other.gameObject.layer;
        if ((hitMask.value & otherLayerMask) == 0)
            return;

        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable == null)
            damageable = other.GetComponentInParent<IDamageable>();

        if (damageable != null)
        {
            if (damageable is PlayerStats playerStats)
            {
                Vector3? sourcePosition = owner != null ? owner.position : transform.position;
                playerStats.TakeDamage(damage, WeaponItem.DamageType.Magic, sourcePosition, owner);
            }
            else
            {
                damageable.TakeDamage(damage);
            }
            string ownerName = owner != null ? owner.name : "UnknownOwner";
            string targetName = other != null ? other.name : "UnknownTarget";
            string critTag = isCriticalHit ? " CRIT" : string.Empty;
            Debug.Log($"[PlayerDamage] {sourceLabel} | Dmg:{damage}{critTag} -> Target:{targetName} | Owner:{ownerName}");
        }
        else
        {
            // Ignore non-damageable trigger volumes (enemy detection, lock helpers, etc.).
            // This prevents premature projectile vanish while lock-on is active.
            if (other.isTrigger)
                return;
        }

        if (impactVfxPrefab != null)
            Instantiate(impactVfxPrefab, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}
