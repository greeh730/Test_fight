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

        [Header("--- Jump Stamina Cost (Расход выносливости) ---")]
        [Tooltip("Процент от максимальной выносливости, отнимаемый за прыжок с земли (0..100%). Например: 25 = 25%")]
        [Range(0f, 100f)]
        [SerializeField] private float jumpStaminaPercent = 25f;

        [Tooltip("Разделять расход выносливости для прыжка в воздухе (двойного прыжка)")]
        [SerializeField] private bool separateAirJumpStamina = false;

        [Tooltip("Процент от максимальной выносливости, отнимаемый за прыжок в воздухе (0..100%). Например: 25 = 25%")]
        [Range(0f, 100f)]
        [SerializeField] private float airJumpStaminaPercent = 25f;

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

        [Header("--- Ledge Slip & Corner Nudge (Соскальзывание с краев) ---")]
        [Tooltip("Включить автоматическое соскальзывание с опасных углов и краев платформ")]
        [SerializeField] private bool enableEdgeSlip = true;

        [Tooltip("Горизонтальная скорость соскальзывания с края платформы")]
        [SerializeField] private float edgeSlipSpeed = 3.5f;

        [Tooltip("Включить сглаживание углов при прыжке снизу (Corner Rounding)")]
        [SerializeField] private bool enableCornerRounding = true;

        [Tooltip("Дистанция проверки выравнивания угла при прыжке")]
        [SerializeField] private float cornerNudgeDistance = 0.28f;

        // Внутреннее состояние соскальзывания
        private bool _isEdgeSlipping = false;
        private float _edgeSlipDirection = 0f;

        [Header("--- Visual & Game Feel ---")]
        [Tooltip("Эффект сжатия и растяжения при прыжке, приземлении и дэше")]
        [SerializeField] private bool enableJuiceSquashStretch = true;
        [SerializeField] private float squashStretchSpeed = 12f;

        // Components
        private Rigidbody2D _rb;
        private Collider2D _col;
        private SpriteRenderer _sr;
        private ContactFilter2D _groundFilter;
        private readonly RaycastHit2D[] _groundHits = new RaycastHit2D[8];
        private readonly RaycastHit2D[] _footRayHits = new RaycastHit2D[8];

        // State
        public bool IsGrounded { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsJumping => _isJumping;
        public bool IsDashing => _isDashing;
        public bool IsSprinting => _isSprinting;
        public bool IsSkidding => _isSkidding;
        public bool IsEdgeSlipping => _isEdgeSlipping;
        public float EdgeSlipDirection => _edgeSlipDirection;
        public float CurrentFriction => _currentSurfaceFriction;
        public int AirJumpsRemaining => _airJumpsLeft;
        public Vector2 Velocity => _rb != null ? _rb.linearVelocity : Vector2.zero;
        public float HorizontalInput => _horizontalInput;
        public bool HasMoveInput => Mathf.Abs(_horizontalInput) > 0.01f;

        // Events for tutorial and external systems
        public event System.Action OnDashStarted;
        public event System.Action OnJumpStarted;

        // Stamina Properties
        public float JumpStaminaPercent
        {
            get => jumpStaminaPercent;
            set => jumpStaminaPercent = Mathf.Clamp(value, 0f, 100f);
        }

        public bool SeparateAirJumpStamina
        {
            get => separateAirJumpStamina;
            set => separateAirJumpStamina = value;
        }

        public float AirJumpStaminaPercent
        {
            get => separateAirJumpStamina ? airJumpStaminaPercent : jumpStaminaPercent;
            set => airJumpStaminaPercent = Mathf.Clamp(value, 0f, 100f);
        }

        public float GetNormalizedJumpStamina(bool isAirJump)
        {
            float raw = (isAirJump && separateAirJumpStamina) ? airJumpStaminaPercent : jumpStaminaPercent;
            float normalized = raw > 1f ? (raw / 100f) : raw;
            return Mathf.Clamp01(normalized);
        }

        private void OnValidate()
        {
            // Auto-migrate legacy 0..1 serialized values (e.g. 0.25 -> 25)
            if (jumpStaminaPercent > 0f && jumpStaminaPercent <= 1f)
            {
                jumpStaminaPercent *= 100f;
            }
            if (airJumpStaminaPercent > 0f && airJumpStaminaPercent <= 1f)
            {
                airJumpStaminaPercent *= 100f;
            }
        }

        private float _horizontalInput;
        private bool _jumpPressed;
        private bool _jumpHeld;
        private bool _downHeld;
        private bool _sprintHeld;
        private bool _isSprinting;
        private bool _wasSprintingWhenJumped;
        private bool _isSkidding;
        private float _currentSurfaceFriction = 1.0f;

        // Timers & Stun
        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private float _hitstunTimer;
        private float _timeSinceJump = 10f;
        private bool _isJumping;
        private int _airJumpsLeft;

        public float MoveSpeedMultiplier { get; set; } = 1.0f;
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
            LayerMask effectiveGroundMask = groundLayer & ~(1 << 2);
            _groundFilter.SetLayerMask(effectiveGroundMask);

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

        private void OnDisable()
        {
            if (Combat.Audio.SoundManager.Instance != null)
            {
                Combat.Audio.SoundManager.Instance.UpdateMovementAudio(false, false, false, 0f);
            }
        }

        private void Update()
        {
            GatherInput();
            UpdateTimers();
            UpdateVisuals();
            UpdateMovementAudio();
        }

        private void UpdateMovementAudio()
        {
            bool isMoving = (HasMoveInput || Mathf.Abs(Velocity.x) > 0.2f) && !IsHitstunned;
            if (Combat.Audio.SoundManager.Instance != null)
            {
                Combat.Audio.SoundManager.Instance.UpdateMovementAudio(IsGrounded, isMoving, _isSprinting, Velocity.x);
            }
        }

        private void FixedUpdate()
        {
            CheckGrounded();
            CheckCornerRounding();

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
                _wasSprintingWhenJumped = false;
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
            bool sprintInput = _sprintHeld && Mathf.Abs(_horizontalInput) > 0.05f;
            if (IsGrounded)
            {
                _isSprinting = sprintInput;
                _wasSprintingWhenJumped = false;
            }
            else
            {
                // В воздухе спринт сохраняется, если игрок выпрыгнул из спринта или держит Shift
                _isSprinting = (_wasSprintingWhenJumped || sprintInput) && Mathf.Abs(_horizontalInput) > 0.05f;
            }

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
            OnDashStarted?.Invoke();

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
                _rb.linearVelocity = new Vector2(_dashDirection * moveSpeed * MoveSpeedMultiplier * 1.15f * speedMult, 0f);
                _rb.gravityScale = baseGravityScale;
            }
        }

        private void UpdateTimers()
        {
            _timeSinceJump += Time.deltaTime;

            // Hitstun Timer
            if (_hitstunTimer > 0f)
            {
                _hitstunTimer -= Time.deltaTime;
            }

            // Coyote Timer: доступен только когда игрок твердо стоит на земле и не совершает активный прыжок
            if (IsGrounded && !_isJumping)
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

        private bool IsValidGroundHit(RaycastHit2D hit, Bounds bounds)
        {
            if (hit.collider == null || hit.collider == _col || hit.collider.isTrigger)
                return false;

            // Коллайдеры, с которыми у игрока отключена коллизия (барьеры EnemyBlockerBarrier2D и др.)
            if (Physics2D.GetIgnoreCollision(_col, hit.collider))
                return false;

            if (hit.collider.GetComponent<LevelGeneration.EnemyBlockerBarrier2D>() != null)
                return false;

            // Направление нормали: угол наклона опоры (нормаль направлена вверх, уклон до ~53°)
            if (hit.normal.y < 0.60f)
                return false;

            // Точка контакта должна быть строго под ногами игрока (не на уровне пояса или головы внутри стены)
            if (hit.point.y > bounds.min.y + 0.08f)
                return false;

            return true;
        }

        private void CheckGrounded()
        {
            if (_col == null) _col = GetComponent<Collider2D>();
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            if (_col == null || _rb == null) return;

            Bounds bounds = _col.bounds;

            // Если игрок активно летит вверх в прыжке или только что оттолкнулся — он не может считаться приземленным
            if (_timeSinceJump < 0.08f || (_isJumping && _rb.linearVelocity.y > 0.15f))
            {
                IsGrounded = false;
                _isEdgeSlipping = false;
                _edgeSlipDirection = 0f;
                _currentSurfaceFriction = 1f;
                return;
            }

            float footInset = bounds.extents.x * 0.60f;
            float footOriginY = bounds.min.y + 0.14f;
            float footCheckDist = 0.14f + groundCheckDistance + 0.04f;

            // 3 вертикальных луча от нижней части тела вниз через ступни: Центр, Лево, Право
            Vector2 centerOrigin = new Vector2(bounds.center.x, footOriginY);
            Vector2 leftOrigin = new Vector2(bounds.center.x - footInset, footOriginY);
            Vector2 rightOrigin = new Vector2(bounds.center.x + footInset, footOriginY);

            RaycastHit2D hitCenter = RaycastFootGround(centerOrigin, footCheckDist, bounds);
            RaycastHit2D hitLeft = RaycastFootGround(leftOrigin, footCheckDist, bounds);
            RaycastHit2D hitRight = RaycastFootGround(rightOrigin, footCheckDist, bounds);

            bool validCenter = hitCenter.collider != null;
            bool validLeft = hitLeft.collider != null;
            bool validRight = hitRight.collider != null;

            // Shape cast для общей детекции с учетом формы коллайдера
            int castCount = _col.Cast(Vector2.down, _groundFilter, _groundHits, groundCheckDistance);
            bool castHitSurface = false;
            RaycastHit2D bestCastHit = default(RaycastHit2D);
            for (int i = 0; i < castCount; i++)
            {
                var hit = _groundHits[i];
                if (IsValidGroundHit(hit, bounds))
                {
                    castHitSurface = true;
                    bestCastHit = hit;
                    break;
                }
            }

            _isEdgeSlipping = false;
            _edgeSlipDirection = 0f;
            bool groundedNow = false;

            // Сценарий 1: Уверенная опора (центр тела на платформе либо обе стороны поддержаны)
            if (validCenter || (validLeft && validRight))
            {
                groundedNow = true;
                RaycastHit2D mainHit = validCenter ? hitCenter : (validLeft ? hitLeft : hitRight);
                UpdateSurfaceFriction(mainHit);
            }
            // Сценарий 2: Опора через форму капсулы (стык между коллайдерами, где лучи попали в микро-шов)
            else if (castHitSurface && Mathf.Abs(bestCastHit.point.x - bounds.center.x) < bounds.extents.x * 0.80f)
            {
                groundedNow = true;
                UpdateSurfaceFriction(bestCastHit);
            }
            // Сценарий 3: Опасный край/угол платформы (центр висит в воздухе над обрывом, опора только на один край)
            else if (enableEdgeSlip && (validLeft ^ validRight))
            {
                // Проверяем, есть ли земля чуть глубже под центром (стык или микро-ступенька)
                RaycastHit2D deeperCheck = RaycastFootGround(centerOrigin, footCheckDist + 0.15f, bounds);
                if (deeperCheck.collider == null)
                {
                    // Реальный обрыв: соскальзываем
                    _isEdgeSlipping = true;
                    _edgeSlipDirection = validLeft ? 1f : -1f;
                }
                else
                {
                    // Под центром есть продолжение пола (стык) -> надежная опора
                    groundedNow = true;
                    UpdateSurfaceFriction(deeperCheck);
                }
            }
            else if (castHitSurface)
            {
                groundedNow = true;
                UpdateSurfaceFriction(bestCastHit);
            }

            // Прилипание к спускам (Slope Down Snapping): работает ТОЛЬКО если игрок на пологом склоне и НЕ прыгает
            if (!groundedNow && !_isEdgeSlipping && _wasGroundedLastFrame && !_isJumping && !_isDashing && _rb.linearVelocity.y <= 0.1f && _timeSinceJump > 0.1f)
            {
                int snapCount = _col.Cast(Vector2.down, _groundFilter, _groundHits, slopeDownSnapDistance);
                for (int i = 0; i < snapCount; i++)
                {
                    var snapHit = _groundHits[i];
                    if (IsValidGroundHit(snapHit, bounds))
                    {
                        float currentBottomY = bounds.min.y;
                        float deltaY = snapHit.point.y - currentBottomY;

                        // Смещаем строго вниз и не глубже slopeDownSnapDistance
                        if (deltaY <= 0.005f && deltaY >= -slopeDownSnapDistance)
                        {
                            groundedNow = true;
                            _rb.position = new Vector2(_rb.position.x, _rb.position.y + deltaY);

                            Vector2 tangent = new Vector2(snapHit.normal.y, -snapHit.normal.x);
                            _rb.linearVelocity = tangent * Vector2.Dot(_rb.linearVelocity, tangent);
                            UpdateSurfaceFriction(snapHit);
                            break;
                        }
                    }
                }
            }

            // Детекция приземления (сброс воздушных ресурсов)
            if (!IsGrounded && groundedNow && _rb.linearVelocity.y <= 0.15f)
            {
                _airJumpsLeft = maxAirJumps;
                _airDashesLeft = maxAirDashes;
                _isJumping = false;

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

        private void UpdateSurfaceFriction(RaycastHit2D hit)
        {
            float surfFriction = friction;
            if (hit.collider != null && hit.collider.sharedMaterial != null && hit.collider.sharedMaterial.friction > 0.001f)
            {
                surfFriction *= hit.collider.sharedMaterial.friction;
            }
            _currentSurfaceFriction = surfFriction;
        }

        private RaycastHit2D RaycastDirection(Vector2 origin, Vector2 direction, float distance)
        {
            int hitCount = Physics2D.Raycast(origin, direction, _groundFilter, _footRayHits, distance);
            for (int i = 0; i < hitCount; i++)
            {
                var hit = _footRayHits[i];
                if (hit.collider != null && hit.collider != _col && !hit.collider.isTrigger)
                {
                    return hit;
                }
            }
            return default(RaycastHit2D);
        }

        private RaycastHit2D RaycastFootGround(Vector2 origin, float distance, Bounds bounds)
        {
            int hitCount = Physics2D.Raycast(origin, Vector2.down, _groundFilter, _footRayHits, distance);
            for (int i = 0; i < hitCount; i++)
            {
                var hit = _footRayHits[i];
                if (IsValidGroundHit(hit, bounds))
                {
                    return hit;
                }
            }
            return default(RaycastHit2D);
        }

        private void CheckCornerRounding()
        {
            if (!enableCornerRounding || _rb == null || _col == null || _rb.linearVelocity.y <= 0.2f) return;

            Bounds bounds = _col.bounds;
            float topY = bounds.max.y - 0.04f;
            float checkDist = cornerNudgeDistance;

            // Два вертикальных луча от левого и правого края макушки капсулы
            float inset = bounds.extents.x * 0.65f;
            Vector2 leftOrigin = new Vector2(bounds.center.x - inset, topY);
            Vector2 rightOrigin = new Vector2(bounds.center.x + inset, topY);

            RaycastHit2D hitLeft = RaycastDirection(leftOrigin, Vector2.up, checkDist);
            RaycastHit2D hitRight = RaycastDirection(rightOrigin, Vector2.up, checkDist);

            bool leftBlocked = hitLeft.collider != null;
            bool rightBlocked = hitRight.collider != null;

            // Если левый край зацепил угол платформы снизу, а правый свободен -> мягко сдвигаем персонажа вправо
            if (leftBlocked && !rightBlocked)
            {
                _rb.position = new Vector2(_rb.position.x + 0.04f, _rb.position.y);
            }
            // Если правый край зацепил угол платформы снизу, а левый свободен -> мягко сдвигаем персонажа влево
            else if (rightBlocked && !leftBlocked)
            {
                _rb.position = new Vector2(_rb.position.x - 0.04f, _rb.position.y);
            }
        }

        private void HandleHorizontalMovement()
        {
            float speedMult = MoveSpeedMultiplier;

            // Спринт при зажатии Shift на земле и сохранение спринта в воздухе при прыжке
            bool isSprintActive = _isSprinting || (_sprintHeld && Mathf.Abs(_horizontalInput) > 0.01f) || (!IsGrounded && _wasSprintingWhenJumped && Mathf.Abs(_horizontalInput) > 0.01f);
            if (isSprintActive)
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

            // Обработка соскальзывания с края платформы (Ledge Slip)
            if (_isEdgeSlipping && enableEdgeSlip)
            {
                // Если игрок намеренно жмет джойстик/клавишу в сторону платформы (пытается забраться обратно)
                bool pressingIntoPlatform = (_horizontalInput * _edgeSlipDirection) < -0.2f;
                if (!pressingIntoPlatform)
                {
                    // Соскальзываем наружу с платформы в сторону обрыва
                    float slipVelocityX = _edgeSlipDirection * edgeSlipSpeed;
                    newVelocityX = Mathf.MoveTowards(newVelocityX, slipVelocityX, groundAcceleration * 2f * Time.fixedDeltaTime);

                    // Если Y скорость нулевая или положительная (зависание на углу), придаем импульс соскальзывания вниз
                    if (_rb.linearVelocity.y > -0.5f)
                    {
                        _rb.linearVelocity = new Vector2(newVelocityX, -0.8f);
                        return;
                    }
                }
            }

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
            _wasSprintingWhenJumped = _isSprinting || (_sprintHeld && Mathf.Abs(_horizontalInput) > 0.05f);
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
            _timeSinceJump = 0f;
            _isJumping = true;
            IsGrounded = false;
            OnJumpStarted?.Invoke();
            if (Combat.Audio.SoundManager.Instance != null)
            {
                Combat.Audio.SoundManager.Instance.PlayJump();
            }

            if (_stamina != null)
            {
                _stamina.ConsumePercent(GetNormalizedJumpStamina(false));
            }

            // Эффект stretch при прыжке
            if (enableJuiceSquashStretch)
            {
                _targetScale = new Vector3(_baseScale.x * 0.8f, _baseScale.y * 1.25f, _baseScale.z);
            }
        }

        private void ExecuteAirJump()
        {
            _airJumpsLeft--;
            _wasSprintingWhenJumped = _wasSprintingWhenJumped || _sprintHeld;
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce * airJumpForceMultiplier);
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
            _timeSinceJump = 0f;
            _isJumping = true;
            IsGrounded = false;
            OnJumpStarted?.Invoke();
            if (Combat.Audio.SoundManager.Instance != null)
            {
                Combat.Audio.SoundManager.Instance.PlayJump();
            }

            if (_stamina != null)
            {
                _stamina.ConsumePercent(GetNormalizedJumpStamina(true));
            }

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
                float checkDist = groundCheckDistance + 0.05f;
                float rayStartY = bounds.min.y + 0.06f;

                float footInset = bounds.extents.x * 0.72f;
                Vector2 centerOrigin = new Vector2(bounds.center.x, rayStartY);
                Vector2 leftOrigin = new Vector2(bounds.center.x - footInset, rayStartY);
                Vector2 rightOrigin = new Vector2(bounds.center.x + footInset, rayStartY);

                Gizmos.color = IsGrounded ? Color.green : (_isEdgeSlipping ? Color.yellow : Color.red);
                Gizmos.DrawLine(centerOrigin, centerOrigin + Vector2.down * checkDist);
                Gizmos.DrawLine(leftOrigin, leftOrigin + Vector2.down * checkDist);
                Gizmos.DrawLine(rightOrigin, rightOrigin + Vector2.down * checkDist);

                // Верхние лучи Corner Rounding
                if (enableCornerRounding)
                {
                    float topY = bounds.max.y - 0.04f;
                    float inset = bounds.extents.x * 0.65f;
                    Vector2 topL = new Vector2(bounds.center.x - inset, topY);
                    Vector2 topR = new Vector2(bounds.center.x + inset, topY);

                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(topL, topL + Vector2.up * cornerNudgeDistance);
                    Gizmos.DrawLine(topR, topR + Vector2.up * cornerNudgeDistance);
                }

                // Вектор соскальзывания при клиффе
                if (_isEdgeSlipping)
                {
                    Gizmos.color = Color.magenta;
                    Vector2 slipOrigin = new Vector2(bounds.center.x, bounds.min.y);
                    Gizmos.DrawRay(slipOrigin, new Vector2(_edgeSlipDirection, -0.5f));
                }
            }
        }
    }
}

