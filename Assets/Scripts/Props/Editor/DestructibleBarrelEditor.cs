using UnityEditor;
using UnityEngine;

namespace Combat.Props.Editor
{
    [CustomEditor(typeof(DestructibleBarrel))]
    [CanEditMultipleObjects]
    public class DestructibleBarrelEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var barrel = (DestructibleBarrel)target;

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                richText = true
            };
            EditorGUILayout.LabelField("🛢️ <b>ДЕРЕВЯННАЯ БОЧКА (РАЗРУШАЕМЫЙ ОБЪЕКТ)</b>", titleStyle);

            var infoStyle = new GUIStyle(EditorStyles.miniLabel) { richText = true };
            EditorGUILayout.LabelField("Разрушается от любых ударов меча игрока и магии. Выбрасывает зелье.", infoStyle);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Ключевой блок: Лечение зелья
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var healTitleStyle = new GUIStyle(EditorStyles.boldLabel) { richText = true };
            EditorGUILayout.LabelField("<color=#33FF88>💚 НАСТРОЙКА ЛЕЧЕНИЯ ЗЕЛЬЯ</color>", healTitleStyle);

            var healProp = serializedObject.FindProperty("healAmount");
            EditorGUILayout.PropertyField(healProp, new GUIContent("Лечение (HP)", "Сколько очков здоровья восстанавливает выпавшее из бочки зелье"));

            // Быстрые пресеты
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+15 HP", EditorStyles.miniButtonLeft)) { healProp.floatValue = 15f; }
            if (GUILayout.Button("+25 HP", EditorStyles.miniButtonMid)) { healProp.floatValue = 25f; }
            if (GUILayout.Button("+35 HP", EditorStyles.miniButtonMid)) { healProp.floatValue = 35f; }
            if (GUILayout.Button("+50 HP", EditorStyles.miniButtonMid)) { healProp.floatValue = 50f; }
            if (GUILayout.Button("+100 HP", EditorStyles.miniButtonRight)) { healProp.floatValue = 100f; }
            EditorGUILayout.EndHorizontal();

            var dropPotionProp = serializedObject.FindProperty("dropPotion");
            EditorGUILayout.PropertyField(dropPotionProp, new GUIContent("Выпадение зелья", "Выпадает ли зелье при уничтожении"));

            if (dropPotionProp.boolValue)
            {
                var dropChanceProp = serializedObject.FindProperty("dropChance");
                EditorGUILayout.Slider(dropChanceProp, 0f, 1f, new GUIContent("Шанс выпадения"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("potionPrefab"), new GUIContent("Префаб зелья"));
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Параметры прочности
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Параметры прочности бочки:", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxHealth"), new GUIContent("Здоровье бочки (HP)", "1 = ломается от 1 удара"));
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Аудио и визуал
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Эффекты и звуки:", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("breakSound"), new GUIContent("Звук разрушения"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("hitSound"), new GUIContent("Звук удара"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("splinterSprite"), new GUIContent("Спрайт осколка"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("splinterCount"), new GUIContent("Количество осколков"));
            EditorGUILayout.EndVertical();

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(6);
                GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
                if (GUILayout.Button("💥 Взорвать / сломать бочку (Тест)", GUILayout.Height(30)))
                {
                    barrel.BreakBarrel(Vector2.up);
                }
                GUI.backgroundColor = Color.white;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
