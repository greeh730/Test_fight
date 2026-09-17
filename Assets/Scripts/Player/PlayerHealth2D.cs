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

        [Header("--- Feedback ---")]
        [SerializeField] private Color hitFlashColor = new Color(1f, 0.25f, 0.25f, 1f);
        [SerializeField] private float flashDuration = 0.12f;

        [Header("--- Events ---")]
        public UnityEvent<float, float> onHealthChanged;
        public UnityEvent onDamaged;

        private Rigidbody2D _rb;
        private SpriteRenderer _sr;
        private Color _originalColor = Color.white;
        private Coroutine _flashRoutine;
        private Coroutine _iFrameRoutine;
        private bool _isInvulnerable;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public bool IsInvulnerable => _isInvulnerable;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _originalColor = _sr.color;

            currentHealth = maxHealth;
        }

        public void TakeHit(AttackConfig attack, CombatZone hitZone, Vector2 hitPoint, Vector2 knockbackDirection)
        {
            if (_isInvulnerable || currentHealth <= 0f) return;

            float damage = attack != null ? attack.damage : 15f;
            currentHealth = Mathf.Max(0f, currentHealth - damage);

            // Физический импульс отталкивания
            if (_rb != null && attack != null)
            {
                Vector2 kb = new Vector2(knockbackDirection.x * attack.knockbackForce.x, attack.knockbackForce.y);
                _rb.linearVelocity = kb;
            }

            onHealthChanged?.Invoke(currentHealth, maxHealth);
            onDamaged?.Invoke();

            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRoutine());

            if (_iFrameRoutine != null) StopCoroutine(_iFrameRoutine);
            _iFrameRoutine = StartCoroutine(IFrameRoutine());

            Debug.Log($"[PLAYER HIT!] Получен урон: {damage:F0} HP. Текущее HP: {currentHealth:F0}/{maxHealth:F0}");
        }

        public void Heal(float amount)
        {
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

        private IEnumerator FlashRoutine()
        {
            if (_sr == null) yield break;
            _sr.color = hitFlashColor;
            yield return new WaitForSeconds(flashDuration);
            _sr.color = _originalColor;
            _flashRoutine = null;
        }

        private IEnumerator IFrameRoutine()
        {
            _isInvulnerable = true;
            float elapsed = 0f;
            float flickerInterval = 0.06f;

            while (elapsed < invulnerabilityDuration)
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
