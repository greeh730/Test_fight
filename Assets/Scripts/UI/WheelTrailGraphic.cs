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
        [Header("--- Trail Geometry ---")]
        [SerializeField] private float outerThickness = 14f;
        [SerializeField] private float coreThickness = 4.5f;
        [SerializeField] private float minVertexDistance = 2.5f;
        [SerializeField] private float fadeDuration = 0.35f;

        [Header("--- Red & White Palette ---")]
        [SerializeField] private Color outerGlowColor = new Color(1f, 0.12f, 0.25f, 0.75f);  // Neon Crimson Red
        [SerializeField] private Color hotCoreColor = new Color(1f, 1f, 1f, 0.98f);           // Pure White Core
        [SerializeField] private Color tipSparkColor = new Color(1f, 0.95f, 0.95f, 1f);

        private readonly List<Vector2> _points = new List<Vector2>(128);
        private float _currentAlpha = 0f;
        private bool _isFading = false;

        public int PointCount => _points.Count;
        public IReadOnlyList<Vector2> Points => _points;
        public Vector2 CurrentTipPosition => _points.Count > 0 ? _points[_points.Count - 1] : Vector2.zero;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
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

        public void StartNewTrail(Vector2 startPoint)
        {
            _points.Clear();
            _points.Add(startPoint);
            _currentAlpha = 1f;
            _isFading = false;
            SetVerticesDirty();
        }

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

        public void FadeOut()
        {
            _isFading = true;
        }

        public void ClearInstant()
        {
            _points.Clear();
            _currentAlpha = 0f;
            _isFading = false;
            SetVerticesDirty();
        }

        public void SetThemeColors(Color outerGlow, Color coreColor, Color tipSpark)
        {
            outerGlowColor = outerGlow;
            hotCoreColor = coreColor;
            tipSparkColor = tipSpark;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            int n = _points.Count;
            if (n < 2 || _currentAlpha <= 0.001f)
            {
                return;
            }

            // Предварительный расчет направлений касательных (tangents) и нормалей
            Vector2[] normals = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 tangent;
                if (i == 0) tangent = (_points[1] - _points[0]).normalized;
                else if (i == n - 1) tangent = (_points[i] - _points[i - 1]).normalized;
                else tangent = ((_points[i + 1] - _points[i]).normalized + (_points[i] - _points[i - 1]).normalized).normalized;

                if (tangent.sqrMagnitude < 0.001f) tangent = Vector2.right;
                normals[i] = new Vector2(-tangent.y, tangent.x);
            }

            // --- СЛОЙ 1: Внешний неоновый красный шлейф (Outer Crimson Glow) ---
            Color redGlow = outerGlowColor;
            redGlow.a = outerGlowColor.a * _currentAlpha;
            DrawRibbonPass(vh, normals, outerThickness * 0.5f, redGlow, redGlow, 0.4f);

            // --- СЛОЙ 2: Внутренний ультра-яркий белый лазерный сердечник (Hot White Core) ---
            Color whiteCore = hotCoreColor;
            whiteCore.a = hotCoreColor.a * _currentAlpha;
            DrawRibbonPass(vh, normals, coreThickness * 0.5f, whiteCore, whiteCore, 0.8f);

            // --- СЛОЙ 3: Сверкающая искра на острие движения (Spark Head Reticle) ---
            DrawTipSpark(vh, _points[n - 1], normals[n - 1]);
        }

        private void DrawRibbonPass(VertexHelper vh, Vector2[] normals, float halfWidth, Color startColor, Color endColor, float taperStart)
        {
            int n = _points.Count;
            int startVert = vh.currentVertCount;

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Mathf.Max(1, n - 1);
                float widthScale = Mathf.Lerp(taperStart, 1.0f, t);
                Color c = Color.Lerp(startColor, endColor, t);

                Vector2 current = _points[i];
                Vector2 normal = normals[i] * (halfWidth * widthScale);

                vh.AddVert(current + normal, c, Vector2.zero);
                vh.AddVert(current - normal, c, Vector2.zero);

                if (i > 0)
                {
                    int b = startVert + (i - 1) * 2;
                    vh.AddTriangle(b, b + 1, b + 2);
                    vh.AddTriangle(b + 1, b + 3, b + 2);
                }
            }

            // Закругленный наконечник
            Vector2 tip = _points[n - 1];
            int tipCenter = vh.currentVertCount;
            vh.AddVert(tip, endColor, Vector2.zero);

            int capSegments = 8;
            for (int j = 0; j <= capSegments; j++)
            {
                float angle = (j / (float)capSegments) * Mathf.PI * 2f;
                Vector2 p = tip + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * halfWidth;
                vh.AddVert(p, endColor, Vector2.zero);

                if (j > 0)
                {
                    vh.AddTriangle(tipCenter, tipCenter + j, tipCenter + j + 1);
                }
            }
        }

        private void DrawTipSpark(VertexHelper vh, Vector2 tip, Vector2 normal)
        {
            Color sparkCol = tipSparkColor;
            sparkCol.a = tipSparkColor.a * _currentAlpha;

            Color sparkFade = new Color(1f, 0.2f, 0.35f, 0f);

            int centerIdx = vh.currentVertCount;
            vh.AddVert(tip, sparkCol, Vector2.zero);

            // 4 луча крестообразной искры
            float spikeLen = outerThickness * 1.35f;
            float spikeW = coreThickness * 0.7f;

            Vector2 right = new Vector2(-normal.y, normal.x); // направление вдоль движения

            Vector2[] dirs = new Vector2[] { right, -right, normal, -normal };
            foreach (var dir in dirs)
            {
                Vector2 perp = new Vector2(-dir.y, dir.x) * spikeW;
                int v0 = vh.currentVertCount;
                vh.AddVert(tip + dir * spikeLen, sparkFade, Vector2.zero);
                vh.AddVert(tip + perp, sparkCol, Vector2.zero);
                vh.AddVert(tip - perp, sparkCol, Vector2.zero);

                vh.AddTriangle(centerIdx, v0 + 1, v0);
                vh.AddTriangle(centerIdx, v0, v0 + 2);
            }
        }
    }
}
