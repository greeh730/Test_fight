using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LevelGeneration.Editor
{
    [CustomEditor(typeof(LevelSequenceGenerator))]
    public class LevelSequenceGeneratorEditor : UnityEditor.Editor
    {
        private bool _showTiersQuickTest = true;

        public override void OnInspectorGUI()
        {
            var gen = (LevelSequenceGenerator)target;
            if (gen == null) return;

            var container = gen.transform.Find("[Generated_Level_Chunks]");
            int chunkCount = container != null ? container.childCount : 0;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("🎮 ПАНЕЛЬ УПРАВЛЕНИЯ И ТЕСТИРОВАНИЯ УРОВНЕЙ", EditorStyles.boldLabel);

            // Информационный дашборд о статусе уровня и прогрессии
            int activeTierIdx = gen.GetActiveTierIndex();
            string activeTierName = (gen.DifficultyTiers != null && activeTierIdx >= 0 && activeTierIdx < gen.DifficultyTiers.Count)
                ? gen.DifficultyTiers[activeTierIdx].tierName
                : $"Тир {activeTierIdx + 1}";

            string statusMsg = chunkCount == 0
                ? "⚠️ Уровень сейчас НЕ сгенерирован в сцене.\n"
                : $"✅ Уровень активен в сцене (секций: {chunkCount}).\n";

            statusMsg += $"💎 Собрано Кристаллов Победы: {gen.CrystalsCollected}  |  🏃 Забег: #{gen.CurrentRun}\n" +
                         $"📊 Активный уровень сложности: {activeTierName}\n" +
                         $"⚙️ Режим выбора: {gen.ActiveTierOverrideMode}";

            EditorGUILayout.HelpBox(statusMsg, chunkCount == 0 ? MessageType.Warning : MessageType.Info);

            EditorGUILayout.Space(4);

            // Кнопки главного управления (Сгенерировать / Очистить)
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = chunkCount == 0 ? new Color(0.2f, 0.9f, 0.4f) : new Color(0.3f, 0.7f, 1.0f);
            if (GUILayout.Button(chunkCount == 0 ? "▶ Сгенерировать уровень" : "🔄 Перегенерировать", GUILayout.Height(34)))
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

            // Секция быстрого переключения тиров сложности
            EditorGUILayout.LabelField("Быстрое переключение сложности для тестов:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();

            bool isTier1 = gen.ActiveTierOverrideMode == TierOverrideMode.ForceTier1 || (gen.ActiveTierOverrideMode == TierOverrideMode.AutoByProgress && activeTierIdx == 0);
            GUI.backgroundColor = isTier1 ? new Color(0.35f, 0.85f, 1.0f) : Color.white;
            if (GUILayout.Button("⭐ Тир 1\n(1-3 забег)", GUILayout.Height(36)))
            {
                SwitchTier(gen, 0);
            }

            bool isTier2 = gen.ActiveTierOverrideMode == TierOverrideMode.ForceTier2 || (gen.ActiveTierOverrideMode == TierOverrideMode.AutoByProgress && activeTierIdx == 1);
            GUI.backgroundColor = isTier2 ? new Color(1f, 0.85f, 0.2f) : Color.white;
            if (GUILayout.Button("⚡ Тир 2\n(4-5 забег)", GUILayout.Height(36)))
            {
                SwitchTier(gen, 1);
            }

            bool isTier3 = gen.ActiveTierOverrideMode == TierOverrideMode.ForceTier3 || (gen.ActiveTierOverrideMode == TierOverrideMode.AutoByProgress && activeTierIdx == 2);
            GUI.backgroundColor = isTier3 ? new Color(1f, 0.45f, 0.45f) : Color.white;
            if (GUILayout.Button("💀 Тир 3\n(6+ забег)", GUILayout.Height(36)))
            {
                SwitchTier(gen, 2);
            }

            bool isAuto = gen.ActiveTierOverrideMode == TierOverrideMode.AutoByProgress;
            GUI.backgroundColor = isAuto ? new Color(0.4f, 0.95f, 0.65f) : Color.white;
            if (GUILayout.Button("🎲 Авто\n(по кристаллам)", GUILayout.Height(36), GUILayout.Width(100)))
            {
                Undo.RecordObject(gen, "Auto Progress Mode");
                gen.ActiveTierOverrideMode = TierOverrideMode.AutoByProgress;
                gen.DebugSpecificPrefab = null;
                gen.DebugPrefabIndexInTier = -1;
                gen.GenerateLevel();
                MarkDirty(gen);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // Интерактивный выбор конкретных комнат по тирам в 1 клик
            _showTiersQuickTest = EditorGUILayout.Foldout(_showTiersQuickTest, "🎯 Мгновенный тест конкретных комнат (в 1 клик):", true, EditorStyles.foldoutHeader);
            if (_showTiersQuickTest && gen.DifficultyTiers != null)
            {
                for (int t = 0; t < gen.DifficultyTiers.Count; t++)
                {
                    var tier = gen.DifficultyTiers[t];
                    if (tier == null) continue;

                    string tierTitle = string.IsNullOrEmpty(tier.tierName) ? $"Тир {t + 1}" : tier.tierName;
                    EditorGUILayout.LabelField($"• {tierTitle} (Забеги: {tier.minRun}..{tier.maxRun}):", EditorStyles.boldLabel);

                    if (tier.chunkPool == null || tier.chunkPool.Count == 0)
                    {
                        EditorGUILayout.LabelField("   (Пул пуст, добавьте префабы в инспекторе ниже)", EditorStyles.miniLabel);
                        continue;
                    }

                    EditorGUILayout.BeginHorizontal();
                    for (int p = 0; p < tier.chunkPool.Count; p++)
                    {
                        var prefab = tier.chunkPool[p];
                        if (prefab == null) continue;

                        string btnName = prefab.name;
                        // Укорачиваем длинные технические имена префабов для компактности кнопок
                        btnName = btnName.Replace("Chunk_Variant_", "").Replace("Chunk_", "").Replace(".prefab", "");

                        bool isSelected = gen.ActiveTierOverrideMode == TierOverrideMode.ForceSpecificPrefab && gen.DebugSpecificPrefab == prefab;
                        GUI.backgroundColor = isSelected ? new Color(0.2f, 1f, 0.6f) : Color.white;

                        if (GUILayout.Button(btnName, GUILayout.Height(28)))
                        {
                            Undo.RecordObject(gen, $"Test Prefab {prefab.name}");
                            gen.SetTestPrefab(prefab);
                            MarkDirty(gen);
                        }

                        if ((p + 1) % 3 == 0 && p < tier.chunkPool.Count - 1)
                        {
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.BeginHorizontal();
                        }
                    }
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space(2);
                }
            }

            EditorGUILayout.Space(6);

            // Drag & Drop слот произвольного префаба
            EditorGUILayout.LabelField("Тестирование произвольного внешнего префаба:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            var newDebugPrefab = (LevelChunk)EditorGUILayout.ObjectField(gen.DebugSpecificPrefab, typeof(LevelChunk), false);
            if (newDebugPrefab != gen.DebugSpecificPrefab)
            {
                Undo.RecordObject(gen, "Change Debug Prefab");
                gen.DebugSpecificPrefab = newDebugPrefab;
                MarkDirty(gen);
            }

            GUI.enabled = gen.DebugSpecificPrefab != null;
            GUI.backgroundColor = new Color(0.3f, 0.9f, 0.9f);
            if (GUILayout.Button("▶ Тестировать", GUILayout.Width(100)))
            {
                Undo.RecordObject(gen, "Test Specific Prefab");
                gen.SetTestPrefab(gen.DebugSpecificPrefab);
                MarkDirty(gen);
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // Кнопка автозаполнения префабов по умолчанию из папок
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔄 Авто-заполнить тиры из папок по умолчанию", GUILayout.Height(24)))
            {
                Undo.RecordObject(gen, "Populate Default Tiers");
                gen.PopulateDefaultTiers();
                gen.GenerateLevel();
                MarkDirty(gen);
            }

            if (GUILayout.Button("🔍 Камера на весь уровень", GUILayout.Height(24), GUILayout.Width(170)))
            {
                FocusSceneViewOnLevel(gen);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // Отрисовка стандартных настроек компонента
            DrawDefaultInspector();
        }

        private void GenerateInScene(LevelSequenceGenerator gen)
        {
            Undo.RecordObject(gen, "Generate Level Preview");
            gen.GenerateLevel();
            MarkDirty(gen);
        }

        private void ClearInScene(LevelSequenceGenerator gen)
        {
            Undo.RecordObject(gen, "Clear Level Preview");
            gen.ClearOldChunks();
            MarkDirty(gen);
        }

        private void SwitchTier(LevelSequenceGenerator gen, int tierIndex)
        {
            Undo.RecordObject(gen, $"Switch to Tier {tierIndex + 1}");
            gen.SetTestTier(tierIndex);
            MarkDirty(gen);
        }

        private void MarkDirty(LevelSequenceGenerator gen)
        {
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
