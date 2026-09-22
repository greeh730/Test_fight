using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Combat;
using Combat.Common;
using Combat.Stances;
using Combat.UI;

namespace Combat.Player
{
    /// <summary>
    /// Контроллер блокирования и парирования игрока по нажатию ПКМ (Right Mouse Button).
    /// - Удержание ПКМ: вход в стойку кругового блока (защита слева и справа).
    ///   Пока игрок держит блок — стамина не восстанавливается.
    ///   Удар поглощается за счет выносливости. При нулевой выносливости блок не ломается,
    ///   но пропускает частичный урон (Chip Damage).
    /// - Быстрый свайп/отпускание ПКМ с направлением: парирование.
    ///   При удачном парировании: 100% восполнение выносливости (снятие истощения),
    ///   оглушение врага (Stun) и мгновенный выход в комбо.
    ///   При неудачном парировании: потеря выносливости и стаггер (неподвижность).
    ///   При нулевой выносливости: парирование активируется в n раз дольше, а стаггер длится в n раз дольше.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealth2D))]
    [RequireComponent(typeof(PlayerStamina2D))]
    public class PlayerBlockAndParry2D : MonoBehaviour
    {
        [Header("--- Block Settings (Удержание ПКМ) ---")]
        [Tooltip("Расход стамины за каждый заблокированный удар")]
        [SerializeField] private float blockStaminaCost = 20f;

        [Tooltip("Доля урона, проходящая сквозь блок при нулевой выносливости (Chip Damage)")]
        [Range(0.1f, 0.9f)]
        [SerializeField] private float chipDamageMultiplier = 0.45f;

        [Tooltip("Множитель скорости перемещения во время удержания блока")]
        [Range(0f, 1f)]
        [SerializeField] private float blockMoveSpeedMultiplier = 0.35f;

        [Tooltip("Множитель отталкивания игрока при блокировании")]
        [SerializeField] private float blockPushbackMultiplier = 0.25f;

        [Header("--- Parry Settings (Свайп/клик ПКМ с направлением) ---")]
        [Tooltip("Базовая длительность подготовки (Startup) парирования в секундах")]
        [SerializeField] private float parryStartupDuration = 0.05f;

        [Tooltip("Длительность активного окна парирования (в секундах)")]
        [SerializeField] private float parryActiveDuration = 0.35f;

        [Tooltip("Длительность стаггера (наказания) при неудачном парировании")]
        [SerializeField] private float parryWhiffDuration = 0.35f;

        [Tooltip("Расход стамины при промахе парирования (с учетом анти-спама)")]
        [SerializeField] private float parryWhiffStaminaCost = 20f;

        [Tooltip("Длительность оглушения врага при успешном парировании (сек)")]
        [SerializeField] private float parryStunDuration = 2.0f;

        [Tooltip("Длительность тактильного хитстопа при успешном парировании (сек)")]
        [SerializeField] private float parryHitstopDuration = 0.12f;

        [Tooltip("Минимальная дистанция перемещения мыши для регистрации свайпа (в пикселях)")]
        [SerializeField] private float gestureSwipeThreshold = 25f;

        [Tooltip("Максимальное время от нажатия до отпускания ПКМ для быстрого тапа/парирования")]
        [SerializeField] private float quickTapParryWindow = 0.26f;

        [Header("--- Vector Wheel Integration ---")]
        [SerializeField] private Combat.UI.VectorWheelController vectorWheel;

        [Header("--- Visual & Colors ---")]
        [SerializeField] private Color blockNormalColor = new Color(0.15f, 0.75f, 1f, 0.75f);
        [SerializeField] private Color blockChipColor = new Color(1f, 0.35f, 0.15f, 0.85f);
        [SerializeField] private Color parryActiveColor = new Color(1f, 0.92f, 0.3f, 0.95f);

        [Header("--- Events ---")]
        public UnityEvent<bool> onBlockStateChanged;
        public UnityEvent onParryStarted;
        public UnityEvent onParrySuccess;
        public UnityEvent onParryWhiff;

        // Components
        private PlayerHealth2D _health;
        private PlayerStamina2D _stamina;
        private PlayerController2D _movement;
        private PlayerCombatController2D _combat;
        private Rigidbody2D _rb;
        private SpriteRenderer _sr;

        // Block / Parry State
        public bool IsBlocking { get; private set; }
        public bool IsParrying { get; private set; }
        public bool IsParryStaggered { get; private set; }
        public float BlockMoveSpeedMultiplier => blockMoveSpeedMultiplier;
        public Vector2 CurrentParryDirection => _parryDirection;

        public float GetCurrentFacing()
        {
            EnsureComponents();
            if (_combat != null) return _combat.FacingDirection;
            if (_movement != null) return _movement.CurrentFacing;
            if (_sr != null) return _sr.flipX ? -1f : 1f;
            return 1f;
        }

        private float _rmbPressTime;
        private Vector2 _rmbPressScreenPos;
        private Vector2 _parryDirection = Vector2.right;

        private Coroutine _parryRoutine;
        private Coroutine _staggerRoutine;

        // Visual Shield GameObject
        private GameObject _shieldRootObj;
        private SpriteRenderer _shieldRenderer;
        private LineRenderer _shieldOutlineRenderer;

        private void Awake()
        {
            EnsureComponents();
            BuildProceduralShieldVisual();
        }

        private void EnsureComponents()
        {
            if (_health == null) _health = GetComponent<PlayerHealth2D>();
            if (_stamina == null) _stamina = GetComponent<PlayerStamina2D>();
            if (_movement == null) _movement = GetComponent<PlayerController2D>();
            if (_combat == null) _combat = GetComponent<PlayerCombatController2D>();
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        }

        private void BuildProceduralShieldVisual()
        {
            if (_shieldRootObj != null) return;

            _shieldRootObj = new GameObject("Player_BlockShield_Visual");
            _shieldRootObj.transform.SetParent(transform, false);
            _shieldRootObj.transform.localPosition = new Vector3(0.45f, 0.05f, 0f);

            // Полупрозрачный заполняющий сегмент фронтального щита
            var fillObj = new GameObject("Shield_Fill");
            fillObj.transform.SetParent(_shieldRootObj.transform, false);
            _shieldRenderer = fillObj.AddComponent<SpriteRenderer>();
            _shieldRenderer.sprite = CombatSprites.WhiteBox;
            _shieldRenderer.color = new Color(blockNormalColor.r, blockNormalColor.g, blockNormalColor.b, 0.24f);
            _shieldRenderer.sortingOrder = 35;
            fillObj.transform.localPosition = new Vector3(0.18f, 0f, 0f);
            fillObj.transform.localScale = new Vector3(0.72f, 1.95f, 1f);

            // Внешний светящийся контур фронтального щита (выпуклая дуга барьера спереди)
            var outlineObj = new GameObject("Shield_Outline");
            outlineObj.transform.SetParent(_shieldRootObj.transform, false);
            _shieldOutlineRenderer = outlineObj.AddComponent<LineRenderer>();
            _shieldOutlineRenderer.positionCount = 18;
            _shieldOutlineRenderer.useWorldSpace = false;
            _shieldOutlineRenderer.loop = true;
            _shieldOutlineRenderer.startWidth = 0.05f;
            _shieldOutlineRenderer.endWidth = 0.05f;
            _shieldOutlineRenderer.sortingOrder = 36;
            _shieldOutlineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            _shieldOutlineRenderer.startColor = blockNormalColor;
            _shieldOutlineRenderer.endColor = blockNormalColor;

            // Формируем выпуклую защитную дугу щита спереди (от -70° до +70°)
            float rx = 0.65f;
            float ry = 1.05f;
            int arcPoints = 14;
            for (int i = 0; i < arcPoints; i++)
            {
                float t = (float)i / (arcPoints - 1);
                float angle = Mathf.Lerp(-1.22f, 1.22f, t); // ~ -70 deg to +70 deg
                float x = Mathf.Cos(angle) * rx;
                float y = Mathf.Sin(angle) * ry;
                _shieldOutlineRenderer.SetPosition(i, new Vector3(x, y, 0f));
            }
            // Замыкаем заднюю кромку щита
            _shieldOutlineRenderer.SetPosition(14, new Vector3(0.05f, ry * 0.75f, 0f));
            _shieldOutlineRenderer.SetPosition(15, new Vector3(-0.06f, 0f, 0f));
            _shieldOutlineRenderer.SetPosition(16, new Vector3(0.05f, -ry * 0.75f, 0f));
            _shieldOutlineRenderer.SetPosition(17, _shieldOutlineRenderer.GetPosition(0));

            _shieldRootObj.SetActive(false);
        }

        private void Start()
        {
            EnsureWheelConnection();
        }

        private void OnEnable()
        {
            EnsureWheelConnection();
        }

        public void EnsureWheelConnection()
        {
            if (vectorWheel == null)
            {
                vectorWheel = FindAnyObjectByType<Combat.UI.VectorWheelController>();
            }

            if (vectorWheel != null)
            {
                vectorWheel.onBlockStateChanged.RemoveListener(OnWheelBlockStateChanged);
                vectorWheel.onParrySwipeCompleted.RemoveListener(OnWheelParrySwipeCompleted);

                vectorWheel.onBlockStateChanged.AddListener(OnWheelBlockStateChanged);
                vectorWheel.onParrySwipeCompleted.AddListener(OnWheelParrySwipeCompleted);
            }
        }

        private void OnDisable()
        {
            if (vectorWheel != null)
            {
                vectorWheel.onBlockStateChanged.RemoveListener(OnWheelBlockStateChanged);
                vectorWheel.onParrySwipeCompleted.RemoveListener(OnWheelParrySwipeCompleted);
            }
            StopBlocking();
            CancelParryAndStagger();
        }

        public void OnWheelBlockStateChanged(bool blocking)
        {
            if (blocking)
            {
                if (!IsParrying && CanEnterBlock())
                {
                    StartBlocking();
                }
            }
            else
            {
                StopBlocking();
            }
        }

        public void OnWheelParrySwipeCompleted(Direction8 dir, Vector2 dirVec, float distance)
        {
            if (IsParryStaggered) return;

            StopBlocking();
            ExecuteParry(dirVec);
        }

        private void Update()
        {
            if (Combat.UI.PauseMenuController.IsGamePaused || Time.timeScale <= 0.0001f)
            {
                if (IsBlocking) StopBlocking();
                return;
            }

            if (_health != null && _health.IsDead)
            {
                if (IsBlocking) StopBlocking();
                CancelParryAndStagger();
                return;
            }

            // Если Векторное Колесо подключено, оно управляет жестами и событиями блока/парирования
            if (vectorWheel == null)
            {
                HandleInput();
            }

            UpdateVisuals();
        }

        private void HandleInput()
        {
            // Если игрок оглушен после промаха парирования — ввод заблокирован
            if (IsParryStaggered)
            {
                if (IsBlocking) StopBlocking();
                return;
            }

            // 1. Нажатие ПКМ (Mouse Down)
            if (IsRmbDown())
            {
                _rmbPressTime = Time.time;
                _rmbPressScreenPos = GetMousePosition();

                // Если не атакуем и не парируем — сразу встаем в блок
                if (!IsParrying && CanEnterBlock())
                {
                    StartBlocking();
                }
            }

            // 2. Удержание ПКМ (Mouse Held)
            if (IsRmbHeld())
            {
                if (!IsParrying && !IsBlocking && CanEnterBlock())
                {
                    StartBlocking();
                }

                // Пока держим блок — регенерация стамины полностью замораживается
                if (IsBlocking && _stamina != null)
                {
                    _stamina.IsRegenPaused = true;
                }
            }

            // 3. Отпускание ПКМ (Mouse Up)
            if (IsRmbUp())
            {
                Vector2 releaseScreenPos = GetMousePosition();
                Vector2 delta = releaseScreenPos - _rmbPressScreenPos;
                float holdDuration = Time.time - _rmbPressTime;

                // Проверка: это жест парирования или просто отпускание блока?
                bool isSwipeGesture = delta.magnitude >= gestureSwipeThreshold;
                bool isQuickTap = holdDuration <= quickTapParryWindow;

                if (!IsParrying && (isSwipeGesture || isQuickTap))
                {
                    // Игрок выбрал направление и отпустил ПКМ -> Парирование!
                    Vector2 parryDir;
                    if (isSwipeGesture)
                    {
                        parryDir = delta.normalized;
                    }
                    else
                    {
                        Vector3 worldMouse = Camera.main != null ? Camera.main.ScreenToWorldPoint(releaseScreenPos) : transform.position + Vector3.right;
                        parryDir = ((Vector2)worldMouse - (Vector2)transform.position).normalized;
                    }

                    if (parryDir.sqrMagnitude < 0.001f)
                    {
                        parryDir = _movement != null ? new Vector2(_movement.CurrentFacing, 0f) : Vector2.right;
                    }

                    StopBlocking();
                    ExecuteParry(parryDir);
                }
                else
                {
                    // Обычное опускание щита
                    StopBlocking();
                }
            }
        }

        private bool CanEnterBlock()
        {
            if (_combat != null)
            {
                // Нельзя заблокировать посреди активного удара или замаха
                if (_combat.CurrentState == CombatState.Startup || _combat.CurrentState == CombatState.Active)
                {
                    return false;
                }
            }
            return true;
        }

        private void StartBlocking()
        {
            EnsureComponents();
            if (IsBlocking) return;
            IsBlocking = true;

            if (_combat != null && _combat.CurrentState == CombatState.Recovery)
            {
                _combat.CancelAttack();
            }

            if (_stamina != null)
            {
                _stamina.IsRegenPaused = true;
            }

            if (_shieldRootObj != null)
            {
                _shieldRootObj.SetActive(true);
            }

            onBlockStateChanged?.Invoke(true);
            Debug.Log("<color=#22C3FF><b>[BLOCK]</b></color> Вход в стойку блокирования (защита спереди).");
        }

        private void StopBlocking()
        {
            if (!IsBlocking) return;
            IsBlocking = false;

            if (_stamina != null)
            {
                _stamina.IsRegenPaused = false;
            }

            if (_shieldRootObj != null)
            {
                _shieldRootObj.SetActive(false);
            }

            onBlockStateChanged?.Invoke(false);
            Debug.Log("<color=#22C3FF>[BLOCK]</color> Стойка блокирования снята.");
        }

        /// <summary>
        /// Запускает процесс парирования в выбранном направлении.
        /// </summary>
        public void ExecuteParry(Vector2 direction)
        {
            EnsureComponents();
            if (IsParrying || IsParryStaggered) return;

            if (Mathf.Abs(direction.x) > 0.05f && _movement != null)
            {
                _movement.SetFacing(direction.x);
            }

            if (_parryRoutine != null) StopCoroutine(_parryRoutine);
            _parryRoutine = StartCoroutine(ParrySequenceRoutine(direction));
        }

        private IEnumerator ParrySequenceRoutine(Vector2 direction)
        {
            IsParrying = true;
            _parryDirection = direction;
            onParryStarted?.Invoke();

            // Если игрок истощен (0 стамины) — парирование активируется в n раз дольше
            float slowdownFactor = (_stamina != null && _stamina.IsExhausted) ? _stamina.ExhaustionSlowdownFactor : 1.0f;
            float startup = parryStartupDuration * slowdownFactor;
            float active = parryActiveDuration;

            // 1. Фаза подготовки (Startup)
            if (startup > 0.005f)
            {
                yield return new WaitForSeconds(startup);
            }

            // 2. Активное окно парирования (золотое сияние щита вместо текстового спама)
            float timer = active;
            StartCoroutine(ShieldFlashRoutine(parryActiveColor, 0.25f));

            while (timer > 0f)
            {
                timer -= Time.deltaTime;
                yield return null;
            }

            // 3. Если время вышло и ни один удар не был спарирован — ПРОМАХ (Whiff)!
            IsParrying = false;
            _parryRoutine = null;

            TriggerParryWhiff(slowdownFactor);
        }

        /// <summary>
        /// Обработка промаха парирования (Whiff): списание стамины и стаггер игрока.
        /// При истощении стаггер длится в n раз дольше.
        /// </summary>
        private void TriggerParryWhiff(float slowdownFactor)
        {
            onParryWhiff?.Invoke();

            // Списываем стамину за промах парирования (с учетом анти-спама!)
            if (_stamina != null)
            {
                _stamina.ConsumeForAction("Parry", parryWhiffStaminaCost);
            }

            float staggerDuration = parryWhiffDuration * slowdownFactor;
            Debug.Log($"<color=orange>[PARRY WHIFF]</color> Промах парирования! Стаггер на {staggerDuration:F2}с.");
            CombatFloatingText.Spawn(transform.position + Vector3.up * 1.5f, "[ПРОМАХ!]", new Color(0.85f, 0.45f, 0.2f), 0.6f);

            if (_staggerRoutine != null) StopCoroutine(_staggerRoutine);
            _staggerRoutine = StartCoroutine(ParryStaggerRoutine(staggerDuration));
        }

        private IEnumerator ParryStaggerRoutine(float duration)
        {
            IsParryStaggered = true;

            // Останавливаем игрока
            if (_rb != null)
            {
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            }

            yield return new WaitForSeconds(duration);

            IsParryStaggered = false;
            _staggerRoutine = null;
        }

        /// <summary>
        /// Главный метод перехвата входящего удара из PlayerHealth2D.TakeHit.
        /// Возвращает true, если удар был перехвачен (спарирован или заблокирован).
        /// </summary>
        public bool TryInterceptAttack(AttackConfig attack, CombatZone hitZone, Vector2 hitPoint, Vector2 knockbackDirection, out bool hitAbsorbed, out float damageMultiplier, out Vector2 modifiedKnockback)
        {
            EnsureComponents();
            hitAbsorbed = false;
            damageMultiplier = 1.0f;
            modifiedKnockback = knockbackDirection;

            // 1. Проверка ПАРИРОВАНИЯ (Parry)
            if (IsParrying)
            {
                bool parryDirectionMatches = true;
                if (attack != null && attack.attacker != null)
                {
                    Vector2 toAttacker = (attack.attacker.transform.position - transform.position).normalized;
                    // Если атакующий находится с противоположной стороны от парирования (dot < -0.3f)
                    if (Vector2.Dot(toAttacker, _parryDirection) < -0.3f)
                    {
                        parryDirectionMatches = false;
                    }
                }

                if (parryDirectionMatches)
                {
                    OnSuccessfulParry(attack);
                    hitAbsorbed = true;
                    damageMultiplier = 0f;
                    modifiedKnockback = Vector2.zero;
                    return true;
                }
            }

            // 2. Проверка БЛОКИРОВАНИЯ (Block)
            if (IsBlocking)
            {
                // Направленный блок: защищает ТОЛЬКО сторону, куда смотрит игрок!
                float facing = GetCurrentFacing();
                float attackDirectionX = 0f;
                if (attack != null && attack.attacker != null)
                {
                    attackDirectionX = attack.attacker.transform.position.x - transform.position.x;
                }
                else if (Mathf.Abs(knockbackDirection.x) > 0.01f)
                {
                    // Отталкивание направлено ОТ атакующего, следовательно атакующий с противоположной стороны
                    attackDirectionX = -knockbackDirection.x;
                }
                else
                {
                    attackDirectionX = hitPoint.x - transform.position.x;
                }

                // Если атакующий находится позади взгляда игрока (удар со спины)
                bool isFromBehind = (attackDirectionX * facing) < -0.15f;
                if (isFromBehind)
                {
                    Debug.Log("<color=red>[BLOCK BYPASS]</color> Удар пришелся со спины! Блок спереди не защитил игрока.");
                    CombatFloatingText.Spawn(transform.position + Vector3.up * 1.3f, "[УДАР В СПИНУ!]", new Color(1f, 0.25f, 0.25f), 0.6f);
                    return false; // Блок пробит, удар проходит в здоровье!
                }

                bool hasStamina = _stamina != null && _stamina.CurrentStamina > 0.05f;

                if (hasStamina)
                {
                    // Блок со стаминой: 100% урона поглощено, списывается выносливость
                    _stamina.ConsumeForAction("Block", blockStaminaCost);
                    hitAbsorbed = true;
                    damageMultiplier = 0f;
                    modifiedKnockback = knockbackDirection * blockPushbackMultiplier;

                    TriggerBlockAbsorbFeedback(hitPoint);
                    return true;
                }
                else
                {
                    // Блок при 0 стамины (истощение): частичный урон (Chip Damage)
                    hitAbsorbed = false;
                    damageMultiplier = chipDamageMultiplier;
                    modifiedKnockback = knockbackDirection * 0.7f;

                    TriggerBlockChipFeedback(hitPoint);
                    return true;
                }
            }

            return false;
        }

        private void OnSuccessfulParry(AttackConfig attack)
        {
            if (_parryRoutine != null)
            {
                StopCoroutine(_parryRoutine);
                _parryRoutine = null;
            }
            IsParrying = false;
            IsParryStaggered = false;

            // 1. Полное (100%) восстановление выносливости и снятие истощения!
            if (_stamina != null)
            {
                _stamina.RestoreState(_stamina.MaxStamina, false);
            }

            // 2. Поиск и оглушение противника (Stun)
            GameObject attackerObj = attack?.attacker;
            if (attackerObj == null)
            {
                // Если атакующий не был явно указан, ищем ближайшего врага
                attackerObj = FindClosestHostileEntity();
            }

            if (attackerObj != null)
            {
                var enemy = attackerObj.GetComponent<EnemyAIController2D>() ?? attackerObj.GetComponentInParent<EnemyAIController2D>();
                if (enemy != null)
                {
                    enemy.Stun(parryStunDuration);
                }

                var dummy = attackerObj.GetComponent<CombatDummy2D>() ?? attackerObj.GetComponentInParent<CombatDummy2D>();
                if (dummy != null)
                {
                    dummy.Stun(parryStunDuration);
                }
            }

            // 3. Тактильный хитстоп
            if (_combat != null)
            {
                _combat.TriggerHitstop(parryHitstopDuration);
            }

            // 4. Всплывающий текст и логи
            CombatFloatingText.Spawn(transform.position + Vector3.up * 1.6f, "[ПАРИРОВАНИЕ!]", new Color(1f, 0.85f, 0.1f), 1.4f);
            Debug.Log("<color=gold><b>[PERFECT PARRY!]</b></color> Идеальное парирование! Стамина 100%, враг оглушен на 2с!");

            onParrySuccess?.Invoke();
        }

        private GameObject FindClosestHostileEntity()
        {
            var enemies = FindObjectsByType<EnemyAIController2D>();
            GameObject closest = null;
            float minDst = 6.0f;

            foreach (var e in enemies)
            {
                if (e == null || e.CurrentState == EnemyState.Dead) continue;
                float dst = Vector2.Distance(transform.position, e.transform.position);
                if (dst < minDst)
                {
                    minDst = dst;
                    closest = e.gameObject;
                }
            }

            if (closest != null) return closest;

            var dummies = FindObjectsByType<CombatDummy2D>();
            foreach (var d in dummies)
            {
                if (d == null || d.IsDead) continue;
                float dst = Vector2.Distance(transform.position, d.transform.position);
                if (dst < minDst)
                {
                    minDst = dst;
                    closest = d.gameObject;
                }
            }

            return closest;
        }

        private void TriggerBlockAbsorbFeedback(Vector2 hitPoint)
        {
            CombatFloatingText.Spawn(transform.position + Vector3.up * 1.5f, "[БЛОК]", new Color(0.2f, 0.85f, 1f), 0.8f);
            StartCoroutine(ShieldFlashRoutine(blockNormalColor, 0.12f));
        }

        private void TriggerBlockChipFeedback(Vector2 hitPoint)
        {
            CombatFloatingText.Spawn(transform.position + Vector3.up * 1.5f, "[БЛОК ПРОБИТ!]", new Color(1f, 0.35f, 0.2f), 1.0f);
            StartCoroutine(ShieldFlashRoutine(blockChipColor, 0.18f));
        }

        private IEnumerator ShieldFlashRoutine(Color flashColor, float duration)
        {
            if (_shieldRenderer != null)
            {
                _shieldRenderer.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0.55f);
            }
            if (_shieldOutlineRenderer != null)
            {
                _shieldOutlineRenderer.startColor = Color.white;
                _shieldOutlineRenderer.endColor = Color.white;
            }

            yield return new WaitForSeconds(duration);

            if (_shieldRenderer != null)
            {
                Color baseC = (_stamina != null && _stamina.CurrentStamina <= 0.05f) ? blockChipColor : blockNormalColor;
                _shieldRenderer.color = new Color(baseC.r, baseC.g, baseC.b, 0.22f);
            }
            if (_shieldOutlineRenderer != null)
            {
                Color baseC = (_stamina != null && _stamina.CurrentStamina <= 0.05f) ? blockChipColor : blockNormalColor;
                _shieldOutlineRenderer.startColor = baseC;
                _shieldOutlineRenderer.endColor = baseC;
            }
        }

        private void UpdateVisuals()
        {
            if (_shieldRootObj == null || !_shieldRootObj.activeSelf) return;

            float facing = GetCurrentFacing();
            _shieldRootObj.transform.localPosition = new Vector3(facing * 0.42f, 0.05f, 0f);

            // Пульсация щита при удержании с ориентацией в сторону взгляда
            float pulse = 0.9f + 0.12f * Mathf.Sin(Time.time * 7f);
            _shieldRootObj.transform.localScale = new Vector3(facing * pulse, pulse, 1f);

            Color targetColor = (_stamina != null && _stamina.CurrentStamina <= 0.05f) ? blockChipColor : blockNormalColor;
            if (_shieldOutlineRenderer != null && _shieldOutlineRenderer.startColor != Color.white)
            {
                _shieldOutlineRenderer.startColor = targetColor;
                _shieldOutlineRenderer.endColor = targetColor;
            }
        }

        private void CancelParryAndStagger()
        {
            if (_parryRoutine != null) { StopCoroutine(_parryRoutine); _parryRoutine = null; }
            if (_staggerRoutine != null) { StopCoroutine(_staggerRoutine); _staggerRoutine = null; }
            IsParrying = false;
            IsParryStaggered = false;
        }

        private static Vector2 GetMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                return UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            }
#endif
            try { return Input.mousePosition; } catch { return Vector2.zero; }
        }

        private static bool IsRmbDown()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                return UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame;
            }
#endif
            try { return Input.GetMouseButtonDown(1); } catch { return false; }
        }

        private static bool IsRmbHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                return UnityEngine.InputSystem.Mouse.current.rightButton.isPressed;
            }
#endif
            try { return Input.GetMouseButton(1); } catch { return false; }
        }

        private static bool IsRmbUp()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                return UnityEngine.InputSystem.Mouse.current.rightButton.wasReleasedThisFrame;
            }
#endif
            try { return Input.GetMouseButtonUp(1); } catch { return false; }
        }
    }
}
