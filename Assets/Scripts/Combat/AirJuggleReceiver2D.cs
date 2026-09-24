using System;
using System.Collections;
using UnityEngine;
using Combat.Common;

namespace Combat
{
    /// <summary>
    /// Состояние воздушного джаггла / комбо-зависания.
    /// </summary>
    public enum AirJuggleState
    {
        None,               // На земле / обычный режим
        Ascending,          // Взлетает вверх после лаунчера
        ApexFrozen,         // Замер в пике подкидывания (Apex Hang)
        JuggleSuspended,    // Замер в воздухе от удара игрока (удлинённое зависание)
        Falling             // Падает вниз после окончания зависания
    }

    /// <summary>
    /// Компонент воздушного джаггла (Air Juggle System) для Abyss of Emotions.
    /// Отвечает за:
    /// 1. Подкидывание в воздух от атак-лаунчеров.
    /// 2. Замирание врага в наивысшей точке траектории (Apex Freeze).
    /// 3. Продление зависания при получении воздушных ударов от подпрыгнувшего игрока.
    /// 4. Корректное восстановление физики и гравитации при падении и приземлении.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public class AirJuggleReceiver2D : MonoBehaviour
    {
        [Header("--- Apex Freeze Settings (Замирание в пике) ---")]
        [Tooltip("Длительность зависания в пике подкидывания (сек)")]
        [SerializeField] private float apexFreezeDuration = 0.45f;

        [Tooltip("Порог вертикальной скорости для фиксации пика подкидывания")]
        [SerializeField] private float apexVelocityThreshold = 0.6f;

        [Tooltip("Минимальная стартовая вертикальная скорость для активации лаунчера")]
        [SerializeField] private float minLaunchVelocityY = 5.0f;

        [Header("--- Juggle Combo Settings (Воздушные удары) ---")]
        [Tooltip("Длительность зависания при воздушном ударе игрока (чуть дольше, чем в пике)")]
        [SerializeField] private float airJuggleFreezeDuration = 0.68f;

        [Tooltip("Небольшой микро-подброс вверх при каждом воздушном ударе, чтобы удерживать цель на уровне глаз")]
        [SerializeField] private float juggleLiftPerHit = 1.6f;

        [Tooltip("Максимальное количество воздушных ударов в одной цепочке джаггла")]
        [SerializeField] private int maxJuggleHits = 5;

        [Tooltip("Слои, считающиеся землей для завершения джаггла при приземлении")]
        [SerializeField] private LayerMask groundLayer = ~0;

        [Header("--- Visual & Feedback ---")]
        [Tooltip("Лёгкое вертикальное колебание во время зависания в воздухе")]
        [SerializeField] private bool enableHoverBob = true;
        [SerializeField] private float hoverBobFrequency = 14f;
        [SerializeField] private float hoverBobAmplitude = 0.05f;

        // Компоненты
        private Rigidbody2D _rb;
        private SpriteRenderer _sr;
        private Collider2D _col;

        // Состояние
        public AirJuggleState CurrentState { get; private set; } = AirJuggleState.None;
        public bool IsAirborne => CurrentState != AirJuggleState.None;
        public bool IsFrozen => CurrentState == AirJuggleState.ApexFrozen || CurrentState == AirJuggleState.JuggleSuspended;
        public int JuggleHitCount { get; private set; } = 0;

        private float _freezeTimer = 0f;
        private float _originalGravityScale = 1.0f;
        private Vector2 _frozenPosition;
        private float _frozenElapsed = 0f;
        private bool _wasAscending = false;
        private float _launchTime = 0f;
        private Coroutine _flashRoutine;

        public event Action OnApexReached;
        public event Action<int> OnAirJuggleHit;
        public event Action OnJuggleEnded;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _sr = GetComponent<SpriteRenderer>();
            _col = GetComponent<Collider2D>();

            if (_rb != null)
            {
                _originalGravityScale = _rb.gravityScale > 0.01f ? _rb.gravityScale : 1.0f;
            }
        }

        /// <summary>
        /// Запускает подкидывание объекта в воздух (Launcher).
        /// </summary>
        public void Launch(Vector2 launchVelocity)
        {
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            if (_rb == null) return;

            // Если уже были в заморозке, сбрасываем старую гравитацию перед новым импульсом
            if (IsFrozen)
            {
                _rb.gravityScale = _originalGravityScale;
            }

            JuggleHitCount = 0;
            _wasAscending = true;
            _launchTime = Time.time;
            CurrentState = AirJuggleState.Ascending;

            // Гарантируем достаточную высоту подкидывания для возможности подпрыгнуть
            float vy = Mathf.Max(launchVelocity.y, minLaunchVelocityY);
            _rb.linearVelocity = new Vector2(launchVelocity.x, vy);

            CombatFloatingText.ShowLauncher(transform.position);
        }

        private void FixedUpdate()
        {
            if (CurrentState == AirJuggleState.None) return;

            // 1. ФАЗА ПОДЪЕМА: Отслеживаем достижение пика подкидывания
            if (CurrentState == AirJuggleState.Ascending)
            {
                // Защита: должны были хоть немного подняться
                if (_rb.linearVelocity.y > 1.0f)
                {
                    _wasAscending = true;
                }

                // Когда вертикальная скорость замедляется около нуля — это пик траектории (Apex)!
                if (_wasAscending && _rb.linearVelocity.y <= apexVelocityThreshold && (Time.time - _launchTime) > 0.12f)
                {
                    EnterApexFreeze();
                    return;
                }
            }

            // 2. ФАЗА ЗАВИСАНИЯ (В пике или после удара игрока)
            if (IsFrozen)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.gravityScale = 0f;

                // Легкое визуальное парение
                _frozenElapsed += Time.fixedDeltaTime;
                if (enableHoverBob)
                {
                    float yOffset = Mathf.Sin(_frozenElapsed * hoverBobFrequency) * hoverBobAmplitude;
                    _rb.MovePosition(new Vector2(_frozenPosition.x, _frozenPosition.y + yOffset));
                }
                else
                {
                    _rb.MovePosition(_frozenPosition);
                }

                _freezeTimer -= Time.fixedDeltaTime;
                if (_freezeTimer <= 0f)
                {
                    ExitFreezeIntoFall();
                }
                return;
            }

            // 3. ФАЗА ПАДЕНИЯ: Проверяем приземление
            if (CurrentState == AirJuggleState.Falling)
            {
                // Если падаем и коснулись земли
                if (_rb.linearVelocity.y <= 0.1f && CheckGrounded())
                {
                    EndJuggle();
                }
            }
        }

        /// <summary>
        /// Вход в режим замирания в верхней точке подкидывания.
        /// </summary>
        private void EnterApexFreeze()
        {
            CurrentState = AirJuggleState.ApexFrozen;
            _freezeTimer = apexFreezeDuration;
            _frozenPosition = transform.position;
            _frozenElapsed = 0f;

            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.gravityScale = 0f;
            }

            CombatFloatingText.ShowApexHang(transform.position);
            TriggerVisualShimmer(new Color(1f, 0.95f, 0.4f, 1f));

            OnApexReached?.Invoke();
        }

        /// <summary>
        /// Вызывается при попадании ударом игрока по цели в воздухе (Apex или Juggle).
        /// Продлевает зависание на увеличенное время (airJuggleFreezeDuration).
        /// </summary>
        public bool OnAirHit(AttackConfig attack, bool isPlayerAirborne)
        {
            if (!IsAirborne && !IsFrozen)
            {
                // Если объект находится над землей (хотя бы 0.6м), также разрешаем начать джаггл
                if (!CheckGrounded())
                {
                    CurrentState = AirJuggleState.ApexFrozen;
                }
                else
                {
                    return false;
                }
            }

            JuggleHitCount++;

            // Если превысили лимит комбо — отправляем врага вниз
            if (JuggleHitCount > maxJuggleHits)
            {
                ExitFreezeIntoFall(applyDownwardSlam: true);
                return true;
            }

            // Продлеваем зависание: враг замирает чуть дольше в полете!
            CurrentState = AirJuggleState.JuggleSuspended;
            _freezeTimer = airJuggleFreezeDuration;
            _frozenElapsed = 0f;

            // Легкий микро-подброс вверх для фиксации на высоте
            Vector3 curPos = transform.position;
            _frozenPosition = new Vector2(curPos.x, curPos.y + juggleLiftPerHit * 0.12f);

            if (_rb != null)
            {
                _rb.linearVelocity = new Vector2(0f, juggleLiftPerHit);
                _rb.gravityScale = 0f;
            }

            // Визуальный отклик
            CombatFloatingText.ShowAirJuggle(transform.position, JuggleHitCount);
            TriggerVisualShimmer(new Color(0.3f, 0.9f, 1f, 1f));

            OnAirJuggleHit?.Invoke(JuggleHitCount);
            return true;
        }

        private void ExitFreezeIntoFall(bool applyDownwardSlam = false)
        {
            CurrentState = AirJuggleState.Falling;
            if (_rb != null)
            {
                _rb.gravityScale = _originalGravityScale;
                if (applyDownwardSlam)
                {
                    _rb.linearVelocity = new Vector2(0f, -14f);
                }
                else
                {
                    _rb.linearVelocity = new Vector2(0f, -1.5f);
                }
            }
        }

        public void EndJuggle()
        {
            CurrentState = AirJuggleState.None;
            _wasAscending = false;
            JuggleHitCount = 0;

            if (_rb != null)
            {
                _rb.gravityScale = _originalGravityScale;
            }

            OnJuggleEnded?.Invoke();
        }

        private bool CheckGrounded()
        {
            if (_col == null) _col = GetComponent<Collider2D>();
            if (_col == null) return true;

            Bounds b = _col.bounds;
            Vector2 checkPos = new Vector2(b.center.x, b.min.y + 0.05f);
            Vector2 checkSize = new Vector2(b.size.x * 0.7f, 0.15f);

            Collider2D hit = Physics2D.OverlapBox(checkPos, checkSize, 0f, groundLayer);
            if (hit != null && hit != _col && !hit.isTrigger)
            {
                return true;
            }
            return false;
        }

        private void TriggerVisualShimmer(Color shimmerColor)
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_sr == null) return;

            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(ShimmerRoutine(shimmerColor));
        }

        private IEnumerator ShimmerRoutine(Color flashCol)
        {
            if (_sr == null) yield break;
            Color orig = _sr.color;
            _sr.color = flashCol;
            yield return new WaitForSeconds(0.08f);
            if (_sr != null) _sr.color = orig;
            _flashRoutine = null;
        }

        private void OnDisable()
        {
            if (IsFrozen)
            {
                EndJuggle();
            }
        }
    }
}
