using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class WireElement : VisualElement
{
    public PlacedComponentData startComponent;
    public string startPinId;
    public PlacedComponentData endComponent;
    public string endPinId;

    public List<Vector2> points = new List<Vector2>();
    public Color wireColor = Color.green;
    public float thickness = 4f;
    public bool isSelected = false;

    public WireElement()
    {
        // Tell UI Toolkit how to draw this element using the vector API
        generateVisualContent += OnGenerateVisualContent;

        // Stretch across the whole canvas so it can draw anywhere
        style.position = Position.Absolute;
        style.top = 0;
        style.left = 0;
        style.right = 0;
        style.bottom = 0;

        // IMPORTANT: This makes the visible line clickable, ignoring the transparent bounds!
        pickingMode = PickingMode.Position;
    }

    public void UpdateWire(List<Vector2> newPoints)
    {
        points = new List<Vector2>(newPoints);
        MarkDirtyRepaint(); // Forces Unity to redraw the line
    }

    private void OnGenerateVisualContent(MeshGenerationContext mgc)
    {
        if (points.Count < 2)
            return;

        // --- THE FIX: Use Unity's built-in painter property ---
        var paint = mgc.painter2D;

        // If selected, draw it White and slightly thicker!
        paint.strokeColor = isSelected ? Color.white : wireColor;
        paint.lineWidth = isSelected ? thickness + 2f : thickness;

        paint.lineCap = LineCap.Round;
        paint.lineJoin = LineJoin.Round;

        // Draw the path through all waypoints
        paint.BeginPath();
        paint.MoveTo(points[0]);
        for (int i = 1; i < points.Count; i++)
        {
            paint.LineTo(points[i]);
        }
        paint.Stroke();
    }

    // --- THE CUSTOM HITBOX OVERRIDE ---
    // Forces UI Toolkit to only let you click the actual drawn line!
    public override bool ContainsPoint(Vector2 localPoint)
    {
        if (points == null || points.Count < 2)
            return false;

        // Check if the mouse is within 10 pixels of any segment of the wire
        for (int i = 0; i < points.Count - 1; i++)
        {
            float dist = DistanceToSegment(localPoint, points[i], points[i + 1]);
            if (dist <= thickness + 10f)
                return true; // 10f is a generous margin for the user's mouse
        }
        return false;
    }

    private float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        Vector2 ap = p - a;
        if (ab.sqrMagnitude == 0f)
            return ap.magnitude;
        float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / ab.sqrMagnitude);
        Vector2 projection = a + t * ab;
        return Vector2.Distance(p, projection);
    }
}
