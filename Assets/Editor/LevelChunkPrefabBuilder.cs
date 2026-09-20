using System.IO;
using UnityEditor;
using UnityEngine;
using LevelGeneration;
using Combat.Common;

namespace LevelGeneration.Editor
{
    public static class LevelChunkPrefabBuilder
    {
        private const string Folder = "Assets/Prefabs/LevelChunks";

        [MenuItem("Tools/Level Generation/Build All Chunk Prefabs")]
        public static string BuildAllPrefabs()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "LevelChunks");
            }

            // Загружаем точный спрайт 1x1 метр (256x256 с PPU 256)
            Sprite squareSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/Square.png");
            if (squareSprite == null)
            {
                var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
                foreach (var s in sprites)
                {
                    if (s.name == "Square" && Mathf.Approximately(s.bounds.size.x, 1f))
                    {
                        squareSprite = s;
                        break;
                    }
                }
            }
            if (squareSprite == null && CombatSprites.WhiteBox != null)
            {
                squareSprite = CombatSprites.WhiteBox;
            }

            Material spriteMat = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");

            // Цветовая палитра уровня
            Color groundBaseColor = new Color(0.12f, 0.16f, 0.22f, 1f);
            Color groundGlowColor = new Color(0.20f, 0.78f, 0.98f, 1f);
            Color platformBodyColor = new Color(0.18f, 0.24f, 0.32f, 1f);
            Color platformGlowColor = new Color(1.0f, 0.68f, 0.22f, 1f);
            Color wallColor = new Color(0.10f, 0.13f, 0.18f, 1f);
            Color decorDark = new Color(0.22f, 0.18f, 0.34f, 1f);
            Color decorBright = new Color(0.85f, 0.75f, 0.30f, 1f);
            Color victoryCrystalColor = new Color(0.30f, 0.90f, 1.0f, 0.95f);

            // =========================================================================
            // 1. CHUNK_START_INTRO (Фиксированный старт: 18 метров)
            // =========================================================================
            {
                var root = new GameObject("Chunk_Start_Intro");
                SetupSockets(root, new Vector3(0f, 0f, 0f), new Vector3(18f, 0f, 0f), new Vector3(4.0f, 0.55f, 0f));

                // Полная толстая земля (глубина 3 метра вниз)
                CreatePlatform(root.transform, "Ground_Base", new Vector3(9f, -1.5f, 0f), new Vector2(18f, 3.0f), groundBaseColor, squareSprite, spriteMat, true, 4);
                // Неоновый край пола сверху
                CreatePlatform(root.transform, "Ground_Glow_Top", new Vector3(9f, -0.06f, 0f), new Vector2(18f, 0.12f), groundGlowColor, squareSprite, spriteMat, false, 6);

                // Левая граница уровня (стена 10 метров)
                CreatePlatform(root.transform, "Wall_Left", new Vector3(-0.5f, 4.5f, 0f), new Vector2(1.0f, 10.0f), wallColor, squareSprite, spriteMat, true, 4);
                CreatePlatform(root.transform, "Wall_Left_Glow", new Vector3(0.02f, 4.5f, 0f), new Vector2(0.06f, 10.0f), groundGlowColor, squareSprite, spriteMat, false, 6);

                // Стартовая арка / ворота
                CreatePlatform(root.transform, "Gate_Pillar_L", new Vector3(2.5f, 2.0f, 0f), new Vector2(0.35f, 4.0f), wallColor, squareSprite, spriteMat, false, 5);
                CreatePlatform(root.transform, "Gate_Pillar_R", new Vector3(6.5f, 2.0f, 0f), new Vector2(0.35f, 4.0f), wallColor, squareSprite, spriteMat, false, 5);
                CreatePlatform(root.transform, "Gate_Lintel", new Vector3(4.5f, 4.1f, 0f), new Vector2(4.4f, 0.4f), platformBodyColor, squareSprite, spriteMat, false, 5);
                CreatePlatform(root.transform, "Gate_Sigil", new Vector3(4.5f, 3.5f, 0f), new Vector2(0.8f, 0.8f), decorBright, squareSprite, spriteMat, false, 7);

                SavePrefab(root, $"{Folder}/Chunk_Start_Intro.prefab");
            }

            // =========================================================================
            // 2. CHUNK_VARIANT_A_COMBATARENA (Вариант А: Боевая арена с подвесными платформами, 24 метра)
            // =========================================================================
            {
                var root = new GameObject("Chunk_Variant_A_CombatArena");
                SetupSockets(root, new Vector3(0f, 0f, 0f), new Vector3(24f, 0f, 0f));

                CreatePlatform(root.transform, "Ground_Base", new Vector3(12f, -1.5f, 0f), new Vector2(24f, 3.0f), groundBaseColor, squareSprite, spriteMat, true, 4);
                CreatePlatform(root.transform, "Ground_Glow_Top", new Vector3(12f, -0.06f, 0f), new Vector2(24f, 0.12f), groundGlowColor, squareSprite, spriteMat, false, 6);

                // Левая подвесная платформа (высота верха Y=2.4)
                CreatePlatform(root.transform, "Platform_Left", new Vector3(6.5f, 2.15f, 0f), new Vector2(6.0f, 0.5f), platformBodyColor, squareSprite, spriteMat, true, 5);
                CreatePlatform(root.transform, "Platform_Left_Glow", new Vector3(6.5f, 2.36f, 0f), new Vector2(6.0f, 0.08f), platformGlowColor, squareSprite, spriteMat, false, 6);

                // Правая подвесная платформа (высота верха Y=2.4)
                CreatePlatform(root.transform, "Platform_Right", new Vector3(17.5f, 2.15f, 0f), new Vector2(6.0f, 0.5f), platformBodyColor, squareSprite, spriteMat, true, 5);
                CreatePlatform(root.transform, "Platform_Right_Glow", new Vector3(17.5f, 2.36f, 0f), new Vector2(6.0f, 0.08f), platformGlowColor, squareSprite, spriteMat, false, 6);

                // Верхняя центральная снайперская балка (высота верха Y=4.2)
                CreatePlatform(root.transform, "Platform_Top", new Vector3(12.0f, 3.95f, 0f), new Vector2(4.5f, 0.5f), platformBodyColor, squareSprite, spriteMat, true, 5);
                CreatePlatform(root.transform, "Platform_Top_Glow", new Vector3(12.0f, 4.16f, 0f), new Vector2(4.5f, 0.08f), groundGlowColor, squareSprite, spriteMat, false, 6);

                // Центральный боевой монумент
                CreatePlatform(root.transform, "Shrine_Base", new Vector3(12.0f, 0.85f, 0f), new Vector2(1.6f, 1.7f), decorDark, squareSprite, spriteMat, false, 5);
                CreatePlatform(root.transform, "Shrine_Core", new Vector3(12.0f, 2.0f, 0f), new Vector2(0.8f, 0.8f), new Color(0.95f, 0.25f, 0.55f, 0.95f), squareSprite, spriteMat, false, 7);

                SavePrefab(root, $"{Folder}/Chunk_Variant_A_CombatArena.prefab");
            }

            // =========================================================================
            // 3. CHUNK_VARIANT_B_TWOTIERELEVATION (Вариант B: Двухуровневая терраса, 26 метров)
            // =========================================================================
            {
                var root = new GameObject("Chunk_Variant_B_TwoTierElevation");
                SetupSockets(root, new Vector3(0f, 0f, 0f), new Vector3(26f, 0f, 0f));

                // Секция 1: Входной пол (X: 0..6, верх Y=0)
                CreatePlatform(root.transform, "Ground_Entry", new Vector3(3.0f, -1.5f, 0f), new Vector2(6.0f, 3.0f), groundBaseColor, squareSprite, spriteMat, true, 4);
                CreatePlatform(root.transform, "Ground_Entry_Glow", new Vector3(3.0f, -0.06f, 0f), new Vector2(6.0f, 0.12f), groundGlowColor, squareSprite, spriteMat, false, 6);

                // Секция 2: Подъем 1 (X: 6..10, верх Y=1.2)
                CreatePlatform(root.transform, "Step_Up_1", new Vector3(8.0f, -0.9f, 0f), new Vector2(4.0f, 4.2f), groundBaseColor, squareSprite, spriteMat, true, 4);
                CreatePlatform(root.transform, "Step_Up_1_Glow", new Vector3(8.0f, 1.14f, 0f), new Vector2(4.0f, 0.12f), groundGlowColor, squareSprite, spriteMat, false, 6);

                // Секция 3: Центральная возвышенность / Терраса (X: 10..16, верх Y=2.4)
                CreatePlatform(root.transform, "Terrace_Center", new Vector3(13.0f, -0.3f, 0f), new Vector2(6.0f, 5.4f), groundBaseColor, squareSprite, spriteMat, true, 4);
                CreatePlatform(root.transform, "Terrace_Center_Glow", new Vector3(13.0f, 2.34f, 0f), new Vector2(6.0f, 0.12f), platformGlowColor, squareSprite, spriteMat, false, 6);

                // Секция 4: Спуск 1 (X: 16..20, верх Y=1.2)
                CreatePlatform(root.transform, "Step_Down_1", new Vector3(18.0f, -0.9f, 0f), new Vector2(4.0f, 4.2f), groundBaseColor, squareSprite, spriteMat, true, 4);
                CreatePlatform(root.transform, "Step_Down_1_Glow", new Vector3(18.0f, 1.14f, 0f), new Vector2(4.0f, 0.12f), groundGlowColor, squareSprite, spriteMat, false, 6);

                // Секция 5: Выходной пол (X: 20..26, верх Y=0)
                CreatePlatform(root.transform, "Ground_Exit", new Vector3(23.0f, -1.5f, 0f), new Vector2(6.0f, 3.0f), groundBaseColor, squareSprite, spriteMat, true, 4);
                CreatePlatform(root.transform, "Ground_Exit_Glow", new Vector3(23.0f, -0.06f, 0f), new Vector2(6.0f, 0.12f), groundGlowColor, squareSprite, spriteMat, false, 6);

                SavePrefab(root, $"{Folder}/Chunk_Variant_B_TwoTierElevation.prefab");
            }

            // =========================================================================
            // 4. CHUNK_VARIANT_C_SPLITPATH (Вариант C: Нижняя полоса + верхняя эстакада, 28 метров)
            // =========================================================================
            {
                var root = new GameObject("Chunk_Variant_C_SplitPath");
                SetupSockets(root, new Vector3(0f, 0f, 0f), new Vector3(28f, 0f, 0f));

                CreatePlatform(root.transform, "Ground_Base", new Vector3(14.0f, -1.5f, 0f), new Vector2(28.0f, 3.0f), groundBaseColor, squareSprite, spriteMat, true, 4);
                CreatePlatform(root.transform, "Ground_Glow_Top", new Vector3(14.0f, -0.06f, 0f), new Vector2(28.0f, 0.12f), groundGlowColor, squareSprite, spriteMat, false, 6);

                // Верхняя воздушная магистраль
                CreatePlatform(root.transform, "Skyway_1", new Vector3(6.5f, 2.55f, 0f), new Vector2(5.5f, 0.5f), platformBodyColor, squareSprite, spriteMat, true, 5);
                CreatePlatform(root.transform, "Skyway_1_Glow", new Vector3(6.5f, 2.76f, 0f), new Vector2(5.5f, 0.08f), platformGlowColor, squareSprite, spriteMat, false, 6);

                CreatePlatform(root.transform, "Skyway_2", new Vector3(14.0f, 3.55f, 0f), new Vector2(7.0f, 0.5f), platformBodyColor, squareSprite, spriteMat, true, 5);
                CreatePlatform(root.transform, "Skyway_2_Glow", new Vector3(14.0f, 3.76f, 0f), new Vector2(7.0f, 0.08f), platformGlowColor, squareSprite, spriteMat, false, 6);

                CreatePlatform(root.transform, "Skyway_3", new Vector3(21.5f, 2.55f, 0f), new Vector2(5.5f, 0.5f), platformBodyColor, squareSprite, spriteMat, true, 5);
                CreatePlatform(root.transform, "Skyway_3_Glow", new Vector3(21.5f, 2.76f, 0f), new Vector2(5.5f, 0.08f), platformGlowColor, squareSprite, spriteMat, false, 6);

                // Подвесные тросы
                CreatePlatform(root.transform, "Cable_1", new Vector3(6.5f, 5.2f, 0f), new Vector2(0.12f, 4.8f), new Color(0.4f, 0.5f, 0.6f, 0.75f), squareSprite, spriteMat, false, 5);
                CreatePlatform(root.transform, "Cable_2", new Vector3(21.5f, 5.2f, 0f), new Vector2(0.12f, 4.8f), new Color(0.4f, 0.5f, 0.6f, 0.75f), squareSprite, spriteMat, false, 5);

                SavePrefab(root, $"{Folder}/Chunk_Variant_C_SplitPath.prefab");
            }

            // =========================================================================
            // 5. CHUNK_END_OUTRO (Фиксированный финал: 18 метров)
            // =========================================================================
            {
                var root = new GameObject("Chunk_End_Outro");
                SetupSockets(root, new Vector3(0f, 0f, 0f), new Vector3(18f, 0f, 0f));

                CreatePlatform(root.transform, "Ground_Base", new Vector3(9f, -1.5f, 0f), new Vector2(18f, 3.0f), groundBaseColor, squareSprite, spriteMat, true, 4);
                CreatePlatform(root.transform, "Ground_Glow_Top", new Vector3(9f, -0.06f, 0f), new Vector2(18f, 0.12f), groundGlowColor, squareSprite, spriteMat, false, 6);

                // Правая граница уровня (стена 10 метров)
                CreatePlatform(root.transform, "Wall_Right", new Vector3(18.5f, 4.5f, 0f), new Vector2(1.0f, 10.0f), wallColor, squareSprite, spriteMat, true, 4);
                CreatePlatform(root.transform, "Wall_Right_Glow", new Vector3(17.98f, 4.5f, 0f), new Vector2(0.06f, 10.0f), groundGlowColor, squareSprite, spriteMat, false, 6);

                // Финальный портал и постамент победы
                CreatePlatform(root.transform, "Victory_Pedestal_Base", new Vector3(13.0f, 0.35f, 0f), new Vector2(5.0f, 0.7f), decorDark, squareSprite, spriteMat, true, 5);
                CreatePlatform(root.transform, "Victory_Pedestal_Tier2", new Vector3(13.0f, 0.85f, 0f), new Vector2(3.2f, 0.5f), platformBodyColor, squareSprite, spriteMat, true, 5);

                CreatePlatform(root.transform, "Portal_Col_L", new Vector3(10.8f, 2.6f, 0f), new Vector2(0.4f, 4.6f), wallColor, squareSprite, spriteMat, false, 5);
                CreatePlatform(root.transform, "Portal_Col_R", new Vector3(15.2f, 2.6f, 0f), new Vector2(0.4f, 4.6f), wallColor, squareSprite, spriteMat, false, 5);
                CreatePlatform(root.transform, "Portal_Lintel", new Vector3(13.0f, 5.0f, 0f), new Vector2(5.2f, 0.5f), platformBodyColor, squareSprite, spriteMat, false, 5);

                CreatePlatform(root.transform, "Victory_Crystal", new Vector3(13.0f, 2.0f, 0f), new Vector2(1.2f, 1.6f), victoryCrystalColor, squareSprite, spriteMat, false, 7);

                SavePrefab(root, $"{Folder}/Chunk_End_Outro.prefab");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return "All 5 level chunk prefabs built successfully with full 1:1 visuals and solid depth!";
        }

        private static GameObject CreatePlatform(Transform parent, string name, Vector3 localPos, Vector2 size, Color col, Sprite sprite, Material mat, bool addCollider = true, int sortingOrder = 4)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = col;
            sr.sortingOrder = sortingOrder;
            if (mat != null) sr.sharedMaterial = mat;

            if (addCollider)
            {
                var col2d = go.AddComponent<BoxCollider2D>();
                col2d.size = Vector2.one;
            }

            return go;
        }

        private static void SetupSockets(GameObject root, Vector3 entryPos, Vector3 exitPos, Vector3? spawnPos = null)
        {
            var chunkComp = root.AddComponent<LevelChunk>();

            var entryObj = new GameObject("Socket_Entry");
            entryObj.transform.SetParent(root.transform, false);
            entryObj.transform.localPosition = entryPos;

            var exitObj = new GameObject("Socket_Exit");
            exitObj.transform.SetParent(root.transform, false);
            exitObj.transform.localPosition = exitPos;

            Transform spawnTform = null;
            if (spawnPos.HasValue)
            {
                var spawnObj = new GameObject("PlayerSpawnPoint");
                spawnObj.transform.SetParent(root.transform, false);
                spawnObj.transform.localPosition = spawnPos.Value;
                spawnTform = spawnObj.transform;
            }

            var so = new SerializedObject(chunkComp);
            so.FindProperty("entryPoint").objectReferenceValue = entryObj.transform;
            so.FindProperty("exitPoint").objectReferenceValue = exitObj.transform;
            if (spawnTform != null)
            {
                so.FindProperty("playerSpawnPoint").objectReferenceValue = spawnTform;
            }
            so.ApplyModifiedProperties();
        }

        private static void SavePrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }
    }
}
