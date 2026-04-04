using UnityEngine;
using UnityEngine.EventSystems;

public class UIDragSpawner : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public GameObject prefabToSpawn; // Assign the ResistorPrefab here in the Inspector
    private GameObject ghostObject;

    public void OnBeginDrag(PointerEventData eventData)
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;

        ghostObject = Instantiate(prefabToSpawn, mousePos, Quaternion.identity);

        // Make it semi-transparent while dragging
        SpriteRenderer[] sprites = ghostObject.GetComponentsInChildren<SpriteRenderer>();
        foreach (var s in sprites)
            s.color = new Color(1, 1, 1, 0.5f);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (ghostObject == null)
            return;

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        // Snap to grid
        mousePos.x = Mathf.Round(mousePos.x);
        mousePos.y = Mathf.Round(mousePos.y);

        ghostObject.transform.position = mousePos;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (ghostObject == null)
            return;

        // Restore opacity
        SpriteRenderer[] sprites = ghostObject.GetComponentsInChildren<SpriteRenderer>();
        foreach (var s in sprites)
            s.color = Color.white;

        ghostObject = null; // Drop it in the world permanently!
    }
}
