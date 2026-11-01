using System;
using UnityEditor;
using UnityEngine;
using Combat.Roguelike;

namespace Combat.Roguelike.Editor
{
    [CustomEditor(typeof(RoguelikeUpgradeManager))]
    public class RoguelikeUpgradeManagerEditor : UnityEditor.Editor
    {
        private SerializedProperty _combatBuffsProp;
        private SerializedProperty _riskRewardPerksProp;

        private bool _combatBuffsFoldout = true;
        private bool _riskRewardFoldout = true;

        private void OnEnable()
        {
            _combatBuffsProp = serializedObject.FindProperty("combatBuffs");
            _riskRewardPerksProp = serializedObject.FindProperty("riskRewardPerks");
        }

        public override void OnInspectorGUI()
        {
            var manager = (RoguelikeUpgradeManager)target;
            serializedObject.Update();

            GUIStyle sectionBoxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 8)
            };

            // 1. БЛОК БАЛАНСИРОВКИ КАРТОЧЕК
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(sectionBoxStyle);
            EditorGUILayout.LabelField("⚖ БАЛАНС И НАСТРОЙКА КАРТОЧЕК УЛУЧШЕНИЙ", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Здесь вы можете менять любые цифры бонусов, штрафов, здоровья, выносливости и урона для каждой карточки прямо в Инспекторе.\nНажмите 'Обновить тексты по цифрам', чтобы автоматически сгенерировать описания под новые значения.", MessageType.None);
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.3f, 0.9f, 1f, 1f);
            if (GUILayout.Button("🔄 Сбросить к дефолту", GUILayout.Height(26)))
            {
                if (EditorUtility.DisplayDialog("Сброс карточек", "Восстановить все каталоги карточек к стандартным значениям?", "Да, сбросить", "Отмена"))
                {
                    Undo.RecordObject(manager, "Reset Upgrade Catalogs");
                    manager.EnsureDefaultCatalogs(force: true);
                    EditorUtility.SetDirty(manager);
                }
            }

            GUI.backgroundColor = new Color(0.4f, 1f, 0.6f, 1f);
            if (GUILayout.Button("📝 Обновить тексты по цифрам", GUILayout.Height(26)))
            {
                Undo.RecordObject(manager, "Sync Card Texts");
                manager.SyncCardTextsFromStats();
                EditorUtility.SetDirty(manager);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // А. Боевые баффы
            _combatBuffsFoldout = EditorGUILayout.Foldout(_combatBuffsFoldout, $"🟢 Боевые баффы ({_combatBuffsProp.arraySize})", true, EditorStyles.foldoutHeader);
            if (_combatBuffsFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_combatBuffsProp, true);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            // Б. Риск и награда
            _riskRewardFoldout = EditorGUILayout.Foldout(_riskRewardFoldout, $"🔴 Риск и награда ({_riskRewardPerksProp.arraySize})", true, EditorStyles.foldoutHeader);
            if (_riskRewardFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_riskRewardPerksProp, true);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // 2. БЛОК СТАТИСТИКИ ЗАБЕГА
            EditorGUILayout.BeginVertical(sectionBoxStyle);
            EditorGUILayout.LabelField("📊 ТЕКУЩИЕ МНОЖИТЕЛИ ЗАБЕГА", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"• Урон игрока: x{manager.PlayerDamageMultiplier:F2}");
            EditorGUILayout.LabelField($"• Скорость бега игрока: x{manager.PlayerSpeedMultiplier:F2}");
            EditorGUILayout.LabelField($"• HP врагов (проклятия): x{manager.EnemyHealthMultiplier:F2}");
            EditorGUILayout.LabelField($"• Скорость атак врагов: x{manager.EnemySpeedMultiplier:F2}");

            if (GUILayout.Button("Сбросить состояние забега (к 1.0x)", GUILayout.Height(24)))
            {
                manager.ResetRunState();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // 3. БЛОК ТЕСТИРОВАНИЯ В PLAY MODE
            EditorGUILayout.BeginVertical(sectionBoxStyle);
            EditorGUILayout.LabelField("🎮 ТЕСТ КАРТОЧЕК В PLAY MODE", EditorStyles.boldLabel);

            if (GUILayout.Button("Открыть окно выбора (3 карточки)", GUILayout.Height(28)))
            {
                if (Application.isPlaying) manager.TestShowUpgradeScreen();
                else EditorUtility.DisplayDialog("Play Mode", "Запустите игру для тестирования окна карточек.", "OK");
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+25 HP", GUILayout.Height(22)))
            {
                if (Application.isPlaying) manager.TestAddHealth(25f);
            }
            if (GUILayout.Button("+30 Стамина", GUILayout.Height(22)))
            {
                if (Application.isPlaying) manager.TestAddStamina(30f);
            }
            if (GUILayout.Button("+20% Урон", GUILayout.Height(22)))
            {
                if (Application.isPlaying) manager.TestAddDamage(20f);
            }
            if (GUILayout.Button("+15% Бег", GUILayout.Height(22)))
            {
                if (Application.isPlaying) manager.TestAddSpeed(15f);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
