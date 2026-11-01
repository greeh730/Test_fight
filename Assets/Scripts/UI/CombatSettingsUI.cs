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
        [SerializeField] private Button footerCloseButton;
        [SerializeField] private Button resetButton;
        [Header("--- Обучение: Пропуск ---")]
        [SerializeField] private Button skipTutorialButton;
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;
        [SerializeField] private KeyCode escapeKey = KeyCode.Escape;

        [Header("--- Audio & Volume UI ---")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Text masterVolumeValueText;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Text sfxVolumeValueText;
        [SerializeField] private Slider footstepsVolumeSlider;
        [SerializeField] private Text footstepsVolumeValueText;
        [SerializeField] private Toggle muteAudioToggle;
        [SerializeField] private Button testAudioButton;

        [Header("--- Combat & Hitboxes UI ---")]
        [SerializeField] private Toggle showHitboxesToggle;

        [Header("--- Fencing Wheel UI ---")]
        [SerializeField] private Toggle showWheelToggle;
        [SerializeField] private Button placementLeftButton;
        [SerializeField] private Button placementRightButton;
        [SerializeField] private Image placementLeftBg;
        [SerializeField] private Image placementRightBg;

        [SerializeField] private Slider wheelSizeSlider;
        [SerializeField] private Text wheelSizeValueText;

        [SerializeField] private Slider orbWeightSlider;
        [SerializeField] private Text orbWeightValueText;

        [SerializeField] private Slider orbSpeedSlider;
        [SerializeField] private Text orbSpeedValueText;

        [SerializeField] private Slider freeZoneRadiusSlider;
        [SerializeField] private Text freeZoneRadiusValueText;

        [SerializeField] private Slider attackAngleSlider;
        [SerializeField] private Text attackAngleValueText;

        [SerializeField] private Toggle enableDeadZoneToggle;
        [SerializeField] private Slider deadZoneAngleSlider;
        [SerializeField] private Text deadZoneAngleValueText;

        [Header("--- Focus Bar UI ---")]
        [SerializeField] private Slider focusBarScaleSlider;
        [SerializeField] private Text focusBarScaleValueText;
        [SerializeField] private Slider focusBarPosXSlider;
        [SerializeField] private Text focusBarPosXValueText;
        [SerializeField] private Slider focusBarPosYSlider;
        [SerializeField] private Text focusBarPosYValueText;

        [Header("--- Magic Arrow Bar UI ---")]
        [SerializeField] private Slider arrowIconSizeSlider;
        [SerializeField] private Text arrowIconSizeValueText;
        [SerializeField] private Slider runeBarWidthSlider;
        [SerializeField] private Text runeBarWidthValueText;
        [SerializeField] private Slider runeBarHeightSlider;
        [SerializeField] private Text runeBarHeightValueText;
        [SerializeField] private Slider runeBarOffsetYSlider;
        [SerializeField] private Text runeBarOffsetYValueText;
        [SerializeField] private Slider runeBarOffsetXSlider;
        [SerializeField] private Text runeBarOffsetXValueText;
        [SerializeField] private Toggle showRuneBarOnlyInMagicToggle;

        [Header("--- Tutorial Mode Overrides ---")]
        [SerializeField] private bool isTutorialMode = false;
        [SerializeField] private GameObject tutorialArrowPointer;
        [SerializeField] private Text tutorialArrowText;
        [SerializeField] private Outline toggleOutline;

        public bool IsTutorialMode
        {
            get => isTutorialMode || (Combat.Tutorial.TutorialManager.Instance != null);
            set => isTutorialMode = value;
        }

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
            if (footerCloseButton != null)
            {
                footerCloseButton.onClick.AddListener(OnSaveAndCloseClicked);
            }
            else if (settingsPanel != null)
            {
                var fBtnTransform = settingsPanel.transform.Find("SettingsWindow/Footer/FooterCloseButton");
                if (fBtnTransform != null)
                {
                    footerCloseButton = fBtnTransform.GetComponent<Button>();
                    if (footerCloseButton != null)
                    {
                        footerCloseButton.onClick.AddListener(OnSaveAndCloseClicked);
                    }
                }
            }

            if (resetButton != null)
            {
                resetButton.onClick.AddListener(OnResetClicked);
            }

            SetupControlListeners();
            InitializeAllToggleStyles();
        }

        public void OnSaveAndCloseClicked()
        {
            if (CombatSettingsManager.Instance != null)
            {
                CombatSettingsManager.Instance.SaveSettings();
            }
            CloseSettings();
        }

        private void Start()
        {
            if (CombatSettingsManager.Instance != null)
            {
                UpdateUIFromSettings(CombatSettingsManager.Instance.CurrentSettings);
            }

            if (IsTutorialMode)
            {
                ApplyTutorialModeLayout();
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
            if (IsOpen && IsTutorialMode)
            {
                UpdateTutorialArrowAnimation();
            }

            if (IsKeyJustPressed(toggleKey))
            {
                ToggleSettings();
            }
            else if (IsKeyJustPressed(escapeKey) && IsOpen)
            {
                CloseSettings();
            }
        }

        private static bool IsKeyJustPressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (key == KeyCode.F1 && kb.f1Key.wasPressedThisFrame) return true;
                if (key == KeyCode.Escape && kb.escapeKey.wasPressedThisFrame) return true;
            }
#endif
            try
            {
                return Input.GetKeyDown(key);
            }
            catch
            {
                return false;
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

            var wheel = FindAnyObjectByType<Fencing.FencingWheel>();
            if (wheel != null) wheel.IsPanelOpen = true;

            if (CombatSettingsManager.Instance != null)
            {
                UpdateUIFromSettings(CombatSettingsManager.Instance.CurrentSettings);
            }

            EnsureSkipTutorialButton();

            if (IsTutorialMode)
            {
                ApplyTutorialModeLayout();
                UpdateTutorialArrowState(showWheelToggle != null && showWheelToggle.isOn);
            }
        }

        public static event Action OnSettingsClosed;
        public static event Action<bool> OnShowWheelToggled;

        public void CloseSettings()
        {
            SetPanelVisible(false);

            var wheel = FindAnyObjectByType<Fencing.FencingWheel>();
            if (wheel != null) wheel.IsPanelOpen = false;

            OnSettingsClosed?.Invoke();
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
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            }
            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            }
            if (footstepsVolumeSlider != null)
            {
                footstepsVolumeSlider.onValueChanged.AddListener(OnFootstepsVolumeChanged);
            }
            if (muteAudioToggle != null)
            {
                muteAudioToggle.onValueChanged.AddListener(OnMuteAudioChanged);
            }
            if (testAudioButton != null)
            {
                testAudioButton.onClick.AddListener(OnTestAudioClicked);
            }

            if (showHitboxesToggle != null)
            {
                showHitboxesToggle.onValueChanged.AddListener(OnShowHitboxesChanged);
            }

            if (showWheelToggle != null)
            {
                showWheelToggle.onValueChanged.AddListener(OnShowWheelChanged);
            }

            if (placementLeftButton != null)
            {
                placementLeftButton.onClick.AddListener(() => OnPlacementSelected(FencingWheelPlacement.BottomLeft));
            }
            if (placementRightButton != null)
            {
                placementRightButton.onClick.AddListener(() => OnPlacementSelected(FencingWheelPlacement.BottomRight));
            }
            if (wheelSizeSlider != null)
            {
                wheelSizeSlider.onValueChanged.AddListener(OnWheelSizeChanged);
            }
            if (orbWeightSlider != null)
            {
                orbWeightSlider.onValueChanged.AddListener(OnOrbWeightChanged);
            }
            if (orbSpeedSlider != null)
            {
                orbSpeedSlider.onValueChanged.AddListener(OnOrbSpeedChanged);
            }
            if (freeZoneRadiusSlider != null)
            {
                freeZoneRadiusSlider.onValueChanged.AddListener(OnFreeZoneRadiusChanged);
            }
            if (attackAngleSlider != null)
            {
                attackAngleSlider.onValueChanged.AddListener(OnAttackAngleChanged);
            }
            if (enableDeadZoneToggle != null)
            {
                enableDeadZoneToggle.onValueChanged.AddListener(OnEnableDeadZoneChanged);
            }
            if (deadZoneAngleSlider != null)
            {
                deadZoneAngleSlider.onValueChanged.AddListener(OnDeadZoneAngleChanged);
            }

            if (focusBarScaleSlider != null)
            {
                focusBarScaleSlider.onValueChanged.AddListener(OnFocusBarScaleChanged);
            }
            if (focusBarPosXSlider != null)
            {
                focusBarPosXSlider.onValueChanged.AddListener(OnFocusBarPosXChanged);
            }
            if (focusBarPosYSlider != null)
            {
                focusBarPosYSlider.onValueChanged.AddListener(OnFocusBarPosYChanged);
            }

            if (arrowIconSizeSlider != null)
            {
                arrowIconSizeSlider.onValueChanged.AddListener(OnArrowIconSizeChanged);
            }
            if (runeBarWidthSlider != null)
            {
                runeBarWidthSlider.onValueChanged.AddListener(OnRuneBarWidthChanged);
            }
            if (runeBarHeightSlider != null)
            {
                runeBarHeightSlider.onValueChanged.AddListener(OnRuneBarHeightChanged);
            }
            if (runeBarOffsetYSlider != null)
            {
                runeBarOffsetYSlider.onValueChanged.AddListener(OnRuneBarOffsetYChanged);
            }
            if (runeBarOffsetXSlider != null)
            {
                runeBarOffsetXSlider.onValueChanged.AddListener(OnRuneBarOffsetXChanged);
            }
            if (showRuneBarOnlyInMagicToggle != null)
            {
                showRuneBarOnlyInMagicToggle.onValueChanged.AddListener(OnShowRuneBarOnlyInMagicChanged);
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
                if (masterVolumeSlider != null)
                {
                    masterVolumeSlider.value = data.masterVolume;
                    if (masterVolumeValueText != null)
                        masterVolumeValueText.text = $"{Mathf.RoundToInt(data.masterVolume * 100f)}%";
                }

                if (sfxVolumeSlider != null)
                {
                    sfxVolumeSlider.value = data.sfxVolume;
                    if (sfxVolumeValueText != null)
                        sfxVolumeValueText.text = $"{Mathf.RoundToInt(data.sfxVolume * 100f)}%";
                }

                if (footstepsVolumeSlider != null)
                {
                    footstepsVolumeSlider.value = data.footstepsVolume;
                    if (footstepsVolumeValueText != null)
                        footstepsVolumeValueText.text = $"{Mathf.RoundToInt(data.footstepsVolume * 100f)}%";
                }

                if (muteAudioToggle != null)
                {
                    muteAudioToggle.isOn = data.isMuted;
                    UpdateToggleVisuals(muteAudioToggle, data.isMuted);
                }

                if (showHitboxesToggle != null)
                {
                    showHitboxesToggle.isOn = data.showHitboxes;
                    UpdateToggleVisuals(showHitboxesToggle, data.showHitboxes);
                }

                if (showWheelToggle != null)
                {
                    showWheelToggle.isOn = data.showFencingWheel;
                    UpdateToggleVisuals(showWheelToggle, data.showFencingWheel);
                }

                if (wheelSizeSlider != null)
                {
                    wheelSizeSlider.value = data.wheelSize;
                    if (wheelSizeValueText != null) wheelSizeValueText.text = $"{Mathf.RoundToInt(data.wheelSize)} px";
                }

                if (orbWeightSlider != null)
                {
                    orbWeightSlider.value = data.orbWeight;
                    if (orbWeightValueText != null) orbWeightValueText.text = $"{data.orbWeight:F2}";
                }

                if (orbSpeedSlider != null)
                {
                    orbSpeedSlider.value = data.orbFollowSpeed;
                    if (orbSpeedValueText != null) orbSpeedValueText.text = $"{data.orbFollowSpeed:F1}";
                }

                if (freeZoneRadiusSlider != null)
                {
                    freeZoneRadiusSlider.value = data.freeZoneRadius;
                    if (freeZoneRadiusValueText != null) freeZoneRadiusValueText.text = $"{data.freeZoneRadius:F2}";
                }

                if (attackAngleSlider != null)
                {
                    attackAngleSlider.value = data.attackSectorAngle;
                    if (attackAngleValueText != null) attackAngleValueText.text = $"{Mathf.RoundToInt(data.attackSectorAngle)}°";
                }

                if (enableDeadZoneToggle != null)
                {
                    enableDeadZoneToggle.isOn = data.enableDeadZone;
                    UpdateToggleVisuals(enableDeadZoneToggle, data.enableDeadZone);
                }

                if (deadZoneAngleSlider != null)
                {
                    deadZoneAngleSlider.interactable = data.enableDeadZone;
                    deadZoneAngleSlider.value = data.deadZoneAngle;
                    if (deadZoneAngleValueText != null) deadZoneAngleValueText.text = $"{Mathf.RoundToInt(data.deadZoneAngle)}°";
                    UpdateDeadZoneSliderVisuals(data.enableDeadZone);
                }

                if (focusBarScaleSlider != null)
                {
                    focusBarScaleSlider.value = data.focusBarScale;
                    if (focusBarScaleValueText != null) focusBarScaleValueText.text = $"{data.focusBarScale:F2}x";
                }
                if (focusBarPosXSlider != null)
                {
                    focusBarPosXSlider.value = data.focusBarPosX;
                    if (focusBarPosXValueText != null) focusBarPosXValueText.text = $"{Mathf.RoundToInt(data.focusBarPosX)} px";
                }
                if (focusBarPosYSlider != null)
                {
                    focusBarPosYSlider.value = data.focusBarPosY;
                    if (focusBarPosYValueText != null) focusBarPosYValueText.text = $"{Mathf.RoundToInt(data.focusBarPosY)} px";
                }

                if (arrowIconSizeSlider != null)
                {
                    arrowIconSizeSlider.value = data.arrowIconSize;
                    if (arrowIconSizeValueText != null) arrowIconSizeValueText.text = $"{Mathf.RoundToInt(data.arrowIconSize)} px";
                }
                if (runeBarWidthSlider != null)
                {
                    runeBarWidthSlider.value = data.runeBarWidth;
                    if (runeBarWidthValueText != null) runeBarWidthValueText.text = $"{Mathf.RoundToInt(data.runeBarWidth)} px";
                }
                if (runeBarHeightSlider != null)
                {
                    runeBarHeightSlider.value = data.runeBarHeight;
                    if (runeBarHeightValueText != null) runeBarHeightValueText.text = $"{Mathf.RoundToInt(data.runeBarHeight)} px";
                }
                if (runeBarOffsetYSlider != null)
                {
                    runeBarOffsetYSlider.value = data.runeBarOffsetY;
                    if (runeBarOffsetYValueText != null) runeBarOffsetYValueText.text = $"{Mathf.RoundToInt(data.runeBarOffsetY)} px";
                }
                if (runeBarOffsetXSlider != null)
                {
                    runeBarOffsetXSlider.value = data.runeBarOffsetX;
                    if (runeBarOffsetXValueText != null) runeBarOffsetXValueText.text = $"{Mathf.RoundToInt(data.runeBarOffsetX)} px";
                }
                if (showRuneBarOnlyInMagicToggle != null)
                {
                    showRuneBarOnlyInMagicToggle.isOn = data.showRuneBarOnlyInMagic;
                    UpdateToggleVisuals(showRuneBarOnlyInMagicToggle, data.showRuneBarOnlyInMagic);
                }

                UpdatePlacementButtonsVisuals(data.wheelPlacement);

                if (Combat.Audio.SoundManager.Instance != null)
                {
                    Combat.Audio.SoundManager.Instance.ApplyVolumeSettings(data.masterVolume, data.sfxVolume, data.footstepsVolume, data.isMuted);
                }
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

        private void OnMasterVolumeChanged(float val)
        {
            if (masterVolumeValueText != null)
                masterVolumeValueText.text = $"{Mathf.RoundToInt(val * 100f)}%";

            if (Combat.Audio.SoundManager.Instance != null)
                Combat.Audio.SoundManager.Instance.MasterVolume = val;

            CommitChange(s => s.masterVolume = val);
        }

        private void OnSfxVolumeChanged(float val)
        {
            if (sfxVolumeValueText != null)
                sfxVolumeValueText.text = $"{Mathf.RoundToInt(val * 100f)}%";

            if (Combat.Audio.SoundManager.Instance != null)
                Combat.Audio.SoundManager.Instance.SfxVolume = val;

            CommitChange(s => s.sfxVolume = val);
        }

        private void OnFootstepsVolumeChanged(float val)
        {
            if (footstepsVolumeValueText != null)
                footstepsVolumeValueText.text = $"{Mathf.RoundToInt(val * 100f)}%";

            if (Combat.Audio.SoundManager.Instance != null)
                Combat.Audio.SoundManager.Instance.FootstepsVolume = val;

            CommitChange(s => s.footstepsVolume = val);
        }

        private void OnMuteAudioChanged(bool val)
        {
            if (_isUpdatingUI) return;
            UpdateToggleVisuals(muteAudioToggle, val);

            if (Combat.Audio.SoundManager.Instance != null)
                Combat.Audio.SoundManager.Instance.IsMuted = val;

            CommitChange(s => s.isMuted = val);
        }

        private void OnTestAudioClicked()
        {
            if (Combat.Audio.SoundManager.Instance != null)
            {
                Combat.Audio.SoundManager.Instance.PlayPlayerAttack();
            }
        }

        private void OnShowHitboxesChanged(bool val)
        {
            if (_isUpdatingUI) return;
            UpdateToggleVisuals(showHitboxesToggle, val);
            CommitChange(s => s.showHitboxes = val);
        }

        private void OnShowWheelChanged(bool val)
        {
            if (_isUpdatingUI) return;
            UpdateToggleVisuals(showWheelToggle, val);
            CommitChange(s => s.showFencingWheel = val);
            if (IsTutorialMode)
            {
                UpdateTutorialArrowState(val);
            }
            OnShowWheelToggled?.Invoke(val);
        }

        private void OnPlacementSelected(FencingWheelPlacement placement)
        {
            if (_isUpdatingUI) return;
            UpdatePlacementButtonsVisuals(placement);
            CommitChange(s => s.wheelPlacement = placement);
        }

        private void UpdatePlacementButtonsVisuals(FencingWheelPlacement placement)
        {
            Color activeCol = new Color(0.12f, 0.45f, 0.85f, 1f);
            Color inactiveCol = new Color(0.14f, 0.18f, 0.25f, 0.9f);

            if (placementLeftBg != null)
                placementLeftBg.color = (placement == FencingWheelPlacement.BottomLeft) ? activeCol : inactiveCol;
            if (placementRightBg != null)
                placementRightBg.color = (placement == FencingWheelPlacement.BottomRight) ? activeCol : inactiveCol;
        }

        private void OnWheelSizeChanged(float val)
        {
            if (_isUpdatingUI) return;
            if (wheelSizeValueText != null) wheelSizeValueText.text = $"{Mathf.RoundToInt(val)} px";
            CommitChange(s => s.wheelSize = val);
        }

        private void OnOrbWeightChanged(float val)
        {
            if (_isUpdatingUI) return;
            if (orbWeightValueText != null) orbWeightValueText.text = $"{val:F2}";
            CommitChange(s => s.orbWeight = val);
        }

        private void OnOrbSpeedChanged(float val)
        {
            if (_isUpdatingUI) return;
            if (orbSpeedValueText != null) orbSpeedValueText.text = $"{val:F1}";
            CommitChange(s => s.orbFollowSpeed = val);
        }

        private void OnFreeZoneRadiusChanged(float val)
        {
            if (_isUpdatingUI) return;
            if (freeZoneRadiusValueText != null) freeZoneRadiusValueText.text = $"{val:F2}";
            CommitChange(s => s.freeZoneRadius = val);
        }

        private void OnAttackAngleChanged(float angle)
        {
            if (_isUpdatingUI) return;

            float deadAngle = Mathf.Clamp(180f - 3f * angle, 0f, 90f);
            bool isDeadEnabled = deadAngle > 0.01f;

            if (attackAngleValueText != null) attackAngleValueText.text = $"{Mathf.RoundToInt(angle)}°";
            if (deadZoneAngleValueText != null) deadZoneAngleValueText.text = $"{Mathf.RoundToInt(deadAngle)}°";

            _isUpdatingUI = true;
            try
            {
                if (deadZoneAngleSlider != null) deadZoneAngleSlider.value = deadAngle;
                if (enableDeadZoneToggle != null)
                {
                    enableDeadZoneToggle.isOn = isDeadEnabled;
                    UpdateToggleVisuals(enableDeadZoneToggle, isDeadEnabled);
                }
                UpdateDeadZoneSliderVisuals(isDeadEnabled);
            }
            finally
            {
                _isUpdatingUI = false;
            }

            CommitChange(s =>
            {
                s.attackSectorAngle = angle;
                s.deadZoneAngle = deadAngle;
                s.enableDeadZone = isDeadEnabled;
            });
        }

        private void OnDeadZoneAngleChanged(float deadAngle)
        {
            if (_isUpdatingUI) return;

            float attackAngle = Mathf.Clamp((180f - deadAngle) / 3f, 15f, 60f);
            bool isDeadEnabled = deadAngle > 0.01f;

            if (deadZoneAngleValueText != null) deadZoneAngleValueText.text = $"{Mathf.RoundToInt(deadAngle)}°";
            if (attackAngleValueText != null) attackAngleValueText.text = $"{Mathf.RoundToInt(attackAngle)}°";

            _isUpdatingUI = true;
            try
            {
                if (attackAngleSlider != null) attackAngleSlider.value = attackAngle;
                if (enableDeadZoneToggle != null)
                {
                    enableDeadZoneToggle.isOn = isDeadEnabled;
                    UpdateToggleVisuals(enableDeadZoneToggle, isDeadEnabled);
                }
                UpdateDeadZoneSliderVisuals(isDeadEnabled);
            }
            finally
            {
                _isUpdatingUI = false;
            }

            CommitChange(s =>
            {
                s.deadZoneAngle = deadAngle;
                s.attackSectorAngle = attackAngle;
                s.enableDeadZone = isDeadEnabled;
            });
        }

        private void OnEnableDeadZoneChanged(bool isEnabled)
        {
            if (_isUpdatingUI) return;
            UpdateToggleVisuals(enableDeadZoneToggle, isEnabled);

            float deadAngle = isEnabled ? 30f : 0f;
            float attackAngle = isEnabled ? 50f : 60f;

            if (deadZoneAngleValueText != null) deadZoneAngleValueText.text = $"{Mathf.RoundToInt(deadAngle)}°";
            if (attackAngleValueText != null) attackAngleValueText.text = $"{Mathf.RoundToInt(attackAngle)}°";

            _isUpdatingUI = true;
            try
            {
                if (deadZoneAngleSlider != null)
                {
                    deadZoneAngleSlider.interactable = isEnabled;
                    deadZoneAngleSlider.value = deadAngle;
                    UpdateDeadZoneSliderVisuals(isEnabled);
                }
                if (attackAngleSlider != null) attackAngleSlider.value = attackAngle;
            }
            finally
            {
                _isUpdatingUI = false;
            }

            CommitChange(s =>
            {
                s.enableDeadZone = isEnabled;
                s.deadZoneAngle = deadAngle;
                s.attackSectorAngle = attackAngle;
            });
        }

        private void OnFocusBarScaleChanged(float val)
        {
            if (_isUpdatingUI) return;
            if (focusBarScaleValueText != null) focusBarScaleValueText.text = $"{val:F2}x";
            CommitChange(s =>
            {
                s.focusBarScale = val;
                if (CombatSettingsManager.Instance != null)
                {
                    CombatSettingsManager.Instance.ApplyFocusBarSettings();
                }
            });
        }

        private void OnFocusBarPosXChanged(float val)
        {
            if (_isUpdatingUI) return;
            if (focusBarPosXValueText != null) focusBarPosXValueText.text = $"{Mathf.RoundToInt(val)} px";
            CommitChange(s =>
            {
                s.focusBarPosX = val;
                if (CombatSettingsManager.Instance != null)
                {
                    CombatSettingsManager.Instance.ApplyFocusBarSettings();
                }
            });
        }

        private void OnFocusBarPosYChanged(float val)
        {
            if (_isUpdatingUI) return;
            if (focusBarPosYValueText != null) focusBarPosYValueText.text = $"{Mathf.RoundToInt(val)} px";
            CommitChange(s =>
            {
                s.focusBarPosY = val;
                if (CombatSettingsManager.Instance != null)
                {
                    CombatSettingsManager.Instance.ApplyFocusBarSettings();
                }
            });
        }

        private void OnArrowIconSizeChanged(float val)
        {
            if (_isUpdatingUI) return;
            if (arrowIconSizeValueText != null) arrowIconSizeValueText.text = $"{Mathf.RoundToInt(val)} px";
            CommitChange(s =>
            {
                s.arrowIconSize = val;
                var wheelUI = FindAnyObjectByType<Fencing.FencingWheelUI>();
                if (wheelUI != null) wheelUI.ArrowIconSize = val;
                var magicMgr = FindAnyObjectByType<Magic.PlayerMagicManager>();
                if (magicMgr != null) magicMgr.ArrowSize = val;
            });
        }

        private void OnRuneBarWidthChanged(float val)
        {
            if (_isUpdatingUI) return;
            if (runeBarWidthValueText != null) runeBarWidthValueText.text = $"{Mathf.RoundToInt(val)} px";
            CommitChange(s =>
            {
                s.runeBarWidth = val;
                ApplyRuneBarToUI(s);
            });
        }

        private void OnRuneBarHeightChanged(float val)
        {
            if (_isUpdatingUI) return;
            if (runeBarHeightValueText != null) runeBarHeightValueText.text = $"{Mathf.RoundToInt(val)} px";
            CommitChange(s =>
            {
                s.runeBarHeight = val;
                ApplyRuneBarToUI(s);
            });
        }

        private void OnRuneBarOffsetYChanged(float val)
        {
            if (_isUpdatingUI) return;
            if (runeBarOffsetYValueText != null) runeBarOffsetYValueText.text = $"{Mathf.RoundToInt(val)} px";
            CommitChange(s =>
            {
                s.runeBarOffsetY = val;
                ApplyRuneBarToUI(s);
            });
        }

        private void OnRuneBarOffsetXChanged(float val)
        {
            if (_isUpdatingUI) return;
            if (runeBarOffsetXValueText != null) runeBarOffsetXValueText.text = $"{Mathf.RoundToInt(val)} px";
            CommitChange(s =>
            {
                s.runeBarOffsetX = val;
                ApplyRuneBarToUI(s);
            });
        }

        private void OnShowRuneBarOnlyInMagicChanged(bool val)
        {
            if (_isUpdatingUI) return;
            UpdateToggleVisuals(showRuneBarOnlyInMagicToggle, val);
            CommitChange(s =>
            {
                s.showRuneBarOnlyInMagic = val;
                ApplyRuneBarToUI(s);
            });
        }

        private void ApplyRuneBarToUI(WheelSettingsData data)
        {
            var wheelUI = FindAnyObjectByType<Fencing.FencingWheelUI>();
            if (wheelUI != null)
            {
                wheelUI.ApplySettings(data);
            }
        }

        private void UpdateDeadZoneSliderVisuals(bool isEnabled)
        {
            if (deadZoneAngleSlider == null) return;
            deadZoneAngleSlider.interactable = isEnabled;

            var parentT = deadZoneAngleSlider.transform.parent;
            if (parentT != null)
            {
                var cg = parentT.GetComponent<CanvasGroup>();
                if (cg == null) cg = parentT.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = isEnabled ? 1f : 0.45f;
            }
        }

        private void OnResetClicked()
        {
            if (CombatSettingsManager.Instance != null)
            {
                CombatSettingsManager.Instance.ResetToDefaults();
                UpdateUIFromSettings(CombatSettingsManager.Instance.CurrentSettings);
            }
        }

        private void InitializeAllToggleStyles()
        {
            var data = GetCurrentData();
            if (muteAudioToggle != null) UpdateToggleVisuals(muteAudioToggle, data.isMuted);
            if (showHitboxesToggle != null) UpdateToggleVisuals(showHitboxesToggle, data.showHitboxes);
            if (showWheelToggle != null) UpdateToggleVisuals(showWheelToggle, data.showFencingWheel);
            if (enableDeadZoneToggle != null) UpdateToggleVisuals(enableDeadZoneToggle, data.enableDeadZone);
            if (showRuneBarOnlyInMagicToggle != null) UpdateToggleVisuals(showRuneBarOnlyInMagicToggle, data.showRuneBarOnlyInMagic);
            UpdateDeadZoneSliderVisuals(data.enableDeadZone);
            UpdatePlacementButtonsVisuals(data.wheelPlacement);
        }

        private void UpdateToggleVisuals(Toggle toggle, bool isOn)
        {
            if (toggle == null) return;
            var bg = toggle.targetGraphic as Image;
            if (bg != null)
            {
                bg.color = isOn ? new Color(0.12f, 0.45f, 0.85f, 1f) : new Color(0.16f, 0.20f, 0.28f, 0.9f);
            }

            var check = toggle.graphic as Image;
            if (check != null)
            {
                check.color = isOn ? Color.white : new Color(1f, 1f, 1f, 0f);
            }
        }

        #region Tutorial Mode Layout & Guidance
        public void ApplyTutorialModeLayout()
        {
            if (settingsPanel == null) return;

            var body = settingsPanel.transform.Find("SettingsWindow/Body");
            if (body == null) return;

            var colFencing = body.Find("ColFencing");
            var divider1 = body.Find("Divider1");
            var colAudio = body.Find("ColAudio");
            var divider2 = body.Find("Divider2");
            var colCombat = body.Find("ColCombat");

            // 1. Скрываем раздел с фокусом, стрелками магии и хитбоксами в обучении
            if (colCombat != null) colCombat.gameObject.SetActive(false);
            if (divider2 != null) divider2.gameObject.SetActive(false);

            // 2. Расширяем колонки колеса фехтования и звука на 2 равные просторные части
            if (colFencing != null)
            {
                var fRt = colFencing.GetComponent<RectTransform>();
                if (fRt != null)
                {
                    fRt.anchorMin = new Vector2(0f, 0f);
                    fRt.anchorMax = new Vector2(0.48f, 1f);
                    fRt.offsetMin = Vector2.zero;
                    fRt.offsetMax = Vector2.zero;
                }
            }

            if (divider1 != null)
            {
                var dRt = divider1.GetComponent<RectTransform>();
                if (dRt != null)
                {
                    dRt.anchorMin = new Vector2(0.50f, 0f);
                    dRt.anchorMax = new Vector2(0.50f, 1f);
                    dRt.anchoredPosition = Vector2.zero;
                    dRt.sizeDelta = new Vector2(2f, 0f);
                }
            }

            if (colAudio != null)
            {
                var aRt = colAudio.GetComponent<RectTransform>();
                if (aRt != null)
                {
                    aRt.anchorMin = new Vector2(0.52f, 0f);
                    aRt.anchorMax = new Vector2(1f, 1f);
                    aRt.offsetMin = Vector2.zero;
                    aRt.offsetMax = Vector2.zero;
                }
            }

            // 3. В разделе колеса фехтования скрываем все лишние настройки
            if (colFencing != null)
            {
                string[] toHide = {
                    "PlacementGroup", "WheelSizeGroup", "OrbSectionHeader", "OrbWeightGroup", "OrbSpeedGroup",
                    "FreeZoneSectionHeader", "FreeZoneRadiusGroup", "AttackSectionHeader", "AttackAngleGroup",
                    "DeadZoneSectionHeader", "EnableDeadZoneToggle", "DeadZoneAngleGroup"
                };
                foreach (var name in toHide)
                {
                    var t = colFencing.Find(name);
                    if (t != null) t.gameObject.SetActive(false);
                }

                // Заголовок секции
                var title = colFencing.Find("SecTitleFencing");
                if (title != null)
                {
                    var txt = title.GetComponent<Text>();
                    if (txt != null)
                    {
                        txt.text = "— БОЕВОЙ ИНТЕРФЕЙС —";
                        txt.fontSize = 16;
                        txt.fontStyle = FontStyle.Bold;
                        txt.color = new Color(1f, 0.78f, 0.2f, 1f);
                        txt.alignment = TextAnchor.MiddleCenter;
                    }
                    var rt = title.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchoredPosition = new Vector2(0f, -10f);
                        rt.sizeDelta = new Vector2(0f, 26f);
                    }
                }

                // Увеличенный карточный чекбокс колеса фехтования
                if (showWheelToggle != null)
                {
                    var tRt = showWheelToggle.GetComponent<RectTransform>();
                    if (tRt != null)
                    {
                        tRt.anchorMin = new Vector2(0f, 1f);
                        tRt.anchorMax = new Vector2(1f, 1f);
                        tRt.pivot = new Vector2(0.5f, 1f);
                        tRt.anchoredPosition = new Vector2(0f, -118f);
                        tRt.sizeDelta = new Vector2(-20f, 74f);
                    }

                    var cardImg = showWheelToggle.GetComponent<Image>();
                    if (cardImg == null) cardImg = showWheelToggle.gameObject.AddComponent<Image>();
                    cardImg.color = new Color(0.12f, 0.16f, 0.24f, 0.95f);

                    if (toggleOutline == null) toggleOutline = showWheelToggle.GetComponent<Outline>();
                    if (toggleOutline == null) toggleOutline = showWheelToggle.gameObject.AddComponent<Outline>();
                    toggleOutline.effectDistance = new Vector2(2f, -2f);

                    var bgT = showWheelToggle.transform.Find("Background");
                    if (bgT != null)
                    {
                        var bgRt = bgT.GetComponent<RectTransform>();
                        if (bgRt != null)
                        {
                            bgRt.anchorMin = new Vector2(0f, 0.5f);
                            bgRt.anchorMax = new Vector2(0f, 0.5f);
                            bgRt.pivot = new Vector2(0f, 0.5f);
                            bgRt.anchoredPosition = new Vector2(16f, 0f);
                            bgRt.sizeDelta = new Vector2(38f, 38f);
                        }

                        var checkT = bgT.Find("Checkmark");
                        if (checkT != null)
                        {
                            var cRt = checkT.GetComponent<RectTransform>();
                            if (cRt != null)
                            {
                                cRt.anchorMin = new Vector2(0.5f, 0.5f);
                                cRt.anchorMax = new Vector2(0.5f, 0.5f);
                                cRt.pivot = new Vector2(0.5f, 0.5f);
                                cRt.anchoredPosition = Vector2.zero;
                                cRt.sizeDelta = new Vector2(28f, 28f);
                            }
                        }
                    }

                    var lblT = showWheelToggle.transform.Find("Label");
                    if (lblT != null)
                    {
                        var lRt = lblT.GetComponent<RectTransform>();
                        if (lRt != null)
                        {
                            lRt.anchorMin = new Vector2(0f, 0f);
                            lRt.anchorMax = new Vector2(1f, 1f);
                            lRt.offsetMin = new Vector2(68f, 6f);
                            lRt.offsetMax = new Vector2(-12f, -6f);
                        }

                        var lTxt = lblT.GetComponent<Text>();
                        if (lTxt != null)
                        {
                            lTxt.fontSize = 17;
                            lTxt.fontStyle = FontStyle.Bold;
                            lTxt.alignment = TextAnchor.MiddleLeft;
                            lTxt.supportRichText = true;
                            lTxt.text = "Включить колесо фехтования\n<size=12><color=#88BBDD>Отображает колесо направлений ударов на экране</color></size>";
                        }
                    }
                }

                // Большая стрелка-указатель
                EnsureTutorialArrow(colFencing);
            }
        }

        private void EnsureTutorialArrow(Transform parent)
        {
            if (tutorialArrowPointer == null && parent != null)
            {
                var existing = parent.Find("TutorialArrowPointer");
                if (existing != null) tutorialArrowPointer = existing.gameObject;
            }

            if (tutorialArrowPointer == null && parent != null)
            {
                tutorialArrowPointer = new GameObject("TutorialArrowPointer", typeof(RectTransform));
                tutorialArrowPointer.transform.SetParent(parent, false);

                var aRt = tutorialArrowPointer.GetComponent<RectTransform>();
                aRt.anchorMin = new Vector2(0f, 1f);
                aRt.anchorMax = new Vector2(1f, 1f);
                aRt.pivot = new Vector2(0.5f, 1f);
                aRt.anchoredPosition = new Vector2(0f, -44f);
                aRt.sizeDelta = new Vector2(0f, 64f);

                var outline = tutorialArrowPointer.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            if (tutorialArrowPointer != null)
            {
                if (tutorialArrowText == null) tutorialArrowText = tutorialArrowPointer.GetComponent<Text>();
                if (tutorialArrowText == null) tutorialArrowText = tutorialArrowPointer.AddComponent<Text>();

                if (tutorialArrowText.font == null)
                {
                    tutorialArrowText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                tutorialArrowText.fontSize = 17;
                tutorialArrowText.fontStyle = FontStyle.Bold;
                tutorialArrowText.alignment = TextAnchor.MiddleCenter;

                tutorialArrowPointer.SetActive(true);
                UpdateTutorialArrowState(showWheelToggle != null && showWheelToggle.isOn);
            }
        }

        public void UpdateTutorialArrowState(bool isWheelOn)
        {
            if (tutorialArrowPointer == null) return;
            tutorialArrowPointer.SetActive(true);

            var iconL = tutorialArrowPointer.transform.Find("ArrowIcon");
            var iconR = tutorialArrowPointer.transform.Find("ArrowIconRight");

            if (tutorialArrowText != null)
            {
                if (isWheelOn)
                {
                    tutorialArrowText.text = "✓  КОЛЕСО ВКЛЮЧЕНО! ТЕПЕРЬ ЗАКРОЙТЕ МЕНЮ (ESC)";
                    tutorialArrowText.color = new Color(0.35f, 1.0f, 0.5f, 1f);
                    if (iconL != null) iconL.gameObject.SetActive(false);
                    if (iconR != null) iconR.gameObject.SetActive(false);
                }
                else
                {
                    tutorialArrowText.text = "НАЖМИТЕ СЮДА, ЧТОБЫ ВКЛЮЧИТЬ!";
                    tutorialArrowText.color = new Color(1f, 0.84f, 0.1f, 1f);
                    if (iconL != null) iconL.gameObject.SetActive(true);
                    if (iconR != null) iconR.gameObject.SetActive(true);
                }
            }
        }

        private void UpdateTutorialArrowAnimation()
        {
            if (tutorialArrowPointer == null || !tutorialArrowPointer.activeSelf) return;

            var rt = tutorialArrowPointer.GetComponent<RectTransform>();
            if (rt != null)
            {
                float bob = Mathf.Sin(Time.unscaledTime * 5.5f) * 8.0f;
                rt.anchoredPosition = new Vector2(0f, -44f + bob);
            }

            if (toggleOutline != null)
            {
                bool isWheelOn = (showWheelToggle != null && showWheelToggle.isOn);
                if (!isWheelOn)
                {
                    float pulse = (Mathf.Sin(Time.unscaledTime * 6f) + 1f) * 0.5f;
                    toggleOutline.effectColor = Color.Lerp(new Color(0.25f, 0.55f, 0.95f, 0.6f), new Color(1f, 0.84f, 0.1f, 1f), pulse);
                }
                else
                {
                    toggleOutline.effectColor = new Color(0.2f, 0.85f, 0.4f, 0.85f);
                }
            }
        }
        #endregion

        public void OnSkipTutorialClicked()
        {
            Debug.Log("<color=#FFAA00>[SettingsUI]</color> Нажата кнопка «Пропустить обучение»!");
            CloseSettings();

            var tut = Combat.Tutorial.TutorialManager.Instance;
            if (tut != null)
            {
                tut.SkipTutorial();
            }
            else
            {
                PlayerPrefs.SetInt("TutorialCompleted", 1);
                PlayerPrefs.Save();
                Time.timeScale = 1.0f;
                ScreenFadeTransition2D.FadeAndLoadScene("SampleScene", 0.35f);
            }
        }

        private void EnsureSkipTutorialButton()
        {
            if (settingsPanel == null) return;

            var footer = settingsPanel.transform.Find("SettingsWindow/Footer");
            if (footer == null) return;

            if (skipTutorialButton == null)
            {
                var existing = footer.Find("SkipTutorialButton");
                if (existing != null)
                {
                    skipTutorialButton = existing.GetComponent<Button>();
                }
            }

            if (skipTutorialButton == null)
            {
                var btnGo = new GameObject("SkipTutorialButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
                btnGo.transform.SetParent(footer, false);

                var rt = btnGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, 0f);
                rt.sizeDelta = new Vector2(240f, 34f);

                var img = btnGo.GetComponent<Image>();
                img.sprite = PlayerHealthBarUI.GetWhiteSprite();
                img.color = new Color(0.88f, 0.42f, 0.10f, 0.95f);

                var outline = btnGo.GetComponent<Outline>();
                outline.effectColor = new Color(1.0f, 0.82f, 0.25f, 0.95f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);

                var btn = btnGo.GetComponent<Button>();
                var colors = btn.colors;
                colors.highlightedColor = new Color(1.0f, 0.58f, 0.18f, 1f);
                colors.pressedColor = new Color(0.7f, 0.32f, 0.06f, 1f);
                btn.colors = colors;
                skipTutorialButton = btn;

                var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                textGo.transform.SetParent(btnGo.transform, false);
                var tRt = textGo.GetComponent<RectTransform>();
                tRt.anchorMin = Vector2.zero;
                tRt.anchorMax = Vector2.one;
                tRt.offsetMin = Vector2.zero;
                tRt.offsetMax = Vector2.zero;

                var txt = textGo.GetComponent<Text>();
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                txt.fontSize = 13;
                txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.text = "ПРОПУСТИТЬ ОБУЧЕНИЕ ⏩";
                txt.color = Color.white;

                var to = textGo.AddComponent<Outline>();
                to.effectColor = new Color(0f, 0f, 0f, 0.85f);
                to.effectDistance = new Vector2(1f, -1f);
            }

            if (skipTutorialButton != null)
            {
                skipTutorialButton.onClick.RemoveListener(OnSkipTutorialClicked);
                skipTutorialButton.onClick.AddListener(OnSkipTutorialClicked);

                bool inTutorial = isTutorialMode || Combat.Tutorial.TutorialManager.Instance != null;
                skipTutorialButton.gameObject.SetActive(inTutorial);

                var hint = footer.Find("HintText");
                if (hint != null)
                {
                    hint.gameObject.SetActive(!inTutorial);
                }
            }
        }
}

    }