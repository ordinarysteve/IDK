using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PinData
{
    public string name;
    public string svgId;
    public string pinName;
    public float cx;
    public float cy;
    public float pinWidth = 10f;
    public float pinHeight = 10f;
}

[CreateAssetMenu(fileName = "New Fritzing Component", menuName = "CAD/Fritzing Component")]
public class FritzingComponentAsset : ScriptableObject
{
    public string Name;
    public Texture2D icon;

    // --- ADD THESE TWO LINES ---
    public float realWorldWidth = 100f;
    public float realWorldHeight = 100f;

    public List<PinData> pins = new List<PinData>();
}
