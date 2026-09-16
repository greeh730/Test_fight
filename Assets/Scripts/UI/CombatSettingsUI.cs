using System;
using UnityEngine;
using UnityEngine.UI;
using Combat.Settings;

namespace Combat.UI
{
    [DisallowMultipleComponent]
    public class CombatSettingsUI : MonoBehaviour
    {
        [Header("--- Windows & Hotkeys ---")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private CanvasGroup panelCanvasGroup;
        [SerializeField] private Button gearOpenButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;
        [SerializeField] private KeyCode escapeKey = KeyCode.Escape;

        [Header("--- Control Settings UI ---")]
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private Text sensitivityValueText;
        [SerializeField] private Slider deadzoneSlider;
        [SerializeField] private Text deadzoneValueText;
        [SerializeField] private Dropdown activationModeDropdown;
        [SerializeField] private Toggle invertXToggle;
        [SerializeField] private Toggle invertYToggle;
        [SerializeField] private Toggle quickCastToggle;
        [SerializeField] private Toggle gamepadToggle;

        [Header("--- Visual & HUD UI ---")]
        [SerializeField] private Dropdown placementDropdown;
        [SerializeField] private Dropdown colorThemeDropdown;
        [SerializeField] private Slider hudScaleSlider;
        [SerializeField] private Text hudScaleValueText;
        [SerializeField] private Slider idleOpacitySlider;
        [SerializeField] private Text idleOpacityValueText;
        [SerializeField] private Toggle showPlaqueToggle;

        private bool _isUpdatingUI = false;

        public bool IsOpen => settingsPanel != null && settingsPanel.activeSelf;

        private void Awake()
        {
            if (gearOpenButton != null)
            {
                gearOpenButton.onClick.AddListener(ToggleSettings);
            }
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(CloseSettings);
            }
            if (resetButton != null)
            {
                resetButton.onClick.AddListener(OnResetClicked);
            }

            SetupControlListeners();
        }

        private void Start()
        {
            if (CombatSettingsManager.Instance != null)
            {
                UpdateUIFromSettings(CombatSettingsManager.Instance.CurrentSettings);
            }

            SetPanelVisible(false);
        }

        private void OnEnable()
        {
            CombatSettingsManager.OnSettingsChanged += OnSettingsChangedFromExternal;
        }

        private void OnDisable()
        {
            CombatSettingsManager.OnSettingsChanged -= OnSettingsChangedFromExternal;
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                ToggleSettings();
            }
            else if (Input.GetKeyDown(escapeKey) && IsOpen)
            {
                CloseSettings();
            }
        }

        public void ToggleSettings()
        {
            if (IsOpen)
            {
                CloseSettings();
            }
            else
            {
                OpenSettings();
            }
        }

        public void OpenSettings()
        {
            SetPanelVisible(true);

            if (CombatSettingsManager.Instance != null)
            {
                UpdateUIFromSettings(CombatSettingsManager.Instance.CurrentSettings);
            }
        }

        public void CloseSettings()
        {
            SetPanelVisible(false);
        }

        private void SetPanelVisible(bool visible)
        {
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(visible);
            }
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = visible ? 1f : 0f;
                panelCanvasGroup.interactable = visible;
                panelCanvasGroup.blocksRaycasts = visible;
            }
        }

        private void SetupControlListeners()
        {
            if (sensitivitySlider != null)
            {
                sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
            }
            if (deadzoneSlider != null)
            {
                deadzoneSlider.onValueChanged.AddListener(OnDeadzoneChanged);
            }
            if (activationModeDropdown != null)
            {
                activationModeDropdown.onValueChanged.AddListener(OnActivationModeChanged);
            }
            if (invertXToggle != null)
            {
                invertXToggle.onValueChanged.AddListener(OnInvertXChanged);
            }
            if (invertYToggle != null)
            {
                invertYToggle.onValueChanged.AddListener(OnInvertYChanged);
            }
            if (quickCastToggle != null)
            {
                quickCastToggle.onValueChanged.AddListener(OnQuickCastChanged);
            }
            if (gamepadToggle != null)
            {
                gamepadToggle.onValueChanged.AddListener(OnGamepadChanged);
            }
            if (placementDropdown != null)
            {
                placementDropdown.onValueChanged.AddListener(OnPlacementChanged);
            }
            if (colorThemeDropdown != null)
            {
                colorThemeDropdown.onValueChanged.AddListener(OnColorThemeChanged);
            }
            if (hudScaleSlider != null)
            {
                hudScaleSlider.onValueChanged.AddListener(OnHudScaleChanged);
            }
            if (idleOpacitySlider != null)
            {
                idleOpacitySlider.onValueChanged.AddListener(OnIdleOpacityChanged);
            }
            if (showPlaqueToggle != null)
            {
                showPlaqueToggle.onValueChanged.AddListener(OnShowPlaqueChanged);
            }
        }

        private void OnSettingsChangedFromExternal(WheelSettingsData data)
        {
            if (!_isUpdatingUI)
            {
                UpdateUIFromSettings(data);
            }
        }

        public void UpdateUIFromSettings(WheelSettingsData data)
        {
            if (data == null) return;
            _isUpdatingUI = true;

            try
            {
                if (sensitivitySlider != null)
                {
                    sensitivitySlider.value = data.gestureSensitivity;
                    if (sensitivityValueText != null)
                        sensitivityValueText.text = $"{data.gestureSensitivity:F2}x";
                }

                if (deadzoneSlider != null)
                {
                    deadzoneSlider.value = data.deadzone;
                    if (deadzoneValueText != null)
                        deadzoneValueText.text = $"{Mathf.RoundToInt(data.deadzone)} px";
                }

                if (activationModeDropdown != null)
                {
                    activationModeDropdown.value = (int)data.activationMode;
                }

                if (invertXToggle != null) invertXToggle.isOn = data.invertX;
                if (invertYToggle != null) invertYToggle.isOn = data.invertY;
                if (quickCastToggle != null) quickCastToggle.isOn = data.quickCastOnEdge;
                if (gamepadToggle != null) gamepadToggle.isOn = data.enableGamepad;

                if (placementDropdown != null)
                {
                    placementDropdown.value = (int)data.placement;
                }

                if (colorThemeDropdown != null)
                {
                    colorThemeDropdown.value = (int)data.colorTheme;
                }

                if (hudScaleSlider != null)
                {
                    hudScaleSlider.value = data.hudScale;
                    if (hudScaleValueText != null)
                        hudScaleValueText.text = $"{Mathf.RoundToInt(data.hudScale * 100f)}%";
                }

                if (idleOpacitySlider != null)
                {
                    idleOpacitySlider.value = data.idleOpacity;
                    if (idleOpacityValueText != null)
                        idleOpacityValueText.text = $"{Mathf.RoundToInt(data.idleOpacity * 100f)}%";
                }

                if (showPlaqueToggle != null) showPlaqueToggle.isOn = data.showAttackPlaque;
            }
            finally
            {
                _isUpdatingUI = false;
            }
        }

        private WheelSettingsData GetCurrentData()
        {
            if (CombatSettingsManager.Instance != null)
            {
                return CombatSettingsManager.Instance.CurrentSettings;
            }
            return new WheelSettingsData();
        }

        private void CommitChange(Action<WheelSettingsData> modifyAction)
        {
            if (_isUpdatingUI) return;
            if (CombatSettingsManager.Instance != null)
            {
                var data = CombatSettingsManager.Instance.CurrentSettings;
                modifyAction(data);
                CombatSettingsManager.Instance.UpdateSettings(data);
            }
        }

        private void OnSensitivityChanged(float val)
        {
            if (sensitivityValueText != null)
                sensitivityValueText.text = $"{val:F2}x";

            CommitChange(s => s.gestureSensitivity = val);
        }

        private void OnDeadzoneChanged(float val)
        {
            if (deadzoneValueText != null)
                deadzoneValueText.text = $"{Mathf.RoundToInt(val)} px";

            CommitChange(s => s.deadzone = val);
        }

        private void OnActivationModeChanged(int val)
        {
            CommitChange(s => s.activationMode = (ActivationMode)val);
        }

        private void OnInvertXChanged(bool val)
        {
            CommitChange(s => s.invertX = val);
        }

        private void OnInvertYChanged(bool val)
        {
            CommitChange(s => s.invertY = val);
        }

        private void OnQuickCastChanged(bool val)
        {
            CommitChange(s => s.quickCastOnEdge = val);
        }

        private void OnGamepadChanged(bool val)
        {
            CommitChange(s => s.enableGamepad = val);
        }

        private void OnPlacementChanged(int val)
        {
            CommitChange(s => s.placement = (WheelPlacement)val);
        }

        private void OnColorThemeChanged(int val)
        {
            CommitChange(s => s.colorTheme = (WheelColorTheme)val);
        }

        private void OnHudScaleChanged(float val)
        {
            if (hudScaleValueText != null)
                hudScaleValueText.text = $"{Mathf.RoundToInt(val * 100f)}%";

            CommitChange(s => s.hudScale = val);
        }

        private void OnIdleOpacityChanged(float val)
        {
            if (idleOpacityValueText != null)
                idleOpacityValueText.text = $"{Mathf.RoundToInt(val * 100f)}%";

            CommitChange(s => s.idleOpacity = val);
        }

        private void OnShowPlaqueChanged(bool val)
        {
            CommitChange(s => s.showAttackPlaque = val);
        }

        private void OnResetClicked()
        {
            if (CombatSettingsManager.Instance != null)
            {
                CombatSettingsManager.Instance.ResetToDefaults();
                UpdateUIFromSettings(CombatSettingsManager.Instance.CurrentSettings);
            }
        }
    }
}
