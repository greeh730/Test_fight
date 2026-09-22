using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.UI
{
    /// <summary>
    /// Радиальная визуализация 8 стрелок (8-Way Directional Arrows) вокруг векторного колеса.
    /// Отображает кольцо из 8 направлений, подсвечивает текущий сектор и дает импульс/вспышку
    /// при фиксации стрелки в комбо-буфер.
    /// </summary>
    [ExecuteAlways]
    public class WheelDirectionArrowsOverlay : MonoBehaviour
    {
        [Serializable]
        public class ArrowSlot
        {
            public Direction8 direction;
            public Image image;
            public RectTransform rectTransform;

            [NonSerialized] public float currentScale = 1.0f;
            [NonSerialized] public float punchScale = 0.0f;
            [NonSerialized] public float flashAmount = 0.0f;
        }

        [Header("--- References ---")]
        [SerializeField] private VectorWheelController wheelController;
        [SerializeField] private DirectionSequenceRecognizer sequenceRecognizer;
        [SerializeField] private Sprite arrowSprite;
        [SerializeField] private RectTransform ringContainer;

        [Header("--- Layout ---")]
        [Tooltip("Радиус расположения стрелок от центра колеса")]
        [SerializeField] private float ringRadius = 58f;

        [Tooltip("Размер каждой стрелки в пикселях")]
        [SerializeField] private Vector2 arrowSize = new Vector2(26f, 26f);

        [Header("--- Palette Colors ---")]
        [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.40f);
        [SerializeField] private Color activeColor = new Color(1f, 0.15f, 0.28f, 1.0f); // Crimson neon
        [SerializeField] private Color parryActiveColor = new Color(0.2f, 0.85f, 1.0f, 1.0f); // Cyan neon
        [SerializeField] private Color tokenFlashColor = new Color(1.0f, 0.95f, 0.45f, 1.0f); // Bright Gold Punch

        [Header("--- Dynamics & Juiciness ---")]
        [SerializeField] private float activeScaleMultiplier = 1.35f;
        [SerializeField] private float punchScaleAmount = 0.50f;
        [SerializeField] private float scaleSmoothing = 14f;
        [SerializeField] private float punchDecay = 7f;
        [SerializeField] private float flashDecay = 5.5f;
        [SerializeField] private float pulseSpeed = 7.5f;

        [Header("--- Runtime Arrow Slots ---")]
        [SerializeField] private List<ArrowSlot> arrowSlots = new List<ArrowSlot>();

        private static readonly Direction8[] AllDirections = new Direction8[]
        {
            Direction8.Right,
            Direction8.UpRight,
            Direction8.Up,
            Direction8.UpLeft,
            Direction8.Left,
            Direction8.DownLeft,
            Direction8.Down,
            Direction8.DownRight
        };

        private void Awake()
        {
            ResolveReferences();
            EnsureArrowsCreated();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (sequenceRecognizer != null)
            {
                sequenceRecognizer.OnRawTokenAdded -= HandleRawTokenAdded;
                sequenceRecognizer.OnRawTokenAdded += HandleRawTokenAdded;
            }
        }

        private void OnDisable()
        {
            if (sequenceRecognizer != null)
            {
                sequenceRecognizer.OnRawTokenAdded -= HandleRawTokenAdded;
            }
        }

        private void Start()
        {
            ResolveReferences();
            EnsureArrowsCreated();
        }

        public void ResolveReferences()
        {
            if (wheelController == null)
            {
                wheelController = GetComponentInParent<VectorWheelController>() ?? FindAnyObjectByType<VectorWheelController>();
            }

            if (sequenceRecognizer == null)
            {
                if (wheelController != null)
                {
                    sequenceRecognizer = wheelController.SequenceRecognizer;
                }
                if (sequenceRecognizer == null)
                {
                    sequenceRecognizer = FindAnyObjectByType<DirectionSequenceRecognizer>();
                }
            }

            if (ringContainer == null)
            {
                ringContainer = GetComponent<RectTransform>();
            }

#if UNITY_EDITOR
            if (arrowSprite == null)
            {
                arrowSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Direction_Arrow_8Way.png");
            }
#endif
        }

        /// <summary>
        /// Создает или пересобирает 8 стрелок вокруг центра.
        /// </summary>
        [ContextMenu("Rebuild 8-Way Arrows")]
        public void EnsureArrowsCreated()
        {
            if (ringContainer == null) ringContainer = GetComponent<RectTransform>();

#if UNITY_EDITOR
            if (arrowSprite == null)
            {
                arrowSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Direction_Arrow_8Way.png");
            }
#endif

            // Если список уже заполнен и все объекты существуют, просто обновляем их трансформы
            bool needsFullRebuild = arrowSlots == null || arrowSlots.Count != 8;
            if (!needsFullRebuild)
            {
                for (int i = 0; i < arrowSlots.Count; i++)
                {
                    if (arrowSlots[i].rectTransform == null || arrowSlots[i].image == null)
                    {
                        needsFullRebuild = true;
                        break;
                    }
                }
            }

            if (needsFullRebuild)
            {
                arrowSlots = new List<ArrowSlot>(8);

                foreach (var dir in AllDirections)
                {
                    string slotName = "Arrow_" + dir;
                    Transform existingChild = ringContainer.Find(slotName);
                    GameObject arrowGo = existingChild != null ? existingChild.gameObject : new GameObject(slotName, typeof(RectTransform), typeof(Image));
                    arrowGo.transform.SetParent(ringContainer, false);

                    var rt = arrowGo.GetComponent<RectTransform>();
                    var img = arrowGo.GetComponent<Image>();

                    if (arrowSprite != null)
                    {
                        img.sprite = arrowSprite;
                    }
                    img.raycastTarget = false;

                    arrowSlots.Add(new ArrowSlot
                    {
                        direction = dir,
                        image = img,
                        rectTransform = rt
                    });
                }
            }

            // Позиционируем и поворачиваем каждую стрелку
            for (int i = 0; i < arrowSlots.Count; i++)
            {
                var slot = arrowSlots[i];
                if (slot.rectTransform == null) continue;

                float angle = slot.direction.ToAngle();
                float rad = angle * Mathf.Deg2Rad;

                slot.rectTransform.sizeDelta = arrowSize;
                slot.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * ringRadius;
                slot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);

                if (arrowSprite != null && slot.image != null && slot.image.sprite != arrowSprite)
                {
                    slot.image.sprite = arrowSprite;
                }
            }
        }

        private void Update()
        {
            if (arrowSlots == null || arrowSlots.Count == 0) return;

            Direction8 activeDir = Direction8.None;
            bool isParry = false;
            float wheelAlpha = 0.5f;

            if (wheelController != null)
            {
                activeDir = wheelController.CurrentDirection;
                isParry = wheelController.ActiveGestureButton == WheelGestureButton.RMB;

                float idleAlpha = wheelController.CurrentSettings != null ? wheelController.CurrentSettings.idleOpacity : 0.40f;
                wheelAlpha = Mathf.Lerp(idleAlpha, 1.0f, wheelController.FadeAlpha);

                // Если удерживается ЛКМ или ПКМ, альфа колеса держится на максимуме
                if (wheelController.IsDragging)
                {
                    wheelAlpha = Mathf.Max(wheelAlpha, 0.85f);
                }
            }

            for (int i = 0; i < arrowSlots.Count; i++)
            {
                var slot = arrowSlots[i];
                if (slot.rectTransform == null || slot.image == null) continue;

                bool isActive = (slot.direction == activeDir && activeDir != Direction8.None);

                // 1. Анимация масштаба
                float targetScale = isActive ? activeScaleMultiplier : 1.0f;
                if (isActive)
                {
                    targetScale += 0.08f * Mathf.Sin(Time.time * pulseSpeed);
                }

                if (Application.isPlaying)
                {
                    slot.currentScale = Mathf.Lerp(slot.currentScale, targetScale, Time.deltaTime * scaleSmoothing);
                    slot.punchScale = Mathf.MoveTowards(slot.punchScale, 0f, Time.deltaTime * punchDecay);
                    slot.flashAmount = Mathf.MoveTowards(slot.flashAmount, 0f, Time.deltaTime * flashDecay);
                }
                else
                {
                    slot.currentScale = targetScale;
                    slot.punchScale = 0f;
                    slot.flashAmount = 0f;
                }

                float totalScale = slot.currentScale + slot.punchScale;
                slot.rectTransform.localScale = Vector3.one * totalScale;

                // 2. Цвета и подсветка
                Color baseCol = isActive ? (isParry ? parryActiveColor : activeColor) : idleColor;
                baseCol.a = Mathf.Clamp01(baseCol.a * wheelAlpha);

                Color finalCol = Color.Lerp(baseCol, tokenFlashColor, slot.flashAmount);
                slot.image.color = finalCol;
            }
        }

        private void HandleRawTokenAdded(Direction8 rawDir)
        {
            if (arrowSlots == null) return;

            for (int i = 0; i < arrowSlots.Count; i++)
            {
                if (arrowSlots[i].direction == rawDir)
                {
                    arrowSlots[i].punchScale = punchScaleAmount;
                    arrowSlots[i].flashAmount = 1.0f;
                    break;
                }
            }
        }

        /// <summary>
        /// Настройка радиуса и размера стрелок из кода или настроек.
        /// </summary>
        public void SetLayout(float radius, Vector2 size)
        {
            ringRadius = radius;
            arrowSize = size;
            EnsureArrowsCreated();
        }
    }
}
