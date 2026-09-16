using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public class WheelTrailGraphic : MaskableGraphic
    {
        [Header("--- Trail Settings ---")]
        [SerializeField] private float thickness = 8f;
        [SerializeField] private float minVertexDistance = 3f;
        [SerializeField] private float fadeDuration = 0.4f;
        [SerializeField] private Color trailColor = new Color(0.2f, 0.85f, 1f, 0.95f);
        [SerializeField] private Color tipGlowColor = new Color(1f, 1f, 1f, 1f);

        private readonly List<Vector2> _points = new List<Vector2>(128);
        private float _currentAlpha = 0f;
        private bool _isFading = false;

        public int PointCount => _points.Count;
        public IReadOnlyList<Vector2> Points => _points;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            color = trailColor;
        }

        private void Update()
        {
            if (_isFading && _currentAlpha > 0f)
            {
                float speed = 1f / Mathf.Max(0.05f, fadeDuration);
                _currentAlpha = Mathf.MoveTowards(_currentAlpha, 0f, Time.deltaTime * speed);
                SetVerticesDirty();

                if (_currentAlpha <= 0.005f)
                {
                    _currentAlpha = 0f;
                    _points.Clear();
                    _isFading = false;
                    SetVerticesDirty();
                }
            }
        }

        /// <summary>
        /// Начинает новый росчерк в указанной локальной точке (обычно (0,0) - центр колеса).
        /// </summary>
        public void StartNewTrail(Vector2 startPoint)
        {
            _points.Clear();
            _points.Add(startPoint);
            _currentAlpha = 1f;
            _isFading = false;
            SetVerticesDirty();
        }

        /// <summary>
        /// Добавляет точку к текущей траектории, если она удалена от предыдущей дальше минимальной дистанции.
        /// </summary>
        public void AddPoint(Vector2 point)
        {
            if (_points.Count == 0)
            {
                StartNewTrail(point);
                return;
            }

            Vector2 last = _points[_points.Count - 1];
            if (Vector2.Distance(last, point) >= minVertexDistance)
            {
                _points.Add(point);
                _currentAlpha = 1f;
                _isFading = false;
                SetVerticesDirty();
            }
        }

        /// <summary>
        /// Запускает плавное исчезновение линии.
        /// </summary>
        public void FadeOut()
        {
            _isFading = true;
        }

        /// <summary>
        /// Немедленно очищает шлейф.
        /// </summary>
        public void ClearInstant()
        {
            _points.Clear();
            _currentAlpha = 0f;
            _isFading = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if (_points.Count < 2 || _currentAlpha <= 0.001f)
            {
                return;
            }

            float halfWidth = thickness * 0.5f;
            Color baseColor = trailColor;
            baseColor.a = trailColor.a * _currentAlpha;

            Color tipColor = tipGlowColor;
            tipColor.a = tipGlowColor.a * _currentAlpha;

            int n = _points.Count;

            // Строим непрерывную ленту из четырехугольников
            for (int i = 0; i < n; i++)
            {
                Vector2 current = _points[i];
                Vector2 tangent;

                if (i == 0)
                {
                    tangent = (_points[1] - current).normalized;
                }
                else if (i == n - 1)
                {
                    tangent = (current - _points[i - 1]).normalized;
                }
                else
                {
                    tangent = ((_points[i + 1] - current).normalized + (current - _points[i - 1]).normalized).normalized;
                }

                if (tangent.sqrMagnitude < 0.001f)
                {
                    tangent = Vector2.right;
                }

                Vector2 normal = new Vector2(-tangent.y, tangent.x);

                // Плавное сужение/расширение шлейфа к концу (перо)
                float t = (float)i / Mathf.Max(1, n - 1);
                float widthScale = Mathf.Lerp(0.6f, 1.0f, t);
                Color vertColor = Color.Lerp(baseColor, tipColor, t * 0.5f);

                Vector2 leftPos = current + normal * (halfWidth * widthScale);
                Vector2 rightPos = current - normal * (halfWidth * widthScale);

                vh.AddVert(leftPos, vertColor, Vector2.zero);
                vh.AddVert(rightPos, vertColor, Vector2.zero);

                if (i > 0)
                {
                    int baseIdx = (i - 1) * 2;
                    vh.AddTriangle(baseIdx, baseIdx + 1, baseIdx + 2);
                    vh.AddTriangle(baseIdx + 1, baseIdx + 3, baseIdx + 2);
                }
            }

            // Добавляем круглый наконечник на кончике пера
            Vector2 tipPos = _points[n - 1];
            int tipCenterIdx = vh.currentVertCount;
            vh.AddVert(tipPos, tipColor, Vector2.zero);

            float capRadius = halfWidth * 1.15f;
            int capSegments = 8;
            for (int j = 0; j <= capSegments; j++)
            {
                float angle = (j / (float)capSegments) * Mathf.PI * 2f;
                Vector2 p = tipPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * capRadius;
                vh.AddVert(p, tipColor, Vector2.zero);

                if (j > 0)
                {
                    vh.AddTriangle(tipCenterIdx, tipCenterIdx + j, tipCenterIdx + j + 1);
                }
            }
        }
    }
}
