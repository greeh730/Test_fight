using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Combat.Player;
using Combat;

namespace LevelGeneration
{
    public enum HazardType
    {
        [Tooltip("Шипы: наносят урон при наступании или контакте")]
        Spikes = 0,

        [Tooltip("Зона урона (огонь, лава, кислота, ядовитый туман) с периодическим тиком")]
        DamageField = 1,

        [Tooltip("Нажимная плита: при наступании утапливается и активирует связанные ловушки/события")]
        PressurePlate = 2,

        [Tooltip("Падающая ловушка (обвал/сталактит): падает вниз при обнаружении игрока под ней")]
        FallingTrap = 3
    }

    /// <summary>
    /// Универсальный модульный компонент опасности / ловушки на арене.
    /// Поддерживает шипы, зоны периодического урона, нажимные плиты и падающие блоки.
    /// Наносит урон как игроку (PlayerHealth2D), так и врагам (EnemyAIController2D).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class ArenaHazard2D : MonoBehaviour
    {
        [Header("--- Тип и поведение ловушки ---")]
        [Tooltip("Тип опасности")]
        [SerializeField] private HazardType hazardType = HazardType.Spikes;

        [Tooltip("Активна ли ловушка изначально")]
        [SerializeField] private bool isActive = true;

        [Header("--- Параметры урона ---")]
        [Tooltip("Мгновенно убивать цель (актуально для бездны или смертельных шипов)")]
        [SerializeField] private bool isInstantKill = false;

        [Tooltip("Количество урона за одно срабатывание")]
        [SerializeField] private float damageAmount = 25f;

        [Tooltip("Интервал между тиками урона в секундах (для зон огня/кислоты)")]
        [SerializeField] private float damageInterval = 0.5f;

        [Tooltip("Сила физического отталкивания цели от ловушки")]
        [SerializeField] private float knockbackForce = 6f;

        [Tooltip("Направление отталкивания (если (0,0), отталкивает от центра ловушки)")]
        [SerializeField] private Vector2 knockbackDirection = Vector2.up;

        [Header("--- Кого поражает ловушка ---")]
        [Tooltip("Наносить ли урон игроку")]
        [SerializeField] private bool affectsPlayer = true;

        [Tooltip("Наносить ли урон врагам")]
        [SerializeField] private bool affectsEnemies = true;

        [Header("--- Нажимная плита (Pressure Plate) ---")]
        [Tooltip("Смещение спрайта вниз при нажатии")]
        [SerializeField] private float pressedOffset = 0.15f;

        [Tooltip("Связанные ловушки или триггеры, которые активируются при нажатии этой плиты")]
        [SerializeField] private List<ArenaHazard2D> linkedHazards = new List<ArenaHazard2D>();

        [Header("--- Падающая ловушка (Falling Trap) ---")]
        [Tooltip("Дистанция обнаружения цели под ловушкой для сброса")]
        [SerializeField] private float fallTriggerDistance = 8f;

        [Tooltip("Скорость падения")]
        [SerializeField] private float fallGravityScale = 3f;

        [Header("--- Звук и эффекты ---")]
        [Tooltip("Звуковой эффект при срабатывании")]
        [SerializeField] private AudioClip triggerSound;

        [Header("--- События (UnityEvent) ---")]
        public UnityEvent onHazardTriggered;
        public UnityEvent onHazardReset;

        private Collider2D _col;
        private Rigidbody2D _rb;
        private SpriteRenderer _sr;
        private Vector3 _initialLocalPos;
        private bool _isPressed = false;
        private bool _hasFallen = false;
        private readonly Dictionary<GameObject, float> _lastDamageTimes = new Dictionary<GameObject, float>();

        public HazardType Type => hazardType;
        public bool IsActive { get => isActive; set => isActive = value; }

        private void Awake()
        {
            _col = GetComponent<Collider2D>();
            _col.isTrigger = (hazardType != HazardType.FallingTrap);
            _sr = GetComponentInChildren<SpriteRenderer>();
            _initialLocalPos = transform.localPosition;

            if (hazardType == HazardType.FallingTrap)
            {
                _rb = GetComponent<Rigidbody2D>();
                if (_rb == null) _rb = gameObject.AddComponent<Rigidbody2D>();
                _rb.bodyType = RigidbodyType2D.Kinematic;
            }
        }

        private void Update()
        {
            if (!isActive) return;

            // Логика обнаружения под собой для падающей ловушки
            if (hazardType == HazardType.FallingTrap && !_hasFallen)
            {
                RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, fallTriggerDistance, LayerMask.GetMask("Default", "Characters", "Player"));
                if (hit.collider != null && hit.collider.CompareTag("Player"))
                {
                    TriggerFall();
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isActive) return;
            HandleTargetContact(other.gameObject, isInitialContact: true);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!isActive) return;
            if (hazardType == HazardType.DamageField)
            {
                HandleTargetContact(other.gameObject, isInitialContact: false);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (hazardType == HazardType.PressurePlate && _isPressed)
            {
                ReleasePressurePlate();
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!isActive) return;
            if (hazardType == HazardType.FallingTrap && _hasFallen)
            {
                HandleTargetContact(collision.gameObject, isInitialContact: true);
            }
        }

        private void HandleTargetContact(GameObject target, bool isInitialContact)
        {
            bool isPlayer = target.CompareTag("Player");
            bool isEnemy = target.CompareTag("Enemy");

            if ((isPlayer && !affectsPlayer) || (isEnemy && !affectsEnemies))
            {
                return;
            }

            if (!isPlayer && !isEnemy)
            {
                // Проверяем родителей
                if (target.GetComponentInParent<PlayerHealth2D>() != null && affectsPlayer) isPlayer = true;
                else if (target.GetComponentInParent<EnemyAIController2D>() != null && affectsEnemies) isEnemy = true;
                else return;
            }

            // Обработка нажимной плиты
            if (hazardType == HazardType.PressurePlate)
            {
                PressPressurePlate();
                return;
            }

            // Проверка кулдауна тика урона
            if (!isInitialContact && damageInterval > 0f)
            {
                if (_lastDamageTimes.TryGetValue(target, out float lastTime))
                {
                    if (Time.time - lastTime < damageInterval) return;
                }
            }
            _lastDamageTimes[target] = Time.time;

            // Нанесение урона
            ApplyDamageToTarget(target, isPlayer);

            onHazardTriggered?.Invoke();
            PlayTriggerAudio();
        }

        private void ApplyDamageToTarget(GameObject target, bool isPlayer)
        {
            Vector2 kbDir = knockbackDirection;
            if (kbDir == Vector2.zero)
            {
                kbDir = (target.transform.position - transform.position).normalized;
                if (kbDir == Vector2.zero) kbDir = Vector2.up;
            }

            if (isPlayer)
            {
                var health = target.GetComponent<PlayerHealth2D>() ?? target.GetComponentInParent<PlayerHealth2D>();
                if (health != null)
                {
                    if (isInstantKill)
                    {
                        health.CanDie = true;
                        health.Die();
                    }
                    else
                    {
                        health.TakeDirectDamage(damageAmount);
                        var rb = target.GetComponent<Rigidbody2D>();
                        if (rb != null) rb.linearVelocity = kbDir * knockbackForce;
                    }
                }
            }
            else
            {
                var enemy = target.GetComponent<EnemyAIController2D>() ?? target.GetComponentInParent<EnemyAIController2D>();
                if (enemy != null)
                {
                    if (isInstantKill)
                    {
                        enemy.TakeDirectDamage(9999f, kbDir * knockbackForce);
                    }
                    else
                    {
                        enemy.TakeDirectDamage(damageAmount, kbDir * knockbackForce);
                    }
                }
                else
                {
                    target.SendMessage("TakeDamage", damageAmount, SendMessageOptions.DontRequireReceiver);
                }
            }
        }

        private void PressPressurePlate()
        {
            if (_isPressed) return;
            _isPressed = true;

            transform.localPosition = _initialLocalPos - new Vector3(0f, pressedOffset, 0f);
            PlayTriggerAudio();
            onHazardTriggered?.Invoke();

            for (int i = 0; i < linkedHazards.Count; i++)
            {
                if (linkedHazards[i] != null) linkedHazards[i].TriggerHazard();
            }
        }

        private void ReleasePressurePlate()
        {
            if (!_isPressed) return;
            _isPressed = false;
            transform.localPosition = _initialLocalPos;
            onHazardReset?.Invoke();
        }

        private void TriggerFall()
        {
            if (_hasFallen) return;
            _hasFallen = true;

            if (_rb != null)
            {
                _rb.bodyType = RigidbodyType2D.Dynamic;
                _rb.gravityScale = fallGravityScale;
            }
            onHazardTriggered?.Invoke();
            PlayTriggerAudio();
        }

        /// <summary>
        /// Внешний вызов активации ловушки (из триггера или плиты)
        /// </summary>
        public void TriggerHazard()
        {
            if (hazardType == HazardType.FallingTrap)
            {
                TriggerFall();
            }
            else
            {
                isActive = true;
                onHazardTriggered?.Invoke();
                PlayTriggerAudio();
            }
        }

        public void ResetHazard()
        {
            isActive = true;
            _isPressed = false;
            _hasFallen = false;
            transform.localPosition = _initialLocalPos;

            if (_rb != null)
            {
                _rb.bodyType = RigidbodyType2D.Kinematic;
                _rb.linearVelocity = Vector2.zero;
            }
            onHazardReset?.Invoke();
        }

        private void PlayTriggerAudio()
        {
            if (triggerSound != null)
            {
                AudioSource.PlayClipAtPoint(triggerSound, transform.position);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Color c;
            switch (hazardType)
            {
                case HazardType.Spikes:        c = new Color(1f, 0.2f, 0.2f, 0.7f); break;
                case HazardType.DamageField:   c = new Color(1f, 0.6f, 0.1f, 0.7f); break;
                case HazardType.PressurePlate: c = new Color(0.2f, 0.8f, 1f, 0.7f); break;
                case HazardType.FallingTrap:   c = new Color(0.8f, 0.3f, 0.9f, 0.7f); break;
                default:                       c = Color.red; break;
            }

            Gizmos.color = c;
            var col = GetComponent<Collider2D>();
            if (col is BoxCollider2D box)
            {
                Gizmos.DrawWireCube(transform.position + (Vector3)box.offset, box.size);
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, 0.5f);
            }

            if (hazardType == HazardType.FallingTrap && fallTriggerDistance > 0f)
            {
                Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.5f);
                Gizmos.DrawLine(transform.position, transform.position + Vector3.down * fallTriggerDistance);
            }

            UnityEditor.Handles.color = c;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.4f, $"[Ловушка: {hazardType}]");
        }
#endif
    }
}
