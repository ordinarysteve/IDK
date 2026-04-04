using UnityEngine;

public class WireManager : MonoBehaviour {
    public static WireManager Instance;
    public GameObject wirePrefab; // Assign in Inspector

    private LineRenderer activeLine;
    private Terminal startingTerminal;

    void Awake() { Instance = this; }

    void Update() {
        if (activeLine != null) {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0;
            mousePos.x = Mathf.Round(mousePos.x);
            mousePos.y = Mathf.Round(mousePos.y);

            activeLine.SetPosition(1, mousePos);

            // Left click to place the wire
            if (Input.GetMouseButtonDown(0)) {
                FinishWire();
            }
            // Right click to cancel drawing
            if (Input.GetMouseButtonDown(1)) {
                Destroy(activeLine.gameObject);
                activeLine = null;
                startingTerminal = null;
            }
        }
    }

    public void StartWireFromTerminal(Terminal term) {
        if (activeLine != null) return; // Already drawing

        startingTerminal = term;
        Vector3 startPos = term.transform.position;
        startPos.z = 0;

        GameObject newWire = Instantiate(wirePrefab, startPos, Quaternion.identity);
        activeLine = newWire.GetComponent<LineRenderer>();
        activeLine.positionCount = 2;
        activeLine.SetPosition(0, startPos);
        activeLine.SetPosition(1, startPos);
    }

    private void FinishWire() {
        Vector3 endPos = activeLine.GetPosition(1);
        
        // Check if we dropped the wire onto a terminal hole
        Collider2D hit = Physics2D.OverlapPoint(endPos);
        if (hit != null) {
            Terminal endTerminal = hit.GetComponent<Terminal>();
            
            if (endTerminal != null && endTerminal != startingTerminal) {
                Debug.Log($"Wired {startingTerminal.parentComponent.type} to {endTerminal.parentComponent.type}!");
                // NOTE: Here is where you will tell your C# CircuitEngine that these two nodes are now merged!
            }
        }

        activeLine = null;
        startingTerminal = null;
    }
}