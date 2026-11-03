using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace EasterEggs.Editor
{
    [CustomEditor(typeof(OutOfBoundsZone2D))]
    public class OutOfBoundsZone2DEditor : UnityEditor.Editor
    {
        private BoxBoundsHandle _boundsHandle = new BoxBoundsHandle();

        private void OnEnable()
        {
            _boundsHandle.SetColor(new Color(1f, 0.9f, 0.2f, 0.9f));
        }

        private void OnSceneGUI()
        {
            var zone = (OutOfBoundsZone2D)target;
            if (zone == null) return;
            var box = zone.BoxCollider;
            if (box == null) return;

            Transform t = zone.transform;

            // 1. Отслеживание перемещения стрелками Move Tool (клавиша W)
            if (t.hasChanged)
            {
                t.hasChanged = false;
                EditorUtility.SetDirty(zone.gameObject);
                SyncZoneWithPrefabAndGenerator(zone);
            }

            // 2. Интерактивные жёлтые ручки изменения размера в окне Scene
            Matrix4x4 handleMatrix = Matrix4x4.TRS(t.position, t.rotation, t.lossyScale);

            using (new Handles.DrawingScope(handleMatrix))
            {
                _boundsHandle.center = box.offset;
                _boundsHandle.size = box.size;

                EditorGUI.BeginChangeCheck();
                _boundsHandle.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(box, "Resize OutOfBounds Zone");
                    box.size = _boundsHandle.size;
                    box.offset = _boundsHandle.center;
                    EditorUtility.SetDirty(zone.gameObject);
                    SyncZoneWithPrefabAndGenerator(zone);
                }
            }
        }

        public override void OnInspectorGUI()
        {
            var zone = (OutOfBoundsZone2D)target;
            var box = zone != null ? zone.BoxCollider : null;

            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("Зона выхода за пределы карты для Роберта (Out of Bounds).\n" +
                "• Перемещайте зону по уровню стрелками Move Tool (клавиша W).\n" +
                "• Растягивайте границы прямо в окне Scene за жёлтые маркеры.\n" +
                "• Любые изменения АВТОМАТИЧЕСКИ сохраняются в префаб и не сбрасываются при Play!", MessageType.Info);

            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck())
            {
                SyncZoneWithPrefabAndGenerator(zone);
            }

            EditorGUILayout.Space(8);
            if (GUILayout.Button("🎯 Сфокусировать окно Scene на этой зоне", GUILayout.Height(30)))
            {
                if (SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.FrameSelected();
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Размер: 6 × 4 м"))
            {
                if (box != null)
                {
                    Undo.RecordObject(box, "Set Default Size");
                    box.size = new Vector2(6f, 4f);
                    box.offset = Vector2.zero;
                    EditorUtility.SetDirty(zone.gameObject);
                    SyncZoneWithPrefabAndGenerator(zone);
                }
            }
            if (GUILayout.Button("Размер: 10 × 6 м"))
            {
                if (box != null)
                {
                    Undo.RecordObject(box, "Set Large Size");
                    box.size = new Vector2(10f, 6f);
                    box.offset = Vector2.zero;
                    EditorUtility.SetDirty(zone.gameObject);
                    SyncZoneWithPrefabAndGenerator(zone);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.4f, 1f);
            if (GUILayout.Button("💾 Синхронизировать с префабом и генератором сейчас", GUILayout.Height(32)))
            {
                SyncZoneWithPrefabAndGenerator(zone);
            }
            GUI.backgroundColor = Color.white;
        }

        public static void SyncZoneWithPrefabAndGenerator(OutOfBoundsZone2D zone)
        {
            if (zone == null) return;
            var box = zone.BoxCollider;
            if (box == null) return;

            Transform parentChunk = zone.transform.parent;
            if (parentChunk == null) return;

            Vector3 localPos = zone.transform.localPosition;
            Vector2 size = box.size;
            Vector2 offset = box.offset;

            // 1. Обновляем параметры в генераторе уровней
            var gen = Object.FindAnyObjectByType<LevelGeneration.LevelSequenceGenerator>();
            if (gen != null)
            {
                if (parentChunk.name.Contains("Start"))
                {
                    gen.StartHitboxLocalPos = localPos;
                    gen.StartHitboxSize = size;
                    gen.StartHitboxOffset = offset;
                    EditorUtility.SetDirty(gen);
                }
                else if (parentChunk.name.Contains("End"))
                {
                    gen.EndHitboxLocalPos = localPos;
                    gen.EndHitboxSize = size;
                    gen.EndHitboxOffset = offset;
                    EditorUtility.SetDirty(gen);
                }
            }

            // 2. Обновляем префаб чанка на диске
            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(parentChunk.gameObject);
            if (string.IsNullOrEmpty(prefabPath))
            {
                if (parentChunk.name.Contains("Start"))
                    prefabPath = "Assets/Prefabs/LevelChunks/Starn and end/Chunk_Start_Intro.prefab";
                else if (parentChunk.name.Contains("End"))
                    prefabPath = "Assets/Prefabs/LevelChunks/Starn and end/Chunk_End_Outro.prefab";
            }

            ApplyHitboxToPrefab(prefabPath, zone.name, localPos, size, offset);

            // Также синхронизируем альтернативный путь в каталоге
            string altPath = prefabPath.Contains("Starn and end/")
                ? prefabPath.Replace("Starn and end/", "")
                : prefabPath.Replace("LevelChunks/", "LevelChunks/Starn and end/");
            if (altPath != prefabPath)
            {
                ApplyHitboxToPrefab(altPath, zone.name, localPos, size, offset);
            }
        }

        private static void ApplyHitboxToPrefab(string path, string hitboxName, Vector3 localPos, Vector2 size, Vector2 offset)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return;

            try
            {
                using (var scope = new PrefabUtility.EditPrefabContentsScope(path))
                {
                    var root = scope.prefabContentsRoot;
                    var hb = root.transform.Find(hitboxName);
                    if (hb == null)
                    {
                        var go = new GameObject(hitboxName);
                        go.transform.SetParent(root.transform, false);
                        hb = go.transform;
                    }
                    hb.localPosition = localPos;
                    var b = hb.GetComponent<BoxCollider2D>();
                    if (b == null) b = hb.gameObject.AddComponent<BoxCollider2D>();
                    b.isTrigger = true;
                    b.size = size;
                    b.offset = offset;
                    if (hb.GetComponent<OutOfBoundsZone2D>() == null)
                    {
                        hb.gameObject.AddComponent<OutOfBoundsZone2D>();
                    }
                }
                AssetDatabase.SaveAssets();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[OutOfBoundsZone2D] Не удалось обновить префаб {path}: {ex.Message}");
            }
        }
    }
}
