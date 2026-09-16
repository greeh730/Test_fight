using System;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class SwipeLineRendererUI : MonoBehaviour
    {
        [Header("--- UI References ---")]
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform startMarker;
        [SerializeField] private RectTransform lineSegment;
        [SerializeField] private RectTransform arrowHead;

        [Header("--- Visual Settings ---")]
        [SerializeField] private float lineWidth = 12f;
        [SerializeField] private float markerSize = 24f;
        [SerializeField] private float arrowSize = 28f;
        [SerializeField] private float fadeDuration = 0.3f;
        [SerializeField] private Color lineColor = new Color(0.2f, 0.8f, 1f, 0.85f);

        private Canvas _parentCanvas;
        private Camera _uiCamera;
        private bool _isActive = false;
        private float _currentAlpha = 0f;

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            _parentCanvas = GetComponentInParent<Canvas>();
            if (canvasRect == null && _parentCanvas != null)
            {
                canvasRect = _parentCanvas.GetComponent<RectTransform>();
            }

            if (_parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                _uiCamera = _parentCanvas.worldCamera != null ? _parentCanvas.worldCamera : Camera.main;
            }

            ApplyColors();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        public void ApplyColors()
        {
            if (startMarker != null && startMarker.TryGetComponent<Image>(out var startImg)) startImg.color = lineColor;
            if (lineSegment != null && lineSegment.TryGetComponent<Image>(out var lineImg)) lineImg.color = lineColor;
            if (arrowHead != null && arrowHead.TryGetComponent<Image>(out var arrowImg)) arrowImg.color = lineColor;
        }

        private void Update()
        {
            float targetAlpha = _isActive ? 1f : 0f;
            if (Mathf.Abs(_currentAlpha - targetAlpha) > 0.01f)
            {
                float speed = _isActive ? 15f : (1f / Mathf.Max(0.05f, fadeDuration));
                _currentAlpha = Mathf.MoveTowards(_currentAlpha, targetAlpha, Time.deltaTime * speed);
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = _currentAlpha;
                }
            }
            else
            {
                _currentAlpha = targetAlpha;
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = _currentAlpha;
                }
            }
        }

        /// <summary>
        /// Отображает и обновляет динамическую линию от начальной точки до текущей позиции мыши.
        /// </summary>
        public void Show(Vector2 startScreenPos, Vector2 currentScreenPos)
        {
            _isActive = true;

            if (canvasRect == null && _parentCanvas != null)
            {
                canvasRect = _parentCanvas.GetComponent<RectTransform>();
            }

            if (canvasRect == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, startScreenPos, _uiCamera, out Vector2 localStart);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, currentScreenPos, _uiCamera, out Vector2 localCurrent);

            Vector2 delta = localCurrent - localStart;
            float distance = delta.magnitude;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

            // Начальная точка
            if (startMarker != null)
            {
                startMarker.anchoredPosition = localStart;
                startMarker.sizeDelta = new Vector2(markerSize, markerSize);
            }

            // Отрезок линии (pivot должен быть [0, 0.5])
            if (lineSegment != null)
            {
                lineSegment.anchoredPosition = localStart;
                lineSegment.localRotation = rotation;
                lineSegment.sizeDelta = new Vector2(Mathf.Max(0f, distance), lineWidth);
            }

            // Наконечник стрелки
            if (arrowHead != null)
            {
                arrowHead.anchoredPosition = localCurrent;
                arrowHead.localRotation = rotation;
                arrowHead.sizeDelta = new Vector2(arrowSize, arrowSize);
                arrowHead.gameObject.SetActive(distance > arrowSize * 0.5f);
            }
        }

        /// <summary>
        /// Скрывает линию с плавным угасанием.
        /// </summary>
        public void Hide(bool instant = false)
        {
            _isActive = false;
            if (instant)
            {
                _currentAlpha = 0f;
                if (canvasGroup != null) canvasGroup.alpha = 0f;
            }
        }
    }
}
