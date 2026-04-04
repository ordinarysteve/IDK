using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class Wire : MonoBehaviour {
    private LineRenderer line;
    public Transform startNode;
    public Transform endNode;

    void Awake() {
        line = GetComponent<LineRenderer>();
        line.positionCount = 2; // Start with a simple straight line
    }

    // Call this every frame while the user is dragging the mouse
    public void UpdateDrawing(Vector3 currentMousePosition) {
        line.SetPosition(0, startNode.position);
        line.SetPosition(1, currentMousePosition);
    }

    // Call this when the user drops the wire on a valid target
    public void FinalizeWire(Transform targetNode) {
        endNode = targetNode;
        line.SetPosition(1, endNode.position);
    }
    
    public void Beautify() {
        if (startNode == null || endNode == null) return;

        Vector3 start = startNode.position;
        Vector3 end = endNode.position;

        // If they are already aligned on an axis, it's already beautiful!
        if (Mathf.Approximately(start.x, end.x) || Mathf.Approximately(start.y, end.y)) {
            return; 
        }

        // To make a right angle, we need 3 points instead of 2
        line.positionCount = 3;
        line.SetPosition(0, start);

        // Decide which way to bend. Let's default to routing horizontally first, then vertically.
        // The corner point shares the Y of the start, and the X of the end.
        Vector3 cornerPoint = new Vector3(end.x, start.y, start.z);
        
        line.SetPosition(1, cornerPoint);
        line.SetPosition(2, end);
    }
}