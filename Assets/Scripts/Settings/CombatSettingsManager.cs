using System;
using UnityEngine;

namespace Combat.Settings
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public class CombatSettingsManager : MonoBehaviour
    {
        private const string PrefsKey = "CombatWheel_Settings_v1";

        public static CombatSettingsManager Instance { get; private set; }

        public static event Action<WheelSettingsData> OnSettingsChanged;

        [SerializeField] private WheelSettingsData settings = new WheelSettingsData();

        public WheelSettingsData CurrentSettings => settings;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            LoadSettings();
        }

        private void Start()
        {
            // Уведомляем слушателей о начальных настройках
            OnSettingsChanged?.Invoke(settings);
        }

        public void UpdateSettings(WheelSettingsData newSettings)
        {
            if (newSettings == null) return;
            settings = newSettings;
            SaveSettings();
            OnSettingsChanged?.Invoke(settings);
        }

        public void ResetToDefaults()
        {
            settings = WheelSettingsData.CreateDefault();
            SaveSettings();
            OnSettingsChanged?.Invoke(settings);
        }

        public void SaveSettings()
        {
            try
            {
                string json = JsonUtility.ToJson(settings);
                PlayerPrefs.SetString(PrefsKey, json);
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CombatSettingsManager] Failed to save settings: {ex.Message}");
            }
        }

        public void LoadSettings()
        {
            if (PlayerPrefs.HasKey(PrefsKey))
            {
                try
                {
                    string json = PlayerPrefs.GetString(PrefsKey);
                    var loaded = JsonUtility.FromJson<WheelSettingsData>(json);
                    if (loaded != null)
                    {
                        settings = loaded;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[CombatSettingsManager] Failed to load settings: {ex.Message}");
                }
            }

            // Если не сохранены — используем дефолтные
            settings = WheelSettingsData.CreateDefault();
        }
    }
}
