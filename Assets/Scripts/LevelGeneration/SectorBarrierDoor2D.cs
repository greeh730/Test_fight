using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LevelGeneration
{
    public enum EnemyBlockerDirection
    {
        None,
        Entrance, // Слева от двери: защищает Chunk 0 от проникновения врагов
        Exit      // Справа от двери: защищает финальную комнату от проникновения врагов
    }

    /// <summary>
    /// Энергетический барьер / дверь боевого сектора.
    /// Блокирует проход игрока и врагов физическим коллайдером в закрытом состоянии
    /// и плавно растворяется в открытом.
    /// Также содержит выделенный непробиваемый барьер врагов (EnemyBlocker),
    /// который всегда активен и не позволяет врагам покинуть боевую арену.
    /// </summary>
    [DisallowMultipleComponent]
    public class SectorBarrierDoor2D : MonoBehaviour
    {
        [Header("--- Геометрия двери ---")]
        [Tooltip("Высота барьера (должна быть достаточно высокой, чтобы боты и игрок не перепрыгнули)")]
        [SerializeField] private float barrierHeight = 30.0f;
        [Tooltip("Ширина барьера")]
        [SerializeField] private float barrierWidth = 1.2f;
        [Tooltip("Дополнительное заглубление коллайдера вниз под землю (защита от проскальзывания врагов снизу)")]
        [SerializeField] private float extraBottomDepth = 10.0f;

        [Header("--- Односторонний барьер врагов ---")]
        [Tooltip("Режим блокировки врагов: Entrance (слева) или Exit (справа)")]
        [SerializeField] private EnemyBlockerDirection enemyBlocker = EnemyBlockerDirection.None;
        [Tooltip("Толщина барьера удержания врагов (метров)")]
        [SerializeField] private float enemyBlockerWidth = 4.0f;

        [Header("--- Настройки состояния ---")]
        [Tooltip("Закрыта ли дверь в данный момент")]
        [SerializeField] private bool isClosed = true;

        [Header("--- Визуальные эффекты ---")]
        [Tooltip("Цвет запертого барьера (тревожный красный/оранжевый)")]
        [SerializeField] private Color closedColor = new Color(1.0f, 0.18f, 0.28f, 0.85f);

        [Tooltip("Цвет открытого барьера (мягкий полупрозрачный зеленый/бирюзовый)")]
        [SerializeField] private Color openColor = new Color(0.0f, 1.0f, 0.65f, 0.12f);

        [Tooltip("Длительность перехода открытия/закрытия")]
        [SerializeField] private float transitionDuration = 0.35f;

        private BoxCollider2D _collider;
        private EnemyBlockerBarrier2D _enemyBlockerComponent;
        private SpriteRenderer _fieldRenderer;
        private List<SpriteRenderer> _laserBars = new List<SpriteRenderer>();
        private Transform _visualRoot;
        private Coroutine _transitionRoutine;
        private static Sprite _sharedBoxSprite;

        public bool IsClosed => isClosed;
        public EnemyBlockerDirection BlockerDirection => enemyBlocker;
        public EnemyBlockerBarrier2D EnemyBlocker => _enemyBlockerComponent;

        private void Awake()
        {
            SetupComponents();
        }

        private void SetupComponents()
        {
            if (_sharedBoxSprite == null)
            {
                Texture2D tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                _sharedBoxSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0f), 1f);
            }

            float totalHeight = barrierHeight + extraBottomDepth;
            float centerY = (barrierHeight - extraBottomDepth) * 0.5f;

            // Настройка физического коллайдера основной двери
            _collider = GetComponent<BoxCollider2D>();
            if (_collider == null)
            {
                _collider = gameObject.AddComponent<BoxCollider2D>();
            }

            _collider.size = new Vector2(barrierWidth, totalHeight);
            _collider.offset = new Vector2(0f, centerY);
            _collider.isTrigger = false;
            _collider.enabled = isClosed;

            // Настройка выделенного непробиваемого барьера врагов
            SetupEnemyBlocker();

            // Настройка визуальных элементов
            if (_visualRoot == null)
            {
                var existingVisual = transform.Find("VisualRoot");
                if (existingVisual != null)
                {
                    _visualRoot = existingVisual;
                }
                else
                {
                    var visualGo = new GameObject("VisualRoot");
                    visualGo.transform.SetParent(transform, false);
                    _visualRoot = visualGo.transform;
                }
            }

            // Основное световое поле
            var fieldTform = _visualRoot.Find("FieldGlow");
            if (fieldTform == null)
            {
                var fieldGo = new GameObject("FieldGlow");
                fieldGo.transform.SetParent(_visualRoot, false);
                _fieldRenderer = fieldGo.AddComponent<SpriteRenderer>();
                _fieldRenderer.sprite = _sharedBoxSprite;
                _fieldRenderer.sortingOrder = 5;
            }
            else
            {
                _fieldRenderer = fieldTform.GetComponent<SpriteRenderer>();
            }
            _fieldRenderer.transform.localScale = new Vector3(barrierWidth * 1.4f, barrierHeight, 1f);

            // Боковые лазерные полосы для киберпанк-стиля
            _laserBars.Clear();
            float[] offsets = { -barrierWidth * 0.35f, 0f, barrierWidth * 0.35f };
            for (int i = 0; i < offsets.Length; i++)
            {
                string barName = $"LaserBar_{i}";
                var barTform = _visualRoot.Find(barName);
                SpriteRenderer barSr;
                if (barTform == null)
                {
                    var barGo = new GameObject(barName);
                    barGo.transform.SetParent(_visualRoot, false);
                    barSr = barGo.AddComponent<SpriteRenderer>();
                    barSr.sprite = _sharedBoxSprite;
                    barSr.sortingOrder = 6;
                }
                else
                {
                    barSr = barTform.GetComponent<SpriteRenderer>();
                }
                barSr.transform.localPosition = new Vector3(offsets[i], 0f, 0f);
                barSr.transform.localScale = new Vector3(0.08f, barrierHeight, 1f);
                _laserBars.Add(barSr);
            }

            // Опорные генераторы барьера (верх и низ)
            CreateCap("CapBottom", new Vector3(0f, 0f, 0f), new Vector3(barrierWidth * 2.2f, 0.35f, 1f));
            CreateCap("CapTop", new Vector3(0f, barrierHeight - 0.35f, 0f), new Vector3(barrierWidth * 2.2f, 0.35f, 1f));

            ApplyVisualState(isClosed, 1.0f);
        }

        private void SetupEnemyBlocker()
        {
            var blockerTform = transform.Find("[EnemyBlocker_Wall]");
            if (blockerTform == null)
            {
                var blockerGo = new GameObject("[EnemyBlocker_Wall]");
                blockerGo.transform.SetParent(transform, false);
                blockerTform = blockerGo.transform;
            }

            _enemyBlockerComponent = blockerTform.GetComponent<EnemyBlockerBarrier2D>();
            if (_enemyBlockerComponent == null)
            {
                _enemyBlockerComponent = blockerTform.gameObject.AddComponent<EnemyBlockerBarrier2D>();
            }

            float totalHeight = barrierHeight + extraBottomDepth;
            float centerY = (barrierHeight - extraBottomDepth) * 0.5f;

            if (enemyBlocker == EnemyBlockerDirection.Entrance)
            {
                // Расположен слева от двери: от x = -enemyBlockerWidth до x = 0
                _enemyBlockerComponent.ConfigureGeometry(
                    new Vector2(enemyBlockerWidth, totalHeight),
                    new Vector2(-enemyBlockerWidth * 0.5f, centerY)
                );
                _enemyBlockerComponent.gameObject.SetActive(true);
            }
            else if (enemyBlocker == EnemyBlockerDirection.Exit)
            {
                // Расположен справа от двери: от x = 0 до x = enemyBlockerWidth
                _enemyBlockerComponent.ConfigureGeometry(
                    new Vector2(enemyBlockerWidth, totalHeight),
                    new Vector2(enemyBlockerWidth * 0.5f, centerY)
                );
                _enemyBlockerComponent.gameObject.SetActive(true);
            }
            else
            {
                _enemyBlockerComponent.gameObject.SetActive(false);
            }
        }

        public void ConfigureAsEntrance()
        {
            enemyBlocker = EnemyBlockerDirection.Entrance;
            SetupComponents();
            if (_enemyBlockerComponent != null)
            {
                _enemyBlockerComponent.IgnoreAllPlayerColliders();
            }
        }

        public void ConfigureAsExit()
        {
            enemyBlocker = EnemyBlockerDirection.Exit;
            SetupComponents();
            if (_enemyBlockerComponent != null)
            {
                _enemyBlockerComponent.IgnoreAllPlayerColliders();
            }
        }

        public void IgnorePlayerObject(GameObject playerObj)
        {
            if (_enemyBlockerComponent != null)
            {
                _enemyBlockerComponent.IgnorePlayerObject(playerObj);
            }
        }

        public void IgnoreCollider(Collider2D col)
        {
            if (_enemyBlockerComponent != null)
            {
                _enemyBlockerComponent.IgnoreCollider(col);
            }
        }

        private void CreateCap(string name, Vector3 localPos, Vector3 localScale)
        {
            var capTform = _visualRoot.Find(name);
            if (capTform == null)
            {
                var capGo = new GameObject(name);
                capGo.transform.SetParent(_visualRoot, false);
                capGo.transform.localPosition = localPos;
                var sr = capGo.AddComponent<SpriteRenderer>();
                sr.sprite = _sharedBoxSprite;
                sr.color = new Color(0.25f, 0.28f, 0.35f, 1f);
                sr.sortingOrder = 7;
                capGo.transform.localScale = localScale;
            }
        }

        private void Update()
        {
            if (isClosed && _fieldRenderer != null)
            {
                // Легкая динамическая пульсация замкнутого лазерного барьера
                float pulse = 0.85f + Mathf.PingPong(Time.time * 2.5f, 0.25f);
                Color c = closedColor;
                c.a = closedColor.a * pulse;
                _fieldRenderer.color = c;
            }
        }

        /// <summary>
        /// Переключить состояние двери
        /// </summary>
        public void SetClosed(bool closed, bool instant = false)
        {
            if (_collider == null) SetupComponents();

            isClosed = closed;
            _collider.enabled = isClosed;

            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
                _transitionRoutine = null;
            }

            if (instant || !gameObject.activeInHierarchy)
            {
                ApplyVisualState(isClosed, 1.0f);
            }
            else
            {
                _transitionRoutine = StartCoroutine(TransitionRoutine(isClosed));
            }
        }

        public void OpenDoor(bool instant = false) => SetClosed(false, instant);
        public void CloseDoor(bool instant = false) => SetClosed(true, instant);

        private IEnumerator TransitionRoutine(bool closing)
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, transitionDuration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Если закрываем — t идет от 0 к 1. Если открываем — от 1 к 0
                float progress = closing ? t : (1.0f - t);
                ApplyVisualState(closing, progress);
                yield return null;
            }

            ApplyVisualState(closing, closing ? 1.0f : 0.0f);
            _transitionRoutine = null;
        }

        private void ApplyVisualState(bool targetClosed, float closedFactor)
        {
            if (_fieldRenderer == null) return;

            Color targetColor = Color.Lerp(openColor, closedColor, closedFactor);
            _fieldRenderer.color = targetColor;

            // Вертикальное схлопывание/вырастание лазерных лучей
            float scaleY = Mathf.Lerp(0.05f, barrierHeight, closedFactor);
            if (!targetClosed && closedFactor <= 0.01f)
            {
                scaleY = 0f;
            }

            for (int i = 0; i < _laserBars.Count; i++)
            {
                if (_laserBars[i] != null)
                {
                    _laserBars[i].color = new Color(targetColor.r, targetColor.g, targetColor.b, targetColor.a * 1.2f);
                    _laserBars[i].transform.localScale = new Vector3(0.08f, scaleY, 1f);
                }
            }
        }
    }
}
