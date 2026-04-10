using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

[System.Serializable]
public class FritzingImporterWindow : EditorWindow
{
    private string folderPath = "";

    [MenuItem("Window/Fritzing Importer")]
    public static void ShowWindow()
    {
        GetWindow<FritzingImporterWindow>("Fritzing Importer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Fritzing Importer Settings", EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();
        folderPath = EditorGUILayout.TextField("Folder Path", folderPath);
        if (GUILayout.Button("Browse", GUILayout.Width(75)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel(
                "Select Fritzing Folder",
                folderPath,
                ""
            );
            if (!string.IsNullOrEmpty(selectedPath))
            {
                folderPath = selectedPath;
            }
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Import Fritzing Components"))
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                EditorUtility.DisplayDialog("Error", "Please provide a valid folder path.", "OK");
                return;
            }

            ImportComponents(folderPath);
        }
    }

    private void ImportComponents(string targetFolder)
    {
        EnsureDirectoriesExist();

        string[] fzpzFiles = Directory.GetFiles(
            targetFolder,
            "*.fzpz",
            SearchOption.AllDirectories
        );
        string[] fzpFiles = Directory.GetFiles(targetFolder, "*.fzp", SearchOption.AllDirectories);

        foreach (var file in fzpzFiles)
        {
            ProcessFzpz(file);
        }

        foreach (var file in fzpFiles)
        {
            ProcessFzp(file, targetFolder);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Fritzing import completed successfully.");
    }

    private void EnsureDirectoriesExist()
    {
        if (!AssetDatabase.IsValidFolder("Assets/ExtractedComponents"))
            AssetDatabase.CreateFolder("Assets", "ExtractedComponents");

        if (!AssetDatabase.IsValidFolder("Assets/ExtractedComponents/Sprites"))
            AssetDatabase.CreateFolder("Assets/ExtractedComponents", "Sprites");

        if (!AssetDatabase.IsValidFolder("Assets/ExtractedComponents/Data"))
            AssetDatabase.CreateFolder("Assets/ExtractedComponents", "Data");
    }

    private void ProcessFzpz(string filePath)
    {
        try
        {
            using (ZipArchive archive = ZipFile.OpenRead(filePath))
            {
                ZipArchiveEntry fzpEntry = archive.Entries.FirstOrDefault(e =>
                    e.FullName.EndsWith(".fzp", System.StringComparison.OrdinalIgnoreCase)
                );
                if (fzpEntry == null)
                    return;

                using (Stream stream = fzpEntry.Open())
                {
                    XDocument doc = XDocument.Load(stream);
                    string componentName =
                        doc.Root?.Element("title")?.Value
                        ?? Path.GetFileNameWithoutExtension(filePath);
                    string svgFileName = GetBreadboardSvgFilename(doc);

                    if (string.IsNullOrEmpty(svgFileName))
                        return;

                    string cleanSvgFileName = svgFileName.Split('/').Last();
                    ZipArchiveEntry svgEntry = archive.Entries.FirstOrDefault(e =>
                        e.FullName.EndsWith(
                            cleanSvgFileName,
                            System.StringComparison.OrdinalIgnoreCase
                        )
                    );

                    if (svgEntry != null)
                    {
                        string relativeSpritePath =
                            $"Assets/ExtractedComponents/Sprites/{componentName}.svg";
                        // System.IO classes require full system paths, not Unity's relative "Assets/..." paths
                        string absoluteSpritePath = Path.Combine(
                            Application.dataPath,
                            relativeSpritePath.Substring("Assets/".Length)
                        );

                        // Ensure the destination directory exists on the file system
                        Directory.CreateDirectory(Path.GetDirectoryName(absoluteSpritePath));

                        svgEntry.ExtractToFile(absoluteSpritePath, true);
                        AssetDatabase.ImportAsset(relativeSpritePath);

                        Texture2D sprite = AssetDatabase.LoadAssetAtPath<Texture2D>(
                            relativeSpritePath
                        );
                        CreateComponentAsset(componentName, sprite, doc);
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error processing .fzpz file '{filePath}': {ex.Message}");
        }
    }

    private void ProcessFzp(string filePath, string rootFolderPath)
    {
        try
        {
            XDocument doc = XDocument.Load(filePath);
            string componentName =
                doc.Root?.Element("title")?.Value ?? Path.GetFileNameWithoutExtension(filePath);
            string svgFileNameRaw = GetBreadboardSvgFilename(doc);

            if (string.IsNullOrEmpty(svgFileNameRaw))
                return;

            string svgFileName = svgFileNameRaw.Split('/').Last();
            string[] foundSvgFiles = Directory.GetFiles(
                rootFolderPath,
                svgFileName,
                SearchOption.AllDirectories
            );

            if (foundSvgFiles.Length > 0)
            {
                string sourceSvg = foundSvgFiles[0];
                string relativeSpritePath =
                    $"Assets/ExtractedComponents/Sprites/{componentName}.svg";
                // System.IO classes require full system paths, not Unity's relative "Assets/..." paths
                string absoluteSpritePath = Path.Combine(
                    Application.dataPath,
                    relativeSpritePath.Substring("Assets/".Length)
                );

                // Ensure the destination directory exists on the file system
                Directory.CreateDirectory(Path.GetDirectoryName(absoluteSpritePath));

                File.Copy(sourceSvg, absoluteSpritePath, true);
                AssetDatabase.ImportAsset(relativeSpritePath);

                Texture2D sprite = AssetDatabase.LoadAssetAtPath<Texture2D>(relativeSpritePath);
                CreateComponentAsset(componentName, sprite, doc);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error processing .fzp file '{filePath}': {ex.Message}");
        }
    }

    private string GetBreadboardSvgFilename(XDocument doc)
    {
        return doc
            .Root?.Element("views")
            ?.Element("breadboardView")
            ?.Element("layers")
            ?.Attribute("image")
            ?.Value;
    }

    private void CreateComponentAsset(string name, Texture2D icon, XDocument doc)
    {
        FritzingComponentAsset asset = ScriptableObject.CreateInstance<FritzingComponentAsset>();
        asset.Name = name;
        asset.icon = icon;

        var connectors = doc.Root?.Element("connectors")?.Elements("connector");
        if (connectors != null)
        {
            foreach (var connector in connectors)
            {
                string pinName = connector.Attribute("name")?.Value;
                string svgId = connector
                    .Element("views")
                    ?.Element("breadboardView")
                    ?.Element("p")
                    ?.Attribute("svgId")
                    ?.Value;

                if (!string.IsNullOrEmpty(pinName) && !string.IsNullOrEmpty(svgId))
                {
                    asset.pins.Add(new PinData { name = pinName, svgId = svgId });
                }
            }
        }

        string assetPath = $"Assets/ExtractedComponents/Data/{name}.asset";
        assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
        AssetDatabase.CreateAsset(asset, assetPath);
    }
}
