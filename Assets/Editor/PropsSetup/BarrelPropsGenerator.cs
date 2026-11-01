using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Combat.Props;

namespace TestFight.Editor
{
    public static class BarrelPropsGenerator
    {
        private const string SpritesFolder = "Assets/Sprites/Props";
        private const string PrefabsFolder = "Assets/Prefabs/Props";

        [MenuItem("Tools/TestFight/Сгенерировать ассеты бочек и зелий (Build Barrel & Potion Assets)")]
        public static void GenerateAll()
        {
            EnsureFolders();

            string barrelPngPath = $"{SpritesFolder}/barrel_wood.png";
            string potionPngPath = $"{SpritesFolder}/potion_healing.png";
            string splinterPngPath = $"{SpritesFolder}/barrel_splinter.png";

            GenerateBarrelTexture(barrelPngPath);
            GeneratePotionTexture(potionPngPath);
            GenerateSplinterTexture(splinterPngPath);

            AssetDatabase.Refresh();

            ConfigureSpriteImporter(barrelPngPath, 64, new Vector2(0.5f, 0.5f));
            ConfigureSpriteImporter(potionPngPath, 64, new Vector2(0.5f, 0.5f));
            ConfigureSpriteImporter(splinterPngPath, 64, new Vector2(0.5f, 0.5f));

            AssetDatabase.Refresh();

            Sprite barrelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(barrelPngPath);
            Sprite potionSprite = AssetDatabase.LoadAssetAtPath<Sprite>(potionPngPath);
            Sprite splinterSprite = AssetDatabase.LoadAssetAtPath<Sprite>(splinterPngPath);

            Material litMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            if (litMat == null)
            {
                litMat = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            }

            // 1. Создаем префаб Зелья
            GameObject potionPrefab = BuildPotionPrefab(potionSprite, litMat);

            // 2. Создаем префаб Бочки
            BuildBarrelPrefab(barrelSprite, splinterSprite, potionPrefab, litMat);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=#00FFAA><b>[BarrelPropsGenerator]</b></color> Префабы бочки и зелья успешно созданы в Assets/Prefabs/Props!");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Sprites")) AssetDatabase.CreateFolder("Assets", "Sprites");
            if (!AssetDatabase.IsValidFolder(SpritesFolder)) AssetDatabase.CreateFolder("Assets/Sprites", "Props");

            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(PrefabsFolder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Props");
        }

        private static void ConfigureSpriteImporter(string path, float pixelsPerUnit, Vector2 pivot)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.spritePivot = pivot;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spritePixelsPerUnit = pixelsPerUnit;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        private static void GenerateBarrelTexture(string filePath)
        {
            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color clear = new Color(0, 0, 0, 0);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    tex.SetPixel(x, y, clear);
                }
            }

            int cx = 32;
            int bottomY = 8;
            int topY = 56;
            int height = topY - bottomY;

            Color darkOutline = new Color(0.12f, 0.08f, 0.04f, 1f);
            Color woodBase = new Color(0.48f, 0.30f, 0.16f, 1f);
            Color woodLight = new Color(0.62f, 0.40f, 0.22f, 1f);
            Color woodHighlight = new Color(0.74f, 0.50f, 0.28f, 1f);
            Color woodShadow = new Color(0.32f, 0.19f, 0.10f, 1f);
            Color woodDarkShadow = new Color(0.22f, 0.13f, 0.07f, 1f);

            Color ironDark = new Color(0.18f, 0.20f, 0.23f, 1f);
            Color ironMid = new Color(0.35f, 0.38f, 0.43f, 1f);
            Color ironLight = new Color(0.60f, 0.65f, 0.70f, 1f);
            Color rivetColor = new Color(0.85f, 0.88f, 0.92f, 1f);

            for (int y = bottomY; y <= topY; y++)
            {
                float t = (float)(y - bottomY) / height;
                float bulge = Mathf.Sin(t * Mathf.PI);
                int halfWidth = Mathf.RoundToInt(15f + bulge * 6f);

                int left = cx - halfWidth;
                int right = cx + halfWidth;

                bool isIronBand = (y >= bottomY + 7 && y <= bottomY + 12) ||
                                  (y >= topY - 14 && y <= topY - 9) ||
                                  (y >= bottomY + 2 && y <= bottomY + 4) ||
                                  (y >= topY - 4 && y <= topY - 2);

                for (int x = left; x <= right; x++)
                {
                    bool isBorder = (x == left || x == right || y == bottomY || y == topY);
                    if (isBorder)
                    {
                        tex.SetPixel(x, y, darkOutline);
                        continue;
                    }

                    if (isIronBand)
                    {
                        float bandGrad = (float)(x - left) / (right - left);
                        Color bandCol = bandGrad < 0.25f ? ironDark : (bandGrad < 0.45f ? ironLight : (bandGrad < 0.75f ? ironMid : ironDark));

                        // Заклепки на обручах
                        if ((x == cx - 11 || x == cx || x == cx + 11) && (y == bottomY + 9 || y == topY - 12))
                        {
                            bandCol = rivetColor;
                        }

                        tex.SetPixel(x, y, bandCol);
                    }
                    else
                    {
                        // Планки дерева (вертикальные бороздки)
                        int plankDist = Mathf.Abs((x - cx) % 9);
                        bool isPlankSeam = (plankDist == 0);

                        if (isPlankSeam)
                        {
                            tex.SetPixel(x, y, woodDarkShadow);
                        }
                        else
                        {
                            float xNorm = (float)(x - left) / (right - left);
                            Color c;
                            if (xNorm < 0.15f) c = woodShadow;
                            else if (xNorm < 0.35f) c = woodHighlight;
                            else if (xNorm < 0.65f) c = woodLight;
                            else if (xNorm < 0.85f) c = woodBase;
                            else c = woodShadow;

                            // Текстурный шум волокон дерева
                            if ((x + y * 2) % 7 == 0) c = Color.Lerp(c, woodDarkShadow, 0.25f);
                            if ((x * 3 + y) % 11 == 0) c = Color.Lerp(c, woodHighlight, 0.2f);

                            tex.SetPixel(x, y, c);
                        }
                    }
                }
            }

            tex.Apply();
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(filePath, bytes);
        }

        private static void GeneratePotionTexture(string filePath)
        {
            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color clear = new Color(0, 0, 0, 0);

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, clear);

            int cx = 32;
            int cy = 26;
            int radius = 15;

            Color glassOutline = new Color(0.12f, 0.22f, 0.26f, 0.95f);
            Color glassHighlight = new Color(0.85f, 0.95f, 1.0f, 0.85f);
            Color liquidRedDark = new Color(0.55f, 0.05f, 0.12f, 1f);
            Color liquidRedMid = new Color(0.88f, 0.12f, 0.20f, 1f);
            Color liquidRedBright = new Color(1.0f, 0.35f, 0.38f, 1f);
            Color corkColor = new Color(0.72f, 0.52f, 0.32f, 1f);
            Color corkDark = new Color(0.48f, 0.32f, 0.18f, 1f);

            // 1. Круглая колба
            for (int y = cy - radius; y <= cy + radius; y++)
            {
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    if (dist <= radius)
                    {
                        if (dist >= radius - 1.4f)
                        {
                            tex.SetPixel(x, y, glassOutline);
                        }
                        else
                        {
                            // Уровень жидкости (до cy + 5)
                            if (y <= cy + 5)
                            {
                                float grad = (float)(y - (cy - radius)) / (radius * 2);
                                float xGrad = (float)(x - (cx - radius)) / (radius * 2);

                                Color col = Color.Lerp(liquidRedDark, liquidRedBright, grad);
                                if (xGrad < 0.35f && y > cy - 6) col = Color.Lerp(col, liquidRedBright, 0.45f);

                                // Пузырек в зелье
                                if ((x == cx - 3 && y == cy - 2) || (x == cx + 5 && y == cy + 1))
                                {
                                    col = new Color(1f, 0.7f, 0.7f, 1f);
                                }

                                tex.SetPixel(x, y, col);
                            }
                            else
                            {
                                // Пустое стекло сверху
                                tex.SetPixel(x, y, new Color(0.7f, 0.85f, 0.9f, 0.18f));
                            }

                            // Блик стекла
                            float highlightDist = Vector2.Distance(new Vector2(x, y), new Vector2(cx - 7, cy + 5));
                            if (highlightDist < 3.2f)
                            {
                                tex.SetPixel(x, y, glassHighlight);
                            }
                        }
                    }
                }
            }

            // 2. Горлышко флакона
            int neckBottom = cy + radius - 3;
            int neckTop = neckBottom + 12;
            int neckHalfW = 4;

            for (int y = neckBottom; y <= neckTop; y++)
            {
                for (int x = cx - neckHalfW; x <= cx + neckHalfW; x++)
                {
                    bool isBorder = (x == cx - neckHalfW || x == cx + neckHalfW);
                    if (isBorder)
                    {
                        tex.SetPixel(x, y, glassOutline);
                    }
                    else
                    {
                        tex.SetPixel(x, y, (x == cx - neckHalfW + 1) ? glassHighlight : new Color(0.7f, 0.85f, 0.9f, 0.25f));
                    }
                }
            }

            // 3. Пробка из коры
            int corkBottom = neckTop;
            int corkTop = corkBottom + 7;
            int corkHalfW = 5;

            for (int y = corkBottom; y <= corkTop; y++)
            {
                for (int x = cx - corkHalfW; x <= corkHalfW + cx; x++)
                {
                    bool isBorder = (x == cx - corkHalfW || x == cx + corkHalfW || y == corkTop);
                    tex.SetPixel(x, y, isBorder ? corkDark : corkColor);
                }
            }

            tex.Apply();
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(filePath, bytes);
        }

        private static void GenerateSplinterTexture(string filePath)
        {
            int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color clear = new Color(0, 0, 0, 0);

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, clear);

            Color woodColor = new Color(0.58f, 0.38f, 0.20f, 1f);
            Color woodDark = new Color(0.28f, 0.16f, 0.08f, 1f);

            for (int y = 3; y < 13; y++)
            {
                for (int x = 5; x < 11; x++)
                {
                    bool border = (x == 5 || x == 10 || y == 3 || y == 12);
                    tex.SetPixel(x, y, border ? woodDark : woodColor);
                }
            }

            tex.Apply();
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(filePath, bytes);
        }

        private static GameObject BuildPotionPrefab(Sprite potionSprite, Material mat)
        {
            string path = $"{PrefabsFolder}/HealingPotion.prefab";

            var root = new GameObject("HealingPotion");
            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = potionSprite;
            if (mat != null) sr.sharedMaterial = mat;
            sr.sortingOrder = 20;

            // Физический твердый коллайдер для приземления на пол и платформы (не проваливается!)
            var solidCol = root.AddComponent<CircleCollider2D>();
            solidCol.isTrigger = false;
            solidCol.radius = 0.22f;
            solidCol.offset = new Vector2(0f, -0.05f);

            // Триггер-зона подбора для игрока и врагов
            var triggerCol = root.AddComponent<CircleCollider2D>();
            triggerCol.isTrigger = true;
            triggerCol.radius = 0.55f;

            var rb = root.AddComponent<Rigidbody2D>();
            rb.gravityScale = 2.6f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            // 2D свет мягкого рубиново-красного свечения
            var lightObj = new GameObject("PotionLight");
            lightObj.transform.SetParent(root.transform, false);
            var light = lightObj.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(1.0f, 0.25f, 0.35f, 1f);
            light.pointLightInnerRadius = 0.2f;
            light.pointLightOuterRadius = 1.6f;
            light.intensity = 0.9f;

            var potion = root.AddComponent<HealingPotionPickup>();
            potion.SetHealAmount(30f);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            GameObject.DestroyImmediate(root);

            return prefab;
        }

        private static void BuildBarrelPrefab(Sprite barrelSprite, Sprite splinterSprite, GameObject potionPrefab, Material mat)
        {
            string path = $"{PrefabsFolder}/DestructibleBarrel.prefab";

            var root = new GameObject("DestructibleBarrel");

            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = barrelSprite;
            if (mat != null) sr.sharedMaterial = mat;
            sr.sortingOrder = 10;

            var col = root.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.85f, 0.95f);
            col.offset = Vector2.zero;

            // Rigidbody2D static чтобы бочка уверенно стояла на поверхности и блокировала проход
            var rb = root.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;

            var barrel = root.AddComponent<DestructibleBarrel>();

            var so = new SerializedObject(barrel);
            so.FindProperty("healAmount").floatValue = 30f;
            so.FindProperty("potionPrefab").objectReferenceValue = potionPrefab;
            so.FindProperty("dropPotion").boolValue = true;
            so.FindProperty("dropChance").floatValue = 1.0f;
            so.FindProperty("maxHealth").floatValue = 1f;
            so.FindProperty("splinterSprite").objectReferenceValue = splinterSprite;
            so.FindProperty("splinterCount").intValue = 6;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            GameObject.DestroyImmediate(root);
        }
    }
}
