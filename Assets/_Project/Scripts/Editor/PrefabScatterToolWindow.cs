using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PrefabScatterToolWindow : EditorWindow
{
    [SerializeField] private List<GameObject> prefabs = new();
    [SerializeField] private Transform parentContainer;
    [SerializeField] private Transform surfaceRoot;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private Vector3 areaCenter = Vector3.zero;
    [SerializeField] private Vector2 areaSize = new Vector2(20f, 20f);
    [SerializeField] private int count = 50;
    [SerializeField] private int seed = 12345;
    [SerializeField] private bool randomizeSeed = true;
    [SerializeField] private float raycastHeight = 200f;
    [SerializeField] private float minDistance = 1.5f;
    [SerializeField] private int maxPlacementAttempts = 25;
    [SerializeField] private bool randomYaw = true;
    [SerializeField] private Vector2 yawRange = new Vector2(0f, 360f);
    [SerializeField] private bool randomUniformScale = true;
    [SerializeField] private Vector2 uniformScaleRange = new Vector2(0.9f, 1.15f);
    [SerializeField] private Vector2 scaleXRange = new Vector2(0.9f, 1.15f);
    [SerializeField] private Vector2 scaleYRange = new Vector2(0.9f, 1.15f);
    [SerializeField] private Vector2 scaleZRange = new Vector2(0.9f, 1.15f);
    [SerializeField] private bool alignToSurfaceNormal = false;
    [SerializeField] private float maxSlopeAngle = 35f;
    [SerializeField] private bool keepPrefabLocalRotationX = false;
    [SerializeField] private bool keepPrefabLocalRotationZ = false;
    [SerializeField] private bool drawScenePreview = true;
    [SerializeField] private bool allowRendererBoundsFallback = true;
    [SerializeField] private bool overridePlacedLayer = true;
    [SerializeField] private int placedLayer = 2;
    [SerializeField] private bool brushEnabled = false;
    [SerializeField] private bool eraseBrush = false;
    [SerializeField] private float brushRadius = 2f;
    [SerializeField] private int brushDensity = 4;
    [SerializeField] private float brushStrokeSpacing = 0.75f;

    private int lastRaycastMissCount;
    private int lastSlopeRejectedCount;
    private int lastDistanceRejectedCount;
    private int lastRendererFallbackCount;

    private SerializedObject serializedWindow;
    private SerializedProperty prefabsProp;
    private System.Random prng;
    private bool brushStrokeActive;
    private int brushUndoGroup = -1;
    private Vector3 lastBrushStampPosition;
    private bool hasLastBrushStampPosition;

    [MenuItem("Tools/Arcadia/Prefab Scatter Tool")]
    public static void OpenWindow()
    {
        var window = GetWindow<PrefabScatterToolWindow>("Prefab Scatter");
        window.minSize = new Vector2(380f, 560f);
        window.Show();
    }

    private void OnEnable()
    {
        serializedWindow = new SerializedObject(this);
        prefabsProp = serializedWindow.FindProperty("prefabs");
        SceneView.duringSceneGui += OnSceneGUI;
        wantsMouseMove = true;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        brushStrokeActive = false;
        brushUndoGroup = -1;
    }

    private void OnGUI()
    {
        if (serializedWindow == null)
            serializedWindow = new SerializedObject(this);

        serializedWindow.Update();

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Prefab Scatter Tool", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Sparge prefab su un'area rettangolare usando raycast verso il basso. "
            + "La modalita Pennello permette anche di dipingerli direttamente nella Scene View.",
            MessageType.Info);

        EditorGUILayout.PropertyField(prefabsProp, true);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Aggiungi Prefab Selezionati"))
            {
                serializedWindow.ApplyModifiedProperties();
                AddSelectedPrefabs();
                serializedWindow.Update();
            }

            if (GUILayout.Button("Svuota Lista"))
            {
                serializedWindow.ApplyModifiedProperties();
                prefabs.Clear();
                EditorUtility.SetDirty(this);
                serializedWindow.Update();
            }
        }

        EditorGUILayout.Space(4f);
        parentContainer = (Transform)EditorGUILayout.ObjectField("Parent Container", parentContainer, typeof(Transform), true);
        surfaceRoot = (Transform)EditorGUILayout.ObjectField("Surface Root", surfaceRoot, typeof(Transform), true);
        groundMask = EditorGUILayout.MaskField("Ground Mask", groundMask, InternalEditorUtility.layers);
        allowRendererBoundsFallback = EditorGUILayout.Toggle("Renderer Fallback", allowRendererBoundsFallback);
        overridePlacedLayer = EditorGUILayout.Toggle("Override Placed Layer", overridePlacedLayer);
        using (new EditorGUI.DisabledScope(!overridePlacedLayer))
        {
            placedLayer = EditorGUILayout.LayerField("Placed Layer", placedLayer);
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Area", EditorStyles.boldLabel);
        areaCenter = EditorGUILayout.Vector3Field("Area Center", areaCenter);
        areaSize = EditorGUILayout.Vector2Field("Area Size", areaSize);
        raycastHeight = Mathf.Max(1f, EditorGUILayout.FloatField("Raycast Height", raycastHeight));
        maxSlopeAngle = Mathf.Clamp(EditorGUILayout.Slider("Max Slope", maxSlopeAngle, 0f, 89f), 0f, 89f);
        minDistance = Mathf.Max(0f, EditorGUILayout.FloatField("Min Distance", minDistance));
        maxPlacementAttempts = Mathf.Max(1, EditorGUILayout.IntField("Attempts / Item", maxPlacementAttempts));

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Random", EditorStyles.boldLabel);
        count = Mathf.Max(1, EditorGUILayout.IntField("Count", count));
        randomizeSeed = EditorGUILayout.Toggle("Randomize Seed", randomizeSeed);
        using (new EditorGUI.DisabledScope(randomizeSeed))
        {
            seed = EditorGUILayout.IntField("Seed", seed);
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Rotation", EditorStyles.boldLabel);
        alignToSurfaceNormal = EditorGUILayout.Toggle("Align To Normal", alignToSurfaceNormal);
        randomYaw = EditorGUILayout.Toggle("Random Yaw", randomYaw);
        using (new EditorGUI.DisabledScope(!randomYaw))
        {
            yawRange = EditorGUILayout.Vector2Field("Yaw Range", yawRange);
        }
        keepPrefabLocalRotationX = EditorGUILayout.Toggle("Keep Prefab X", keepPrefabLocalRotationX);
        keepPrefabLocalRotationZ = EditorGUILayout.Toggle("Keep Prefab Z", keepPrefabLocalRotationZ);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Scale", EditorStyles.boldLabel);
        randomUniformScale = EditorGUILayout.Toggle("Uniform Scale", randomUniformScale);
        if (randomUniformScale)
        {
            uniformScaleRange = EditorGUILayout.Vector2Field("Uniform Range", uniformScaleRange);
        }
        else
        {
            scaleXRange = EditorGUILayout.Vector2Field("Scale X", scaleXRange);
            scaleYRange = EditorGUILayout.Vector2Field("Scale Y", scaleYRange);
            scaleZRange = EditorGUILayout.Vector2Field("Scale Z", scaleZRange);
        }

        EditorGUILayout.Space(4f);
        drawScenePreview = EditorGUILayout.Toggle("Draw Scene Preview", drawScenePreview);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Pennello Scene View", EditorStyles.boldLabel);
        brushEnabled = EditorGUILayout.Toggle("Attiva Pennello", brushEnabled);
        using (new EditorGUI.DisabledScope(!brushEnabled))
        {
            eraseBrush = EditorGUILayout.Toggle("Modalita Gomma", eraseBrush);
            brushRadius = Mathf.Max(0.1f, EditorGUILayout.FloatField("Raggio", brushRadius));
            brushDensity = Mathf.Clamp(EditorGUILayout.IntField("Piante per Passata", brushDensity), 1, 100);
            brushStrokeSpacing = Mathf.Max(0.05f, EditorGUILayout.FloatField("Spaziatura Trascinamento", brushStrokeSpacing));
        }

        if (brushEnabled)
        {
            EditorGUILayout.HelpBox(
                EditorApplication.isPlaying
                    ? "Il pennello e disattivato durante il Play Mode."
                    : eraseBrush
                    ? "GOMMA ATTIVA: trascina con il tasto sinistro per rimuovere i prefab del pennello dentro il cerchio. Alt continua a controllare la camera."
                    : "Trascina con il tasto sinistro sulla superficie per dipingere. Alt continua a controllare la camera. Usa Min Distance per evitare sovrapposizioni.",
                eraseBrush && !EditorApplication.isPlaying ? MessageType.Warning : MessageType.Info);
        }

        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use Selection As Center"))
                UseSelectionAsCenter();

            if (GUILayout.Button("Use Selection As Parent"))
                UseSelectionAsParent();
        }

        if (lastRaycastMissCount > 0 || lastSlopeRejectedCount > 0 || lastDistanceRejectedCount > 0 || lastRendererFallbackCount > 0)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                $"Ultimo scatter\n"
                + $"- Raycast miss: {lastRaycastMissCount}\n"
                + $"- Rejected by slope: {lastSlopeRejectedCount}\n"
                + $"- Rejected by min distance: {lastDistanceRejectedCount}\n"
                + $"- Renderer fallback hits: {lastRendererFallbackCount}",
                MessageType.None);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Scatter Prefabs", GUILayout.Height(34f)))
                ScatterPrefabs();

            if (GUILayout.Button("Clear Children", GUILayout.Height(34f)))
                ClearChildren();
        }

        if (GUILayout.Button("Apply Layer To Children"))
            ApplyLayerToChildren();

        serializedWindow.ApplyModifiedProperties();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (drawScenePreview)
        {
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            Color fill = new Color(0.15f, 0.65f, 0.25f, 0.08f);
            Color outline = new Color(0.15f, 0.75f, 0.25f, 0.95f);

            Vector3 size = new Vector3(Mathf.Max(0.1f, areaSize.x), 0.01f, Mathf.Max(0.1f, areaSize.y));
            Handles.DrawSolidRectangleWithOutline(GetRectPoints(areaCenter, size), fill, outline);
            Handles.Label(areaCenter + Vector3.up * 0.1f, $"Scatter Area\n{areaSize.x:0.##} x {areaSize.y:0.##}");
        }

        if (brushEnabled)
            HandleBrushSceneGUI(sceneView);
    }

    private void HandleBrushSceneGUI(SceneView sceneView)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Event currentEvent = Event.current;
        if (currentEvent == null)
            return;

        int controlId = GUIUtility.GetControlID("ArcadiaPrefabBrush".GetHashCode(), FocusType.Passive);
        if (currentEvent.type == EventType.Layout)
            HandleUtility.AddDefaultControl(controlId);

        Ray mouseRay = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
        bool hasSurface = TryRaycastBrushSurface(mouseRay, out RaycastHit hoverHit);

        if (hasSurface)
        {
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            Handles.color = eraseBrush
                ? new Color(1f, 0.2f, 0.15f, 0.95f)
                : new Color(0.2f, 1f, 0.35f, 0.95f);
            Handles.DrawWireDisc(hoverHit.point, hoverHit.normal, brushRadius);
            Handles.DrawLine(hoverHit.point, hoverHit.point + hoverHit.normal * 0.4f);
        }

        if (currentEvent.alt || currentEvent.button != 0)
            return;

        if (currentEvent.type == EventType.MouseDown && hasSurface)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(eraseBrush ? "Erase Prefab Brush" : "Paint Prefab Brush");
            brushUndoGroup = Undo.GetCurrentGroup();
            brushStrokeActive = true;
            hasLastBrushStampPosition = false;
            ApplyBrushAt(hoverHit);
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.MouseDrag && brushStrokeActive && hasSurface)
        {
            if (!hasLastBrushStampPosition || Vector3.Distance(lastBrushStampPosition, hoverHit.point) >= brushStrokeSpacing)
                ApplyBrushAt(hoverHit);

            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.MouseUp && brushStrokeActive)
        {
            if (brushUndoGroup >= 0)
                Undo.CollapseUndoOperations(brushUndoGroup);

            brushStrokeActive = false;
            brushUndoGroup = -1;
            hasLastBrushStampPosition = false;
            currentEvent.Use();
        }
    }

    private void ApplyBrushAt(RaycastHit centerHit)
    {
        lastBrushStampPosition = centerHit.point;
        hasLastBrushStampPosition = true;

        if (eraseBrush)
        {
            EraseBrushPrefabs(centerHit.point);
            MarkCurrentEditingSceneDirty();
            return;
        }

        List<GameObject> validPrefabs = GetValidPrefabs();
        if (validPrefabs.Count == 0)
        {
            Debug.LogWarning("[PrefabScatterTool] Assegna almeno un prefab prima di usare il pennello.");
            brushStrokeActive = false;
            return;
        }

        EnsureParentContainer();
        prng ??= new System.Random(System.Environment.TickCount);

        List<Vector3> occupiedPositions = GetExistingBrushPositions();
        for (int i = 0; i < brushDensity; i++)
        {
            if (!TryFindBrushPlacement(centerHit.point, occupiedPositions, out RaycastHit placementHit))
                continue;

            GameObject prefab = validPrefabs[prng.Next(validPrefabs.Count)];
            PlacePrefab(prefab, placementHit, occupiedPositions);
        }

        MarkCurrentEditingSceneDirty();
    }

    private bool TryFindBrushPlacement(Vector3 center, List<Vector3> occupiedPositions, out RaycastHit placementHit)
    {
        placementHit = default;

        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            float angle = (float)prng.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Sqrt((float)prng.NextDouble()) * brushRadius;
            Vector3 sample = center + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
            Ray ray = new Ray(sample + Vector3.up * raycastHeight, Vector3.down);

            if (!TryRaycastBrushSurface(ray, out RaycastHit hit, raycastHeight * 2f))
                continue;

            if (Vector3.Angle(hit.normal, Vector3.up) > maxSlopeAngle)
                continue;

            if (minDistance > 0f && !IsFarEnough(hit.point, occupiedPositions))
                continue;

            placementHit = hit;
            return true;
        }

        return false;
    }

    private bool TryRaycastBrushSurface(Ray ray, out RaycastHit bestHit, float distance = Mathf.Infinity)
    {
        bestHit = default;
        RaycastHit[] hits = Physics.RaycastAll(ray, distance, groundMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            if (hitTransform == null)
                continue;

            if (parentContainer != null && (hitTransform == parentContainer || hitTransform.IsChildOf(parentContainer)))
                continue;

            if (surfaceRoot != null && hitTransform != surfaceRoot && !hitTransform.IsChildOf(surfaceRoot))
                continue;

            bestHit = hits[i];
            return true;
        }

        return false;
    }

    private List<Vector3> GetExistingBrushPositions()
    {
        List<Vector3> positions = new();
        if (parentContainer == null)
            return positions;

        for (int i = 0; i < parentContainer.childCount; i++)
        {
            Transform child = parentContainer.GetChild(i);
            if (child != null)
                positions.Add(child.position);
        }

        return positions;
    }

    private void EraseBrushPrefabs(Vector3 center)
    {
        if (parentContainer == null)
            return;

        List<GameObject> validPrefabs = GetValidPrefabs();
        float sqrRadius = brushRadius * brushRadius;
        for (int i = parentContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = parentContainer.GetChild(i);
            if (child == null)
                continue;

            Vector3 delta = child.position - center;
            delta.y = 0f;
            if (delta.sqrMagnitude > sqrRadius || !IsBrushPrefabInstance(child.gameObject, validPrefabs))
                continue;

            Undo.DestroyObjectImmediate(child.gameObject);
        }
    }

    private static bool IsBrushPrefabInstance(GameObject instance, List<GameObject> validPrefabs)
    {
        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
        if (source != null && validPrefabs.Contains(source))
            return true;

        for (int i = 0; i < validPrefabs.Count; i++)
        {
            GameObject prefab = validPrefabs[i];
            if (prefab != null && instance.name.StartsWith(prefab.name, System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static Vector3[] GetRectPoints(Vector3 center, Vector3 size)
    {
        Vector3 half = new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
        return new[]
        {
            center + new Vector3(-half.x, 0f, -half.z),
            center + new Vector3(-half.x, 0f, half.z),
            center + new Vector3(half.x, 0f, half.z),
            center + new Vector3(half.x, 0f, -half.z),
        };
    }

    private void UseSelectionAsCenter()
    {
        if (Selection.activeTransform == null)
            return;

        areaCenter = Selection.activeTransform.position;
        Repaint();
        SceneView.RepaintAll();
    }

    private void UseSelectionAsParent()
    {
        if (Selection.activeTransform == null)
            return;

        parentContainer = Selection.activeTransform;
        if (surfaceRoot == null)
            surfaceRoot = Selection.activeTransform;
        Repaint();
    }

    private void ScatterPrefabs()
    {
        if (prefabs == null || prefabs.Count == 0)
        {
            Debug.LogWarning("[PrefabScatterTool] Nessun prefab assegnato.");
            return;
        }

        List<GameObject> validPrefabs = GetValidPrefabs();
        if (validPrefabs.Count == 0)
        {
            Debug.LogWarning("[PrefabScatterTool] La lista prefab non contiene elementi validi.");
            return;
        }

        EnsureParentContainer();

        int effectiveSeed = randomizeSeed ? System.Environment.TickCount ^ System.DateTime.Now.Millisecond : seed;
        prng = new System.Random(effectiveSeed);
        lastRaycastMissCount = 0;
        lastSlopeRejectedCount = 0;
        lastDistanceRejectedCount = 0;
        lastRendererFallbackCount = 0;

        Undo.SetCurrentGroupName("Scatter Prefabs");
        int undoGroup = Undo.GetCurrentGroup();

        List<Vector3> placedPositions = new();
        int placedCount = 0;

        for (int i = 0; i < count; i++)
        {
            if (TryFindPlacement(placedPositions, out RaycastHit hit))
            {
                GameObject prefab = validPrefabs[prng.Next(validPrefabs.Count)];
                PlacePrefab(prefab, hit, placedPositions);
                placedCount++;
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        MarkCurrentEditingSceneDirty();
        Debug.Log($"[PrefabScatterTool] Posizionati {placedCount}/{count} prefab. Seed: {effectiveSeed} | Miss:{lastRaycastMissCount} | Slope:{lastSlopeRejectedCount} | Distance:{lastDistanceRejectedCount} | Fallback:{lastRendererFallbackCount}");
    }

    private void AddSelectedPrefabs()
    {
        int added = 0;
        GameObject[] selectedObjects = Selection.gameObjects;
        for (int i = 0; i < selectedObjects.Length; i++)
        {
            GameObject candidate = selectedObjects[i];
            if (candidate == null || !EditorUtility.IsPersistent(candidate))
                continue;

            if (PrefabUtility.GetPrefabAssetType(candidate) == PrefabAssetType.NotAPrefab || prefabs.Contains(candidate))
                continue;

            prefabs.Add(candidate);
            added++;
        }

        EditorUtility.SetDirty(this);
        if (added == 0)
            Debug.LogWarning("[PrefabScatterTool] Seleziona uno o piu prefab nel Project prima di premere il pulsante.");
    }

    private void EnsureParentContainer()
    {
        if (parentContainer != null)
            return;

        GameObject container = new GameObject("ScatteredPrefabs");
        MoveToCurrentEditingScene(container);
        Undo.RegisterCreatedObjectUndo(container, "Create Scatter Container");
        parentContainer = container.transform;
    }

    private bool TryFindPlacement(List<Vector3> placedPositions, out RaycastHit bestHit)
    {
        bestHit = default;

        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            Vector3 sample = GetRandomPointInArea();
            Vector3 rayOrigin = sample + Vector3.up * raycastHeight;

            bool foundHit = Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastHeight * 2f, groundMask, QueryTriggerInteraction.Ignore);
            if (!foundHit && allowRendererBoundsFallback)
                foundHit = TryGetRendererFallbackHit(sample, out hit);

            if (!foundHit)
            {
                lastRaycastMissCount++;
                continue;
            }

            float slope = Vector3.Angle(hit.normal, Vector3.up);
            if (slope > maxSlopeAngle)
            {
                lastSlopeRejectedCount++;
                continue;
            }

            if (minDistance > 0f && !IsFarEnough(hit.point, placedPositions))
            {
                lastDistanceRejectedCount++;
                continue;
            }

            bestHit = hit;
            return true;
        }

        return false;
    }

    private bool TryGetRendererFallbackHit(Vector3 sample, out RaycastHit fallbackHit)
    {
        fallbackHit = default;

        Transform root = surfaceRoot != null ? surfaceRoot : parentContainer;
        if (root == null)
            return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return false;

        Renderer bestRenderer = null;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Bounds bounds = renderer.bounds;
            if (sample.x < bounds.min.x || sample.x > bounds.max.x || sample.z < bounds.min.z || sample.z > bounds.max.z)
                continue;

            float heightDistance = Mathf.Abs(bounds.max.y - sample.y);
            if (heightDistance < bestDistance)
            {
                bestDistance = heightDistance;
                bestRenderer = renderer;
            }
        }

        if (bestRenderer == null)
            return false;

        Vector3 point = sample;
        point.y = bestRenderer.bounds.max.y;
        fallbackHit.point = point;
        fallbackHit.normal = Vector3.up;
        lastRendererFallbackCount++;
        return true;
    }

    private Vector3 GetRandomPointInArea()
    {
        float halfX = areaSize.x * 0.5f;
        float halfZ = areaSize.y * 0.5f;
        float x = Mathf.Lerp(-halfX, halfX, (float)prng.NextDouble());
        float z = Mathf.Lerp(-halfZ, halfZ, (float)prng.NextDouble());
        return areaCenter + new Vector3(x, 0f, z);
    }

    private bool IsFarEnough(Vector3 point, List<Vector3> placedPositions)
    {
        float sqrMinDistance = minDistance * minDistance;
        for (int i = 0; i < placedPositions.Count; i++)
        {
            Vector3 delta = placedPositions[i] - point;
            delta.y = 0f;
            if (delta.sqrMagnitude < sqrMinDistance)
                return false;
        }

        return true;
    }

    private void PlacePrefab(GameObject prefab, RaycastHit hit, List<Vector3> placedPositions)
    {
        GameObject instance = InstantiateInCurrentEditingScene(prefab);
        if (instance == null)
        {
            instance = Instantiate(prefab);
            MoveToCurrentEditingScene(instance);
        }

        Undo.RegisterCreatedObjectUndo(instance, "Scatter Prefab");
        Vector3 desiredWorldScale = ComputeScale(prefab.transform.localScale);

        instance.transform.position = hit.point;
        instance.transform.rotation = ComputeRotation(instance.transform.rotation, hit.normal);
        instance.transform.localScale = desiredWorldScale;

        if (parentContainer != null)
            instance.transform.SetParent(parentContainer, true);

        if (overridePlacedLayer)
            SetLayerRecursively(instance, placedLayer);

        placedPositions.Add(hit.point);
    }

    private Quaternion ComputeRotation(Quaternion prefabRotation, Vector3 normal)
    {
        Vector3 euler = prefabRotation.eulerAngles;
        float yaw = randomYaw ? Mathf.Lerp(yawRange.x, yawRange.y, (float)prng.NextDouble()) : euler.y;

        Quaternion baseRotation = Quaternion.Euler(
            keepPrefabLocalRotationX ? euler.x : 0f,
            yaw,
            keepPrefabLocalRotationZ ? euler.z : 0f);

        if (!alignToSurfaceNormal)
            return baseRotation;

        Quaternion normalRotation = Quaternion.FromToRotation(Vector3.up, normal);
        return normalRotation * baseRotation;
    }

    private Vector3 ComputeScale(Vector3 prefabScale)
    {
        if (randomUniformScale)
        {
            float min = Mathf.Min(uniformScaleRange.x, uniformScaleRange.y);
            float max = Mathf.Max(uniformScaleRange.x, uniformScaleRange.y);
            float factor = Mathf.Lerp(min, max, (float)prng.NextDouble());
            return prefabScale * factor;
        }

        return new Vector3(
            prefabScale.x * Mathf.Lerp(Mathf.Min(scaleXRange.x, scaleXRange.y), Mathf.Max(scaleXRange.x, scaleXRange.y), (float)prng.NextDouble()),
            prefabScale.y * Mathf.Lerp(Mathf.Min(scaleYRange.x, scaleYRange.y), Mathf.Max(scaleYRange.x, scaleYRange.y), (float)prng.NextDouble()),
            prefabScale.z * Mathf.Lerp(Mathf.Min(scaleZRange.x, scaleZRange.y), Mathf.Max(scaleZRange.x, scaleZRange.y), (float)prng.NextDouble()));
    }

    private List<GameObject> GetValidPrefabs()
    {
        List<GameObject> result = new();
        for (int i = 0; i < prefabs.Count; i++)
        {
            GameObject prefab = prefabs[i];
            if (prefab != null)
                result.Add(prefab);
        }

        return result;
    }

    private void ClearChildren()
    {
        if (parentContainer == null)
        {
            Debug.LogWarning("[PrefabScatterTool] Nessun parent container assegnato.");
            return;
        }

        Undo.SetCurrentGroupName("Clear Scatter Children");
        int undoGroup = Undo.GetCurrentGroup();

        List<GameObject> children = new();
        for (int i = parentContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = parentContainer.GetChild(i);
            if (child != null)
                children.Add(child.gameObject);
        }

        for (int i = 0; i < children.Count; i++)
            Undo.DestroyObjectImmediate(children[i]);

        Undo.CollapseUndoOperations(undoGroup);
        MarkCurrentEditingSceneDirty();
    }

    private static GameObject InstantiateInCurrentEditingScene(GameObject prefab)
    {
        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage != null)
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab, prefabStage.scene);

        return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
    }

    private static void MoveToCurrentEditingScene(GameObject gameObject)
    {
        if (gameObject == null)
            return;

        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage != null && gameObject.scene != prefabStage.scene)
            SceneManager.MoveGameObjectToScene(gameObject, prefabStage.scene);
    }

    private static void MarkCurrentEditingSceneDirty()
    {
        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage != null)
        {
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid())
            EditorSceneManager.MarkSceneDirty(activeScene);
    }

    private void ApplyLayerToChildren()
    {
        if (parentContainer == null)
        {
            Debug.LogWarning("[PrefabScatterTool] Nessun parent container assegnato.");
            return;
        }

        Undo.SetCurrentGroupName("Apply Scatter Layer To Children");
        int undoGroup = Undo.GetCurrentGroup();

        for (int i = 0; i < parentContainer.childCount; i++)
        {
            Transform child = parentContainer.GetChild(i);
            if (child == null)
                continue;

            Undo.RecordObject(child.gameObject, "Apply Layer To Child");
            SetLayerRecursively(child.gameObject, placedLayer);
        }

        Undo.CollapseUndoOperations(undoGroup);
        MarkCurrentEditingSceneDirty();
        Debug.Log($"[PrefabScatterTool] Layer applicato ai figli di '{parentContainer.name}': {LayerMask.LayerToName(placedLayer)} ({placedLayer})");
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null)
            return;

        root.layer = layer;
        Transform transform = root.transform;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null)
                SetLayerRecursively(child.gameObject, layer);
        }
    }
}
