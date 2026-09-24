using System;
using UnityEngine;
using UnityEditor;
using Combat.Player;

namespace Combat.Player.Editor
{
    /// <summary>
    /// Пользовательский инспектор для PlayerStamina2D.
    /// Предоставляет быстрый доступ к расходу стамины на прыжки и отображает баланс выносливости.
    /// </summary>
    [CustomEditor(typeof(PlayerStamina2D))]
    public class PlayerStamina2DEditor : UnityEditor.Editor
    {
        private static readonly int[] Presets = new int[] { 0, 10, 15, 20, 25, 30, 33, 50, 75, 100 };

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var stamina = (PlayerStamina2D)target;
            if (stamina == null) return;

            var controller = stamina.GetComponent<PlayerController2D>();
            if (controller != null)
            {
                EditorGUILayout.Space(8);
                var boxStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset(10, 10, 8, 8)
                };

                using (new EditorGUILayout.VerticalScope(boxStyle))
                {
                    var titleStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        normal = { textColor = new Color(0.2f, 0.85f, 1f) }
                    };
                    EditorGUILayout.LabelField("⚡ РАСХОД СТАМИНЫ НА ПРЫЖКИ (PlayerController2D)", titleStyle);
                    EditorGUILayout.Space(4);

                    float pct = controller.JumpStaminaPercent;
                    float flatAmount = stamina.MaxStamina * (pct / 100f);
                    EditorGUILayout.LabelField($"Текущий расход за прыжок: {pct:F0}% ({flatAmount:F1} из {stamina.MaxStamina} стамины)");

                    EditorGUILayout.LabelField("Быстро переключить процент (нажмите кнопку):", EditorStyles.miniBoldLabel);
                    EditorGUILayout.BeginHorizontal();
                    foreach (int p in Presets)
                    {
                        bool isSelected = Mathf.Approximately(pct, p);
                        var prevBg = GUI.backgroundColor;
                        if (isSelected) GUI.backgroundColor = new Color(0.2f, 0.85f, 1f);
                        if (GUILayout.Button(p + "%", GUILayout.Height(22), GUILayout.MinWidth(28)))
                        {
                            Undo.RecordObject(controller, "Change Jump Stamina Percent");
                            controller.JumpStaminaPercent = p;
                            if (!controller.SeparateAirJumpStamina)
                            {
                                controller.AirJumpStaminaPercent = p;
                            }
                            EditorUtility.SetDirty(controller);
                        }
                        GUI.backgroundColor = prevBg;
                    }
                    EditorGUILayout.EndHorizontal();

                    int jumps = pct > 0.001f ? Mathf.FloorToInt(100f / pct) : 999;
                    EditorGUILayout.HelpBox($"При {pct:F0}% игрок сделает ровно {jumps} прыжк(а/ов) подряд до наступления истощения (Exhaustion).", MessageType.Info);
                }
            }
        }
    }
}
