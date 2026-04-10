using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

[System.Serializable]
public class PowerSourceDef
{
    public string keyword;
    public double voltage;
}

[System.Serializable]
public class PowerConfig
{
    public List<PowerSourceDef> sources;
}

public class CircuitNode
{
    public PlacedComponentData Component;
    public string PinId;

    public string GetUniqueId() => $"{Component.instanceId}_{PinId}";
}

public class ElectricalNet
{
    public int NetId;
    public List<CircuitNode> ConnectedNodes = new List<CircuitNode>();
}

// --- UPGRADED: True Capacity Physics ---
public class ActiveBattery
{
    public string instanceId;
    public int sourceIndex;
    public double maxVoltage;
    public double max_mAh; // The total fuel tank
    public double current_mAh; // Fuel remaining
}

public class SimulatorBridge : MonoBehaviour
{
    public CircuitEngine physicsEngine;

    [Header("Configuration")]
    public TextAsset powerConfigFile;

    [Header("Simulation State")]
    public bool isRunning = false;

    // --- NEW: Time Control ---
    // 3600 means 1 real-world second = 1 simulated hour!
    // This allows you to watch a battery drain without waiting 3 days.
    public float timeScaleMultiplier = 3600f;

    public Dictionary<int, double> liveVoltages = new Dictionary<int, double>();
    public Dictionary<string, int> nodeToNetId = new Dictionary<string, int>();
    public double[] liveCurrents;
    public List<ActiveBattery> liveBatteries = new List<ActiveBattery>();

    private List<WireElement> activeWires = new List<WireElement>();
    private List<ElectricalNet> activeNets = new List<ElectricalNet>();

    // Add this helper method to SimulatorBridge.cs
    private int GetPinNet(PlacedComponentData comp, string pinName, int fallback)
    {
        string fullId = $"{comp.instanceId}_{pinName}";
        return nodeToNetId.ContainsKey(fullId) ? nodeToNetId[fullId] : fallback;
    }

    public void ToggleSimulation()
    {
        if (isRunning)
        {
            isRunning = false;
        }
        else
        {
            PushToEngine();
            isRunning = true;
        }
    }

    private void Update()
    {
        if (!isRunning || physicsEngine == null)
            return;

        float dt = Time.deltaTime;
        var result = physicsEngine.stepTime(dt);

        liveVoltages = result.voltages;
        liveCurrents = result.sourceCurrents;

        foreach (var bat in liveBatteries)
        {
            // 1. How many mA are being drawn right now?
            double amps = System.Math.Abs(result.sourceCurrents[bat.sourceIndex]);
            double mA_drawn = amps * 1000.0;

            // 2. How much time passed this frame? (Convert seconds to hours)
            double simulatedHoursPassed = (dt * timeScaleMultiplier) / 3600.0;

            // 3. Drain the fuel tank! (mA * hours)
            bat.current_mAh -= (mA_drawn * simulatedHoursPassed);
            if (bat.current_mAh < 0)
                bat.current_mAh = 0;

            // 4. Calculate live voltage based on battery health percentage
            double healthPercent = bat.current_mAh / bat.max_mAh;
            double liveVoltage = bat.maxVoltage * healthPercent;

            physicsEngine.setVoltageSource(bat.sourceIndex, liveVoltage);
        }
    }

    public void RegisterWire(WireElement wire)
    {
        if (!activeWires.Contains(wire))
        {
            activeWires.Add(wire);
            CompileCircuit();
        }
    }

    public void UnregisterWire(WireElement wire)
    {
        if (activeWires.Contains(wire))
        {
            activeWires.Remove(wire);
            CompileCircuit();
        }
    }

    public void CompileCircuit()
    {
        activeNets.Clear();
        var nodeToNetMap = new Dictionary<string, ElectricalNet>();
        int nextNetId = 1;

        foreach (var wire in activeWires)
        {
            if (wire == null || wire.startComponent == null || wire.endComponent == null)
                continue;

            var startNode = new CircuitNode
            {
                Component = wire.startComponent,
                PinId = wire.startPinId,
            };
            var endNode = new CircuitNode { Component = wire.endComponent, PinId = wire.endPinId };

            string startId = startNode.GetUniqueId();
            string endId = endNode.GetUniqueId();
            nodeToNetMap.TryGetValue(startId, out ElectricalNet startNet);
            nodeToNetMap.TryGetValue(endId, out ElectricalNet endNet);

            if (startNet == null && endNet == null)
            {
                var newNet = new ElectricalNet { NetId = nextNetId++ };
                newNet.ConnectedNodes.Add(startNode);
                newNet.ConnectedNodes.Add(endNode);
                nodeToNetMap[startId] = newNet;
                nodeToNetMap[endId] = newNet;
                activeNets.Add(newNet);
            }
            else if (startNet != null && endNet == null)
            {
                startNet.ConnectedNodes.Add(endNode);
                nodeToNetMap[endId] = startNet;
            }
            else if (startNet == null && endNet != null)
            {
                endNet.ConnectedNodes.Add(startNode);
                nodeToNetMap[startId] = endNet;
            }
            else if (startNet != null && endNet != null && startNet != endNet)
            {
                foreach (var node in endNet.ConnectedNodes)
                {
                    startNet.ConnectedNodes.Add(node);
                    nodeToNetMap[node.GetUniqueId()] = startNet;
                }
                activeNets.Remove(endNet);
            }
        }
        if (isRunning)
            PushToEngine();
    }

    // --- ADD THIS METHOD TO SIMULATORBRIDGE.CS ---
    public void ResetSimulation()
    {
        isRunning = false;
        liveVoltages.Clear();
        liveCurrents = null;

        // Refill all battery "fuel tanks"
        foreach (var bat in liveBatteries)
        {
            bat.current_mAh = bat.max_mAh;
        }

        // Re-push to engine to update the math matrix with full voltages
        PushToEngine();
    }

    public void PushToEngine()
    {
        Dictionary<string, double> savedCapacities = new Dictionary<string, double>();
        foreach (var b in liveBatteries)
            savedCapacities[b.instanceId] = b.current_mAh;

        physicsEngine = new CircuitEngine();
        nodeToNetId.Clear();
        liveBatteries.Clear();
        int vSourceCount = 0;

        foreach (var net in activeNets)
        {
            foreach (var node in net.ConnectedNodes)
                nodeToNetId[node.GetUniqueId()] = net.NetId;
        }

        var uiManager = Object.FindAnyObjectByType<CadUIManager>();
        if (uiManager == null)
            return;

        var allCompVEs = uiManager
            .GetComponent<UIDocument>()
            .rootVisualElement.Query<VisualElement>()
            .Where(v => v.userData is PlacedComponentData)
            .ToList();

        // --- SMART GROUND LOGIC ---
        HashSet<int> posNets = new HashSet<int>();
        HashSet<int> negNets = new HashSet<int>();

        foreach (var ve in allCompVEs)
        {
            var comp = ve.userData as PlacedComponentData;
            if (comp == null || comp.asset == null)
                continue;
            if (comp.asset.Name.Contains("Battery") || comp.asset.Name.Contains("18650"))
            {
                int n0 = GetPinNet(comp, "connector0pin", -1); // Negative
                int n1 = GetPinNet(comp, "connector1pin", -1); // Positive
                if (n0 != -1)
                    negNets.Add(n0);
                if (n1 != -1)
                    posNets.Add(n1);
            }
        }

        // A net is only Ground if it touches a Minus but NEVER touches a Plus
        HashSet<int> groundNetIds = new HashSet<int>(negNets.Where(n => !posNets.Contains(n)));

        // If the circuit is just a loop with no clear "bottom", pick the first negative found
        if (groundNetIds.Count == 0 && negNets.Count > 0)
            groundNetIds.Add(negNets.First());

        int floatingCounter = 9000;
        foreach (var ve in allCompVEs)
        {
            var comp = ve.userData as PlacedComponentData;
            if (comp == null || comp.asset == null)
                continue;

            int pin0 = GetPinNet(comp, "connector0pin", floatingCounter++);
            int pin1 = GetPinNet(comp, "connector1pin", floatingCounter++);

            if (groundNetIds.Contains(pin0))
                pin0 = 0;
            if (groundNetIds.Contains(pin1))
                pin1 = 0;

            string compName = comp.asset.Name;
            if (compName.Contains("Resistor"))
            {
                physicsEngine.addResistor(pin0, pin1, comp.componentValue);
            }
            else if (compName.Contains("LED") || compName.Contains("OLED"))
            {
                physicsEngine.addResistor(pin0, pin1, 150);
            }
            else if (compName.Contains("Battery") || compName.Contains("18650"))
            {
                // Check if this is a 3.0V or 3.7V battery based on name
                double volts = compName.Contains("3.0V") ? 3.0 : 3.7;
                double capacity = 2500.0;
                if (savedCapacities.ContainsKey(comp.instanceId))
                    capacity = savedCapacities[comp.instanceId];

                int internalNode = floatingCounter++;
                physicsEngine.addVoltageSource(internalNode, pin0, volts);
                physicsEngine.addResistor(pin1, internalNode, 0.1);

                liveBatteries.Add(
                    new ActiveBattery
                    {
                        instanceId = comp.instanceId,
                        sourceIndex = vSourceCount++,
                        maxVoltage = volts,
                        max_mAh = 2500.0,
                        current_mAh = capacity,
                    }
                );
            }
        }
    }
}
