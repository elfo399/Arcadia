using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("")]
public sealed class QuestSelectionFrameEffect : BaseMeshEffect
{
    [SerializeField] private Color effectColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Vector2 firstOffset = new Vector2(0f, -3f);
    [SerializeField] private Vector2 secondOffset = new Vector2(0f, 3f);
    [SerializeField] private bool useGraphicAlpha = true;

    public void Configure(Color color, Vector2 offsetA, Vector2 offsetB)
    {
        effectColor = color;
        firstOffset = offsetA;
        secondOffset = offsetB;

        if (graphic != null)
            graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vertexHelper)
    {
        if (!IsActive() || vertexHelper.currentVertCount == 0)
            return;

        var vertices = new List<UIVertex>();
        vertexHelper.GetUIVertexStream(vertices);

        int start = 0;
        int end = vertices.Count;
        ApplyEffect(vertices, start, end, firstOffset);

        start = end;
        end = vertices.Count;
        ApplyEffect(vertices, start, end, secondOffset);

        vertexHelper.Clear();
        vertexHelper.AddUIVertexTriangleStream(vertices);
    }

    private void ApplyEffect(List<UIVertex> vertices, int start, int end, Vector2 offset)
    {
        int requiredCapacity = vertices.Count + end - start;
        if (vertices.Capacity < requiredCapacity)
            vertices.Capacity = requiredCapacity;

        for (int i = start; i < end; i++)
            vertices.Add(vertices[i]);

        for (int i = start; i < end; i++)
        {
            UIVertex vertex = vertices[i];
            Vector3 position = vertex.position;
            position.x += offset.x;
            position.y += offset.y;
            vertex.position = position;

            Color32 color = effectColor;
            if (useGraphicAlpha)
                color.a = (byte)(color.a * vertex.color.a / 255);
            vertex.color = color;
            vertices[i] = vertex;
        }
    }
}
