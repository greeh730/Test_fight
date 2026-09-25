using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Combat;
using Combat.UI;

namespace Combat.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerHealth2D : MonoBehaviour, IHurtboxTarget2D
    {
        [Header("--- Health Settings ---")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;
        [SerializeField] private float invulnerabilityDuration = 0.4f;
        [Tooltip("Длительность оглушения игрока при получении любого удара (стан)")]
        [SerializeField] private float hitstunDuration = 0.28f;

        [Header("--- Death & Respawn Settings ---")]
        [Tooltip("Если включено (галочка), игрок погибает при HP <= 0. Если выключено — игрок бессмертен (HP не падает ниже 1).")]
        [SerializeField] private bool canDie = true;

        [Tooltip("Клавиша для быстрого перезапуска сцены")]
        [SerializeField] private KeyCode respawnKey = KeyCode.R;

        [Tooltip("Цвет спрайта игрока при гибели")]
        [SerializeField] private Color deathColor = new Color(0.35f, 0.35f, 0.35f, 0.85f);

        [Header("--- Feedback ---")]
        [SerializeField] private Color hitFlashColor = new Color(1f, 0.25f, 0.25f, 1f);
        [SerializeField] private float flashDuration = 0.12f;

        [Header("--- Events ---")]
        public UnityEvent<float, float> onHealthChanged;
        public UnityEvent onDamaged;
        public UnityEvent onDeath;
        public UnityEvent onRespawn;

        public static event Action OnAnyPlayerDeath;
        public static event Action OnAnyPlayerRespawn;

        public static void TriggerDeathEvent() => OnAnyPlayerDeath?.Invoke();
        public static void TriggerRespawnEvent() => OnAnyPlayerRespawn?.Invoke();

        private Rigidbody2D _rb;
        private SpriteRenderer _sr;
        private PlayerController2D _movement;
        private PlayerCombatController2D _combat;

        private Color _originalColor = Color.white;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private Coroutine _flashRoutine;
        private Coroutine _iFrameRoutine;
        private bool _isInvulnerable;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public bool IsInvulnerable => _isInvulnerable;
        public bool CanDie { get => canDie; set => canDie = value; }
        public bool IsDead { get; private set; }

        public void SetInvulnerable(bool invulnerable)
        {
            _isInvulnerable = invulnerable;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _sr = GetComponent<SpriteRenderer>();
            _movement = GetComponent<PlayerController2D>();
            _combat = GetComponent<PlayerCombatController2D>();

            if (_sr != null)
            {
                _sr.enabled = true;
                _originalColor = _sr.color;
            }
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;

            currentHealth = maxHealth;

            if (PlayerHealthBarUI.Instance == null)
            {
                PlayerHealthBarUI.EnsureExists();
            }
        }

        private void Start()
        {
            if (PlayerHealthBarUI.Instance == null)
            {
                PlayerHealthBarUI.EnsureExists();
            }
        }

        private void OnEnable()
        {
            if (_sr != null) _sr.enabled = true;
        }

        private void Update()
        {
            if (IsDead)
            {
                CheckRespawnInput();
            }
        }

        private void CheckRespawnInput()
        {
            bool keyPressed = false;

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.rKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
            {
                keyPressed = true;
            }
#endif
            try
            {
                if (!keyPressed && (Input.GetKeyDown(respawnKey) || Input.GetKeyDown(KeyCode.Space)))
                {
                    keyPressed = true;
                }
            }
            catch { }

            if (keyPressed)
            {
                RestartGame();
            }
        }

        public void TakeHit(AttackConfig attack, CombatZone hitZone, Vector2 hitPoint, Vector2 knockbackDirection)
        {
            if (_isInvulnerable || IsDead) return;

            float damage = attack != null ? attack.damage : 15f;
            Vector2 effectiveKnockbackDir = knockbackDirection;

            // Проверка перехвата удара Блоком или Парированием
            var blockParry = GetComponent<PlayerBlockAndParry2D>();
            if (blockParry != null && blockParry.TryInterceptAttack(attack, hitZone, hitPoint, knockbackDirection, out bool hitAbsorbed, out float damageMultiplier, out Vector2 modifiedKnockback))
            {
                if (hitAbsorbed)
                {
                    // Удар полностью спарирован или заблокирован (0 урона!)
                    if (_rb != null && modifiedKnockback.sqrMagnitude > 0.001f)
                    {
                        _rb.linearVelocity = modifiedKnockback;
                    }
                    return;
                }

                // Частичный урон (Chip Damage) сквозь пробитый блок при нулевой выносливости
                damage *= damageMultiplier;
                effectiveKnockbackDir = modifiedKnockback;
            }

            if (!canDie)
            {
                // Режим бессмертия: HP не опускается ниже 1
                currentHealth = Mathf.Max(1f, currentHealth - damage);
            }
            else
            {
                currentHealth = Mathf.Max(0f, currentHealth - damage);
            }

            // Физический импульс отталкивания
            if (_rb != null && attack != null)
            {
                Vector2 kb = new Vector2(effectiveKnockbackDir.x * attack.knockbackForce.x, attack.knockbackForce.y);
                _rb.linearVelocity = kb;
            }

            // Оглушение (стан) игрока от любого удара
            if (_movement == null) _movement = GetComponent<PlayerController2D>();
            if (_combat == null) _combat = GetComponent<PlayerCombatController2D>();
            if (_movement != null) _movement.ApplyHitstun(hitstunDuration);
            if (_combat != null) _combat.ApplyHitstun(hitstunDuration);

            onHealthChanged?.Invoke(currentHealth, maxHealth);
            onDamaged?.Invoke();

            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRoutine());

            if (_iFrameRoutine != null) StopCoroutine(_iFrameRoutine);
            _iFrameRoutine = StartCoroutine(IFrameRoutine());

            Debug.Log($"[PLAYER HIT!] Получен урон: {damage:F0} HP. Текущее HP: {currentHealth:F0}/{maxHealth:F0}");

            if (canDie && currentHealth <= 0f && !IsDead)
            {
                Die();
            }
        }

        public void Die()
        {
            if (IsDead) return;
            IsDead = true;

            if (_flashRoutine != null) { StopCoroutine(_flashRoutine); _flashRoutine = null; }
            if (_iFrameRoutine != null) { StopCoroutine(_iFrameRoutine); _iFrameRoutine = null; }

            // Отключаем управление и боевые действия
            if (_movement != null) _movement.enabled = false;
            if (_combat != null)
            {
                _combat.CancelAttack();
                _combat.enabled = false;
            }

            // Физика: падение на бок
            if (_rb != null)
            {
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            }
            transform.rotation = Quaternion.Euler(0f, 0f, 90f);

            if (_sr != null)
            {
                _sr.enabled = true;
                _sr.color = deathColor;
            }

            PlayerDeathScreenUI.ShowDeathScreen();
            onDeath?.Invoke();
            OnAnyPlayerDeath?.Invoke();

            Debug.Log("<color=red><b>[PLAYER DIED]</b></color> Игрок погиб! Нажмите [R] для перезагрузки сцены.");
        }

        /// <summary>
        /// Перезагружает активную сцену для начала заново
        /// </summary>
        public void RestartGame()
        {
            PlayerDeathScreenUI.RestartGame();
        }

        public void Respawn()
        {
            RestartGame();
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            onHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void ModifyMaxHealth(float deltaAmount, bool healCurrent = true)
        {
            maxHealth = Mathf.Max(10f, maxHealth + deltaAmount);
            if (healCurrent && deltaAmount > 0f)
            {
                currentHealth = Mathf.Min(maxHealth, currentHealth + deltaAmount);
            }
            else
            {
                currentHealth = Mathf.Clamp(currentHealth, 1f, maxHealth);
            }
            onHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void ResetHealth()
        {
            currentHealth = maxHealth;
            _isInvulnerable = false;
            if (_sr != null)
            {
                _sr.enabled = true;
                _sr.color = _originalColor;
            }
            onHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void RestoreState(Vector2 position, float health, bool isDeadState)
        {
            if (_flashRoutine != null) { StopCoroutine(_flashRoutine); _flashRoutine = null; }
            if (_iFrameRoutine != null) { StopCoroutine(_iFrameRoutine); _iFrameRoutine = null; }

            transform.position = new Vector3(position.x, position.y, transform.position.z);
            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
            }

            if (isDeadState)
            {
                currentHealth = 0f;
                onHealthChanged?.Invoke(currentHealth, maxHealth);
                Die();
            }
            else
            {
                IsDead = false;
                transform.rotation = _spawnRotation;

                if (_movement != null) _movement.enabled = true;
                if (_combat != null) _combat.enabled = true;

                currentHealth = Mathf.Clamp(health, 1f, maxHealth);
                _isInvulnerable = false;
                if (_sr != null)
                {
                    _sr.enabled = true;
                    _sr.color = _originalColor;
                }
                onHealthChanged?.Invoke(currentHealth, maxHealth);
            }
        }

        private IEnumerator FlashRoutine()
        {
            if (_sr == null) yield break;
            _sr.color = hitFlashColor;
            yield return new WaitForSeconds(flashDuration);
            if (!IsDead) _sr.color = _originalColor;
            _flashRoutine = null;
        }

        private IEnumerator IFrameRoutine(float customDuration = -1f)
        {
            _isInvulnerable = true;
            float duration = customDuration > 0f ? customDuration : invulnerabilityDuration;
            float elapsed = 0f;
            float flickerInterval = 0.06f;

            while (elapsed < duration)
            {
                if (_sr != null) _sr.enabled = !_sr.enabled;
                yield return new WaitForSeconds(flickerInterval);
                elapsed += flickerInterval;
            }

            if (_sr != null) _sr.enabled = true;
            _isInvulnerable = false;
            _iFrameRoutine = null;
        }
    }
}
