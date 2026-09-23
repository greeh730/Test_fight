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
        // Создаем и показываем кастомное окно
        EditorWindow.GetWindow(typeof(NormalMapAssignerWindow), false, "Assign Normals");
    }

    private void OnGUI()
    {
        GUILayout.Label("Настройка папок", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Поля для перетаскивания папок
        mainTexturesFolder = (DefaultAsset)EditorGUILayout.ObjectField("Папка с текстурами", mainTexturesFolder, typeof(DefaultAsset), false);
        normalMapsFolder = (DefaultAsset)EditorGUILayout.ObjectField("Папка с нормалями", normalMapsFolder, typeof(DefaultAsset), false);

        EditorGUILayout.Space();

        if (GUILayout.Button("Связать текстуры и нормали", GUILayout.Height(40)))
        {
            AssignNormalsByFolder();
        }
    }

    private void AssignNormalsByFolder()
    {
        if (mainTexturesFolder == null || normalMapsFolder == null)
        {
            Debug.LogError("Пожалуйста, укажите обе папки в окне скрипта!");
            return;
        }

        string mainFolderPath = AssetDatabase.GetAssetPath(mainTexturesFolder);
        string normalFolderPath = AssetDatabase.GetAssetPath(normalMapsFolder);

        // Получаем все файлы в папке с текстурами (игнорируем мета-файлы)
        string[] mainFiles = Directory.GetFiles(mainFolderPath);
        int processedCount = 0;

        foreach (string mainFilePath in mainFiles)
        {
            if (mainFilePath.EndsWith(".meta")) continue;

            // Получаем точное имя файла с расширением (например, "Frame_001.png")
            string fileName = Path.GetFileName(mainFilePath);

            // Формируем путь, где должен лежать такой же файл в папке с нормалями
            string expectedNormalPath = normalFolderPath + "/" + fileName;

            Texture2D normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(expectedNormalPath);

            if (normalMap != null)
            {
                TextureImporter mainImporter = AssetImporter.GetAtPath(mainFilePath) as TextureImporter;

                if (mainImporter != null)
                {
                    SecondarySpriteTexture[] secondaryTextures = new SecondarySpriteTexture[1];
                    // Строгое системное имя для 2D URP Lit шейдера
                    secondaryTextures[0].name = "_NormalMap";
                    secondaryTextures[0].texture = normalMap;

                    mainImporter.secondarySpriteTextures = secondaryTextures;
                    mainImporter.SaveAndReimport();

                    processedCount++;
                }
            }
            else
            {
                Debug.LogWarning("Для текстуры " + fileName + " не найдена нормаль с таким же именем в папке нормалей!");
            }
        }

        Debug.Log("Готово! Связано нормалей: " + processedCount);
    }
}