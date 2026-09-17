using System;
using System.Collections;
using UnityEngine;

namespace Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class CombatDummy2D : MonoBehaviour, IHurtboxTarget2D
    {
        [Header("--- Dummy Settings ---")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;
        [SerializeField] private float resetHealthDelay = 3.0f;

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
        private float _lastHitTime;

        public float CurrentHealth => currentHealth;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _sr = GetComponent<SpriteRenderer>();

            currentHealth = maxHealth;
            if (_sr != null) _sr.color = normalColor;

            EnsureHurtboxes();
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

            var col = go.GetComponent<BoxCollider2D>();
            if (col == null) col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.offset = offset;
            col.size = size;

            var hb = go.GetComponent<CombatHurtbox2D>();
            if (hb == null) hb = go.AddComponent<CombatHurtbox2D>();
            hb.Initialize(zone, this);
        }

        public void TakeHit(AttackConfig attack, CombatZone hitZone, Vector2 hitPoint, Vector2 knockbackDirection)
        {
            currentHealth = Mathf.Max(0f, currentHealth - attack.damage);
            _lastHitTime = Time.time;

            // Применяем физический импульс отталкивания
            if (_rb != null)
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

            Debug.Log($"<color=orange><b>[DUMMY HIT!]</b></color> Атака: <b>{attack.attackName}</b> | Зона: <b><color=#{ColorUtility.ToHtmlStringRGB(zoneColor)}>{hitZone}</color></b> | Урон: <b>{attack.damage}</b> | HP: <b>{currentHealth:F0}/{maxHealth}</b>");
        }

        private IEnumerator FlashColorRoutine(Color flashColor)
        {
            if (_sr != null) _sr.color = flashColor;
            yield return new WaitForSeconds(flashDuration);
            if (_sr != null) _sr.color = normalColor;
            _flashRoutine = null;
        }

        private void Update()
        {
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
