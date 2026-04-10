using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Resistor
{
    public int n1 { get; }
    public int n2 { get; }
    public double resistance { get; }
    public double tolerance { get; }

    public Resistor(int n1, int n2, double resistance, double tolerance = 5)
    {
        if (resistance < 0)
        {
            throw new Exception("Resistance cannot be negative.");
        }
        this.n1 = n1;
        this.n2 = n2;
        this.resistance = resistance;
        this.tolerance = tolerance;
    }

    public string stamp()
    {
        return $"{formatResistance()} ±{tolerance}%";
    }

    private string formatResistance()
    {
        if (this.resistance >= 1_000_000)
        {
            return $"{(this.resistance / 1_000_000).ToString("0.0").Replace(".0", "")}MΩ";
        }
        if (this.resistance >= 1_000)
        {
            return $"{(this.resistance / 1_000).ToString("0.0").Replace(".0", "")}kΩ";
        }
        return $"{this.resistance}Ω";
    }
}

public class VoltageSource
{
    public int nodePositive;
    public int nodeNegative;
    public double voltage;
    public int mnaIndex;

    public VoltageSource(int nodePositive, int nodeNegative, double voltage, int mnaIndex)
    {
        this.nodePositive = nodePositive;
        this.nodeNegative = nodeNegative;
        this.voltage = voltage;
        this.mnaIndex = mnaIndex;
    }

    public void stampMatrix(double[][] matrix, double[] rhs)
    {
        if (this.nodePositive >= 0)
        {
            matrix[this.nodePositive][this.mnaIndex] += 1;
            matrix[this.mnaIndex][this.nodePositive] += 1;
        }
        if (this.nodeNegative >= 0)
        {
            matrix[this.nodeNegative][this.mnaIndex] -= 1;
            matrix[this.mnaIndex][this.nodeNegative] -= 1;
        }
        rhs[this.mnaIndex] += this.voltage;
    }
}

public class Capacitor
{
    private double vPrev = 0;
    public int n1;
    public int n2;
    public double capacitance { get; }

    public Capacitor(int n1, int n2, double capacitance)
    {
        this.n1 = n1;
        this.n2 = n2;
        this.capacitance = capacitance;
    }

    public void stamp(double[][] matrix, double[] rhs, double dt)
    {
        double gEq = this.capacitance / dt;
        double iEq = gEq * this.vPrev;
        if (this.n1 >= 0)
        {
            matrix[this.n1][this.n1] += gEq;
            rhs[this.n1] += iEq;
        }
        if (this.n2 >= 0)
        {
            matrix[this.n2][this.n2] += gEq;
            rhs[this.n2] -= iEq;
        }
        if (this.n1 >= 0 && this.n2 >= 0)
        {
            matrix[this.n1][this.n2] -= gEq;
            matrix[this.n2][this.n1] -= gEq;
        }
    }

    public void updateState(double v1, double v2)
    {
        this.vPrev = v1 - v2;
    }
}

public class Inductor
{
    public double prevCurrent;
    public int nodePositive;
    public int nodeNegative;
    public double inductance { get; }
    public int mnaIndex;

    public Inductor(
        int nodePositive,
        int nodeNegative,
        double inductance,
        int mnaIndex,
        double initialCurrent = 0
    )
    {
        if (inductance <= 0)
        {
            throw new Exception("Inductance must be positive.");
        }
        this.nodePositive = nodePositive;
        this.nodeNegative = nodeNegative;
        this.inductance = inductance;
        this.mnaIndex = mnaIndex;
        this.prevCurrent = initialCurrent;
    }

    public void stampMatrix(double[][] matrix, double[] rhs, double dt)
    {
        double lOverDt = this.inductance / dt;
        if (this.nodePositive >= 0)
        {
            matrix[this.nodePositive][this.mnaIndex] += 1;
            matrix[this.mnaIndex][this.nodePositive] += 1;
        }
        if (this.nodeNegative >= 0)
        {
            matrix[this.nodeNegative][this.mnaIndex] -= 1;
            matrix[this.mnaIndex][this.nodeNegative] -= 1;
        }
        matrix[this.mnaIndex][this.mnaIndex] -= lOverDt;
        rhs[this.mnaIndex] -= lOverDt * this.prevCurrent;
    }

    public void updateState(double current)
    {
        this.prevCurrent = current;
    }
}

public interface NonLinearComponent
{
    int[] getNodes();
    void stamp(
        double[][] matrix,
        double[] rhs,
        Dictionary<int, int> nodeToIndex,
        Dictionary<int, double> prevVoltages
    );
}

public class Diode : NonLinearComponent
{
    public int n1; // Anode
    public int n2; // Cathode
    public double Is { get; }
    public double n { get; }
    public double Vt { get; }

    public Diode(int n1, int n2, double Is = 1e-14, double n = 1, double Vt = 0.02585)
    {
        this.n1 = n1;
        this.n2 = n2;
        this.Is = Is;
        this.n = n;
        this.Vt = Vt;
    }

    public int[] getNodes()
    {
        return new int[] { n1, n2 };
    }

    public void stamp(
        double[][] matrix,
        double[] rhs,
        Dictionary<int, int> nodeToIndex,
        Dictionary<int, double> prevVoltages
    )
    {
        double vPrevIter =
            (prevVoltages.ContainsKey(this.n1) ? prevVoltages[this.n1] : 0)
            - (prevVoltages.ContainsKey(this.n2) ? prevVoltages[this.n2] : 0);

        // Diode voltage limiting to aid convergence
        double vLimited = Math.Max(-1, Math.Min(vPrevIter, 1.5));
        double nVt = this.n * this.Vt;
        double expTerm = Math.Exp(vLimited / nVt);
        double Id = this.Is * (expTerm - 1);
        double Geq = (this.Is / nVt) * expTerm;
        double Ieq = Id - Geq * vLimited;

        int idx1 = nodeToIndex.ContainsKey(this.n1) ? nodeToIndex[this.n1] : -1;
        int idx2 = nodeToIndex.ContainsKey(this.n2) ? nodeToIndex[this.n2] : -1;

        if (idx1 >= 0)
        {
            matrix[idx1][idx1] += Geq;
            rhs[idx1] -= Ieq;
        }
        if (idx2 >= 0)
        {
            matrix[idx2][idx2] += Geq;
            rhs[idx2] += Ieq;
        }
        if (idx1 >= 0 && idx2 >= 0)
        {
            matrix[idx1][idx2] -= Geq;
            matrix[idx2][idx1] -= Geq;
        }
    }
}

public class NMOSFET : NonLinearComponent
{
    public int d;
    public int g;
    public int s;
    public double vth { get; }
    public double kp { get; }
    public double lambda { get; }

    public NMOSFET(int d, int g, int s, double vth = 1.0, double kp = 2e-3, double lambda = 0.02)
    {
        this.d = d;
        this.g = g;
        this.s = s;
        this.vth = vth;
        this.kp = kp;
        this.lambda = lambda;
    }

    public int[] getNodes()
    {
        return new int[] { d, g, s };
    }

    public void stamp(
        double[][] matrix,
        double[] rhs,
        Dictionary<int, int> nodeToIndex,
        Dictionary<int, double> nodeVoltages
    )
    {
        double vD = nodeVoltages.ContainsKey(this.d) ? nodeVoltages[this.d] : 0;
        double vG = nodeVoltages.ContainsKey(this.g) ? nodeVoltages[this.g] : 0;
        double vS = nodeVoltages.ContainsKey(this.s) ? nodeVoltages[this.s] : 0;

        double vgs = vG - vS;
        double vds = vD - vS;

        // Handle symmetry: if Vds is negative, the device conducts in reverse (S/D swap)
        int effectiveD = this.d;
        int effectiveS = this.s;
        if (vds < 0)
        {
            vds = -vds;
            vgs = vG - vD;
            effectiveD = this.s;
            effectiveS = this.d;
        }

        double id = 0;
        double gm = 0;
        double gds = 0;
        double vgst = vgs - this.vth;

        if (vgst > 0)
        {
            // Device is on
            double lamTerm = 1 + this.lambda * vds;
            if (vds < vgst)
            {
                // Linear (Triode) Region
                id = this.kp * (vgst * vds - 0.5 * vds * vds) * lamTerm;
                gm = this.kp * vds * lamTerm;
                gds =
                    this.kp * (vgst - vds) * lamTerm
                    + this.kp * (vgst * vds - 0.5 * vds * vds) * this.lambda;
            }
            else
            {
                // Saturation Region
                id = 0.5 * this.kp * vgst * vgst * lamTerm;
                gm = this.kp * vgst * lamTerm;
                gds = 0.5 * this.kp * vgst * vgst * this.lambda;
            }
        }

        // Equivalent current source for Newton-Raphson
        double ieq = id - gm * vgs - gds * vds;

        int idxD = nodeToIndex.ContainsKey(effectiveD) ? nodeToIndex[effectiveD] : -1;
        int idxG = nodeToIndex.ContainsKey(this.g) ? nodeToIndex[this.g] : -1;
        int idxS = nodeToIndex.ContainsKey(effectiveS) ? nodeToIndex[effectiveS] : -1;

        // Local function replacement for the Action delegate performance fix
        void AddToMatrix(int r, int c, double val)
        {
            if (r >= 0 && c >= 0)
                matrix[r][c] += val;
        }

        // Drain KCL contribution
        AddToMatrix(idxD, idxD, gds);
        AddToMatrix(idxD, idxG, gm);
        AddToMatrix(idxD, idxS, -(gm + gds));
        if (idxD >= 0)
            rhs[idxD] -= ieq;

        // Source KCL contribution
        AddToMatrix(idxS, idxD, -gds);
        AddToMatrix(idxS, idxG, -gm);
        AddToMatrix(idxS, idxS, gm + gds);
        if (idxS >= 0)
            rhs[idxS] += ieq;
    }
}

public class CircuitResult
{
    public Dictionary<int, double> voltages;
    public double[] sourceCurrents;
}

public class CircuitEngine
{
    private List<Resistor> resistors = new List<Resistor>();
    private List<VoltageSource> vSources = new List<VoltageSource>();
    private List<(int from, int to, double current)> iSources = new List<(int, int, double)>();
    private List<Capacitor> capacitors = new List<Capacitor>();
    private List<Inductor> inductors = new List<Inductor>();
    private List<NonLinearComponent> nonLinearComponents = new List<NonLinearComponent>();
    private HashSet<int> nodes = new HashSet<int>();

    public void addResistor(int n1, int n2, double resistance, double tolerance = 5)
    {
        this.resistors.Add(new Resistor(n1, n2, resistance, tolerance));
        if (n1 != 0)
            this.nodes.Add(n1);
        if (n2 != 0)
            this.nodes.Add(n2);
    }

    public void addVoltageSource(int p, int n, double voltage)
    {
        this.vSources.Add(new VoltageSource(p, n, voltage, -1));
        if (p != 0)
            this.nodes.Add(p);
        if (n != 0)
            this.nodes.Add(n);
    }

    public void setVoltageSource(int index, double newVoltage)
    {
        if (index >= 0 && index < vSources.Count)
        {
            vSources[index].voltage = newVoltage;
        }
    }

    public void addCurrentSource(int from, int to, double current)
    {
        this.iSources.Add((from, to, current));
        if (from != 0)
            this.nodes.Add(from);
        if (to != 0)
            this.nodes.Add(to);
    }

    public void addCapacitor(int n1, int n2, double capacitance)
    {
        this.capacitors.Add(new Capacitor(n1, n2, capacitance));
        if (n1 != 0)
            this.nodes.Add(n1);
        if (n2 != 0)
            this.nodes.Add(n2);
    }

    public void addInductor(int n1, int n2, double inductance, double initialCurrent = 0)
    {
        this.inductors.Add(new Inductor(n1, n2, inductance, -1, initialCurrent));
        if (n1 != 0)
            this.nodes.Add(n1);
        if (n2 != 0)
            this.nodes.Add(n2);
    }

    public void addNonLinearComponent(NonLinearComponent component)
    {
        this.nonLinearComponents.Add(component);
        foreach (int node in component.getNodes())
        {
            if (node != 0)
                this.nodes.Add(node);
        }
    }

    // Paste this inside ElectricPhysicsEngine.cs
    public void AddComponent(
        FritzingComponentAsset asset,
        UnityEngine.UIElements.VisualElement uiElement
    )
    {
        int nextAvailableNode = this.nodes.Count + 1;

        // Now using asset.Name!
        if (asset.Name.Contains("Resistor"))
        {
            addResistor(nextAvailableNode, nextAvailableNode + 1, 1000);
            Debug.Log(
                $"Physics Engine: Registered 1k Resistor between Node {nextAvailableNode} and {nextAvailableNode + 1}"
            );
        }
        else if (asset.Name.Contains("Capacitor"))
        {
            addCapacitor(nextAvailableNode, nextAvailableNode + 1, 0.00001); // 10uF
            Debug.Log("Physics Engine: Registered Capacitor");
        }
        else if (asset.Name.Contains("Voltage"))
        {
            addVoltageSource(nextAvailableNode, 0, 5.0);
            Debug.Log("Physics Engine: Registered 5V Source");
        }
        // ... add your other components here!
    }

    public CircuitResult stepTime(double dt)
    {
        var result = this.solveNonLinear(dt);

        // Update state of transient components
        foreach (var c in this.capacitors)
        {
            double v1 = result.voltages.ContainsKey(c.n1) ? result.voltages[c.n1] : 0;
            double v2 = result.voltages.ContainsKey(c.n2) ? result.voltages[c.n2] : 0;
            c.updateState(v1, v2);
        }

        int numVSources = this.vSources.Count;
        for (int k = 0; k < this.inductors.Count; k++)
        {
            double current = result.sourceCurrents[numVSources + k];
            this.inductors[k].updateState(current);
        }

        return result;
    }

    public CircuitResult solve(double dt = 0)
    {
        return this.solveNonLinear(dt);
    }

    public CircuitResult solveNonLinear(double dt = 0)
    {
        List<int> sortedNodes = this.nodes.ToList();
        sortedNodes.Sort();

        Dictionary<int, int> nodeToIndex = new Dictionary<int, int>();
        for (int i = 0; i < sortedNodes.Count; i++)
        {
            nodeToIndex[sortedNodes[i]] = i;
        }

        int numNodes = sortedNodes.Count;
        int numVSources = this.vSources.Count;
        int numInductors = this.inductors.Count;
        int dim = numNodes + numVSources + numInductors;

        double[] x = new double[dim];
        double[] prevX = new double[dim];

        int iterations = 0;
        int MAX_ITER = 50;
        double TOLERANCE = 1e-3;

        while (iterations < MAX_ITER)
        {
            double[][] A = new double[dim][];
            for (int i = 0; i < dim; i++)
            {
                A[i] = new double[dim];
            }
            double[] z = new double[dim];

            // Stamp Resistors
            foreach (var r in this.resistors)
            {
                double g = 1.0 / r.resistance;
                int i1 = nodeToIndex.ContainsKey(r.n1) ? nodeToIndex[r.n1] : -1;
                int i2 = nodeToIndex.ContainsKey(r.n2) ? nodeToIndex[r.n2] : -1;

                if (i1 >= 0)
                    A[i1][i1] += g;
                if (i2 >= 0)
                    A[i2][i2] += g;
                if (i1 >= 0 && i2 >= 0)
                {
                    A[i1][i2] -= g;
                    A[i2][i1] -= g;
                }
            }

            // Stamp Capacitors
            if (dt > 0)
            {
                foreach (var c in this.capacitors)
                {
                    int iP = nodeToIndex.ContainsKey(c.n1) ? nodeToIndex[c.n1] : -1;
                    int iN = nodeToIndex.ContainsKey(c.n2) ? nodeToIndex[c.n2] : -1;

                    int originalP = c.n1;
                    int originalN = c.n2;
                    c.n1 = iP;
                    c.n2 = iN;

                    c.stamp(A, z, dt);

                    c.n1 = originalP;
                    c.n2 = originalN;
                }
            }

            // Stamp independent Current sources
            foreach (var s in this.iSources)
            {
                int iFrom = nodeToIndex.ContainsKey(s.from) ? nodeToIndex[s.from] : -1;
                int iTo = nodeToIndex.ContainsKey(s.to) ? nodeToIndex[s.to] : -1;
                if (iFrom >= 0)
                    z[iFrom] -= s.current;
                if (iTo >= 0)
                    z[iTo] += s.current;
            }

            // Stamp Voltage sources
            for (int j = 0; j < numVSources; j++)
            {
                var s = this.vSources[j];
                int iP = nodeToIndex.ContainsKey(s.nodePositive) ? nodeToIndex[s.nodePositive] : -1;
                int iN = nodeToIndex.ContainsKey(s.nodeNegative) ? nodeToIndex[s.nodeNegative] : -1;
                int vIdx = numNodes + j;

                int originalP = s.nodePositive;
                int originalN = s.nodeNegative;
                int originalIndex = s.mnaIndex;

                s.nodePositive = iP;
                s.nodeNegative = iN;
                s.mnaIndex = vIdx;

                s.stampMatrix(A, z);

                s.nodePositive = originalP;
                s.nodeNegative = originalN;
                s.mnaIndex = originalIndex;
            }

            // Stamp Inductors
            for (int k = 0; k < numInductors; k++)
            {
                var l = this.inductors[k];
                int iIdx = numNodes + numVSources + k;

                int iP = nodeToIndex.ContainsKey(l.nodePositive) ? nodeToIndex[l.nodePositive] : -1;
                int iN = nodeToIndex.ContainsKey(l.nodeNegative) ? nodeToIndex[l.nodeNegative] : -1;

                if (dt > 0)
                {
                    // Inductor logic fix implementation
                    int originalP = l.nodePositive;
                    int originalN = l.nodeNegative;
                    int originalIndex = l.mnaIndex;

                    l.nodePositive = iP;
                    l.nodeNegative = iN;
                    l.mnaIndex = iIdx;

                    l.stampMatrix(A, z, dt);

                    l.nodePositive = originalP;
                    l.nodeNegative = originalN;
                    l.mnaIndex = originalIndex;
                }
                else
                {
                    if (iP >= 0)
                    {
                        A[iP][iIdx] += 1;
                        A[iIdx][iP] += 1;
                    }
                    if (iN >= 0)
                    {
                        A[iN][iIdx] -= 1;
                        A[iIdx][iN] -= 1;
                    }
                    z[iIdx] += 0;
                }
            }

            // Newton-Raphson: Stamp Non-Linear
            Dictionary<int, double> prevVoltages = new Dictionary<int, double>();
            prevVoltages[0] = 0; // Ground
            for (int i = 0; i < sortedNodes.Count; i++)
            {
                prevVoltages[sortedNodes[i]] = prevX[i];
            }

            foreach (var nlc in this.nonLinearComponents)
            {
                nlc.stamp(A, z, nodeToIndex, prevVoltages);
            }

            x = this.gaussianElimination(A, z);

            // Convergence check fix implementation
            bool converged = true;
            for (int i = 0; i < dim; i++)
            {
                if (Math.Abs(x[i] - prevX[i]) > TOLERANCE)
                {
                    converged = false;
                    break;
                }
            }

            if (converged && iterations > 0)
            {
                break;
            }

            Array.Copy(x, prevX, dim);
            iterations++;
        }

        Dictionary<int, double> voltages = new Dictionary<int, double>();
        voltages[0] = 0;
        for (int i = 0; i < sortedNodes.Count; i++)
        {
            voltages[sortedNodes[i]] = x[i];
        }

        double[] sourceCurrents = new double[numVSources + numInductors];
        Array.Copy(x, numNodes, sourceCurrents, 0, numVSources + numInductors);

        return new CircuitResult { voltages = voltages, sourceCurrents = sourceCurrents };
    }

    private double[] gaussianElimination(double[][] A, double[] b)
    {
        int n = b.Length;
        for (int i = 0; i < n; i++)
        {
            double max = Math.Abs(A[i][i]);
            int maxRow = i;
            for (int k = i + 1; k < n; k++)
            {
                if (Math.Abs(A[k][i]) > max)
                {
                    max = Math.Abs(A[k][i]);
                    maxRow = k;
                }
            }

            // Swap maximum row with current row
            double[] tempA = A[i];
            A[i] = A[maxRow];
            A[maxRow] = tempA;

            double tempB = b[i];
            b[i] = b[maxRow];
            b[maxRow] = tempB;

            // Gaussian elimination safety check
            if (Math.Abs(A[i][i]) < 1e-12)
            {
                continue;
            }

            for (int k = i + 1; k < n; k++)
            {
                double c = -A[k][i] / A[i][i];
                for (int j = i; j < n; j++)
                {
                    if (i == j)
                    {
                        A[k][j] = 0;
                    }
                    else
                    {
                        A[k][j] += c * A[i][j];
                    }
                }
                b[k] += c * b[i];
            }
        }

        double[] x = new double[n];
        for (int i = n - 1; i >= 0; i--)
        {
            if (Math.Abs(A[i][i]) < 1e-12)
            {
                x[i] = 0;
                continue;
            }
            x[i] = b[i] / A[i][i];
            for (int k = i - 1; k >= 0; k--)
            {
                b[k] -= A[k][i] * x[i];
            }
        }
        return x;
    }
}
