using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using LevelGeneration;

namespace LevelGeneration.Editor
{
    /// <summary>
    /// Автоматическая система валидации и починки освещения, теней и тайлов для всех комнат (чанков) уровней.
    /// Предотвращает появление фантомных теней, рассинхронизацию ShadowCaster2D, путаницу между тайлами
    /// фона и коллизии, и автоматически настраивает факелы и освещение во всех префабах комнат.
    /// </summary>
    [InitializeOnLoad]
    public class ChunkLightingAutoValidator : UnityEditor.AssetModificationProcessor
    {
        private static bool _isProcessing = false;
        private static readonly Color TorchWarmColor = new Color(1.0f, 0.72f, 0.35f, 1.0f);
        private const float TorchStandardIntensity = 1.35f;
        private const float TorchOuterRadius = 6.0f;
        private const float TorchInnerRadius = 0.8f;
        private const float TorchShadowIntensity = 0.75f;

        private static readonly HashSet<GameObject> _pendingChunksToUpdate = new HashSet<GameObject>();
        private static bool _updateScheduled = false;

        static ChunkLightingAutoValidator()
        {
            PrefabStage.prefabSaving -= OnPrefabSaving;
            PrefabStage.prefabSaving += OnPrefabSaving;

            Tilemap.tilemapTileChanged -= OnTilemapTileChanged;
            Tilemap.tilemapTileChanged += OnTilemapTileChanged;
        }

        private static void OnTilemapTileChanged(Tilemap tm, Tilemap.SyncTile[] syncTiles)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || tm == null) return;

            GameObject chunkRoot = null;
            Transform t = tm.transform;
            while (t != null)
            {
                if (t.GetComponent<LevelChunk>() != null || t.name.StartsWith("Chunk_") || t.GetComponent<Grid>() != null)
                {
                    chunkRoot = t.gameObject;
                    break;
                }
                t = t.parent;
            }
            if (chunkRoot == null) chunkRoot = tm.gameObject;

            _pendingChunksToUpdate.Add(chunkRoot);

            if (!_updateScheduled)
            {
                _updateScheduled = true;
                EditorApplication.delayCall += DelayedProcessTileChanges;
            }
        }

        private static void DelayedProcessTileChanges()
        {
            _updateScheduled = false;
            if (_pendingChunksToUpdate.Count == 0) return;

            foreach (var chunk in _pendingChunksToUpdate)
            {
                if (chunk != null)
                {
                    ValidateAndFixChunkHierarchy(chunk);
                }
            }
            _pendingChunksToUpdate.Clear();
        }

        private static void OnPrefabSaving(GameObject root)
        {
            if (root == null) return;
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null) return;

            string assetPath = stage.assetPath.Replace('\\', '/');
            if (IsChunkPath(assetPath))
            {
                ValidateAndFixChunkHierarchy(root);
            }
        }

        public static string[] OnWillSaveAssets(string[] paths)
        {
            if (_isProcessing) return paths;

            foreach (string rawPath in paths)
            {
                if (string.IsNullOrEmpty(rawPath)) continue;
                string path = rawPath.Replace('\\', '/');
                if (IsChunkPath(path) && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    var stage = PrefabStageUtility.GetCurrentPrefabStage();
                    if (stage != null && stage.assetPath.Replace('\\', '/').Equals(path, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    EditorApplication.delayCall += () => SafeFixPrefabOnDisk(path);
                }
            }
            return paths;
        }

        private static bool IsChunkPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return path.IndexOf("Assets/Prefabs/LevelChunks", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void SafeFixPrefabOnDisk(string prefabPath)
        {
            if (_isProcessing || string.IsNullOrEmpty(prefabPath)) return;
            _isProcessing = true;
            try
            {
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                if (root != null)
                {
                    bool modified = ValidateAndFixChunkHierarchy(root);
                    if (modified)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                        Debug.Log($"<color=#00FFAA><b>[ChunkValidator]</b></color> Префаб '{System.IO.Path.GetFileName(prefabPath)}' успешно авто-валидирован (тайлы, тени и свет синхронизированы).");
                    }
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ChunkValidator] Ошибка при авто-валидации '{prefabPath}': {ex.Message}");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        [MenuItem("Tools/TestFight/Освещение и тени/Авто-проверка и исправление всех комнат (Validate & Fix All Chunks)", false, 10)]
        public static void FixAllChunkPrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/LevelChunks" });
            int fixedCount = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root != null)
                {
                    try
                    {
                        bool modified = ValidateAndFixChunkHierarchy(root);
                        if (modified)
                        {
                            PrefabUtility.SaveAsPrefabAsset(root, path);
                            fixedCount++;
                        }
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }

            // Синхронизируем тиры в LevelSequenceGenerator
            var gen = UnityEngine.Object.FindAnyObjectByType<LevelSequenceGenerator>();
            if (gen != null)
            {
                gen.SyncTiersFromFolders();
                EditorUtility.SetDirty(gen);
            }

            Debug.Log($"<color=#00FFAA><b>[ChunkValidator]</b></color> Проверка завершена! Авто-исправлено префабов: <b>{fixedCount}</b> из <b>{guids.Length}</b>.");
        }

        [MenuItem("Tools/TestFight/Освещение и тени/Обновить тени и свет в текущей сцене (Update Active Scene Shadows & Lights)", false, 11)]
        public static void FixActiveSceneChunks()
        {
            var chunksRoot = GameObject.Find("--- LEVEL CHUNKS ---");
            int fixedCount = 0;
            if (chunksRoot != null)
            {
                for (int i = 0; i < chunksRoot.transform.childCount; i++)
                {
                    var child = chunksRoot.transform.GetChild(i).gameObject;
                    if (ValidateAndFixChunkHierarchy(child))
                    {
                        fixedCount++;
                        EditorUtility.SetDirty(child);
                    }
                }
            }
            else
            {
                var allChunks = UnityEngine.Object.FindObjectsByType<LevelChunk>(FindObjectsInactive.Include);
                foreach (var c in allChunks)
                {
                    if (ValidateAndFixChunkHierarchy(c.gameObject))
                    {
                        fixedCount++;
                        EditorUtility.SetDirty(c.gameObject);
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"<color=#00FFAA><b>[ChunkValidator]</b></color> Текущая сцена обновлена! Синхронизировано комнат: <b>{fixedCount}</b>.");
        }

        /// <summary>
        /// Главный конвейер валидации геометрии, тайлов, коллизий и света для одного чанка
        /// </summary>
        public static bool ValidateAndFixChunkHierarchy(GameObject chunkRoot)
        {
            if (chunkRoot == null) return false;
            bool modified = false;

            Tilemap solidTm = null;
            Tilemap bgTm = null;

            var allTilemaps = chunkRoot.GetComponentsInChildren<Tilemap>(true);
            foreach (var tm in allTilemaps)
            {
                if (tm.name.IndexOf("Solid", StringComparison.OrdinalIgnoreCase) >= 0) solidTm = tm;
                else if (tm.name.IndexOf("Background", StringComparison.OrdinalIgnoreCase) >= 0) bgTm = tm;
            }

            // 1. Проверка и распределение тайлов (Solid vs Background)
            if (solidTm != null && bgTm != null)
            {
                solidTm.CompressBounds();
                bgTm.CompressBounds();

                // Ищем ошибочные фоновые тайлы на Solid
                foreach (var pos in solidTm.cellBounds.allPositionsWithin)
                {
                    var t = solidTm.GetTile(pos);
                    if (t != null && IsBackgroundTile(t))
                    {
                        solidTm.SetTile(pos, null);
                        if (!bgTm.HasTile(pos))
                        {
                            bgTm.SetTile(pos, t);
                        }
                        bgTm.SetColliderType(pos, Tile.ColliderType.None);
                        modified = true;
                    }
                }

                // Ищем ошибочные солид тайлы на Background
                foreach (var pos in bgTm.cellBounds.allPositionsWithin)
                {
                    var t = bgTm.GetTile(pos);
                    if (t != null && IsStrictSolidTile(t))
                    {
                        bgTm.SetTile(pos, null);
                        solidTm.SetTile(pos, t);
                        modified = true;
                    }
                    else if (t != null && bgTm.GetColliderType(pos) != Tile.ColliderType.None)
                    {
                        bgTm.SetColliderType(pos, Tile.ColliderType.None);
                        modified = true;
                    }
                }
            }

            // 2. Обновление коллизий и теней на Tilemap_Solid
            if (solidTm != null)
            {
                var col = solidTm.GetComponent<TilemapCollider2D>();
                if (col != null) col.ProcessTilemapChanges();

                var comp = solidTm.GetComponent<CompositeCollider2D>();
                if (comp != null)
                {
                    comp.GenerateGeometry();
                }

                var shadowScript = solidTm.GetComponent<TestFight.LevelGeneration.TilemapShadowCaster2D>();
                if (shadowScript == null)
                {
                    shadowScript = solidTm.gameObject.AddComponent<TestFight.LevelGeneration.TilemapShadowCaster2D>();
                    modified = true;
                }

                if (comp != null)
                {
                    Transform container = solidTm.transform.Find("ShadowCasters");
                    if (container == null)
                    {
                        var go = new GameObject("ShadowCasters");
                        go.transform.SetParent(solidTm.transform, false);
                        container = go.transform;
                        modified = true;
                    }

                    if (container.childCount != comp.pathCount)
                    {
                        shadowScript.RebuildShadows();
                        modified = true;
                    }
                }
            }

            // 3. Автоматическая синхронизация источников света для факелов
            if (bgTm != null)
            {
                Transform torchContainer = chunkRoot.transform.Find("Torch_Lights");
                if (torchContainer == null)
                {
                    var go = new GameObject("Torch_Lights");
                    go.transform.SetParent(chunkRoot.transform, false);
                    torchContainer = go.transform;
                    modified = true;
                }

                var torchCellPositions = new List<Vector3Int>();
                bgTm.CompressBounds();
                foreach (var pos in bgTm.cellBounds.allPositionsWithin)
                {
                    var t = bgTm.GetTile(pos);
                    if (t != null && t.name.IndexOf("torch", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        torchCellPositions.Add(pos);
                    }
                }

                var matchedLights = new HashSet<Light2D>();
                foreach (var cellPos in torchCellPositions)
                {
                    Vector3 expectedWorld = bgTm.CellToWorld(cellPos) + new Vector3(0.5f, 0.5f, 0f);
                    Vector3 expectedLocal = chunkRoot.transform.InverseTransformPoint(expectedWorld);

                    Light2D nearestLight = null;
                    float nearestDist = float.MaxValue;

                    for (int i = 0; i < torchContainer.childCount; i++)
                    {
                        var l = torchContainer.GetChild(i).GetComponent<Light2D>();
                        if (l != null && !matchedLights.Contains(l))
                        {
                            float d = Vector2.Distance(l.transform.localPosition, expectedLocal);
                            if (d < nearestDist && d < 1.2f)
                            {
                                nearestDist = d;
                                nearestLight = l;
                            }
                        }
                    }

                    if (nearestLight == null)
                    {
                        var lightGo = new GameObject($"TorchLight_{cellPos.x}_{cellPos.y}");
                        lightGo.transform.SetParent(torchContainer, false);
                        lightGo.transform.localPosition = expectedLocal;

                        var newL = lightGo.AddComponent<Light2D>();
                        SetupStandardTorchLight(newL);
                        matchedLights.Add(newL);
                        modified = true;
                    }
                    else
                    {
                        if (Vector3.Distance(nearestLight.transform.localPosition, expectedLocal) > 0.05f)
                        {
                            nearestLight.transform.localPosition = expectedLocal;
                            modified = true;
                        }

                        if (nearestLight.intensity < 1.0f || !nearestLight.shadowsEnabled || nearestLight.pointLightOuterRadius < 4.0f)
                        {
                            SetupStandardTorchLight(nearestLight);
                            modified = true;
                        }
                        matchedLights.Add(nearestLight);
                    }
                }

                // Удаляем висящие источники света, под которыми нет факелов
                for (int i = torchContainer.childCount - 1; i >= 0; i--)
                {
                    var child = torchContainer.GetChild(i);
                    var l = child.GetComponent<Light2D>();
                    if (l != null && !matchedLights.Contains(l))
                    {
                        bool hasNearbyTorch = false;
                        foreach (var cp in torchCellPositions)
                        {
                            Vector3 el = chunkRoot.transform.InverseTransformPoint(bgTm.CellToWorld(cp) + new Vector3(0.5f, 0.5f, 0f));
                            if (Vector2.Distance(child.localPosition, el) <= 1.5f)
                            {
                                hasNearbyTorch = true;
                                break;
                            }
                        }

                        if (!hasNearbyTorch)
                        {
                            UnityEngine.Object.DestroyImmediate(child.gameObject);
                            modified = true;
                        }
                    }
                }
            }

            return modified;
        }

        private static void SetupStandardTorchLight(Light2D light)
        {
            if (light == null) return;
            light.lightType = Light2D.LightType.Point;
            light.color = TorchWarmColor;
            light.intensity = TorchStandardIntensity;
            light.pointLightInnerRadius = TorchInnerRadius;
            light.pointLightOuterRadius = TorchOuterRadius;
            light.falloffIntensity = 0.5f;
            light.shadowsEnabled = true;
            light.shadowIntensity = TorchShadowIntensity;
            light.shadowVolumeIntensity = TorchShadowIntensity;
        }

        public static bool IsBackgroundTile(TileBase tile)
        {
            if (tile == null) return false;
            string name = tile.name.ToLowerInvariant();
            if (name.StartsWith("floortile") || name.StartsWith("platformtile"))
            {
                return name.Contains("wallright") || name.Contains("corner");
            }
            return name.Contains("wall") || name.Contains("dark") || name.Contains("window") || name.Contains("torch") || name.Contains("bg");
        }

        public static bool IsStrictSolidTile(TileBase tile)
        {
            if (tile == null) return false;
            string name = tile.name.ToLowerInvariant();
            if (name.StartsWith("floortile") || name.StartsWith("platformtile"))
            {
                if (name.Contains("wallright") || name.Contains("corner")) return false;
                return true;
            }
            return false;
        }
    }
}
