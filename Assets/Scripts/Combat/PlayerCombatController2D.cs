using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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

        // Runtime State
        public CombatState CurrentState { get; private set; } = CombatState.Idle;
        public AttackConfig CurrentAttack { get; private set; }
        public float FacingDirection { get; private set; } = 1f;

        private Coroutine _attackRoutine;
        private readonly HashSet<Collider2D> _hitTargetsInCurrentSwing = new HashSet<Collider2D>();
        private readonly HashSet<IHurtboxTarget2D> _hitReceiversInCurrentSwing = new HashSet<IHurtboxTarget2D>();
        private readonly List<Collider2D> _overlapResults = new List<Collider2D>(16);
        private ContactFilter2D _contactFilter;

        public AttackConfig AttackRight => attackRight;
        public AttackConfig AttackUp => attackUp;
        public AttackConfig AttackDown => attackDown;
        public AttackConfig AttackLeft => attackLeft;

        private void Awake()
        {
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
        }

        private void Update()
        {
            UpdateFacingDirection();
        }

        private SpriteRenderer _sr;

        private void UpdateFacingDirection()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();

            // Если есть SpriteRenderer (PlayerController2D управляет flipX)
            if (_sr != null)
            {
                FacingDirection = _sr.flipX ? -1f : 1f;
                return;
            }

            // Fallback: определяем по локальному масштабу или скорости
            if (transform.localScale.x < -0.01f)
            {
                FacingDirection = -1f;
            }
            else if (transform.localScale.x > 0.01f)
            {
                FacingDirection = 1f;
            }
            else
            {
                var rb = GetComponent<Rigidbody2D>();
                if (rb != null && Mathf.Abs(rb.linearVelocity.x) > 0.1f)
                {
                    FacingDirection = Mathf.Sign(rb.linearVelocity.x);
                }
            }
        }

        public void SetFacingDirection(float dir)
        {
            FacingDirection = Mathf.Sign(dir);
        }

        private void OnWheelSwipeCompleted(Direction8 dir, Vector2 vector, float distance)
        {
            AttackDirection? mapped = MapDirection(dir);
            if (mapped.HasValue)
            {
                ExecuteAttack(mapped.Value);
            }
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

        public bool ExecuteAttack(AttackDirection dir)
        {
            if (CurrentState != CombatState.Idle) return false;

            AttackConfig attack = dir switch
            {
                AttackDirection.Right => attackRight,
                AttackDirection.Up => attackUp,
                AttackDirection.Down => attackDown,
                AttackDirection.Left => attackLeft,
                _ => null
            };

            if (attack == null) return false;

            if (_attackRoutine != null) StopCoroutine(_attackRoutine);
            _attackRoutine = StartCoroutine(AttackSequenceRoutine(attack));
            return true;
        }

        private IEnumerator AttackSequenceRoutine(AttackConfig attack)
        {
            CurrentAttack = attack;
            _hitTargetsInCurrentSwing.Clear();
            _hitReceiversInCurrentSwing.Clear();

            // 1. ФАЗА ЗАМАХА (STARTUP)
            CurrentState = CombatState.Startup;
            yield return new WaitForSeconds(attack.startupTime);

            // 2. АКТИВНАЯ ФАЗА (ACTIVE) — включение хитбокса и поиск попаданий
            CurrentState = CombatState.Active;
            float activeTimer = attack.activeTime;

            while (activeTimer > 0f)
            {
                Vector2 boxCenter = GetHitboxCenter(attack);
                Vector2 boxSize = attack.hitboxSize;

                // Отображаем неоновый хитбокс в игре
                if (visualizer != null)
                {
                    visualizer.ShowHitbox(boxCenter, boxSize, attack.hitboxColor);
                }

                // Физическая проверка пересечения хитбокса с Hurtbox целями
                CheckHitboxOverlap(attack, boxCenter, boxSize);

                activeTimer -= Time.deltaTime;
                yield return null;
            }

            // Прячем хитбокс
            if (visualizer != null)
            {
                visualizer.HideHitbox();
            }

            // 3. ФАЗА ВОССТАНОВЛЕНИЯ (RECOVERY)
            CurrentState = CombatState.Recovery;
            yield return new WaitForSeconds(attack.recoveryTime);

            CurrentState = CombatState.Idle;
            CurrentAttack = null;
            _attackRoutine = null;
        }

        private void CheckHitboxOverlap(AttackConfig attack, Vector2 center, Vector2 size)
        {
            _overlapResults.Clear();
            int count = Physics2D.OverlapBox(center, size, 0f, _contactFilter, _overlapResults);
            for (int i = 0; i < count; i++)
            {
                var col = _overlapResults[i];
                if (col == null || col.gameObject == gameObject) continue;
                if (_hitTargetsInCurrentSwing.Contains(col)) continue;

                // Проверяем наличие CombatHurtbox2D
                var hurtbox = col.GetComponent<CombatHurtbox2D>();
                if (hurtbox != null)
                {
                    // Проверяем, перекрывает ли атака зону данного hurtbox
                    if (attack.targetedZones.Overlaps(hurtbox.BodyZone))
                    {
                        var receiver = hurtbox.GetTargetReceiver();
                        if (receiver != null && _hitReceiversInCurrentSwing.Contains(receiver))
                        {
                            continue;
                        }

                        _hitTargetsInCurrentSwing.Add(col);
                        if (receiver != null) _hitReceiversInCurrentSwing.Add(receiver);

                        Vector2 knockbackDir = new Vector2(FacingDirection, 1f).normalized;
                        hurtbox.ReceiveHit(attack, center, knockbackDir);
                    }
                }
                else
                {
                    // Прямая цель с IHurtboxTarget2D
                    var target = col.GetComponent<IHurtboxTarget2D>() ?? col.GetComponentInParent<IHurtboxTarget2D>();
                    if (target != null)
                    {
                        if (_hitReceiversInCurrentSwing.Contains(target)) continue;

                        _hitTargetsInCurrentSwing.Add(col);
                        _hitReceiversInCurrentSwing.Add(target);

                        Vector2 knockbackDir = new Vector2(FacingDirection, 1f).normalized;
                        target.TakeHit(attack, attack.targetedZones, center, knockbackDir);
                    }
                }
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
