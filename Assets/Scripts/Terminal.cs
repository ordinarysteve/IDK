using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CircleCollider2D))]
public class Terminal : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler {
    
    public int nodeId = -1; // -1 means unconnected
    public CircuitComponent parentComponent; 

    void Awake() {
        parentComponent = GetComponentInParent<CircuitComponent>();
    }

    public void OnPointerEnter(PointerEventData eventData) {
        transform.localScale = Vector3.one * 1.5f; // Highlight on hover
    }

    public void OnPointerExit(PointerEventData eventData) {
        transform.localScale = Vector3.one;
    }

    public void OnPointerDown(PointerEventData eventData) {
        WireManager.Instance.StartWireFromTerminal(this);
    }
}