using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Combat.Player;

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

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class EnemyAIController2D : MonoBehaviour, IHurtboxTarget2D
    {
        [Header("--- Health & Defense ---")]
        [SerializeField] private float maxHealth = 120f;
        [SerializeField] private float currentHealth = 120f;
        [SerializeField] private float resetHealthDelay = 3.5f;

        [Header("--- Stamina & Poise System ---")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float currentStamina = 100f;
        [Tooltip("Множитель урона по стамине при обычных ударах")]
        [SerializeField] private float staminaDrainMultiplier = 1.0f;
        [Tooltip("Урон по стамине при успешной контратаке игрока")]
        [SerializeField] private float staminaCounterDrain = 55f;
        [Tooltip("Длительность оглушения при полном истощении стамины (сек)")]
        [SerializeField] private float staminaBreakStunDuration = 2.5f;
        [Tooltip("Задержка без получения урона до начала регенерации стамины (сек)")]
        [SerializeField] private float staminaRegenIdleDelay = 7.0f;
        [Tooltip("Скорость восстановления стамины в секунду")]
        [SerializeField] private float staminaRegenRate = 35f;

        [Header("--- Poise / Knockback Curve Settings ---")]
        [Tooltip("Множитель отталкивания при полной (100%) стамине (например 0.2 = высокая устойчивость)")]
        [SerializeField] private float knockbackMultAtFullStamina = 0.2f;

        [Tooltip("Множитель отталкивания при пустой (0%) стамине (например 1.0 = нормальное, 1.5+ = усиленное)")]
        [SerializeField] private float knockbackMultAtZeroStamina = 1.0f;

        [Tooltip("Множитель длительности стана от контратак при пустой стамине (1.0 = базовый, 1.8 = увеличенный)")]
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

        [Header("--- Movement ---")]
        [SerializeField] private float moveSpeed = 3.6f;

        [Header("--- Attack Parameters (Straight Mid Strike) ---")]
        [Tooltip("Дистанция до игрока для начала замаха")]
        [SerializeField] private float attackRange = 2.05f;

        [Tooltip("Длительность фазы замаха (телеграфа), во время которой открыто окно КОНТРАТАКИ")]
        [SerializeField] private float telegraphDuration = 0.75f;

        [Tooltip("Длительность активной фазы удара")]
        [SerializeField] private float activeStrikeDuration = 0.15f;

        [Tooltip("Длительность задержки восстановления после удара")]
        [SerializeField] private float recoveryDuration = 0.55f;

        [Tooltip("Урон прямого удара врага")]
        [SerializeField] private float attackDamage = 18f;

        [Tooltip("Импульс отталкивания игрока при попадании врага")]
        [SerializeField] private Vector2 knockbackToPlayer = new Vector2(7f, 3.5f);

        [Tooltip("Импульс выпада врага вперед при ударе")]
        [SerializeField] private float strikeLungeForce = 2.8f;

        [Tooltip("Смещение хитбокса прямого среднего удара")]
        [SerializeField] private Vector2 hitboxOffset = new Vector2(1.15f, 0.0f);

        [Tooltip("Размер хитбокса прямого среднего удара")]
        [SerializeField] private Vector2 hitboxSize = new Vector2(1.5f, 0.85f);

        [Header("--- Counter-Attack Mechanics ---")]
        [Tooltip("Множитель урона по врагу при успешной контратаке игрока")]
        [SerializeField] private float counterDamageMultiplier = 1.75f;

        [Tooltip("Длительность оглушения врага после контратаки")]
        [SerializeField] private float counterStunDuration = 1.0f;

        [Tooltip("Длительность тактильного хитстопа (паузы) при контратаке")]
        [SerializeField] private float counterHitstopDuration = 0.08f;

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
        public float FacingDirection { get; private set; } = -1f;
        public bool CanDie { get => canDie; set => canDie = value; }
        public bool IsDead => CurrentState == EnemyState.Dead;
        public float MaxStamina => maxStamina;
        public float CurrentStamina => currentStamina;

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
        private readonly List<Collider2D> _overlapResults = new List<Collider2D>(8);
        private ContactFilter2D _playerFilter;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<Collider2D>();
            _sr = GetComponent<SpriteRenderer>();

            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;

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

            // Регенерация стамины, если врага не трогали 7 секунд (и он не в стане)
            if (currentStamina < maxStamina && CurrentState != EnemyState.Stunned && Time.time - _lastHitTime >= staminaRegenIdleDelay)
            {
                currentStamina = Mathf.MoveTowards(currentStamina, maxStamina, staminaRegenRate * Time.deltaTime);
                if (staminaBar != null)
                {
                    staminaBar.SetStamina(currentStamina, maxStamina);
                }
            }

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

        private void UpdateIdle()
        {
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
            float dist = Vector2.Distance(transform.position, _playerTransform.position);

            // Игрок убежал слишком далеко
            if (dist > loseTargetRange)
            {
                CurrentState = EnemyState.Idle;
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                return;
            }

            // Дистанция атаки достигнута — начинаем замах
            if (dist <= attackRange)
            {
                StartTelegraphAttack();
                return;
            }

            // Преследуем игрока по горизонтали
            float dirX = Mathf.Sign(_playerTransform.position.x - transform.position.x);
            SetFacing(dirX);

            _rb.linearVelocity = new Vector2(dirX * moveSpeed, _rb.linearVelocity.y);
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
            Vector2 pos = transform.position;
            return new Vector2(pos.x + hitboxOffset.x * FacingDirection, pos.y + hitboxOffset.y);
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

            // 1. ФАЗА ТЕЛЕГРАФА (WINDUP) — ОТКРЫТО ОКНО КОНТРАТАКИ!
            CurrentState = EnemyState.TelegraphWindup;
            float timer = 0f;

            while (timer < telegraphDuration)
            {
                timer += Time.deltaTime;
                float progress = timer / telegraphDuration;

                // Пульсация и показ полупрозрачного предупреждающего хитбокса
                float pulse = 0.5f + 0.5f * Mathf.Sin(timer * 18f);
                Vector2 center = GetHitboxCenter();
                telegraphVisualizer.ShowTelegraph(center, hitboxSize, pulse);

                yield return null;
            }

            // 2. АКТИВНАЯ ФАЗА УДАРА (ACTIVE STRIKE)
            CurrentState = EnemyState.ActiveStrike;
            Vector2 strikeCenter = GetHitboxCenter();
            telegraphVisualizer.ShowActiveStrike(strikeCenter, hitboxSize);

            // Микро-рывок вперед при ударе
            _rb.linearVelocity = new Vector2(FacingDirection * strikeLungeForce, _rb.linearVelocity.y);

            // Проверка нанесения урона игроку
            CheckHitPlayer(strikeCenter, hitboxSize);

            yield return new WaitForSeconds(activeStrikeDuration);

            // 3. ФАЗА ВОССТАНОВЛЕНИЯ (RECOVERY)
            CurrentState = EnemyState.Recovery;
            telegraphVisualizer.HideHitbox();
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);

            yield return new WaitForSeconds(recoveryDuration);

            CurrentState = EnemyState.Chasing;
            _stateRoutine = null;
        }

        private void CheckHitPlayer(Vector2 center, Vector2 size)
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
                    // Создаем AttackConfig удара врага
                    var enemyAttack = new AttackConfig(
                        "Прямой средний удар врага",
                        CombatZone.Mid,
                        hitboxOffset,
                        hitboxSize,
                        telegraphDuration,
                        activeStrikeDuration,
                        recoveryDuration,
                        attackDamage,
                        knockbackToPlayer,
                        new Color(1f, 0.2f, 0.2f, 1f)
                    );

                    Vector2 knockbackDir = new Vector2(FacingDirection, 0.5f).normalized;
                    hurtbox.TakeHit(enemyAttack, CombatZone.Mid, center, knockbackDir);
                    break;
                }
            }
        }

        /// <summary>
        /// Реализация получения урона от атак игрока.
        /// Если враг находится в фазе замаха (TelegraphWindup) — СРАБАТЫВАЕТ КОНТРАТАКА!
        /// </summary>
        public void TakeHit(AttackConfig attack, CombatZone hitZone, Vector2 hitPoint, Vector2 knockbackDirection)
        {
            if (CurrentState == EnemyState.Dead) return;

            _lastHitTime = Time.time;

            if (CurrentState == EnemyState.TelegraphWindup)
            {
                // ==========================================
                // УСПЕШНАЯ КОНТРАТАКА (COUNTER-ATTACK)!
                // ==========================================
                TriggerCounterAttackSuccess(attack, knockbackDirection);
            }
            else
            {
                // Обычное получение урона
                TakeNormalHit(attack, knockbackDirection);
            }
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

        private void TriggerCounterAttackSuccess(AttackConfig attack, Vector2 knockbackDirection)
        {
            if (_stateRoutine != null) StopCoroutine(_stateRoutine);
            telegraphVisualizer.HideHitbox();

            // 1. Бонусный урон контратаки
            float baseDmg = attack != null ? attack.damage : 20f;
            float counterDmg = baseDmg * counterDamageMultiplier;

            if (!canDie)
            {
                currentHealth = Mathf.Max(1f, currentHealth - counterDmg);
            }
            else
            {
                currentHealth = Mathf.Max(0f, currentHealth - counterDmg);
            }

            // 2. Мощный импульс отталкивания с учетом кривой стойкости (стамина)
            Vector2 baseKb = (attack != null ? attack.knockbackForce : new Vector2(8f, 4f)) * 1.5f;
            Vector2 effectiveKb = CalculateEffectiveKnockback(baseKb);
            effectiveKb.x *= knockbackDirection.x;
            _rb.linearVelocity = effectiveKb;

            // 3. Списание стамины от контратаки
            DrainStamina(staminaCounterDrain);

            // 4. Всплывающий текст "КОНТРАТАКА!"
            telegraphVisualizer.ShowCounterAttackPopup(transform.position);

            // 5. Запускаем хитстоп через централизованный контроллер игрока
            var pCombat = _playerTransform != null ? _playerTransform.GetComponent<PlayerCombatController2D>() : null;
            if (pCombat != null)
            {
                pCombat.TriggerHitstop(counterHitstopDuration);
            }
            else
            {
                Time.timeScale = 1.0f;
            }

            Debug.Log($"<color=yellow>[КОНТРАТАКА!]</color> Удар врага ПРЕРВАН! Нанесен критический урон: {counterDmg:F1} (x{counterDamageMultiplier:F2}). HP: {currentHealth:F0}/{maxHealth:F0} | Стамина: {currentStamina:F0}/{maxStamina:F0}");

            // Проверка гибели от контратаки
            if (canDie && currentHealth <= 0f)
            {
                Die(knockbackDirection, true);
                return;
            }

            // Если стамина опустилась до 0 — наступает Stamina Break!
            if (currentStamina <= 0f)
            {
                TriggerStaminaBreak(knockbackDirection);
                return;
            }

            // 6. Обычный стан от контратаки (длительность масштабируется при низкой стамине)
            float effectiveStun = CalculateEffectiveStunDuration(counterStunDuration);
            CurrentState = EnemyState.Stunned;
            _stateRoutine = StartCoroutine(StunRoutine(effectiveStun, replenishStaminaAfter: false));
        }

        private void TakeNormalHit(AttackConfig attack, Vector2 knockbackDirection)
        {
            float dmg = attack != null ? attack.damage : 20f;

            if (!canDie)
            {
                currentHealth = Mathf.Max(1f, currentHealth - dmg);
            }
            else
            {
                currentHealth = Mathf.Max(0f, currentHealth - dmg);
            }

            if (attack != null)
            {
                Vector2 rawKb = new Vector2(knockbackDirection.x * attack.knockbackForce.x, attack.knockbackForce.y);
                Vector2 effectiveKb = CalculateEffectiveKnockback(rawKb);
                _rb.linearVelocity = effectiveKb;
            }

            // Списание стамины от обычного удара
            DrainStamina(dmg * staminaDrainMultiplier);

            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRoutine(normalColor, flashColor, 0.12f));

            Debug.Log($"[ENEMY HIT] Получен обычный удар: {dmg:F1} HP. Текущее HP: {currentHealth:F0}/{maxHealth:F0} | Стамина: {currentStamina:F0}/{maxStamina:F0}");

            // Проверка гибели от обычного удара
            if (canDie && currentHealth <= 0f)
            {
                Die(knockbackDirection, false);
                return;
            }

            // Если стамина опустилась до 0 — наступает Stamina Break!
            if (currentStamina <= 0f)
            {
                TriggerStaminaBreak(knockbackDirection);
                return;
            }

            // Если не были в атаке или стане — если стояли в Idle, сразу агримся на игрока
            if (CurrentState == EnemyState.Idle)
            {
                CurrentState = EnemyState.Chasing;
            }
        }

        private void TriggerStaminaBreak(Vector2 knockbackDirection)
        {
            if (CurrentState == EnemyState.Dead) return;

            if (_stateRoutine != null) { StopCoroutine(_stateRoutine); _stateRoutine = null; }
            if (telegraphVisualizer != null)
            {
                telegraphVisualizer.HideHitbox();
                telegraphVisualizer.ShowNoStaminaPopup(transform.position);
            }

            // Небольшой импульс ошеломления
            if (_rb != null)
            {
                float kbX = knockbackDirection.x != 0f ? knockbackDirection.x : -FacingDirection;
                _rb.linearVelocity = new Vector2(kbX * 2.6f, 1.2f);
            }

            CurrentState = EnemyState.Stunned;
            Debug.Log($"<color=orange><b>[НЕТ СТАМИНЫ!]</b></color> Враг истощен и оглушен на {staminaBreakStunDuration:F1}с!");
            _stateRoutine = StartCoroutine(StunRoutine(staminaBreakStunDuration, replenishStaminaAfter: true));
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

        private IEnumerator RespawnRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            Respawn();
            _respawnRoutine = null;
        }

        private IEnumerator StunRoutine(float duration, bool replenishStaminaAfter = false)
        {
            if (_sr != null) _sr.color = stunColor;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                // Мерцание оглушения
                if (_sr != null)
                {
                    float flash = Mathf.PingPong(elapsed * 8f, 1f);
                    _sr.color = Color.Lerp(stunColor, Color.white, flash);
                }
                yield return null;
            }

            if (_sr != null) _sr.color = normalColor;

            // Восстановление стамины после стана
            if (replenishStaminaAfter || currentStamina <= 0f)
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

        private void OnDisable()
        {
            Time.timeScale = 1.0f;
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

        private void OnDrawGizmosSelected()
        {
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
