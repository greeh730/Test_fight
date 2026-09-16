using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

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
        [SerializeField] private float fadeOutSpeed = 3.0f;

        [Header("--- UI References ---")]
        [SerializeField] private RectTransform wheelRect;
        [SerializeField] private Image wheelOutlineImage;
        [SerializeField] private Image sectorHighlightImage;
        [SerializeField] private Image pointerArrowImage;
        [SerializeField] private Image centerDotImage;
        [SerializeField] private WheelTrailGraphic wheelTrailGraphic;

        [Header("--- Visual Settings ---")]
        [SerializeField] private bool showPointer = false;
        [SerializeField] private bool showHighlight = true;
        [SerializeField] private bool smoothPointerRotation = true;
        [SerializeField] private float pointerRotationSpeed = 35f;

        [SerializeField] private Color outlineColor = Color.white;
        [SerializeField] private Color highlightColor = new Color(0.2f, 0.75f, 1f, 0.55f);
        [SerializeField] private Color pointerColor = new Color(1f, 0.85f, 0.2f, 0.95f);

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

        private Camera _canvasCamera;
        private Canvas _parentCanvas;

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

            UpdateVisuals(instant: false);
        }

        private void HandleLMBGestureTrail()
        {
            Vector2 mousePos = GetMousePosition();

            // 1. Нажатие ЛКМ - старт рисования из центра колеса
            if (IsLMBDown())
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

            // 2. Удержание ЛКМ - движение мыши оставляет непрерывный изогнутый след внутри колеса
            if (IsDragging && IsLMBHeld())
            {
                Vector2 mouseDelta = mousePos - _lastMousePos;
                _lastMousePos = mousePos;

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
                        onDirectionChanged?.Invoke(CurrentDirection);
                    }

                    onVectorChanged?.Invoke(_penPosition.normalized);
                }
            }

            // 3. Отпускание ЛКМ - завершение жеста и старт плавного гашения
            if (IsDragging && IsLMBUp())
            {
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

            // 4. Плавное гашение подсветки колеса при отпущенной кнопке
            if (!IsDragging)
            {
                if (_fadeAlpha > 0f)
                {
                    _fadeAlpha = Mathf.MoveTowards(_fadeAlpha, 0f, Time.deltaTime * fadeOutSpeed);
                    if (_fadeAlpha <= 0.01f)
                    {
                        _fadeAlpha = 0f;
                        CurrentDirection = Direction8.None;
                    }
                }
            }
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
                }
            }
        }

        private void UpdateVisuals(bool instant)
        {
            // 1. Сектор подсветки (Sector Highlight)
            if (sectorHighlightImage != null)
            {
                sectorHighlightImage.gameObject.SetActive(showHighlight);
                if (showHighlight)
                {
                    Color targetColor = highlightColor;
                    targetColor.a = highlightColor.a * _fadeAlpha;
                    sectorHighlightImage.color = targetColor;

                    if (CurrentDirection != Direction8.None)
                    {
                        float targetSectorAngle = CurrentDirection.ToAngle();
                        sectorHighlightImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, targetSectorAngle);
                    }
                }
            }

            // 2. Стрелка-указатель (Pointer Arrow)
            if (pointerArrowImage != null)
            {
                pointerArrowImage.gameObject.SetActive(showPointer);
                if (showPointer)
                {
                    Color targetPtrColor = pointerColor;
                    targetPtrColor.a = pointerColor.a * _fadeAlpha;
                    pointerArrowImage.color = targetPtrColor;

                    if (CurrentDirection != Direction8.None)
                    {
                        Quaternion targetRot = Quaternion.Euler(0f, 0f, CurrentRawAngle);
                        if (instant || !smoothPointerRotation)
                        {
                            pointerArrowImage.rectTransform.localRotation = targetRot;
                        }
                        else
                        {
                            pointerArrowImage.rectTransform.localRotation = Quaternion.Slerp(
                                pointerArrowImage.rectTransform.localRotation,
                                targetRot,
                                Time.deltaTime * pointerRotationSpeed
                            );
                        }
                    }
                }
            }
        }

        public void ApplyVisualColors()
        {
            if (wheelOutlineImage != null) wheelOutlineImage.color = outlineColor;
            if (sectorHighlightImage != null) sectorHighlightImage.color = highlightColor;
            if (pointerArrowImage != null) pointerArrowImage.color = pointerColor;
            if (centerDotImage != null) centerDotImage.color = outlineColor;
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
            return Input.mousePosition;
        }

        private static Vector2 GetMouseDelta()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                return UnityEngine.InputSystem.Mouse.current.delta.ReadValue();
            }
#endif
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
        }

        private static bool IsLMBDown()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                return UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
            }
#endif
            return Input.GetMouseButtonDown(0);
        }

        private static bool IsLMBHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                return UnityEngine.InputSystem.Mouse.current.leftButton.isPressed;
            }
#endif
            return Input.GetMouseButton(0);
        }

        private static bool IsLMBUp()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                return UnityEngine.InputSystem.Mouse.current.leftButton.wasReleasedThisFrame;
            }
#endif
            return Input.GetMouseButtonUp(0);
        }

        private void OnValidate()
        {
            ApplyVisualColors();
        }
    }
}
