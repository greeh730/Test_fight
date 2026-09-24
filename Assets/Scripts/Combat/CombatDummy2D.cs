using System;
using System.Collections;
using UnityEngine;
using Combat.Common;

namespace Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class CombatDummy2D : MonoBehaviour, ICombatEntity2D
    {
        [Header("--- Dummy Settings ---")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;
        [SerializeField] private float resetHealthDelay = 3.0f;

        [Header("--- Death & Respawn Settings ---")]
        [Tooltip("Если включено (галочка), манекен разрушается при HP <= 0. Если выключено — манекен не погибает и восстанавливает HP.")]
        [SerializeField] private bool canDie = true;

        [Tooltip("Время до автоматического восстановления манекена (сек)")]
        [SerializeField] private float respawnDelay = 3.0f;

        [Tooltip("Цвет разрушенного манекена")]
        [SerializeField] private Color brokenColor = new Color(0.4f, 0.4f, 0.4f, 0.7f);

        [Header("--- Visual Feedback ---")]
        [SerializeField] private Color normalColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        [SerializeField] private float flashDuration = 0.15f;

        [Header("--- 3 Zone Setup ---")]
        [Tooltip("Высота верхней границы зоны Low относительно центра")]
        [SerializeField] private float lowZoneTop = -0.2f;

        [Tooltip("Высота верхней границы зоны Mid относительно центра")]
        [SerializeField] private float midZoneTop = 0.35f;

        private Rigidbody2D _rb;
        private SpriteRenderer _sr;
        private Coroutine _flashRoutine;
        private Coroutine _respawnRoutine;
        private Coroutine _statusEffectRoutine;
        private float _lastHitTime;
        private float _vulnerabilityMultiplier = 1.0f;
        private float _originalGravityScale = 1.0f;

        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private Vector3 _spawnScale;
        private readonly System.Collections.Generic.List<Collider2D> _hurtboxColliders = new System.Collections.Generic.List<Collider2D>();

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool CanDie
        {
            get => canDie;
            set
            {
                canDie = value;
                if (!canDie && IsDead)
                {
                    Respawn();
                }
            }
        }
        public bool IsDead { get; private set; }
        public Rigidbody2D Rigidbody => _rb;
        public AirJuggleReceiver2D AirJuggle => _airJuggle;

        private AirJuggleReceiver2D _airJuggle;

        private void OnEnable()
        {
            CombatTargetResolver.Register(this);
        }

        private void OnDisable()
        {
            CombatTargetResolver.Unregister(this);
        }

        private void OnValidate()
        {
            if (transform.localScale.sqrMagnitude < 0.001f)
            {
                transform.localScale = Vector3.one;
            }

            if (!canDie && IsDead)
            {
                Respawn();
            }
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _sr = GetComponent<SpriteRenderer>();

            if (_rb != null)
            {
                _originalGravityScale = _rb.gravityScale;
            }

            _airJuggle = GetComponent<AirJuggleReceiver2D>();
            if (_airJuggle == null)
            {
                _airJuggle = gameObject.AddComponent<AirJuggleReceiver2D>();
            }

            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
            _spawnScale = transform.localScale;
            if (_spawnScale.sqrMagnitude < 0.001f)
            {
                _spawnScale = Vector3.one;
                transform.localScale = Vector3.one;
            }

            if (!canDie)
            {
                IsDead = false;
            }
            currentHealth = maxHealth;

            if (_sr != null) _sr.color = normalColor;

            EnsureHurtboxes();
        }

        private void Start()
        {
            if (!canDie && IsDead)
            {
                Respawn();
            }
            else if (!IsDead)
            {
                EnableAllColliders(true);
            }
        }

        private void EnsureHurtboxes()
        {
            float highCenter = (1.0f + midZoneTop) * 0.5f;
            float highHeight = 1.0f - midZoneTop;

            float midCenter = (midZoneTop + lowZoneTop) * 0.5f;
            float midHeight = midZoneTop - lowZoneTop;

            float lowCenter = (lowZoneTop - 1.0f) * 0.5f;
            float lowHeight = lowZoneTop - (-1.0f);

            SetupZoneCollider("Hurtbox_High", CombatZone.High, new Vector2(0f, highCenter), new Vector2(0.9f, highHeight));
            SetupZoneCollider("Hurtbox_Mid", CombatZone.Mid, new Vector2(0f, midCenter), new Vector2(0.9f, midHeight));
            SetupZoneCollider("Hurtbox_Low", CombatZone.Low, new Vector2(0f, lowCenter), new Vector2(0.9f, lowHeight));
        }

        private void SetupZoneCollider(string objName, CombatZone zone, Vector2 offset, Vector2 size)
        {
            var child = transform.Find(objName);
            GameObject go;
            if (child == null)
            {
                go = new GameObject(objName);
                go.transform.SetParent(transform, false);
            }
            else
            {
                go = child.gameObject;
            }

            go.layer = gameObject.layer;

            var col = go.GetComponent<BoxCollider2D>();
            if (col == null) col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.offset = offset;
            col.size = size;
            col.enabled = !IsDead;

            if (!_hurtboxColliders.Contains(col)) _hurtboxColliders.Add(col);

            var hb = go.GetComponent<CombatHurtbox2D>();
            if (hb == null) hb = go.AddComponent<CombatHurtbox2D>();
            hb.Initialize(zone, this);
        }

        private void EnableAllColliders(bool enable)
        {
            var rootCol = GetComponent<Collider2D>();
            if (rootCol != null) rootCol.enabled = enable;

            for (int i = 0; i < _hurtboxColliders.Count; i++)
            {
                if (_hurtboxColliders[i] != null) _hurtboxColliders[i].enabled = enable;
            }
        }

        public void TakeHit(AttackConfig attack, CombatZone hitZone, Vector2 hitPoint, Vector2 knockbackDirection)
        {
            if (IsDead)
            {
                if (!canDie)
                {
                    Respawn();
                }
                else
                {
                    return;
                }
            }

            float baseDamage = attack != null ? attack.damage : 20f;
            float damage = baseDamage * _vulnerabilityMultiplier;

            if (!canDie)
            {
                // Режим бессмертия: HP не опускается ниже 1, манекен никогда не разрушается
                currentHealth = Mathf.Max(1f, currentHealth - damage);
            }
            else
            {
                currentHealth = Mathf.Max(0f, currentHealth - damage);
            }
            _lastHitTime = Time.time;

            // 1. Проверяем лаунчер (подкидывание в воздух) или джаггл в воздухе
            bool isLauncher = attack != null && (attack.isLauncher || attack.knockbackForce.y >= 9f);
            if (isLauncher && _airJuggle != null)
            {
                Vector2 launchVel = new Vector2(
                    knockbackDirection.x * Mathf.Max(2.5f, attack.knockbackForce.x),
                    Mathf.Max(12.5f, attack.knockbackForce.y)
                );
                _airJuggle.Launch(launchVel);
            }
            else if (_airJuggle != null && (_airJuggle.IsFrozen || _airJuggle.IsAirborne))
            {
                _airJuggle.OnAirHit(attack, false);
            }
            else if (_rb != null && attack != null)
            {
                Vector2 force = new Vector2(
                    knockbackDirection.x * attack.knockbackForce.x,
                    attack.knockbackForce.y
                );
                _rb.linearVelocity = Vector2.zero; // сброс старой инерции для четкого удара
                _rb.AddForce(force, ForceMode2D.Impulse);
            }

            // Визуальная вспышка цветом зоны
            Color zoneColor = hitZone.GetZoneColor(1f);
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashColorRoutine(zoneColor));

            Debug.Log($"<color=orange><b>[DUMMY HIT!]</b></color> Атака: <b>{attack?.attackName}</b> | Зона: <b><color=#{ColorUtility.ToHtmlStringRGB(zoneColor)}>{hitZone}</color></b> | Урон: <b>{damage:F0}</b> | HP: <b>{currentHealth:F0}/{maxHealth}</b>");

            // Проверка гибели/разрушения
            if (canDie && currentHealth <= 0f && !IsDead)
            {
                Die(knockbackDirection);
            }
        }

        public void Die(Vector2 knockbackDirection)
        {
            if (IsDead) return;
            IsDead = true;

            if (_flashRoutine != null) { StopCoroutine(_flashRoutine); _flashRoutine = null; }
            if (_statusEffectRoutine != null) { StopCoroutine(_statusEffectRoutine); _statusEffectRoutine = null; }
            _vulnerabilityMultiplier = 1.0f;

            // Отключаем хёртбоксы, чтобы удары не проходили сквозь разрушенный манекен
            for (int i = 0; i < _hurtboxColliders.Count; i++)
            {
                if (_hurtboxColliders[i] != null) _hurtboxColliders[i].enabled = false;
            }

            // Физическое опрокидывание на бок
            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.angularVelocity = 0f;
                _rb.gravityScale = _originalGravityScale;
            }

            float tiltSign = Mathf.Sign(knockbackDirection.x != 0f ? knockbackDirection.x : 1f);
            transform.rotation = Quaternion.Euler(0f, 0f, -80f * tiltSign);

            if (_sr != null) _sr.color = brokenColor;

            ShowBrokenPopup(transform.position);

            Debug.Log("<color=red><b>[DUMMY BROKEN]</b></color> Манекен разрушен! Восстановление через " + respawnDelay + " сек.");

            // Начисление очков стиля за разрушение тренировочного манекена
            if (Combat.Style.StyleManager.Instance != null)
            {
                Combat.Style.StyleManager.Instance.AddDummyKill(this, transform.position);
            }

            if (_respawnRoutine != null) StopCoroutine(_respawnRoutine);
            _respawnRoutine = StartCoroutine(RespawnRoutine(respawnDelay));
        }

        public void Respawn()
        {
            IsDead = false;

            if (_respawnRoutine != null)
            {
                StopCoroutine(_respawnRoutine);
                _respawnRoutine = null;
            }

            if (_statusEffectRoutine != null)
            {
                StopCoroutine(_statusEffectRoutine);
                _statusEffectRoutine = null;
            }

            _vulnerabilityMultiplier = 1.0f;

            if (_spawnScale.sqrMagnitude < 0.001f)
            {
                _spawnScale = Vector3.one;
            }

            transform.position = _spawnPosition;
            transform.rotation = _spawnRotation;
            transform.localScale = _spawnScale;

            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.angularVelocity = 0f;
                _rb.gravityScale = _originalGravityScale;
            }

            currentHealth = maxHealth;
            if (_sr != null) _sr.color = normalColor;

            // Включаем все коллайдеры хёртбоксов и основной коллайдер
            EnableAllColliders(true);

            Debug.Log("<color=cyan><b>[DUMMY RESPAWNED]</b></color> Манекен восстановлен и готов к бою!");
        }

        public void RestoreState(Vector2 position, float health, bool isDeadState)
        {
            if (_respawnRoutine != null) { StopCoroutine(_respawnRoutine); _respawnRoutine = null; }
            if (_statusEffectRoutine != null) { StopCoroutine(_statusEffectRoutine); _statusEffectRoutine = null; }
            if (_flashRoutine != null) { StopCoroutine(_flashRoutine); _flashRoutine = null; }

            _vulnerabilityMultiplier = 1.0f;

            transform.position = new Vector3(position.x, position.y, transform.position.z);
            transform.rotation = _spawnRotation;

            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.angularVelocity = 0f;
                _rb.gravityScale = _originalGravityScale;
            }

            if (isDeadState)
            {
                Die(Vector2.right);
            }
            else
            {
                IsDead = false;
                currentHealth = Mathf.Clamp(health, 1f, maxHealth);
                if (_sr != null) _sr.color = normalColor;
                EnableAllColliders(true);
            }
        }

        private IEnumerator RespawnRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            Respawn();
            _respawnRoutine = null;
        }

        public void ApplyVulnerabilityMark(float duration, float multiplier)
        {
            if (IsDead) return;
            if (_statusEffectRoutine != null) StopCoroutine(_statusEffectRoutine);
            _statusEffectRoutine = StartCoroutine(VulnerabilityRoutine(duration, multiplier));
        }

        private IEnumerator VulnerabilityRoutine(float duration, float multiplier)
        {
            _vulnerabilityMultiplier = multiplier;
            if (_sr != null) _sr.color = new Color(0.9f, 0.4f, 1f, 1f);
            yield return new WaitForSeconds(duration);
            _vulnerabilityMultiplier = 1.0f;
            if (!IsDead && _sr != null) _sr.color = normalColor;
            _statusEffectRoutine = null;
        }

        public void ApplyRoot(float duration)
        {
            if (IsDead) return;
            StartCoroutine(RootRoutine(duration));
        }

        private IEnumerator RootRoutine(float duration)
        {
            if (_rb != null) _rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(duration);
        }

        public void ApplyGravitySuspension(float duration, Vector2 anchorPos)
        {
            if (IsDead) return;
            StartCoroutine(GravitySuspensionRoutine(duration, anchorPos));
        }

        private IEnumerator GravitySuspensionRoutine(float duration, Vector2 anchorPos)
        {
            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.gravityScale = 0f;
            }
            float elapsed = 0f;
            while (elapsed < duration && !IsDead)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(transform.position, (Vector3)anchorPos, Time.deltaTime * 5f);
                yield return null;
            }
            if (_rb != null)
            {
                _rb.gravityScale = _originalGravityScale;
            }
        }

        public void PullTowards(Vector2 targetPos, float speed)
        {
            if (IsDead) return;
            Vector2 dir = (targetPos - (Vector2)transform.position).normalized;
            if (_rb != null)
            {
                _rb.linearVelocity = dir * speed;
            }
        }

        public void Disorient(float duration)
        {
            // Манекен не имеет AI для дезориентации
        }

        public void Stun(float duration)
        {
            if (IsDead) return;
            CombatFloatingText.Spawn(transform.position + Vector3.up * 1.5f, $"[ОГЛУШЕН! {duration:F1}с]", new Color(1f, 0.85f, 0.2f), 1.2f);
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashColorRoutine(new Color(1f, 0.85f, 0.2f)));
        }

        private void ShowBrokenPopup(Vector3 pos)
        {
            CombatFloatingText.ShowDummyBroken(pos);
        }

        private IEnumerator FlashColorRoutine(Color flashColor)
        {
            if (_sr != null) _sr.color = flashColor;
            yield return new WaitForSeconds(flashDuration);
            if (!IsDead && _sr != null) _sr.color = normalColor;
            _flashRoutine = null;
        }

        private void Update()
        {
            if (IsDead) return;

            // Автоматическое восстановление здоровья манекена после паузы
            if (currentHealth < maxHealth && Time.time - _lastHitTime > resetHealthDelay)
            {
                currentHealth = Mathf.MoveTowards(currentHealth, maxHealth, Time.deltaTime * 30f);
            }
        }

        private void OnDrawGizmos()
        {
            Vector3 pos = transform.position;

            // Отрисовка 3 зон манекена в окне Scene
            // High
            Gizmos.color = new Color(1f, 0.2f, 0.25f, 0.4f);
            Gizmos.DrawWireCube(pos + new Vector3(0f, 0.55f, 0f), new Vector3(0.9f, 0.45f, 0.1f));

            // Mid
            Gizmos.color = new Color(1f, 0.85f, 0.15f, 0.4f);
            Gizmos.DrawWireCube(pos + new Vector3(0f, 0.08f, 0f), new Vector3(0.9f, 0.5f, 0.1f));

            // Low
            Gizmos.color = new Color(0.15f, 0.85f, 1f, 0.4f);
            Gizmos.DrawWireCube(pos + new Vector3(0f, -0.45f, 0f), new Vector3(0.9f, 0.55f, 0.1f));
        }
    }
}
