using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Combat.Settings;

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

        [Header("--- Plaque HUD (Под колесом) ---")]
        [SerializeField] private CanvasGroup plaqueCanvasGroup;
        [SerializeField] private RectTransform plaqueRect;
        [SerializeField] private Text attackNameText;
        [SerializeField] private string[] attackNames = new string[8]
        {
            "СРЕДНИЙ ВПЕРЕД ▶",    // Right (0)
            "ВЕРХНИЙ ВПЕРЕД ↗",    // UpRight (1)
            "ВЕРХНИЙ УДАР ▲",      // Up (2)
            "ВЕРХНИЙ НАЗАД ↖",     // UpLeft (3)
            "СРЕДНИЙ НАЗАД ◀",     // Left (4)
            "НИЖНИЙ НАЗАД ↙",      // DownLeft (5)
            "НИЖНИЙ УДАР ▼",       // Down (6)
            "НИЖНИЙ ВПЕРЕД ↘"      // DownRight (7)
        };
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

        // Runtime State
        public Direction8 CurrentDirection { get; private set; } = Direction8.None;
        public Vector2 CurrentVector { get; private set; } = Vector2.zero;
        public float CurrentRawAngle { get; private set; } = 0f;
        public bool IsDragging { get; private set; } = false;

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
        }

        private void OnDisable()
        {
            CombatSettingsManager.OnSettingsChanged -= ApplySettings;
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
            UpdateVisuals(instant: true);
        }

        private void Update()
        {
            if (trackingMode == MouseTrackingMode.LMBDragSwipe)
            {
                HandleLMBGestureTrail();
            }
            else
            {
                HandleContinuousModes();
            }

            UpdateJuiceAnimations();
            UpdateVisuals(instant: false);
        }

        private void HandleLMBGestureTrail()
        {
            Vector2 mousePos = GetMousePosition();
            bool isToggle = _currentSettings != null && _currentSettings.activationMode == ActivationMode.Toggle;

            // 1. Активация жеста
            if (isToggle)
            {
                if (IsLMBDown())
                {
                    if (!IsDragging)
                    {
                        StartDrag(mousePos);
                    }
                    else
                    {
                        ExecuteSwipeComplete();
                        return;
                    }
                }
            }
            else // Режим удержания (Hold)
            {
                if (IsLMBDown())
                {
                    StartDrag(mousePos);
                }
            }

            // 2. Движение мыши или правого стика геймпада
            if (IsDragging && (isToggle || IsLMBHeld()))
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
                    Direction8 newDir = Direction8Extensions.FromAngle(CurrentRawAngle);

                    if (newDir != CurrentDirection)
                    {
                        CurrentDirection = newDir;
                        _flashIntensity = 1f;
                        UpdatePlaqueText(CurrentDirection);
                        onDirectionChanged?.Invoke(CurrentDirection);
                    }

                    onVectorChanged?.Invoke(_penPosition.normalized);
                }

                // Быстрый удар при касании края колеса (Quick-Cast on Edge)
                if (_currentSettings != null && _currentSettings.quickCastOnEdge && dist >= wheelRadius * 0.95f)
                {
                    ExecuteSwipeComplete();
                    return;
                }
            }

            // 3. Завершение жеста в режиме Hold
            if (!isToggle && IsDragging && IsLMBUp())
            {
                ExecuteSwipeComplete();
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
            if (dist >= minSwipeDistance && CurrentDirection != Direction8.None)
            {
                onSwipeCompleted?.Invoke(CurrentDirection, _penPosition.normalized, dist);
                if (wheelTrailGraphic != null)
                {
                    onGesturePathCompleted?.Invoke(wheelTrailGraphic.Points, CurrentDirection);
                }
            }

            if (wheelTrailGraphic != null)
            {
                wheelTrailGraphic.FadeOut();
            }

            IsDragging = false;
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
                Direction8 newDir = Direction8Extensions.FromAngle(CurrentRawAngle);

                if (newDir != CurrentDirection)
                {
                    CurrentDirection = newDir;
                    _flashIntensity = 1f;
                    UpdatePlaqueText(CurrentDirection);
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
                    Color col = Color.Lerp(highlightColor, Color.white, _flashIntensity * 0.7f);
                    col.a = Mathf.Clamp01(highlightColor.a * _fadeAlpha * pulse);
                    sectorHighlightImage.color = col;

                    float targetSectorAngle = CurrentDirection.ToAngle();
                    sectorHighlightImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, targetSectorAngle);
                }
                else
                {
                    Color col = highlightColor;
                    col.a = 0f;
                    sectorHighlightImage.color = col;
                }
            }

            // 2. Центральное ядро-реактор
            if (centerCoreImage != null)
            {
                Color coreCol = centerCoreColor;
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

        private void UpdatePlaqueText(Direction8 dir)
        {
            if (attackNameText == null) return;

            _textPunchScale = 1.14f;

            if (dir == Direction8.None)
            {
                attackNameText.text = neutralStanceName;
                attackNameText.color = textIdleColor;
            }
            else
            {
                int idx = (int)dir;
                if (idx >= 0 && idx < attackNames.Length)
                {
                    attackNameText.text = attackNames[idx];
                    attackNameText.color = textActiveColor;
                }
            }
        }

        public void SetAttackPlaqueWithCombo(string attackName, int comboStep, bool isFinisher)
        {
            if (attackNameText == null) return;

            _textPunchScale = isFinisher ? 1.25f : 1.15f;

            string badge = comboStep switch
            {
                1 => "[КОМБО 1]",
                2 => "<color=#FFD700>[КОМБО 2]</color>",
                _ => isFinisher ? "<color=#FF3344>[ФИНИШЕР! x3]</color>" : $"<color=#FF7722>[КОМБО {comboStep}]</color>"
            };

            attackNameText.text = $"{attackName} {badge}";
            attackNameText.color = isFinisher ? new Color(1f, 0.4f, 0.4f, 1f) : textActiveColor;
        }

        public void ApplyVisualColors()
        {
            if (wheelOutlineImage != null) wheelOutlineImage.color = outlineColor;
            if (sectorHighlightImage != null) sectorHighlightImage.color = highlightColor;
            if (centerCoreImage != null) centerCoreImage.color = centerCoreColor;
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
