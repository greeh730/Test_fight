using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LevelGeneration.Editor
{
    [CustomEditor(typeof(LevelSequenceGenerator))]
    public class LevelSequenceGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var gen = (LevelSequenceGenerator)target;
            if (gen == null) return;

            var container = gen.transform.Find("[Generated_Level_Chunks]");
            int chunkCount = container != null ? container.childCount : 0;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("🎮 ПРЕДПРОСМОТР ЛОКАЦИЙ В SCENE VIEW", EditorStyles.boldLabel);

            // Информационный баннер о статусе уровня
            if (chunkCount == 0)
            {
                EditorGUILayout.HelpBox(
                    "⚠️ Уровень сейчас НЕ сгенерирован в сцене.\n" +
                    "Окно Scene показывает старую тестовую арену или пустоту.\n" +
                    "Нажмите кнопку ниже, чтобы сгенерировать локации прямо в сцене без запуска игры!",
                    MessageType.Warning
                );
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"✅ Уровень сгенерирован в сцене (активно секций: {chunkCount}).\n" +
                    "Вы можете осматривать, настраивать платформы и тестировать навигацию прямо в Scene View!",
                    MessageType.Info
                );
            }

            EditorGUILayout.Space(4);

            // Кнопки главного управления
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = chunkCount == 0 ? new Color(0.2f, 0.9f, 0.4f) : new Color(0.3f, 0.7f, 1.0f);
            if (GUILayout.Button(chunkCount == 0 ? "▶ Сгенерировать уровень в сцене" : "🔄 Перегенерировать в сцене", GUILayout.Height(34)))
            {
                GenerateInScene(gen);
            }

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("✕ Очистить", GUILayout.Height(34), GUILayout.Width(80)))
            {
                ClearInScene(gen);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            // Переключатель конкретных комнат (Варианты A, B, C, D)
            EditorGUILayout.LabelField("Переключить боевую комнату для предпросмотра:", EditorStyles.miniBoldLabel);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = gen.ForcePreviewCombatVariantIndex == 0 ? new Color(1f, 0.85f, 0.2f) : Color.white;
            if (GUILayout.Button("Вариант A\n(CombatArena)", GUILayout.Height(32)))
            {
                SwitchVariant(gen, 0);
            }

            GUI.backgroundColor = gen.ForcePreviewCombatVariantIndex == 1 ? new Color(1f, 0.85f, 0.2f) : Color.white;
            if (GUILayout.Button("Вариант B\n(TwoTier)", GUILayout.Height(32)))
            {
                SwitchVariant(gen, 1);
            }

            GUI.backgroundColor = gen.ForcePreviewCombatVariantIndex == 2 ? new Color(1f, 0.85f, 0.2f) : Color.white;
            if (GUILayout.Button("Вариант C\n(SplitPath)", GUILayout.Height(32)))
            {
                SwitchVariant(gen, 2);
            }

            GUI.backgroundColor = gen.ForcePreviewCombatVariantIndex == 3 ? new Color(1f, 0.85f, 0.2f) : Color.white;
            if (GUILayout.Button("Вариант D\n(Chucn_varint_D)", GUILayout.Height(32)))
            {
                SwitchVariant(gen, 3);
            }

            GUI.backgroundColor = gen.ForcePreviewCombatVariantIndex == -1 ? new Color(0.4f, 0.9f, 0.7f) : Color.white;
            if (GUILayout.Button("🎲 Случайно", GUILayout.Height(32), GUILayout.Width(75)))
            {
                SwitchVariant(gen, -1);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            if (GUILayout.Button("🔍 Сфокусировать камеру Scene View на всем уровне", GUILayout.Height(26)))
            {
                FocusSceneViewOnLevel(gen);
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // Отрисовка стандартных настроек компонента
            DrawDefaultInspector();
        }

        private void GenerateInScene(LevelSequenceGenerator gen)
        {
            Undo.RecordObject(gen, "Generate Level Preview");
            gen.GenerateLevel();
            EditorUtility.SetDirty(gen.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gen.gameObject.scene);
            SceneView.RepaintAll();
        }

        private void ClearInScene(LevelSequenceGenerator gen)
        {
            Undo.RecordObject(gen, "Clear Level Preview");
            gen.ClearOldChunks();
            EditorUtility.SetDirty(gen.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gen.gameObject.scene);
            SceneView.RepaintAll();
        }

        private void SwitchVariant(LevelSequenceGenerator gen, int variantIndex)
        {
            Undo.RecordObject(gen, "Switch Level Variant");
            gen.ForcePreviewCombatVariantIndex = variantIndex;
            gen.GenerateLevel();
            EditorUtility.SetDirty(gen.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gen.gameObject.scene);
            SceneView.RepaintAll();
        }

        private void FocusSceneViewOnLevel(LevelSequenceGenerator gen)
        {
            var container = gen.transform.Find("[Generated_Level_Chunks]");
            if (container == null || container.childCount == 0) return;

            var bounds = new Bounds(container.GetChild(0).position, Vector3.one * 10f);
            var renderers = container.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }

            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.Frame(bounds, false);
            }
        }
    }
}
