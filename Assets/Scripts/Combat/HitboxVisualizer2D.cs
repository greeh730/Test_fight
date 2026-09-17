using System;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Компонент визуального отображения хитбоксов в Scene View и Game View (Runtime).
    /// </summary>
    [DisallowMultipleComponent]
    public class HitboxVisualizer2D : MonoBehaviour
    {
        [Header("--- In-Game (Runtime) Visualization ---")]
        [Tooltip("Показывать ли полупрозрачные неоновые хитбоксы во время игры")]
        [SerializeField] private bool showInGameVisuals = true;

        [Tooltip("Непрозрачность заливки хитбокса в игре")]
        [Range(0.05f, 1f)]
        [SerializeField] private float inGameFillAlpha = 0.35f;

        [Tooltip("Яркость контура хитбокса в игре")]
        [Range(0.1f, 1f)]
        [SerializeField] private float inGameBorderAlpha = 0.9f;

        [Header("--- Scene View (Gizmos) ---")]
        [Tooltip("Отображать хитбоксы атак в окне Scene при выборе игрока")]
        [SerializeField] private bool showSceneGizmos = true;

        [Tooltip("Отображать все 4 атаки одновременно в Scene View (если выключено, рисуется только текущая выбранная)")]
        [SerializeField] private bool showAllAttacksInGizmos = true;

        // Runtime dynamic visualizer objects
        private GameObject _activeVisualObj;
        private SpriteRenderer _fillRenderer;
        private LineRenderer _borderRenderer;
        private static Sprite _whiteBoxSprite;

        public bool ShowInGameVisuals
        {
            get => showInGameVisuals;
            set => showInGameVisuals = value;
        }

        public bool ShowSceneGizmos => showSceneGizmos;
        public bool ShowAllAttacksInGizmos => showAllAttacksInGizmos;

        private void Awake()
        {
            EnsureSprite();
            CreateVisualObject();
        }

        private static void EnsureSprite()
        {
            if (_whiteBoxSprite != null) return;

            var tex = new Texture2D(4, 4);
            var cols = new Color[16];
            for (int i = 0; i < 16; i++) cols[i] = Color.white;
            tex.SetPixels(cols);
            tex.Apply();
            _whiteBoxSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
        }

        private void CreateVisualObject()
        {
            if (_activeVisualObj != null) return;

            _activeVisualObj = new GameObject("Runtime_Hitbox_Visual");
            _activeVisualObj.transform.SetParent(transform, false);

            // Sprite fill
            _fillRenderer = _activeVisualObj.AddComponent<SpriteRenderer>();
            _fillRenderer.sprite = _whiteBoxSprite;
            _fillRenderer.sortingOrder = 50;

            // Line outline
            _borderRenderer = _activeVisualObj.AddComponent<LineRenderer>();
            _borderRenderer.positionCount = 5;
            _borderRenderer.useWorldSpace = false;
            _borderRenderer.loop = true;
            _borderRenderer.startWidth = 0.04f;
            _borderRenderer.endWidth = 0.04f;
            _borderRenderer.sortingOrder = 51;

            var unlitMat = new Material(Shader.Find("Sprites/Default"));
            _borderRenderer.material = unlitMat;

            _activeVisualObj.SetActive(false);
        }

        /// <summary>
        /// Отображает визуальный хитбокс в игре во время активных кадров удара.
        /// </summary>
        public void ShowHitbox(Vector2 center, Vector2 size, Color color, bool isFinisher = false)
        {
            if (!showInGameVisuals) return;
            if (_activeVisualObj == null) CreateVisualObject();

            _activeVisualObj.transform.position = new Vector3(center.x, center.y, 0f);
            _activeVisualObj.transform.localScale = new Vector3(size.x, size.y, 1f);

            float fillAlpha = isFinisher ? Mathf.Min(1f, inGameFillAlpha * 1.6f) : inGameFillAlpha;
            float borderAlpha = isFinisher ? 1f : inGameBorderAlpha;
            float borderWidth = isFinisher ? 0.07f : 0.04f;

            Color fillCol = new Color(color.r, color.g, color.b, fillAlpha);
            Color borderCol = new Color(color.r, color.g, color.b, borderAlpha);

            _fillRenderer.color = fillCol;

            // Устанавливаем координаты 4 углов рамки
            Vector3[] corners = new Vector3[5]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(-0.5f, -0.5f, 0f)
            };
            _borderRenderer.SetPositions(corners);
            _borderRenderer.startWidth = borderWidth;
            _borderRenderer.endWidth = borderWidth;
            _borderRenderer.startColor = borderCol;
            _borderRenderer.endColor = borderCol;

            _activeVisualObj.SetActive(true);
        }

        /// <summary>
        /// Скрывает хитбокс после завершения активной фазы удара.
        /// </summary>
        public void HideHitbox()
        {
            if (_activeVisualObj != null)
            {
                _activeVisualObj.SetActive(false);
            }
        }

        /// <summary>
        /// Рисует хитбокс в Scene View с цветной рамкой и подписью.
        /// </summary>
        public static void DrawGizmoHitbox(Vector2 center, Vector2 size, Color color, string label = null)
        {
            Gizmos.color = color;
            Gizmos.DrawWireCube(new Vector3(center.x, center.y, 0f), new Vector3(size.x, size.y, 0.1f));

            Color fill = new Color(color.r, color.g, color.b, color.a * 0.25f);
            Gizmos.color = fill;
            Gizmos.DrawCube(new Vector3(center.x, center.y, 0f), new Vector3(size.x, size.y, 0.05f));
        }
    }
}
