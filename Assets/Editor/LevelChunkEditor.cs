using UnityEditor;
using UnityEngine;
using LevelGeneration;

namespace LevelGeneration.Editor
{
    [CustomEditor(typeof(LevelChunk))]
    [CanEditMultipleObjects]
    public class LevelChunkEditor : UnityEditor.Editor
    {
        private SerializedProperty _entryPointProp;
        private SerializedProperty _exitPointProp;
        private SerializedProperty _playerSpawnPointProp;
        private SerializedProperty _enemySpawnPointsProp;

        private void OnEnable()
        {
            _entryPointProp = serializedObject.FindProperty("entryPoint");
            _exitPointProp = serializedObject.FindProperty("exitPoint");
            _playerSpawnPointProp = serializedObject.FindProperty("playerSpawnPoint");
            _enemySpawnPointsProp = serializedObject.FindProperty("enemySpawnPoints");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var chunk = (LevelChunk)target;

            // Стилизованный заголовок
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Фрагмент уровня (Level Chunk)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Зелёный круг — точка входа (X=0). Красный круг — точка выхода.\nЛиния между ними показывает нулевой уровень пола.", MessageType.None);
            EditorGUILayout.Space(6);

            // Поля сокетов
            EditorGUILayout.PropertyField(_entryPointProp, new GUIContent("🟢 Точка входа (Entry)"));
            EditorGUILayout.PropertyField(_exitPointProp, new GUIContent("🔴 Точка выхода (Exit)"));
            EditorGUILayout.PropertyField(_playerSpawnPointProp, new GUIContent("🔵 Спавн игрока (Spawn)"));
            EditorGUILayout.PropertyField(_enemySpawnPointsProp, new GUIContent("👾 Точки врагов (Enemies)"), true);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);

            bool hasEntry = chunk.EntryPoint != null && chunk.EntryPoint != chunk.transform;
            bool hasExit = chunk.ExitPoint != null && chunk.ExitPoint != chunk.transform;

            // Кнопка быстрого создания сокетов, если их ещё нет
            if (!hasEntry || !hasExit)
            {
                GUI.backgroundColor = new Color(0.3f, 0.9f, 0.4f, 1f);
                if (GUILayout.Button("🟢 Создать сокеты в 1 клик (Вход и Выход)", GUILayout.Height(32)))
                {
                    Undo.RecordObject(chunk, "Create Default Sockets");
                    chunk.CreateDefaultSockets(20f);
                    EditorUtility.SetDirty(chunk);
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.Space(4);
            }

            // Статус стыковки по высоте и длине
            if (hasEntry && hasExit)
            {
                float length = chunk.Length;
                float deltaY = Mathf.Abs(chunk.ExitPoint.position.y - chunk.EntryPoint.position.y);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"📏 Длина секции: {length:F2} м", EditorStyles.boldLabel);

                if (deltaY < 0.001f)
                {
                    GUI.color = Color.green;
                    EditorGUILayout.LabelField("✔ Идеальная стыковка: перепад высоты ΔY = 0.00 м", EditorStyles.boldLabel);
                    GUI.color = Color.white;
                }
                else
                {
                    EditorGUILayout.HelpBox($"Внимание! Точка выхода смещена по высоте на {deltaY:F2} м!\nДля бесшовной стыковки рекомендуется выровнять высоту в Y=0.", MessageType.Warning);
                    GUI.backgroundColor = new Color(1f, 0.8f, 0.3f, 1f);
                    if (GUILayout.Button("📏 Выровнять сокеты по высоте (Y = 0)"))
                    {
                        Undo.RecordObject(chunk.EntryPoint, "Align Entry Y");
                        Undo.RecordObject(chunk.ExitPoint, "Align Exit Y");
                        chunk.AlignSocketsToZero();
                        EditorUtility.SetDirty(chunk);
                    }
                    GUI.backgroundColor = Color.white;
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Быстрые действия:", EditorStyles.boldLabel);

            // Кнопка добавления спавна игрока
            if (chunk.PlayerSpawnPoint == null)
            {
                if (GUILayout.Button("🔵 Добавить точку спавна игрока (для старта)"))
                {
                    Undo.RecordObject(chunk, "Add Player Spawn Point");
                    chunk.AddPlayerSpawnPoint();
                    EditorUtility.SetDirty(chunk);
                }
            }

            // Кнопка создания базового пола под длину секции
            if (GUILayout.Button("🧱 Создать базовый пол под размер сокетов"))
            {
                CreateDefaultFloorUnderSockets(chunk);
            }

            // Кнопка добавления точки спавна врага
            if (GUILayout.Button("👾 Добавить точку спавна врага на платформе"))
            {
                Undo.RegisterFullObjectHierarchyUndo(chunk.gameObject, "Add Enemy Spawn Point");
                chunk.AddEnemySpawnPoint();
                EditorUtility.SetDirty(chunk);
            }
        }

        private void CreateDefaultFloorUnderSockets(LevelChunk chunk)
        {
            float length = chunk.Length > 0.1f ? chunk.Length : 20f;
            Undo.RegisterFullObjectHierarchyUndo(chunk.gameObject, "Create Default Floor");

            var squareSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/Square.png");
            var spriteMat = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");

            // Базовый пол 3 метра вглубь
            var ground = new GameObject("Ground_Base");
            ground.transform.SetParent(chunk.transform, false);
            ground.transform.localPosition = new Vector3(length * 0.5f, -1.5f, 0f);
            ground.transform.localScale = new Vector3(length, 3f, 1f);

            var sr = ground.AddComponent<SpriteRenderer>();
            sr.sprite = squareSprite;
            sr.color = new Color(0.12f, 0.16f, 0.22f, 1f);
            sr.sortingOrder = 4;
            if (spriteMat != null) sr.sharedMaterial = spriteMat;

            var col = ground.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;

            // Неоновая кромка сверху
            var glow = new GameObject("Ground_Glow_Top");
            glow.transform.SetParent(chunk.transform, false);
            glow.transform.localPosition = new Vector3(length * 0.5f, -0.06f, 0f);
            glow.transform.localScale = new Vector3(length, 0.12f, 1f);

            var srGlow = glow.AddComponent<SpriteRenderer>();
            srGlow.sprite = squareSprite;
            srGlow.color = new Color(0.20f, 0.78f, 0.98f, 1f);
            srGlow.sortingOrder = 6;
            if (spriteMat != null) srGlow.sharedMaterial = spriteMat;

            EditorUtility.SetDirty(chunk.gameObject);
        }
    }
}
