using System;
using UnityEngine;

namespace Combat.Settings
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public class CombatSettingsManager : MonoBehaviour
    {
        private const string PrefsKey = "CombatWheel_Settings_v1";

        private static bool _applicationIsQuitting = false;
        private static CombatSettingsManager _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsOnSubsystemRegistration()
        {
            _applicationIsQuitting = false;
            _instance = null;
        }

        public static CombatSettingsManager Instance
        {
            get
            {
                if (_applicationIsQuitting) return null;

                if (_instance == null && Application.isPlaying)
                {
                    var existing = FindAnyObjectByType<CombatSettingsManager>();
                    if (existing != null)
                    {
                        _instance = existing;
                    }
                    else
                    {
                        var go = new GameObject("[CombatSettingsManager]");
                        _instance = go.AddComponent<CombatSettingsManager>();
                    }
                    if (_instance != null && _instance.transform.parent == null)
                    {
                        DontDestroyOnLoad(_instance.gameObject);
                    }
                }
                return _instance;
            }
            private set { _instance = value; }
        }

        private void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitializeSettings()
        {
            _applicationIsQuitting = false;
            var inst = Instance;
            if (inst != null)
            {
                inst.LoadSettings();
            }
        }

        public static event Action<WheelSettingsData> OnSettingsChanged;

        [SerializeField] private WheelSettingsData settings = new WheelSettingsData();

        public WheelSettingsData CurrentSettings => settings;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (Application.isPlaying && transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
            LoadSettings();
        }

        private void Start()
        {
            // Уведомляем слушателей о начальных настройках
            ApplyHitboxSetting();
            ApplyFencingSettings();
            ApplyFocusBarSettings();
            OnSettingsChanged?.Invoke(settings);
        }

        public void UpdateSettings(WheelSettingsData newSettings)
        {
            if (newSettings == null) return;
            settings = newSettings;
            SaveSettings();
            ApplyHitboxSetting();
            ApplyFencingSettings();
            ApplyFocusBarSettings();
            OnSettingsChanged?.Invoke(settings);
        }

        public void ResetToDefaults()
        {
            settings = WheelSettingsData.CreateDefault();
            SaveSettings();
            ApplyHitboxSetting();
            ApplyFencingSettings();
            ApplyFocusBarSettings();
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

        public void ApplyHitboxSetting()
        {
            // Устаревший визуализатор хитбоксов удален
        }

        public void ApplyFencingSettings()
        {
            var wheel = FindAnyObjectByType<Fencing.FencingWheel>();
            if (wheel != null)
            {
                wheel.ApplySettings(settings);
            }

            var wheelUI = FindAnyObjectByType<Fencing.FencingWheelUI>();
            if (wheelUI != null)
            {
                wheelUI.ApplySettings(settings);
            }
        }

        public void ApplyFocusBarSettings()
        {
            var focusUI = FindAnyObjectByType<FocusUI>();
            if (focusUI != null && settings != null)
            {
                if (settings.focusBarPosY < 75f)
                {
                    settings.focusBarPosY = 83.43f;
                    settings.focusBarPosX = 18.08f;
                    settings.focusBarScale = 1.91f;
                }
                focusUI.ApplySettings(settings.focusBarScale, settings.focusBarPosX, settings.focusBarPosY);
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
                        // Валидация координат шкалы фокуса: предотвращаем наложение на полоску здоровья
                        if (loaded.focusBarPosY < 75f)
                        {
                            loaded.focusBarPosY = 83.43f;
                            loaded.focusBarPosX = 18.08f;
                            loaded.focusBarScale = 1.91f;
                        }
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
