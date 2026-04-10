using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEditor;
using UnityEngine;

public class FritzingGodScript : EditorWindow
{
    private class FritzingData
    {
        public string CleanTitle;
        public float RealWorldWidth = 100f;
        public float RealWorldHeight = 100f;
        public Dictionary<string, string> PinNames = new Dictionary<string, string>();
        public Dictionary<string, Vector2> PinCoords = new Dictionary<string, Vector2>();
    }

    [MenuItem("CAD Tools/THE GOD SCRIPT (Extract Names, Pins, & Size)")]
    public static void RunMasterExtraction()
    {
        string searchDir = EditorUtility.OpenFolderPanel("Select Master Fritzing Folder", "", "");
        if (string.IsNullOrEmpty(searchDir))
            return;

        Dictionary<string, FritzingData> masterDataMap = new Dictionary<string, FritzingData>();
        string[] fzpzFiles = Directory.GetFiles(searchDir, "*.fzpz", SearchOption.AllDirectories);
        string[] fzpFiles = Directory.GetFiles(searchDir, "*.fzp", SearchOption.AllDirectories);
        string[] allSvgFiles = Directory.GetFiles(searchDir, "*.svg", SearchOption.AllDirectories);

        foreach (string file in fzpzFiles)
        {
            try
            {
                using (FileStream zipStream = new FileStream(file, FileMode.Open))
                using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
                {
                    ZipArchiveEntry fzpEntry = archive.Entries.FirstOrDefault(e =>
                        e.FullName.EndsWith(".fzp", System.StringComparison.OrdinalIgnoreCase)
                    );
                    if (fzpEntry == null)
                        continue;

                    using (Stream stream = fzpEntry.Open())
                    {
                        XmlDocument doc = new XmlDocument();
                        doc.Load(stream);
                        FritzingData data = ParseXmlData(doc);
                        if (data != null && !masterDataMap.ContainsKey(data.CleanTitle))
                        {
                            string svgFileName = GetBreadboardSvgName(doc);
                            ZipArchiveEntry svgEntry = archive.Entries.FirstOrDefault(e =>
                                e.FullName.EndsWith(
                                    svgFileName,
                                    System.StringComparison.OrdinalIgnoreCase
                                )
                            );
                            if (svgEntry != null)
                            {
                                using (Stream svgStream = svgEntry.Open())
                                using (StreamReader reader = new StreamReader(svgStream))
                                {
                                    ExtractPinsFromSvg(reader.ReadToEnd(), data);
                                }
                            }
                            masterDataMap.Add(data.CleanTitle, data);
                        }
                    }
                }
            }
            catch { }
        }

        foreach (string file in fzpFiles)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(file);
                FritzingData data = ParseXmlData(doc);
                if (data != null && !masterDataMap.ContainsKey(data.CleanTitle))
                {
                    string svgFileName = GetBreadboardSvgName(doc);
                    string matchingSvgPath = allSvgFiles.FirstOrDefault(s =>
                        s.EndsWith(svgFileName, System.StringComparison.OrdinalIgnoreCase)
                    );
                    if (!string.IsNullOrEmpty(matchingSvgPath))
                        ExtractPinsFromSvg(File.ReadAllText(matchingSvgPath), data);
                    masterDataMap.Add(data.CleanTitle, data);
                }
            }
            catch { }
        }

        string[] guids = AssetDatabase.FindAssets("t:FritzingComponentAsset");
        int successCount = 0;

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            FritzingComponentAsset asset = AssetDatabase.LoadAssetAtPath<FritzingComponentAsset>(
                assetPath
            );
            if (asset == null || string.IsNullOrEmpty(asset.Name))
                continue;

            string cleanAssetName = SanitizeText(asset.Name);
            if (masterDataMap.TryGetValue(cleanAssetName, out FritzingData masterData))
            {
                // --- INJECT PHYSICAL SIZE ---
                asset.realWorldWidth = masterData.RealWorldWidth;
                asset.realWorldHeight = masterData.RealWorldHeight;

                asset.pins.Clear();
                foreach (var kvp in masterData.PinNames)
                {
                    string baseId = kvp.Key;
                    Vector2 coords = Vector2.zero;
                    masterData.PinCoords.TryGetValue(baseId, out coords);
                    asset.pins.Add(
                        new PinData
                        {
                            name = kvp.Value,
                            svgId = baseId + "pin",
                            pinName = baseId + "pin",
                            cx = coords.x,
                            cy = coords.y,
                        }
                    );
                }
                EditorUtility.SetDirty(asset);
                successCount++;
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log(
            $"<color=green><b>GOD SCRIPT COMPLETE:</b></color> Injected True Sizes for {successCount} components!"
        );
    }

    // --- NEW: CONVERT REAL WORLD MEASUREMENTS TO UI PIXELS ---
    private static float ParsePhysicalSize(string val)
    {
        if (string.IsNullOrEmpty(val))
            return 100f;
        float multiplier = 1f;
        string lowerVal = val.ToLowerInvariant();

        // We set a base scale: 1 real-world inch = 100 UI pixels
        if (lowerVal.EndsWith("in"))
            multiplier = 100f;
        else if (lowerVal.EndsWith("mm"))
            multiplier = 100f / 25.4f; // Convert mm to inches
        else if (lowerVal.EndsWith("cm"))
            multiplier = 100f / 2.54f;

        float num = ParseFloat(Regex.Replace(val, @"[^\d.]", ""));
        return num * multiplier;
    }

    private static FritzingData ParseXmlData(XmlDocument doc)
    {
        XmlNode titleNode = doc.SelectSingleNode("//title");
        if (titleNode == null || string.IsNullOrEmpty(titleNode.InnerText))
            return null;
        FritzingData data = new FritzingData { CleanTitle = SanitizeText(titleNode.InnerText) };
        foreach (XmlNode node in doc.GetElementsByTagName("connector"))
        {
            if (node.Attributes["id"] != null && node.Attributes["name"] != null)
                data.PinNames[node.Attributes["id"].Value] = node.Attributes["name"].Value;
        }
        return data;
    }

    private static string GetBreadboardSvgName(XmlDocument doc)
    {
        XmlNode layerNode = doc.SelectSingleNode("//breadboardView/layers[@image]");
        if (layerNode != null)
            return Path.GetFileName(layerNode.Attributes["image"].Value);
        return "MISSING_SVG";
    }

    private static void ExtractPinsFromSvg(string svgText, FritzingData data)
    {
        svgText = Regex.Replace(svgText, @"xmlns(:\w+)?=""[^""]*""", "");
        svgText = Regex.Replace(svgText, @"<!DOCTYPE[^>]*>", "");

        try
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(svgText);
            float svgWidth = 1;
            float svgHeight = 1;

            XmlNode svgNode = doc.SelectSingleNode("//svg");
            if (svgNode != null)
            {
                // EXCTRACT TRUE PHYSICAL SIZE FOR UNITY!
                if (svgNode.Attributes["width"] != null && svgNode.Attributes["height"] != null)
                {
                    data.RealWorldWidth = ParsePhysicalSize(svgNode.Attributes["width"].Value);
                    data.RealWorldHeight = ParsePhysicalSize(svgNode.Attributes["height"].Value);
                }

                // Extract internal math size for Percentage calculation
                if (svgNode.Attributes["viewBox"] != null)
                {
                    string[] vb = svgNode
                        .Attributes["viewBox"]
                        .Value.Split(
                            new char[] { ' ', ',' },
                            System.StringSplitOptions.RemoveEmptyEntries
                        );
                    if (vb.Length >= 4)
                    {
                        svgWidth = ParseFloat(vb[2]);
                        svgHeight = ParseFloat(vb[3]);
                    }
                }
                else if (svgNode.Attributes["width"] != null)
                {
                    svgWidth = ParseFloat(
                        Regex.Replace(svgNode.Attributes["width"].Value, @"[^\d.]", "")
                    );
                    svgHeight = ParseFloat(
                        Regex.Replace(svgNode.Attributes["height"].Value, @"[^\d.]", "")
                    );
                }
            }

            XmlNodeList nodes = doc.SelectNodes("//*[@id]");
            foreach (XmlNode node in nodes)
            {
                string id = node.Attributes["id"].Value;
                string normalizedId = id;
                if (normalizedId.EndsWith("pin", System.StringComparison.OrdinalIgnoreCase))
                    normalizedId = normalizedId.Substring(0, normalizedId.Length - 3);
                else if (
                    normalizedId.EndsWith("terminal", System.StringComparison.OrdinalIgnoreCase)
                )
                    normalizedId = normalizedId.Substring(0, normalizedId.Length - 8);

                float x = 0,
                    y = 0;
                bool found = false;
                if (node.Attributes["cx"] != null)
                {
                    x = ParseFloat(node.Attributes["cx"].Value);
                    y = ParseFloat(node.Attributes["cy"].Value);
                    found = true;
                }
                else if (node.Attributes["x"] != null)
                {
                    x = ParseFloat(node.Attributes["x"].Value);
                    y = ParseFloat(node.Attributes["y"].Value);
                    if (node.Attributes["width"] != null)
                    {
                        x += ParseFloat(node.Attributes["width"].Value) / 2f;
                        y += ParseFloat(node.Attributes["height"].Value) / 2f;
                    }
                    found = true;
                }

                if (found)
                {
                    XmlNode curr = node;
                    while (curr != null && curr.Name != "svg")
                    {
                        if (curr.Attributes != null && curr.Attributes["transform"] != null)
                        {
                            var m = Regex.Match(
                                curr.Attributes["transform"].Value,
                                @"translate\(([-\d.]+)[,\s]+([-\d.]+)\)"
                            );
                            if (m.Success)
                            {
                                x += ParseFloat(m.Groups[1].Value);
                                y += ParseFloat(m.Groups[2].Value);
                            }
                        }
                        curr = curr.ParentNode;
                    }
                    if (svgWidth > 0 && svgHeight > 0)
                        data.PinCoords[normalizedId] = new Vector2(x / svgWidth, y / svgHeight);
                }
            }
        }
        catch { }
    }

    private static float ParseFloat(string val)
    {
        if (float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out float result))
            return result;
        return 0f;
    }

    private static string SanitizeText(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "";
        return Regex.Replace(input, "[^a-zA-Z0-9]", "").ToLowerInvariant();
    }
}
