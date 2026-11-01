#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Combat.Settings;
using Fencing;

namespace Combat.Editor
{
    /// <summary>
    /// Мост сохранения настроек фехтования между Game Mode и сценой проекта.
    /// Когда игрок изменяет настройки в режиме Game и затем останавливает воспроизведение,
    /// данный скрипт автоматически переносит сохраненные в PlayerPrefs параметры
    /// на компоненты FencingWheel, AttackOrb, FencingWheelUI и CombatSettingsManager в открытой сцене,
    /// и сохраняет сцену на диск.
    /// </summary>
    [InitializeOnLoad]
    public static class FencingSettingsPlayModeBridge
    {
        private const string PrefsKey = "CombatWheel_Settings_v1";

        static FencingSettingsPlayModeBridge()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                ApplySavedSettingsToScene();
            }
        }

        [MenuItem("Combat/Применить сохраненные настройки фехтования к сцене")]
        public static void ApplySavedSettingsToScene()
        {
            if (Application.isPlaying) return;

            if (!PlayerPrefs.HasKey(PrefsKey)) return;

            string json = PlayerPrefs.GetString(PrefsKey, null);
            if (string.IsNullOrEmpty(json)) return;

            WheelSettingsData data = null;
            try
            {
                data = JsonUtility.FromJson<WheelSettingsData>(json);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[FencingSettingsPlayModeBridge] Ошибка разбора настроек: {ex.Message}");
                return;
            }

            if (data == null) return;

            bool isDirty = false;

            // 1. Применяем к CombatSettingsManager в сцене
            var mgr = Object.FindAnyObjectByType<CombatSettingsManager>();
            if (mgr != null)
            {
                mgr.LoadSettings();
                EditorUtility.SetDirty(mgr);
                isDirty = true;
            }

            // 2. Применяем к FencingWheel и AttackOrb (если не включен приоритет Инспектора)
            var wheel = Object.FindAnyObjectByType<FencingWheel>();
            if (wheel != null && !wheel.UseInspectorSettings)
            {
                wheel.ApplySettings(data);
                EditorUtility.SetDirty(wheel);

                var orb = wheel.Orb != null ? wheel.Orb : wheel.GetComponentInChildren<AttackOrb>();
                if (orb != null)
                {
                    orb.Weight = data.orbWeight;
                    orb.FollowSpeed = data.orbFollowSpeed;
                    EditorUtility.SetDirty(orb);
                }
                isDirty = true;
            }

            // 3. Применяем к FencingWheelUI (если не включен приоритет Инспектора)
            var wheelUI = Object.FindAnyObjectByType<FencingWheelUI>();
            if (wheelUI != null && !wheelUI.UseInspectorSettings)
            {
                wheelUI.ApplySettings(data);
                EditorUtility.SetDirty(wheelUI);
                isDirty = true;
            }

            // 4. Применяем к FocusUI
            var focusUI = Object.FindAnyObjectByType<FocusUI>();
            if (focusUI != null)
            {
                focusUI.ApplySettings(data.focusBarScale, data.focusBarPosX, data.focusBarPosY);
                EditorUtility.SetDirty(focusUI);
                isDirty = true;
            }

            // 5. Сохраняем сцену
            if (isDirty)
            {
                var scene = EditorSceneManager.GetActiveScene();
                if (scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("<color=#00FF88>[FencingSettingsPlayModeBridge] Параметры колеса фехтования успешно сохранены в сцену проекта!</color>");
                }
            }
        }
    }
}
#endif
