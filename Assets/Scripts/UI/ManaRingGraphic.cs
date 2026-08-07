using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ManaRingGraphic : MaskableGraphic
{
    [SerializeField, Range(2f, 40f)] private float thickness = 14f;
    [SerializeField, Range(16, 256)] private int segments = 96;
    [SerializeField] private float startAngle = -45f;
    [SerializeField, Range(90f, 360f)] private float sweepAngle = 270f;
    [SerializeField] private Color trackColor = new(0.08f, 0.1f, 0.14f, 0.9f);
    [SerializeField] private Color redZoneTrackColor = new(0.28f, 0.035f, 0.04f, 0.95f);
    [SerializeField] private Color manaColor = new(0.18f, 0.78f, 1f, 1f);
    [SerializeField] private Color warningColor = new(1f, 0.68f, 0.12f, 1f);
    [SerializeField] private Color saturatedColor = new(1f, 0.08f, 0.06f, 1f);

    private ManaStatus status = new(
        50f,
        90f,
        100f,
        130f,
        3f,
        0f,
        ManaState.Normal);

    public void SetStatus(ManaStatus value)
    {
        status = value;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        float displayMaximum = Mathf.Max(1f, status.DisplayMaximum);
        float saturationStart = Mathf.Clamp01(status.SaturationThreshold / displayMaximum);
        float current = Mathf.Clamp01(status.Current / displayMaximum);

        AddArc(vertexHelper, 0f, 1f, trackColor);
        AddArc(vertexHelper, saturationStart, 1f, redZoneTrackColor);

        float normalEnd = Mathf.Min(current, saturationStart);
        Color activeManaColor = status.IsWarning ? warningColor : manaColor;
        AddArc(vertexHelper, 0f, normalEnd, activeManaColor);

        if (current > saturationStart)
        {
            AddArc(vertexHelper, saturationStart, current, saturatedColor);
        }
    }

    private void AddArc(VertexHelper vertexHelper, float from, float to, Color arcColor)
    {
        if (to <= from)
        {
            return;
        }

        Rect rect = rectTransform.rect;
        float outerRadius = Mathf.Min(rect.width, rect.height) * 0.5f;
        float innerRadius = Mathf.Max(0f, outerRadius - thickness);
        int firstSegment = Mathf.FloorToInt(from * segments);
        int lastSegment = Mathf.CeilToInt(to * segments);

        for (int index = firstSegment; index < lastSegment; index++)
        {
            float segmentStart = Mathf.Max(from, (float)index / segments);
            float segmentEnd = Mathf.Min(to, (float)(index + 1) / segments);
            AddSegment(vertexHelper, segmentStart, segmentEnd, innerRadius, outerRadius, arcColor);
        }
    }

    private void AddSegment(
        VertexHelper vertexHelper,
        float from,
        float to,
        float innerRadius,
        float outerRadius,
        Color arcColor)
    {
        float startRadians = (startAngle + sweepAngle * from) * Mathf.Deg2Rad;
        float endRadians = (startAngle + sweepAngle * to) * Mathf.Deg2Rad;
        Vector2 startDirection = new(Mathf.Cos(startRadians), Mathf.Sin(startRadians));
        Vector2 endDirection = new(Mathf.Cos(endRadians), Mathf.Sin(endRadians));
        int vertexStart = vertexHelper.currentVertCount;

        AddVertex(vertexHelper, startDirection * innerRadius, arcColor);
        AddVertex(vertexHelper, startDirection * outerRadius, arcColor);
        AddVertex(vertexHelper, endDirection * outerRadius, arcColor);
        AddVertex(vertexHelper, endDirection * innerRadius, arcColor);

        vertexHelper.AddTriangle(vertexStart, vertexStart + 1, vertexStart + 2);
        vertexHelper.AddTriangle(vertexStart, vertexStart + 2, vertexStart + 3);
    }

    private static void AddVertex(VertexHelper vertexHelper, Vector2 position, Color vertexColor)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = vertexColor;
        vertexHelper.AddVert(vertex);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        thickness = Mathf.Max(2f, thickness);
        segments = Mathf.Clamp(segments, 16, 256);
        SetVerticesDirty();
    }
#endif
}
