using UnityEngine;

// This tells Unity to run our visual updates even when we aren't pressing Play!
[ExecuteAlways]
public class CircuitComponent : MonoBehaviour
{
    public enum ComponentType
    {
        Resistor,
        Capacitor,
        VoltageSource,
        Inductor,
        Diode,
        NMOSFET,
    }

    public ComponentType type;

    // We can go back to a normal public variable now
    public double primaryValue = 1000;

    [HideInInspector]
    public int engineId = -1;
    public Terminal[] terminals;

    void Awake()
    {
        if (terminals == null || terminals.Length == 0)
        {
            terminals = GetComponentsInChildren<Terminal>();
        }
    }

    void Start()
    {
        UpdateVisuals();
    }

    // MAGIC UNITY FUNCTION: Runs every time you type a number in the Inspector
    void OnValidate()
    {
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (type == ComponentType.Resistor)
        {
            ResistorColorManager colorManager = GetComponent<ResistorColorManager>();
            if (colorManager != null)
            {
                colorManager.UpdateColorBands(primaryValue);
            }
        }
    }
}
