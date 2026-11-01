// Assets/Scripts/Fencing/FencingWheelGraphic.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Fencing
{
    /// <summary>
    /// Процедурная UI-графика колеса фехтования.
    /// Динамически генерирует сетку Canvas с наглядным делением на 6 атакующих лепестков,
    /// 2 мертвые зоны (верх/низ), зеленую свободную зону, контуры и натянутую нитку.
    /// Реагирует на изменение любых параметров в реальном времени.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class FencingWheelGraphic : MaskableGraphic
    {
        [Header("Геометрия UI")]
        [Tooltip("Радиус UI колеса в экранных пикселях")]
        [SerializeField] private float uiRadius = 85f;

        [Header("Палитра - Фехтование (Красное колесо)")]
        [SerializeField] private Color fencingRimColor = new Color(0.95f, 0.3f, 0.3f, 0.95f);
        [SerializeField] private Color fencingBackgroundColor = new Color(0.18f, 0.04f, 0.06f, 0.9f);
        [SerializeField] private Color fencingPetalBaseColor = new Color(0.72f, 0.12f, 0.16f, 0.78f);
        [SerializeField] private Color fencingPetalHoverColor = new Color(1.0f, 0.32f, 0.22f, 0.95f);
        [SerializeField] private Color fencingPetalFlashColor = new Color(1.0f, 0.92f, 0.5f, 1f);
        [SerializeField] private Color fencingDeadZoneColor = new Color(0.38f, 0.05f, 0.08f, 0.88f);
        [SerializeField] private Color fencingDeadZoneBorderColor = new Color(0.7f, 0.1f, 0.15f, 0.95f);
        [SerializeField] private Color fencingFreeZoneColor = new Color(0.16f, 0.68f, 0.38f, 0.45f);
        [SerializeField] private Color fencingFreeZoneBorderColor = new Color(0.25f, 0.88f, 0.48f, 0.95f);
        [SerializeField] private Color fencingDividerColor = new Color(0.9f, 0.35f, 0.35f, 0.65f);
        [SerializeField] private Color fencingStringColor = new Color(1.0f, 0.75f, 0.75f, 0.85f);

        [Header("Палитра - Магия (Синее колесо)")]
        [SerializeField] private Color magicRimColor = new Color(0.3f, 0.65f, 1.0f, 0.95f);
        [SerializeField] private Color magicBackgroundColor = new Color(0.04f, 0.07f, 0.18f, 0.9f);
        [SerializeField] private Color magicPetalBaseColor = new Color(0.12f, 0.35f, 0.75f, 0.78f);
        [SerializeField] private Color magicPetalHoverColor = new Color(0.25f, 0.75f, 1.0f, 0.95f);
        [SerializeField] private Color magicPetalFlashColor = new Color(0.7f, 0.95f, 1.0f, 1f);
        [SerializeField] private Color magicDeadZoneColor = new Color(0.12f, 0.08f, 0.35f, 0.88f);
        [SerializeField] private Color magicDeadZoneBorderColor = new Color(0.25f, 0.2f, 0.7f, 0.95f);
        [SerializeField] private Color magicFreeZoneColor = new Color(0.16f, 0.68f, 0.55f, 0.45f);
        [SerializeField] private Color magicFreeZoneBorderColor = new Color(0.25f, 0.88f, 0.75f, 0.95f);
        [SerializeField] private Color magicDividerColor = new Color(0.4f, 0.65f, 0.95f, 0.65f);
        [SerializeField] private Color magicStringColor = new Color(0.75f, 0.92f, 1.0f, 0.85f);

        [Header("Активная стойка")]
        [SerializeField] private StanceType currentStance = StanceType.Fencing;

        [Header("Текущая цветовая палитра")]
        [SerializeField] private Color rimColor = new Color(0.95f, 0.3f, 0.3f, 0.95f);
        [SerializeField] private Color backgroundColor = new Color(0.18f, 0.04f, 0.06f, 0.9f);
        [SerializeField] private Color petalBaseColor = new Color(0.72f, 0.12f, 0.16f, 0.78f);
        [SerializeField] private Color petalHoverColor = new Color(1.0f, 0.32f, 0.22f, 0.95f);
        [SerializeField] private Color petalFlashColor = new Color(1.0f, 0.92f, 0.5f, 1f);
        [SerializeField] private Color deadZoneColor = new Color(0.38f, 0.05f, 0.08f, 0.88f);
        [SerializeField] private Color deadZoneBorderColor = new Color(0.7f, 0.1f, 0.15f, 0.95f);
        [SerializeField] private Color freeZoneColor = new Color(0.16f, 0.68f, 0.38f, 0.45f);
        [SerializeField] private Color freeZoneBorderColor = new Color(0.25f, 0.88f, 0.48f, 0.95f);
        [SerializeField] private Color dividerColor = new Color(0.9f, 0.35f, 0.35f, 0.65f);
        [SerializeField] private Color stringColor = new Color(1.0f, 0.75f, 0.75f, 0.85f);

        [Header("Отображение деталей")]
        [SerializeField] private bool showDividers = true;
        [SerializeField] private bool showElasticString = true;

        // Внутреннее состояние для динамического рендеринга
        private FencingWheel wheel;
        private Vector2 orbUIPos = Vector2.zero;
        private AttackDirection hoveredDirection = AttackDirection.None;
        private AttackDirection flashDirection = AttackDirection.None;
        private float flashIntensity = 0f;

        public StanceType CurrentStance => currentStance;
        public float UIRadius { get => uiRadius; set { uiRadius = Mathf.Max(30f, value); SetVerticesDirty(); } }
        public Color RimColor { get => rimColor; set { rimColor = value; SetVerticesDirty(); } }
        public Color PetalBaseColor { get => petalBaseColor; set { petalBaseColor = value; SetVerticesDirty(); } }
        public Color PetalHoverColor { get => petalHoverColor; set { petalHoverColor = value; SetVerticesDirty(); } }
        public Color DeadZoneColor { get => deadZoneColor; set { deadZoneColor = value; SetVerticesDirty(); } }
        public Color FreeZoneColor { get => freeZoneColor; set { freeZoneColor = value; SetVerticesDirty(); } }
        public bool ShowDividers { get => showDividers; set { showDividers = value; SetVerticesDirty(); } }
        public bool ShowElasticString { get => showElasticString; set { showElasticString = value; SetVerticesDirty(); } }

        protected override void Awake()
        {
            base.Awake();
            ApplyPaletteForCurrentStance();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ApplyPaletteForCurrentStance();
        }

        /// <summary>
        /// Переключает палитру колеса в зависимости от стойки (Фехтование = Красный, Магия = Синий).
        /// </summary>
        public void SetStance(StanceType stance)
        {
            currentStance = stance;
            ApplyPaletteForCurrentStance();
            SetVerticesDirty();
        }

        public void ApplyPaletteForCurrentStance()
        {
            if (currentStance == StanceType.Fencing)
            {
                rimColor = fencingRimColor;
                backgroundColor = fencingBackgroundColor;
                petalBaseColor = fencingPetalBaseColor;
                petalHoverColor = fencingPetalHoverColor;
                petalFlashColor = fencingPetalFlashColor;
                deadZoneColor = fencingDeadZoneColor;
                deadZoneBorderColor = fencingDeadZoneBorderColor;
                freeZoneColor = fencingFreeZoneColor;
                freeZoneBorderColor = fencingFreeZoneBorderColor;
                dividerColor = fencingDividerColor;
                stringColor = fencingStringColor;
            }
            else
            {
                rimColor = magicRimColor;
                backgroundColor = magicBackgroundColor;
                petalBaseColor = magicPetalBaseColor;
                petalHoverColor = magicPetalHoverColor;
                petalFlashColor = magicPetalFlashColor;
                deadZoneColor = magicDeadZoneColor;
                deadZoneBorderColor = magicDeadZoneBorderColor;
                freeZoneColor = magicFreeZoneColor;
                freeZoneBorderColor = magicFreeZoneBorderColor;
                dividerColor = magicDividerColor;
                stringColor = magicStringColor;
            }
        }

        public void BindWheel(FencingWheel targetWheel)
        {
            this.wheel = targetWheel;
            if (this.wheel != null)
            {
                SetStance(this.wheel.CurrentStance);
            }
            SetVerticesDirty();
        }

        public void UpdateOrbState(Vector2 normalizedOrbPos, AttackDirection hovered, AttackDirection flashDir, float flashAlpha)
        {
            this.orbUIPos = normalizedOrbPos * uiRadius;
            this.hoveredDirection = hovered;
            this.flashDirection = flashDir;
            this.flashIntensity = flashAlpha;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            float outerR = uiRadius;
            float ratio = (wheel != null && wheel.WheelRadius > 0.01f) 
                ? (wheel.FreeZoneRadius / wheel.WheelRadius) 
                : 0.4f;
            float innerR = Mathf.Clamp(outerR * ratio, 10f, outerR * 0.9f);

            // 1. Фоновый круг
            DrawFilledCircle(vh, Vector2.zero, outerR, backgroundColor, 48);

            // 2. Мертвые зоны (Верхняя + Нижняя) - только если включены в модели колеса
            if (wheel != null && wheel.EnableDeadZone && wheel.DeadZoneAngle > 0.01f && wheel.DeadSectors != null && wheel.DeadSectors.Count > 0)
            {
                for (int i = 0; i < wheel.DeadSectors.Count; i++)
                {
                    AttackSector dead = wheel.DeadSectors[i];
                    if (dead.angleWidth > 0.01f)
                    {
                        float startA = dead.angleCenter - dead.angleWidth * 0.5f;
                        float endA = dead.angleCenter + dead.angleWidth * 0.5f;
                        DrawWedge(vh, startA, endA, innerR, outerR, deadZoneColor, 16);

                        // Границы мертвых зон
                        DrawRadialLine(vh, startA, innerR, outerR, 1.8f, deadZoneBorderColor);
                        DrawRadialLine(vh, endA, innerR, outerR, 1.8f, deadZoneBorderColor);
                    }
                }
            }

            // 3. Атакующие лепестки (6 секторов)
            if (wheel != null && wheel.AttackSectors != null)
            {
                for (int i = 0; i < wheel.AttackSectors.Count; i++)
                {
                    AttackSector sector = wheel.AttackSectors[i];
                    Color col = petalBaseColor;

                    // Подсветка при наведении шарика
                    if (hoveredDirection == sector.direction)
                    {
                        col = petalHoverColor;
                    }

                    // Вспышка при ударе
                    if (flashDirection == sector.direction && flashIntensity > 0.01f)
                    {
                        col = Color.Lerp(col, petalFlashColor, flashIntensity);
                    }

                    float startA = sector.angleCenter - sector.angleWidth * 0.5f;
                    float endA = sector.angleCenter + sector.angleWidth * 0.5f;
                    DrawWedge(vh, startA, endA, innerR, outerR, col, 14);

                    // Радиальные разделители между лепестками
                    if (showDividers)
                    {
                        DrawRadialLine(vh, startA, innerR, outerR, 1.2f, dividerColor);
                        DrawRadialLine(vh, endA, innerR, outerR, 1.2f, dividerColor);
                    }
                }
            }

            // 4. Центральная свободная зона (зеленая)
            DrawFilledCircle(vh, Vector2.zero, innerR, freeZoneColor, 36);
            DrawRing(vh, innerR - 1.5f, innerR + 1.5f, freeZoneBorderColor, 36);

            // 5. Внешний обод (металлический контур)
            DrawRing(vh, outerR - 2.5f, outerR + 1.5f, rimColor, 52);

            // 6. Натянутая нитка (от центра к шарику)
            if (showElasticString && orbUIPos.sqrMagnitude > 4f)
            {
                DrawSegmentLine(vh, Vector2.zero, orbUIPos, 2.2f, stringColor);
            }
        }

        #region Примитивы отрисовки меша
        private void DrawWedge(VertexHelper vh, float startDeg, float endDeg, float innerR, float outerR, Color col, int segments)
        {
            float step = (endDeg - startDeg) / segments;
            for (int i = 0; i < segments; i++)
            {
                float a0 = (startDeg + i * step) * Mathf.Deg2Rad;
                float a1 = (startDeg + (i + 1) * step) * Mathf.Deg2Rad;

                Vector2 in0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * innerR;
                Vector2 out0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * outerR;
                Vector2 in1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * innerR;
                Vector2 out1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * outerR;

                int baseIdx = vh.currentVertCount;
                vh.AddVert(in0, col, Vector2.zero);
                vh.AddVert(out0, col, Vector2.zero);
                vh.AddVert(out1, col, Vector2.zero);
                vh.AddVert(in1, col, Vector2.zero);

                vh.AddTriangle(baseIdx, baseIdx + 1, baseIdx + 2);
                vh.AddTriangle(baseIdx, baseIdx + 2, baseIdx + 3);
            }
        }

        private void DrawFilledCircle(VertexHelper vh, Vector2 center, float radius, Color col, int segments)
        {
            int centerIdx = vh.currentVertCount;
            vh.AddVert(center, col, Vector2.zero);

            float step = 360f / segments;
            for (int i = 0; i <= segments; i++)
            {
                float rad = i * step * Mathf.Deg2Rad;
                Vector2 pos = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
                vh.AddVert(pos, col, Vector2.zero);
            }

            for (int i = 1; i <= segments; i++)
            {
                vh.AddTriangle(centerIdx, centerIdx + i, centerIdx + i + 1);
            }
        }

        private void DrawRing(VertexHelper vh, float innerR, float outerR, Color col, int segments)
        {
            float step = 360f / segments;
            for (int i = 0; i < segments; i++)
            {
                float a0 = (i * step) * Mathf.Deg2Rad;
                float a1 = ((i + 1) * step) * Mathf.Deg2Rad;

                Vector2 in0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * innerR;
                Vector2 out0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * outerR;
                Vector2 in1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * innerR;
                Vector2 out1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * outerR;

                int baseIdx = vh.currentVertCount;
                vh.AddVert(in0, col, Vector2.zero);
                vh.AddVert(out0, col, Vector2.zero);
                vh.AddVert(out1, col, Vector2.zero);
                vh.AddVert(in1, col, Vector2.zero);

                vh.AddTriangle(baseIdx, baseIdx + 1, baseIdx + 2);
                vh.AddTriangle(baseIdx, baseIdx + 2, baseIdx + 3);
            }
        }

        private void DrawRadialLine(VertexHelper vh, float angleDeg, float innerR, float outerR, float thickness, Color col)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 start = dir * innerR;
            Vector2 end = dir * outerR;
            DrawSegmentLine(vh, start, end, thickness, col);
        }

        private void DrawSegmentLine(VertexHelper vh, Vector2 start, Vector2 end, float thickness, Color col)
        {
            Vector2 dir = (end - start).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);

            int baseIdx = vh.currentVertCount;
            vh.AddVert(start - perp, col, Vector2.zero);
            vh.AddVert(start + perp, col, Vector2.zero);
            vh.AddVert(end + perp, col, Vector2.zero);
            vh.AddVert(end - perp, col, Vector2.zero);

            vh.AddTriangle(baseIdx, baseIdx + 1, baseIdx + 2);
            vh.AddTriangle(baseIdx, baseIdx + 2, baseIdx + 3);
        }
        #endregion
    }
}
