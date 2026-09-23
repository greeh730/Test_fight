using System;
using UnityEditor;
using UnityEngine;
using Combat.Roguelike;
using Combat.UI;
using Combat.Tactician;

namespace Combat.Roguelike.Editor
{
    [CustomEditor(typeof(RoguelikeUpgradeManager))]
    public class RoguelikeUpgradeManagerEditor : UnityEditor.Editor
    {
        private static readonly Direction8[] AllDirections = new Direction8[]
        {
            Direction8.Right,
            Direction8.Down,
            Direction8.Up,
            Direction8.Left,
            Direction8.UpRight,
            Direction8.DownRight,
            Direction8.DownLeft,
            Direction8.UpLeft
        };

        private SerializedProperty _tacticianCardsProp;
        private SerializedProperty _combatBuffsProp;
        private SerializedProperty _riskRewardPerksProp;

        private bool _tacticianCardsFoldout = true;
        private bool _combatBuffsFoldout = true;
        private bool _riskRewardFoldout = true;

        private void OnEnable()
        {
            _tacticianCardsProp = serializedObject.FindProperty("tacticianCards");
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

            // А. Способности Тактика
            _tacticianCardsFoldout = EditorGUILayout.Foldout(_tacticianCardsFoldout, $"🔹 Карточки Тактика ({_tacticianCardsProp.arraySize})", true, EditorStyles.foldoutHeader);
            if (_tacticianCardsFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_tacticianCardsProp, true);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            // Б. Боевые баффы
            _combatBuffsFoldout = EditorGUILayout.Foldout(_combatBuffsFoldout, $"🟢 Боевые баффы ({_combatBuffsProp.arraySize})", true, EditorStyles.foldoutHeader);
            if (_combatBuffsFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_combatBuffsProp, true);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            // В. Перки риска и награды
            _riskRewardFoldout = EditorGUILayout.Foldout(_riskRewardFoldout, $"🔴 Перки с риском (Двусторонние) ({_riskRewardPerksProp.arraySize})", true, EditorStyles.foldoutHeader);
            if (_riskRewardFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_riskRewardPerksProp, true);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(12);

            // 2. БЛОК ТЕСТИРОВАНИЯ В ПЛЕЙ-МОДЕ
            EditorGUILayout.BeginVertical(sectionBoxStyle);
            EditorGUILayout.LabelField("🃏 ТЕСТИРОВАНИЕ КАРТОЧЕК В ИГРЕ", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Запустите игру (Play Mode), чтобы прямо отсюда открывать выбор карточек и нажимать на них!", MessageType.Info);
            }
            else
            {
                GUI.backgroundColor = new Color(0.2f, 0.85f, 1f, 1f);
                if (GUILayout.Button("▶ ОТКРЫТЬ ВЫБОР 3 КАРТОЧЕК НА ЭКРАНЕ", GUILayout.Height(34)))
                {
                    manager.TestShowUpgradeScreen();
                }

                GUI.backgroundColor = new Color(1f, 0.45f, 0.45f, 1f);
                if (GUILayout.Button("✖ Скрыть окно карточек", GUILayout.Height(22)))
                {
                    manager.TestHideUpgradeScreen();
                }
                GUI.backgroundColor = Color.white;

                // Кнопки прямого клика по карточкам из Инспектора
                if (manager.CurrentOffer != null && manager.CurrentOffer.Count > 0)
                {
                    EditorGUILayout.Space(6);
                    EditorGUILayout.LabelField("Текущий выбор (кликните для взятия):", EditorStyles.boldLabel);

                    for (int i = 0; i < manager.CurrentOffer.Count; i++)
                    {
                        var card = manager.CurrentOffer[i];
                        if (card == null) continue;

                        Color btnColor;
                        switch (card.category)
                        {
                            case UpgradeCategory.TacticianAbility:
                                btnColor = new Color(0.3f, 0.9f, 1f, 1f);
                                break;
                            case UpgradeCategory.CombatBuff:
                                btnColor = new Color(0.4f, 1f, 0.5f, 1f);
                                break;
                            case UpgradeCategory.RiskAndReward:
                                btnColor = new Color(1f, 0.45f, 0.45f, 1f);
                                break;
                            default:
                                btnColor = Color.white;
                                break;
                        }

                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        EditorGUILayout.LabelField($"{card.iconSymbol} <b>{card.title}</b>  <i>[{card.category}]</i>", new GUIStyle(EditorStyles.label) { richText = true });
                        if (!string.IsNullOrEmpty(card.positiveEffectText))
                        {
                            EditorGUILayout.LabelField($"  <color=#00AA00>✓ {card.positiveEffectText}</color>", new GUIStyle(EditorStyles.miniLabel) { richText = true });
                        }
                        if (!string.IsNullOrEmpty(card.negativeEffectText))
                        {
                            EditorGUILayout.LabelField($"  <color=#CC0000>⚠ {card.negativeEffectText}</color>", new GUIStyle(EditorStyles.miniLabel) { richText = true });
                        }

                        GUI.backgroundColor = btnColor;
                        if (GUILayout.Button($"Выбрать Карточку #{i + 1}: {card.title}", GUILayout.Height(28)))
                        {
                            manager.TestPickCard(i);
                        }
                        GUI.backgroundColor = Color.white;

                        EditorGUILayout.EndVertical();
                    }
                }
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // 3. БЛОК СТОЙКИ ТАКТИКА (Переключение способностей)
            EditorGUILayout.BeginVertical(sectionBoxStyle);
            EditorGUILayout.LabelField("⚔ БЫСТРОЕ ПЕРЕКЛЮЧЕНИЕ СПОСОБНОСТЕЙ ТАКТИКА", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Зелёная кнопка = способность открыта игроку, серая = заблокирована:", MessageType.None);

            for (int i = 0; i < AllDirections.Length; i += 2)
            {
                EditorGUILayout.BeginHorizontal();
                for (int j = 0; j < 2; j++)
                {
                    int index = i + j;
                    if (index >= AllDirections.Length) break;
                    var dir = AllDirections[index];

                    bool isUnlocked = Application.isPlaying && manager.IsTacticianAbilityUnlocked(dir);
                    string name = TacticianCombatController2D.GetAbilityName(dir);

                    if (isUnlocked)
                    {
                        GUI.backgroundColor = new Color(0.3f, 1f, 0.5f, 1f);
                    }
                    else
                    {
                        GUI.backgroundColor = new Color(0.7f, 0.7f, 0.75f, 1f);
                    }

                    string status = isUnlocked ? " [ОТКРЫТО]" : " [ЗАКРЫТО]";
                    if (GUILayout.Button(name + status, GUILayout.Height(26)))
                    {
                        if (Application.isPlaying)
                        {
                            manager.ToggleTacticianAbility(dir);
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Стойка Тактика", "Запустите Play Mode для переключения способностей игрока.", "OK");
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.4f, 0.9f, 1f, 1f);
            if (GUILayout.Button("Разблокировать ВСЕ 8", GUILayout.Height(26)))
            {
                if (Application.isPlaying) manager.TestUnlockAllTactician();
            }

            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f, 1f);
            if (GUILayout.Button("Заблокировать ВСЕ 8", GUILayout.Height(26)))
            {
                if (Application.isPlaying) manager.TestResetAllTactician();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // 4. ТЕКУЩИЕ МНОЖИТЕЛИ И БЫСТРЫЕ БАФФЫ
            EditorGUILayout.BeginVertical(sectionBoxStyle);
            EditorGUILayout.LabelField("📊 ТЕКУЩИЕ МНОЖИТЕЛИ ЗАБЕГА", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Урон игрока: x{manager.PlayerDamageMultiplier:F2} | Скорость игрока: x{manager.PlayerSpeedMultiplier:F2}");
            EditorGUILayout.LabelField($"HP врагов: x{manager.EnemyHealthMultiplier:F2} | Скорость врагов: x{manager.EnemySpeedMultiplier:F2}");

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+30 Макс. HP", GUILayout.Height(24)))
            {
                if (Application.isPlaying) manager.TestAddHealth(30f);
            }
            if (GUILayout.Button("+50 Стамина", GUILayout.Height(24)))
            {
                if (Application.isPlaying) manager.TestAddStamina(50f);
            }
            if (GUILayout.Button("+25% Урон", GUILayout.Height(24)))
            {
                if (Application.isPlaying) manager.TestAddDamage(25f);
            }
            if (GUILayout.Button("+15% Скорость", GUILayout.Height(24)))
            {
                if (Application.isPlaying) manager.TestAddSpeed(15f);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(1f, 0.35f, 0.35f, 1f);
            if (GUILayout.Button("СБРОСИТЬ ЗАБЕГ (Reset Run State)", GUILayout.Height(26)))
            {
                if (Application.isPlaying) manager.ResetRunState();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();
        }
    }
}
