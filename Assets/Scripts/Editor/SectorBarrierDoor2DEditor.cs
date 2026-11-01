using UnityEngine;
using UnityEditor;
using LevelGeneration;

namespace TestFight.Editor
{
    [CustomEditor(typeof(SectorBarrierDoor2D))]
    [CanEditMultipleObjects]
    public class SectorBarrierDoor2DEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var door = (SectorBarrierDoor2D)target;

            // Стильная шапка
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            string stateText = door.IsClosed ? "<color=#FF5555>● ЗАКРЫТА</color>" : "<color=#55FF88>○ ОТКРЫТА (Поднята)</color>";
            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { richText = true, fontSize = 13 };
            EditorGUILayout.LabelField($"Решётка подземелья: {stateText}", titleStyle);
            EditorGUILayout.EndVertical();

            // Кнопки быстрого тестирования
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = door.IsClosed ? new Color(0.4f, 1f, 0.4f) : Color.white;
            if (GUILayout.Button("▶ Открыть (Тест)", GUILayout.Height(28)))
            {
                door.OpenDoor(instant: !Application.isPlaying);
                EditorUtility.SetDirty(door);
            }

            GUI.backgroundColor = !door.IsClosed ? new Color(1f, 0.4f, 0.4f) : Color.white;
            if (GUILayout.Button("▼ Закрыть (Тест)", GUILayout.Height(28)))
            {
                door.CloseDoor(instant: !Application.isPlaying);
                EditorUtility.SetDirty(door);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            GUI.backgroundColor = new Color(0.3f, 0.8f, 1f);
            if (GUILayout.Button("📌 Зафиксировать текущую позицию (Save Position)", GUILayout.Height(26)))
            {
                var lsg = UnityEngine.Object.FindAnyObjectByType<LevelSequenceGenerator>();
                if (lsg != null)
                {
                    lsg.CaptureDoorOffsetsFromScene();
                    EditorUtility.SetDirty(lsg);
                    Debug.Log($"<color=#00FFAA><b>[Portcullis]</b></color> Позиция решётки зафиксирована! EntranceOffset: {lsg.EntranceDoorOffset}, ExitOffset: {lsg.ExitDoorOffset}");
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📦 Выбрать префаб решётки в Project"))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/DungeonPortcullisDoor.prefab");
                if (prefab != null)
                {
                    Selection.activeObject = prefab;
                    EditorGUIUtility.PingObject(prefab);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void OnSceneGUI()
        {
            var door = (SectorBarrierDoor2D)target;
            Vector3 basePos = door.transform.position;

            // Подпись в Scene View
            Handles.Label(basePos + new Vector3(0f, 4.4f, 0f), $"[Решётка: {(door.IsClosed ? "Закрыта" : "Открыта")}]", EditorStyles.boldLabel);
        }
    }
}
