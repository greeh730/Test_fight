using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Combat.Common;

namespace Combat.Tactician
{
    /// <summary>
    /// Гравитационный якорь (⬆️):
    /// Магическая сфера, висящая в воздухе 5 секунд.
    /// Любой враг, попавший в её радиус (например, подброшенный вверх),
    /// захватывается гравитационной аномалией и зависает в воздухе на 2 секунды.
    /// </summary>
    [DisallowMultipleComponent]
    public class TacticianGravityAnchor2D : MonoBehaviour
    {
        [Header("--- Gravity Anchor Settings ---")]
        [SerializeField] private float captureRadius = 2.6f;
        [SerializeField] private float suspensionDuration = 5.0f;
        [SerializeField] private float lifetime = 5.0f;
        [SerializeField] private LayerMask enemyLayers = ~0;

        [Header("--- Visuals ---")]
        [SerializeField] private Color coreColor = new Color(0.75f, 0.35f, 1f, 1f);
        [SerializeField] private Color fieldColor = new Color(0.55f, 0.2f, 0.9f, 0.45f);

        private SpriteRenderer _coreRenderer;
        private LineRenderer _fieldRing;
        private float _spawnTime;
        private readonly HashSet<ICombatEntity2D> _capturedTargets = new HashSet<ICombatEntity2D>();

        public void Initialize(float radius, float suspDuration, float life, Color color)
        {
            captureRadius = radius;
            suspensionDuration = suspDuration;
            lifetime = life;
            coreColor = color;
            fieldColor = new Color(color.r * 0.7f, color.g * 0.9f, 1f, 0.45f);

            if (_coreRenderer != null)
            {
                _coreRenderer.color = coreColor;
            }

            if (_fieldRing != null)
            {
                int segments = 28;
                Vector3[] pts = new Vector3[segments];
                for (int i = 0; i < segments; i++)
                {
                    float rad = i * Mathf.PI * 2f / segments;
                    pts[i] = new Vector3(Mathf.Cos(rad) * captureRadius, Mathf.Sin(rad) * captureRadius, 0f);
                }
                _fieldRing.SetPositions(pts);
                _fieldRing.startColor = fieldColor;
                _fieldRing.endColor = fieldColor;
            }
        }

        private void Awake()
        {
            _spawnTime = Time.time;
            BuildVisuals();
        }

        private void BuildVisuals()
        {
            // 1. Центральное ядро сингулярности
            var coreObj = new GameObject("Anchor_Core");
            coreObj.transform.SetParent(transform, false);
            _coreRenderer = coreObj.AddComponent<SpriteRenderer>();
            _coreRenderer.sprite = CombatSprites.WhiteBox;
            coreObj.transform.localScale = new Vector3(0.42f, 0.42f, 1f);
            coreObj.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            _coreRenderer.color = coreColor;
            _coreRenderer.sortingOrder = 50;

            // 2. Гравитационное кольцо поля
            _fieldRing = gameObject.AddComponent<LineRenderer>();
            _fieldRing.useWorldSpace = false;
            _fieldRing.loop = true;
            _fieldRing.startWidth = 0.045f;
            _fieldRing.endWidth = 0.045f;
            _fieldRing.sortingOrder = 49;

            var mat = new Material(Shader.Find("Sprites/Default"));
            _fieldRing.material = mat;
            _fieldRing.startColor = fieldColor;
            _fieldRing.endColor = fieldColor;

            int segments = 28;
            _fieldRing.positionCount = segments;
            Vector3[] pts = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float rad = i * Mathf.PI * 2f / segments;
                pts[i] = new Vector3(Mathf.Cos(rad) * captureRadius, Mathf.Sin(rad) * captureRadius, 0f);
            }
            _fieldRing.SetPositions(pts);
        }

        private void Update()
        {
            float age = Time.time - _spawnTime;

            // Вращение и пульсация ядра
            if (_coreRenderer != null)
            {
                _coreRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, (age * 120f) % 360f);
                float pulse = 0.85f + 0.15f * Mathf.Sin(age * 8f);
                _coreRenderer.transform.localScale = Vector3.one * (0.42f * pulse);
            }

            // Пульсация внешнего поля
            if (_fieldRing != null)
            {
                float ringPulse = 1f + 0.08f * Mathf.Sin(age * 6f);
                _fieldRing.transform.localScale = Vector3.one * ringPulse;
            }

            // Захват врагов в радиусе сферы
            CheckForEnemies();

            // Автоматическое затухание и уничтожение через 5 секунд
            if (age >= lifetime)
            {
                StartCoroutine(DespawnRoutine());
            }
        }

        private void CheckForEnemies()
        {
            var targets = CombatTargetResolver.GetUniqueAliveTargets(
                Physics2D.OverlapCircleAll(transform.position, captureRadius, enemyLayers),
                gameObject
            );
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (_capturedTargets.Add(target))
                {
                    target.ApplyGravitySuspension(suspensionDuration, transform.position);
                }
            }
        }

        private IEnumerator DespawnRoutine()
        {
            enabled = false;
            float dur = 0.3f;
            float el = 0f;
            Vector3 startScale = transform.localScale;

            while (el < dur)
            {
                el += Time.deltaTime;
                float t = el / dur;
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
