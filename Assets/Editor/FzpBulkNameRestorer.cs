using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEditor;
using UnityEngine;

public class FzpBulkNameRestorer : EditorWindow
{
    [MenuItem("CAD Tools/1. Bulk Rescue Human Names (Nuclear Match)")]
    public static void BulkRestoreNames()
    {
        string searchDirectory = EditorUtility.OpenFolderPanel("Select Master Folder", "", "");
        if (string.IsNullOrEmpty(searchDirectory))
            return;

        string[] fzpFiles = Directory.GetFiles(
            searchDirectory,
            "*.fzp",
            SearchOption.AllDirectories
        );
        string[] fzpzFiles = Directory.GetFiles(
            searchDirectory,
            "*.fzpz",
            SearchOption.AllDirectories
        );

        Dictionary<string, XmlDocument> fzpDocMap = new Dictionary<string, XmlDocument>();

        foreach (string file in fzpFiles)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(file);
                CacheXmlDocument(doc, fzpDocMap);
            }
            catch { }
        }

        foreach (string file in fzpzFiles)
        {
            try
            {
                using (FileStream zipStream = new FileStream(file, FileMode.Open))
                using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (
                            entry.FullName.EndsWith(
                                ".fzp",
                                System.StringComparison.OrdinalIgnoreCase
                            )
                        )
                        {
                            using (Stream entryStream = entry.Open())
                            {
                                XmlDocument doc = new XmlDocument();
                                doc.Load(entryStream);
                                CacheXmlDocument(doc, fzpDocMap);
                            }
                        }
                    }
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

            if (fzpDocMap.TryGetValue(cleanAssetName, out XmlDocument matchingDoc))
            {
                if (RestoreNamesForAsset(asset, matchingDoc))
                    successCount++;
            }
            else
            {
                Debug.LogWarning(
                    $"Still missing match for: [{cleanAssetName}] (Original: {asset.Name})"
                );
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log(
            $"<color=cyan><b>RESTORE COMPLETE:</b></color> Rescued human names for {successCount} components!"
        );
    }

    // --- THE NUCLEAR SANITIZER ---
    // Destroys spaces, dashes, brackets, everything.
    // "Arduino Uno (Rev3)" becomes "arduinounorev3"
    private static string SanitizeText(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "";
        return Regex.Replace(input, "[^a-zA-Z0-9]", "").ToLowerInvariant();
    }

    private static void CacheXmlDocument(XmlDocument doc, Dictionary<string, XmlDocument> map)
    {
        XmlNode titleNode = doc.SelectSingleNode("//title");
        if (titleNode != null && !string.IsNullOrEmpty(titleNode.InnerText))
        {
            string cleanTitle = SanitizeText(titleNode.InnerText);
            if (!map.ContainsKey(cleanTitle))
                map.Add(cleanTitle, doc);
        }
    }

    private static bool RestoreNamesForAsset(FritzingComponentAsset asset, XmlDocument doc)
    {
        try
        {
            XmlNodeList connectors = doc.GetElementsByTagName("connector");
            bool modified = false;

            foreach (XmlNode node in connectors)
            {
                if (node.Attributes["id"] != null && node.Attributes["name"] != null)
                {
                    string id = node.Attributes["id"].Value;
                    string name = node.Attributes["name"].Value;

                    PinData existingPin = asset.pins.FirstOrDefault(p =>
                        p.pinName == id || p.svgId == id
                    );
                    if (existingPin != null)
                    {
                        existingPin.name = name;
                        existingPin.svgId = id;
                        modified = true;
                    }
                    else
                    {
                        // Fallback: Add the pin if it doesn't exist yet so it gets the name!
                        asset.pins.Add(
                            new PinData
                            {
                                name = name,
                                svgId = id,
                                pinName = id,
                            }
                        );
                        modified = true;
                    }
                }
            }

            if (modified)
            {
                EditorUtility.SetDirty(asset);
                return true;
            }
        }
        catch { }
        return false;
    }
}
