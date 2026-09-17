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
        [Tooltip("Размер коробки мертвой зоны (Ширина, Высота). Пока игрок внутри неё, камера неподвижна даже при микро-шагах.")]
        [SerializeField] private Vector2 deadzoneSize = new Vector2(2.4f, 1.8f);

        [Tooltip("Смещение фокуса камеры относительно игрока (0,0 для прицела строго в центр)")]
        [SerializeField] private Vector2 offset = Vector2.zero;

        [Header("--- Smooth Follow (Плавность следования) ---")]
        [Tooltip("Время сглаживания по горизонтали (чем больше, тем мягче движение)")]
        [SerializeField] private float smoothTimeX = 0.28f;

        [Tooltip("Время сглаживания по вертикали")]
        [SerializeField] private float smoothTimeY = 0.35f;

        [Header("--- Gizmos ---")]
        [SerializeField] private bool drawGizmosInEditor = true;

        // Runtime Focus State
        private Vector2 _focusPoint;
        private Vector2 _currentVelocity;
        private Rigidbody2D _targetRb;
        private Combat.Player.PlayerController2D _playerController;
        private bool _isFollowingX = false;
        private bool _isFollowingY = false;
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
                _playerController = target.GetComponent<Combat.Player.PlayerController2D>();
                _focusPoint = target.position;
                SnapToTarget();
                _isInitialized = true;
            }
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            _focusPoint = target.position;
            _currentVelocity = Vector2.zero;
            _isFollowingX = false;
            _isFollowingY = false;

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
                _playerController = target.GetComponent<Combat.Player.PlayerController2D>();
                _focusPoint = target.position;
                _isInitialized = true;
            }

            Vector2 playerPos = target.position;
            float halfWidth = deadzoneSize.x * 0.5f;
            float halfHeight = deadzoneSize.y * 0.5f;

            float speedX = _targetRb != null ? Mathf.Abs(_targetRb.linearVelocity.x) : 0f;
            float speedY = _targetRb != null ? Mathf.Abs(_targetRb.linearVelocity.y) : 0f;
            bool hasMoveInput = _playerController != null ? _playerController.HasMoveInput : (speedX > 0.3f);
            bool isGrounded = _playerController != null ? _playerController.IsGrounded : (speedY < 0.15f);

            // 1. Горизонтальная мертвая зона (независимая)
            if (!_isFollowingX)
            {
                // Камера стоит на месте, пока игрок внутри коробки (микро-шаги не двигают камеру)
                if (Mathf.Abs(playerPos.x - _focusPoint.x) > halfWidth)
                {
                    _isFollowingX = true;
                }
            }
            else
            {
                // Следование продолжается, пока игрок бежит или зажимает клавиши движения
                if (!hasMoveInput && speedX < 0.15f)
                {
                    _isFollowingX = false;
                }
            }

            if (_isFollowingX)
            {
                _focusPoint.x = playerPos.x;
            }

            // 2. Вертикальная мертвая зона (независимая)
            if (!_isFollowingY)
            {
                // Мелкие подскоки и неровности остаются внутри коробки без тряски
                if (Mathf.Abs(playerPos.y - _focusPoint.y) > halfHeight)
                {
                    _isFollowingY = true;
                }
            }
            else
            {
                // Вертикальное следование завершается, когда игрок приземлился
                if (isGrounded && speedY < 0.15f)
                {
                    _isFollowingY = false;
                }
            }

            if (_isFollowingY)
            {
                _focusPoint.y = playerPos.y;
            }

            // 3. Целевая позиция камеры (строго за персонажем, без овершута и уходов вперед)
            float targetX = _focusPoint.x + offset.x;
            float targetY = _focusPoint.y + offset.y;

            // 4. Независимое сглаживание по X и Y (SmoothDamp)
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
