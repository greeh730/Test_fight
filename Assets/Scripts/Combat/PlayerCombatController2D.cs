using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Combat.UI;

namespace Combat
{
    public enum CombatState
    {
        Idle,
        Startup,
        Active,
        Recovery
    }

    [Serializable]
    public class ComboStepEvent : UnityEvent<int, bool> { }

    [DisallowMultipleComponent]
    public class PlayerCombatController2D : MonoBehaviour
    {
        [Header("--- 4 Настраиваемые атаки (Хитбоксы и параметры) ---")]
        [Tooltip("Атака вправо: прямой выпад в корпус (Mid)")]
        [SerializeField] private AttackConfig attackRight = new AttackConfig(
            "Прямой выпад ▶",
            CombatZone.Mid,
            new Vector2(1.2f, 0.0f),
            new Vector2(1.4f, 0.8f),
            0.06f, 0.15f, 0.18f,
            20f,
            new Vector2(6.5f, 1.5f),
            new Color(1f, 0.85f, 0.15f, 0.8f) // Желтый (Mid)
        );

        [Tooltip("Атака вверх: восходящий рубящий / апперкот (Mid + High)")]
        [SerializeField] private AttackConfig attackUp = new AttackConfig(
            "Восходящий рубящий ▲",
            CombatZone.Mid | CombatZone.High,
            new Vector2(0.9f, 0.45f),
            new Vector2(1.3f, 1.4f),
            0.08f, 0.18f, 0.22f,
            25f,
            new Vector2(4.0f, 7.0f), // Подбрасывание вверх
            new Color(1f, 0.4f, 0.1f, 0.8f) // Оранжево-красный (Mid+High)
        );

        [Tooltip("Атака вниз: нижняя подсечка по ногам (Low)")]
        [SerializeField] private AttackConfig attackDown = new AttackConfig(
            "Нижняя подсечка ▼",
            CombatZone.Low,
            new Vector2(1.0f, -0.45f),
            new Vector2(1.5f, 0.6f),
            0.07f, 0.16f, 0.20f,
            18f,
            new Vector2(5.5f, 0.5f),
            new Color(0.15f, 0.85f, 1f, 0.8f) // Голубой (Low)
        );

        [Tooltip("Атака влево: круговой сокрушающий замах (High + Mid)")]
        [SerializeField] private AttackConfig attackLeft = new AttackConfig(
            "Круговой замах ◀",
            CombatZone.High | CombatZone.Mid,
            new Vector2(1.3f, 0.2f),
            new Vector2(1.7f, 1.2f),
            0.12f, 0.20f, 0.26f,
            35f,
            new Vector2(9.0f, 3.0f), // Мощное отталкивание
            new Color(1f, 0.15f, 0.25f, 0.8f) // Красный (High+Mid)
        );

        [Header("--- Combo System Settings ---")]
        [Tooltip("Максимальное количество ударов в одной цепочке комбо")]
        [SerializeField] private int maxComboSteps = 3;

        [Tooltip("Время бездействия (в секундах), после которого комбо сбрасывается")]
        [SerializeField] private float comboResetTime = 1.2f;

        [Tooltip("Множитель времени замаха со 2-го шага комбо (меньше 1 = быстрее)")]
        [Range(0.3f, 1f)]
        [SerializeField] private float comboStartupMultiplier = 0.65f;

        [Tooltip("Множитель урона для завершающего удара комбо (Finisher)")]
        [Range(1f, 3f)]
        [SerializeField] private float finisherDamageMultiplier = 1.4f;

        [Tooltip("Множитель силы отталкивания для завершающего удара комбо")]
        [Range(1f, 3f)]
        [SerializeField] private float finisherKnockbackMultiplier = 1.5f;

        [Tooltip("Сила импульса выпада вперед при ударах комбо")]
        [SerializeField] private float comboLungeForce = 3.5f;

        [Header("--- Input Buffer & Cancel Windows ---")]
        [Tooltip("Длительность окна буферизации ввода (в секундах)")]
        [SerializeField] private float inputBufferDuration = 0.35f;

        [Tooltip("Доля времени фазы Recovery, после которой разрешена отмена следующим ударом")]
        [Range(0f, 1f)]
        [SerializeField] private float recoveryCancelThreshold = 0.3f;

        [Tooltip("Длительность тактильной микро-паузы (Hitstop) при попадании")]
        [Range(0f, 0.15f)]
        [SerializeField] private float hitstopDuration = 0.045f;

        [Header("--- Target Layer Mask ---")]
        [Tooltip("Слои, на которых ищутся враги и мишени")]
        [SerializeField] private LayerMask targetLayers = ~0;

        [Header("--- Input & Wheel Binding ---")]
        [SerializeField] private VectorWheelController vectorWheel;
        [Tooltip("Квантовать диагональные свайпы на 4 ближайших направления")]
        [SerializeField] private bool quantizeDiagonalsTo4Cardinal = true;

        [Header("--- Visualizer ---")]
        [SerializeField] private HitboxVisualizer2D visualizer;
        [SerializeField] private bool showAllHitboxesInEditorGizmos = true;

        [Header("--- Combo Events ---")]
        public ComboStepEvent onComboStepChanged;

        // Runtime State
        public CombatState CurrentState { get; private set; } = CombatState.Idle;
        public AttackConfig CurrentAttack { get; private set; }
        public float FacingDirection { get; private set; } = 1f;
        public int CurrentComboStep { get; private set; } = 1;
        public bool IsFinisher => CurrentComboStep >= maxComboSteps;
        public bool CanCancelIntoCombo => _canCancelIntoCombo;

        private Coroutine _attackRoutine;
        private Coroutine _hitstopRoutine;
        private readonly HashSet<Collider2D> _hitTargetsInCurrentSwing = new HashSet<Collider2D>();
        private readonly HashSet<IHurtboxTarget2D> _hitReceiversInCurrentSwing = new HashSet<IHurtboxTarget2D>();
        private readonly List<Collider2D> _overlapResults = new List<Collider2D>(16);
        private ContactFilter2D _contactFilter;

        private AttackDirection? _bufferedAttack;
        private float _bufferedAttackTime = -10f;
        private float _lastAttackStartTime = -10f;
        private float _lastAttackFinishTime = -10f;
        private bool _canCancelIntoCombo;
        private bool _hasHitTargetInCurrentAttack;
        private bool _lastWasFinisher;
        private Rigidbody2D _rb;
        private SpriteRenderer _sr;

        public AttackConfig AttackRight => attackRight;
        public AttackConfig AttackUp => attackUp;
        public AttackConfig AttackDown => attackDown;
        public AttackConfig AttackLeft => attackLeft;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _sr = GetComponent<SpriteRenderer>();

            _contactFilter = new ContactFilter2D();
            _contactFilter.SetLayerMask(targetLayers);
            _contactFilter.useTriggers = true;

            if (visualizer == null)
            {
                visualizer = GetComponent<HitboxVisualizer2D>();
                if (visualizer == null) visualizer = gameObject.AddComponent<HitboxVisualizer2D>();
            }

            if (vectorWheel == null)
            {
                vectorWheel = FindAnyObjectByType<VectorWheelController>();
            }
        }

        private void OnEnable()
        {
            if (vectorWheel != null)
            {
                vectorWheel.onSwipeCompleted.AddListener(OnWheelSwipeCompleted);
            }
        }

        private void OnDisable()
        {
            if (vectorWheel != null)
            {
                vectorWheel.onSwipeCompleted.RemoveListener(OnWheelSwipeCompleted);
            }
            Time.timeScale = 1f;
        }

        private void Update()
        {
            UpdateFacingDirection();
            UpdateComboTimers();
            CheckInputBuffer();
        }

        private void UpdateFacingDirection()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();

            if (_sr != null)
            {
                FacingDirection = _sr.flipX ? -1f : 1f;
                return;
            }

            if (transform.localScale.x < -0.01f)
            {
                FacingDirection = -1f;
            }
            else if (transform.localScale.x > 0.01f)
            {
                FacingDirection = 1f;
            }
            else if (_rb != null && Mathf.Abs(_rb.linearVelocity.x) > 0.1f)
            {
                FacingDirection = Mathf.Sign(_rb.linearVelocity.x);
            }
        }

        public void SetFacingDirection(float dir)
        {
            FacingDirection = Mathf.Sign(dir);
        }

        private void UpdateComboTimers()
        {
            // Сброс комбо при долгом бездействии в Idle
            if (CurrentComboStep > 1 && CurrentState == CombatState.Idle && Time.time - _lastAttackStartTime > comboResetTime)
            {
                ResetCombo();
            }

            // Устаревание буфера ввода
            if (_bufferedAttack.HasValue && Time.time - _bufferedAttackTime > inputBufferDuration)
            {
                _bufferedAttack = null;
            }
        }

        private void CheckInputBuffer()
        {
            if (!_bufferedAttack.HasValue) return;

            if (CanExecuteAttackNow())
            {
                var dir = _bufferedAttack.Value;
                ClearBuffer();
                ExecuteAttack(dir);
            }
        }

        public bool CanExecuteAttackNow()
        {
            if (CurrentState == CombatState.Idle) return true;
            if (_canCancelIntoCombo) return true;
            return false;
        }

        private void ClearBuffer()
        {
            _bufferedAttack = null;
            _bufferedAttackTime = -10f;
        }

        public void ResetCombo()
        {
            CurrentComboStep = 1;
            _lastWasFinisher = false;
            onComboStepChanged?.Invoke(CurrentComboStep, false);
        }

        private void OnWheelSwipeCompleted(Direction8 dir, Vector2 vector, float distance)
        {
            AttackDirection? mapped = MapDirection(dir);
            if (!mapped.HasValue) return;

            TryAttackOrBuffer(mapped.Value);
        }

        public bool TryAttackOrBuffer(AttackDirection dir)
        {
            if (CanExecuteAttackNow())
            {
                ClearBuffer();
                return ExecuteAttack(dir);
            }

            _bufferedAttack = dir;
            _bufferedAttackTime = Time.time;
            return false;
        }

        public AttackDirection? MapDirection(Direction8 dir)
        {
            if (quantizeDiagonalsTo4Cardinal)
            {
                return dir switch
                {
                    Direction8.Right or Direction8.DownRight => AttackDirection.Right,
                    Direction8.Up or Direction8.UpRight => AttackDirection.Up,
                    Direction8.Left or Direction8.UpLeft => AttackDirection.Left,
                    Direction8.Down or Direction8.DownLeft => AttackDirection.Down,
                    _ => null
                };
            }

            return dir switch
            {
                Direction8.Right => AttackDirection.Right,
                Direction8.Up => AttackDirection.Up,
                Direction8.Left => AttackDirection.Left,
                Direction8.Down => AttackDirection.Down,
                _ => null
            };
        }

        private void PrepareNextComboStep(bool isChaining)
        {
            if (isChaining)
            {
                CurrentComboStep = (CurrentComboStep >= maxComboSteps) ? 1 : CurrentComboStep + 1;
            }
            else
            {
                if (Time.time - _lastAttackStartTime <= comboResetTime && !_lastWasFinisher && _lastAttackStartTime > 0f)
                {
                    CurrentComboStep = (CurrentComboStep >= maxComboSteps) ? 1 : CurrentComboStep + 1;
                }
                else
                {
                    CurrentComboStep = 1;
                }
            }
            _lastAttackStartTime = Time.time;
        }

        public bool ExecuteAttack(AttackDirection dir)
        {
            if (!CanExecuteAttackNow()) return false;

            AttackConfig attack = dir switch
            {
                AttackDirection.Right => attackRight,
                AttackDirection.Up => attackUp,
                AttackDirection.Down => attackDown,
                AttackDirection.Left => attackLeft,
                _ => null
            };

            if (attack == null) return false;

            bool isChaining = _attackRoutine != null;

            if (_attackRoutine != null)
            {
                StopCoroutine(_attackRoutine);
                if (visualizer != null) visualizer.HideHitbox();
            }

            PrepareNextComboStep(isChaining);

            _attackRoutine = StartCoroutine(AttackSequenceRoutine(attack));
            return true;
        }

        private IEnumerator AttackSequenceRoutine(AttackConfig attack)
        {
            CurrentAttack = attack;
            _hitTargetsInCurrentSwing.Clear();
            _hitReceiversInCurrentSwing.Clear();
            _canCancelIntoCombo = false;
            _hasHitTargetInCurrentAttack = false;

            int thisAttackStep = CurrentComboStep;
            bool isFinisher = thisAttackStep >= maxComboSteps;

            if (vectorWheel != null)
            {
                vectorWheel.SetAttackPlaqueWithCombo(attack.attackName, thisAttackStep, isFinisher);
            }
            onComboStepChanged?.Invoke(thisAttackStep, isFinisher);

            // 1. ФАЗА ЗАМАХА (STARTUP) — удары в комбо ускоряются!
            CurrentState = CombatState.Startup;
            float startup = thisAttackStep > 1 ? attack.startupTime * comboStartupMultiplier : attack.startupTime;
            yield return new WaitForSeconds(startup);

            // 2. АКТИВНАЯ ФАЗА (ACTIVE)
            CurrentState = CombatState.Active;
            float activeTimer = attack.activeTime;

            // Микро-выпад вперед при ударе для динамики и сокращения дистанции
            ApplyComboLunge();

            while (activeTimer > 0f)
            {
                Vector2 boxCenter = GetHitboxCenter(attack);
                Vector2 boxSize = attack.hitboxSize;

                if (visualizer != null)
                {
                    visualizer.ShowHitbox(boxCenter, boxSize, attack.hitboxColor, isFinisher);
                }

                CheckHitboxOverlap(attack, boxCenter, boxSize, isFinisher);

                activeTimer -= Time.deltaTime;
                yield return null;
            }

            if (visualizer != null)
            {
                visualizer.HideHitbox();
            }

            // 3. ФАЗА ВОССТАНОВЛЕНИЯ (RECOVERY)
            CurrentState = CombatState.Recovery;
            float recovery = attack.recoveryTime;
            float cancelOpenTime = recovery * recoveryCancelThreshold;
            float recoveryTimer = 0f;

            while (recoveryTimer < recovery)
            {
                recoveryTimer += Time.deltaTime;

                if (recoveryTimer >= cancelOpenTime || _hasHitTargetInCurrentAttack)
                {
                    _canCancelIntoCombo = true;

                    if (_bufferedAttack.HasValue)
                    {
                        _lastAttackFinishTime = Time.time;
                        _lastWasFinisher = isFinisher;
                        var nextDir = _bufferedAttack.Value;
                        ClearBuffer();
                        ExecuteAttack(nextDir);
                        yield break;
                    }
                }

                yield return null;
            }

            _lastAttackFinishTime = Time.time;
            _lastWasFinisher = isFinisher;
            _canCancelIntoCombo = false;
            CurrentState = CombatState.Idle;
            CurrentAttack = null;
            _attackRoutine = null;
        }

        private void ApplyComboLunge()
        {
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            if (_rb != null && comboLungeForce > 0.05f)
            {
                _rb.linearVelocity = new Vector2(FacingDirection * comboLungeForce, _rb.linearVelocity.y);
            }
        }

        private void TriggerHitstop()
        {
            if (hitstopDuration <= 0.005f) return;
            if (_hitstopRoutine != null) StopCoroutine(_hitstopRoutine);
            _hitstopRoutine = StartCoroutine(HitstopRoutine());
        }

        private IEnumerator HitstopRoutine()
        {
            float prevScale = Time.timeScale;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(hitstopDuration);
            Time.timeScale = prevScale > 0.01f ? prevScale : 1f;
            _hitstopRoutine = null;
        }

        private void CheckHitboxOverlap(AttackConfig attack, Vector2 center, Vector2 size, bool isFinisher)
        {
            _overlapResults.Clear();
            int count = Physics2D.OverlapBox(center, size, 0f, _contactFilter, _overlapResults);

            AttackConfig effectiveAttack = isFinisher
                ? new AttackConfig(
                    attack.attackName + " [ФИНИШЕР!]",
                    attack.targetedZones,
                    attack.hitboxOffset,
                    attack.hitboxSize,
                    attack.startupTime,
                    attack.activeTime,
                    attack.recoveryTime,
                    attack.damage * finisherDamageMultiplier,
                    attack.knockbackForce * finisherKnockbackMultiplier,
                    attack.hitboxColor
                )
                : attack;

            for (int i = 0; i < count; i++)
            {
                var col = _overlapResults[i];
                if (col == null || col.gameObject == gameObject) continue;
                if (_hitTargetsInCurrentSwing.Contains(col)) continue;

                var hurtbox = col.GetComponent<CombatHurtbox2D>();
                if (hurtbox != null)
                {
                    if (effectiveAttack.targetedZones.Overlaps(hurtbox.BodyZone))
                    {
                        var receiver = hurtbox.GetTargetReceiver();
                        if (receiver != null && _hitReceiversInCurrentSwing.Contains(receiver))
                        {
                            continue;
                        }

                        _hitTargetsInCurrentSwing.Add(col);
                        if (receiver != null) _hitReceiversInCurrentSwing.Add(receiver);

                        OnTargetHitSuccess();

                        Vector2 knockbackDir = new Vector2(FacingDirection, 1f).normalized;
                        hurtbox.ReceiveHit(effectiveAttack, center, knockbackDir);
                    }
                }
                else
                {
                    var target = col.GetComponent<IHurtboxTarget2D>() ?? col.GetComponentInParent<IHurtboxTarget2D>();
                    if (target != null)
                    {
                        if (_hitReceiversInCurrentSwing.Contains(target)) continue;

                        _hitTargetsInCurrentSwing.Add(col);
                        _hitReceiversInCurrentSwing.Add(target);

                        OnTargetHitSuccess();

                        Vector2 knockbackDir = new Vector2(FacingDirection, 1f).normalized;
                        target.TakeHit(effectiveAttack, effectiveAttack.targetedZones, center, knockbackDir);
                    }
                }
            }
        }

        private void OnTargetHitSuccess()
        {
            if (!_hasHitTargetInCurrentAttack)
            {
                _hasHitTargetInCurrentAttack = true;
                _canCancelIntoCombo = true;
                TriggerHitstop();
            }
        }

        public Vector2 GetHitboxCenter(AttackConfig attack)
        {
            Vector2 playerPos = transform.position;
            return new Vector2(
                playerPos.x + attack.hitboxOffset.x * FacingDirection,
                playerPos.y + attack.hitboxOffset.y
            );
        }

        private void OnDrawGizmosSelected()
        {
            if (showAllHitboxesInEditorGizmos)
            {
                DrawAttackGizmo(attackRight, "Right");
                DrawAttackGizmo(attackUp, "Up");
                DrawAttackGizmo(attackDown, "Down");
                DrawAttackGizmo(attackLeft, "Left");
            }
            else if (CurrentAttack != null)
            {
                DrawAttackGizmo(CurrentAttack, CurrentAttack.attackName);
            }
        }

        private void DrawAttackGizmo(AttackConfig attack, string label)
        {
            if (attack == null) return;
            float dir = Application.isPlaying ? FacingDirection : (transform.localScale.x < 0 ? -1f : 1f);
            Vector2 pos = (Vector2)transform.position + new Vector2(attack.hitboxOffset.x * dir, attack.hitboxOffset.y);

            Color c = attack.hitboxColor;
            Gizmos.color = c;
            Gizmos.DrawWireCube(new Vector3(pos.x, pos.y, 0f), new Vector3(attack.hitboxSize.x, attack.hitboxSize.y, 0.1f));

            Gizmos.color = new Color(c.r, c.g, c.b, 0.18f);
            Gizmos.DrawCube(new Vector3(pos.x, pos.y, 0f), new Vector3(attack.hitboxSize.x, attack.hitboxSize.y, 0.05f));
        }
    }
}
