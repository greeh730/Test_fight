using System;
using UnityEngine;

namespace Combat.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerController2D : MonoBehaviour
    {
        [Header("--- Horizontal Movement ---")]
        [Tooltip("Максимальная горизонтальная скорость бега")]
        [SerializeField] private float moveSpeed = 8.5f;

        [Tooltip("Ускорение на земле (высокое для отзывчивости)")]
        [SerializeField] private float groundAcceleration = 65f;

        [Tooltip("Торможение на земле (резкая остановка без скольжения)")]
        [SerializeField] private float groundDeceleration = 75f;

        [Tooltip("Ускорение в воздухе для управляемости прыжка")]
        [SerializeField] private float airAcceleration = 35f;

        [Header("--- Jump & Air Control ---")]
        [Tooltip("Начальная вертикальная сила прыжка")]
        [SerializeField] private float jumpForce = 13.5f;

        [Tooltip("Базовый масштаб гравитации Rigidbody2D")]
        [SerializeField] private float baseGravityScale = 3.5f;

        [Tooltip("Множитель гравитации при падении (быстрое и четкое приземление)")]
        [SerializeField] private float fallMultiplier = 1.4f;

        [Tooltip("Множитель гравитации при раннем отпускании кнопки прыжка (короткий прыжок)")]
        [SerializeField] private float lowJumpMultiplier = 2.2f;

        [Tooltip("Ускорение быстрого падения при зажатии S в воздухе")]
        [SerializeField] private float fastFallMultiplier = 1.8f;

        [Tooltip("Coyote Time: время после схода с платформы, когда прыжок еще доступен")]
        [SerializeField] private float coyoteTime = 0.12f;

        [Tooltip("Jump Buffer: время буферизации нажатия прыжка до приземления")]
        [SerializeField] private float jumpBufferTime = 0.12f;

        [Header("--- Ground Detection ---")]
        [Tooltip("Слои, считающиеся землей (по умолчанию всё, кроме триггеров и игрока)")]
        [SerializeField] private LayerMask groundLayer = ~0;

        [Tooltip("Дистанция проверки земли вниз")]
        [SerializeField] private float groundCheckDistance = 0.08f;

        [Header("--- Visual & Game Feel ---")]
        [Tooltip("Эффект сжатия и растяжения при прыжке и приземлении")]
        [SerializeField] private bool enableJuiceSquashStretch = true;
        [SerializeField] private float squashStretchSpeed = 12f;

        // Components
        private Rigidbody2D _rb;
        private Collider2D _col;
        private SpriteRenderer _sr;
        private ContactFilter2D _groundFilter;
        private readonly RaycastHit2D[] _groundHits = new RaycastHit2D[6];

        // State
        public bool IsGrounded { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsJumping => _isJumping;
        public Vector2 Velocity => _rb != null ? _rb.linearVelocity : Vector2.zero;

        private float _horizontalInput;
        private bool _jumpPressed;
        private bool _jumpHeld;
        private bool _downHeld;

        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private bool _isJumping;

        // Juice Animation
        private Vector3 _baseScale = Vector3.one;
        private Vector3 _targetScale = Vector3.one;
        private bool _wasGroundedLastFrame;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<Collider2D>();
            _sr = GetComponent<SpriteRenderer>();

            _baseScale = transform.localScale;
            _targetScale = _baseScale;

            _groundFilter = new ContactFilter2D();
            _groundFilter.useTriggers = false;
            _groundFilter.SetLayerMask(groundLayer);

            ConfigurePhysics();
        }

        private void ConfigurePhysics()
        {
            if (_rb != null)
            {
                _rb.bodyType = RigidbodyType2D.Dynamic;
                _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                _rb.freezeRotation = true;
                _rb.gravityScale = baseGravityScale;
            }

            // Назначаем материал без трения, чтобы игрок не лип к стенам
            var noFrictionMat = new PhysicsMaterial2D("PlayerMaterial_ZeroFriction")
            {
                friction = 0f,
                bounciness = 0f
            };
            if (_col != null)
            {
                _col.sharedMaterial = noFrictionMat;
            }
        }

        private void Update()
        {
            GatherInput();
            UpdateTimers();
            UpdateVisuals();
        }

        private void FixedUpdate()
        {
            CheckGrounded();
            HandleHorizontalMovement();
            HandleJumpAndGravity();

            _wasGroundedLastFrame = IsGrounded;
        }

        private void GatherInput()
        {
            float h = 0f;
            bool jumpDown = false;
            bool jumpHold = false;
            bool downHold = false;

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) h -= 1f;

                if (kb.wKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
                    jumpDown = true;

                if (kb.wKey.isPressed || kb.spaceKey.isPressed || kb.upArrowKey.isPressed)
                    jumpHold = true;

                if (kb.sKey.isPressed || kb.downArrowKey.isPressed)
                    downHold = true;
            }

            var gp = UnityEngine.InputSystem.Gamepad.current;
            if (gp != null)
            {
                float stickX = gp.leftStick.x.ReadValue();
                if (Mathf.Abs(stickX) > 0.2f) h = Mathf.Sign(stickX);
                if (gp.dpad.right.isPressed) h += 1f;
                if (gp.dpad.left.isPressed) h -= 1f;

                if (gp.buttonSouth.wasPressedThisFrame || gp.dpad.up.wasPressedThisFrame)
                    jumpDown = true;
                if (gp.buttonSouth.isPressed || gp.dpad.up.isPressed)
                    jumpHold = true;
                if (gp.dpad.down.isPressed || gp.leftStick.y.ReadValue() < -0.5f)
                    downHold = true;
            }
#endif

            // Fallback на случай Legacy Input
            try
            {
                if (h == 0f) h = Input.GetAxisRaw("Horizontal");
                if (!jumpDown && (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow)))
                    jumpDown = true;
                if (!jumpHold && (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.UpArrow)))
                    jumpHold = true;
                if (!downHold && (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)))
                    downHold = true;
            }
            catch { /* Игнорируем в случае строгого New Input System */ }

            _horizontalInput = Mathf.Clamp(h, -1f, 1f);
            _jumpHeld = jumpHold;
            _downHeld = downHold;

            if (jumpDown)
            {
                _jumpBufferTimer = jumpBufferTime;
            }
        }

        private void UpdateTimers()
        {
            // Coyote Timer
            if (IsGrounded)
            {
                _coyoteTimer = coyoteTime;
            }
            else
            {
                _coyoteTimer -= Time.deltaTime;
            }

            // Jump Buffer Timer
            if (_jumpBufferTimer > 0f)
            {
                _jumpBufferTimer -= Time.deltaTime;
            }
        }

        private void CheckGrounded()
        {
            if (_col == null) return;

            int count = _col.Cast(Vector2.down, _groundFilter, _groundHits, groundCheckDistance);
            bool groundedNow = false;

            for (int i = 0; i < count; i++)
            {
                var hit = _groundHits[i];
                if (hit.collider != null && hit.collider != _col && !hit.collider.isTrigger)
                {
                    // Проверяем что поверхность действительно снизу (нормаль вверх)
                    if (hit.normal.y > 0.5f)
                    {
                        groundedNow = true;
                        break;
                    }
                }
            }

            // Детекция приземления (для эффекта squash)
            if (!IsGrounded && groundedNow && _rb.linearVelocity.y <= 0.1f)
            {
                if (enableJuiceSquashStretch)
                {
                    _targetScale = new Vector3(_baseScale.x * 1.25f, _baseScale.y * 0.75f, _baseScale.z);
                }
            }

            IsGrounded = groundedNow;

            if (IsGrounded && _rb.linearVelocity.y <= 0.05f)
            {
                _isJumping = false;
            }
        }

        private void HandleHorizontalMovement()
        {
            float targetVelocityX = _horizontalInput * moveSpeed;
            float currentVelocityX = _rb.linearVelocity.x;

            float accelRate;
            if (IsGrounded)
            {
                // Если направление ввода совпадает или отлично от нуля — ускоряемся, иначе тормозим
                accelRate = (Mathf.Abs(targetVelocityX) > 0.01f) ? groundAcceleration : groundDeceleration;
            }
            else
            {
                accelRate = airAcceleration;
            }

            float newVelocityX = Mathf.MoveTowards(currentVelocityX, targetVelocityX, accelRate * Time.fixedDeltaTime);
            _rb.linearVelocity = new Vector2(newVelocityX, _rb.linearVelocity.y);
        }

        private void HandleJumpAndGravity()
        {
            // 1. Попытка совершить прыжок (Jump Buffer + Coyote Time)
            if (_jumpBufferTimer > 0f && _coyoteTimer > 0f)
            {
                ExecuteJump();
            }

            // 2. Управление гравитацией для сочного платформинга
            if (_rb.linearVelocity.y < -0.01f)
            {
                // Падение вниз: гравитация выше для быстрого приземления
                float mult = fallMultiplier;
                if (_downHeld)
                {
                    mult *= fastFallMultiplier; // Быстрое падение на S
                }
                _rb.gravityScale = baseGravityScale * mult;
            }
            else if (_rb.linearVelocity.y > 0.01f && !_jumpHeld)
            {
                // Раннее отпускание кнопки прыжка (короткий прыжок)
                _rb.gravityScale = baseGravityScale * lowJumpMultiplier;
            }
            else
            {
                // Обычный подъем
                _rb.gravityScale = baseGravityScale;
            }
        }

        private void ExecuteJump()
        {
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
            _isJumping = true;

            // Эффект stretch при прыжке
            if (enableJuiceSquashStretch)
            {
                _targetScale = new Vector3(_baseScale.x * 0.8f, _baseScale.y * 1.25f, _baseScale.z);
            }
        }

        private void UpdateVisuals()
        {
            // 1. Разворот спрайта по направлению движения (A = влево, D = вправо)
            if (_sr != null && Mathf.Abs(_horizontalInput) > 0.05f)
            {
                _sr.flipX = _horizontalInput < 0f;
            }

            // 2. Плавная интерполяция сжатия/растяжения (Squash & Stretch)
            if (enableJuiceSquashStretch)
            {
                _targetScale = Vector3.Lerp(_targetScale, _baseScale, Time.deltaTime * squashStretchSpeed);
                transform.localScale = _targetScale;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (_col == null) _col = GetComponent<Collider2D>();
            if (_col != null)
            {
                Bounds bounds = _col.bounds;
                Vector2 boxSize = new Vector2(bounds.size.x * 0.9f, 0.04f);
                Vector2 boxCenter = new Vector2(bounds.center.x, bounds.min.y - (groundCheckDistance * 0.5f));
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(boxCenter, new Vector3(boxSize.x, boxSize.y + groundCheckDistance, 0.1f));
            }
        }
    }
}
