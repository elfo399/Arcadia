using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WeaponThrowProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifetime = 2.5f;
    [SerializeField] private float hitEnableDelay = 0.08f;
    [SerializeField] private float minArmingDistance = 0.9f;
    [SerializeField] private bool useBallisticArc = true;
    [SerializeField] private float gravityMultiplier = 1f;
    [SerializeField] private float spinSpeed = 720f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private bool forceIgnoreRaycastLayer = true;
    [SerializeField] private bool logHits = true;

    private Transform owner;
    private WeaponItem weapon;
    private string instanceId;
    private Vector3 direction;
    private int bladeDamage;
    private float bladeChance;
    private float handleMultiplier;
    private float breakChance;
    private float spawnTime;
    private bool initialized;
    private bool resolved;
    private Vector3 spawnPosition;
    private Collider projectileCollider;
    private bool collisionsArmed;
    private Vector3 currentVelocity;

    public void Initialize(
        Transform ownerTransform,
        WeaponItem sourceWeapon,
        string sourceInstanceId,
        Vector3 fireDirection,
        int computedBladeDamage,
        float projectileSpeed,
        float projectileLifetime,
        LayerMask mask)
    {
        owner = ownerTransform;
        weapon = sourceWeapon;
        instanceId = sourceInstanceId;
        direction = fireDirection.sqrMagnitude > 0.0001f ? fireDirection.normalized : transform.forward;
        bladeDamage = Mathf.Max(1, computedBladeDamage);
        speed = Mathf.Max(0.1f, projectileSpeed);
        lifetime = Mathf.Max(0.1f, projectileLifetime);
        hitMask = mask;
        bladeChance = weapon != null ? Mathf.Clamp01(weapon.throwBladeHitChance) : 0.65f;
        handleMultiplier = weapon != null ? Mathf.Clamp(weapon.throwHandleDamageMultiplier, 0.1f, 1f) : 0.5f;
        breakChance = weapon != null ? Mathf.Clamp01(weapon.throwBreakChance) : 0.1f;
        spawnTime = Time.time;
        spawnPosition = transform.position;
        currentVelocity = direction * speed;
        collisionsArmed = false;
        if (projectileCollider != null)
            projectileCollider.enabled = false;
        initialized = true;
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    private void Awake()
    {
        if (forceIgnoreRaycastLayer)
        {
            int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreRaycastLayer >= 0) SetLayerRecursively(gameObject, ignoreRaycastLayer);
        }

        projectileCollider = GetComponent<Collider>();
        if (projectileCollider == null)
        {
            Debug.LogError("[WeaponThrowProjectile] Collider mancante sul prefab.");
            enabled = false;
            return;
        }
        if (!projectileCollider.isTrigger)
        {
            Debug.LogWarning("[WeaponThrowProjectile] Il collider del projectile dovrebbe essere Trigger.");
        }

        // Prefab-driven: il Rigidbody deve essere configurato nel prefab.
        var rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("[WeaponThrowProjectile] Rigidbody mancante sul prefab. Aggiungilo nel prefab (isKinematic=true, useGravity=false).");
            enabled = false;
            return;
        }
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null) return;
        root.layer = layer;
        Transform t = root.transform;
        for (int i = 0; i < t.childCount; i++)
        {
            var child = t.GetChild(i);
            if (child != null) SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void Update()
    {
        if (!initialized || resolved) return;

        if (!collisionsArmed && Time.time >= spawnTime + Mathf.Max(0f, hitEnableDelay))
        {
            collisionsArmed = true;
            if (projectileCollider != null)
                projectileCollider.enabled = true;
        }

        if (useBallisticArc)
        {
            currentVelocity += Physics.gravity * Mathf.Max(0f, gravityMultiplier) * Time.deltaTime;
            transform.position += currentVelocity * Time.deltaTime;
            if (currentVelocity.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(currentVelocity.normalized, Vector3.up);
            if (spinSpeed > 0f)
                transform.Rotate(Vector3.forward * spinSpeed * Time.deltaTime, Space.Self);
        }
        else
        {
            transform.position += direction * speed * Time.deltaTime;
            float spin = spinSpeed > 0f ? spinSpeed : 850f;
            transform.Rotate(Vector3.right * (spin * Time.deltaTime), Space.Self);
        }

        if (Time.time >= spawnTime + lifetime)
            ResolveAndDrop(null, transform.position);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!initialized || resolved || other == null) return;
        if (!collisionsArmed) return;
        if ((transform.position - spawnPosition).sqrMagnitude < Mathf.Max(0f, minArmingDistance) * Mathf.Max(0f, minArmingDistance)) return;

        Transform hitTransform = other.transform;
        if (owner != null && (hitTransform == owner || hitTransform.IsChildOf(owner) || owner.IsChildOf(hitTransform)))
            return;

        int otherLayerMask = 1 << other.gameObject.layer;
        if ((hitMask.value & otherLayerMask) == 0)
            return;

        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable == null)
            damageable = other.GetComponentInParent<IDamageable>();

        ResolveAndDrop(damageable, transform.position);
    }

    private void ResolveAndDrop(IDamageable damageable, Vector3 dropPos)
    {
        if (resolved) return;
        resolved = true;

        bool bladeHit = Random.value <= bladeChance;
        int finalDamage = bladeHit
            ? bladeDamage
            : Mathf.Max(1, Mathf.RoundToInt(bladeDamage * handleMultiplier));

        if (damageable != null)
            damageable.TakeDamage(finalDamage);

        if (logHits)
        {
            string weaponName = weapon != null && !string.IsNullOrWhiteSpace(weapon.weaponName) ? weapon.weaponName : "ThrownWeapon";
            string targetName = damageable != null ? damageable.ToString() : "None";
            string hitPart = bladeHit ? "Blade" : "Handle";
            Debug.Log($"[PlayerDamage] Throw {weaponName} | Hit:{hitPart} | Dmg:{finalDamage} | Target:{targetName}");
        }

        bool broken = Random.value <= breakChance;
        if (logHits)
        {
            string weaponName = weapon != null && !string.IsNullOrWhiteSpace(weapon.weaponName) ? weapon.weaponName : "Weapon";
            Debug.Log($"[ThrowDrop] {weaponName} | Broken:{broken} | Pos:{dropPos}");
        }
        if (!broken && weapon != null && !string.IsNullOrWhiteSpace(instanceId))
            SpawnPickup(dropPos);

        Destroy(gameObject);
    }

    private void SpawnPickup(Vector3 pos)
    {
        Vector3 spawnPos = ResolveDropPosition(pos);
        var pickupGo = new GameObject($"{weapon.name}_Dropped");
        pickupGo.transform.position = spawnPos;
        // Non usare la rotazione del projectile (spesso verticale): per il drop fisico è più stabile.
        pickupGo.transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        int interactLayer = LayerMask.NameToLayer("Interactable");
        if (interactLayer >= 0) pickupGo.layer = interactLayer;

        // Collider fisico: serve per collisione reale col terreno.
        var physicsCollider = pickupGo.AddComponent<BoxCollider>();
        Vector3 colliderSize = weapon != null ? weapon.droppedPickupColliderSize : new Vector3(0.7f, 0.12f, 0.22f);
        Vector3 colliderCenter = weapon != null ? weapon.droppedPickupColliderCenter : new Vector3(0f, 0.05f, 0f);
        physicsCollider.size = new Vector3(
            Mathf.Max(0.02f, colliderSize.x),
            Mathf.Max(0.02f, colliderSize.y),
            Mathf.Max(0.02f, colliderSize.z));
        physicsCollider.center = colliderCenter;

        var rb = pickupGo.AddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.mass = weapon != null ? Mathf.Max(0.01f, weapon.droppedPickupMass) : 1f;
        rb.drag = weapon != null ? Mathf.Max(0f, weapon.droppedPickupLinearDrag) : 0.2f;
        rb.angularDrag = weapon != null ? Mathf.Max(0f, weapon.droppedPickupAngularDrag) : 0.25f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Evita clip iniziale: alza il root considerando center+halfHeight del collider fisico.
        float halfHeight = Mathf.Max(0.01f, physicsCollider.size.y * 0.5f);
        float minBottomY = spawnPos.y + physicsCollider.center.y - halfHeight;
        if (minBottomY < spawnPos.y + 0.02f)
        {
            float lift = (spawnPos.y + 0.02f) - minBottomY;
            pickupGo.transform.position += Vector3.up * lift;
        }

        var pickup = pickupGo.AddComponent<WeaponWorldPickup>();
        pickup.Initialize(weapon, instanceId);

        // Trigger interazione separato, così il player la può raccogliere mentre mantiene fisica reale.
        var triggerGo = new GameObject("InteractTrigger");
        triggerGo.transform.SetParent(pickupGo.transform, false);
        triggerGo.layer = pickupGo.layer;
        var trigger = triggerGo.AddComponent<SphereCollider>();
        trigger.radius = 0.45f;
        trigger.isTrigger = true;

        if (weapon.modelPrefab != null)
        {
            var model = Instantiate(weapon.modelPrefab, pickupGo.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(weapon.droppedModelLocalEuler);
            model.transform.localScale = Vector3.one;

            // Il collider del pickup è quello root, quindi rimuovo collider dal model per evitare conflitti.
            var modelColliders = model.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < modelColliders.Length; i++)
            {
                if (modelColliders[i] != null) Destroy(modelColliders[i]);
            }

            // Se il prefab non mostra renderer visibili, aggiunge marker fallback.
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                CreateFallbackMarker(pickupGo.transform);
        }
        else
        {
            CreateFallbackMarker(pickupGo.transform);
        }

        if (rb != null)
        {
            float forwardImpulse = weapon != null ? Mathf.Max(0f, weapon.droppedForwardImpulse) : 1.8f;
            float upImpulse = weapon != null ? Mathf.Max(0f, weapon.droppedUpImpulse) : 0.6f;
            Vector3 torque = weapon != null ? weapon.droppedInitialTorque : new Vector3(6f, 0f, 2f);

            Vector3 impulse = direction * forwardImpulse + Vector3.up * upImpulse;
            rb.AddForce(impulse, ForceMode.Impulse);
            rb.AddTorque(torque, ForceMode.Impulse);
        }
    }

    private static Vector3 ResolveDropPosition(Vector3 requestedPos)
    {
        Vector3 from = requestedPos + Vector3.up * 2.0f;
        if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            float minY = hit.point.y + 0.08f;
            // Realistico: non forzare a terra se la posizione è già sopra il suolo.
            // Alziamo solo quando rischia clipping sotto terreno.
            if (requestedPos.y < minY)
                requestedPos.y = minY;
            return requestedPos;
        }
        return requestedPos + Vector3.up * 0.12f;
    }

    private static void CreateFallbackMarker(Transform parent)
    {
        var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "Marker";
        marker.transform.SetParent(parent, false);
        marker.transform.localScale = new Vector3(0.2f, 0.08f, 0.6f);
        var markerCol = marker.GetComponent<Collider>();
        if (markerCol != null) Destroy(markerCol);
    }
}
