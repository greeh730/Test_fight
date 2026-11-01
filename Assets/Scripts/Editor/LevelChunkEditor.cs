using System;
using UnityEngine;
using UnityEditor;
using LevelGeneration;

namespace TestFight.Editor
{
    [CustomEditor(typeof(LevelChunk))]
    [CanEditMultipleObjects]
    public class LevelChunkEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var chunk = (LevelChunk)target;

            // Стильная шапка
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            float length = chunk.Length;
            float deltaY = chunk.ExitPoint.position.y - chunk.EntryPoint.position.y;
            string slopeText = Mathf.Abs(deltaY) < 0.05f 
                ? "<color=#55FF88>Плоская арена (Y = 0)</color>" 
                : (deltaY > 0 ? $"<color=#33CCFF>Подъем (▲ +{deltaY:F1}м)</color>" : $"<color=#FF9933>Спуск (▼ {deltaY:F1}м)</color>");

            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { richText = true, fontSize = 13 };
            EditorGUILayout.LabelField($"Чанк: <b>{chunk.name}</b>", titleStyle);

            var infoStyle = new GUIStyle(EditorStyles.miniLabel) { richText = true };
            EditorGUILayout.LabelField($"Длина: <b>{length:F1}м</b> | Форма: {slopeText}", infoStyle);
            EditorGUILayout.LabelField($"Точек спавна врагов: <b>{chunk.EnemySpawnPoints.Count}</b>", infoStyle);
            EditorGUILayout.EndVertical();

            // Валидация
            DrawValidationBox(chunk);

            // Кнопки быстрых действий
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Быстрые действия:", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.3f, 0.85f, 1f);
            if (GUILayout.Button("📐 Подогнать сокет выхода\n(Auto-Fit Exit to Tiles)", GUILayout.Height(36)))
            {
                Undo.RecordObject(chunk, "Auto-Fit Exit Socket");
                chunk.AutoFitExitSocketToTilemap();
                EditorUtility.SetDirty(chunk);
            }

            GUI.backgroundColor = new Color(0.4f, 1f, 0.5f);
            if (GUILayout.Button("🔄 Собрать спавны\n(Scan Children)", GUILayout.Height(36)))
            {
                Undo.RecordObject(chunk, "Scan Spawn Points");
                chunk.CollectAllSpawnPoints();
                EditorUtility.SetDirty(chunk);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("➕ Спавн врага"))
            {
                AddSpawnPoint(chunk);
            }

            if (GUILayout.Button("➕ Триггер"))
            {
                AddTriggerZone(chunk);
            }

            if (GUILayout.Button("➕ Ловушка"))
            {
                AddHazard(chunk);
            }

            GUI.backgroundColor = new Color(0.95f, 0.75f, 0.4f);
            if (GUILayout.Button("🛢️ Бочка с зельем"))
            {
                AddBarrel(chunk);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            DrawDefaultInspector();

            EditorGUILayout.Space(6);
            if (PrefabUtility.IsPartOfPrefabInstance(chunk))
            {
                GUI.backgroundColor = new Color(0.5f, 1f, 0.7f);
                if (GUILayout.Button("💾 Применить изменения в префаб (Apply to Prefab)", GUILayout.Height(28)))
                {
                    PrefabUtility.ApplyPrefabInstance(chunk.gameObject, InteractionMode.UserAction);
                    Debug.Log($"<color=#00FFAA><b>[LevelChunk]</b></color> Изменения чанка '{chunk.name}' сохранены в префаб!");
                }
                GUI.backgroundColor = Color.white;
            }
        }

        private void DrawValidationBox(LevelChunk chunk)
        {
            bool hasEntry = chunk.EntryPoint != null && chunk.EntryPoint != chunk.transform;
            bool hasExit = chunk.ExitPoint != null && chunk.ExitPoint != chunk.transform;
            bool hasSpawns = chunk.EnemySpawnPoints != null && chunk.EnemySpawnPoints.Count > 0;

            if (hasEntry && hasExit && hasSpawns)
            {
                EditorGUILayout.HelpBox("✅ Чанк настроен корректно и готов к генерации!", MessageType.Info);
            }
            else
            {
                string warn = "Внимание по настройке чанка:";
                if (!hasEntry) warn += "\n• Не назначен Socket_Entry (точка входа)";
                if (!hasExit) warn += "\n• Не назначен Socket_Exit (точка выхода)";
                if (!hasSpawns) warn += "\n• Нет точек спавна врагов (добавьте хотя бы одну)";
                EditorGUILayout.HelpBox(warn, MessageType.Warning);
            }
        }

        private void AddSpawnPoint(LevelChunk chunk)
        {
            Vector3 mid = (chunk.EntryPoint.position + chunk.ExitPoint.position) * 0.5f + Vector3.up * 0.5f;
            var go = new GameObject($"EnemySpawnPoint_{chunk.EnemySpawnPoints.Count + 1}");
            go.transform.SetParent(chunk.transform);
            go.transform.position = mid;
            var sp = go.AddComponent<ArenaEnemySpawnPoint2D>();
            sp.SnapToFloor();

            chunk.CollectAllSpawnPoints();
            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create Enemy Spawn Point");
        }

        private void AddTriggerZone(LevelChunk chunk)
        {
            Vector3 mid = (chunk.EntryPoint.position + chunk.ExitPoint.position) * 0.5f + Vector3.up * 2f;
            var go = new GameObject("ArenaTriggerZone");
            go.transform.SetParent(chunk.transform);
            go.transform.position = mid;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(4f, 4f);

            go.AddComponent<ArenaTriggerZone2D>();
            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create Arena Trigger Zone");
        }

        private void AddHazard(LevelChunk chunk)
        {
            Vector3 mid = (chunk.EntryPoint.position + chunk.ExitPoint.position) * 0.5f;
            var go = new GameObject("ArenaHazard_Spikes");
            go.transform.SetParent(chunk.transform);
            go.transform.position = mid;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(2f, 0.5f);

            go.AddComponent<ArenaHazard2D>();
            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create Arena Hazard");
        }

        private void AddBarrel(LevelChunk chunk)
        {
            Vector3 mid = (chunk.EntryPoint.position + chunk.ExitPoint.position) * 0.5f;
            var hit = Physics2D.Raycast(mid + Vector3.up * 4f, Vector2.down, 10f);
            Vector3 spawnPos = hit.collider != null ? (Vector3)hit.point + Vector3.up * 0.45f : mid + Vector3.up * 0.45f;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/DestructibleBarrel.prefab");
            GameObject go;
            if (prefab != null)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, chunk.transform);
                go.transform.position = spawnPos;
            }
            else
            {
                go = new GameObject("DestructibleBarrel");
                go.transform.SetParent(chunk.transform);
                go.transform.position = spawnPos;
                go.AddComponent<Combat.Props.DestructibleBarrel>();
            }

            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create Destructible Barrel");
        }

        private void OnSceneGUI()
        {
            var chunk = (LevelChunk)target;
            if (chunk == null) return;

            // Интерактивный хэндл для точки входа (Socket_Entry)
            if (chunk.EntryPoint != null && chunk.EntryPoint != chunk.transform)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newEntry = Handles.PositionHandle(chunk.EntryPoint.position, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(chunk.EntryPoint, "Move Entry Socket");
                    chunk.EntryPoint.position = newEntry;
                }
                Handles.color = Color.green;
                Handles.Label(chunk.EntryPoint.position + Vector3.up * 0.5f, "◄ ВХОД (Socket_Entry)", EditorStyles.boldLabel);
            }

            // Интерактивный хэндл для точки выхода (Socket_Exit)
            if (chunk.ExitPoint != null && chunk.ExitPoint != chunk.transform)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newExit = Handles.PositionHandle(chunk.ExitPoint.position, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(chunk.ExitPoint, "Move Exit Socket");
                    chunk.ExitPoint.position = newExit;
                }
                Handles.color = Color.red;
                Handles.Label(chunk.ExitPoint.position + Vector3.up * 0.5f, "ВЫХОД (Socket_Exit) ►", EditorStyles.boldLabel);
            }
        }
    }
}
