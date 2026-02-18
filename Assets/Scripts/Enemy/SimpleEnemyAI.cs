using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(EnemyHealth))]
public class SimpleEnemyAI : MonoBehaviour
{
    private enum AiState
    {
        Idle,
        Chase,
        Windup,
        Recovery,
        Return
    }

    [Header("Setup")]
    public NavMeshAgent agent;
    public Transform playerTarget;
    [SerializeField] private Animator animator;

    [Header("Parametri Base")]
    public float sightRange = 15f;
    public float attackRange = 2f;
    [SerializeField] private float leashRange = 22f;
    [SerializeField] private float repathInterval = 0.15f;
    [SerializeField] private float returnStopDistance = 0.4f;
    [SerializeField] private float preferredCombatDistance = 1.2f;
    [SerializeField] private float personalSpaceRadius = 0.6f;
    [SerializeField] private float attackStartRangeMultiplier = 1.12f;

    [Header("Attacco Melee")]
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float windupDuration = 0.45f;
    [SerializeField] private float hitDelay = 0.55f;
    [SerializeField] private float recoveryDuration = 0.55f;
    [SerializeField] private float attackCooldown = 1.1f;
    [SerializeField] private float attackHitRangeMultiplier = 1.15f;
    [SerializeField] private bool useAnimationEventForHit = false;

    [Header("Sensing")]
    [SerializeField] private bool requireLineOfSight = false;
    [SerializeField] private LayerMask sightBlockMask = ~0;
    [SerializeField] private float eyeHeight = 1.2f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    [Header("Animator")]
    [SerializeField] private string moveSpeedParameter = "MoveSpeed";
    [SerializeField] private string inCombatParameter = "InCombat";
    [SerializeField] private string attackTriggerParameter = "Attack";
    [SerializeField] private bool forceAlwaysAnimate = true;
    [SerializeField] private bool useFirstAnimatorParamFallback = true;

    private bool playerInSight;
    private bool playerInDetectionRange;
    private bool playerInAttackRange;
    private AiState currentState = AiState.Idle;
    private Vector3 spawnPosition;
    private float stateTimer;
    private float attackCooldownTimer;
    private float nextRepathTime;
    private bool hitAppliedInCurrentAttack;
    private bool hasMoveSpeedParam;
    private bool hasInCombatParam;
    private bool hasAttackTriggerParam;
    private string resolvedMoveSpeedParameter;
    private string resolvedInCombatParameter;
    private string resolvedAttackTriggerParameter;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        spawnPosition = transform.position;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null && forceAlwaysAnimate)
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        CacheAnimatorParameters();

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        CapsuleCollider col = GetComponent<CapsuleCollider>();
        col.isTrigger = false;

        // Evita che l'agente cerchi di arrivare esattamente al centro del target.
        if (agent != null)
            agent.stoppingDistance = GetEffectiveCombatDistance();
    }

    private void Start()
    {
        if (agent != null) agent.enabled = false;

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerTarget = p.transform;

        Invoke(nameof(ActivateAgent), 0.5f);
    }

    private void ActivateAgent()
    {
        if (agent == null) return;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            agent.Warp(hit.position);
        }

        agent.enabled = true;
        EnterState(AiState.Idle);
    }

    private void Update()
    {
        if (playerTarget == null || agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        float dt = Time.deltaTime;
        if (attackCooldownTimer > 0f) attackCooldownTimer -= dt;
        stateTimer += dt;

        float distance = Vector3.Distance(transform.position, playerTarget.position);
        float distanceFromSpawn = Vector3.Distance(transform.position, spawnPosition);

        bool hasLos = HasLineOfSightToPlayer();
        playerInDetectionRange = distance <= sightRange;
        playerInSight = requireLineOfSight ? (playerInDetectionRange && hasLos) : playerInDetectionRange;
        playerInAttackRange = distance <= attackRange;

        switch (currentState)
        {
            case AiState.Idle:
                agent.isStopped = true;
                if (playerInDetectionRange)
                    EnterState(AiState.Chase);
                break;

            case AiState.Chase:
                if (distanceFromSpawn > leashRange)
                {
                    EnterState(AiState.Return);
                    break;
                }

                float attackStartRange = Mathf.Max(attackRange, attackRange * attackStartRangeMultiplier);
                if (distance <= attackStartRange && attackCooldownTimer <= 0f)
                {
                    EnterState(AiState.Windup);
                    break;
                }

                if (!playerInDetectionRange)
                {
                    agent.isStopped = true;
                    EnterState(AiState.Idle);
                    break;
                }

                ChasePlayer();
                break;

            case AiState.Windup:
                agent.isStopped = true;
                FacePlayer();

                // Il danno deve arrivare molto tardi (fase impatto), non nel caricamento.
                float minLateHit = windupDuration * 0.90f;
                float actualHitDelay = Mathf.Clamp(Mathf.Max(hitDelay, minLateHit), 0f, windupDuration);
                if (!useAnimationEventForHit && !hitAppliedInCurrentAttack && stateTimer >= actualHitDelay)
                {
                    hitAppliedInCurrentAttack = true;
                    ApplyMeleeHit();
                }

                // Se usi animation event ma manca l'evento nel clip, fallback all'ultimo frame.
                if (useAnimationEventForHit && !hitAppliedInCurrentAttack && stateTimer >= windupDuration)
                {
                    hitAppliedInCurrentAttack = true;
                    ApplyMeleeHit();
                }

                if (stateTimer >= windupDuration)
                {
                    attackCooldownTimer = attackCooldown;
                    EnterState(AiState.Recovery);
                }
                break;

            case AiState.Recovery:
                agent.isStopped = true;
                FacePlayer();
                if (stateTimer >= recoveryDuration)
                {
                    if (distanceFromSpawn > leashRange)
                        EnterState(AiState.Return);
                    else
                        EnterState(playerInDetectionRange ? AiState.Chase : AiState.Idle);
                }
                break;

            case AiState.Return:
                agent.isStopped = false;
                agent.SetDestination(spawnPosition);
                if (Vector3.Distance(transform.position, spawnPosition) <= returnStopDistance)
                {
                    agent.isStopped = true;
                    EnterState(AiState.Idle);
                }
                else if (playerInDetectionRange && distanceFromSpawn <= leashRange * 0.9f)
                {
                    EnterState(AiState.Chase);
                }
                break;
        }

        UpdateAnimator();
    }

    private void ChasePlayer()
    {
        if (playerTarget == null) return;

        Vector3 toEnemy = transform.position - playerTarget.position;
        float planarDist = toEnemy.magnitude;
        if (planarDist <= Mathf.Max(0.1f, personalSpaceRadius))
        {
            // Troppo vicino: non continuare ad avanzare nel player.
            agent.isStopped = true;
            FacePlayer();
            return;
        }

        if (Time.time < nextRepathTime) return;

        nextRepathTime = Time.time + Mathf.Max(0.05f, repathInterval);
        agent.isStopped = false;

        // Insegue mantenendo distanza preferita, non al centro del player.
        float targetDistance = GetEffectiveCombatDistance();
        Vector3 dir = toEnemy.sqrMagnitude > 0.0001f ? toEnemy.normalized : -transform.forward;
        Vector3 desiredPos = playerTarget.position + dir * targetDistance;
        desiredPos.y = transform.position.y;

        agent.SetDestination(desiredPos);
    }

    private void FacePlayer()
    {
        if (playerTarget == null) return;

        Vector3 targetPos = new Vector3(playerTarget.position.x, transform.position.y, playerTarget.position.z);
        Vector3 toTarget = targetPos - transform.position;
        if (toTarget.sqrMagnitude <= 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, 720f * Time.deltaTime);
    }

    private void ApplyMeleeHit()
    {
        if (playerTarget == null) return;

        float maxHitDistance = attackRange * attackHitRangeMultiplier;
        float dist = Vector3.Distance(transform.position, playerTarget.position);
        if (dist > maxHitDistance) return;

        if (requireLineOfSight && !HasLineOfSightToPlayer()) return;

        IDamageable damageable = playerTarget.GetComponent<IDamageable>();
        if (damageable == null)
            damageable = playerTarget.GetComponentInParent<IDamageable>();

        // Fallback robusto: in alcuni setup PlayerStats vive su oggetto singleton
        // separato dal Transform taggato "Player".
        if (damageable == null && PlayerStats.instance != null)
            damageable = PlayerStats.instance;
        if (damageable == null)
            damageable = FindObjectOfType<PlayerStats>();
        if (damageable == null)
        {
            if (debugLogs)
                Debug.LogWarning($"[SimpleEnemyAI] {name} non trova IDamageable del player.");
            return;
        }

        damageable.TakeDamage(attackDamage);
        if (debugLogs)
            Debug.Log($"[SimpleEnemyAI] {name} hit player for {attackDamage} damage.");
    }

    private bool HasLineOfSightToPlayer()
    {
        if (playerTarget == null) return false;

        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Vector3 target = playerTarget.position + Vector3.up * 1.0f;
        Vector3 dir = target - origin;
        float dist = dir.magnitude;
        if (dist <= 0.001f) return true;

        if (!Physics.Raycast(origin, dir / dist, out RaycastHit hit, dist, sightBlockMask, QueryTriggerInteraction.Ignore))
            return true;

        Transform hitTf = hit.transform;
        return hitTf == playerTarget || hitTf.IsChildOf(playerTarget) || playerTarget.IsChildOf(hitTf);
    }

    private void EnterState(AiState state)
    {
        currentState = state;
        stateTimer = 0f;
        if (state == AiState.Windup)
        {
            hitAppliedInCurrentAttack = false;
            if (animator != null && hasAttackTriggerParam)
                animator.SetTrigger(resolvedAttackTriggerParameter);
        }

        if (debugLogs)
            Debug.Log($"[SimpleEnemyAI] {name} => {state}");
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        if (hasMoveSpeedParam)
        {
            float speed = 0f;
            if (agent != null && agent.enabled && !agent.isStopped)
                speed = agent.velocity.magnitude;
            animator.SetFloat(resolvedMoveSpeedParameter, speed);
        }

        if (hasInCombatParam)
        {
            bool inCombat = currentState == AiState.Chase || currentState == AiState.Windup || currentState == AiState.Recovery;
            animator.SetBool(resolvedInCombatParameter, inCombat);
        }
    }

    public void ConfigureFromData(EnemyData data)
    {
        if (data == null) return;
        attackDamage = Mathf.Max(0, data.damage);
    }

    // Chiamabile da Animation Event sul frame di impatto del pugno/arma.
    public void AnimationEvent_ApplyHit()
    {
        if (!useAnimationEventForHit) return;
        if (currentState != AiState.Windup) return;
        if (hitAppliedInCurrentAttack) return;

        hitAppliedInCurrentAttack = true;
        ApplyMeleeHit();
    }

    private void CacheAnimatorParameters()
    {
        hasMoveSpeedParam = false;
        hasInCombatParam = false;
        hasAttackTriggerParam = false;
        resolvedMoveSpeedParameter = string.Empty;
        resolvedInCombatParameter = string.Empty;
        resolvedAttackTriggerParameter = string.Empty;

        if (animator == null) return;

        hasMoveSpeedParam = TryResolveAnimatorParameter(
            AnimatorControllerParameterType.Float,
            new[] { moveSpeedParameter, "Speed", "Move", "Velocity" },
            out resolvedMoveSpeedParameter);

        hasInCombatParam = TryResolveAnimatorParameter(
            AnimatorControllerParameterType.Bool,
            new[] { inCombatParameter, "InCombat", "Combat" },
            out resolvedInCombatParameter);

        hasAttackTriggerParam = TryResolveAnimatorParameter(
            AnimatorControllerParameterType.Trigger,
            new[] { attackTriggerParameter, "Attack", "DoAttack" },
            out resolvedAttackTriggerParameter);

        if (useFirstAnimatorParamFallback)
        {
            if (!hasMoveSpeedParam)
            {
                hasMoveSpeedParam = TryResolveFirstAnimatorParameterOfType(
                    AnimatorControllerParameterType.Float,
                    out resolvedMoveSpeedParameter);
            }

            if (!hasInCombatParam)
            {
                hasInCombatParam = TryResolveFirstAnimatorParameterOfType(
                    AnimatorControllerParameterType.Bool,
                    out resolvedInCombatParameter);
            }

            if (!hasAttackTriggerParam)
            {
                hasAttackTriggerParam = TryResolveFirstAnimatorParameterOfType(
                    AnimatorControllerParameterType.Trigger,
                    out resolvedAttackTriggerParameter);
            }
        }
    }

    private bool TryResolveAnimatorParameter(AnimatorControllerParameterType expectedType, string[] candidates, out string resolvedName)
    {
        resolvedName = string.Empty;
        if (animator == null || candidates == null || candidates.Length == 0) return false;

        for (int c = 0; c < candidates.Length; c++)
        {
            string candidate = candidates[c];
            if (string.IsNullOrWhiteSpace(candidate)) continue;

            var parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == expectedType && parameters[i].name == candidate)
                {
                    resolvedName = candidate;
                    return true;
                }
            }
        }
        return false;
    }

    private bool TryResolveFirstAnimatorParameterOfType(AnimatorControllerParameterType expectedType, out string resolvedName)
    {
        resolvedName = string.Empty;
        if (animator == null) return false;

        var parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == expectedType)
            {
                resolvedName = parameters[i].name;
                return true;
            }
        }

        return false;
    }

    private float GetEffectiveCombatDistance()
    {
        // Deve rimanere dentro attackRange, altrimenti l'enemy resta "fuori range".
        float maxAllowed = Mathf.Max(0.1f, attackRange * 0.7f);
        return Mathf.Clamp(preferredCombatDistance, 0.1f, maxAllowed);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, leashRange);
    }
}
