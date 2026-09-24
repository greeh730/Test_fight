using System;
using UnityEngine;
using UnityEditor;
using Combat.Player;

namespace Combat.Player.Editor
{
    /// <summary>
    /// Пользовательский инспектор для PlayerController2D.
    /// Позволяет удобно "тыкать" проценты расхода стамины на прыжки кнопками быстрого выбора
    /// или настраивать их точным ползунком (0..100%).
    /// </summary>
    [CustomEditor(typeof(PlayerController2D))]
    public class PlayerController2DEditor : UnityEditor.Editor
    {
        private SerializedProperty _jumpStaminaProp;
        private SerializedProperty _separateAirJumpProp;
        private SerializedProperty _airJumpStaminaProp;

        private static readonly int[] Presets = new int[] { 0, 10, 15, 20, 25, 30, 33, 50, 75, 100 };

        private void OnEnable()
        {
            _jumpStaminaProp = serializedObject.FindProperty("jumpStaminaPercent");
            _separateAirJumpProp = serializedObject.FindProperty("separateAirJumpStamina");
            _airJumpStaminaProp = serializedObject.FindProperty("airJumpStaminaPercent");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Отрисовка поля скрипта
            SerializedProperty scriptProp = serializedObject.FindProperty("m_Script");
            if (scriptProp != null)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(scriptProp);
                }
            }

            // Блок быстрой интерактивной настройки процентов стамины
            DrawStaminaPercentageBox();

            EditorGUILayout.Space(6);

            // Отрисовываем остальные параметры контроллера
            DrawPropertiesExcluding(serializedObject, "m_Script", "jumpStaminaPercent", "separateAirJumpStamina", "airJumpStaminaPercent");

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawStaminaPercentageBox()
        {
            var boxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(12, 12, 10, 10)
            };

            using (new EditorGUILayout.VerticalScope(boxStyle))
            {
                var titleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 12,
                    normal = { textColor = new Color(0.2f, 0.85f, 1f) }
                };

                EditorGUILayout.LabelField("⚡ РАСХОД ВЫНОСЛИВОСТИ НА ПРЫЖОК", titleStyle);
                EditorGUILayout.Space(2);

                if (_jumpStaminaProp != null)
                {
                    // Ползунок 0..100%
                    EditorGUILayout.Slider(_jumpStaminaProp, 0f, 100f, new GUIContent("Прыжок с земли (%)"));

                    // Кнопки быстрого выбора ("тыкалка")
                    EditorGUILayout.LabelField("Быстрый выбор процента (нажмите кнопку):", EditorStyles.miniBoldLabel);
                    EditorGUILayout.BeginHorizontal();
                    foreach (int p in Presets)
                    {
                        bool isSelected = Mathf.Approximately(_jumpStaminaProp.floatValue, p);
                        var prevBg = GUI.backgroundColor;
                        if (isSelected) GUI.backgroundColor = new Color(0.2f, 0.85f, 1f);
                        if (GUILayout.Button(p + "%", GUILayout.Height(24), GUILayout.MinWidth(28)))
                        {
                            _jumpStaminaProp.floatValue = p;
                            if (_separateAirJumpProp != null && !_separateAirJumpProp.boolValue && _airJumpStaminaProp != null)
                            {
                                _airJumpStaminaProp.floatValue = p;
                            }
                        }
                        GUI.backgroundColor = prevBg;
                    }
                    EditorGUILayout.EndHorizontal();
                }

                if (_separateAirJumpProp != null)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.PropertyField(_separateAirJumpProp, new GUIContent("Разделять расход для двойного прыжка"));

                    if (_separateAirJumpProp.boolValue && _airJumpStaminaProp != null)
                    {
                        EditorGUILayout.Slider(_airJumpStaminaProp, 0f, 100f, new GUIContent("Прыжок в воздухе (%)"));

                        EditorGUILayout.LabelField("Быстрый выбор для воздуха (%):", EditorStyles.miniBoldLabel);
                        EditorGUILayout.BeginHorizontal();
                        foreach (int p in Presets)
                        {
                            bool isSelected = Mathf.Approximately(_airJumpStaminaProp.floatValue, p);
                            var prevBg = GUI.backgroundColor;
                            if (isSelected) GUI.backgroundColor = new Color(1f, 0.7f, 0.2f);
                            if (GUILayout.Button(p + "%", GUILayout.Height(22), GUILayout.MinWidth(28)))
                            {
                                _airJumpStaminaProp.floatValue = p;
                            }
                            GUI.backgroundColor = prevBg;
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }

                EditorGUILayout.Space(6);
                float currentVal = _jumpStaminaProp != null ? _jumpStaminaProp.floatValue : 25f;
                int jumps = currentVal > 0.001f ? Mathf.FloorToInt(100f / currentVal) : 999;
                string infoMsg = currentVal <= 0.001f
                    ? "Прыжки бесплатны (не тратят стамину)."
                    : $"При расходе {currentVal:F0}% игрок сделает {jumps} прыжк(а/ов) подряд до наступления истощения (Exhaustion).";
                EditorGUILayout.HelpBox(infoMsg, MessageType.Info);
            }
        }
    }
}
