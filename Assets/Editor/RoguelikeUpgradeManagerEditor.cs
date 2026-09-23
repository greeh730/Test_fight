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

        public override void OnInspectorGUI()
        {
            var manager = (RoguelikeUpgradeManager)target;

            // Отрисовка стандартных сериализованных полей
            DrawDefaultInspector();

            EditorGUILayout.Space(12);

            // Стилизованный заголовок панели тестирования
            GUIStyle headerBoxStyle = new GUIStyle(EditorStyles.helpBox);
            headerBoxStyle.normal.textColor = Color.white;
            headerBoxStyle.fontSize = 12;

            EditorGUILayout.BeginVertical(headerBoxStyle);
            EditorGUILayout.LabelField("🃏 ТЕСТИРОВАНИЕ КАРТОЧЕК В ИНСПЕКТОРЕ", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Запустите игру (Play Mode), чтобы интерактивно открывать и выбирать карточки улучшений!", MessageType.Info);
            }
            else
            {
                // Кнопка открытия окна карточек
                GUI.backgroundColor = new Color(0.2f, 0.85f, 1f, 1f);
                if (GUILayout.Button("▶ ОТКРЫТЬ ВЫБОР 3 КАРТОЧЕК НА ЭКРАНЕ", GUILayout.Height(36)))
                {
                    manager.TestShowUpgradeScreen();
                }

                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f, 1f);
                if (GUILayout.Button("✖ Скрыть окно карточек", GUILayout.Height(24)))
                {
                    manager.TestHideUpgradeScreen();
                }
                GUI.backgroundColor = Color.white;

                // Если уже сгенерированы карточки — даем кнопки прямого выбора прямо из Инспектора!
                if (manager.CurrentOffer != null && manager.CurrentOffer.Count > 0)
                {
                    EditorGUILayout.Space(6);
                    EditorGUILayout.LabelField("Текущее предложение карточек:", EditorStyles.boldLabel);

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

            // Секция Тактика
            EditorGUILayout.BeginVertical(headerBoxStyle);
            EditorGUILayout.LabelField("⚔ РАЗБЛОКИРОВКА СПОСОБНОСТЕЙ ТАКТИКА", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Нажимайте на кнопки, чтобы мгновенно открывать или закрывать конкретные способности Тактика:", MessageType.None);

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
                        GUI.backgroundColor = new Color(0.3f, 1f, 0.5f, 1f); // Зеленый
                    }
                    else
                    {
                        GUI.backgroundColor = new Color(0.7f, 0.7f, 0.75f, 1f); // Серый
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

            // Секция Быстрых Баффов
            EditorGUILayout.BeginVertical(headerBoxStyle);
            EditorGUILayout.LabelField("⚡ БЫСТРЫЕ БАФФЫ ХАРАКТЕРИСТИК", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+30 Макс. HP", GUILayout.Height(26)))
            {
                if (Application.isPlaying) manager.TestAddHealth(30f);
            }
            if (GUILayout.Button("+50 Стамина", GUILayout.Height(26)))
            {
                if (Application.isPlaying) manager.TestAddStamina(50f);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+25% к урону", GUILayout.Height(26)))
            {
                if (Application.isPlaying) manager.TestAddDamage(25f);
            }
            if (GUILayout.Button("+15% к скорости", GUILayout.Height(26)))
            {
                if (Application.isPlaying) manager.TestAddSpeed(15f);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(1f, 0.3f, 0.3f, 1f);
            if (GUILayout.Button("СБРОСИТЬ ЗАБЕГ (Reset Run State)", GUILayout.Height(28)))
            {
                if (Application.isPlaying) manager.ResetRunState();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();
        }
    }
}
