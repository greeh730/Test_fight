using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Combat;
using Combat.Settings;
using Combat.Stances;

namespace Combat.UI
{
    public enum MouseTrackingMode
    {
        /// <summary>
        /// Игрок зажимает ЛКМ и вычерчивает кривую траекторию внутри колеса в реальном времени.
        /// При отпускании ЛКМ шлейф и подсветка плавно гаснут.
        /// </summary>
        LMBDragSwipe = 0,

        /// <summary>
        /// Положение свободного курсора мыши относительно центра колеса на экране.
        /// </summary>
        RelativeWheelCenter = 1,

        /// <summary>
        /// Вектор смещения/взмаха мыши (Mouse Delta, как в For Honor / Mount & Blade).
        /// Подходит при заблокированном или скрытом курсоре.
        /// </summary>
        MouseDelta = 2,

        /// <summary>
        /// Положение курсора относительно центра экрана.
        /// </summary>
        RelativeScreenCenter = 3
    }

    public enum WheelGestureButton
    {
        None = 0,
        LMB = 1,
        RMB = 2
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class VectorWheelController : MonoBehaviour
    {
        [Header("--- Input Settings ---")]
        [Tooltip("Режим отслеживания мыши")]
        [SerializeField] private MouseTrackingMode trackingMode = MouseTrackingMode.LMBDragSwipe;

        [Tooltip("Мертвая зона / минимальное смещение от центра для фиксации сектора")]
        [SerializeField] private float minSwipeDistance = 25f;

        [Tooltip("Радиус внутренней зоны колеса для движения пера")]
        [SerializeField] private float wheelRadius = 85f;

        [Tooltip("Чувствительность перемещения пера от движения мыши")]
        [SerializeField] private float mouseMotionScale = 0.85f;

        [Tooltip("Чувствительность накопления дельты (для режима MouseDelta)")]
        [SerializeField] private float deltaSensitivity = 2.5f;

        [Tooltip("Скорость затухания дельты со временем (для режима MouseDelta)")]
        [SerializeField] private float deltaDecay = 8f;

        [Tooltip("Скорость плавного гашения подсветки колеса после отпускания ЛКМ")]
        [SerializeField] private float fadeOutSpeed = 3.2f;

        [Header("--- UI References ---")]
        [SerializeField] private RectTransform wheelRect;
        [SerializeField] private Image wheelOutlineImage;
        [SerializeField] private Image sectorHighlightImage;
        [SerializeField] private Image centerCoreImage;
        [SerializeField] private WheelTrailGraphic wheelTrailGraphic;

        [Header("--- 8-Way Motion Sequence Recognition ---")]
        [SerializeField] private DirectionSequenceRecognizer sequenceRecognizer;
        [SerializeField] private PlayerCombatController2D playerCombat;
        [SerializeField] private WheelDirectionArrowsOverlay arrowsOverlay;

        [Header("--- Plaque HUD (Под колесом) ---")]
        [SerializeField] private CanvasGroup plaqueCanvasGroup;
        [SerializeField] private RectTransform plaqueRect;
        [SerializeField] private Text attackNameText;
        [SerializeField] private string neutralStanceName = "— БОЕВАЯ СТОЙКА —";

        [Header("--- Visual Palette ---")]
        [SerializeField] private Color outlineColor = Color.white;
        [SerializeField] private Color highlightColor = new Color(1f, 0.12f, 0.25f, 0.65f); // Crimson Neon
        [SerializeField] private Color centerCoreColor = Color.white;
        [SerializeField] private Color textActiveColor = new Color(1f, 0.95f, 0.95f, 1f);
        [SerializeField] private Color textIdleColor = new Color(0.9f, 0.35f, 0.45f, 0.6f);

        [Header("--- Juiciness & Game Feel ---")]
        [SerializeField] private float activeScaleMultiplier = 1.025f; // Плавное увеличение на 2.5%
        [SerializeField] private float scaleSmoothSpeed = 8f;         // Мягкая интерполяция без резких скачков
        [SerializeField] private float sectorPulseSpeed = 6f;

        [Header("--- Events ---")]
        public UnityEvent<Direction8> onDirectionChanged;
        public UnityEvent<Vector2> onVectorChanged;

        [Serializable]
        public class SwipeCompletedEvent : UnityEvent<Direction8, Vector2, float> { }
        public SwipeCompletedEvent onSwipeCompleted;

        [Serializable]
        public class GesturePathEvent : UnityEvent<IReadOnlyList<Vector2>, Direction8> { }
        public GesturePathEvent onGesturePathCompleted;

        [Serializable]
        public class ParrySwipeCompletedEvent : UnityEvent<Direction8, Vector2, float> { }
        [Header("--- Parry & Defense Events (ПКМ) ---")]
        public ParrySwipeCompletedEvent onParrySwipeCompleted;
        public UnityEvent<bool> onBlockStateChanged;

        // Runtime State
        public WheelGestureButton ActiveGestureButton { get; private set; } = WheelGestureButton.None;
        public Direction8 CurrentDirection { get; private set; } = Direction8.None;
        public Vector2 CurrentVector { get; private set; } = Vector2.zero;
        public float CurrentRawAngle { get; private set; } = 0f;
        public bool IsDragging { get; private set; } = false;

        public DirectionSequenceRecognizer SequenceRecognizer => sequenceRecognizer;
        public WheelDirectionArrowsOverlay ArrowsOverlay => arrowsOverlay;
        public float FadeAlpha => _fadeAlpha;
        public float WheelRadius => wheelRadius;
        public Color HighlightColor => highlightColor;
        public WheelSettingsData CurrentSettings => _currentSettings;

        // Legacy Charge stubs
        public float DirectionHoldTimer => 0f;
        public bool IsChargeReady => false;
        public float ChargeProgress => 0f;
        public bool ConsumeCharge() => false;

        private Vector2 _penPosition = Vector2.zero;
        private Vector2 _lastMousePos = Vector2.zero;
        private Vector2 _accumulatedDelta = Vector2.zero;
        private float _fadeAlpha = 0f;
        private float _currentScale = 1.0f;
        private float _targetScale = 1.0f;
        private float _flashIntensity = 0f;
        private float _textPunchScale = 1.0f;

        private Camera _canvasCamera;
        private Canvas _parentCanvas;
        private WheelSettingsData _currentSettings = new WheelSettingsData();

        private void Awake()
        {
            if (wheelRect == null)
            {
                wheelRect = GetComponent<RectTransform>();
            }

            if (wheelTrailGraphic == null)
            {
                wheelTrailGraphic = GetComponentInChildren<WheelTrailGraphic>();
            }

            if (sequenceRecognizer == null)
            {
                sequenceRecognizer = GetComponent<DirectionSequenceRecognizer>() ?? FindAnyObjectByType<DirectionSequenceRecognizer>();
                if (sequenceRecognizer == null)
                {
                    sequenceRecognizer = gameObject.AddComponent<DirectionSequenceRecognizer>();
                }
            }

            if (playerCombat == null)
            {
                playerCombat = FindAnyObjectByType<PlayerCombatController2D>();
            }

            if (playerCombat != null && sequenceRecognizer != null)
            {
                playerCombat.SetSequenceRecognizer(sequenceRecognizer);
            }

            if (arrowsOverlay == null)
            {
                arrowsOverlay = GetComponentInChildren<WheelDirectionArrowsOverlay>();
            }

            _parentCanvas = GetComponentInParent<Canvas>();
            if (_parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                _canvasCamera = _parentCanvas.worldCamera != null ? _parentCanvas.worldCamera : Camera.main;
            }

            ApplyVisualColors();
            UpdatePlaqueText(Direction8.None);
        }

        private void OnEnable()
        {
            CombatSettingsManager.OnSettingsChanged += ApplySettings;
            if (CombatSettingsManager.Instance != null)
            {
                ApplySettings(CombatSettingsManager.Instance.CurrentSettings);
            }

            if (sequenceRecognizer != null)
            {
                sequenceRecognizer.OnBufferChanged -= HandleRecognizerBufferChanged;
                sequenceRecognizer.OnBufferChanged += HandleRecognizerBufferChanged;
                sequenceRecognizer.OnSequenceMatched -= HandleRecognizerSequenceMatched;
                sequenceRecognizer.OnSequenceMatched += HandleRecognizerSequenceMatched;
            }
        }

        private void OnDisable()
        {
            CombatSettingsManager.OnSettingsChanged -= ApplySettings;

            if (sequenceRecognizer != null)
            {
                sequenceRecognizer.OnBufferChanged -= HandleRecognizerBufferChanged;
                sequenceRecognizer.OnSequenceMatched -= HandleRecognizerSequenceMatched;
            }
        }

        public void ApplySettings(WheelSettingsData data)
        {
            if (data == null) return;
            _currentSettings = data;

            mouseMotionScale = 0.85f * data.gestureSensitivity;
            minSwipeDistance = data.deadzone;

            // Визуальный радиус мертвой зоны: центральное кольцо точно соответствует deadzone
            if (centerCoreImage != null)
            {
                centerCoreImage.rectTransform.sizeDelta = Vector2.one * (minSwipeDistance * 2f);
            }

            // 1. Позиционирование (Placement)
            if (wheelRect != null)
            {
                switch (data.placement)
                {
                    case WheelPlacement.CenterScreen:
                        wheelRect.anchorMin = new Vector2(0.5f, 0.5f);
                        wheelRect.anchorMax = new Vector2(0.5f, 0.5f);
                        wheelRect.pivot = new Vector2(0.5f, 0.5f);
                        wheelRect.anchoredPosition = Vector2.zero;
                        break;

                    case WheelPlacement.BottomLeft:
                        wheelRect.anchorMin = new Vector2(0f, 0f);
                        wheelRect.anchorMax = new Vector2(0f, 0f);
                        wheelRect.pivot = new Vector2(0f, 0f);
                        wheelRect.anchoredPosition = new Vector2(40f, 90f);
                        break;

                    case WheelPlacement.BottomRight:
                    default:
                        wheelRect.anchorMin = new Vector2(1f, 0f);
                        wheelRect.anchorMax = new Vector2(1f, 0f);
                        wheelRect.pivot = new Vector2(1f, 0f);
                        wheelRect.anchoredPosition = new Vector2(-40f, 90f);
                        break;
                }
            }

            // 2. Отображение плашки атак
            if (plaqueCanvasGroup != null)
            {
                plaqueCanvasGroup.gameObject.SetActive(data.showAttackPlaque);
            }

            // 3. Цветовая палитра
            WheelSettingsData.GetThemeColors(data.colorTheme, out Color primary, out Color secondary, out Color core);
            highlightColor = primary;
            centerCoreColor = core;
            if (wheelTrailGraphic != null)
            {
                wheelTrailGraphic.SetThemeColors(primary, core, core);
            }
            if (centerCoreImage != null)
            {
                centerCoreImage.color = core;
            }
            if (plaqueRect != null && plaqueRect.TryGetComponent<Image>(out var plqImg))
            {
                plqImg.color = primary;
            }
        }

        private void Start()
        {
            if (playerCombat != null)
            {
                SetStance(playerCombat.CurrentStance);
            }
            else
            {
                SetStance(CombatStance.Normal);
            }
            UpdateVisuals(instant: true);
        }

        public void CancelDrag()
        {
            if (ActiveGestureButton == WheelGestureButton.RMB)
            {
                onBlockStateChanged?.Invoke(false);
            }
            IsDragging = false;
            ActiveGestureButton = WheelGestureButton.None;
            CurrentDirection = Direction8.None;
            CurrentVector = Vector2.zero;
            if (wheelTrailGraphic != null)
            {
                wheelTrailGraphic.ClearInstant();
            }
            if (sequenceRecognizer != null)
            {
                sequenceRecognizer.ClearBuffer();
            }
            UpdatePlaqueText(Direction8.None);
        }

        private void Update()
        {
            if (PauseMenuController.IsGamePaused || Time.timeScale <= 0.0001f)
            {
                if (IsDragging)
                {
                    CancelDrag();
                }
                return;
            }

            if (trackingMode == MouseTrackingMode.LMBDragSwipe)
            {
                HandleGestureTrail();
            }
            else
            {
                HandleContinuousModes();
            }

            UpdateJuiceAnimations();
            UpdateVisuals(instant: false);
        }

        private void HandleGestureTrail()
        {
            // Если курсор мыши находится над элементом интерфейса, не начинаем жест колеса
            if (!IsDragging && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Vector2 mousePos = GetMousePosition();
            bool isToggle = _currentSettings != null && _currentSettings.activationMode == ActivationMode.Toggle;

            // 1. Активация жеста
            if (!IsDragging)
            {
                if (IsLMBDown())
                {
                    ActiveGestureButton = WheelGestureButton.LMB;
                    StartDrag(mousePos);
                }
                else if (IsRMBDown())
                {
                    ActiveGestureButton = WheelGestureButton.RMB;
                    StartDrag(mousePos);
                    onBlockStateChanged?.Invoke(true);
                    UpdatePlaqueText(Direction8.None);
                }
            }
            else if (isToggle && ActiveGestureButton == WheelGestureButton.LMB && IsLMBDown())
            {
                ExecuteSwipeComplete();
                return;
            }

            // 2. Движение мыши или правого стика геймпада
            bool isHeld = (ActiveGestureButton == WheelGestureButton.LMB && (isToggle || IsLMBHeld()))
                       || (ActiveGestureButton == WheelGestureButton.RMB && IsRMBHeld());

            if (IsDragging && isHeld)
            {
                Vector2 mouseDelta = mousePos - _lastMousePos;
                _lastMousePos = mousePos;

                // Инверсия осей
                if (_currentSettings != null)
                {
                    if (_currentSettings.invertX) mouseDelta.x = -mouseDelta.x;
                    if (_currentSettings.invertY) mouseDelta.y = -mouseDelta.y;
                }

                // Ввод с правого стика геймпада
                if (_currentSettings == null || _currentSettings.enableGamepad)
                {
                    Vector2 stick = GetGamepadRightStick();
                    if (stick.sqrMagnitude > 0.04f)
                    {
                        mouseDelta += stick * 250f * Time.deltaTime * (_currentSettings != null ? _currentSettings.gestureSensitivity : 1f);
                    }
                }

                if (mouseDelta.sqrMagnitude > 0.001f)
                {
                    _penPosition += mouseDelta * mouseMotionScale;
                    _penPosition = Vector2.ClampMagnitude(_penPosition, wheelRadius);

                    if (wheelTrailGraphic != null)
                    {
                        wheelTrailGraphic.AddPoint(_penPosition);
                    }
                }

                CurrentVector = _penPosition;
                float dist = _penPosition.magnitude;

                if (dist >= minSwipeDistance)
                {
                    _fadeAlpha = 1f;
                    CurrentRawAngle = Mathf.Repeat(Mathf.Atan2(_penPosition.y, _penPosition.x) * Mathf.Rad2Deg, 360f);
                    Direction8 newDir = EvaluateDirection(CurrentRawAngle);

                    if (newDir != CurrentDirection)
                    {
                        CurrentDirection = newDir;
                        _flashIntensity = 1f;

                        if (ActiveGestureButton == WheelGestureButton.LMB)
                        {
                            UpdatePlaqueText(CurrentDirection);

                            if (playerCombat != null && playerCombat.CurrentStance == CombatStance.Tactician)
                            {
                                // В стойке Тактика буфер связок не используется
                            }
                            else if (sequenceRecognizer != null)
                            {
                                float facing = playerCombat != null ? playerCombat.FacingDirection : 1f;
                                sequenceRecognizer.AddToken(CurrentDirection, facing);
                            }
                        }
                        else if (ActiveGestureButton == WheelGestureButton.RMB)
                        {
                            UpdatePlaqueText(CurrentDirection);
                        }

                        onDirectionChanged?.Invoke(CurrentDirection);
                    }

                    onVectorChanged?.Invoke(_penPosition.normalized);
                }
                else
                {
                    if (CurrentDirection != Direction8.None)
                    {
                        CurrentDirection = Direction8.None;
                        if (ActiveGestureButton == WheelGestureButton.RMB || (playerCombat != null && playerCombat.CurrentStance == CombatStance.Tactician))
                        {
                            UpdatePlaqueText(Direction8.None);
                        }
                        onDirectionChanged?.Invoke(Direction8.None);
                    }
                }
            }

            // 3. Завершение жеста в режиме Hold
            if (IsDragging)
            {
                if (ActiveGestureButton == WheelGestureButton.LMB && !isToggle && IsLMBUp())
                {
                    ExecuteSwipeComplete();
                }
                else if (ActiveGestureButton == WheelGestureButton.RMB && IsRMBUp())
                {
                    ExecuteRMBComplete();
                }
            }

            // 4. Плавное затухание в покое
            if (!IsDragging)
            {
                if (_fadeAlpha > 0f)
                {
                    _fadeAlpha = Mathf.MoveTowards(_fadeAlpha, 0f, Time.deltaTime * fadeOutSpeed);
                    if (_fadeAlpha <= 0.01f)
                    {
                        _fadeAlpha = 0f;
                        CurrentDirection = Direction8.None;
                        UpdatePlaqueText(Direction8.None);
                    }
                }
            }
        }

        private void StartDrag(Vector2 mousePos)
        {
            IsDragging = true;
            _penPosition = Vector2.zero;
            _lastMousePos = mousePos;
            _fadeAlpha = 1f;

            if (wheelTrailGraphic != null)
            {
                wheelTrailGraphic.StartNewTrail(Vector2.zero);
            }
        }

        private void ExecuteSwipeComplete()
        {
            if (!IsDragging) return;

            float dist = _penPosition.magnitude;
            if (CurrentDirection != Direction8.None)
            {
                onSwipeCompleted?.Invoke(CurrentDirection, _penPosition.normalized, dist);
                if (wheelTrailGraphic != null)
                {
                    onGesturePathCompleted?.Invoke(wheelTrailGraphic.Points, CurrentDirection);
                }
            }

            if (sequenceRecognizer != null)
            {
                sequenceRecognizer.ClearBuffer();
            }

            if (wheelTrailGraphic != null)
            {
                wheelTrailGraphic.FadeOut();
            }

            IsDragging = false;
            ActiveGestureButton = WheelGestureButton.None;
            CurrentVector = Vector2.zero;
            UpdatePlaqueText(Direction8.None);
        }

        private void ExecuteRMBComplete()
        {
            if (!IsDragging) return;

            float dist = _penPosition.magnitude;
            Direction8 releasedDir = CurrentDirection;
            Vector2 releasedVec = _penPosition.normalized;

            if (dist >= minSwipeDistance && releasedDir != Direction8.None)
            {
                // Игрок выбрал направление на колесе и отпустил ПКМ -> Направленное Парирование!
                onParrySwipeCompleted?.Invoke(releasedDir, releasedVec, dist);
            }

            // В любом случае блок снимается
            onBlockStateChanged?.Invoke(false);

            if (wheelTrailGraphic != null)
            {
                wheelTrailGraphic.FadeOut();
            }

            IsDragging = false;
            ActiveGestureButton = WheelGestureButton.None;
            CurrentVector = Vector2.zero;
            UpdatePlaqueText(Direction8.None);
        }

        private void HandleContinuousModes()
        {
            Vector2 mouseScreenPos = GetMousePosition();
            Vector2 inputVec = Vector2.zero;

            switch (trackingMode)
            {
                case MouseTrackingMode.RelativeWheelCenter:
                {
                    Vector2 wheelScreenPos = (_parentCanvas != null && _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                        ? (Vector2)wheelRect.position
                        : (Vector2)RectTransformUtility.WorldToScreenPoint(_canvasCamera, wheelRect.position);
                    inputVec = mouseScreenPos - wheelScreenPos;
                    break;
                }

                case MouseTrackingMode.RelativeScreenCenter:
                {
                    Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                    inputVec = mouseScreenPos - screenCenter;
                    break;
                }

                case MouseTrackingMode.MouseDelta:
                {
                    Vector2 delta = GetMouseDelta() * deltaSensitivity;
                    if (delta.sqrMagnitude > 0.001f)
                    {
                        _accumulatedDelta += delta;
                        if (_accumulatedDelta.magnitude > 100f)
                        {
                            _accumulatedDelta = _accumulatedDelta.normalized * 100f;
                        }
                    }
                    else
                    {
                        _accumulatedDelta = Vector2.Lerp(_accumulatedDelta, Vector2.zero, Time.deltaTime * deltaDecay);
                    }
                    inputVec = _accumulatedDelta;
                    break;
                }
            }

            CurrentVector = inputVec;
            float mag = inputVec.magnitude;

            if (mag >= minSwipeDistance)
            {
                _fadeAlpha = 1f;
                CurrentRawAngle = Mathf.Repeat(Mathf.Atan2(inputVec.y, inputVec.x) * Mathf.Rad2Deg, 360f);
                Direction8 newDir = EvaluateDirection(CurrentRawAngle);

                if (newDir != CurrentDirection)
                {
                    CurrentDirection = newDir;
                    _flashIntensity = 1f;
                    if (ActiveGestureButton == WheelGestureButton.LMB)
                    {
                        UpdatePlaqueText(CurrentDirection);

                        if (playerCombat != null && playerCombat.CurrentStance == CombatStance.Tactician)
                        {
                            // В стойке Тактика буфер связок не используется
                        }
                        else if (sequenceRecognizer != null)
                        {
                            float facing = playerCombat != null ? playerCombat.FacingDirection : 1f;
                            sequenceRecognizer.AddToken(CurrentDirection, facing);
                        }
                    }
                    else if (ActiveGestureButton == WheelGestureButton.RMB)
                    {
                        UpdatePlaqueText(CurrentDirection);
                    }
                    onDirectionChanged?.Invoke(CurrentDirection);
                }

                onVectorChanged?.Invoke(inputVec.normalized);
            }
            else
            {
                _fadeAlpha = Mathf.MoveTowards(_fadeAlpha, 0f, Time.deltaTime * fadeOutSpeed);
                if (_fadeAlpha <= 0.01f)
                {
                    CurrentDirection = Direction8.None;
                    UpdatePlaqueText(Direction8.None);
                }
            }
        }

        /// <summary>
        /// Определяет сектор направления Direction8 с учетом "умного магнетизма" к возможным продолжениям комбо.
        /// </summary>
        private Direction8 EvaluateDirection(float angleDeg)
        {
            ICollection<Direction8> favoredDirs = null;
            if (ActiveGestureButton == WheelGestureButton.LMB && sequenceRecognizer != null && sequenceRecognizer.CurrentBuffer.Count > 0
                && (playerCombat == null || playerCombat.CurrentStance == CombatStance.Normal))
            {
                favoredDirs = CombatSequenceLibrary.Instance.GetPossibleNextDirections(sequenceRecognizer.CurrentBuffer, sequenceRecognizer.CurrentBuffer.Count);
            }

            return Direction8Extensions.FromAngleWithMagnetism(angleDeg, favoredDirs);
        }

        private void UpdateJuiceAnimations()
        {
            // Плавная и мягкая интерполяция масштаба колеса (без резких рывков)
            float baseScale = _currentSettings != null ? _currentSettings.hudScale : 1.0f;
            _targetScale = (IsDragging ? activeScaleMultiplier : 1.0f) * baseScale;
            _currentScale = Mathf.Lerp(_currentScale, _targetScale, Time.deltaTime * scaleSmoothSpeed);
            if (wheelRect != null)
            {
                wheelRect.localScale = Vector3.one * _currentScale;
            }

            // Затухание вспышки переключения сектора
            if (_flashIntensity > 0f)
            {
                _flashIntensity = Mathf.MoveTowards(_flashIntensity, 0f, Time.deltaTime * 6f);
            }

            // Пружина масштаба текста плашки
            if (_textPunchScale > 1.0f)
            {
                _textPunchScale = Mathf.MoveTowards(_textPunchScale, 1.0f, Time.deltaTime * 3.5f);
                if (attackNameText != null)
                {
                    attackNameText.transform.localScale = Vector3.one * _textPunchScale;
                }
            }
        }

        private void UpdateVisuals(bool instant)
        {
            float idleAlpha = _currentSettings != null ? _currentSettings.idleOpacity : 0.45f;
            float effectiveOutlineAlpha = Mathf.Lerp(idleAlpha, 1.0f, _fadeAlpha);

            // 0. Внешний контур колеса с учетом idleOpacity
            if (wheelOutlineImage != null)
            {
                Color outCol = outlineColor;
                outCol.a = effectiveOutlineAlpha;
                wheelOutlineImage.color = outCol;
            }

            // 1. Сектор подсветки с эффектом дыхания и вспышкой
            if (sectorHighlightImage != null)
            {
                if (CurrentDirection != Direction8.None)
                {
                    float pulse = 1f + 0.18f * Mathf.Sin(Time.time * sectorPulseSpeed);
                    Color baseCol = (ActiveGestureButton == WheelGestureButton.RMB)
                        ? new Color(1f, 0.85f, 0.22f, 0.85f)
                        : highlightColor;

                    Color col = Color.Lerp(baseCol, Color.white, _flashIntensity * 0.7f);
                    col.a = Mathf.Clamp01(baseCol.a * _fadeAlpha * pulse);
                    sectorHighlightImage.color = col;

                    float targetSectorAngle = CurrentDirection.ToAngle();
                    sectorHighlightImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, targetSectorAngle);
                }
                else
                {
                    Color col = (ActiveGestureButton == WheelGestureButton.RMB)
                        ? new Color(1f, 0.85f, 0.22f, 0f)
                        : highlightColor;
                    col.a = 0f;
                    sectorHighlightImage.color = col;
                }
            }

            // 2. Центральное ядро-реактор
            if (centerCoreImage != null)
            {
                Color baseCore = (ActiveGestureButton == WheelGestureButton.RMB)
                    ? new Color(0.2f, 0.85f, 1f, 1f)
                    : centerCoreColor;
                Color coreCol = baseCore;
                coreCol.a = effectiveOutlineAlpha * (0.5f + 0.5f * _fadeAlpha);
                centerCoreImage.color = coreCol;
            }

            // 3. Плашка названия атаки под колесом
            if (plaqueCanvasGroup != null)
            {
                float targetPlaqueAlpha = IsDragging || _fadeAlpha > 0.05f ? 1.0f : Mathf.Min(0.45f, idleAlpha + 0.1f);
                plaqueCanvasGroup.alpha = Mathf.MoveTowards(plaqueCanvasGroup.alpha, targetPlaqueAlpha, Time.deltaTime * 6f);
            }
        }

        private void HandleRecognizerBufferChanged(IReadOnlyList<Direction8> buffer)
        {
            if (ActiveGestureButton != WheelGestureButton.LMB) return;

            if (buffer == null || buffer.Count == 0)
            {
                if (attackNameText != null)
                {
                    attackNameText.text = neutralStanceName;
                    attackNameText.color = textIdleColor;
                }
            }
            else
            {
                _textPunchScale = 1.15f;
                if (attackNameText != null)
                {
                    attackNameText.text = $"[ {buffer.ToGlyphString(" ")} ]";
                    attackNameText.color = textActiveColor;
                }
            }
        }

        private void HandleRecognizerSequenceMatched(ComboSequenceDefinition seq, bool isStale)
        {
            _textPunchScale = 1.35f;
            if (attackNameText != null)
            {
                string staleNotice = isStale ? " <color=#FF4444>[ПРИВЫКАНИЕ!]</color>" : "";
                attackNameText.text = $"<color=#FFD700>{seq.SequenceName}</color> [{seq.GlyphPattern}]{staleNotice}";
                attackNameText.color = isStale ? new Color(1f, 0.45f, 0.45f, 1f) : new Color(1f, 0.9f, 0.2f, 1f);
            }
        }

        private void UpdatePlaqueText(Direction8 dir)
        {
            if (attackNameText == null) return;

            _textPunchScale = 1.14f;

            if (ActiveGestureButton == WheelGestureButton.RMB)
            {
                if (dir == Direction8.None)
                {
                    attackNameText.text = "🛡️ НАПРАВЛЕННЫЙ БЛОК (ПКМ)";
                    attackNameText.color = new Color(0.25f, 0.88f, 1f, 1f);
                }
                else
                {
                    int idx = (int)dir;
                    if (idx >= 0 && idx < ParryDirectionNames.Length)
                    {
                        attackNameText.text = $"🛡️ ПАРИРОВАНИЕ {ParryDirectionNames[idx]}";
                        attackNameText.color = new Color(1f, 0.88f, 0.25f, 1f);
                    }
                }
                return;
            }

            if (playerCombat != null && playerCombat.CurrentStance == CombatStance.Tactician)
            {
                if (dir != Direction8.None && (int)dir >= 0 && (int)dir < _currentAttackNames.Length)
                {
                    attackNameText.text = _currentAttackNames[(int)dir];
                    attackNameText.color = highlightColor;
                }
                else
                {
                    attackNameText.text = neutralStanceName;
                    attackNameText.color = textIdleColor;
                }
                return;
            }

            if (sequenceRecognizer != null && sequenceRecognizer.CurrentBuffer.Count > 0)
            {
                attackNameText.text = $"[ {sequenceRecognizer.GetBufferGlyphString()} ]";
                attackNameText.color = textActiveColor;
            }
            else if (dir != Direction8.None && (int)dir >= 0 && (int)dir < _currentAttackNames.Length)
            {
                attackNameText.text = _currentAttackNames[(int)dir];
                attackNameText.color = highlightColor;
            }
            else
            {
                attackNameText.text = neutralStanceName;
                attackNameText.color = textIdleColor;
            }
        }

        public void SetAttackPlaqueWithCombo(string attackName, int comboStep, bool isFinisher, bool isCharged = false)
        {
            if (attackNameText == null) return;

            _textPunchScale = isFinisher ? 1.35f : (comboStep > 1 ? 1.18f : 1.05f);

            string badge = comboStep switch
            {
                1 => "[УДАР 1]",
                2 => "<color=#FFD700>[КОМБО 2]</color>",
                _ => isFinisher ? "<color=#FF2222>★ ФИНИШЕР x3 ★</color>" : $"<color=#FF7722>[КОМБО {comboStep}]</color>"
            };

            attackNameText.text = $"{attackName} {badge}";
            attackNameText.color = isFinisher ? new Color(1f, 0.25f, 0.25f, 1f) : textActiveColor;
        }

        public void SetAttackPlaqueWithSequence(string attackName, string glyphs, int comboStep, bool isStale)
        {
            if (attackNameText == null) return;

            _textPunchScale = isStale ? 1.15f : (comboStep > 1 ? 1.28f : 1.12f);
            string staleBadge = isStale ? " <color=#FF4444>[ПРИВЫКАНИЕ! +75% СТАМИНА ВРАГА]</color>" : "";
            string stepBadge = comboStep > 1 ? $" <color=#FFD700>[СВЯЗКА x{comboStep}]</color>" : "";

            attackNameText.text = $"{attackName} [{glyphs}]{stepBadge}{staleBadge}";
            attackNameText.color = isStale ? new Color(1f, 0.45f, 0.45f, 1f) : textActiveColor;
        }

        public void ApplyVisualColors()
        {
            if (wheelOutlineImage != null) wheelOutlineImage.color = outlineColor;
            if (sectorHighlightImage != null) sectorHighlightImage.color = highlightColor;
            if (centerCoreImage != null) centerCoreImage.color = centerCoreColor;
        }

        private static readonly string[] DefaultNormalAttackNames = new string[8]
        {
            "ВПРАВО ▶",       // Right (0)
            "ВВЕРХ-ВПРАВО ↗", // UpRight (1)
            "ВВЕРХ ▲",         // Up (2)
            "ВВЕРХ-ВЛЕВО ↖",   // UpLeft (3)
            "ВЛЕВО ◀",        // Left (4)
            "ВНИЗ-ВЛЕВО ↙",    // DownLeft (5)
            "ВНИЗ ▼",          // Down (6)
            "ВНИЗ-ВПРАВО ↘"    // DownRight (7)
        };

        private static readonly string[] TacticianAttackNames = new string[8]
        {
            "ПРОЩУПЫВАЮЩИЙ ВЫПАД ➡️", // Right (0)
            "КИНЕТИЧЕСКИЙ ПОДБРОС ↗️", // UpRight (1)
            "ГРАВИТАЦИОННЫЙ ЯКОРЬ ⬆️", // Up (2)
            "ВЕЕРНАЯ ЗАЩИТА ↖️",     // UpLeft (3)
            "ТАКТИЧЕСКИЙ ОТХОД ⬅️",   // Left (4)
            "МАГИЧЕСКИЙ ГАРПУН ↙️",   // DownLeft (5)
            "ГЛУБИННАЯ ПЕЧАТЬ ⬇️",    // Down (6)
            "НАПРАВЛЕННЫЕ ШИПЫ ↘️"    // DownRight (7)
        };

        private string[] _currentAttackNames = (string[])DefaultNormalAttackNames.Clone();

        private static readonly string[] ParryDirectionNames = new string[8]
        {
            "ВПРАВО ▶",       // Right (0)
            "ВВЕРХ-ВПРАВО ↗", // UpRight (1)
            "ВВЕРХ ▲",         // Up (2)
            "ВВЕРХ-ВЛЕВО ↖",   // UpLeft (3)
            "ВЛЕВО ◀",        // Left (4)
            "ВНИЗ-ВЛЕВО ↙",    // DownLeft (5)
            "ВНИЗ ▼",          // Down (6)
            "ВНИЗ-ВПРАВО ↘"    // DownRight (7)
        };

        /// <summary>
        /// Переключает отображение колеса и плашки под выбранную боевую стойку
        /// </summary>
        public void SetStance(CombatStance stance)
        {
            if (stance == CombatStance.Tactician)
            {
                _currentAttackNames = (string[])TacticianAttackNames.Clone();
                neutralStanceName = "— СТОЙКА ТАКТИКА —";
                highlightColor = new Color(0f, 0.85f, 1f, 0.75f); // Runic Cyan
                outlineColor = new Color(0.6f, 0.95f, 1f, 1f);
            }
            else
            {
                _currentAttackNames = (string[])DefaultNormalAttackNames.Clone();
                neutralStanceName = "— БОЕВАЯ СТОЙКА —";
                highlightColor = new Color(1f, 0.12f, 0.25f, 0.65f); // Crimson Neon
                outlineColor = Color.white;
            }

            UpdatePlaqueText(CurrentDirection);
            ApplyVisualColors();
        }

        // --- Вспомогательные методы чтения ввода (Input System + Legacy Fallback) ---

        private static Vector2 GetMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                return UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            }
#endif
            try { return Input.mousePosition; } catch { return Vector2.zero; }
        }

        private static Vector2 GetMouseDelta()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                return UnityEngine.InputSystem.Mouse.current.delta.ReadValue();
            }
#endif
            try { return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f; } catch { return Vector2.zero; }
        }

        private static bool IsLMBDown()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                return UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
            }
#endif
            try { return Input.GetMouseButtonDown(0); } catch { return false; }
        }

        private static bool IsLMBHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                return UnityEngine.InputSystem.Mouse.current.leftButton.isPressed;
            }
#endif
            try { return Input.GetMouseButton(0); } catch { return false; }
        }

        private static bool IsLMBUp()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                return UnityEngine.InputSystem.Mouse.current.leftButton.wasReleasedThisFrame;
            }
#endif
            try { return Input.GetMouseButtonUp(0); } catch { return false; }
        }

        private static bool IsRMBDown()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                return UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame;
            }
#endif
            try { return Input.GetMouseButtonDown(1); } catch { return false; }
        }

        private static bool IsRMBHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                return UnityEngine.InputSystem.Mouse.current.rightButton.isPressed;
            }
#endif
            try { return Input.GetMouseButton(1); } catch { return false; }
        }

        private static bool IsRMBUp()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                return UnityEngine.InputSystem.Mouse.current.rightButton.wasReleasedThisFrame;
            }
#endif
            try { return Input.GetMouseButtonUp(1); } catch { return false; }
        }

        private static Vector2 GetGamepadRightStick()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Gamepad.current != null)
            {
                return UnityEngine.InputSystem.Gamepad.current.rightStick.ReadValue();
            }
#endif
            try
            {
                return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            }
            catch
            {
                return Vector2.zero;
            }
        }

        private void OnValidate()
        {
            ApplyVisualColors();
        }
    }
}
