using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.ProBuilder;
using UnityEngine;
using UnityEngine.ProBuilder;

public class ProBuilderFloorDeformerWindow : EditorWindow
{
    [SerializeField] private ProBuilderMesh targetMesh;
    [SerializeField] private bool randomizeSeed = true;
    [SerializeField] private int seed = 12345;
    [SerializeField] private float heightAmplitude = 0.45f;
    [SerializeField] private float noiseScale = 3.5f;
    [SerializeField] private float secondaryNoiseStrength = 0.35f;
    [SerializeField] private float borderInsetPercent = 0.12f;
    [SerializeField] private bool resetToFlatPlaneBeforeApply = true;

    [MenuItem("Tools/Arcadia/ProBuilder Floor Deformer")]
    public static void OpenWindow()
    {
        var window = GetWindow<ProBuilderFloorDeformerWindow>("Floor Deformer");
        window.minSize = new Vector2(360f, 300f);
        window.Show();
    }

    private void OnSelectionChange()
    {
        if (targetMesh == null)
            TryUseCurrentSelection();

        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("ProBuilder Floor Deformer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Deforma un ProBuilderMesh usando noise seedato. Pensato per pavimenti di stanza: "
            + "tiene i bordi piu' piatti per non rompere porte, muri e navigazione.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            targetMesh = (ProBuilderMesh)EditorGUILayout.ObjectField("Target Mesh", targetMesh, typeof(ProBuilderMesh), true);
            if (GUILayout.Button("Use Selection", GUILayout.Width(110f)))
                TryUseCurrentSelection(force: true);
        }

        EditorGUILayout.Space(4f);
        randomizeSeed = EditorGUILayout.Toggle("Randomize Seed", randomizeSeed);
        using (new EditorGUI.DisabledScope(randomizeSeed))
        {
            seed = EditorGUILayout.IntField("Seed", seed);
        }

        heightAmplitude = EditorGUILayout.Slider("Height Amplitude", heightAmplitude, 0f, 3f);
        noiseScale = Mathf.Max(0.1f, EditorGUILayout.FloatField("Noise Scale", noiseScale));
        secondaryNoiseStrength = EditorGUILayout.Slider("Secondary Noise", secondaryNoiseStrength, 0f, 1f);
        borderInsetPercent = EditorGUILayout.Slider("Flat Border", borderInsetPercent, 0f, 0.45f);
        resetToFlatPlaneBeforeApply = EditorGUILayout.Toggle("Reset To Flat First", resetToFlatPlaneBeforeApply);

        if (targetMesh != null)
        {
            var meshFilter = targetMesh.GetComponent<MeshFilter>();
            int vertexCount = meshFilter != null && meshFilter.sharedMesh != null ? meshFilter.sharedMesh.vertexCount : 0;
            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                $"Target: {targetMesh.name}\n"
                + $"Vertices: {vertexCount}\n"
                + $"Suggerimento: prima subdividi il floor in ProBuilder, poi usa questo tool.",
                MessageType.None);
        }

        EditorGUILayout.Space(10f);
        using (new EditorGUI.DisabledScope(targetMesh == null))
        {
            if (GUILayout.Button("Deform Floor", GUILayout.Height(34f)))
                DeformSelectedMesh();

            if (GUILayout.Button("Flatten Floor", GUILayout.Height(28f)))
                FlattenSelectedMesh();
        }
    }

    private void TryUseCurrentSelection(bool force = false)
    {
        if (!force && targetMesh != null)
            return;

        if (Selection.activeGameObject == null)
            return;

        targetMesh = Selection.activeGameObject.GetComponent<ProBuilderMesh>();
    }

    private void DeformSelectedMesh()
    {
        if (targetMesh == null)
        {
            Debug.LogWarning("[ProBuilderFloorDeformer] Nessun ProBuilderMesh selezionato.");
            return;
        }

        var vertices = targetMesh.GetVertices();
        if (vertices == null || vertices.Length == 0)
        {
            Debug.LogWarning("[ProBuilderFloorDeformer] Il mesh selezionato non contiene vertici.");
            return;
        }

        int effectiveSeed = randomizeSeed ? System.Environment.TickCount ^ System.DateTime.Now.Millisecond : seed;
        float offsetX = HashToOffset(effectiveSeed, 0);
        float offsetZ = HashToOffset(effectiveSeed, 1);
        float offsetX2 = HashToOffset(effectiveSeed, 2);
        float offsetZ2 = HashToOffset(effectiveSeed, 3);

        Bounds localBounds = CalculateLocalBounds(vertices);
        float baseY = CalculateAverageY(vertices);

        Undo.RegisterCompleteObjectUndo(targetMesh, "Deform ProBuilder Floor");
        Undo.RegisterCompleteObjectUndo(targetMesh.gameObject, "Deform ProBuilder Floor");

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 pos = vertices[i].position;

            float normalizedX = Mathf.InverseLerp(localBounds.min.x, localBounds.max.x, pos.x);
            float normalizedZ = Mathf.InverseLerp(localBounds.min.z, localBounds.max.z, pos.z);
            float edgeDistance = Mathf.Min(normalizedX, 1f - normalizedX, normalizedZ, 1f - normalizedZ);
            float borderFactor = EvaluateBorderFactor(edgeDistance);

            float noiseX = (pos.x + offsetX) / noiseScale;
            float noiseZ = (pos.z + offsetZ) / noiseScale;
            float primaryNoise = Mathf.PerlinNoise(noiseX, noiseZ);

            float secondaryNoise = Mathf.PerlinNoise(
                (pos.x + offsetX2) / (noiseScale * 0.55f),
                (pos.z + offsetZ2) / (noiseScale * 0.55f));

            float combined = Mathf.Lerp(primaryNoise, secondaryNoise, secondaryNoiseStrength);
            float signedNoise = combined * 2f - 1f;
            float centerWeight = ComputeCenterWeight(pos, localBounds);
            float displacement = signedNoise * heightAmplitude * borderFactor * centerWeight;

            float finalY = resetToFlatPlaneBeforeApply ? baseY + displacement : pos.y + displacement;
            vertices[i].position = new Vector3(pos.x, finalY, pos.z);
        }

        ApplyVertexChanges(vertices);
        Debug.Log($"[ProBuilderFloorDeformer] Floor deformato: '{targetMesh.name}' | Seed: {effectiveSeed}");
    }

    private void FlattenSelectedMesh()
    {
        if (targetMesh == null)
        {
            Debug.LogWarning("[ProBuilderFloorDeformer] Nessun ProBuilderMesh selezionato.");
            return;
        }

        var vertices = targetMesh.GetVertices();
        if (vertices == null || vertices.Length == 0)
            return;

        float baseY = CalculateAverageY(vertices);

        Undo.RegisterCompleteObjectUndo(targetMesh, "Flatten ProBuilder Floor");
        Undo.RegisterCompleteObjectUndo(targetMesh.gameObject, "Flatten ProBuilder Floor");

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 pos = vertices[i].position;
            vertices[i].position = new Vector3(pos.x, baseY, pos.z);
        }

        ApplyVertexChanges(vertices);
        Debug.Log($"[ProBuilderFloorDeformer] Floor appiattito: '{targetMesh.name}'");
    }

    private void ApplyVertexChanges(Vertex[] vertices)
    {
        targetMesh.SetVertices(vertices);
        targetMesh.ToMesh();
        targetMesh.Refresh();
        ProBuilderEditor.Refresh();
        UnityEditor.EditorUtility.SetDirty(targetMesh);
        UnityEditor.EditorUtility.SetDirty(targetMesh.gameObject);

        var meshCollider = targetMesh.GetComponent<MeshCollider>();
        var meshFilter = targetMesh.GetComponent<MeshFilter>();
        if (meshCollider != null && meshFilter != null)
            meshCollider.sharedMesh = meshFilter.sharedMesh;

        if (targetMesh.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(targetMesh.gameObject.scene);
    }

    private static Bounds CalculateLocalBounds(Vertex[] vertices)
    {
        Bounds bounds = new Bounds(vertices[0].position, Vector3.zero);
        for (int i = 1; i < vertices.Length; i++)
            bounds.Encapsulate(vertices[i].position);
        return bounds;
    }

    private static float CalculateAverageY(Vertex[] vertices)
    {
        float total = 0f;
        for (int i = 0; i < vertices.Length; i++)
            total += vertices[i].position.y;
        return total / vertices.Length;
    }

    private float EvaluateBorderFactor(float edgeDistance)
    {
        if (borderInsetPercent <= 0.0001f)
            return 1f;

        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edgeDistance / borderInsetPercent));
    }

    private static float ComputeCenterWeight(Vector3 position, Bounds bounds)
    {
        float centerX = Mathf.InverseLerp(bounds.min.x, bounds.max.x, position.x) * 2f - 1f;
        float centerZ = Mathf.InverseLerp(bounds.min.z, bounds.max.z, position.z) * 2f - 1f;
        float radial = Mathf.Clamp01(1f - Mathf.Sqrt(centerX * centerX + centerZ * centerZ));
        return Mathf.Lerp(0.85f, 1f, radial);
    }

    private static float HashToOffset(int baseSeed, int salt)
    {
        unchecked
        {
            int hash = baseSeed;
            hash = (hash * 397) ^ salt;
            hash ^= (hash << 13);
            hash ^= (hash >> 17);
            hash ^= (hash << 5);
            return Mathf.Abs(hash % 100000) / 13.37f;
        }
    }
}
