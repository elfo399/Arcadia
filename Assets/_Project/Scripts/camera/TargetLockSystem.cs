using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class TargetLockSystem : MonoBehaviour
{
    [Header("Riferimenti")]
    public CinemachineFreeLook freeLookCamera;
    public CinemachineVirtualCamera lockOnCamera;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private Transform playerCameraTarget;
    public Transform playerModel;

    [Header("Ricerca")]
    public float scanRadius = 20f;
    public LayerMask enemyLayer;
    public float maxLockDistance = 25f;

    [Header("Switching")]
    public float switchCooldown = 0.2f;
    public float switchThreshold = 0.5f;

    [Header("Parametri Movimento")]
    public float rotationSpeed = 20f;

    [Header("Fallback Lock Point")]
    [SerializeField] private float fallbackLockPointOffset = 0f;
    [SerializeField, Range(0.35f, 1f)] private float fallbackLockPointBoundsHeight = 0.55f;
    [SerializeField] private Shader targetIconShader;

    [Header("Debug")]
    public bool isLockedOn = false;
    public Transform currentTarget;

    public Transform CurrentLockPoint => GetCurrentLookTarget();
    public Vector3 CurrentLockAimPoint => isLockedOn && currentTarget != null ? GetTargetIconWorldPosition() : Vector3.zero;

    private Camera mainCam;
    private Transform currentLookTarget;
    private Texture2D runtimeDotTexture;
    private GameObject worldTargetIcon;
    private SpriteRenderer worldTargetIconRenderer;
    private Sprite runtimeDotSprite;
    private Material worldTargetIconMaterial;
    private float lastSwitchTime;
    private PlayerController playerController;
    private readonly List<Transform> lockPointCandidates = new List<Transform>();
    private readonly List<EnemyHealth> candidateEnemies = new List<EnemyHealth>();
    private readonly HashSet<EnemyHealth> scannedEnemies = new HashSet<EnemyHealth>();

    void Awake()
    {
        mainCam = ResolveGameplayCamera();
        playerController = GetComponentInParent<PlayerController>();
        HideTargetIcon();
    }

    void OnEnable()
    {
        if (!isLockedOn)
            HideTargetIcon();
    }

    void Start()
    {
        if (playerController != null && playerController.Controls != null)
            playerController.Controls.Player.LockOn.performed += HandleLockOnInput;

        ResolveSceneCameraReferences();
        if (freeLookCamera != null)
            freeLookCamera.Priority = 10;
        if (lockOnCamera != null)
            lockOnCamera.Priority = 0;
        if (!isLockedOn)
            HideTargetIcon();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        HideTargetIcon(false);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (playerController != null && playerController.Controls != null)
            playerController.Controls.Player.LockOn.performed -= HandleLockOnInput;

        DestroyRuntimeTargetIcon();
    }

    void Update()
    {
        if (playerController != null && playerController.IsGameplayInputBlocked)
            return;

        if (!isLockedOn)
            return;

        if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
        {
            StopLockOn();
            return;
        }

        if (Vector3.Distance(transform.position, CurrentLockAimPoint) > maxLockDistance)
        {
            StopLockOn();
            return;
        }

        HandleRotation();
        HandleTargetSwitching();
    }

    void LateUpdate()
    {
        UpdateTargetUI();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        mainCam = ResolveGameplayCamera();
        playerController = GetComponentInParent<PlayerController>();
        ResolveSceneCameraReferences();
        StopLockOn();
    }

    private void ResolveSceneCameraReferences()
    {
        if (playerController == null)
            playerController = GetComponentInParent<PlayerController>();

        SceneRuntimeReferences sceneReferences = SceneRuntimeReferences.Current;
        if (sceneReferences != null)
        {
            if (sceneReferences.GameplayCamera != null)
                gameplayCamera = sceneReferences.GameplayCamera;
            if (sceneReferences.FreeLookCamera != null)
                freeLookCamera = sceneReferences.FreeLookCamera;
            if (sceneReferences.LockOnCamera != null)
                lockOnCamera = sceneReferences.LockOnCamera;
        }

        mainCam = gameplayCamera;

        Transform followTarget = playerController != null ? playerController.transform : transform;
        Transform lookAtTarget = playerCameraTarget != null ? playerCameraTarget : followTarget;

        if (freeLookCamera != null)
        {
            freeLookCamera.Follow = followTarget;
            freeLookCamera.LookAt = lookAtTarget != null ? lookAtTarget : followTarget;
        }

        if (lockOnCamera != null)
        {
            lockOnCamera.Follow = followTarget;
            lockOnCamera.LookAt = isLockedOn && currentTarget != null ? GetCurrentLookTarget() : null;
        }
    }

    private Camera ResolveGameplayCamera()
    {
        SceneRuntimeReferences sceneReferences = SceneRuntimeReferences.Current;
        if (sceneReferences != null && sceneReferences.GameplayCamera != null)
            gameplayCamera = sceneReferences.GameplayCamera;

        return gameplayCamera;
    }

    private void HandleTargetSwitching()
    {
        if (Time.time < lastSwitchTime + switchCooldown) return;
        if (playerController == null || playerController.Controls == null) return;

        Vector2 lookInput = playerController.Controls.Player.Look.ReadValue<Vector2>();
        if (Mathf.Abs(lookInput.x) > switchThreshold)
        {
            SwitchTarget(Mathf.Sign(lookInput.x));
            lastSwitchTime = Time.time;
        }
    }

    private void SwitchTarget(float direction)
    {
        if (mainCam == null)
            mainCam = ResolveGameplayCamera();

        if (mainCam == null || currentTarget == null)
            return;

        TrySwitchLockCandidate(direction);
    }

    private void HandleLockOnInput(InputAction.CallbackContext context)
    {
        if (playerController != null && playerController.IsGameplayInputBlocked)
            return;

        if (isLockedOn) StopLockOn();
        else FindAndLockTarget();
    }

    private void FindAndLockTarget()
    {
        if (mainCam == null)
            mainCam = ResolveGameplayCamera();

        if (mainCam == null)
            return;

        if (!TryFindAndLockBestCandidate())
            RecenterCamera();
    }

    private bool TryFindAndLockBestCandidate()
    {
        float bestScore = Mathf.Infinity;
        Transform bestRoot = null;
        Transform bestPoint = null;
        CollectCandidateEnemies(candidateEnemies);

        for (int enemyIndex = 0; enemyIndex < candidateEnemies.Count; enemyIndex++)
        {
            EnemyHealth health = candidateEnemies[enemyIndex];
            if (health == null) continue;

            CollectLockPointCandidates(health.transform, lockPointCandidates);
            for (int i = 0; i < lockPointCandidates.Count; i++)
            {
                Transform candidatePoint = lockPointCandidates[i];
                Vector3 candidateWorldPos = GetLockPointWorldPosition(health.transform, candidatePoint);
                Vector3 viewport = mainCam.WorldToViewportPoint(candidateWorldPos);
                if (viewport.z <= 0f)
                    continue;

                Vector3 dirToTarget = (candidateWorldPos - mainCam.transform.position).normalized;
                if (Vector3.Angle(mainCam.transform.forward, dirToTarget) >= 60f)
                    continue;

                float distance = Vector3.Distance(transform.position, candidateWorldPos);
                if (distance > maxLockDistance)
                    continue;

                Vector2 screenDelta = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
                float priority = GetLockPointPriority(candidatePoint);
                float score = ((screenDelta.sqrMagnitude * 100f) + (distance * 0.02f)) / priority;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestRoot = health.transform;
                    bestPoint = candidatePoint;
                }
            }
        }

        if (bestRoot == null)
            return false;

        StartLockOn(bestRoot, bestPoint);
        return true;
    }

    private bool TrySwitchLockCandidate(float direction)
    {
        Transform bestRoot = null;
        Transform bestPoint = null;
        float bestScore = Mathf.Infinity;
        Vector3 currentScreenPos = mainCam.WorldToViewportPoint(GetTargetIconWorldPosition());
        CollectCandidateEnemies(candidateEnemies);

        for (int enemyIndex = 0; enemyIndex < candidateEnemies.Count; enemyIndex++)
        {
            EnemyHealth health = candidateEnemies[enemyIndex];
            if (health == null) continue;

            CollectLockPointCandidates(health.transform, lockPointCandidates);
            for (int i = 0; i < lockPointCandidates.Count; i++)
            {
                Transform candidatePoint = lockPointCandidates[i];
                if (health.transform == currentTarget && candidatePoint == currentLookTarget)
                    continue;

                Vector3 candidateWorldPos = GetLockPointWorldPosition(health.transform, candidatePoint);
                Vector3 candidateScreenPos = mainCam.WorldToViewportPoint(candidateWorldPos);
                if (candidateScreenPos.z <= 0f)
                    continue;

                float diffX = candidateScreenPos.x - currentScreenPos.x;
                if ((direction > 0 && diffX <= 0.01f) || (direction < 0 && diffX >= -0.01f))
                    continue;

                float distance = Vector3.Distance(transform.position, candidateWorldPos);
                if (distance > maxLockDistance)
                    continue;

                float diffY = Mathf.Abs(candidateScreenPos.y - currentScreenPos.y);
                float priority = GetLockPointPriority(candidatePoint);
                float score = (Mathf.Abs(diffX) + (diffY * 2f) + (distance * 0.01f)) / priority;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestRoot = health.transform;
                    bestPoint = candidatePoint;
                }
            }
        }

        if (bestRoot == null)
            return false;

        StartLockOn(bestRoot, bestPoint);
        return true;
    }

    private void StartLockOn(Transform target, Transform lockPoint = null)
    {
        ResolveSceneCameraReferences();
        if (lockOnCamera == null)
            return;

        currentTarget = target;
        currentLookTarget = lockPoint != null ? lockPoint : ResolveBestLockPoint(target);
        isLockedOn = true;
        lockOnCamera.LookAt = currentLookTarget;
        lockOnCamera.Priority = 20;
        ShowTargetIcon();
        UpdateTargetUI();
    }

    public void StopLockOn()
    {
        isLockedOn = false;
        currentTarget = null;
        currentLookTarget = null;

        if (mainCam != null && freeLookCamera != null)
        {
            freeLookCamera.m_XAxis.Value = mainCam.transform.eulerAngles.y;
            freeLookCamera.m_YAxis.Value = 0.5f;
        }

        if (lockOnCamera != null)
        {
            lockOnCamera.Priority = 0;
            lockOnCamera.LookAt = null;
        }

        HideTargetIcon();

        if (playerModel != null)
            playerModel.localRotation = Quaternion.identity;
    }

    private void HandleRotation()
    {
        if (playerController != null && playerController.isDodging)
            return;

        Vector3 dir = CurrentLockAimPoint - transform.position;
        dir.y = 0;
        if (dir == Vector3.zero) return;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);

        if (playerModel != null)
            playerModel.localRotation = Quaternion.identity;
    }

    private void UpdateTargetUI()
    {
        if (!isLockedOn || currentTarget == null || (playerController != null && playerController.IsGameplayInputBlocked))
        {
            HideTargetIcon();
            return;
        }

        if (mainCam == null)
            mainCam = ResolveGameplayCamera();

        if (mainCam == null)
        {
            HideTargetIcon();
            return;
        }

        Vector3 worldPos = GetTargetIconWorldPosition();
        Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);
        if (screenPos.z <= 0f)
        {
            HideTargetIcon();
            return;
        }

        ShowTargetIcon();
        PositionWorldTargetIcon(worldPos);
    }

    private Transform GetCurrentLookTarget()
    {
        if (currentLookTarget == null && currentTarget != null)
            currentLookTarget = ResolveBestLockPoint(currentTarget);

        return currentLookTarget != null ? currentLookTarget : currentTarget;
    }

    private Vector3 GetTargetIconWorldPosition()
    {
        Transform lockPoint = GetCurrentLookTarget();
        if (lockPoint != null && lockPoint != currentTarget)
            return lockPoint.position;

        Bounds bounds;
        if (TryGetTargetBounds(currentTarget, out bounds))
        {
            float iconHeight = Mathf.Lerp(bounds.min.y, bounds.max.y, fallbackLockPointBoundsHeight) + fallbackLockPointOffset;
            return new Vector3(bounds.center.x, iconHeight, bounds.center.z);
        }

        return currentTarget != null ? currentTarget.position + Vector3.up * 1.1f : Vector3.zero;
    }

    private Transform ResolveBestLockPoint(Transform target)
    {
        if (target == null)
            return null;

        CollectConfiguredLockPoints(target, lockPointCandidates);
        if (lockPointCandidates.Count == 0)
            CollectLockPointCandidates(target, lockPointCandidates);

        if (lockPointCandidates.Count == 0)
            return target;

        if (mainCam == null)
            mainCam = ResolveGameplayCamera();

        Transform bestPoint = lockPointCandidates[0];
        float bestScore = Mathf.Infinity;
        for (int i = 0; i < lockPointCandidates.Count; i++)
        {
            Transform point = lockPointCandidates[i];
            if (point == null)
                continue;

            float priority = GetLockPointPriority(point);
            float score = 0f;
            if (mainCam != null)
            {
                Vector3 viewport = mainCam.WorldToViewportPoint(point.position);
                Vector2 screenDelta = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
                score = screenDelta.sqrMagnitude * 100f;
                if (viewport.z <= 0f)
                    score += 1000f;
            }

            score /= priority;
            if (score < bestScore)
            {
                bestScore = score;
                bestPoint = point;
            }
        }

        return bestPoint != null ? bestPoint : target;
    }

    private static void CollectConfiguredLockPoints(Transform root, List<Transform> results)
    {
        results.Clear();
        if (root == null)
            return;

        TargetLockPoint[] explicitPoints = root.GetComponentsInChildren<TargetLockPoint>(true);
        for (int i = 0; i < explicitPoints.Length; i++)
        {
            TargetLockPoint point = explicitPoints[i];
            if (point != null && point.IsLockable)
                AddUniqueLockPoint(results, point.transform);
        }
    }

    private void CollectCandidateEnemies(List<EnemyHealth> results)
    {
        results.Clear();
        scannedEnemies.Clear();

        IReadOnlyList<TargetLockPoint> sceneLockPoints = TargetLockPoint.ActiveLockPoints;
        for (int i = 0; i < sceneLockPoints.Count; i++)
        {
            TargetLockPoint point = sceneLockPoints[i];
            if (point == null || !point.IsLockable)
                continue;

            EnemyHealth health = point.GetComponentInParent<EnemyHealth>();
            if (health == null || !health.gameObject.activeInHierarchy)
                continue;

            if (Vector3.Distance(transform.position, point.transform.position) > scanRadius)
                continue;

            if (scannedEnemies.Add(health))
                results.Add(health);
        }

        Collider[] colliders = Physics.OverlapSphere(transform.position, scanRadius, enemyLayer);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null)
                continue;

            EnemyHealth health = col.GetComponentInParent<EnemyHealth>();
            if (health == null || !health.gameObject.activeInHierarchy)
                continue;

            if (scannedEnemies.Add(health))
                results.Add(health);
        }
    }

    private static void CollectLockPointCandidates(Transform root, List<Transform> results)
    {
        results.Clear();
        if (root == null)
            return;

        TargetLockPoint[] explicitPoints = root.GetComponentsInChildren<TargetLockPoint>(true);
        for (int i = 0; i < explicitPoints.Length; i++)
        {
            TargetLockPoint point = explicitPoints[i];
            if (point != null && point.IsLockable)
                AddUniqueLockPoint(results, point.transform);
        }

        if (results.Count == 0)
            results.Add(root);
    }

    private static void AddUniqueLockPoint(List<Transform> results, Transform point)
    {
        if (point == null)
            return;

        for (int i = 0; i < results.Count; i++)
        {
            if (results[i] == point)
                return;
        }

        results.Add(point);
    }

    private static bool IsExplicitLockPoint(Transform root, Transform point)
    {
        return root != null && point != null && point != root && point.IsChildOf(root);
    }

    private static bool IsConfiguredLockPoint(Transform point)
    {
        return point != null && point.GetComponent<TargetLockPoint>() != null;
    }

    private Vector3 GetLockPointWorldPosition(Transform root, Transform point)
    {
        if (IsConfiguredLockPoint(point) || IsExplicitLockPoint(root, point))
            return point.position;

        Bounds bounds;
        if (TryGetTargetBounds(root, out bounds))
        {
            float iconHeight = Mathf.Lerp(bounds.min.y, bounds.max.y, fallbackLockPointBoundsHeight) + fallbackLockPointOffset;
            return new Vector3(bounds.center.x, iconHeight, bounds.center.z);
        }

        return root != null ? root.position + Vector3.up * 1.1f : Vector3.zero;
    }

    private static float GetLockPointPriority(Transform point)
    {
        if (point == null)
            return 1f;

        TargetLockPoint lockPoint = point.GetComponent<TargetLockPoint>();
        return lockPoint != null ? lockPoint.Priority : 1f;
    }

    private static bool TryGetTargetBounds(Transform target, out Bounds bounds)
    {
        bounds = default;
        if (target == null)
            return false;

        bool hasBounds = false;
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!IsUsableTargetRenderer(renderer))
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds)
            return true;

        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy || collider.isTrigger)
                continue;

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
    }

    private static bool IsUsableTargetRenderer(Renderer renderer)
    {
        if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
            return false;

        if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
            return false;

        Bounds bounds = renderer.bounds;
        return bounds.size.sqrMagnitude > 0.0001f;
    }

    private void PositionWorldTargetIcon(Vector3 worldPos)
    {
        SpriteRenderer renderer = EnsureWorldTargetIcon();
        if (renderer == null)
            return;

        Transform iconTransform = renderer.transform;
        iconTransform.position = worldPos;

        if (mainCam != null)
        {
            iconTransform.rotation = Quaternion.LookRotation(iconTransform.position - mainCam.transform.position, mainCam.transform.up);
            float distance = Vector3.Distance(mainCam.transform.position, worldPos);
            float scale = Mathf.Clamp(distance * 0.012f, 0.08f, 0.35f);
            iconTransform.localScale = Vector3.one * scale;
        }
    }

    private SpriteRenderer EnsureWorldTargetIcon()
    {
        if (worldTargetIconRenderer != null)
            return worldTargetIconRenderer;

        worldTargetIcon = new GameObject("RuntimeWorldTargetLockDot");
        DontDestroyOnLoad(worldTargetIcon);
        worldTargetIconRenderer = worldTargetIcon.AddComponent<SpriteRenderer>();
        worldTargetIconRenderer.sprite = GetRuntimeDotSprite();
        worldTargetIconRenderer.color = Color.white;
        worldTargetIconRenderer.sortingOrder = 32767;

        if (targetIconShader != null)
        {
            worldTargetIconMaterial = new Material(targetIconShader);
            worldTargetIconRenderer.material = worldTargetIconMaterial;
        }

        worldTargetIcon.SetActive(false);
        return worldTargetIconRenderer;
    }

    private Sprite GetRuntimeDotSprite()
    {
        if (runtimeDotSprite != null)
            return runtimeDotSprite;

        Texture2D texture = GetRuntimeDotTexture();
        runtimeDotSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 32f);
        runtimeDotSprite.name = "Runtime_TargetLock_Dot_Sprite";
        return runtimeDotSprite;
    }

    private Texture2D GetRuntimeDotTexture()
    {
        if (runtimeDotTexture != null)
            return runtimeDotTexture;

        const int size = 32;
        const float radius = 13.5f;
        const float softEdge = 2.5f;
        runtimeDotTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        runtimeDotTexture.name = "Runtime_TargetLock_Dot";
        runtimeDotTexture.wrapMode = TextureWrapMode.Clamp;
        runtimeDotTexture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01((radius - distance) / softEdge);
                runtimeDotTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        runtimeDotTexture.Apply(false, true);
        return runtimeDotTexture;
    }

    private void HideTargetIcon(bool unused = false)
    {
        if (worldTargetIcon != null && worldTargetIcon.activeSelf)
            worldTargetIcon.SetActive(false);
    }

    private void ShowTargetIcon()
    {
        SpriteRenderer renderer = EnsureWorldTargetIcon();
        if (renderer != null && !renderer.gameObject.activeSelf)
            renderer.gameObject.SetActive(true);
    }

    private void DestroyRuntimeTargetIcon()
    {
        if (worldTargetIcon != null)
            Destroy(worldTargetIcon);
        if (worldTargetIconMaterial != null)
            Destroy(worldTargetIconMaterial);
        if (runtimeDotSprite != null)
            Destroy(runtimeDotSprite);
        if (runtimeDotTexture != null)
            Destroy(runtimeDotTexture);

        worldTargetIcon = null;
        worldTargetIconRenderer = null;
        worldTargetIconMaterial = null;
        runtimeDotSprite = null;
        runtimeDotTexture = null;
    }

    private void RecenterCamera()
    {
        if (freeLookCamera != null)
            freeLookCamera.m_XAxis.Value = transform.eulerAngles.y;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, scanRadius);

        if (isLockedOn && CurrentLockPoint != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawSphere(CurrentLockPoint.position, 0.08f);
        }
    }
}
