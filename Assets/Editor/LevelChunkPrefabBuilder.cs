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

        [MenuItem("Tools/Level Generation/Build All Chunk Prefabs (100x27)")]
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

            // Загружаем точный квадратный спрайт
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

            // Цветовая палитра нового нео-кибер/готического стиля Бездны
            Color groundBaseColor = new Color(0.10f, 0.13f, 0.18f, 1f);     // Тёмный сланец основания
            Color groundGlowColor = new Color(0.20f, 0.85f, 1.0f, 1f);      // Электрик-циан для пола
            Color wallColor = new Color(0.08f, 0.10f, 0.14f, 1f);            // Монолитные стены границ
            Color archPillarColor = new Color(0.13f, 0.17f, 0.24f, 1f);      // Архитектурные пилоны фона
            Color ceilingBeamColor = new Color(0.09f, 0.11f, 0.15f, 1f);     // Верхние балки потолка

            Color platformBodyColor = new Color(0.14f, 0.19f, 0.27f, 1f);    // Корпус парящих платформ
            Color tier1GlowColor = new Color(1.0f, 0.70f, 0.20f, 1f);       // Янтарно-золотой неон (нижний ярус)
            Color tier2GlowColor = new Color(0.20f, 0.88f, 1.0f, 1f);       // Неоновый бирюзовый (средний ярус)
            Color tier3GlowColor = new Color(0.92f, 0.28f, 0.75f, 1f);       // Неоновый пурпурный (верхний ярус / Apex)

            Color cableColor = new Color(0.20f, 0.26f, 0.36f, 0.65f);       // Тонкие подвесные тросы
            Color decorDark = new Color(0.16f, 0.13f, 0.25f, 1f);           // Декоративные постаменты
            Color decorBright = new Color(0.85f, 0.75f, 0.30f, 1f);          // Светящиеся символы и реликвии
            Color victoryCrystalColor = new Color(0.30f, 0.95f, 1.0f, 0.95f);// Кристалл победы

            const float ceilingY = 27.2f;

            // =========================================================================
            // 1. CHUNK_START_INTRO (Стартовая секция: длина 20м, высота 28м)
            // =========================================================================
            {
                var root = new GameObject("Chunk_Start_Intro");
                SetupSockets(root, new Vector3(0f, 0f, 0f), new Vector3(20f, 0f, 0f), new Vector3(4.5f, 0.55f, 0f));

                // Пол толщиной 4 метра
                CreatePlatform(root.transform, "Ground_Base", new Vector3(10f, -2.0f, 0f), new Vector2(20f, 4.0f), groundBaseColor, squareSprite, spriteMat, true, 4);
                CreatePlatform(root.transform, "Ground_Glow_Top", new Vector3(10f, -0.06f, 0f), new Vector2(20f, 0.12f), groundGlowColor, squareSprite, spriteMat, false, 6);

                // Левая монолитная стена высотой 31 метр
                CreatePlatform(root.transform, "Wall_Left", new Vector3(-0.5f, 13.5f, 0f), new Vector2(1.0f, 31.0f), wallColor, squareSprite, spriteMat, true, 4);
                CreatePlatform(root.transform, "Wall_Left_Glow", new Vector3(0.02f, 13.5f, 0f), new Vector2(0.06f, 31.0f), groundGlowColor, squareSprite, spriteMat, false, 6);

                // Верхний потолок
                CreatePlatform(root.transform, "Ceiling_Beam", new Vector3(10f, ceilingY, 0f), new Vector2(22f, 0.8f), ceilingBeamColor, squareSprite, spriteMat, true, 4);

                // Фоновые архитектурные арки и пилоны
                CreatePillar(root.transform, "Pillar_1", new Vector3(3.0f, 13.5f, 0f), new Vector2(0.6f, 27f), archPillarColor, squareSprite, spriteMat);
                CreatePillar(root.transform, "Pillar_2", new Vector3(12.0f, 13.5f, 0f), new Vector2(0.6f, 27f), archPillarColor, squareSprite, spriteMat);
                CreatePillar(root.transform, "Pillar_3", new Vector3(18.5f, 13.5f, 0f), new Vector2(0.6f, 27f), archPillarColor, squareSprite, spriteMat);

                // Стартовые ворота
                CreatePlatform(root.transform, "Gate_Pillar_L", new Vector3(2.5f, 3.5f, 0f), new Vector2(0.45f, 7.0f), wallColor, squareSprite, spriteMat, false, 5);
                CreatePlatform(root.transform, "Gate_Pillar_R", new Vector3(7.0f, 3.5f, 0f), new Vector2(0.45f, 7.0f), wallColor, squareSprite, spriteMat, false, 5);
                CreatePlatform(root.transform, "Gate_Lintel", new Vector3(4.75f, 7.2f, 0f), new Vector2(5.2f, 0.6f), platformBodyColor, squareSprite, spriteMat, false, 5);
                CreatePlatform(root.transform, "Gate_Sigil", new Vector3(4.75f, 6.2f, 0f), new Vector2(1.2f, 1.2f), decorBright, squareSprite, spriteMat, false, 7);

                // Ознакомительная парящая платформа
                CreateAirbornePlatform(root.transform, "Intro_Platform", new Vector3(14.0f, 2.4f, 0f), new Vector2(6.0f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);

                SavePrefab(root, $"{Folder}/Chunk_Start_Intro.prefab");
            }

            // =========================================================================
            // 2. CHUNK_VARIANT_A_COMBATARENA (Вариант А: Грандиозный амфитеатр и воздушная цитадель)
            // Метрики: 100 по X, 27 по Y. 11 ступенчатых ярусов, 8 точек спавна.
            // =========================================================================
            {
                var root = new GameObject("Chunk_Variant_A_CombatArena");
                SetupSockets(root, new Vector3(0f, 0f, 0f), new Vector3(100f, 0f, 0f), null, new Vector3[]
                {
                    new Vector3(20.0f, 0.55f, 0f),   // Пол слева (Y=0.0)
                    new Vector3(80.0f, 0.55f, 0f),   // Пол справа (Y=0.0)
                    new Vector3(17.0f, 5.35f, 0f),   // Левое нижнее крыло (Y=4.8)
                    new Vector3(83.0f, 5.35f, 0f),   // Правое нижнее крыло (Y=4.8)
                    new Vector3(25.0f, 12.55f, 0f),  // Левая средняя площадка (Y=12.0)
                    new Vector3(75.0f, 12.55f, 0f),  // Правая средняя площадка (Y=12.0)
                    new Vector3(50.0f, 17.35f, 0f),  // Центральный парящий остров (Y=16.8)
                    new Vector3(50.0f, 24.55f, 0f)   // Апекс-трон на вершине цитадели (Y=24.0)
                });

                // Полная монолитная земля X: 0..100
                CreatePlatform(root.transform, "Ground_Base", new Vector3(50f, -2.0f, 0f), new Vector2(100f, 4.0f), groundBaseColor, squareSprite, spriteMat, true, 4);
                CreatePlatform(root.transform, "Ground_Glow_Top", new Vector3(50f, -0.06f, 0f), new Vector2(100f, 0.12f), groundGlowColor, squareSprite, spriteMat, false, 6);

                // Верхний потолочный свод
                CreatePlatform(root.transform, "Ceiling_Beam", new Vector3(50f, ceilingY, 0f), new Vector2(102f, 0.8f), ceilingBeamColor, squareSprite, spriteMat, true, 4);

                // Фоновые колоссальные колонны (высота 27м)
                float[] pillarsA = { 12f, 28f, 45f, 55f, 72f, 88f };
                foreach (float px in pillarsA)
                {
                    CreatePillar(root.transform, $"Pillar_{px}", new Vector3(px, 13.5f, 0f), new Vector2(0.8f, 27f), archPillarColor, squareSprite, spriteMat);
                }

                // Центральный боевой алтарь на земле
                CreatePlatform(root.transform, "Shrine_Base", new Vector3(50f, 1.2f, 0f), new Vector2(6.0f, 2.4f), decorDark, squareSprite, spriteMat, true, 5);
                CreatePlatform(root.transform, "Shrine_Glow", new Vector3(50f, 2.44f, 0f), new Vector2(6.0f, 0.10f), groundGlowColor, squareSprite, spriteMat, false, 6);
                CreatePlatform(root.transform, "Shrine_Core", new Vector3(50f, 3.1f, 0f), new Vector2(1.2f, 1.2f), new Color(0.95f, 0.25f, 0.55f, 0.95f), squareSprite, spriteMat, false, 7);

                // --- НИЖНИЙ ЯРУС (Янтарный неон, Y = 2.4 .. 9.6, шаг ΔY = 2.4м, перекрытие для ботов) ---
                // Левое крыло
                CreateAirbornePlatform(root.transform, "Plat_L_Tier1", new Vector3(10f, 2.4f, 0f), new Vector2(6.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Plat_L_Tier2", new Vector3(17f, 4.8f, 0f), new Vector2(6.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Plat_L_Tier3", new Vector3(24f, 7.2f, 0f), new Vector2(6.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Plat_L_Tier4", new Vector3(31f, 9.6f, 0f), new Vector2(6.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);

                // Правое крыло
                CreateAirbornePlatform(root.transform, "Plat_R_Tier1", new Vector3(90f, 2.4f, 0f), new Vector2(6.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Plat_R_Tier2", new Vector3(83f, 4.8f, 0f), new Vector2(6.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Plat_R_Tier3", new Vector3(76f, 7.2f, 0f), new Vector2(6.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Plat_R_Tier4", new Vector3(69f, 9.6f, 0f), new Vector2(6.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);

                // --- СРЕДНИЙ ЯРУС (Бирюзовый неон, Y = 12.0 .. 16.8) ---
                CreateAirbornePlatform(root.transform, "Plat_Mid_Deck_L", new Vector3(25f, 12.0f, 0f), new Vector2(9.0f, 0.5f), platformBodyColor, tier2GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Plat_Mid_Deck_R", new Vector3(75f, 12.0f, 0f), new Vector2(9.0f, 0.5f), platformBodyColor, tier2GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Plat_Mid_High_L", new Vector3(33f, 14.4f, 0f), new Vector2(7.0f, 0.5f), platformBodyColor, tier2GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Plat_Mid_High_R", new Vector3(67f, 14.4f, 0f), new Vector2(7.0f, 0.5f), platformBodyColor, tier2GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);

                // Центральный парящий боевой остров
                CreateAirbornePlatform(root.transform, "Plat_Central_Island", new Vector3(50f, 16.8f, 0f), new Vector2(16.0f, 0.6f), platformBodyColor, tier2GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);

                // --- ВЫСОТНЫЙ ЯРУС (Пурпурный неон, Y = 19.2 .. 26.0) ---
                CreateAirbornePlatform(root.transform, "Plat_Sky_Bridge_L", new Vector3(40f, 19.2f, 0f), new Vector2(6.5f, 0.5f), platformBodyColor, tier3GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Plat_Sky_Bridge_R", new Vector3(60f, 19.2f, 0f), new Vector2(6.5f, 0.5f), platformBodyColor, tier3GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Plat_Sky_Obs_L", new Vector3(44f, 21.6f, 0f), new Vector2(6.5f, 0.5f), platformBodyColor, tier3GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Plat_Sky_Obs_R", new Vector3(56f, 21.6f, 0f), new Vector2(6.5f, 0.5f), platformBodyColor, tier3GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);

                // АПЕКС-ТРОН (Вершина мира, Y = 24.0m)
                CreateAirbornePlatform(root.transform, "Plat_Apex_Throne", new Vector3(50f, 24.0f, 0f), new Vector2(10.0f, 0.6f), platformBodyColor, tier3GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreatePlatform(root.transform, "Apex_Core", new Vector3(50f, 25.2f, 0f), new Vector2(1.4f, 1.4f), victoryCrystalColor, squareSprite, spriteMat, false, 7);

                SavePrefab(root, $"{Folder}/Chunk_Variant_A_CombatArena.prefab");
            }

            // =========================================================================
            // 3. CHUNK_VARIANT_B_TWOTIERELEVATION (Вариант B: Каскадные парящие террасы)
            // Метрики: 100 по X, 27 по Y. Подъем террасами, центральная цитадель, спуск.
            // =========================================================================
            {
                var root = new GameObject("Chunk_Variant_B_TwoTierElevation");
                SetupSockets(root, new Vector3(0f, 0f, 0f), new Vector3(100f, 0f, 0f), null, new Vector3[]
                {
                    new Vector3(12.0f, 0.55f, 0f),   // Входной пол (Y=0.0)
                    new Vector3(88.0f, 0.55f, 0f),   // Выходной пол (Y=0.0)
                    new Vector3(50.0f, 4.55f, 0f),   // Монолитная цитадель внизу (Y=4.0)
                    new Vector3(26.0f, 7.75f, 0f),   // Восходящая терраса слева (Y=7.2)
                    new Vector3(74.0f, 7.75f, 0f),   // Нисходящая терраса справа (Y=7.2)
                    new Vector3(50.0f, 14.95f, 0f),  // Главная воздушная палуба (Y=14.4)
                    new Vector3(38.0f, 17.35f, 0f),  // Верхний балкон запада (Y=16.8)
                    new Vector3(50.0f, 24.75f, 0f)   // Апекс-пик цитадели (Y=24.2)
                });

                // Нижняя рельефная земля с каменной цитаделью
                // Секция 1: Вход (X: 0..20, Y=0)
                CreatePlatform(root.transform, "Ground_Entry", new Vector3(10f, -2.0f, 0f), new Vector2(20f, 4.0f), groundBaseColor, squareSprite, spriteMat, true, 4);
                CreatePlatform(root.transform, "Ground_Entry_Glow", new Vector3(10f, -0.06f, 0f), new Vector2(20f, 0.12f), groundGlowColor, squareSprite, spriteMat, false, 6);

                // Секция 2: Подъем 1 (X: 20..36, верх Y=2.0)
                CreatePlatform(root.transform, "Ground_Step_1", new Vector3(28f, -1.0f, 0f), new Vector2(16f, 6.0f), groundBaseColor, squareSprite, spriteMat, true, 4);
                CreatePlatform(root.transform, "Ground_Step_1_Glow", new Vector3(28f, 1.94f, 0f), new Vector2(16f, 0.12f), groundGlowColor, squareSprite, spriteMat, false, 6);

                // Секция 3: Центральное плато (X: 36..64, верх Y=4.0)
                CreatePlatform(root.transform, "Ground_Plaza", new Vector3(50f, 0.0f, 0f), new Vector2(28f, 8.0f), groundBaseColor, squareSprite, spriteMat, true, 4);
                CreatePlatform(root.transform, "Ground_Plaza_Glow", new Vector3(50f, 3.94f, 0f), new Vector2(28f, 0.12f), tier1GlowColor, squareSprite, spriteMat, false, 6);

                // Секция 4: Спуск 1 (X: 64..80, верх Y=2.0)
                CreatePlatform(root.transform, "Ground_Step_2", new Vector3(72f, -1.0f, 0f), new Vector2(16f, 6.0f), groundBaseColor, squareSprite, spriteMat, true, 4);
                CreatePlatform(root.transform, "Ground_Step_2_Glow", new Vector3(72f, 1.94f, 0f), new Vector2(16f, 0.12f), groundGlowColor, squareSprite, spriteMat, false, 6);

                // Секция 5: Выход (X: 80..100, Y=0)
                CreatePlatform(root.transform, "Ground_Exit", new Vector3(90f, -2.0f, 0f), new Vector2(20f, 4.0f), groundBaseColor, squareSprite, spriteMat, true, 4);
                CreatePlatform(root.transform, "Ground_Exit_Glow", new Vector3(90f, -0.06f, 0f), new Vector2(20f, 0.12f), groundGlowColor, squareSprite, spriteMat, false, 6);

                CreatePlatform(root.transform, "Ceiling_Beam", new Vector3(50f, ceilingY, 0f), new Vector2(102f, 0.8f), ceilingBeamColor, squareSprite, spriteMat, true, 4);

                // Фоновые пилоны
                float[] pillarsB = { 15f, 36f, 64f, 85f };
                foreach (float px in pillarsB)
                {
                    CreatePillar(root.transform, $"Pillar_{px}", new Vector3(px, 13.5f, 0f), new Vector2(0.8f, 27f), archPillarColor, squareSprite, spriteMat);
                }

                // Парящие каскадные ступени (подъем слева, Y=2.4..12.0)
                CreateAirbornePlatform(root.transform, "Air_Step_L1", new Vector3(10f, 2.4f, 0f), new Vector2(6.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Air_Step_L2", new Vector3(18f, 4.8f, 0f), new Vector2(6.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Air_Step_L3", new Vector3(26f, 7.2f, 0f), new Vector2(6.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Air_Step_L4", new Vector3(34f, 9.6f, 0f), new Vector2(6.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Air_Step_L5", new Vector3(42f, 12.0f, 0f), new Vector2(8.0f, 0.5f), platformBodyColor, tier2GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);

                // Парящие каскадные ступени (спуск справа, Y=12.0..2.4)
                CreateAirbornePlatform(root.transform, "Air_Step_R5", new Vector3(58f, 12.0f, 0f), new Vector2(8.0f, 0.5f), platformBodyColor, tier2GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Air_Step_R4", new Vector3(66f, 9.6f, 0f), new Vector2(6.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Air_Step_R3", new Vector3(74f, 7.2f, 0f), new Vector2(6.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Air_Step_R2", new Vector3(82f, 4.8f, 0f), new Vector2(6.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Air_Step_R1", new Vector3(90f, 2.4f, 0f), new Vector2(6.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);

                // Центральная грандиозная палуба цитадели (Y = 14.4m, ширина 18м)
                CreateAirbornePlatform(root.transform, "Citadel_Air_Deck", new Vector3(50f, 14.4f, 0f), new Vector2(18.0f, 0.6f), platformBodyColor, tier2GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);

                // Верхний каскад к апексу (Y = 16.8 .. 24.2m)
                CreateAirbornePlatform(root.transform, "Air_Upper_L1", new Vector3(38f, 16.8f, 0f), new Vector2(7.0f, 0.5f), platformBodyColor, tier3GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Air_Upper_R1", new Vector3(62f, 16.8f, 0f), new Vector2(7.0f, 0.5f), platformBodyColor, tier3GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Air_Upper_L2", new Vector3(44f, 19.2f, 0f), new Vector2(7.0f, 0.5f), platformBodyColor, tier3GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Air_Upper_R2", new Vector3(56f, 19.2f, 0f), new Vector2(7.0f, 0.5f), platformBodyColor, tier3GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Air_Upper_Balcony", new Vector3(50f, 21.6f, 0f), new Vector2(12.0f, 0.5f), platformBodyColor, tier3GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Air_Apex_Peak", new Vector3(50f, 24.2f, 0f), new Vector2(8.0f, 0.6f), platformBodyColor, tier3GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);

                SavePrefab(root, $"{Folder}/Chunk_Variant_B_TwoTierElevation.prefab");
            }

            // =========================================================================
            // 4. CHUNK_VARIANT_C_SPLITPATH (Вариант C: Трехъярусная воздушная эстакада)
            // Метрики: 100 по X, 27 по Y. 3 непрерывных боевых эшелона с вертикальными лифтами-прыжками.
            // =========================================================================
            {
                var root = new GameObject("Chunk_Variant_C_SplitPath");
                SetupSockets(root, new Vector3(0f, 0f, 0f), new Vector3(100f, 0f, 0f), null, new Vector3[]
                {
                    new Vector3(25.0f, 0.55f, 0f),   // Пол слева (Y=0.0)
                    new Vector3(75.0f, 0.55f, 0f),   // Пол справа (Y=0.0)
                    new Vector3(28.0f, 10.15f, 0f),  // Средний эшелон запад (Y=9.6)
                    new Vector3(50.0f, 11.75f, 0f),  // Средний эшелон центр (Y=11.2)
                    new Vector3(72.0f, 10.15f, 0f),  // Средний эшелон восток (Y=9.6)
                    new Vector3(38.0f, 18.95f, 0f),  // Высотный эшелон запад (Y=18.4)
                    new Vector3(62.0f, 18.95f, 0f),  // Высотный эшелон восток (Y=18.4)
                    new Vector3(50.0f, 23.75f, 0f)   // Высотный соборный мост (Y=23.2)
                });

                // Нижний скоростной коридор X: 0..100
                CreatePlatform(root.transform, "Ground_Base", new Vector3(50f, -2.0f, 0f), new Vector2(100f, 4.0f), groundBaseColor, squareSprite, spriteMat, true, 4);
                CreatePlatform(root.transform, "Ground_Glow_Top", new Vector3(50f, -0.06f, 0f), new Vector2(100f, 0.12f), groundGlowColor, squareSprite, spriteMat, false, 6);
                CreatePlatform(root.transform, "Ceiling_Beam", new Vector3(50f, ceilingY, 0f), new Vector2(102f, 0.8f), ceilingBeamColor, squareSprite, spriteMat, true, 4);

                // Опорные мостовые пилоны (высота 27м)
                float[] pillarsC = { 20f, 38f, 62f, 80f };
                foreach (float px in pillarsC)
                {
                    CreatePillar(root.transform, $"Pillar_{px}", new Vector3(px, 13.5f, 0f), new Vector2(0.9f, 27f), archPillarColor, squareSprite, spriteMat);
                }

                // Подъем со дна на Средний эшелон (шаг ΔY = 2.4м)
                CreateAirbornePlatform(root.transform, "Lift_L1", new Vector3(8f, 2.4f, 0f), new Vector2(5.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Lift_L2", new Vector3(15f, 4.8f, 0f), new Vector2(5.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Lift_L3", new Vector3(22f, 7.2f, 0f), new Vector2(5.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);

                CreateAirbornePlatform(root.transform, "Lift_R1", new Vector3(92f, 2.4f, 0f), new Vector2(5.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Lift_R2", new Vector3(85f, 4.8f, 0f), new Vector2(5.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Lift_R3", new Vector3(78f, 7.2f, 0f), new Vector2(5.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);

                // ЭШЕЛОН 2: СРЕДНЯЯ ЭСТАКАДА (Бирюзовый неон, Y = 9.6 .. 11.2)
                CreateAirbornePlatform(root.transform, "Skyway_Mid_West", new Vector3(28f, 9.6f, 0f), new Vector2(13.0f, 0.6f), platformBodyColor, tier2GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Skyway_Mid_Bridge1", new Vector3(38f, 10.4f, 0f), new Vector2(7.0f, 0.5f), platformBodyColor, tier2GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Skyway_Mid_Center", new Vector3(50f, 11.2f, 0f), new Vector2(16.0f, 0.6f), platformBodyColor, tier2GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Skyway_Mid_Bridge2", new Vector3(62f, 10.4f, 0f), new Vector2(7.0f, 0.5f), platformBodyColor, tier2GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Skyway_Mid_East", new Vector3(72f, 9.6f, 0f), new Vector2(13.0f, 0.6f), platformBodyColor, tier2GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);

                // Подъем со среднего на высший эшелон (шаг ΔY = 2.4м)
                CreateAirbornePlatform(root.transform, "High_Lift_L1", new Vector3(32f, 13.6f, 0f), new Vector2(6.0f, 0.5f), platformBodyColor, tier2GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "High_Lift_L2", new Vector3(38f, 16.0f, 0f), new Vector2(6.0f, 0.5f), platformBodyColor, tier2GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);

                CreateAirbornePlatform(root.transform, "High_Lift_R1", new Vector3(68f, 13.6f, 0f), new Vector2(6.0f, 0.5f), platformBodyColor, tier2GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "High_Lift_R2", new Vector3(62f, 16.0f, 0f), new Vector2(6.0f, 0.5f), platformBodyColor, tier2GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);

                // ЭШЕЛОН 3: ВЫСШАЯ НЕБЕСНАЯ ЭСТАКАДА (Пурпурный неон, Y = 18.4 .. 25.6)
                CreateAirbornePlatform(root.transform, "Skyway_High_West", new Vector3(38f, 18.4f, 0f), new Vector2(10.0f, 0.6f), platformBodyColor, tier3GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Skyway_High_East", new Vector3(62f, 18.4f, 0f), new Vector2(10.0f, 0.6f), platformBodyColor, tier3GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Skyway_Cathedral_Deck", new Vector3(50f, 20.8f, 0f), new Vector2(16.0f, 0.6f), platformBodyColor, tier3GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Skyway_High_Catwalk", new Vector3(50f, 23.2f, 0f), new Vector2(8.0f, 0.5f), platformBodyColor, tier3GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Skyway_Apex_Core", new Vector3(50f, 25.6f, 0f), new Vector2(5.0f, 0.5f), platformBodyColor, tier3GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);

                SavePrefab(root, $"{Folder}/Chunk_Variant_C_SplitPath.prefab");
            }

            // =========================================================================
            // 5. CHUCN_VARINT_D (Вариант D: Парящие острова Бездны)
            // Метрики: 100 по X, 27 по Y. Разрезанная бездна с летающими монолитами.
            // =========================================================================
            {
                var root = new GameObject("Chucn_varint_D");
                SetupSockets(root, new Vector3(0f, 0f, 0f), new Vector3(100f, 0f, 0f), null, new Vector3[]
                {
                    new Vector3(15.0f, 0.55f, 0f),   // Западный берег (Y=0.0)
                    new Vector3(85.0f, 0.55f, 0f),   // Восточный берег (Y=0.0)
                    new Vector3(50.0f, 0.55f, 0f),   // Остров бездны внизу (Y=0.0)
                    new Vector3(34.0f, 10.15f, 0f),  // Парящий монолит запада (Y=9.6)
                    new Vector3(66.0f, 10.15f, 0f),  // Парящий монолит востока (Y=9.6)
                    new Vector3(50.0f, 14.95f, 0f),  // Центральный летающий монолит (Y=14.4)
                    new Vector3(50.0f, 22.15f, 0f),  // Высший святилищный остров (Y=21.6)
                    new Vector3(50.0f, 26.75f, 0f)   // Апекс-маяк Бездны (Y=26.2)
                });

                // Прерывистая земля с двумя провалами бездны
                // Западный берег (X: 0..30)
                CreatePlatform(root.transform, "Ground_West_Shore", new Vector3(15f, -2.0f, 0f), new Vector2(30f, 4.0f), groundBaseColor, squareSprite, spriteMat, true, 4);
                CreatePlatform(root.transform, "Ground_West_Glow", new Vector3(15f, -0.06f, 0f), new Vector2(30f, 0.12f), groundGlowColor, squareSprite, spriteMat, false, 6);

                // Центральный остров бездны (X: 38..62)
                CreatePlatform(root.transform, "Ground_Abyss_Center", new Vector3(50f, -2.0f, 0f), new Vector2(24f, 4.0f), groundBaseColor, squareSprite, spriteMat, true, 4);
                CreatePlatform(root.transform, "Ground_Abyss_Glow", new Vector3(50f, -0.06f, 0f), new Vector2(24f, 0.12f), groundGlowColor, squareSprite, spriteMat, false, 6);

                // Восточный берег (X: 70..100)
                CreatePlatform(root.transform, "Ground_East_Shore", new Vector3(85f, -2.0f, 0f), new Vector2(30f, 4.0f), groundBaseColor, squareSprite, spriteMat, true, 4);
                CreatePlatform(root.transform, "Ground_East_Glow", new Vector3(85f, -0.06f, 0f), new Vector2(30f, 0.12f), groundGlowColor, squareSprite, spriteMat, false, 6);

                CreatePlatform(root.transform, "Ceiling_Beam", new Vector3(50f, ceilingY, 0f), new Vector2(102f, 0.8f), ceilingBeamColor, squareSprite, spriteMat, true, 4);

                // Фоновые колонны
                float[] pillarsD = { 10f, 34f, 66f, 90f };
                foreach (float px in pillarsD)
                {
                    CreatePillar(root.transform, $"Pillar_{px}", new Vector3(px, 13.5f, 0f), new Vector2(0.8f, 27f), archPillarColor, squareSprite, spriteMat);
                }

                // Нижние перекидные острова над бездной (Y = 2.4 .. 4.8)
                CreateAirbornePlatform(root.transform, "Island_Low_1", new Vector3(12f, 2.4f, 0f), new Vector2(6.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Island_Low_Bridge_W", new Vector3(34f, 2.4f, 0f), new Vector2(6.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Island_Low_Bridge_E", new Vector3(66f, 2.4f, 0f), new Vector2(6.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Island_Low_2", new Vector3(88f, 2.4f, 0f), new Vector2(6.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);

                CreateAirbornePlatform(root.transform, "Island_Low_3", new Vector3(20f, 4.8f, 0f), new Vector2(6.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Island_Low_4", new Vector3(80f, 4.8f, 0f), new Vector2(6.5f, 0.5f), platformBodyColor, tier1GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);

                // Средний архипелаг (Y = 7.2 .. 14.4)
                CreateAirbornePlatform(root.transform, "Island_Mid_1", new Vector3(26f, 7.2f, 0f), new Vector2(7.0f, 0.5f), platformBodyColor, tier2GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Island_Mid_2", new Vector3(74f, 7.2f, 0f), new Vector2(7.0f, 0.5f), platformBodyColor, tier2GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);

                CreateAirbornePlatform(root.transform, "Island_Mid_3", new Vector3(34f, 9.6f, 0f), new Vector2(7.5f, 0.5f), platformBodyColor, tier2GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Island_Mid_4", new Vector3(66f, 9.6f, 0f), new Vector2(7.5f, 0.5f), platformBodyColor, tier2GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);

                CreateAirbornePlatform(root.transform, "Island_Mid_5", new Vector3(42f, 12.0f, 0f), new Vector2(8.0f, 0.5f), platformBodyColor, tier2GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Island_Mid_6", new Vector3(58f, 12.0f, 0f), new Vector2(8.0f, 0.5f), platformBodyColor, tier2GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);

                CreateAirbornePlatform(root.transform, "Island_Flying_Monolith", new Vector3(50f, 14.4f, 0f), new Vector2(14.0f, 0.6f), platformBodyColor, tier2GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);

                // Верхние летающие храмовые острова (Y = 16.8 .. 26.2)
                CreateAirbornePlatform(root.transform, "Island_High_1", new Vector3(40f, 16.8f, 0f), new Vector2(7.0f, 0.5f), platformBodyColor, tier3GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Island_High_2", new Vector3(60f, 16.8f, 0f), new Vector2(7.0f, 0.5f), platformBodyColor, tier3GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);

                CreateAirbornePlatform(root.transform, "Island_High_3", new Vector3(44f, 19.2f, 0f), new Vector2(7.0f, 0.5f), platformBodyColor, tier3GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Island_High_4", new Vector3(56f, 19.2f, 0f), new Vector2(7.0f, 0.5f), platformBodyColor, tier3GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);

                CreateAirbornePlatform(root.transform, "Island_Sky_Sanctum", new Vector3(50f, 21.6f, 0f), new Vector2(11.0f, 0.6f), platformBodyColor, tier3GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Island_Upper_Spire", new Vector3(50f, 24.0f, 0f), new Vector2(7.5f, 0.5f), platformBodyColor, tier3GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);
                CreateAirbornePlatform(root.transform, "Island_Apex_Beacon", new Vector3(50f, 26.2f, 0f), new Vector2(4.5f, 0.5f), platformBodyColor, tier3GlowColor, cableColor, squareSprite, spriteMat, true, ceilingY);

                SavePrefab(root, $"{Folder}/Chucn_varint_D.prefab");
            }

            // =========================================================================
            // 6. CHUNK_END_OUTRO (Финал уровня: длина 22м, высота 28м)
            // =========================================================================
            {
                var root = new GameObject("Chunk_End_Outro");
                SetupSockets(root, new Vector3(0f, 0f, 0f), new Vector3(22f, 0f, 0f));

                CreatePlatform(root.transform, "Ground_Base", new Vector3(11f, -2.0f, 0f), new Vector2(22f, 4.0f), groundBaseColor, squareSprite, spriteMat, true, 4);
                CreatePlatform(root.transform, "Ground_Glow_Top", new Vector3(11f, -0.06f, 0f), new Vector2(22f, 0.12f), groundGlowColor, squareSprite, spriteMat, false, 6);

                // Правая граница уровня (стена 31 метр)
                CreatePlatform(root.transform, "Wall_Right", new Vector3(22.5f, 13.5f, 0f), new Vector2(1.0f, 31.0f), wallColor, squareSprite, spriteMat, true, 4);
                CreatePlatform(root.transform, "Wall_Right_Glow", new Vector3(21.98f, 13.5f, 0f), new Vector2(0.06f, 31.0f), groundGlowColor, squareSprite, spriteMat, false, 6);

                CreatePlatform(root.transform, "Ceiling_Beam", new Vector3(11f, ceilingY, 0f), new Vector2(24f, 0.8f), ceilingBeamColor, squareSprite, spriteMat, true, 4);

                // Фоновые пилоны
                CreatePillar(root.transform, "Pillar_1", new Vector3(4.0f, 13.5f, 0f), new Vector2(0.6f, 27f), archPillarColor, squareSprite, spriteMat);
                CreatePillar(root.transform, "Pillar_2", new Vector3(11.0f, 13.5f, 0f), new Vector2(0.6f, 27f), archPillarColor, squareSprite, spriteMat);
                CreatePillar(root.transform, "Pillar_3", new Vector3(18.0f, 13.5f, 0f), new Vector2(0.6f, 27f), archPillarColor, squareSprite, spriteMat);

                // Финальный постамент и кристалл победы
                CreatePlatform(root.transform, "Victory_Pedestal_Base", new Vector3(15.0f, 0.45f, 0f), new Vector2(6.0f, 0.9f), decorDark, squareSprite, spriteMat, true, 5);
                CreatePlatform(root.transform, "Victory_Pedestal_Tier2", new Vector3(15.0f, 1.15f, 0f), new Vector2(4.0f, 0.6f), platformBodyColor, squareSprite, spriteMat, true, 5);

                // Арка портала
                CreatePlatform(root.transform, "Portal_Col_L", new Vector3(12.5f, 3.5f, 0f), new Vector2(0.45f, 6.0f), wallColor, squareSprite, spriteMat, false, 5);
                CreatePlatform(root.transform, "Portal_Col_R", new Vector3(17.5f, 3.5f, 0f), new Vector2(0.45f, 6.0f), wallColor, squareSprite, spriteMat, false, 5);
                CreatePlatform(root.transform, "Portal_Lintel", new Vector3(15.0f, 6.6f, 0f), new Vector2(5.6f, 0.6f), platformBodyColor, squareSprite, spriteMat, false, 5);

                var crystalObj = CreatePlatform(root.transform, "Victory_Crystal", new Vector3(15.0f, 2.6f, 0f), new Vector2(1.4f, 1.8f), victoryCrystalColor, squareSprite, spriteMat, false, 7);
                var triggerCol = crystalObj.AddComponent<BoxCollider2D>();
                triggerCol.isTrigger = true;
                triggerCol.size = Vector2.one;
                crystalObj.AddComponent<VictoryCrystal>();

                SavePrefab(root, $"{Folder}/Chunk_End_Outro.prefab");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return "All 6 level chunk prefabs (100x27) built successfully with full new style and bot-reachable airborne platforms!";
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

        private static void CreateAirbornePlatform(Transform parent, string name, Vector3 localPos, Vector2 size, Color bodyColor, Color glowColor, Color cableColor, Sprite sprite, Material mat, bool addCables = true, float ceilingY = 27.2f)
        {
            // 1. Корпус платформы
            var body = CreatePlatform(parent, name, localPos, size, bodyColor, sprite, mat, true, 4);

            // 2. Верхний неоновый край платформы
            float topY = localPos.y + (size.y * 0.5f) - 0.05f;
            CreatePlatform(parent, $"{name}_GlowTop", new Vector3(localPos.x, topY, 0f), new Vector2(size.x, 0.10f), glowColor, sprite, mat, false, 6);

            // 3. Подвесные тросы, уходящие в потолок
            if (addCables && ceilingY > localPos.y)
            {
                float cableHeight = ceilingY - (localPos.y + size.y * 0.5f);
                float cableCenterY = (localPos.y + size.y * 0.5f) + (cableHeight * 0.5f);

                float cableOffset = size.x * 0.40f;
                CreatePlatform(parent, $"{name}_Cable_L", new Vector3(localPos.x - cableOffset, cableCenterY, 0f), new Vector2(0.08f, cableHeight), cableColor, sprite, mat, false, 3);
                CreatePlatform(parent, $"{name}_Cable_R", new Vector3(localPos.x + cableOffset, cableCenterY, 0f), new Vector2(0.08f, cableHeight), cableColor, sprite, mat, false, 3);
            }
        }

        private static void CreatePillar(Transform parent, string name, Vector3 localPos, Vector2 size, Color col, Sprite sprite, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = col;
            sr.sortingOrder = 3;
            if (mat != null) sr.sharedMaterial = mat;
        }

        private static void SetupSockets(GameObject root, Vector3 entryPos, Vector3 exitPos, Vector3? spawnPos = null, Vector3[] enemySpawnPositions = null)
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

            if (enemySpawnPositions != null && enemySpawnPositions.Length > 0)
            {
                var prop = so.FindProperty("enemySpawnPoints");
                prop.ClearArray();
                for (int i = 0; i < enemySpawnPositions.Length; i++)
                {
                    var enemyPointObj = new GameObject($"EnemySpawnPoint_{i + 1}");
                    enemyPointObj.transform.SetParent(root.transform, false);
                    enemyPointObj.transform.localPosition = enemySpawnPositions[i];
                    prop.InsertArrayElementAtIndex(i);
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = enemyPointObj.transform;
                }
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
