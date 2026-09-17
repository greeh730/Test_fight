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
        Stunned          // Оглушен после успешной контратаки игрока
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
        [SerializeField] private float counterStunDuration = 1.5f;

        [Tooltip("Длительность тактильного хитстопа (паузы) при контратаке")]
        [SerializeField] private float counterHitstopDuration = 0.12f;

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

        private Transform _playerTransform;
        private Rigidbody2D _rb;
        private SpriteRenderer _sr;
        private Coroutine _stateRoutine;
        private Coroutine _flashRoutine;
        private Coroutine _hitstopRoutine;

        private Vector3 _faceBaseLocalPos = new Vector3(0.16f, 0.14f, 0f);
        private Vector3 _faceBaseLocalScale = new Vector3(0.45f, 0.26f, 1f);
        private float _lastHitTime;
        private readonly List<Collider2D> _overlapResults = new List<Collider2D>(8);
        private ContactFilter2D _playerFilter;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _sr = GetComponent<SpriteRenderer>();

            if (_sr != null) _sr.color = normalColor;
            currentHealth = maxHealth;

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
            if (player != null) _playerTransform = player.transform;
        }

        private void Update()
        {
            if (_playerTransform == null)
            {
                FindPlayer();
                return;
            }

            // Авто-восстановление здоровья после долгого простоя вне боя
            if (currentHealth < maxHealth && CurrentState == EnemyState.Idle && Time.time - _lastHitTime > resetHealthDelay)
            {
                currentHealth = maxHealth;
                if (_sr != null) _sr.color = normalColor;
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

        private void TriggerCounterAttackSuccess(AttackConfig attack, Vector2 knockbackDirection)
        {
            if (_stateRoutine != null) StopCoroutine(_stateRoutine);
            telegraphVisualizer.HideHitbox();

            // 1. Бонусный урон контратаки
            float baseDmg = attack != null ? attack.damage : 20f;
            float counterDmg = baseDmg * counterDamageMultiplier;
            currentHealth = Mathf.Max(0f, currentHealth - counterDmg);

            // 2. Мощный импульс отталкивания
            Vector2 kb = (attack != null ? attack.knockbackForce : new Vector2(8f, 4f)) * 1.5f;
            kb.x *= knockbackDirection.x;
            _rb.linearVelocity = kb;

            // 3. Всплывающий текст "КОНТРАТАКА!"
            telegraphVisualizer.ShowCounterAttackPopup(transform.position);

            // 4. Сокрушительный хитстоп
            TriggerHitstop(counterHitstopDuration);

            // 5. Переход в состояние оглушения (Stunned)
            CurrentState = EnemyState.Stunned;
            _stateRoutine = StartCoroutine(StunRoutine(counterStunDuration));

            Debug.Log($"<color=yellow>[КОНТРАТАКА!]</color> Удар врага ПРЕРВАН! Нанесен критический урон: {counterDmg:F1} (x{counterDamageMultiplier:F2}). HP врага: {currentHealth:F0}/{maxHealth:F0}");
        }

        private void TakeNormalHit(AttackConfig attack, Vector2 knockbackDirection)
        {
            float dmg = attack != null ? attack.damage : 20f;
            currentHealth = Mathf.Max(0f, currentHealth - dmg);

            if (attack != null)
            {
                Vector2 kb = new Vector2(knockbackDirection.x * attack.knockbackForce.x, attack.knockbackForce.y);
                _rb.linearVelocity = kb;
            }

            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRoutine(normalColor, flashColor, 0.12f));

            Debug.Log($"[ENEMY HIT] Получен обычный удар: {dmg:F1} HP. Текущее HP: {currentHealth:F0}/{maxHealth:F0}");

            // Если не были в атаке или стане — если стояли в Idle, сразу агримся на игрока
            if (CurrentState == EnemyState.Idle)
            {
                CurrentState = EnemyState.Chasing;
            }
        }

        private IEnumerator StunRoutine(float duration)
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
            CurrentState = EnemyState.Chasing;
            _stateRoutine = null;
        }

        private void TriggerHitstop(float duration)
        {
            if (_hitstopRoutine != null) StopCoroutine(_hitstopRoutine);
            _hitstopRoutine = StartCoroutine(HitstopRoutine(duration));
        }

        private IEnumerator HitstopRoutine(float duration)
        {
            float prevScale = Time.timeScale;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = prevScale > 0.01f ? prevScale : 1f;
            _hitstopRoutine = null;
        }

        private IEnumerator FlashRoutine(Color defaultCol, Color flashCol, float duration)
        {
            if (_sr == null) yield break;
            _sr.color = flashCol;
            yield return new WaitForSeconds(duration);
            if (CurrentState != EnemyState.Stunned)
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
