using System;
using UnityEngine;

namespace Combat.Cameras
{
    [DisallowMultipleComponent]
    public class CameraFollow2D : MonoBehaviour
    {
        [Header("--- Target ---")]
        [SerializeField] private Transform target;

        [Header("--- Deadzone Box (Зона покоя вокруг игрока) ---")]
        [Tooltip("Размер невидимой коробки вокруг игрока (Ширина, Высота). Пока игрок внутри неё, камера неподвижна.")]
        [SerializeField] private Vector2 deadzoneSize = new Vector2(2.4f, 1.8f);

        [Tooltip("Смещение фокуса камеры относительно игрока (по умолчанию 0,0 для прицела строго в центр)")]
        [SerializeField] private Vector2 offset = Vector2.zero;

        [Header("--- Smooth Follow (Плавность следования) ---")]
        [Tooltip("Время сглаживания по горизонтали (чем больше, тем мягче движение)")]
        [SerializeField] private float smoothTimeX = 0.28f;

        [Tooltip("Время сглаживания по вертикали")]
        [SerializeField] private float smoothTimeY = 0.35f;

        [Header("--- Re-centering (Центрирование на игроке) ---")]
        [Tooltip("Плавно возвращать коробку и фокус точно в центр игрока при остановке")]
        [SerializeField] private bool autoRecenter = true;

        [Tooltip("Скорость центрирования коробки при остановке игрока")]
        [SerializeField] private float recenterSpeed = 3.0f;

        [Tooltip("Задержка перед центрированием после остановки (в секундах)")]
        [SerializeField] private float recenterDelay = 0.25f;

        [Header("--- Lookahead (Упреждение при серьезном беге) ---")]
        [Tooltip("Включить смещение камеры вперед в сторону длительного бега")]
        [SerializeField] private bool enableLookahead = true;

        [Tooltip("Дистанция упреждения вперед при беге")]
        [SerializeField] private float lookaheadDistance = 1.6f;

        [Tooltip("Минимальная скорость бега, необходимая для включения упреждения (исключает дрожание от нажатий A/D)")]
        [SerializeField] private float minSpeedForLookahead = 3.0f;

        [Tooltip("Скорость плавного смещения упреждения")]
        [SerializeField] private float lookaheadSmoothSpeed = 2.2f;

        [Header("--- Gizmos ---")]
        [SerializeField] private bool drawGizmosInEditor = true;

        // Runtime Focus State
        private Vector2 _focusPoint;
        private Vector2 _currentVelocity;
        private float _currentLookaheadX;
        private float _targetLookaheadX;
        private Rigidbody2D _targetRb;
        private float _idleTimer;
        private bool _isInitialized = false;

        private void Start()
        {
            if (target == null)
            {
                var player = GameObject.Find("Player");
                if (player != null) target = player.transform;
            }

            if (target != null)
            {
                _targetRb = target.GetComponent<Rigidbody2D>();
                _focusPoint = target.position;
                SnapToTarget();
                _isInitialized = true;
            }
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            _focusPoint = target.position;
            _currentLookaheadX = 0f;
            _targetLookaheadX = 0f;
            _currentVelocity = Vector2.zero;
            _idleTimer = 0f;

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
                _targetRb = target.GetComponent<Rigidbody2D>();
                _focusPoint = target.position;
                _isInitialized = true;
            }

            Vector2 playerPos = target.position;

            // 1. Расчет мертвой зоны (Deadzone Box)
            float halfWidth = deadzoneSize.x * 0.5f;
            float halfHeight = deadzoneSize.y * 0.5f;
            bool isPushedX = false;
            bool isPushedY = false;

            if (playerPos.x > _focusPoint.x + halfWidth)
            {
                _focusPoint.x = playerPos.x - halfWidth;
                isPushedX = true;
            }
            else if (playerPos.x < _focusPoint.x - halfWidth)
            {
                _focusPoint.x = playerPos.x + halfWidth;
                isPushedX = true;
            }

            if (playerPos.y > _focusPoint.y + halfHeight)
            {
                _focusPoint.y = playerPos.y - halfHeight;
                isPushedY = true;
            }
            else if (playerPos.y < _focusPoint.y - halfHeight)
            {
                _focusPoint.y = playerPos.y + halfHeight;
                isPushedY = true;
            }

            // 2. Плавный возврат в центр игрока при остановке (Re-centering)
            if (autoRecenter)
            {
                float speedX = _targetRb != null ? Mathf.Abs(_targetRb.linearVelocity.x) : 0f;
                float speedY = _targetRb != null ? Mathf.Abs(_targetRb.linearVelocity.y) : 0f;
                bool isIdle = speedX < 0.2f && speedY < 0.2f && !isPushedX && !isPushedY;

                if (isIdle)
                {
                    _idleTimer += Time.deltaTime;
                    if (_idleTimer >= recenterDelay)
                    {
                        _focusPoint = Vector2.MoveTowards(_focusPoint, playerPos, recenterSpeed * Time.deltaTime);
                    }
                }
                else
                {
                    _idleTimer = 0f;
                }
            }

            // 3. Упреждение взгляда (Lookahead) только при уверенном беге с выталкиванием коробки
            if (enableLookahead)
            {
                float speedX = _targetRb != null ? _targetRb.linearVelocity.x : 0f;

                // Упреждение включается только когда игрок активно толкает коробку на высокой скорости
                if (isPushedX && Mathf.Abs(speedX) >= minSpeedForLookahead)
                {
                    _targetLookaheadX = Mathf.Sign(speedX) * lookaheadDistance;
                }
                else
                {
                    _targetLookaheadX = 0f;
                }

                _currentLookaheadX = Mathf.MoveTowards(_currentLookaheadX, _targetLookaheadX, Time.deltaTime * lookaheadSmoothSpeed);
            }
            else
            {
                _currentLookaheadX = 0f;
            }

            // 4. Целевая позиция камеры
            float targetX = _focusPoint.x + offset.x + _currentLookaheadX;
            float targetY = _focusPoint.y + offset.y;

            // 5. Независимое сглаживание по X и Y (SmoothDamp)
            float newX = Mathf.SmoothDamp(transform.position.x, targetX, ref _currentVelocity.x, smoothTimeX);
            float newY = Mathf.SmoothDamp(transform.position.y, targetY, ref _currentVelocity.y, smoothTimeY);

            transform.position = new Vector3(newX, newY, transform.position.z);
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmosInEditor) return;

            Vector2 center = Application.isPlaying ? _focusPoint : (target != null ? (Vector2)target.position : (Vector2)transform.position);

            // Отрисовка коробки мертвой зоны в окне Scene
            Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.85f);
            Gizmos.DrawWireCube(new Vector3(center.x, center.y, 0f), new Vector3(deadzoneSize.x, deadzoneSize.y, 0.1f));

            // Точка центра коробки (фокус)
            Gizmos.color = new Color(1f, 0.85f, 0.15f, 0.9f);
            Gizmos.DrawWireSphere(new Vector3(center.x, center.y, 0f), 0.08f);

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
