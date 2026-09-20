using System.IO;
using UnityEditor;
using UnityEngine;
using LevelGeneration;
using Combat;
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

            Sprite squareSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            if (squareSprite == null)
            {
                var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
                foreach (var s in sprites)
                {
                    if (s.name == "Square" || s.name == "WhiteBox")
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

            Color groundColor = new Color(0.16f, 0.20f, 0.26f, 1f);
            Color groundTopColor = new Color(0.24f, 0.60f, 0.85f, 1f);
            Color platformColor = new Color(0.22f, 0.32f, 0.44f, 1f);
            Color decorColor = new Color(0.85f, 0.70f, 0.25f, 0.9f);

            // 1. CHUNK_START_INTRO (Fixed Start)
            {
                var root = new GameObject("Chunk_Start_Intro");
                SetupSockets(root, new Vector3(0f, 0f, 0f), new Vector3(16f, 0f, 0f), new Vector3(3.5f, 0.85f, 0f));

                CreatePlatform(root.transform, "Ground", new Vector3(8f, -0.4f, 0f), new Vector2(16f, 0.8f), groundColor, squareSprite);
                CreatePlatform(root.transform, "Ground_Glow_Top", new Vector3(8f, -0.04f, 0f), new Vector2(16f, 0.08f), groundTopColor, squareSprite);
                CreatePlatform(root.transform, "Wall_Left", new Vector3(-0.4f, 2.5f, 0f), new Vector2(0.8f, 5.8f), groundColor, squareSprite);

                var decor = CreatePlatform(root.transform, "Start_Signpost", new Vector3(3.5f, 1.8f, 0f), new Vector2(0.25f, 2.4f), decorColor, squareSprite);
                CreatePlatform(decor.transform, "Sign_Placard", new Vector3(0f, 0.8f, 0f), new Vector2(2.5f, 0.8f), new Color(0.9f, 0.85f, 0.3f, 1f), squareSprite);

                SavePrefab(root, $"{Folder}/Chunk_Start_Intro.prefab");
            }

            // 2. CHUNK_VARIANT_A_COMBATARENA (Variant A)
            {
                var root = new GameObject("Chunk_Variant_A_CombatArena");
                SetupSockets(root, new Vector3(0f, 0f, 0f), new Vector3(22f, 0f, 0f));

                CreatePlatform(root.transform, "Ground", new Vector3(11f, -0.4f, 0f), new Vector2(22f, 0.8f), groundColor, squareSprite);
                CreatePlatform(root.transform, "Ground_Glow_Top", new Vector3(11f, -0.04f, 0f), new Vector2(22f, 0.08f), groundTopColor, squareSprite);

                CreatePlatform(root.transform, "Platform_Floating_1", new Vector3(6.5f, 2.3f, 0f), new Vector2(5.5f, 0.4f), platformColor, squareSprite);
                CreatePlatform(root.transform, "Platform_Floating_2", new Vector3(15.5f, 2.3f, 0f), new Vector2(5.5f, 0.4f), platformColor, squareSprite);
                CreatePlatform(root.transform, "Center_Pillar", new Vector3(11f, 1.0f, 0f), new Vector2(1.2f, 2.0f), new Color(0.2f, 0.28f, 0.38f), squareSprite);

                SavePrefab(root, $"{Folder}/Chunk_Variant_A_CombatArena.prefab");
            }

            // 3. CHUNK_VARIANT_B_TWOTIERELEVATION (Variant B)
            {
                var root = new GameObject("Chunk_Variant_B_TwoTierElevation");
                SetupSockets(root, new Vector3(0f, 0f, 0f), new Vector3(24f, 0f, 0f));

                CreatePlatform(root.transform, "Ground_Entry", new Vector3(2.5f, -0.4f, 0f), new Vector2(5f, 0.8f), groundColor, squareSprite);
                CreatePlatform(root.transform, "Ground_Entry_Top", new Vector3(2.5f, -0.04f, 0f), new Vector2(5f, 0.08f), groundTopColor, squareSprite);

                CreatePlatform(root.transform, "Step_Up_1", new Vector3(6.5f, 0.1f, 0f), new Vector2(3f, 1.8f), groundColor, squareSprite);
                CreatePlatform(root.transform, "Step_Up_1_Top", new Vector3(6.5f, 0.96f, 0f), new Vector2(3f, 0.08f), groundTopColor, squareSprite);

                CreatePlatform(root.transform, "Terrace_Center", new Vector3(12f, 0.6f, 0f), new Vector2(8f, 2.8f), groundColor, squareSprite);
                CreatePlatform(root.transform, "Terrace_Center_Top", new Vector3(12f, 1.96f, 0f), new Vector2(8f, 0.08f), new Color(1f, 0.65f, 0.2f, 1f), squareSprite);

                CreatePlatform(root.transform, "Step_Down_1", new Vector3(17.5f, 0.1f, 0f), new Vector2(3f, 1.8f), groundColor, squareSprite);
                CreatePlatform(root.transform, "Step_Down_1_Top", new Vector3(17.5f, 0.96f, 0f), new Vector2(3f, 0.08f), groundTopColor, squareSprite);

                CreatePlatform(root.transform, "Ground_Exit", new Vector3(21.5f, -0.4f, 0f), new Vector2(5f, 0.8f), groundColor, squareSprite);
                CreatePlatform(root.transform, "Ground_Exit_Top", new Vector3(21.5f, -0.04f, 0f), new Vector2(5f, 0.08f), groundTopColor, squareSprite);

                SavePrefab(root, $"{Folder}/Chunk_Variant_B_TwoTierElevation.prefab");
            }

            // 4. CHUNK_VARIANT_C_SPLITPATH (Variant C)
            {
                var root = new GameObject("Chunk_Variant_C_SplitPath");
                SetupSockets(root, new Vector3(0f, 0f, 0f), new Vector3(26f, 0f, 0f));

                CreatePlatform(root.transform, "Ground", new Vector3(13f, -0.4f, 0f), new Vector2(26f, 0.8f), groundColor, squareSprite);
                CreatePlatform(root.transform, "Ground_Glow_Top", new Vector3(13f, -0.04f, 0f), new Vector2(26f, 0.08f), groundTopColor, squareSprite);

                CreatePlatform(root.transform, "HighBeam_1", new Vector3(6f, 2.7f, 0f), new Vector2(4.5f, 0.35f), platformColor, squareSprite);
                CreatePlatform(root.transform, "HighBeam_2", new Vector3(13f, 3.2f, 0f), new Vector2(5.5f, 0.35f), platformColor, squareSprite);
                CreatePlatform(root.transform, "HighBeam_3", new Vector3(20f, 2.7f, 0f), new Vector2(4.5f, 0.35f), platformColor, squareSprite);

                CreatePlatform(root.transform, "Hanger_1", new Vector3(6f, 4.2f, 0f), new Vector2(0.12f, 3.0f), new Color(0.5f, 0.6f, 0.7f, 0.7f), squareSprite);
                CreatePlatform(root.transform, "Hanger_2", new Vector3(20f, 4.2f, 0f), new Vector2(0.12f, 3.0f), new Color(0.5f, 0.6f, 0.7f, 0.7f), squareSprite);

                SavePrefab(root, $"{Folder}/Chunk_Variant_C_SplitPath.prefab");
            }

            // 5. CHUNK_END_OUTRO (Fixed End)
            {
                var root = new GameObject("Chunk_End_Outro");
                SetupSockets(root, new Vector3(0f, 0f, 0f), new Vector3(16f, 0f, 0f));

                CreatePlatform(root.transform, "Ground", new Vector3(8f, -0.4f, 0f), new Vector2(16f, 0.8f), groundColor, squareSprite);
                CreatePlatform(root.transform, "Ground_Glow_Top", new Vector3(8f, -0.04f, 0f), new Vector2(16f, 0.08f), groundTopColor, squareSprite);
                CreatePlatform(root.transform, "Wall_Right", new Vector3(16.4f, 2.5f, 0f), new Vector2(0.8f, 5.8f), groundColor, squareSprite);

                CreatePlatform(root.transform, "Victory_Pedestal_Base", new Vector3(11f, 0.3f, 0f), new Vector2(4f, 0.6f), new Color(0.28f, 0.22f, 0.45f), squareSprite);
                CreatePlatform(root.transform, "Victory_Pedestal_Top", new Vector3(11f, 0.8f, 0f), new Vector2(2.5f, 0.4f), new Color(0.38f, 0.30f, 0.60f), squareSprite);
                CreatePlatform(root.transform, "Victory_Crystal", new Vector3(11f, 1.8f, 0f), new Vector2(1.0f, 1.5f), new Color(0.3f, 0.9f, 1f, 0.85f), squareSprite);

                SavePrefab(root, $"{Folder}/Chunk_End_Outro.prefab");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return "All 5 level chunk prefabs built successfully!";
        }

        private static GameObject CreatePlatform(Transform parent, string name, Vector3 localPos, Vector2 size, Color col, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = col;
            sr.sortingOrder = 5;

            var col2d = go.AddComponent<BoxCollider2D>();
            col2d.size = Vector2.one;

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
