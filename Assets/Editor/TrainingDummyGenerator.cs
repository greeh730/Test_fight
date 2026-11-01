using System.IO;
using UnityEditor;
using UnityEngine;

namespace Combat.Editor
{
    public static class TrainingDummyGenerator
    {
        [MenuItem("Combat/Generate Training Dummy Sprite")]
        public static void GenerateSprite()
        {
            int w = 64;
            int h = 80;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;

            Color32 c_Clear = new Color32(0, 0, 0, 0);
            Color32 c_Out = new Color32(24, 16, 12, 255);
            Color32 c_WoodDark = new Color32(65, 38, 20, 255);
            Color32 c_WoodMid = new Color32(115, 72, 42, 255);
            Color32 c_WoodLight = new Color32(165, 108, 64, 255);
            Color32 c_WoodHighlight = new Color32(205, 142, 92, 255);
            Color32 c_IronDark = new Color32(38, 42, 50, 255);
            Color32 c_IronMid = new Color32(75, 84, 98, 255);
            Color32 c_IronLight = new Color32(140, 152, 172, 255);
            Color32 c_IronHi = new Color32(200, 210, 225, 255);
            Color32 c_RopeDark = new Color32(115, 82, 38, 255);
            Color32 c_RopeMid = new Color32(173, 132, 72, 255);
            Color32 c_RopeLight = new Color32(220, 180, 110, 255);
            Color32 c_BurlapDark = new Color32(145, 102, 45, 255);
            Color32 c_BurlapMid = new Color32(195, 148, 75, 255);
            Color32 c_BurlapLight = new Color32(232, 188, 112, 255);
            Color32 c_BurlapHi = new Color32(250, 218, 155, 255);
            Color32 c_TargetDark = new Color32(140, 22, 32, 255);
            Color32 c_TargetMid = new Color32(215, 38, 52, 255);
            Color32 c_TargetHi = new Color32(255, 75, 90, 255);
            Color32 c_WrapDark = new Color32(112, 120, 132, 255);
            Color32 c_WrapMid = new Color32(180, 190, 202, 255);
            Color32 c_WrapLight = new Color32(235, 240, 248, 255);
            Color32 c_Stitch = new Color32(40, 28, 18, 255);

            Color32[] pixels = new Color32[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = c_Clear;

            void SetP(int x, int y, Color32 col)
            {
                if (x >= 0 && x < w && y >= 0 && y < h)
                {
                    pixels[y * w + x] = col;
                }
            }

            // 1. BASE STAND (Y: 0..7)
            for (int y = 0; y <= 7; y++)
            {
                int minX = (y <= 2) ? 14 : (y <= 5 ? 18 : 22);
                int maxX = (y <= 2) ? 49 : (y <= 5 ? 45 : 41);
                for (int x = minX; x <= maxX; x++)
                {
                    if (x == minX || x == maxX || y == 0 || y == 7) SetP(x, y, c_Out);
                    else
                    {
                        if (y <= 2) SetP(x, y, (x < 32) ? c_WoodDark : c_WoodMid);
                        else if (y <= 5) SetP(x, y, (x < 28) ? c_WoodMid : (x < 38 ? c_WoodLight : c_WoodDark));
                        else SetP(x, y, c_WoodDark);
                    }
                }
            }
            // Base iron brackets & rivets
            for (int y = 1; y <= 4; y++)
            {
                SetP(20, y, c_IronMid); SetP(21, y, c_IronLight); SetP(22, y, c_IronDark);
                SetP(41, y, c_IronMid); SetP(42, y, c_IronLight); SetP(43, y, c_IronDark);
            }
            SetP(21, 3, c_IronHi); SetP(42, 3, c_IronHi); // Rivet glints

            // 2. MAIN SUPPORT POST (Low Zone, Y: 8..27)
            for (int y = 8; y <= 27; y++)
            {
                for (int x = 25; x <= 38; x++)
                {
                    if (x == 25 || x == 38) SetP(x, y, c_Out);
                    else if (x <= 27) SetP(x, y, c_WoodDark);
                    else if (x <= 31) SetP(x, y, c_WoodMid);
                    else if (x <= 35) SetP(x, y, c_WoodLight);
                    else SetP(x, y, c_WoodDark);
                }
            }
            // Vertical wood grain lines
            for (int y = 8; y <= 27; y++)
            {
                if (y % 4 != 0) SetP(30, y, c_WoodHighlight);
                if ((y + 2) % 5 != 0) SetP(33, y, c_WoodMid);
            }
            // Rope coil wraps (Y: 13..15 and 19..22)
            for (int y = 13; y <= 15; y++)
            {
                for (int x = 25; x <= 38; x++)
                {
                    if (x == 25 || x == 38) SetP(x, y, c_Out);
                    else SetP(x, y, (x % 3 == 0) ? c_RopeLight : ((x % 3 == 1) ? c_RopeMid : c_RopeDark));
                }
            }
            for (int y = 19; y <= 22; y++)
            {
                for (int x = 25; x <= 38; x++)
                {
                    if (x == 25 || x == 38) SetP(x, y, c_Out);
                    else SetP(x, y, ((x + y) % 3 == 0) ? c_RopeLight : c_RopeMid);
                }
            }
            // Rope knot
            SetP(37, 20, c_RopeLight); SetP(38, 20, c_RopeMid); SetP(39, 20, c_Out);
            SetP(37, 21, c_RopeDark); SetP(38, 21, c_RopeLight); SetP(39, 21, c_Out);

            // Metal collar ring at Y = 25..27
            for (int y = 25; y <= 27; y++)
            {
                for (int x = 24; x <= 39; x++)
                {
                    if (x == 24 || x == 39 || y == 25 || y == 27) SetP(x, y, c_Out);
                    else SetP(x, y, (x <= 28) ? c_IronDark : (x <= 34 ? c_IronMid : c_IronLight));
                }
            }
            SetP(27, 26, c_IronHi); SetP(32, 26, c_IronHi); SetP(36, 26, c_IronHi);

            // 3. HORIZONTAL CROSSBAR ARMS (Y: 43..49)
            // Left arm (X: 6..24)
            for (int y = 44; y <= 48; y++)
            {
                for (int x = 6; x <= 24; x++)
                {
                    if (x == 6 || y == 44 || y == 48) SetP(x, y, c_Out);
                    else SetP(x, y, (y == 47) ? c_WoodHighlight : (y >= 46 ? c_WoodLight : c_WoodMid));
                }
            }
            // Right arm (X: 39..57)
            for (int y = 44; y <= 48; y++)
            {
                for (int x = 39; x <= 57; x++)
                {
                    if (x == 57 || y == 44 || y == 48) SetP(x, y, c_Out);
                    else SetP(x, y, (y == 47) ? c_WoodHighlight : (y >= 46 ? c_WoodLight : c_WoodMid));
                }
            }
            // Arm bandages / wraps
            for (int y = 44; y <= 48; y++)
            {
                for (int x = 11; x <= 16; x++)
                {
                    if (y == 44 || y == 48) SetP(x, y, c_Out);
                    else SetP(x, y, (x % 2 == 0) ? c_WrapLight : c_WrapMid);
                }
                for (int x = 47; x <= 52; x++)
                {
                    if (y == 44 || y == 48) SetP(x, y, c_Out);
                    else SetP(x, y, (x % 2 == 0) ? c_WrapLight : c_WrapMid);
                }
            }

            // 4. TORSO (Burlap / Padded Straw Sack, Y: 28..57)
            for (int y = 28; y <= 57; y++)
            {
                // Rounded barrel shape
                float t = (y - 28f) / 29f; // 0..1
                int widthBonus = (int)(Mathf.Sin(t * Mathf.PI) * 4f);
                int minX = 23 - widthBonus;
                int maxX = 40 + widthBonus;

                for (int x = minX; x <= maxX; x++)
                {
                    if (x == minX || x == maxX || y == 28 || y == 57)
                    {
                        SetP(x, y, c_Out);
                    }
                    else
                    {
                        if (x < 26) SetP(x, y, c_BurlapDark);
                        else if (x < 31) SetP(x, y, c_BurlapMid);
                        else if (x < 37) SetP(x, y, c_BurlapLight);
                        else SetP(x, y, c_BurlapDark);
                    }
                }
            }
            // Straw cross-stitching on edges
            for (int y = 30; y <= 55; y += 3)
            {
                SetP(22, y, c_Stitch);
                SetP(41, y, c_Stitch);
            }

            // 5. TARGET BULLSEYE ON CHEST (Center at X: 31, Y: 39)
            int cx = 31, cy = 39;
            for (int dy = -7; dy <= 7; dy++)
            {
                for (int dx = -7; dx <= 7; dx++)
                {
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    int px = cx + dx;
                    int py = cy + dy;
                    if (dist <= 6.8f && dist > 4.6f)
                    {
                        SetP(px, py, c_TargetMid);
                    }
                    else if (dist <= 4.6f && dist > 2.4f)
                    {
                        SetP(px, py, c_BurlapHi);
                    }
                    else if (dist <= 2.4f)
                    {
                        SetP(px, py, (dist < 1.0f) ? c_TargetHi : c_TargetMid);
                    }
                }
            }
            SetP(cx, cy, Color.white); // Glint in center of bullseye!

            // 6. NECK & HEAD (High Zone, Y: 57..76)
            // Neck
            for (int y = 57; y <= 60; y++)
            {
                for (int x = 28; x <= 35; x++)
                {
                    if (x == 28 || x == 35) SetP(x, y, c_Out);
                    else SetP(x, y, (x < 32) ? c_WoodDark : c_WoodMid);
                }
            }
            // Head base
            for (int y = 60; y <= 73; y++)
            {
                int minX = (y >= 71) ? 26 : 24;
                int maxX = (y >= 71) ? 37 : 39;
                for (int x = minX; x <= maxX; x++)
                {
                    if (x == minX || x == maxX || y == 60 || y == 73) SetP(x, y, c_Out);
                    else SetP(x, y, (x < 28) ? c_BurlapDark : (x < 34 ? c_BurlapMid : c_BurlapLight));
                }
            }
            // Cross-stitched dummy face / mouth
            SetP(29, 62, c_Stitch); SetP(30, 62, c_Stitch);
            SetP(32, 62, c_Stitch); SetP(33, 62, c_Stitch);
            SetP(35, 62, c_Stitch); SetP(36, 62, c_Stitch);

            // Iron Training Helmet (Y: 65..75)
            for (int y = 65; y <= 75; y++)
            {
                int minX = (y >= 73) ? 27 : (y >= 70 ? 25 : 23);
                int maxX = (y >= 73) ? 36 : (y >= 70 ? 38 : 40);
                for (int x = minX; x <= maxX; x++)
                {
                    if (x == minX || x == maxX || y == 65 || y == 75) SetP(x, y, c_Out);
                    else
                    {
                        if (y == 67 || y == 68) // Visor slit
                        {
                            if (x >= 27 && x <= 36) SetP(x, y, c_IronDark);
                            else SetP(x, y, c_IronMid);
                        }
                        else
                        {
                            if (x <= 27) SetP(x, y, c_IronDark);
                            else if (x <= 33) SetP(x, y, c_IronMid);
                            else if (x <= 37) SetP(x, y, c_IronLight);
                            else SetP(x, y, c_IronDark);
                        }
                    }
                }
            }
            // Helmet specular shine & rivets
            SetP(34, 72, c_IronHi); SetP(35, 72, c_IronHi);
            SetP(26, 66, c_IronHi); SetP(31, 66, c_IronHi); SetP(37, 66, c_IronHi);

            // Top finial / rope knot at Y: 75..78
            SetP(31, 76, c_Out); SetP(32, 76, c_Out);
            SetP(31, 77, c_TargetMid); SetP(32, 77, c_TargetHi);
            SetP(31, 78, c_Out); SetP(32, 78, c_Out);

            tex.SetPixels32(pixels);
            tex.Apply();

            byte[] pngData = tex.EncodeToPNG();
            string folder = "Assets/Sprites/Dummy";
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            string filePath = $"{folder}/TrainingDummy_PixelArt.png";
            File.WriteAllBytes(filePath, pngData);

            AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(filePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 40f;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }

            Debug.Log($"<color=#00FFAA>[TrainingDummyGenerator]</color> Pixel art манекена успешно сохранен: {filePath}");
        }
    }
}
