using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        [Header("--- Anchor Settings ---")]
        [SerializeField] private float captureRadius = 1.85f;
        [SerializeField] private float suspensionDuration = 2.0f;
        [SerializeField] private float lifetime = 5.0f;
        [SerializeField] private LayerMask enemyLayers = ~0;

        [Header("--- Visuals ---")]
        [SerializeField] private Color coreColor = new Color(0.7f, 0.3f, 1f, 0.95f);    // Фиолетовый
        [SerializeField] private Color fieldColor = new Color(0.35f, 0.8f, 1f, 0.45f); // Голубоватый шлейф

        private LineRenderer _fieldRing;
        private SpriteRenderer _coreRenderer;
        private float _spawnTime;
        private readonly HashSet<EnemyAIController2D> _capturedEnemies = new HashSet<EnemyAIController2D>();

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

            var tex = new Texture2D(2, 2);
            tex.SetPixels(new Color[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            _coreRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2);
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
            var colliders = Physics2D.OverlapCircleAll(transform.position, captureRadius, enemyLayers);
            for (int i = 0; i < colliders.Length; i++)
            {
                var col = colliders[i];
                if (col == null || col.CompareTag("Player")) continue;

                var enemy = col.GetComponent<EnemyAIController2D>() ?? col.GetComponentInParent<EnemyAIController2D>();
                if (enemy != null && !enemy.IsDead && !_capturedEnemies.Contains(enemy))
                {
                    _capturedEnemies.Add(enemy);
                    enemy.ApplyGravitySuspension(suspensionDuration, transform.position);
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
