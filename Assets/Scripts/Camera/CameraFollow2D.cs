// Assets/Scripts/Camera/CameraFollow2D.cs
using System;
using System.Collections;
using UnityEngine;

namespace Combat.Cameras
{
    /// <summary>
    /// Плавная и отзывчивая 2D-камера слежения за персонажем.
    /// Решает проблему дергания камеры:
    /// 1. Использует непрерывную плавающую мертвую зону (Continuous Bounding Box) без скачков позиций.
    /// 2. Исполняется в LateUpdate с высоким приоритетом [DefaultExecutionOrder(100)] после интерполяции Rigidbody2D.
    /// 3. Сглаживает движение через Mathf.SmoothDamp с защитой от микро-спайков кадров (clamped delta time).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public class CameraFollow2D : MonoBehaviour
    {
        [Header("--- Target (Цель слежения) ---")]
        [Tooltip("Объект, за которым следует камера (обычно игрок)")]
        [SerializeField] private Transform target;

        [Header("--- Deadzone Box (Мёртвая зона) ---")]
        [Tooltip("Размер коробки мертвой зоны (Ширина, Высота). Внутри коробки микро-смещения персонажа не дергают камеру. При выходе за границу коробка мягко скользит с игроком.")]
        [SerializeField] private Vector2 deadzoneSize = new Vector2(0.6f, 0.4f);

        [Tooltip("Смещение фокуса камеры относительно цели (X, Y)")]
        [SerializeField] private Vector2 offset = Vector2.zero;

        [Header("--- Smooth Follow (Плавность следования) ---")]
        [Tooltip("Время сглаживания по горизонтали в секундах (0.1 - 0.15 для быстрой и плавной отзывчивости)")]
        [Range(0.01f, 0.5f)]
        [SerializeField] private float smoothTimeX = 0.12f;

        [Tooltip("Время сглаживания по вертикали в секундах")]
        [Range(0.01f, 0.5f)]
        [SerializeField] private float smoothTimeY = 0.15f;

        [Header("--- Gizmos ---")]
        [SerializeField] private bool drawGizmosInEditor = true;

        // Runtime Focus State
        private Vector2 _focusPoint;
        private Vector2 _currentVelocity;
        private bool _isInitialized = false;

        private Coroutine _shakeRoutine;
        private Vector2 _shakeOffset = Vector2.zero;

        #region Properties
        public Transform Target { get => target; set => target = value; }
        public Vector2 DeadzoneSize { get => deadzoneSize; set => deadzoneSize = new Vector2(Mathf.Max(0f, value.x), Mathf.Max(0f, value.y)); }
        public Vector2 Offset { get => offset; set => offset = value; }
        public float SmoothTimeX { get => smoothTimeX; set => smoothTimeX = Mathf.Max(0.01f, value); }
        public float SmoothTimeY { get => smoothTimeY; set => smoothTimeY = Mathf.Max(0.01f, value); }
        #endregion

        private void Start()
        {
            if (target == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player == null) player = GameObject.Find("Player");
                if (player != null) target = player.transform;
            }

            if (target != null)
            {
                SnapToTarget();
                _isInitialized = true;
            }
        }

        /// <summary>
        /// Мгновенно перемещает камеру на цель без сглаживания (при спавне, телепортах).
        /// </summary>
        public void SnapToTarget()
        {
            if (target == null) return;
            _focusPoint = target.position;
            _currentVelocity = Vector2.zero;

            Vector3 snapPos = new Vector3(
                _focusPoint.x + offset.x,
                _focusPoint.y + offset.y,
                transform.position.z
            );
            transform.position = snapPos;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            if (!_isInitialized)
            {
                SnapToTarget();
                _isInitialized = true;
            }

            Vector2 playerPos = target.position;
            float halfWidth = Mathf.Max(0f, deadzoneSize.x * 0.5f);
            float halfHeight = Mathf.Max(0f, deadzoneSize.y * 0.5f);

            // 1. Непрерывная мёртвая зона (Continuous Bounding Box)
            // Фокус движется ТОЛЬКО когда персонаж выходит за край коробки,
            // при этом смещение происходит ровно на величину выхода за край — БЕЗ резких скачков!
            float deltaX = playerPos.x - _focusPoint.x;
            if (Mathf.Abs(deltaX) > halfWidth)
            {
                _focusPoint.x = playerPos.x - Mathf.Sign(deltaX) * halfWidth;
            }

            float deltaY = playerPos.y - _focusPoint.y;
            if (Mathf.Abs(deltaY) > halfHeight)
            {
                _focusPoint.y = playerPos.y - Mathf.Sign(deltaY) * halfHeight;
            }

            // 2. Целевая позиция камеры
            float targetX = _focusPoint.x + offset.x;
            float targetY = _focusPoint.y + offset.y;

            // 3. Защищенный дельта-тайм от микрофризов и смены timeScale
            float dt = Time.timeScale > 0.001f ? Time.deltaTime : Time.unscaledDeltaTime;
            dt = Mathf.Clamp(dt, 0.0001f, 0.05f);

            // 4. Идеально плавный SmoothDamp
            float newX = Mathf.SmoothDamp(transform.position.x, targetX, ref _currentVelocity.x, Mathf.Max(0.01f, smoothTimeX), Mathf.Infinity, dt);
            float newY = Mathf.SmoothDamp(transform.position.y, targetY, ref _currentVelocity.y, Mathf.Max(0.01f, smoothTimeY), Mathf.Infinity, dt);

            transform.position = new Vector3(newX + _shakeOffset.x, newY + _shakeOffset.y, transform.position.z);
        }

        /// <summary>
        /// Тряска экрана (импульс при взрывах или сильных ударах).
        /// </summary>
        public void StopShake()
        {
            if (_shakeRoutine != null)
            {
                StopCoroutine(_shakeRoutine);
                _shakeRoutine = null;
            }
            _shakeOffset = Vector2.zero;
        }

        public void Shake(float duration = 0.35f, float intensity = 0.45f)
        {
            if (!gameObject.activeInHierarchy) return;
            if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
            _shakeRoutine = StartCoroutine(ShakeRoutine(duration, intensity));
        }

        private IEnumerator ShakeRoutine(float duration, float intensity)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                float currentIntensity = Mathf.Lerp(intensity, 0f, progress);
                _shakeOffset = UnityEngine.Random.insideUnitCircle * currentIntensity;
                yield return null;
            }
            _shakeOffset = Vector2.zero;
            _shakeRoutine = null;
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmosInEditor) return;

            Vector2 center = Application.isPlaying ? _focusPoint : (target != null ? (Vector2)target.position : (Vector2)transform.position);

            // Отрисовка коробки мертвой зоны в окне Scene
            Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.75f);
            Gizmos.DrawWireCube(new Vector3(center.x + offset.x, center.y + offset.y, 0f), new Vector3(deadzoneSize.x, deadzoneSize.y, 0.1f));

            // Точка центра коробки (фокус)
            Gizmos.color = new Color(1f, 0.85f, 0.15f, 0.9f);
            Gizmos.DrawWireSphere(new Vector3(center.x + offset.x, center.y + offset.y, 0f), 0.08f);

            // Если задан offset, рисуем вектор до камеры
            if (offset.sqrMagnitude > 0.001f)
            {
                Gizmos.color = new Color(1f, 0.3f, 0.4f, 0.6f);
                Gizmos.DrawLine(new Vector3(center.x, center.y, 0f), new Vector3(center.x + offset.x, center.y + offset.y, 0f));
                Gizmos.DrawWireSphere(new Vector3(center.x + offset.x, center.y + offset.y, 0f), 0.06f);
            }
        }
    }
}
