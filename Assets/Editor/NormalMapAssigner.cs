#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public class NormalMapAssignerWindow : EditorWindow
{
    private DefaultAsset mainTexturesFolder;
    private DefaultAsset normalMapsFolder;

    [MenuItem("Tools/2D/Folder Normal Map Assigner")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(NormalMapAssignerWindow), false, "Assign Normals");
    }

    private void OnGUI()
    {
        GUILayout.Label("Settings", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        mainTexturesFolder = (DefaultAsset)EditorGUILayout.ObjectField("Main Textures Folder", mainTexturesFolder, typeof(DefaultAsset), false);
        normalMapsFolder = (DefaultAsset)EditorGUILayout.ObjectField("Normal Maps Folder", normalMapsFolder, typeof(DefaultAsset), false);

        EditorGUILayout.Space();

        if (GUILayout.Button("Assign Normals", GUILayout.Height(40)))
        {
            AssignNormalsByFolder();
        }
    }

    private void AssignNormalsByFolder()
    {
        if (mainTexturesFolder == null || normalMapsFolder == null)
        {
            Debug.LogError("Please specify both folders in the window!");
            return;
        }

        string mainFolderPath = AssetDatabase.GetAssetPath(mainTexturesFolder);
        string normalFolderPath = AssetDatabase.GetAssetPath(normalMapsFolder);

        string[] mainFiles = Directory.GetFiles(mainFolderPath);
        int processedCount = 0;

        foreach (string mainFilePath in mainFiles)
        {
            if (mainFilePath.EndsWith(".meta")) continue;

            string fileName = Path.GetFileName(mainFilePath);
            string expectedNormalPath = normalFolderPath + "/" + fileName;

            Texture2D normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(expectedNormalPath);

            if (normalMap != null)
            {
                TextureImporter mainImporter = AssetImporter.GetAtPath(mainFilePath) as TextureImporter;

                if (mainImporter != null)
                {
                    SecondarySpriteTexture[] secondaryTextures = new SecondarySpriteTexture[1];
                    secondaryTextures[0].name = "_NormalMap";
                    secondaryTextures[0].texture = normalMap;

                    mainImporter.secondarySpriteTextures = secondaryTextures;
                    mainImporter.SaveAndReimport();

                    processedCount++;
                }
            }
            else
            {
                Debug.LogWarning("Normal map not found for: " + fileName);
            }
        }

        Debug.Log("Done! Processed normal maps: " + processedCount);
    }
}
#endif
