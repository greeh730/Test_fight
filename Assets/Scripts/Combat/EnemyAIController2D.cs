using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Combat.Player;
using Combat.Common;
using Combat.Navigation;

namespace Combat
{
    public enum EnemyState
    {
        Idle,
        Chasing,
        TelegraphWindup, // Окно для контратаки!
        ActiveStrike,
        Recovery,
        Stunned,         // Оглушен после успешной контратаки игрока
        Dead             // Повержен
    }

    public enum EnemyTacticalRole
    {
        Solo,
        Front,
        Flanker,
        Support
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class EnemyAIController2D : MonoBehaviour, ICombatEntity2D
    {
        [Header("--- Health & Defense ---")]
        [SerializeField] private float maxHealth = 250f;
        [SerializeField] private float currentHealth = 250f;
        [SerializeField] private float resetHealthDelay = 3.5f;

        [Header("--- Stamina & Poise System ---")]
        [SerializeField] private float maxStamina = 135f;
        [SerializeField] private float currentStamina = 135f;
        [Tooltip("Множитель урона по стамине при обычных ударах")]
        [SerializeField] private float staminaDrainMultiplier = 1.0f;
        [Tooltip("Длительность оглушения при полном истощении стамины (сек)")]
        [SerializeField] private float staminaBreakStunDuration = 3.0f;
        [Tooltip("Задержка без получения урона до начала регенерации стамины (> 10 сек для джаггл-комбо)")]
        [SerializeField] private float staminaRegenIdleDelay = 11.0f;
        [Tooltip("Скорость восстановления стамины в секунду")]
        [SerializeField] private float staminaRegenRate = 30f;

        [Header("--- Poise / Knockback Curve Settings ---")]
        [Tooltip("Множитель отталкивания при полной (100%) стамине (например 0.2 = высокая устойчивость)")]
        [SerializeField] private float knockbackMultAtFullStamina = 0.2f;

        [Tooltip("Множитель отталкивания при пустой (0%) стамине (например 1.0 = нормальное, 1.5+ = усиленное)")]
        [SerializeField] private float knockbackMultAtZeroStamina = 1.0f;

        [Tooltip("Множитель длительности стана при пустой стамине (1.0 = базовый, 1.8 = увеличенный)")]
        [SerializeField] private float stunDurationMultAtZeroStamina = 1.5f;

        [Header("--- UI & Visuals ---")]
        [SerializeField] private EnemyStaminaBar2D staminaBar;

        [Header("--- Death & Respawn Settings ---")]
        [Tooltip("Если включено (галочка), враг погибает при HP <= 0. Если выключено — враг бессмертен.")]
        [SerializeField] private bool canDie = true;

        [Tooltip("Возрождать врага после гибели?")]
        [SerializeField] private bool respawnOnDeath = true;

        [Tooltip("Задержка перед возрождением врага (сек)")]
        [SerializeField] private float respawnDelay = 4.0f;

        [Tooltip("Цвет поверженного врага")]
        [SerializeField] private Color defeatColor = new Color(0.28f, 0.28f, 0.28f, 0.75f);

        [Header("--- Vision & Detection (FOV) ---")]
        [Tooltip("Дистанция обнаружения игрока (радиус обзора)")]
        [SerializeField] private float detectionRange = 9.0f;
        [Tooltip("Дистанция потери цели")]
        [SerializeField] private float loseTargetRange = 13.0f;
        [Tooltip("Слои препятствий для проверки прямой видимости")]
        [SerializeField] private LayerMask visionObstacleLayers = 0;

        [Header("--- Movement & Jumping ---")]
        [SerializeField] private float moveSpeed = 3.6f;
        [Tooltip("Сила прыжка врага на платформы и к игроку")]
        [SerializeField] private float jumpForce = 14.8f;
        [Tooltip("Кулдаун между прыжками врага (сек)")]
        [SerializeField] private float jumpCooldown = 0.85f;
        [Tooltip("Слои земли и платформ для прыжков")]
        [SerializeField] private LayerMask groundLayer = ~0;

        [Header("--- Navigation & Pathfinding ---")]
        [Tooltip("Использовать умную навигацию по графу платформ (A*)")]
        [SerializeField] private bool usePlatformPathfinding = true;

        [Tooltip("Отрисовывать проложенный путь прямо в Scene View")]
        [SerializeField] private bool showPathGizmos = true;

        [Tooltip("Цвет линии пути в Scene View")]
        [SerializeField] private Color pathGizmoColor = new Color(0f, 1f, 0.85f, 0.95f);

        [Header("--- Tactical & Flanking Coordination ---")]
        [Tooltip("Включить тактическое окружение игрока (один спереди, другой заходит сзади/с фланга)")]
        [SerializeField] private bool enableFlanking = true;

        [Tooltip("Множитель скорости при обходе с фланга")]
        [SerializeField] private float flankSpeedMultiplier = 1.18f;

        [Tooltip("Минимальный интервал между боевыми прыжками сближения (сек)")]
        [SerializeField] private float combatLeapIntervalMin = 2.4f;

        [Tooltip("Максимальный интервал между боевыми прыжками сближения (сек)")]
        [SerializeField] private float combatLeapIntervalMax = 4.2f;

        [Tooltip("Дистанция разделения между врагами для предотвращения наложения спрайтов")]
        [SerializeField] private float separationDistance = 1.15f;

        [Header("--- Attack Parameters (Straight Mid Strike) ---")]
        [Tooltip("Дистанция до игрока для начала замаха")]
        [SerializeField] private float attackRange = 2.05f;

        [Tooltip("Длительность фазы замаха (телеграфа)")]
        [SerializeField] private float telegraphDuration = 0.70f;

        [Tooltip("Длительность активной фазы удара")]
        [SerializeField] private float activeStrikeDuration = 0.15f;

        [Tooltip("Длительность задержки восстановления после удара")]
        [SerializeField] private float recoveryDuration = 0.55f;

        [Tooltip("Урон прямого удара врага")]
        [SerializeField] private float attackDamage = 28f;

        [Tooltip("Импульс отталкивания игрока при попадании врага")]
        [SerializeField] private Vector2 knockbackToPlayer = new Vector2(7f, 3.5f);

        [Tooltip("Импульс выпада врага вперед при ударе")]
        [SerializeField] private float strikeLungeForce = 2.8f;

        [Tooltip("Смещение хитбокса прямого среднего удара")]
        [SerializeField] private Vector2 hitboxOffset = new Vector2(1.15f, 0.0f);

        [Tooltip("Размер хитбокса прямого среднего удара")]
        [SerializeField] private Vector2 hitboxSize = new Vector2(1.5f, 0.85f);

        [Header("--- Attack Variants (Anti-Air & Mix-Up) ---")]
        [Tooltip("Вероятность провести подсечку по ногам (Low Sweep) вместо прямого среднего удара")]
        [SerializeField] [Range(0f, 1f)] private float lowSweepChance = 0.35f;

        [Tooltip("Смещение хитбокса удара вверх над собой (Anti-Air)")]
        [SerializeField] private Vector2 upHitboxOffset = new Vector2(0.0f, 1.35f);

        [Tooltip("Размер хитбокса удара вверх над собой (Anti-Air)")]
        [SerializeField] private Vector2 upHitboxSize = new Vector2(1.6f, 1.35f);

        [Tooltip("Смещение хитбокса нижней подсечки по ногам (Low Sweep)")]
        [SerializeField] private Vector2 lowHitboxOffset = new Vector2(1.15f, -0.45f);

        [Tooltip("Размер хитбокса нижней подсечки по ногам (Low Sweep)")]
        [SerializeField] private Vector2 lowHitboxSize = new Vector2(1.5f, 0.6f);

        [Header("--- Visuals & Face ---")]
        [SerializeField] private Color normalColor = new Color(0.72f, 0.15f, 0.15f, 1f); // Темно-красный кубик
        [SerializeField] private Color stunColor = new Color(1f, 0.88f, 0.25f, 1f);     // Желто-золотой при стане
        [SerializeField] private Color flashColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Transform faceTransform;

        [Header("--- Components & Visualizer ---")]
        [SerializeField] private EnemyTelegraphVisualizer2D telegraphVisualizer;

        // State
        public EnemyState CurrentState { get; private set; } = EnemyState.Idle;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public float FacingDirection { get; private set; } = -1f;
        public bool CanDie { get => canDie; set => canDie = value; }
        public bool IsDead => CurrentState == EnemyState.Dead;
        public float MaxStamina => maxStamina;
        public float CurrentStamina => currentStamina;
        public Rigidbody2D Rigidbody => _rb;
        public EnemyTacticalRole TacticalRole { get; private set; } = EnemyTacticalRole.Solo;

        private static readonly List<EnemyAIController2D> _activeEnemies = new List<EnemyAIController2D>();
        public static IReadOnlyList<EnemyAIController2D> ActiveEnemies => _activeEnemies;

        private void OnEnable()
        {
            CombatTargetResolver.Register(this);
            if (!_activeEnemies.Contains(this)) _activeEnemies.Add(this);
        }

        private void OnDisable()
        {
            Time.timeScale = 1.0f;
            CombatTargetResolver.Unregister(this);
            _activeEnemies.Remove(this);
        }

        private Transform _playerTransform;
        private PlayerHealth2D _playerHealth;
        private Rigidbody2D _rb;
        private Collider2D _col;
        private SpriteRenderer _sr;
        private Coroutine _stateRoutine;
        private Coroutine _flashRoutine;
        private Coroutine _hitstopRoutine;
        private Coroutine _respawnRoutine;

        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private Vector3 _faceBaseLocalPos = new Vector3(0.16f, 0.14f, 0f);
        private Vector3 _faceBaseLocalScale = new Vector3(0.45f, 0.26f, 1f);
        private float _lastHitTime;
        private float _hitstunTimer;
        private float _jumpCooldownTimer;
        private float _combatLeapTimer;
        public bool IsGrounded { get; private set; }
        private float _stunRemaining;
        private readonly List<Collider2D> _overlapResults = new List<Collider2D>(8);
        private ContactFilter2D _playerFilter;
        private ContactFilter2D _groundFilter;
        private readonly RaycastHit2D[] _groundCastHits = new RaycastHit2D[8];

        // Navigation & Pathfinding
        private readonly List<NavPathStep> _currentNavPath = new List<NavPathStep>();
        private int _currentPathIndex = 0;
        private float _pathRecalculateTimer = 0f;
        private Vector2 _lastPathGoal = Vector2.zero;
        private bool _isJumpingToNextPlatform = false;
        private bool _isDroppingToNextPlatform = false;
        private float _jumpAirborneGraceTimer = 0f;
        private float _jumpMaxDurationTimer = 0f;
        private float _activeJumpForwardSpeed = 0f;
        private float _activeJumpTargetY = 0f;
        public IReadOnlyList<NavPathStep> CurrentNavPath => _currentNavPath;
        public int CurrentPathIndex => _currentPathIndex;

        // Tactician Status Effects
        private float _vulnerabilityMultiplier = 1.0f;
        private float _vulnerabilityTimer = 0f;
        private bool _isRooted = false;
        private float _rootTimer = 0f;
        private bool _isGravitySuspended = false;
        private float _gravitySuspendTimer = 0f;
        private Vector2 _gravityAnchorPos;
        private float _originalGravityScale = 1.0f;
        private float _disorientTimer = 0f;
        private float _staminaBoostTimer = 0f;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<Collider2D>();
            _sr = GetComponent<SpriteRenderer>();

            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;

            // Назначаем физический материал без трения, чтобы бот не застревал и не лип к углам платформ
            if (_col != null && _col.sharedMaterial == null)
            {
                _col.sharedMaterial = new PhysicsMaterial2D("EnemyMaterial_ZeroFriction")
                {
                    friction = 0f,
                    bounciness = 0f
                };
            }

            if (_sr != null) _sr.color = normalColor;
            currentHealth = maxHealth;
            currentStamina = maxStamina;

            if (faceTransform == null) faceTransform = transform.Find("Face");
            if (faceTransform != null)
            {
                _faceBaseLocalPos = faceTransform.localPosition;
                _faceBaseLocalScale = faceTransform.localScale;
            }

            if (telegraphVisualizer == null)
            {
                telegraphVisualizer = GetComponent<EnemyTelegraphVisualizer2D>();
                if (telegraphVisualizer == null) telegraphVisualizer = gameObject.AddComponent<EnemyTelegraphVisualizer2D>();
            }

            if (staminaBar == null)
            {
                staminaBar = GetComponent<EnemyStaminaBar2D>();
                if (staminaBar == null) staminaBar = gameObject.AddComponent<EnemyStaminaBar2D>();
            }
            staminaBar.Initialize(maxStamina);

            _playerFilter = new ContactFilter2D();
            _playerFilter.useTriggers = true;
            _playerFilter.SetLayerMask(~0);

            _groundFilter = new ContactFilter2D();
            _groundFilter.useTriggers = false;
            _groundFilter.SetLayerMask(groundLayer != 0 ? groundLayer : ~0);

            _combatLeapTimer = UnityEngine.Random.Range(combatLeapIntervalMin, combatLeapIntervalMax);
        }

        private void Start()
        {
            FindPlayer();
            SetFacing(-1f); // По умолчанию смотрим влево (в сторону игрока)
        }

        private void FindPlayer()
        {
            if (_playerTransform != null) return;
            var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
            if (player != null)
            {
                _playerTransform = player.transform;
                _playerHealth = player.GetComponent<PlayerHealth2D>();
            }
        }

        private void Update()
        {
            if (CurrentState == EnemyState.Dead) return;

            if (_playerTransform == null)
            {
                FindPlayer();
                return;
            }

            // Если игрок погиб — прекращаем погоню и возвращаемся в Idle
            if (_playerHealth != null && _playerHealth.IsDead)
            {
                if (CurrentState == EnemyState.Chasing)
                {
                    CurrentState = EnemyState.Idle;
                    _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                }
                return;
            }

            // Авто-восстановление здоровья после долгого простоя вне боя
            if (currentHealth < maxHealth && CurrentState == EnemyState.Idle && Time.time - _lastHitTime > resetHealthDelay)
            {
                currentHealth = maxHealth;
                if (_sr != null) _sr.color = normalColor;
            }

            // Регенерация стамины (при Stale Move boost восстанавливается на +75% быстрее)
            if (_staminaBoostTimer > 0f)
            {
                _staminaBoostTimer -= Time.deltaTime;
            }

            float effectiveRegenRate = (_staminaBoostTimer > 0f) ? (staminaRegenRate * 1.75f) : staminaRegenRate;
            float effectiveDelay = (_staminaBoostTimer > 0f) ? 0.35f : staminaRegenIdleDelay;

            if (currentStamina < maxStamina && CurrentState != EnemyState.Stunned && Time.time - _lastHitTime >= effectiveDelay)
            {
                currentStamina = Mathf.MoveTowards(currentStamina, maxStamina, effectiveRegenRate * Time.deltaTime);
                if (staminaBar != null)
                {
                    staminaBar.SetStamina(currentStamina, maxStamina);
                }
            }

            // Обновление негативных эффектов Тактика (Root, Gravity, Smoke, Vulnerability)
            UpdateTacticianStatusEffects();

            // Проверка заземления каждый кадр
            CheckGrounded();

            // Основная машина состояний ИИ
            switch (CurrentState)
            {
                case EnemyState.Idle:
                    UpdateIdle();
                    break;

                case EnemyState.Chasing:
                    UpdateChasing();
                    break;
            }
        }

        private void UpdateTacticianStatusEffects()
        {
            // 1. Метка уязвимости
            if (_vulnerabilityTimer > 0f)
            {
                _vulnerabilityTimer -= Time.deltaTime;
                if (_vulnerabilityTimer <= 0f)
                {
                    _vulnerabilityMultiplier = 1.0f;
                    if (_sr != null && CurrentState != EnemyState.Stunned && CurrentState != EnemyState.Dead)
                    {
                        _sr.color = normalColor;
                    }
                }
            }

            // 2. Обездвиживание (Root)
            if (_isRooted)
            {
                _rootTimer -= Time.deltaTime;
                if (_rootTimer <= 0f)
                {
                    _isRooted = false;
                }
            }

            // 3. Гравитационный якорь (зависание в невесомости)
            if (_isGravitySuspended)
            {
                _gravitySuspendTimer -= Time.deltaTime;
                if (_rb != null)
                {
                    _rb.linearVelocity = Vector2.zero;
                    transform.position = Vector3.Lerp(transform.position, _gravityAnchorPos, Time.deltaTime * 6f);
                }

                if (_gravitySuspendTimer <= 0f)
                {
                    _isGravitySuspended = false;
                    if (_rb != null) _rb.gravityScale = _originalGravityScale;
                }
            }

            // 4. Ослепление / дезориентация от дыма
            if (_disorientTimer > 0f)
            {
                _disorientTimer -= Time.deltaTime;
            }
        }

        private void UpdateIdle()
        {
            if (_disorientTimer > 0f) return; // Ослеплен, не может обнаружить игрока

            float dist = Vector2.Distance(transform.position, _playerTransform.position);
            if (dist <= detectionRange)
            {
                // Игрок попал в поле зрения!
                CurrentState = EnemyState.Chasing;
                Debug.Log("[ENEMY AI] Игрок замечен! Враг начинает преследование.");
            }
        }

        private void UpdateChasing()
        {
            if (_hitstunTimer > 0f)
            {
                _hitstunTimer -= Time.deltaTime;
                return;
            }

            if (_isGravitySuspended)
            {
                return;
            }

            if (_isRooted)
            {
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                return;
            }

            if (_disorientTimer > 0f)
            {
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                return;
            }

            CheckGrounded();
            if (_jumpCooldownTimer > 0f) _jumpCooldownTimer -= Time.deltaTime;
            if (_combatLeapTimer > 0f) _combatLeapTimer -= Time.deltaTime;

            float distToPlayer = Vector2.Distance(transform.position, _playerTransform.position);
            float dy = _playerTransform.position.y - transform.position.y;
            float dx = _playerTransform.position.x - transform.position.x;
            float absDx = Mathf.Abs(dx);

            // Игрок убежал слишком далеко
            if (distToPlayer > loseTargetRange)
            {
                CurrentState = EnemyState.Idle;
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                return;
            }

            // -------------------------------------------------------------
            // 1. ТАКТИЧЕСКАЯ КООРДИНАЦИЯ И РАСПРЕДЕЛЕНИЕ РОЛЕЙ (FLANKING)
            // -------------------------------------------------------------
            EnemyTacticalRole role = EnemyTacticalRole.Solo;
            float targetX = _playerTransform.position.x;
            float currentMoveSpeed = moveSpeed;
            bool isFlankingThrough = false;

            if (enableFlanking)
            {
                // Находим всех живых преследователей в зоне видимости игрока
                List<EnemyAIController2D> chasers = new List<EnemyAIController2D>(4);
                for (int i = 0; i < _activeEnemies.Count; i++)
                {
                    var e = _activeEnemies[i];
                    if (e != null && !e.IsDead && e.gameObject.activeInHierarchy && _playerTransform != null)
                    {
                        float d = Vector2.Distance(e.transform.position, _playerTransform.position);
                        if (d <= e.loseTargetRange && (e.CurrentState == EnemyState.Chasing || e.CurrentState == EnemyState.TelegraphWindup || e.CurrentState == EnemyState.ActiveStrike || e.CurrentState == EnemyState.Recovery))
                        {
                            chasers.Add(e);
                        }
                    }
                }

                if (chasers.Count > 1)
                {
                    // Сортируем:
                    // 1. Активно атакующий держит Front
                    // 2. Ближайший к игроку — Front, второй — Flanker
                    chasers.Sort((a, b) =>
                    {
                        int aAttacking = (a.CurrentState == EnemyState.TelegraphWindup || a.CurrentState == EnemyState.ActiveStrike) ? 0 : 1;
                        int bAttacking = (b.CurrentState == EnemyState.TelegraphWindup || b.CurrentState == EnemyState.ActiveStrike) ? 0 : 1;
                        if (aAttacking != bAttacking) return aAttacking.CompareTo(bAttacking);

                        float distA = Vector2.Distance(a.transform.position, _playerTransform.position);
                        float distB = Vector2.Distance(b.transform.position, _playerTransform.position);
                        return distA.CompareTo(distB);
                    });

                    var frontBot = chasers[0];
                    float frontSide = Mathf.Sign(frontBot.transform.position.x - _playerTransform.position.x);
                    if (Mathf.Abs(frontBot.transform.position.x - _playerTransform.position.x) < 0.15f)
                    {
                        frontSide = frontBot.FacingDirection != 0f ? -frontBot.FacingDirection : 1f;
                    }

                    if (this == frontBot)
                    {
                        role = EnemyTacticalRole.Front;
                        targetX = _playerTransform.position.x + frontSide * (attackRange * 0.8f);
                    }
                    else if (chasers.Count >= 2 && this == chasers[1])
                    {
                        role = EnemyTacticalRole.Flanker;
                        // Фланкер заходит с противоположной стороны!
                        float desiredFlankSide = -frontSide;
                        targetX = _playerTransform.position.x + desiredFlankSide * (attackRange * 0.85f);
                        currentMoveSpeed = moveSpeed * flankSpeedMultiplier;

                        float mySide = Mathf.Sign(transform.position.x - _playerTransform.position.x);
                        if (Mathf.Sign(mySide) == Mathf.Sign(frontSide))
                        {
                            isFlankingThrough = true;
                        }
                    }
                    else
                    {
                        role = EnemyTacticalRole.Support;
                        targetX = _playerTransform.position.x + frontSide * (attackRange + 2.5f);
                    }
                }
            }
            TacticalRole = role;

            // -------------------------------------------------------------
            // 2. ДИСТАНЦИЯ АТАКИ И ПРОВЕРКА УДАРА
            // -------------------------------------------------------------
            // Если бот выполняет сквозной обход, он не начинает атаку спереди, а бежит за спину
            if (!isFlankingThrough)
            {
                if (distToPlayer <= attackRange || (dy > 0.8f && dy < 3.2f && absDx < 1.8f))
                {
                    StartTelegraphAttack();
                    return;
                }
            }

            // -------------------------------------------------------------
            // 3. НАВИГАЦИЯ ПО ПЛАТФОРМАМ (A* PLATFORM PATHFINDING)
            // -------------------------------------------------------------
            Vector2 goalDestination = new Vector2(targetX, _playerTransform.position.y);

            bool isMidJump = _isJumpingToNextPlatform || (!IsGrounded && _jumpAirborneGraceTimer > 0f);

            // Не сбрасываем и не пересчитываем путь во время полета/прыжка на платформу!
            if (usePlatformPathfinding && PlatformNavGraph2D.Instance != null && !isMidJump)
            {
                _pathRecalculateTimer -= Time.deltaTime;
                if (_pathRecalculateTimer <= 0f || Vector2.Distance(_lastPathGoal, goalDestination) > 1.35f || _currentNavPath.Count == 0)
                {
                    PlatformNavGraph2D.Instance.FindPath(transform.position, goalDestination, _currentNavPath);
                    _currentPathIndex = 0;
                    _lastPathGoal = goalDestination;
                    _pathRecalculateTimer = UnityEngine.Random.Range(0.35f, 0.55f);
                }
            }

            // Определение направления движения и прыжков по маршруту:
            float dirX = Mathf.Sign(targetX - transform.position.x);
            bool pathHandledMovement = false;

            if (usePlatformPathfinding && _currentNavPath != null && _currentNavPath.Count > 0 && _currentPathIndex < _currentNavPath.Count)
            {
                var step = _currentNavPath[_currentPathIndex];
                float stepDx = step.Position.x - transform.position.x;
                float absStepDx = Mathf.Abs(stepDx);

                if (step.Action == NavActionType.Walk)
                {
                    dirX = Mathf.Sign(stepDx);
                    if (absStepDx < 0.45f)
                    {
                        _currentPathIndex++;
                    }
                    pathHandledMovement = true;
                }
                else if (step.Action == NavActionType.Jump)
                {
                    pathHandledMovement = true;

                    if (_jumpAirborneGraceTimer > 0f) _jumpAirborneGraceTimer -= Time.deltaTime;
                    if (_jumpMaxDurationTimer > 0f) _jumpMaxDurationTimer -= Time.deltaTime;

                    if (!_isJumpingToNextPlatform)
                    {
                        // Если бот еще не дошел до точки отталкивания — бежим к ней
                        if (absStepDx > 0.45f && IsGrounded)
                        {
                            dirX = Mathf.Sign(stepDx);
                        }
                        else if (IsGrounded)
                        {
                            // Находимся у точки отрыва. При активном кд останавливаемся и ждем взлета
                            dirX = 0f;
                            if (_jumpCooldownTimer <= 0f)
                            {
                                _isJumpingToNextPlatform = true;
                                _jumpAirborneGraceTimer = 0.22f; // Первые 220мс игнорируем землю, чтобы дать боту взлететь
                                _jumpMaxDurationTimer = step.FlightDuration + 0.75f;
                                _activeJumpForwardSpeed = step.ForwardSpeed;
                                _activeJumpTargetY = step.LandingTarget.y;
                                _jumpCooldownTimer = jumpCooldown * 1.15f;
                                Jump(step.JumpForce, step.ForwardSpeed);
                            }
                        }
                    }
                    else
                    {
                        // Во время прыжка: строго удерживаем расчетную горизонтальную скорость полета
                        _rb.linearVelocity = new Vector2(_activeJumpForwardSpeed, _rb.linearVelocity.y);

                        // Проверка приземления (только после отрыва от стартовой платформы)
                        if (_jumpAirborneGraceTimer <= 0f && IsGrounded)
                        {
                            _isJumpingToNextPlatform = false;
                            _currentPathIndex++;
                        }
                        else if (_jumpMaxDurationTimer <= 0f)
                        {
                            // Таймаут прыжка (если застрял или сорвался) — перезапускаем поиск пути
                            _isJumpingToNextPlatform = false;
                            _pathRecalculateTimer = 0f;
                            _currentNavPath.Clear();
                        }
                    }
                }
                else if (step.Action == NavActionType.Drop)
                {
                    pathHandledMovement = true;
                    if (IsGrounded && !_isDroppingToNextPlatform)
                    {
                        if (absStepDx > 0.35f)
                        {
                            dirX = Mathf.Sign(stepDx);
                        }
                        else
                        {
                            _isDroppingToNextPlatform = true;
                            _rb.linearVelocity = new Vector2(step.ForwardSpeed, _rb.linearVelocity.y);
                        }
                    }
                    else if (!IsGrounded)
                    {
                        _rb.linearVelocity = new Vector2(step.ForwardSpeed, _rb.linearVelocity.y);
                    }
                    else if (_isDroppingToNextPlatform && IsGrounded)
                    {
                        _isDroppingToNextPlatform = false;
                        _currentPathIndex++;
                    }
                }
            }

            // -------------------------------------------------------------
            // 4. ДОПОЛНИТЕЛЬНЫЙ РЕАКТИВНЫЙ ПРЫЖКОВЫЙ ИНТЕЛЛЕКТ (FALLBACK & COMBAT)
            // -------------------------------------------------------------
            // Если активен путь платформенной навигации, реактивные прыжки в стену отключены!
            if (!pathHandledMovement && IsGrounded && _jumpCooldownTimer <= 0f)
            {
                // Проверка потолка: если прямо над головой сплошное перекрытие — категорически не прыгаем!
                bool hasCeilingAbove = false;
                var ceilingHits = Physics2D.RaycastAll(transform.position, Vector2.up, 1.9f, groundLayer);
                for (int c = 0; c < ceilingHits.Length; c++)
                {
                    var col = ceilingHits[c].collider;
                    if (col != null && !col.isTrigger && col != _col && col.gameObject != gameObject && !col.transform.IsChildOf(transform))
                    {
                        hasCeilingAbove = true;
                        break;
                    }
                }

                if (!hasCeilingAbove)
                {
                    bool shouldJump = false;
                    float customJumpForce = jumpForce;
                    float customForwardVel = 0f;
                    float dyToPlayer = _playerTransform.position.y - transform.position.y;

                    // Б. Фланговый прыжок через игрока (когда бот обходит вплотную на том же уровне)
                    if (isFlankingThrough && absDx < 2.0f && Mathf.Abs(dyToPlayer) < 1.3f)
                    {
                        shouldJump = true;
                        customJumpForce = jumpForce * 1.05f;
                        float flankDir = Mathf.Sign(targetX - transform.position.x);
                        customForwardVel = flankDir * (moveSpeed * 1.35f);
                        _jumpCooldownTimer = jumpCooldown * 1.3f;
                    }
                    // В. Боевой прыжок сближения (Combat Leap / Pounce) ТОЛЬКО при нахождении на одной высоте!
                    else if (_combatLeapTimer <= 0f && distToPlayer >= 2.6f && distToPlayer <= 6.0f && Mathf.Abs(dyToPlayer) < 1.3f)
                    {
                        shouldJump = true;
                        customJumpForce = jumpForce * 0.95f;
                        float leapDir = Mathf.Sign(targetX - transform.position.x);
                        customForwardVel = leapDir * (moveSpeed * 1.35f);
                        _combatLeapTimer = UnityEngine.Random.Range(combatLeapIntervalMin, combatLeapIntervalMax);
                    }
                    // Г. Препятствие / стена перед врагом на уровне пояса
                    else if (absDx > 0.5f)
                    {
                        Vector2 checkOrigin = (Vector2)transform.position + new Vector2(0f, 0.25f);
                        var wallHits = Physics2D.RaycastAll(checkOrigin, new Vector2(FacingDirection, 0f), 0.85f, groundLayer);
                        bool wallAhead = false;
                        for (int i = 0; i < wallHits.Length; i++)
                        {
                            var col = wallHits[i].collider;
                            if (col != null && !col.isTrigger && col != _col && col.gameObject != gameObject && !col.transform.IsChildOf(transform))
                            {
                                wallAhead = true;
                                break;
                            }
                        }

                        if (wallAhead)
                        {
                            shouldJump = true;
                            customForwardVel = FacingDirection * (moveSpeed * 1.05f);
                        }
                        else
                        {
                            // Д. Проверка ямы / обрыва перед ногами
                            Vector2 edgeOrigin = (Vector2)transform.position + new Vector2(FacingDirection * 0.65f, -0.2f);
                            var edgeHits = Physics2D.RaycastAll(edgeOrigin, Vector2.down, 1.4f, groundLayer);
                            bool groundAhead = false;
                            for (int i = 0; i < edgeHits.Length; i++)
                            {
                                var col = edgeHits[i].collider;
                                if (col != null && !col.isTrigger && col != _col && col.gameObject != gameObject && !col.transform.IsChildOf(transform))
                                {
                                    groundAhead = true;
                                    break;
                                }
                            }
                            if (!groundAhead)
                            {
                                shouldJump = true;
                                customForwardVel = FacingDirection * (moveSpeed * 1.05f);
                            }
                        }
                    }

                    if (shouldJump)
                    {
                        Jump(customJumpForce, customForwardVel);
                    }
                }
            }

            // -------------------------------------------------------------
            // 5. ДВИЖЕНИЕ И РАЗДЕЛЕНИЕ ВРАГОВ (ANTI-STACKING)
            // -------------------------------------------------------------
            if (Mathf.Abs(targetX - transform.position.x) < 0.2f && !pathHandledMovement)
            {
                dirX = 0f;
            }

            // Поворот: в прыжке смотрим в сторону полета, при обходе — по направлению бега, иначе на игрока
            if (_isJumpingToNextPlatform && Mathf.Abs(_activeJumpForwardSpeed) > 0.1f)
            {
                SetFacing(_activeJumpForwardSpeed);
            }
            else if (isFlankingThrough)
            {
                SetFacing(dirX != 0f ? dirX : FacingDirection);
            }
            else
            {
                SetFacing(Mathf.Sign(_playerTransform.position.x - transform.position.x));
            }

            // На земле применяем ходьбу и мягкое расталкивание между врагами (в воздухе/прыжке не перебиваем скорость!)
            if (!_isJumpingToNextPlatform && IsGrounded)
            {
                float separationVelocityX = 0f;
                for (int i = 0; i < _activeEnemies.Count; i++)
                {
                    var other = _activeEnemies[i];
                    if (other != null && other != this && !other.IsDead && other.gameObject.activeInHierarchy)
                    {
                        float diffX = transform.position.x - other.transform.position.x;
                        float diffY = Mathf.Abs(transform.position.y - other.transform.position.y);
                        if (Mathf.Abs(diffX) < separationDistance && diffY < 1.1f)
                        {
                            float pushSign = diffX > 0.001f ? 1f : (diffX < -0.001f ? -1f : (string.CompareOrdinal(name, other.name) >= 0 ? 1f : -1f));
                            float overlap = separationDistance - Mathf.Abs(diffX);
                            separationVelocityX += pushSign * (overlap / separationDistance) * 2.4f;
                        }
                    }
                }

                float finalVelX = (dirX * currentMoveSpeed) + separationVelocityX;
                _rb.linearVelocity = new Vector2(finalVelX, _rb.linearVelocity.y);
            }
        }

        public void CheckGrounded()
        {
            if (_col == null) _col = GetComponent<Collider2D>();
            if (_col == null) return;

            _groundFilter = new ContactFilter2D();
            _groundFilter.useTriggers = false;
            _groundFilter.SetLayerMask(groundLayer != 0 ? groundLayer : ~0);

            // 1. Прямой Collider2D.Cast вниз (игнорирует собственный коллайдер и триггеры)
            int count = _col.Cast(Vector2.down, _groundFilter, _groundCastHits, 0.15f);
            bool grounded = false;
            for (int i = 0; i < count; i++)
            {
                var h = _groundCastHits[i];
                if (h.collider != null && h.collider != _col && !h.collider.isTrigger && !h.collider.transform.IsChildOf(transform))
                {
                    if (h.normal.y > 0.35f)
                    {
                        grounded = true;
                        break;
                    }
                }
            }

            // 2. Fallback: RaycastAll по краям нижней границы коллайдера
            if (!grounded)
            {
                Bounds b = _col.bounds;
                Vector2 leftPoint = new Vector2(b.min.x + 0.08f, b.min.y + 0.02f);
                Vector2 centerPoint = new Vector2(b.center.x, b.min.y + 0.02f);
                Vector2 rightPoint = new Vector2(b.max.x - 0.08f, b.min.y + 0.02f);

                if (CheckRayDown(leftPoint) || CheckRayDown(centerPoint) || CheckRayDown(rightPoint))
                {
                    grounded = true;
                }
            }

            IsGrounded = grounded;
        }

        private bool CheckRayDown(Vector2 origin)
        {
            var hits = Physics2D.RaycastAll(origin, Vector2.down, 0.22f, groundLayer != 0 ? groundLayer : ~0);
            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i].collider;
                if (col != null && col != _col && !col.isTrigger && !col.transform.IsChildOf(transform))
                {
                    if (hits[i].normal.y > 0.35f) return true;
                }
            }
            return false;
        }

        public void Jump(float customForce = -1f, float customForwardSpeed = 0f)
        {
            if (!IsGrounded || _rb == null) return;
            _jumpCooldownTimer = jumpCooldown;
            float forceY = customForce > 0f ? customForce : jumpForce;
            float velX = customForwardSpeed != 0f ? customForwardSpeed : _rb.linearVelocity.x;
            _rb.linearVelocity = new Vector2(velX, forceY);
            Debug.Log($"[ENEMY AI] {name} совершает прыжок! (ForceY: {forceY:F1}, VelX: {velX:F1})");
        }

        public void SetFacing(float dir)
        {
            if (Mathf.Abs(dir) < 0.01f) return;
            FacingDirection = Mathf.Sign(dir);

            if (_sr != null) _sr.flipX = FacingDirection < 0f;

            if (faceTransform == null) faceTransform = transform.Find("Face");
            if (faceTransform != null)
            {
                float absX = Mathf.Abs(_faceBaseLocalPos.x > 0.001f ? _faceBaseLocalPos.x : 0.16f);
                float absScaleX = Mathf.Abs(_faceBaseLocalScale.x > 0.001f ? _faceBaseLocalScale.x : 0.45f);

                faceTransform.localPosition = new Vector3(absX * FacingDirection, _faceBaseLocalPos.y, _faceBaseLocalPos.z);
                faceTransform.localScale = new Vector3(absScaleX * FacingDirection, _faceBaseLocalScale.y, _faceBaseLocalScale.z);
            }
        }

        public Vector2 GetHitboxCenter()
        {
            return GetHitboxCenter(hitboxOffset);
        }

        public Vector2 GetHitboxCenter(Vector2 offset)
        {
            Vector2 pos = transform.position;
            return new Vector2(pos.x + offset.x * FacingDirection, pos.y + offset.y);
        }

        private void StartTelegraphAttack()
        {
            if (_stateRoutine != null) StopCoroutine(_stateRoutine);
            _stateRoutine = StartCoroutine(AttackSequenceRoutine());
        }

        private IEnumerator AttackSequenceRoutine()
        {
            // Останавливаем бег
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);

            // Поворачиваемся к игроку
            if (_playerTransform != null)
            {
                SetFacing(Mathf.Sign(_playerTransform.position.x - transform.position.x));
            }

            // Выбор типа атаки:
            // 1. Если игрок сверху — бьем вверх (Anti-Air)
            // 2. Иначе с вероятностью lowSweepChance бьем по ногам (Low Sweep)
            // 3. Иначе обычный прямой удар по центру (Mid)
            CombatZone chosenZone = CombatZone.Mid;
            Vector2 chosenOffset = hitboxOffset;
            Vector2 chosenSize = hitboxSize;
            string attackName = "Прямой средний удар врага";
            Vector2 strikeLunge = new Vector2(FacingDirection * strikeLungeForce, 0f);

            float dy = (_playerTransform != null) ? (_playerTransform.position.y - transform.position.y) : 0f;
            float absDx = (_playerTransform != null) ? Mathf.Abs(_playerTransform.position.x - transform.position.x) : 0f;

            if (dy > 1.0f && absDx < 1.9f)
            {
                chosenZone = CombatZone.High;
                chosenOffset = upHitboxOffset;
                chosenSize = upHitboxSize;
                attackName = "Вертикальный удар врага вверх (Anti-Air)";
                strikeLunge = new Vector2(0f, 2.4f);
            }
            else if (UnityEngine.Random.value < lowSweepChance)
            {
                chosenZone = CombatZone.Low;
                chosenOffset = lowHitboxOffset;
                chosenSize = lowHitboxSize;
                attackName = "Нижняя подсечка врага по ногам";
                strikeLunge = new Vector2(FacingDirection * (strikeLungeForce * 1.15f), 0f);
            }

            // 1. ФАЗА ТЕЛЕГРАФА (WINDUP)
            CurrentState = EnemyState.TelegraphWindup;
            float timer = 0f;

            while (timer < telegraphDuration)
            {
                timer += Time.deltaTime;
                float progress = timer / telegraphDuration;

                // Пульсация и показ полупрозрачного предупреждающего хитбокса
                float pulse = 0.5f + 0.5f * Mathf.Sin(timer * 18f);
                Vector2 center = GetHitboxCenter(chosenOffset);
                telegraphVisualizer.ShowTelegraph(center, chosenSize, pulse);

                yield return null;
            }

            // 2. АКТИВНАЯ ФАЗА УДАРА (ACTIVE STRIKE)
            CurrentState = EnemyState.ActiveStrike;
            Vector2 strikeCenter = GetHitboxCenter(chosenOffset);
            telegraphVisualizer.ShowActiveStrike(strikeCenter, chosenSize);

            // Микро-рывок вперед или подскок при ударе
            if (_rb != null)
            {
                _rb.linearVelocity = new Vector2(strikeLunge.x, _rb.linearVelocity.y + strikeLunge.y);
            }

            // Проверка нанесения урона игроку
            CheckHitPlayer(strikeCenter, chosenSize, chosenZone, chosenOffset, attackName);

            yield return new WaitForSeconds(activeStrikeDuration);

            // 3. ФАЗА ВОССТАНОВЛЕНИЯ (RECOVERY)
            CurrentState = EnemyState.Recovery;
            telegraphVisualizer.HideHitbox();
            if (_rb != null)
            {
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            }

            yield return new WaitForSeconds(recoveryDuration);

            CurrentState = EnemyState.Chasing;
            _stateRoutine = null;
        }

        private void CheckHitPlayer(Vector2 center, Vector2 size, CombatZone zone, Vector2 offset, string attackName)
        {
            _overlapResults.Clear();
            int count = Physics2D.OverlapBox(center, size, 0f, _playerFilter, _overlapResults);

            for (int i = 0; i < count; i++)
            {
                var col = _overlapResults[i];
                if (col == null || col.gameObject == gameObject || col.transform.IsChildOf(transform)) continue;
                if (_playerTransform != null && col.gameObject != _playerTransform.gameObject && !col.transform.IsChildOf(_playerTransform)) continue;

                var hurtbox = col.GetComponent<IHurtboxTarget2D>() ?? col.GetComponentInParent<IHurtboxTarget2D>();
                if (hurtbox != null)
                {
                    Vector2 kbToPlayer = knockbackToPlayer;
                    if (zone == CombatZone.High)
                    {
                        kbToPlayer = new Vector2(knockbackToPlayer.x * 0.5f, 7.0f);
                    }
                    else if (zone == CombatZone.Low)
                    {
                        kbToPlayer = new Vector2(knockbackToPlayer.x * 1.3f, 2.0f);
                    }

                    var enemyAttack = new AttackConfig(
                        attackName,
                        zone,
                        offset,
                        size,
                        telegraphDuration,
                        activeStrikeDuration,
                        recoveryDuration,
                        attackDamage,
                        kbToPlayer,
                        new Color(1f, 0.2f, 0.2f, 1f)
                    );

                    Vector2 knockbackDir = new Vector2(FacingDirection, zone == CombatZone.High ? 0.9f : 0.4f).normalized;
                    enemyAttack.attacker = gameObject;
                    hurtbox.TakeHit(enemyAttack, zone, center, knockbackDir);
                    break;
                }
            }
        }

        /// <summary>
        /// Реализация получения урона от атак игрока.
        /// Контратака отключена: враг получает честный урон, а его текущий замах прерывается.
        /// </summary>
        public void TakeHit(AttackConfig attack, CombatZone hitZone, Vector2 hitPoint, Vector2 knockbackDirection)
        {
            if (CurrentState == EnemyState.Dead) return;

            _lastHitTime = Time.time;

            // Обычное получение урона без крит-контратаки
            TakeNormalHit(attack, knockbackDirection);
        }

        public Vector2 CalculateEffectiveKnockback(Vector2 baseKnockback)
        {
            float ratio = Mathf.Clamp01(currentStamina / maxStamina);
            // При ratio = 1 (100% стамины) -> mult = knockbackMultAtFullStamina (по умолчанию 0.2)
            // При ratio = 0 (0% стамины) -> mult = knockbackMultAtZeroStamina (по умолчанию 1.0)
            float mult = Mathf.Lerp(knockbackMultAtZeroStamina, knockbackMultAtFullStamina, ratio);
            return baseKnockback * mult;
        }

        public float CalculateEffectiveStunDuration(float baseDuration)
        {
            float ratio = Mathf.Clamp01(currentStamina / maxStamina);
            float mult = Mathf.Lerp(stunDurationMultAtZeroStamina, 1.0f, ratio);
            return baseDuration * mult;
        }

        private void DrainStamina(float amount)
        {
            currentStamina = Mathf.Max(0f, currentStamina - amount);
            if (staminaBar != null)
            {
                staminaBar.SetStamina(currentStamina, maxStamina);
            }
        }

        private void TakeNormalHit(AttackConfig attack, Vector2 knockbackDirection)
        {
            // 1. Срыв текущего замаха/атаки при получении удара
            if (CurrentState == EnemyState.TelegraphWindup || CurrentState == EnemyState.ActiveStrike)
            {
                if (_stateRoutine != null) { StopCoroutine(_stateRoutine); _stateRoutine = null; }
                if (telegraphVisualizer != null) telegraphVisualizer.HideHitbox();
                CurrentState = EnemyState.Chasing;
            }

            // 2. Продлеваем стан/окно джаггла, если враг уже оглушен (>10 сек комбо)
            if (CurrentState == EnemyState.Stunned)
            {
                _stunRemaining = Mathf.Max(_stunRemaining, 1.8f);
            }

            float baseDmg = attack != null ? attack.damage : 20f;
            float dmg = baseDmg * _vulnerabilityMultiplier;

            if (!canDie)
            {
                currentHealth = Mathf.Max(1f, currentHealth - dmg);
            }
            else
            {
                currentHealth = Mathf.Max(0f, currentHealth - dmg);
            }

            float awayDir;
            if (_playerTransform != null && Mathf.Abs(transform.position.x - _playerTransform.position.x) > 0.05f)
            {
                awayDir = Mathf.Sign(transform.position.x - _playerTransform.position.x);
            }
            else if (Mathf.Abs(knockbackDirection.x) > 0.01f)
            {
                awayDir = Mathf.Sign(knockbackDirection.x);
            }
            else
            {
                awayDir = -FacingDirection;
            }

            if (_playerTransform != null)
            {
                SetFacing(Mathf.Sign(_playerTransform.position.x - transform.position.x));
            }

            if (_rb == null) _rb = GetComponent<Rigidbody2D>();

            bool isStale = attack != null && attack.isStale;
            bool isLauncher = attack != null && attack.isLauncher;

            if (isStale)
            {
                _staminaBoostTimer = 3.5f;
                Combat.Common.CombatFloatingText.ShowAdaptation(transform.position);
            }

            if (attack != null && _rb != null)
            {
                Vector2 rawKb = new Vector2(awayDir * attack.knockbackForce.x, attack.knockbackForce.y);
                Vector2 effectiveKb = CalculateEffectiveKnockback(rawKb);
                _rb.linearVelocity = effectiveKb;
                _hitstunTimer = 0.25f;
            }

            // Списание стамины от обычного удара (при Stale Move на 60% меньше урона по стойкости)
            bool wasAboveZero = currentStamina > 0f;
            float staminaDrain = isStale ? (dmg * staminaDrainMultiplier * 0.4f) : (dmg * staminaDrainMultiplier);
            DrainStamina(staminaDrain);

            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRoutine(normalColor, flashColor, 0.12f));

            Debug.Log($"[ENEMY HIT] Получен удар: {dmg:F1} HP | Stale: {isStale} | HP: {currentHealth:F0}/{maxHealth:F0} | Стамина: {currentStamina:F0}/{maxStamina:F0}");

            // Проверка гибели от удара
            if (canDie && currentHealth <= 0f)
            {
                Die(new Vector2(awayDir, 0f), false);
                return;
            }

            // Лаунчер: если стамина на нуле (0), враг подлетает высоко в воздух для джаггл-комбо!
            if (isLauncher && currentStamina <= 0f)
            {
                if (_rb != null) _rb.linearVelocity = new Vector2(awayDir * 2.8f, 13.5f);
                _hitstunTimer = 0.7f;
                Combat.Common.CombatFloatingText.ShowLauncher(transform.position);
                CurrentState = EnemyState.Stunned;
                if (_stateRoutine != null) StopCoroutine(_stateRoutine);
                _stateRoutine = StartCoroutine(StunRoutine(2.5f, replenishStaminaAfter: false));
                return;
            }

            // Если стамина только что опустилась до 0 — наступает Stamina Break!
            if (wasAboveZero && currentStamina <= 0f)
            {
                TriggerStaminaBreak();
                return;
            }

            // Если не были в атаке или стане — если стояли в Idle, сразу агримся на игрока
            if (CurrentState == EnemyState.Idle)
            {
                CurrentState = EnemyState.Chasing;
            }
        }

        private void TriggerStaminaBreak()
        {
            if (CurrentState == EnemyState.Dead) return;

            if (_stateRoutine != null) { StopCoroutine(_stateRoutine); _stateRoutine = null; }
            if (telegraphVisualizer != null)
            {
                telegraphVisualizer.HideHitbox();
                telegraphVisualizer.ShowNoStaminaPopup(transform.position);
            }

            // Сохраняем физический импульс удара (включая подбрасывание вверх от верхней атаки).
            CurrentState = EnemyState.Stunned;
            Debug.Log($"<color=orange><b>[НЕТ СТАМИНЫ!]</b></color> Враг истощен и оглушен на {staminaBreakStunDuration:F1}с!");
            _stateRoutine = StartCoroutine(StunRoutine(staminaBreakStunDuration, replenishStaminaAfter: true));
        }

        /// <summary>
        /// Принудительно оглушает врага (вызывается при успешном парировании игрока).
        /// Срывает любые замахи и действия, останавливает врага и открывает его для комбо.
        /// </summary>
        public void Stun(float duration)
        {
            if (CurrentState == EnemyState.Dead) return;

            if (_stateRoutine != null) { StopCoroutine(_stateRoutine); _stateRoutine = null; }
            if (_flashRoutine != null) { StopCoroutine(_flashRoutine); _flashRoutine = null; }
            if (telegraphVisualizer != null) telegraphVisualizer.HideHitbox();

            if (_rb != null) _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);

            CurrentState = EnemyState.Stunned;
            float effectiveStun = CalculateEffectiveStunDuration(duration);
            _stateRoutine = StartCoroutine(StunRoutine(effectiveStun, replenishStaminaAfter: false));

            if (telegraphVisualizer != null)
            {
                telegraphVisualizer.SpawnCustomPopup(transform.position + Vector3.up * 1.3f, $"[ОГЛУШЕН! {effectiveStun:F1}с]", new Color(1f, 0.85f, 0.2f));
            }
            Debug.Log($"<color=gold><b>[PARRY STUN!]</b></color> Враг парирован и оглушен на {effectiveStun:F1}с!");
        }

        // ==========================================
        // TACTICIAN STATUS EFFECT API
        // ==========================================

        public void ApplyVulnerabilityMark(float duration, float bonusMultiplier = 1.35f)
        {
            if (CurrentState == EnemyState.Dead) return;
            _vulnerabilityMultiplier = bonusMultiplier;
            _vulnerabilityTimer = duration;

            if (_sr != null && CurrentState != EnemyState.Stunned)
            {
                _sr.color = new Color(0.9f, 0.35f, 1f, 1f); // Фиолетово-рунический оттенок уязвимости
            }

            if (telegraphVisualizer != null)
            {
                telegraphVisualizer.SpawnCustomPopup(transform.position + Vector3.up * 1.3f, "[УЯЗВИМОСТЬ +35%]", new Color(0.85f, 0.4f, 1f));
            }
            Debug.Log($"<color=#D866FF>[VULNERABILITY MARK]</color> На врага наложена метка уязвимости на {duration:F1}с (+35% урона)!");
        }

        public void ApplyRoot(float duration)
        {
            if (CurrentState == EnemyState.Dead) return;
            _isRooted = true;
            _rootTimer = Mathf.Max(_rootTimer, duration);
            if (_rb != null) _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);

            if (telegraphVisualizer != null)
            {
                telegraphVisualizer.SpawnCustomPopup(transform.position + Vector3.up * 1.0f, $"[ОБЕЗДВИЖЕН {duration:F1}с]", new Color(0f, 0.9f, 1f));
            }
            Debug.Log($"<color=#00E5FF>[ROOT EFFECT]</color> Враг обездвижен на {duration:F1}с!");
        }

        public void ApplyGravitySuspension(float duration, Vector2 anchorPos)
        {
            if (CurrentState == EnemyState.Dead) return;
            _isGravitySuspended = true;
            _gravitySuspendTimer = duration;
            _gravityAnchorPos = anchorPos;

            if (_rb != null)
            {
                _originalGravityScale = _rb.gravityScale > 0.01f ? _rb.gravityScale : 1.0f;
                _rb.gravityScale = 0f;
                _rb.linearVelocity = Vector2.zero;
            }

            if (telegraphVisualizer != null)
            {
                telegraphVisualizer.SpawnCustomPopup(transform.position + Vector3.up * 1.2f, $"[ГРАВИТАЦИОННЫЙ ЗАХВАТ {duration:F1}с]", new Color(0.65f, 0.35f, 1f));
            }
            Debug.Log($"<color=#9955FF>[GRAVITY ANCHOR]</color> Враг захвачен гравитационной аномалией на {duration:F1}с!");
        }
        public void Disorient(float duration) => DisorientFromSmoke(duration);

        public void DisorientFromSmoke(float duration)
        {
            if (CurrentState == EnemyState.Dead) return;
            _disorientTimer = duration;

            if (CurrentState == EnemyState.TelegraphWindup || CurrentState == EnemyState.ActiveStrike)
            {
                if (_stateRoutine != null) { StopCoroutine(_stateRoutine); _stateRoutine = null; }
                if (telegraphVisualizer != null) telegraphVisualizer.HideHitbox();
                CurrentState = EnemyState.Idle;
            }

            if (_rb != null) _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);

            if (telegraphVisualizer != null)
            {
                telegraphVisualizer.SpawnCustomPopup(transform.position + Vector3.up * 1.1f, "[ОСЛЕПЛЕН / ПОТЕРЯ ЦЕЛИ]", new Color(0.8f, 0.85f, 0.9f));
            }
            Debug.Log($"<color=#CCCCCC>[SMOKE BLIND]</color> Враг ослеплен дымовой завесой на {duration:F1}с, атака сорвана!");
        }

        public void PullTowards(Vector2 targetPos, float pullSpeed)
        {
            if (CurrentState == EnemyState.Dead) return;
            Vector2 dir = (targetPos - (Vector2)transform.position).normalized;
            if (_rb != null)
            {
                _rb.linearVelocity = new Vector2(dir.x * pullSpeed, 4.0f);
                _hitstunTimer = 0.35f;
            }

            if (telegraphVisualizer != null)
            {
                telegraphVisualizer.SpawnCustomPopup(transform.position + Vector3.up * 1.1f, "[ПРИТЯГИВАНИЕ!]", new Color(0.9f, 0.2f, 0.9f));
            }
            Debug.Log($"<color=#E033FF>[HARPOON PULL]</color> Враг притянут к игроку!");
        }

        /// <summary>
        /// Применяет процедурное масштабирование сложности (от кругов кристалла).
        /// Увеличивает максимальное здоровье, выносливость и ускоряет фазу замаха (телеграфа).
        /// </summary>
        public void ApplyDifficultyScaling(float healthMultiplier, float staminaMultiplier, float telegraphSpeedMultiplier)
        {
            if (healthMultiplier > 0.01f)
            {
                maxHealth = Mathf.Round(maxHealth * healthMultiplier);
                currentHealth = maxHealth;
            }

            if (staminaMultiplier > 0.01f)
            {
                maxStamina = Mathf.Round(maxStamina * staminaMultiplier);
                currentStamina = maxStamina;
                if (staminaBar != null)
                {
                    staminaBar.Initialize(maxStamina);
                }
            }

            if (telegraphSpeedMultiplier > 0.01f)
            {
                // Замах ускоряется (длительность делится на множитель скорости, но не меньше 0.35с)
                telegraphDuration = Mathf.Max(0.35f, telegraphDuration / telegraphSpeedMultiplier);
            }
        }

        public void ApplyDifficultyScaling(float staminaMultiplier, float telegraphSpeedMultiplier)
        {
            ApplyDifficultyScaling(1.0f, staminaMultiplier, telegraphSpeedMultiplier);
        }

        public void Die(Vector2 knockbackDirection, bool wasCounter = false)
        {
            if (CurrentState == EnemyState.Dead) return;
            CurrentState = EnemyState.Dead;

            if (_stateRoutine != null) { StopCoroutine(_stateRoutine); _stateRoutine = null; }
            if (_flashRoutine != null) { StopCoroutine(_flashRoutine); _flashRoutine = null; }

            if (telegraphVisualizer != null)
            {
                telegraphVisualizer.HideHitbox();
                telegraphVisualizer.ShowDefeatPopup(transform.position);
            }

            if (staminaBar != null) staminaBar.SetVisible(false);

            if (_col != null) _col.enabled = false;

            // Физический отброс и падение
            if (_rb != null)
            {
                float kbX = knockbackDirection.x != 0f ? knockbackDirection.x : -FacingDirection;
                _rb.linearVelocity = new Vector2(kbX * 4f, 2.5f);
            }

            transform.rotation = Quaternion.Euler(0f, 0f, -90f * FacingDirection);

            if (_sr != null) _sr.color = defeatColor;

            Debug.Log($"<color=red><b>[ENEMY DEFEATED]</b></color> Враг повержен! {(wasCounter ? "(Контратакой!) " : "")}Возрождение через {respawnDelay:F1}с");

            // Начисление очков стиля игроку за убийство врага
            if (Combat.Style.StyleManager.Instance != null)
            {
                Combat.Style.StyleManager.Instance.AddEnemyKill(this, wasCounter, transform.position);
            }

            if (respawnOnDeath)
            {
                if (_respawnRoutine != null) StopCoroutine(_respawnRoutine);
                _respawnRoutine = StartCoroutine(RespawnRoutine(respawnDelay));
            }
        }

        public void Respawn()
        {
            if (CurrentState != EnemyState.Dead) return;

            transform.position = _spawnPosition;
            transform.rotation = _spawnRotation;

            if (_rb != null) _rb.linearVelocity = Vector2.zero;

            currentHealth = maxHealth;
            currentStamina = maxStamina;
            if (staminaBar != null)
            {
                staminaBar.SetStamina(currentStamina, maxStamina);
                staminaBar.SetVisible(true);
            }

            if (_sr != null) _sr.color = normalColor;
            if (_col != null) _col.enabled = true;

            SetFacing(-1f);
            CurrentState = EnemyState.Idle;

            Debug.Log("<color=green><b>[ENEMY RESPAWNED]</b></color> Враг возродился на исходной позиции со 100% HP и стамины!");
        }

        public void RestoreState(Vector2 position, float health, float stamina, float facing, bool isDeadState)
        {
            if (_stateRoutine != null) { StopCoroutine(_stateRoutine); _stateRoutine = null; }
            if (_flashRoutine != null) { StopCoroutine(_flashRoutine); _flashRoutine = null; }
            if (_respawnRoutine != null) { StopCoroutine(_respawnRoutine); _respawnRoutine = null; }
            if (_hitstopRoutine != null) { StopCoroutine(_hitstopRoutine); _hitstopRoutine = null; }

            _isRooted = false;
            _rootTimer = 0f;
            _vulnerabilityMultiplier = 1.0f;
            _vulnerabilityTimer = 0f;
            _isGravitySuspended = false;
            _gravitySuspendTimer = 0f;
            _disorientTimer = 0f;

            transform.position = new Vector3(position.x, position.y, transform.position.z);
            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.gravityScale = _originalGravityScale;
            }

            if (isDeadState)
            {
                currentHealth = 0f;
                currentStamina = 0f;
                Die(Vector2.zero, false);
            }
            else
            {
                CurrentState = EnemyState.Idle;
                transform.rotation = _spawnRotation;

                if (_col != null) _col.enabled = true;
                if (_sr != null)
                {
                    _sr.color = normalColor;
                }

                currentHealth = Mathf.Clamp(health, 1f, maxHealth);
                currentStamina = Mathf.Clamp(stamina, 0f, maxStamina);

                if (staminaBar != null)
                {
                    staminaBar.SetStamina(currentStamina, maxStamina);
                    staminaBar.SetVisible(true);
                }

                if (telegraphVisualizer != null)
                {
                    telegraphVisualizer.HideHitbox();
                }

                SetFacing(facing != 0f ? Mathf.Sign(facing) : -1f);
            }
        }

        private IEnumerator RespawnRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            Respawn();
            _respawnRoutine = null;
        }

        private IEnumerator StunRoutine(float duration, bool replenishStaminaAfter = false)
        {
            if (_sr != null) _sr.color = stunColor;

            _stunRemaining = duration;
            while (_stunRemaining > 0f)
            {
                _stunRemaining -= Time.deltaTime;
                // Мерцание оглушения
                if (_sr != null)
                {
                    float flash = Mathf.PingPong(Time.time * 8f, 1f);
                    _sr.color = Color.Lerp(stunColor, Color.white, flash);
                }
                yield return null;
            }

            if (_sr != null) _sr.color = normalColor;

            // Восстановление стамины после стана (только если прошло время без ударов)
            if ((replenishStaminaAfter || currentStamina <= 0f) && Time.time - _lastHitTime >= 2.0f)
            {
                currentStamina = maxStamina;
                if (staminaBar != null)
                {
                    staminaBar.SetStamina(currentStamina, maxStamina);
                }
                Debug.Log("<color=yellow>[STAMINA RESTORED]</color> Стамина врага полностью восстановилась после стана!");
            }

            CurrentState = EnemyState.Chasing;
            _stateRoutine = null;
        }


        private IEnumerator FlashRoutine(Color defaultCol, Color flashCol, float duration)
        {
            if (_sr == null) yield break;
            _sr.color = flashCol;
            yield return new WaitForSeconds(duration);
            if (CurrentState != EnemyState.Stunned && CurrentState != EnemyState.Dead)
            {
                _sr.color = defaultCol;
            }
            _flashRoutine = null;
        }

        private void OnDrawGizmos()
        {
            if (!showPathGizmos) return;

            // Отрисовка активного маршрута навигации прямо в окне Scene!
            if (_currentNavPath != null && _currentNavPath.Count > 0)
            {
                Vector2 prevPos = transform.position;

                for (int i = Mathf.Clamp(_currentPathIndex - 1, 0, _currentNavPath.Count - 1); i < _currentNavPath.Count; i++)
                {
                    var step = _currentNavPath[i];

                    if (step.Action == NavActionType.Jump)
                    {
                        // Траектория прыжка (параболическая дуга)
                        Gizmos.color = new Color(1.0f, 0.72f, 0.1f, 0.95f);
                        PlatformNavGraph2D.DrawJumpArcGizmo(step.Position, step.LandingTarget, step.JumpForce, step.ForwardSpeed, step.FlightDuration);
                        Gizmos.DrawSphere(step.Position, 0.14f);
                        Gizmos.DrawSphere(step.LandingTarget, 0.18f);
                        prevPos = step.LandingTarget;
                    }
                    else if (step.Action == NavActionType.Drop)
                    {
                        // Траектория спуска
                        Gizmos.color = new Color(0.2f, 0.8f, 1.0f, 0.85f);
                        Gizmos.DrawLine(prevPos, step.Position);
                        Gizmos.DrawLine(step.Position, step.LandingTarget);
                        Gizmos.DrawSphere(step.LandingTarget, 0.15f);
                        prevPos = step.LandingTarget;
                    }
                    else
                    {
                        // Обычный бег по платформе
                        Gizmos.color = pathGizmoColor;
                        Gizmos.DrawLine(prevPos, step.Position);
                        Gizmos.DrawSphere(step.Position, 0.13f);
                        prevPos = step.Position;
                    }
                }

                // Маркер целевой точки
                Gizmos.color = new Color(1.0f, 0.2f, 0.2f, 0.85f);
                if (_playerTransform != null)
                {
                    Gizmos.DrawWireSphere(_lastPathGoal, 0.35f);
                }
            }

#if UNITY_EDITOR
            if (showPathGizmos && CurrentState == EnemyState.Chasing && _currentNavPath != null && _currentNavPath.Count > 0 && _currentPathIndex < _currentNavPath.Count)
            {
                var currStep = _currentNavPath[_currentPathIndex];
                string label = $"[NAV] {currStep.Action} ({_currentPathIndex + 1}/{_currentNavPath.Count})\n{currStep.Description}";
                GUIStyle style = new GUIStyle();
                style.normal.textColor = pathGizmoColor;
                style.fontStyle = FontStyle.Bold;
                style.fontSize = 11;
                style.alignment = TextAnchor.MiddleCenter;
                UnityEditor.Handles.Label(transform.position + new Vector3(0f, 1.35f, 0f), label, style);
            }
#endif
        }

        private void OnDrawGizmosSelected()
        {
            // Предпросмотр пути в окне Scene даже в режиме редактирования (Edit Mode)
            if (!Application.isPlaying && showPathGizmos && PlatformNavGraph2D.Instance != null)
            {
                var target = _playerTransform != null ? _playerTransform : (GameObject.FindWithTag("Player")?.transform);
                if (target != null)
                {
                    var previewPath = new List<NavPathStep>();
                    if (PlatformNavGraph2D.Instance.FindPath(transform.position, target.position, previewPath))
                    {
                        Vector2 prevPos = transform.position;
                        for (int i = 0; i < previewPath.Count; i++)
                        {
                            var step = previewPath[i];
                            if (step.Action == NavActionType.Jump)
                            {
                                Gizmos.color = new Color(1.0f, 0.72f, 0.1f, 0.95f);
                                PlatformNavGraph2D.DrawJumpArcGizmo(step.Position, step.LandingTarget, step.JumpForce, step.ForwardSpeed, step.FlightDuration);
                                Gizmos.DrawSphere(step.Position, 0.14f);
                                Gizmos.DrawSphere(step.LandingTarget, 0.18f);
                                prevPos = step.LandingTarget;
                            }
                            else if (step.Action == NavActionType.Drop)
                            {
                                Gizmos.color = new Color(0.2f, 0.8f, 1.0f, 0.85f);
                                Gizmos.DrawLine(prevPos, step.Position);
                                Gizmos.DrawLine(step.Position, step.LandingTarget);
                                Gizmos.DrawSphere(step.LandingTarget, 0.15f);
                                prevPos = step.LandingTarget;
                            }
                            else
                            {
                                Gizmos.color = pathGizmoColor;
                                Gizmos.DrawLine(prevPos, step.Position);
                                Gizmos.DrawSphere(step.Position, 0.13f);
                                prevPos = step.Position;
                            }
                        }

#if UNITY_EDITOR
                        string label = $"[NAV PREVIEW] {previewPath.Count} steps to Player";
                        GUIStyle style = new GUIStyle();
                        style.normal.textColor = pathGizmoColor;
                        style.fontStyle = FontStyle.Bold;
                        style.fontSize = 11;
                        style.alignment = TextAnchor.MiddleCenter;
                        UnityEditor.Handles.Label(transform.position + new Vector3(0f, 1.35f, 0f), label, style);
#endif
                    }
                }
            }

            // Зеленый круг зоны обнаружения
            Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, detectionRange);

            // Красный круг дистанции атаки
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, attackRange);

            // Хитбокс прямого удара
            Gizmos.color = new Color(1f, 0.7f, 0.1f, 0.8f);
            Gizmos.DrawWireCube(GetHitboxCenter(), hitboxSize);
        }
    }
}
