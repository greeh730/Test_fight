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
        [Header("--- Horizontal Movement & Weight Feel ---")]
        [Tooltip("Максимальная горизонтальная скорость бега")]
        [SerializeField] private float moveSpeed = 8.5f;

        [Tooltip("Ускорение на земле (высокое для отзывчивости)")]
        [SerializeField] private float groundAcceleration = 65f;

        [Tooltip("Торможение на земле (резкая остановка без скольжения)")]
        [SerializeField] private float groundDeceleration = 75f;

        [Tooltip("Торможение при резкой смене направления (бег вправо -> нажали влево). Создает эффект микро-заноса (skid)")]
        [SerializeField] private float turnDeceleration = 38f;

        [Tooltip("Множитель скорости бега при зажатии Shift (Спринт)")]
        [SerializeField] private float sprintMultiplier = 1.4f;

        [Tooltip("Коэффициент сцепления с землей (1.0 = норма, 0.2 = скользкий лед, 1.8 = грязь)")]
        [SerializeField] [Range(0.05f, 3.0f)] private float friction = 1.0f;

        [Tooltip("Дистанция прилипания к спускам и лестницам (исключает подпрыгивания на спусках)")]
        [SerializeField] private float slopeDownSnapDistance = 0.35f;

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

        [Tooltip("Максимальная скорость свободного падения (Terminal Velocity)")]
        [SerializeField] private float maxFallSpeed = 22f;

        [Header("--- Double Jump & Apex Hang (Пик прыжка) ---")]
        [Tooltip("Максимальное количество прыжков в воздухе (1 = двойной прыжок)")]
        [SerializeField] private int maxAirJumps = 1;

        [Tooltip("Множитель силы прыжка в воздухе")]
        [SerializeField] private float airJumpForceMultiplier = 0.92f;

        [Tooltip("Включить парение/зависание в наивысшей точке прыжка (эффект Celeste)")]
        [SerializeField] private bool enableApexHang = true;

        [Tooltip("Порог вертикальной скорости для входа в зону пика прыжка")]
        [SerializeField] private float apexVelocityThreshold = 2.6f;

        [Tooltip("Множитель гравитации в пике прыжка (чем меньше, тем дольше парение)")]
        [SerializeField] private float apexGravityMultiplier = 0.35f;

        [Tooltip("Бонус к горизонтальной скорости в пике прыжка")]
        [SerializeField] private float apexSpeedMultiplier = 1.15f;

        [Header("--- Dash / Mobility (Двойной тап A/D или Shift) ---")]
        [Tooltip("Включить механику рывка (Dash)")]
        [SerializeField] private bool enableDash = true;

        [Tooltip("Скорость рывка")]
        [SerializeField] private float dashSpeed = 19.5f;

        [Tooltip("Длительность рывка в секундах")]
        [SerializeField] private float dashDuration = 0.16f;

        [Tooltip("Кулдаун (перезарядка) между рывками")]
        [SerializeField] private float dashCooldown = 0.5f;

        [Tooltip("Окно времени для двойного тапа A или D (в секундах)")]
        [SerializeField] private float doubleTapWindow = 0.25f;

        [Tooltip("Количество рывков в воздухе до приземления")]
        [SerializeField] private int maxAirDashes = 1;

        [Tooltip("Расход выносливости на совершение рывка")]
        [SerializeField] private float dashStaminaCost = 18f;

        [Header("--- Ground Detection ---")]
        [Tooltip("Слои, считающиеся землей (по умолчанию всё, кроме триггеров и игрока)")]
        [SerializeField] private LayerMask groundLayer = ~0;

        [Tooltip("Дистанция проверки земли вниз")]
        [SerializeField] private float groundCheckDistance = 0.08f;

        [Header("--- Visual & Game Feel ---")]
        [Tooltip("Эффект сжатия и растяжения при прыжке, приземлении и дэше")]
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
        public bool IsDashing => _isDashing;
        public bool IsSprinting => _isSprinting;
        public bool IsSkidding => _isSkidding;
        public float CurrentFriction => _currentSurfaceFriction;
        public int AirJumpsRemaining => _airJumpsLeft;
        public Vector2 Velocity => _rb != null ? _rb.linearVelocity : Vector2.zero;
        public float HorizontalInput => _horizontalInput;
        public bool HasMoveInput => Mathf.Abs(_horizontalInput) > 0.01f;

        private float _horizontalInput;
        private bool _jumpPressed;
        private bool _jumpHeld;
        private bool _downHeld;
        private bool _sprintHeld;
        private bool _isSprinting;
        private bool _isSkidding;
        private float _currentSurfaceFriction = 1.0f;

        // Timers & Stun
        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private float _hitstunTimer;
        private bool _isJumping;
        private int _airJumpsLeft;

        public bool IsHitstunned => _hitstunTimer > 0f;

        public void ApplyHitstun(float duration)
        {
            _hitstunTimer = Mathf.Max(_hitstunTimer, duration);
            if (_isDashing)
            {
                _isDashing = false;
                if (_rb != null) _rb.gravityScale = baseGravityScale;
            }
        }

        // Dash State
        private bool _isDashing;
        private float _dashTimer;
        private float _dashCooldownTimer;
        private float _dashDirection = 1f;
        private int _airDashesLeft;
        private float _lastTapTimeA = -10f;
        private float _lastTapTimeD = -10f;

        // Juice Animation
        private Vector3 _baseScale = Vector3.one;
        private Vector3 _targetScale = Vector3.one;
        private bool _wasGroundedLastFrame;

        [Header("--- Face / Head Orientation ---")]
        [Tooltip("Трансформ лица (дочерний объект Face)")]
        [SerializeField] private Transform faceTransform;

        private Vector3 _faceBaseLocalPos = new Vector3(0.16f, 0.14f, 0f);
        private Vector3 _faceBaseLocalScale = new Vector3(0.45f, 0.26f, 1f);
        private float _currentFacing = 1f;
        private Combat.PlayerCombatController2D _combatController;
        private PlayerStamina2D _stamina;
        private PlayerBlockAndParry2D _blockParry;

        public float CurrentFacing => _currentFacing;

        public void SetFacing(float direction)
        {
            if (Mathf.Abs(direction) < 0.01f) return;
            _currentFacing = Mathf.Sign(direction);

            if (_sr != null)
            {
                _sr.flipX = _currentFacing < 0f;
            }

            if (faceTransform == null) faceTransform = transform.Find("Face");
            if (faceTransform != null)
            {
                float absX = Mathf.Abs(_faceBaseLocalPos.x > 0.001f ? _faceBaseLocalPos.x : 0.16f);
                float absScaleX = Mathf.Abs(_faceBaseLocalScale.x > 0.001f ? _faceBaseLocalScale.x : 0.45f);

                faceTransform.localPosition = new Vector3(absX * _currentFacing, _faceBaseLocalPos.y, _faceBaseLocalPos.z);
                faceTransform.localScale = new Vector3(absScaleX * _currentFacing, _faceBaseLocalScale.y, _faceBaseLocalScale.z);
            }

            if (_combatController != null)
            {
                _combatController.SetFacingDirection(_currentFacing);
            }
        }

        public float CurrentFacing => _currentFacing;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<Collider2D>();
            _sr = GetComponent<SpriteRenderer>();

            _baseScale = transform.localScale;
            _targetScale = _baseScale;

            if (faceTransform == null) faceTransform = transform.Find("Face");
            if (faceTransform != null)
            {
                _faceBaseLocalPos = faceTransform.localPosition;
                _faceBaseLocalScale = faceTransform.localScale;
            }

            _combatController = GetComponent<Combat.PlayerCombatController2D>();
            _stamina = GetComponent<PlayerStamina2D>();
            _blockParry = GetComponent<PlayerBlockAndParry2D>();

            _groundFilter = new ContactFilter2D();
            _groundFilter.useTriggers = false;
            _groundFilter.SetLayerMask(groundLayer);

            _airJumpsLeft = maxAirJumps;
            _airDashesLeft = maxAirDashes;

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

            if (_isDashing)
            {
                HandleDashMovement();
            }
            else
            {
                HandleHorizontalMovement();
                HandleJumpAndGravity();
            }

            _wasGroundedLastFrame = IsGrounded;
        }

        private void GatherInput()
        {
            if (IsHitstunned)
            {
                _horizontalInput = 0f;
                _jumpHeld = false;
                _downHeld = false;
                _sprintHeld = false;
                _isSprinting = false;
                return;
            }

            float h = 0f;
            bool jumpDown = false;
            bool jumpHold = false;
            bool downHold = false;
            bool shiftHold = false;
            bool aPressedThisFrame = false;
            bool dPressedThisFrame = false;
            bool shiftPressedThisFrame = false;

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) h -= 1f;

                if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) dPressedThisFrame = true;
                if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) aPressedThisFrame = true;

                if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) shiftHold = true;
                if (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame) shiftPressedThisFrame = true;

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

                if (gp.leftTrigger.ReadValue() > 0.3f || gp.rightShoulder.isPressed) shiftHold = true;

                if (gp.buttonSouth.wasPressedThisFrame || gp.dpad.up.wasPressedThisFrame)
                    jumpDown = true;
                if (gp.buttonSouth.isPressed || gp.dpad.up.isPressed)
                    jumpHold = true;
                if (gp.dpad.down.isPressed || gp.leftStick.y.ReadValue() < -0.5f)
                    downHold = true;

                if (gp.buttonEast.wasPressedThisFrame || gp.rightTrigger.wasPressedThisFrame)
                    shiftPressedThisFrame = true;
            }
#endif

            // Fallback на случай Legacy Input
            try
            {
                if (h == 0f) h = Input.GetAxisRaw("Horizontal");
                if (!aPressedThisFrame && (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))) aPressedThisFrame = true;
                if (!dPressedThisFrame && (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))) dPressedThisFrame = true;
                if (!shiftPressedThisFrame && (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))) shiftPressedThisFrame = true;
                if (!shiftHold && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))) shiftHold = true;

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
            _sprintHeld = shiftHold;
            _isSprinting = _sprintHeld && IsGrounded && Mathf.Abs(_horizontalInput) > 0.05f;

            if (jumpDown)
            {
                _jumpBufferTimer = jumpBufferTime;
            }

            // Детекция двойного тапа A / D и Shift
            HandleDashInput(aPressedThisFrame, dPressedThisFrame, shiftPressedThisFrame);
        }

        private void HandleDashInput(bool aPressed, bool dPressed, bool shiftPressed)
        {
            if (!enableDash) return;

            // 1. Двойной тап клавиши A (влево)
            if (aPressed)
            {
                if (Time.time - _lastTapTimeA <= doubleTapWindow && CanDash())
                {
                    StartDash(-1f);
                    _lastTapTimeA = -10f; // сбрасываем, чтобы не сработало на третий клик
                    return;
                }
                _lastTapTimeA = Time.time;
            }

            // 2. Двойной тап клавиши D (вправо)
            if (dPressed)
            {
                if (Time.time - _lastTapTimeD <= doubleTapWindow && CanDash())
                {
                    StartDash(1f);
                    _lastTapTimeD = -10f;
                    return;
                }
                _lastTapTimeD = Time.time;
            }

            // 3. Быстрый рывок по Shift (в текущую сторону движения или взгляда)
            if (shiftPressed && CanDash())
            {
                float dir = _horizontalInput != 0f ? Mathf.Sign(_horizontalInput) : (_sr != null && _sr.flipX ? -1f : 1f);
                StartDash(dir);
            }
        }

        private bool CanDash()
        {
            if (!enableDash) return false;
            if (_isDashing) return false;
            if (_dashCooldownTimer > 0f) return false;
            if (!IsGrounded && _airDashesLeft <= 0) return false;
            if (_blockParry != null && (_blockParry.IsBlocking || _blockParry.IsParrying || _blockParry.IsParryStaggered)) return false;
            if (_stamina != null && (_stamina.IsExhausted || !_stamina.CanAfford(dashStaminaCost))) return false;
            return true;
        }

        private void StartDash(float direction)
        {
            if (_stamina != null)
            {
                _stamina.ConsumeForAction("Dash", dashStaminaCost);
            }

            _isDashing = true;
            _dashDirection = direction;
            float speedMult = _stamina != null ? _stamina.ActionSpeedMultiplier : 1.0f;
            _dashTimer = dashDuration / speedMult;
            _dashCooldownTimer = dashCooldown / speedMult;

            if (!IsGrounded)
            {
                _airDashesLeft--;
            }

            _rb.gravityScale = 0f;
            _rb.linearVelocity = new Vector2(_dashDirection * dashSpeed * speedMult, 0f);

            // Сочный горизонтальный stretch при рывке
            if (enableJuiceSquashStretch)
            {
                _targetScale = new Vector3(_baseScale.x * 1.35f, _baseScale.y * 0.72f, _baseScale.z);
            }
        }

        private void HandleDashMovement()
        {
            float speedMult = _stamina != null ? _stamina.ActionSpeedMultiplier : 1.0f;
            _dashTimer -= Time.fixedDeltaTime;
            _rb.linearVelocity = new Vector2(_dashDirection * dashSpeed * speedMult, 0f);

            if (_dashTimer <= 0f)
            {
                _isDashing = false;
                // Сохраняем приятную остаточную инерцию (carry-over)
                _rb.linearVelocity = new Vector2(_dashDirection * moveSpeed * 1.15f * speedMult, 0f);
                _rb.gravityScale = baseGravityScale;
            }
        }

        private void UpdateTimers()
        {
            // Hitstun Timer
            if (_hitstunTimer > 0f)
            {
                _hitstunTimer -= Time.deltaTime;
            }

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

            // Dash Cooldown Timer
            if (_dashCooldownTimer > 0f)
            {
                _dashCooldownTimer -= Time.deltaTime;
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

                        // Считываем физическое сцепление (friction) поверхности
                        float surfFriction = friction;
                        if (hit.collider.sharedMaterial != null && hit.collider.sharedMaterial.friction > 0.001f)
                        {
                            surfFriction *= hit.collider.sharedMaterial.friction;
                        }
                        _currentSurfaceFriction = surfFriction;
                        break;
                    }
                }
            }

            // Прилипание к спускам (Slope Down Snapping): исключает отрыв и подпрыгивания на уступах и лестницах
            if (!groundedNow && _wasGroundedLastFrame && !_isJumping && !_isDashing && _rb.linearVelocity.y <= 0.1f)
            {
                int snapCount = _col.Cast(Vector2.down, _groundFilter, _groundHits, slopeDownSnapDistance);
                for (int i = 0; i < snapCount; i++)
                {
                    var snapHit = _groundHits[i];
                    if (snapHit.collider != null && snapHit.collider != _col && !snapHit.collider.isTrigger && snapHit.normal.y > 0.45f)
                    {
                        groundedNow = true;
                        float snapY = snapHit.point.y + (_col.bounds.extents.y);
                        _rb.position = new Vector2(_rb.position.x, snapY);

                        Vector2 tangent = new Vector2(snapHit.normal.y, -snapHit.normal.x);
                        _rb.linearVelocity = tangent * Vector2.Dot(_rb.linearVelocity, tangent);

                        float surfFriction = friction;
                        if (snapHit.collider.sharedMaterial != null && snapHit.collider.sharedMaterial.friction > 0.001f)
                        {
                            surfFriction *= snapHit.collider.sharedMaterial.friction;
                        }
                        _currentSurfaceFriction = surfFriction;
                        break;
                    }
                }
            }

            // Детекция приземления (для эффекта squash и сброса воздушных ресурсов)
            if (!IsGrounded && groundedNow && _rb.linearVelocity.y <= 0.1f)
            {
                _airJumpsLeft = maxAirJumps;
                _airDashesLeft = maxAirDashes;

                if (enableJuiceSquashStretch)
                {
                    _targetScale = new Vector3(_baseScale.x * 1.25f, _baseScale.y * 0.75f, _baseScale.z);
                }
            }

            IsGrounded = groundedNow;

            if (!IsGrounded)
            {
                _currentSurfaceFriction = 1f;
            }

            if (IsGrounded && _rb.linearVelocity.y <= 0.05f)
            {
                _isJumping = false;
            }
        }

        private void HandleHorizontalMovement()
        {
            float speedMult = 1f;

            // Спринт при зажатии Shift на земле
            if (IsGrounded && _sprintHeld && Mathf.Abs(_horizontalInput) > 0.01f)
            {
                speedMult *= sprintMultiplier;
            }

            // Замедление при истощении (Exhaustion debuff)
            if (_stamina != null)
            {
                speedMult *= _stamina.ActionSpeedMultiplier;
            }

            // Ограничение скорости при блокировании, парировании или стаггере
            if (_blockParry != null)
            {
                if (_blockParry.IsParryStaggered || _blockParry.IsParrying)
                {
                    speedMult = 0f;
                }
                else if (_blockParry.IsBlocking)
                {
                    speedMult *= _blockParry.BlockMoveSpeedMultiplier;
                }
            }

            // Бонус к скорости и управляемости в пике прыжка (Apex bonus)
            if (enableApexHang && !_isDashing && Mathf.Abs(_rb.linearVelocity.y) < apexVelocityThreshold)
            {
                speedMult *= apexSpeedMultiplier;
            }

            float targetVelocityX = _horizontalInput * moveSpeed * speedMult;
            float currentVelocityX = _rb.linearVelocity.x;

            // Расчет сцепления с землей (Friction)
            float effectiveFriction = Mathf.Clamp(_currentSurfaceFriction, 0.15f, 2.5f);
            float effectiveDecel = groundDeceleration * effectiveFriction;
            float effectiveAccel = groundAcceleration * Mathf.Clamp(effectiveFriction, 0.4f, 1.8f);
            float effectiveTurnDecel = turnDeceleration * effectiveFriction;

            // Детекция микро-заноса (Skid) при резком развороте на 180°
            bool isTurning = Mathf.Abs(targetVelocityX) > 0.05f && Mathf.Abs(currentVelocityX) > 0.5f && Mathf.Sign(targetVelocityX) != Mathf.Sign(currentVelocityX);
            _isSkidding = isTurning && IsGrounded;

            float accelRate;
            if (IsGrounded)
            {
                if (_isSkidding)
                {
                    // Эффект заноса (масса тела не позволяет развернуться мгновенно)
                    accelRate = effectiveTurnDecel;
                    if (enableJuiceSquashStretch)
                    {
                        _targetScale = new Vector3(_baseScale.x * 0.92f, _baseScale.y * 1.08f, _baseScale.z);
                    }
                }
                else if (Mathf.Abs(targetVelocityX) > 0.01f)
                {
                    accelRate = _isSprinting ? effectiveAccel * 1.15f : effectiveAccel;
                }
                else
                {
                    accelRate = effectiveDecel;
                }
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
            if (_blockParry != null && (_blockParry.IsBlocking || _blockParry.IsParrying || _blockParry.IsParryStaggered))
            {
                _jumpBufferTimer = 0f;
            }

            // 1. Обработка прыжка
            if (_jumpBufferTimer > 0f)
            {
                if (_coyoteTimer > 0f)
                {
                    // Обычный прыжок с земли или с края
                    ExecuteJump();
                }
                else if (_airJumpsLeft > 0)
                {
                    // Двойной прыжок в воздухе
                    ExecuteAirJump();
                }
            }

            // 2. Управление гравитацией (Apex Floatiness + Snappy Fall)
            bool isAtApex = enableApexHang && Mathf.Abs(_rb.linearVelocity.y) < apexVelocityThreshold && !_isDashing;

            if (isAtApex)
            {
                // Зависание в пике (как в Celeste): сниженная гравитация на мгновение в наивысшей точке
                _rb.gravityScale = baseGravityScale * apexGravityMultiplier;
            }
            else if (_rb.linearVelocity.y < -0.01f)
            {
                // Падение вниз: гравитация выше для четкого приземления
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

            // 3. Ограничение предельной скорости падения (Terminal Velocity)
            if (_rb.linearVelocity.y < -maxFallSpeed)
            {
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, -maxFallSpeed);
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

        private void ExecuteAirJump()
        {
            _airJumpsLeft--;
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce * airJumpForceMultiplier);
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
            _isJumping = true;

            // Эффект stretch при двойном прыжке
            if (enableJuiceSquashStretch)
            {
                _targetScale = new Vector3(_baseScale.x * 0.78f, _baseScale.y * 1.3f, _baseScale.z);
            }
        }

        private void UpdateVisuals()
        {
            if (_combatController == null) _combatController = GetComponent<Combat.PlayerCombatController2D>();

            // Если персонаж атакует — лицо и поворот удерживаются в сторону удара
            bool isAttacking = _combatController != null && _combatController.CurrentState != Combat.CombatState.Idle;

            // 1. Поворот персонажа и лица по направлению движения (если сейчас не в атаке)
            if (!isAttacking)
            {
                if (_isDashing)
                {
                    SetFacing(_dashDirection);
                }
                else if (Mathf.Abs(_horizontalInput) > 0.05f)
                {
                    SetFacing(_horizontalInput);
                }
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

