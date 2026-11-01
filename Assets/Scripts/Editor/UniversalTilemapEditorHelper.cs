using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using LevelGeneration;

namespace TestFight.Editor
{
    /// <summary>
    /// Универсальный помощник редактирования тайлов в редакторе Unity.
    /// Поддерживает как сцену (Scene View), так и режим редактирования префабов (Prefab Mode):
    /// 1. При использовании ластика (Erase) автоматически определяет, на каком слое (Solid или Background)
    ///    находится тайл под курсором мыши, и мгновенно переключает активный таргет.
    ///    В результате ластик стирает ЛЮБОЙ тайл фона или стен с одного клика!
    /// 2. Корректно работает в Prefab Mode (Second_Level, First_Level и т.д.) без привязки к сцене.
    /// 3. Показывает удобную плавающую панель переключения слоев прямо в Scene View.
    /// 4. Горячие клавиши: [1] - Solid (пол/стены), [2] - Background (фон), [Tab] - смена слоя.
    /// </summary>
    [InitializeOnLoad]
    public static class UniversalTilemapEditorHelper
    {
        private static bool _autoSwitchEnabled = true;
        private static bool _showToolbar = true;
        private static Tilemap _lastAutoTarget;
        private static double _lastSwitchTime = 0;
        private static bool _manualLayerOverride = false;
        private static TileBase _lastBrushTile = null;

        static UniversalTilemapEditorHelper()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;
        }

        public static TileBase GetActiveBrushTile()
        {
            var brush = GridPaintingState.gridBrush;
            if (brush == null) return null;
            var cellsProp = brush.GetType().GetProperty("cells");
            var cells = cellsProp?.GetValue(brush) as Array;
            if (cells == null || cells.Length == 0) return null;

            var firstCell = cells.GetValue(0);
            if (firstCell == null) return null;
            var tileProp = firstCell.GetType().GetProperty("tile") ?? (System.Reflection.MemberInfo)firstCell.GetType().GetField("tile");
            if (tileProp is System.Reflection.PropertyInfo pi) return pi.GetValue(firstCell) as TileBase;
            if (tileProp is System.Reflection.FieldInfo fi) return fi.GetValue(firstCell) as TileBase;
            return null;
        }

        public static bool IsBackgroundTile(TileBase tile)
        {
            if (tile == null) return false;
            if (tile is Tile unityTile && unityTile.colliderType == Tile.ColliderType.None) return true;
            string name = tile.name.ToLowerInvariant();
            if (name.StartsWith("floortile") || name.StartsWith("platformtile")) return false;
            return name.Contains("walltile") || name.Contains("dark") || name.Contains("window") || name.Contains("torch") || name.Contains("bg") || name.Contains("background");
        }

        private static void OnSelectionChanged()
        {
            var active = Selection.activeGameObject;
            if (active == null) return;

            var tm = active.GetComponent<Tilemap>();
            if (tm != null)
            {
                SetPaintTarget(tm);
            }
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            Event e = Event.current;

            // Горячие клавиши: Tab для переключения слоя, 1 для Solid, 2 для Background
            if (e.type == EventType.KeyDown && !e.alt && !e.control && !e.command)
            {
                if (e.keyCode == KeyCode.Tab)
                {
                    ToggleLayer();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Alpha1)
                {
                    _manualLayerOverride = true;
                    SelectLayer("Tilemap_Solid");
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Alpha2)
                {
                    _manualLayerOverride = true;
                    SelectLayer("Tilemap_Background");
                    e.Use();
                }
            }

            // Автоматическое определение тайлмапа под курсором мыши
            if (_autoSwitchEnabled && (e.type == EventType.MouseMove || e.type == EventType.MouseDrag || e.type == EventType.MouseDown))
            {
                HandleAutoSwitching(e);
            }

            // Отрисовка плавающего тулбара в Scene View
            DrawSceneViewToolbar(sceneView);
        }

        private static List<Tilemap> GetAllActiveTilemaps()
        {
            var list = new List<Tilemap>();

            // Если открыт режим редактирования префаба (Prefab Mode)
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.prefabContentsRoot != null)
            {
                var stageTilemaps = prefabStage.prefabContentsRoot.GetComponentsInChildren<Tilemap>(true);
                for (int i = 0; i < stageTilemaps.Length; i++)
                {
                    var tm = stageTilemaps[i];
                    if (tm != null && tm.gameObject.activeInHierarchy && tm.enabled)
                    {
                        list.Add(tm);
                    }
                }
                return list;
            }

            // В обычном режиме сцены
            var all = UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (all != null)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    var tm = all[i];
                    if (tm != null && tm.gameObject.activeInHierarchy && tm.enabled)
                    {
                        list.Add(tm);
                    }
                }
            }

            return list;
        }

        private static void HandleAutoSwitching(Event e)
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - _lastSwitchTime < 0.04) return;

            // Проецируем луч на плоскость Z = 0
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Plane zZeroPlane = new Plane(Vector3.forward, Vector3.zero);
            Vector3 worldPos = ray.origin;
            if (zZeroPlane.Raycast(ray, out float enter))
            {
                worldPos = ray.GetPoint(enter);
            }

            Type activeTool = ToolManager.activeToolType;
            bool isEraser = activeTool != null && activeTool.Name.IndexOf("Erase", StringComparison.OrdinalIgnoreCase) >= 0;

            var allTilemaps = GetAllActiveTilemaps();
            if (allTilemaps == null || allTilemaps.Count == 0) return;

            List<Tilemap> candidateSolid = new List<Tilemap>();
            List<Tilemap> candidateBg = new List<Tilemap>();

            for (int i = 0; i < allTilemaps.Count; i++)
            {
                var tm = allTilemaps[i];
                if (tm.name.IndexOf("Solid", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    candidateSolid.Add(tm);
                }
                else if (tm.name.IndexOf("Background", StringComparison.OrdinalIgnoreCase) >= 0 || tm.name.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    candidateBg.Add(tm);
                }
            }

            Tilemap bestTarget = null;

            if (isEraser)
            {
                _manualLayerOverride = false;

                // При стирании: проверяем, на каком слое есть тайл под курсором
                // 1. Сначала проверяем Solid
                for (int i = 0; i < candidateSolid.Count; i++)
                {
                    var tm = candidateSolid[i];
                    Vector3Int cell = tm.WorldToCell(worldPos);
                    if (tm.HasTile(cell))
                    {
                        bestTarget = tm;
                        break;
                    }
                }

                // 2. Если на Solid тайла нет - проверяем Background
                if (bestTarget == null)
                {
                    for (int i = 0; i < candidateBg.Count; i++)
                    {
                        var tm = candidateBg[i];
                        Vector3Int cell = tm.WorldToCell(worldPos);
                        if (tm.HasTile(cell))
                        {
                            bestTarget = tm;
                            break;
                        }
                    }
                }
            }
            else
            {
                var activeTile = GetActiveBrushTile();
                if (activeTile != _lastBrushTile)
                {
                    _lastBrushTile = activeTile;
                    _manualLayerOverride = false;
                }

                bool preferBackground;
                if (_manualLayerOverride)
                {
                    var currentTarget = GridPaintingState.scenePaintTarget;
                    preferBackground = currentTarget != null && currentTarget.name.IndexOf("Background", StringComparison.OrdinalIgnoreCase) >= 0;
                }
                else
                {
                    preferBackground = IsBackgroundTile(activeTile);
                }

                Tilemap solidForChunk = FindTilemapForPosition(candidateSolid, worldPos.x);
                Tilemap bgForChunk = FindTilemapForPosition(candidateBg, worldPos.x);

                bestTarget = preferBackground ? (bgForChunk ?? solidForChunk) : (solidForChunk ?? bgForChunk);
            }

            if (bestTarget != null && bestTarget != _lastAutoTarget)
            {
                _lastAutoTarget = bestTarget;
                _lastSwitchTime = now;
                SetPaintTarget(bestTarget);
            }
        }

        private static Tilemap FindTilemapForPosition(List<Tilemap> list, float worldX)
        {
            if (list == null || list.Count == 0) return null;
            if (list.Count == 1) return list[0];

            Tilemap closest = null;
            float minDist = float.MaxValue;

            for (int i = 0; i < list.Count; i++)
            {
                var tm = list[i];
                float chunkCenterX = tm.transform.position.x + 10f;
                float dist = Mathf.Abs(worldX - chunkCenterX);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = tm;
                }
            }

            return closest;
        }

        public static void SetPaintTarget(Tilemap tm)
        {
            if (tm == null) return;
            if (GridPaintingState.scenePaintTarget != tm.gameObject)
            {
                GridPaintingState.scenePaintTarget = tm.gameObject;
            }
        }

        private static void ToggleLayer()
        {
            var cur = GridPaintingState.scenePaintTarget;
            bool isSolid = cur != null && cur.name.IndexOf("Solid", StringComparison.OrdinalIgnoreCase) >= 0;
            SelectLayer(isSolid ? "Tilemap_Background" : "Tilemap_Solid");
        }

        public static void SelectLayer(string layerName)
        {
            // 1. Проверяем Prefab Mode
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.prefabContentsRoot != null)
            {
                var foundInPrefab = prefabStage.prefabContentsRoot.transform.Find(layerName)?.GetComponent<Tilemap>();
                if (foundInPrefab == null)
                {
                    var allInStage = prefabStage.prefabContentsRoot.GetComponentsInChildren<Tilemap>(true);
                    foreach (var t in allInStage)
                    {
                        if (t.name.IndexOf(layerName.Replace("Tilemap_", ""), StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            foundInPrefab = t;
                            break;
                        }
                    }
                }

                if (foundInPrefab != null)
                {
                    SetPaintTarget(foundInPrefab);
                    return;
                }
            }

            // 2. Проверяем соседей текущего выбранного объекта
            var cur = GridPaintingState.scenePaintTarget;
            Transform parent = cur != null ? cur.transform.parent : null;

            if (parent != null)
            {
                var sibling = parent.Find(layerName);
                if (sibling != null)
                {
                    var tm = sibling.GetComponent<Tilemap>();
                    if (tm != null)
                    {
                        SetPaintTarget(tm);
                        return;
                    }
                }
            }

            // 3. Ищем среди всех активных тайлмапов
            var all = GetAllActiveTilemaps();
            string searchWord = layerName.Replace("Tilemap_", "");
            for (int i = 0; i < all.Count; i++)
            {
                var tm = all[i];
                if (tm.name.IndexOf(searchWord, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    SetPaintTarget(tm);
                    return;
                }
            }
        }

        public static void SelectChunk(string chunkSearch)
        {
            var gen = GameObject.Find("[Generated_Level_Chunks]");
            if (gen == null) return;

            Transform targetChunk = null;
            for (int i = 0; i < gen.transform.childCount; i++)
            {
                var ch = gen.transform.GetChild(i);
                if (ch.name.IndexOf(chunkSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    targetChunk = ch;
                    break;
                }
            }

            if (targetChunk != null)
            {
                var cur = GridPaintingState.scenePaintTarget;
                bool isBg = cur != null && cur.name.IndexOf("Background", StringComparison.OrdinalIgnoreCase) >= 0;
                string layer = isBg ? "Tilemap_Background" : "Tilemap_Solid";
                var tm = targetChunk.Find(layer)?.GetComponent<Tilemap>() ?? targetChunk.GetComponentInChildren<Tilemap>();
                if (tm != null) SetPaintTarget(tm);
            }
        }

        private static void DrawSceneViewToolbar(SceneView sceneView)
        {
            if (!_showToolbar) return;

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            bool isPrefabMode = prefabStage != null && prefabStage.prefabContentsRoot != null;

            Handles.BeginGUI();

            var current = GridPaintingState.scenePaintTarget;
            string chunkName = isPrefabMode
                ? prefabStage.prefabContentsRoot.name
                : (current != null && current.transform.parent != null ? current.transform.parent.name : "");

            bool isSolid = current != null && current.name.IndexOf("Solid", StringComparison.OrdinalIgnoreCase) >= 0;

            float panelWidth = 380f;
            float panelHeight = isPrefabMode ? 122f : 134f;
            Rect panelRect = new Rect(12, 12, panelWidth, panelHeight);

            // Полупрозрачный фон панели
            EditorGUI.DrawRect(panelRect, new Color(0.10f, 0.12f, 0.16f, 0.94f));
            GUI.Box(panelRect, GUIContent.none, EditorStyles.helpBox);

            GUILayout.BeginArea(new Rect(panelRect.x + 8, panelRect.y + 6, panelWidth - 16, panelHeight - 12));

            // Строка 1: Индикатор активного слоя и тумблер Авто
            GUILayout.BeginHorizontal();
            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniBoldLabel) { richText = true };
            string layerIcon = isSolid ? "<color=#FFCC00>⬛ Solid (Пол/Стены)</color>" : "<color=#55CCFF>🧱 Background (Фон)</color>";
            GUILayout.Label($"Слой: {layerIcon}", labelStyle);

            GUI.backgroundColor = _autoSwitchEnabled ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button(_autoSwitchEnabled ? "Авто: ВКЛ" : "Авто: ВЫКЛ", EditorStyles.miniButton, GUILayout.Width(76)))
            {
                _autoSwitchEnabled = !_autoSwitchEnabled;
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            // Строка 1.5: Индикатор выбранного тайла в палитре
            var activeBrushTile = GetActiveBrushTile();
            if (activeBrushTile != null)
            {
                GUILayout.BeginHorizontal();
                bool tileIsBg = IsBackgroundTile(activeBrushTile);
                string cat = tileIsBg ? "<color=#55CCFF>🧱 ФОН</color>" : "<color=#FFCC00>⬛ SOLID</color>";
                var tileLabelStyle = new GUIStyle(EditorStyles.miniLabel) { richText = true };
                GUILayout.Label($"Кисть: <b>{activeBrushTile.name}</b> ({cat})", tileLabelStyle);
                GUILayout.EndHorizontal();
            }

            // Строка 2: Индикатор текущего префаба или выбор чанка на сцене
            if (isPrefabMode)
            {
                GUILayout.BeginHorizontal();
                var prefabLabelStyle = new GUIStyle(EditorStyles.miniLabel) { richText = true };
                GUILayout.Label($"📦 Режим префаба: <b><color=#00FFAA>{chunkName}</color></b>", prefabLabelStyle);
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginHorizontal();
                bool isIntro = chunkName.IndexOf("Start", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isArena = chunkName.IndexOf("First_Level", StringComparison.OrdinalIgnoreCase) >= 0 || chunkName.IndexOf("Tier", StringComparison.OrdinalIgnoreCase) >= 0 || chunkName.IndexOf("Second", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isOutro = chunkName.IndexOf("End", StringComparison.OrdinalIgnoreCase) >= 0;

                GUI.backgroundColor = isIntro ? new Color(0.4f, 1f, 0.6f) : Color.white;
                if (GUILayout.Button("◀ Старт", EditorStyles.miniButtonLeft)) SelectChunk("Start");

                GUI.backgroundColor = isArena ? new Color(0.4f, 1f, 0.6f) : Color.white;
                if (GUILayout.Button("★ Арена", EditorStyles.miniButtonMid)) SelectChunk("Level");

                GUI.backgroundColor = isOutro ? new Color(0.4f, 1f, 0.6f) : Color.white;
                if (GUILayout.Button("▶ Выход", EditorStyles.miniButtonRight)) SelectChunk("End");

                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();
            }

            // Строка 3: Быстрый выбор слоя
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = isSolid ? new Color(1f, 0.85f, 0.3f) : Color.white;
            if (GUILayout.Button("[1] Пол / Стены (Solid)", EditorStyles.miniButtonLeft))
            {
                _manualLayerOverride = true;
                SelectLayer("Tilemap_Solid");
            }

            GUI.backgroundColor = !isSolid ? new Color(0.4f, 0.85f, 1f) : Color.white;
            if (GUILayout.Button("[2] Задний фон (Background)", EditorStyles.miniButtonRight))
            {
                _manualLayerOverride = true;
                SelectLayer("Tilemap_Background");
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            // Строка 4: Кнопки сохранения и авто-сортировки
            GUILayout.BeginHorizontal();
            if (isPrefabMode)
            {
                if (GUILayout.Button("💾 Сохранить", EditorStyles.miniButtonLeft))
                {
                    SaveCurrentPrefabStage(prefabStage);
                }
            }
            else
            {
                if (GUILayout.Button("💾 Сохранить в префабы", EditorStyles.miniButtonLeft))
                {
                    SaveAllChunksToPrefabs();
                }
            }

            GUI.backgroundColor = new Color(0.4f, 0.9f, 1f);
            if (GUILayout.Button("🔧 Исправить тайлы", EditorStyles.miniButtonRight))
            {
                CleanAndDistributeTiles();
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.EndArea();

            Handles.EndGUI();
        }

        private static void SaveCurrentPrefabStage(PrefabStage prefabStage)
        {
            if (prefabStage == null || prefabStage.prefabContentsRoot == null) return;
            global::LevelGeneration.Editor.ChunkLightingAutoValidator.ValidateAndFixChunkHierarchy(prefabStage.prefabContentsRoot);

            PrefabUtility.SaveAsPrefabAsset(prefabStage.prefabContentsRoot, prefabStage.prefabAssetPath);
            Debug.Log($"<color=#00FFAA><b>[TilemapEditor]</b></color> Префаб '{prefabStage.prefabContentsRoot.name}' успешно сохранен (тайлы, тени и свет обновлены)!");
        }

        [MenuItem("Tools/TestFight/Очистить и распределить тайлы чанков (Auto-Fix Misplaced Tiles)")]
        public static void CleanAndDistributeTiles()
        {
            var allTilemaps = GetAllActiveTilemaps();
            int fixedCount = 0;

            var solidMaps = new List<Tilemap>();
            var bgMaps = new List<Tilemap>();

            foreach (var tm in allTilemaps)
            {
                if (tm.name.IndexOf("Solid", StringComparison.OrdinalIgnoreCase) >= 0) solidMaps.Add(tm);
                else if (tm.name.IndexOf("Background", StringComparison.OrdinalIgnoreCase) >= 0) bgMaps.Add(tm);
            }

            foreach (var solid in solidMaps)
            {
                Tilemap bg = null;
                Transform parent = solid.transform.parent;
                if (parent != null)
                {
                    bg = parent.Find("Tilemap_Background")?.GetComponent<Tilemap>();
                }
                if (bg == null && bgMaps.Count > 0) bg = bgMaps[0];
                if (bg == null) continue;

                solid.CompressBounds();
                var bounds = solid.cellBounds;
                bool changed = false;

                foreach (var pos in bounds.allPositionsWithin)
                {
                    var tile = solid.GetTile(pos);
                    if (tile != null && IsBackgroundTile(tile))
                    {
                        Undo.RecordObjects(new UnityEngine.Object[] { solid, bg }, "Move Tile to Background");
                        solid.SetTile(pos, null);
                        bg.SetTile(pos, tile);
                        fixedCount++;
                        changed = true;
                    }
                }

                bg.CompressBounds();
                foreach (var pos in bg.cellBounds.allPositionsWithin)
                {
                    var tile = bg.GetTile(pos);
                    if (tile != null && !IsBackgroundTile(tile))
                    {
                        Undo.RecordObjects(new UnityEngine.Object[] { solid, bg }, "Move Tile to Solid");
                        bg.SetTile(pos, null);
                        solid.SetTile(pos, tile);
                        fixedCount++;
                        changed = true;
                    }
                }

                if (changed)
                {
                    var collider = solid.GetComponent<TilemapCollider2D>();
                    if (collider != null) collider.ProcessTilemapChanges();
                    var comp = solid.GetComponent<CompositeCollider2D>();
                    if (comp != null) comp.GenerateGeometry();
                    var shadow = solid.GetComponent<TestFight.LevelGeneration.TilemapShadowCaster2D>();
                    if (shadow != null) shadow.RebuildShadows();

                    EditorUtility.SetDirty(solid);
                    EditorUtility.SetDirty(bg);
                }
            }

            if (fixedCount > 0)
            {
                Debug.Log($"<color=#00FFAA><b>[TilemapEditor]</b></color> Готово! Автоматически перемещено {fixedCount} неверно расположенных тайлов.");
            }
        }

        [MenuItem("Tools/TestFight/Сохранить все чанки в префабы (Apply Prefab Changes)")]
        public static void SaveAllChunksToPrefabs()
        {
            var gen = GameObject.Find("[Generated_Level_Chunks]");
            if (gen == null)
            {
                Debug.LogWarning("[TilemapEditor] Объект [Generated_Level_Chunks] не найден в сцене.");
                return;
            }

            CleanAndDistributeTiles();

            int count = 0;
            for (int i = 0; i < gen.transform.childCount; i++)
            {
                var chunk = gen.transform.GetChild(i).gameObject;
                if (PrefabUtility.IsPartOfPrefabInstance(chunk))
                {
                    global::LevelGeneration.Editor.ChunkLightingAutoValidator.ValidateAndFixChunkHierarchy(chunk);

                    PrefabUtility.ApplyPrefabInstance(chunk, InteractionMode.AutomatedAction);
                    count++;
                }
            }

            var lsg = UnityEngine.Object.FindAnyObjectByType<LevelSequenceGenerator>();
            if (lsg != null)
            {
                lsg.CaptureDoorOffsetsFromScene();
                EditorUtility.SetDirty(lsg);
            }

            Debug.Log($"<color=#00FFAA><b>[TilemapEditor]</b></color> Успешно применены изменения для {count} чанков в префабы на диске, а также сохранены позиции решёток.");
        }
    }
}
