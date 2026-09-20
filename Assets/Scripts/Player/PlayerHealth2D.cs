using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Combat;

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

        [Header("--- Death & Respawn Settings ---")]
        [Tooltip("Если включено (галочка), игрок погибает при HP <= 0. Если выключено — игрок бессмертен (HP не падает ниже 1).")]
        [SerializeField] private bool canDie = true;

        [Tooltip("Автоматическое возрождение через указанное количество секунд (0 = только по клавише R)")]
        [SerializeField] private float autoRespawnDelay = 3.0f;

        [Tooltip("Клавиша для быстрого возрождения")]
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

        private Rigidbody2D _rb;
        private SpriteRenderer _sr;
        private PlayerController2D _movement;
        private PlayerCombatController2D _combat;

        private Color _originalColor = Color.white;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private Coroutine _flashRoutine;
        private Coroutine _iFrameRoutine;
        private Coroutine _autoRespawnRoutine;
        private GameObject _deathOverlayObj;
        private bool _isInvulnerable;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public bool IsInvulnerable => _isInvulnerable;
        public bool CanDie { get => canDie; set => canDie = value; }
        public bool IsDead { get; private set; }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _sr = GetComponent<SpriteRenderer>();
            _movement = GetComponent<PlayerController2D>();
            _combat = GetComponent<PlayerCombatController2D>();

            if (_sr != null) _originalColor = _sr.color;
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;

            currentHealth = maxHealth;
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
                Respawn();
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

            ShowDeathBanner();
            onDeath?.Invoke();

            Debug.Log("<color=red><b>[PLAYER DIED]</b></color> Игрок погиб! Нажмите [R] для возрождения.");

            if (autoRespawnDelay > 0f)
            {
                if (_autoRespawnRoutine != null) StopCoroutine(_autoRespawnRoutine);
                _autoRespawnRoutine = StartCoroutine(AutoRespawnRoutine(autoRespawnDelay));
            }
        }

        public void Respawn()
        {
            if (!IsDead) return;
            IsDead = false;

            if (_autoRespawnRoutine != null)
            {
                StopCoroutine(_autoRespawnRoutine);
                _autoRespawnRoutine = null;
            }

            HideDeathBanner();

            // Возврат на спавн и сброс поворота
            transform.position = _spawnPosition;
            transform.rotation = _spawnRotation;

            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
            }

            ResetHealth();

            // Включаем обратно компоненты
            if (_movement != null) _movement.enabled = true;
            if (_combat != null) _combat.enabled = true;

            // Временная неуязвимость при возрождении (1.2 сек)
            if (_iFrameRoutine != null) StopCoroutine(_iFrameRoutine);
            _iFrameRoutine = StartCoroutine(IFrameRoutine(1.2f));

            onRespawn?.Invoke();
            Debug.Log("<color=green><b>[PLAYER RESPAWN]</b></color> Игрок возрожден на исходной позиции!");
        }

        private void ShowDeathBanner()
        {
            HideDeathBanner();

            _deathOverlayObj = new GameObject("Player_Death_Banner");
            _deathOverlayObj.transform.position = transform.position + new Vector3(0f, 1.3f, 0f);

            var titleObj = new GameObject("Death_Title");
            titleObj.transform.SetParent(_deathOverlayObj.transform, false);
            titleObj.transform.localPosition = new Vector3(0f, 0.45f, 0f);

            var titleTm = titleObj.AddComponent<TextMesh>();
            titleTm.text = "ВЫ ПОГИБЛИ";
            titleTm.fontSize = 54;
            titleTm.characterSize = 0.088f;
            titleTm.alignment = TextAlignment.Center;
            titleTm.anchor = TextAnchor.MiddleCenter;
            titleTm.fontStyle = FontStyle.Bold;
            titleTm.color = new Color(0.95f, 0.15f, 0.15f, 1f);
            var mr1 = titleObj.GetComponent<MeshRenderer>();
            if (mr1 != null) mr1.sortingOrder = 95;

            var subObj = new GameObject("Death_Subtitle");
            subObj.transform.SetParent(_deathOverlayObj.transform, false);
            subObj.transform.localPosition = new Vector3(0f, -0.18f, 0f);

            var subTm = subObj.AddComponent<TextMesh>();
            subTm.text = autoRespawnDelay > 0f ? $"[ R ] Возродиться ({autoRespawnDelay:F0}с)" : "[ R ] Возродиться";
            subTm.fontSize = 32;
            subTm.characterSize = 0.075f;
            subTm.alignment = TextAlignment.Center;
            subTm.anchor = TextAnchor.MiddleCenter;
            subTm.fontStyle = FontStyle.Bold;
            subTm.color = new Color(1f, 0.95f, 0.95f, 0.95f);
            var mr2 = subObj.GetComponent<MeshRenderer>();
            if (mr2 != null) mr2.sortingOrder = 96;
        }

        private void HideDeathBanner()
        {
            if (_deathOverlayObj != null)
            {
                Destroy(_deathOverlayObj);
                _deathOverlayObj = null;
            }
        }

        private IEnumerator AutoRespawnRoutine(float delay)
        {
            float elapsed = 0f;
            while (elapsed < delay)
            {
                elapsed += Time.deltaTime;
                if (_deathOverlayObj != null)
                {
                    var sub = _deathOverlayObj.transform.Find("Death_Subtitle");
                    if (sub != null)
                    {
                        var tm = sub.GetComponent<TextMesh>();
                        if (tm != null)
                        {
                            float remaining = Mathf.Max(0f, delay - elapsed);
                            tm.text = $"[ R ] Возродиться ({remaining:F1}с)";
                        }
                    }
                }
                yield return null;
            }
            Respawn();
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            onHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void ResetHealth()
        {
            currentHealth = maxHealth;
            _isInvulnerable = false;
            if (_sr != null) _sr.color = _originalColor;
            onHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void RestoreState(Vector2 position, float health, bool isDeadState)
        {
            if (_flashRoutine != null) { StopCoroutine(_flashRoutine); _flashRoutine = null; }
            if (_iFrameRoutine != null) { StopCoroutine(_iFrameRoutine); _iFrameRoutine = null; }
            if (_autoRespawnRoutine != null) { StopCoroutine(_autoRespawnRoutine); _autoRespawnRoutine = null; }

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
                HideDeathBanner();
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

        private void OnDestroy()
        {
            HideDeathBanner();
        }
    }
}
