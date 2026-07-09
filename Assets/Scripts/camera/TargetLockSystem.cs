using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TargetLockSystem : MonoBehaviour
{
    [Header("Riferimenti")]
    public CinemachineFreeLook freeLookCamera;
    public CinemachineVirtualCamera lockOnCamera;
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

    [Header("UI")]
    public RectTransform targetIcon;
    [SerializeField] private float targetIconHeadOffset = 0f;
    [SerializeField, Range(0.35f, 1f)] private float targetIconBoundsHeight = 0.55f;

    [Header("Debug")]
    public bool isLockedOn = false;
    public Transform currentTarget;

    public Transform CurrentLockPoint => GetCurrentLookTarget();
    public Vector3 CurrentLockAimPoint => isLockedOn && currentTarget != null ? GetTargetIconWorldPosition() : Vector3.zero;

    private Camera mainCam;
    private Transform currentLookTarget;
    private Canvas targetIconCanvas;
    private Canvas runtimeOverlayCanvas;
    private Graphic[] targetIconGraphics;
    private CanvasRenderer[] targetIconRenderers;
    private RectTransform runtimeTargetIcon;
    private RawImage runtimeTargetIconImage;
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

    private const string LockOnCameraName = "CM_LockOn";
    private const string CameraTargetName = "CamTarget";
    private const string LegacyLockOnPointName = "LockOnPoint";
    private const string HeadPointName = "HeadPoint";

    void Awake()
    {
        mainCam = ResolveGameplayCamera();
        playerController = GetComponentInParent<PlayerController>();
        HideTargetIcon();
    }

    void OnEnable()
    {
        Canvas.willRenderCanvases -= UpdateTargetUI;
        Canvas.willRenderCanvases += UpdateTargetUI;

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
        Canvas.willRenderCanvases -= UpdateTargetUI;
        HideTargetIcon(false);
    }

    void OnDestroy()
    {
        Canvas.willRenderCanvases -= UpdateTargetUI;
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (playerController != null && playerController.Controls != null)
            playerController.Controls.Player.LockOn.performed -= HandleLockOnInput;
    }

    void Update()
    {
        if (playerController != null && playerController.IsInventoryOpen)
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

        Transform followTarget = playerController != null ? playerController.transform : transform;
        Transform lookAtTarget = ResolveLookAtTarget(followTarget);

        if (freeLookCamera == null)
            freeLookCamera = FindObjectOfType<CinemachineFreeLook>();

        if (lockOnCamera == null)
            lockOnCamera = FindVirtualCamera(LockOnCameraName);

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

    private Transform ResolveLookAtTarget(Transform followTarget)
    {
        if (followTarget == null)
            return null;

        Transform[] children = followTarget.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == CameraTargetName)
                return children[i];
        }

        return followTarget;
    }

    private CinemachineVirtualCamera FindVirtualCamera(string preferredName)
    {
        CinemachineVirtualCamera[] cameras = FindObjectsOfType<CinemachineVirtualCamera>();
        CinemachineVirtualCamera fallback = null;

        for (int i = 0; i < cameras.Length; i++)
        {
            CinemachineVirtualCamera camera = cameras[i];
            if (camera == null)
                continue;

            if (fallback == null)
                fallback = camera;
            if (camera.name == preferredName)
                return camera;
        }

        return fallback;
    }

    private Camera ResolveGameplayCamera()
    {
        Camera[] cameras = FindObjectsOfType<Camera>();
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera != null && camera.isActiveAndEnabled && camera.GetComponent<CinemachineBrain>() != null)
                return camera;
        }

        return Camera.main;
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
        CacheTargetIconCanvas();
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
        if (!isLockedOn || currentTarget == null || (playerController != null && playerController.IsInventoryOpen))
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
            float iconHeight = Mathf.Lerp(bounds.min.y, bounds.max.y, targetIconBoundsHeight) + targetIconHeadOffset;
            return new Vector3(bounds.center.x, iconHeight, bounds.center.z);
        }

        Transform headPoint = ResolveTargetChild(currentTarget, HeadPointName);
        if (headPoint != null)
            return headPoint.position + Vector3.up * targetIconHeadOffset;

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

        TargetLockPoint[] sceneLockPoints = FindObjectsOfType<TargetLockPoint>(true);
        for (int i = 0; i < sceneLockPoints.Length; i++)
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

    private static Transform ResolveTargetChild(Transform root, string childName)
    {
        if (root == null)
            return null;

        Transform direct = root.Find(childName);
        if (direct != null)
            return direct;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName)
                return children[i];
        }

        return null;
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

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || !child.gameObject.activeInHierarchy)
                continue;

            TargetLockPoint lockPoint = child.GetComponent<TargetLockPoint>();
            if (lockPoint != null && !lockPoint.IsLockable)
                continue;

            if (IsLockPointName(child.name))
                AddUniqueLockPoint(results, child);
        }

        if (results.Count == 0)
            results.Add(root);
    }

    private static bool IsLockPointName(string objectName)
    {
        return objectName == LegacyLockOnPointName
               || objectName.StartsWith("LockPoint", System.StringComparison.OrdinalIgnoreCase)
               || objectName.StartsWith("LockOnPoint", System.StringComparison.OrdinalIgnoreCase);
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
            float iconHeight = Mathf.Lerp(bounds.min.y, bounds.max.y, targetIconBoundsHeight) + targetIconHeadOffset;
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

    private void PositionTargetIcon(Vector3 screenPos)
    {
        RectTransform iconTransform = EnsureRuntimeTargetIcon();
        if (iconTransform == null)
            return;

        iconTransform.anchorMin = Vector2.zero;
        iconTransform.anchorMax = Vector2.zero;
        iconTransform.anchoredPosition = new Vector2(screenPos.x, screenPos.y);
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

        Shader shader = Shader.Find("GUI/Text Shader");
        if (shader != null)
        {
            worldTargetIconMaterial = new Material(shader);
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

    private void CacheTargetIconCanvas()
    {
        if (targetIcon == null)
        {
            targetIconCanvas = null;
            targetIconGraphics = null;
            targetIconRenderers = null;
            return;
        }

        if (targetIconCanvas == null || !targetIcon.IsChildOf(targetIconCanvas.transform))
            targetIconCanvas = targetIcon.GetComponentInParent<Canvas>();

        if (targetIconGraphics == null || targetIconGraphics.Length == 0)
            targetIconGraphics = targetIcon.GetComponentsInChildren<Graphic>(true);

        if (targetIconRenderers == null || targetIconRenderers.Length == 0)
            targetIconRenderers = targetIcon.GetComponentsInChildren<CanvasRenderer>(true);
    }

    private RectTransform EnsureRuntimeTargetIcon()
    {
        RectTransform parent = GetTargetIconRootRect();
        if (runtimeTargetIcon != null)
        {
            if (parent != null && runtimeTargetIcon.parent != parent)
                runtimeTargetIcon.SetParent(parent, false);

            return runtimeTargetIcon;
        }

        if (parent == null)
            return null;

        GameObject iconObject = new GameObject("RuntimeTargetLockDot");
        iconObject.transform.SetParent(parent, false);
        runtimeTargetIcon = iconObject.AddComponent<RectTransform>();
        runtimeTargetIcon.anchorMin = Vector2.zero;
        runtimeTargetIcon.anchorMax = Vector2.zero;
        runtimeTargetIcon.pivot = new Vector2(0.5f, 0.5f);
        runtimeTargetIcon.sizeDelta = new Vector2(18f, 18f);

        runtimeTargetIconImage = iconObject.AddComponent<RawImage>();
        runtimeTargetIconImage.texture = GetRuntimeDotTexture();
        runtimeTargetIconImage.color = Color.white;
        runtimeTargetIconImage.raycastTarget = false;
        iconObject.SetActive(false);
        return runtimeTargetIcon;
    }

    private RectTransform GetTargetIconRootRect()
    {
        Canvas canvas = GetOrCreateRuntimeOverlayCanvas();
        return canvas != null ? canvas.GetComponent<RectTransform>() : null;
    }

    private Canvas GetOrCreateRuntimeOverlayCanvas()
    {
        if (runtimeOverlayCanvas != null && runtimeOverlayCanvas.GetComponent<RectTransform>() != null)
            return runtimeOverlayCanvas;

        if (runtimeOverlayCanvas != null)
        {
            Destroy(runtimeOverlayCanvas.gameObject);
            runtimeOverlayCanvas = null;
        }

        GameObject canvasObject = new GameObject("TargetLockOverlayCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        DontDestroyOnLoad(canvasObject);
        runtimeOverlayCanvas = canvasObject.GetComponent<Canvas>();
        runtimeOverlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        runtimeOverlayCanvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        return runtimeOverlayCanvas;
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

    private void HideTargetIcon(bool createRuntimeIcon = false)
    {
        if (worldTargetIcon != null && worldTargetIcon.activeSelf)
            worldTargetIcon.SetActive(false);

        RectTransform runtimeIcon = createRuntimeIcon ? EnsureRuntimeTargetIcon() : runtimeTargetIcon;
        if (runtimeIcon != null && runtimeIcon.gameObject.activeSelf)
            runtimeIcon.gameObject.SetActive(false);

        if (targetIcon == null)
            return;

        CacheTargetIconCanvas();
        if (!targetIcon.gameObject.activeSelf)
            targetIcon.gameObject.SetActive(true);

        if (targetIconGraphics != null)
        {
            for (int i = 0; i < targetIconGraphics.Length; i++)
            {
                if (targetIconGraphics[i] != null)
                    targetIconGraphics[i].enabled = false;
            }
        }

        if (targetIconRenderers != null)
        {
            for (int i = 0; i < targetIconRenderers.Length; i++)
            {
                if (targetIconRenderers[i] != null)
                    targetIconRenderers[i].SetAlpha(0f);
            }
        }
    }

    private void ShowTargetIcon()
    {
        SpriteRenderer renderer = EnsureWorldTargetIcon();
        if (renderer != null && !renderer.gameObject.activeSelf)
            renderer.gameObject.SetActive(true);

        if (runtimeTargetIcon != null && runtimeTargetIcon.gameObject.activeSelf)
            runtimeTargetIcon.gameObject.SetActive(false);

        if (targetIcon == null)
            return;

        CacheTargetIconCanvas();
        if (!targetIcon.gameObject.activeSelf)
            targetIcon.gameObject.SetActive(true);

        if (targetIconGraphics != null)
        {
            for (int i = 0; i < targetIconGraphics.Length; i++)
            {
                if (targetIconGraphics[i] != null)
                    targetIconGraphics[i].enabled = false;
            }
        }

        if (targetIconRenderers != null)
        {
            for (int i = 0; i < targetIconRenderers.Length; i++)
            {
                if (targetIconRenderers[i] != null)
                    targetIconRenderers[i].SetAlpha(0f);
            }
        }
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
