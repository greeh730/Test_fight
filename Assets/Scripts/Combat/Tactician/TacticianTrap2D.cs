using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Combat.Common;

namespace Combat.Tactician
{
    /// <summary>
    /// Глубинная печать (⬇️):
    /// Нажимная ловушка прямо под ногами Тактика.
    /// Активируется, если враг наступает на нее. Наносит урон и обездвиживает (Root) на 1.5 сек.
    /// Может быть досрочно сдетонирована направленными шипами (↘️) для усиленного эффекта.
    /// </summary>
    [DisallowMultipleComponent]
    public class TacticianTrap2D : MonoBehaviour
    {
        [Header("--- Trap Settings ---")]
        [SerializeField] private float triggerRadius = 1.1f;
        [SerializeField] private float damage = 15f;
        [SerializeField] private float rootDuration = 1.5f;
        [SerializeField] private float lifetime = 25f;
        [SerializeField] private LayerMask enemyLayers = ~0;

        [Header("--- Visuals ---")]
        [SerializeField] private Color runeColor = new Color(0f, 0.9f, 1f, 0.85f); // Яркий рунический циан
        [SerializeField] private Color burstColor = new Color(0.3f, 1f, 0.9f, 1f);

        private LineRenderer _circleRenderer;
        private SpriteRenderer _centerRuneRenderer;
        private bool _isTriggered = false;
        private float _spawnTime;

        public static readonly List<TacticianTrap2D> ActiveTraps = new List<TacticianTrap2D>();

        private void OnEnable()
        {
            ActiveTraps.Add(this);
        }

        private void OnDisable()
        {
            ActiveTraps.Remove(this);
        }

        public void Initialize(float radius, float dmg, float rootDur, float life, Color color)
        {
            triggerRadius = radius;
            damage = dmg;
            rootDuration = rootDur;
            lifetime = life;
            runeColor = color;
            burstColor = new Color(Mathf.Min(1f, color.r * 1.2f), Mathf.Min(1f, color.g * 1.2f), Mathf.Min(1f, color.b * 1.2f), 1f);

            if (_circleRenderer != null)
            {
                int segments = 24;
                Vector3[] pts = new Vector3[segments];
                for (int i = 0; i < segments; i++)
                {
                    float rad = i * Mathf.PI * 2f / segments;
                    pts[i] = new Vector3(Mathf.Cos(rad) * triggerRadius, Mathf.Sin(rad) * triggerRadius * 0.35f, 0f);
                }
                _circleRenderer.SetPositions(pts);
                _circleRenderer.startColor = runeColor;
                _circleRenderer.endColor = runeColor;
            }

            if (_centerRuneRenderer != null)
            {
                _centerRuneRenderer.color = runeColor;
            }
        }

        private void Awake()
        {
            _spawnTime = Time.time;
            BuildVisuals();
        }

        private void BuildVisuals()
        {
            // 1. Рунический круг на земле
            _circleRenderer = gameObject.AddComponent<LineRenderer>();
            _circleRenderer.useWorldSpace = false;
            _circleRenderer.loop = true;
            _circleRenderer.startWidth = 0.05f;
            _circleRenderer.endWidth = 0.05f;
            _circleRenderer.sortingOrder = 25;

            var mat = new Material(Shader.Find("Sprites/Default"));
            _circleRenderer.material = mat;
            _circleRenderer.startColor = runeColor;
            _circleRenderer.endColor = runeColor;

            int segments = 24;
            _circleRenderer.positionCount = segments;
            Vector3[] pts = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float rad = i * Mathf.PI * 2f / segments;
                pts[i] = new Vector3(Mathf.Cos(rad) * triggerRadius, Mathf.Sin(rad) * triggerRadius * 0.35f, 0f);
            }
            _circleRenderer.SetPositions(pts);

            // 2. Центральная руническая метка
            var runeObj = new GameObject("Center_Rune");
            runeObj.transform.SetParent(transform, false);
            _centerRuneRenderer = runeObj.AddComponent<SpriteRenderer>();
            _centerRuneRenderer.sprite = CombatSprites.WhiteBox;
            runeObj.transform.localScale = new Vector3(0.35f, 0.35f, 1f);
            runeObj.transform.localRotation = Quaternion.Euler(0f, 0f, 45f); // Ромбическая руна
            _centerRuneRenderer.color = runeColor;
            _centerRuneRenderer.sortingOrder = 26;
        }

        private void Update()
        {
            if (_isTriggered) return;

            // Мягкая пульсация руны
            float pulse = 0.75f + 0.25f * Mathf.Sin((Time.time - _spawnTime) * 4.5f);
            Color c = runeColor;
            c.a = runeColor.a * pulse;
            if (_circleRenderer != null)
            {
                _circleRenderer.startColor = c;
                _circleRenderer.endColor = c;
            }
            if (_centerRuneRenderer != null)
            {
                _centerRuneRenderer.color = c;
                _centerRuneRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, (Time.time * 45f) % 360f);
            }

            // Проверка наступания врагов
            CheckEnemyProximity();

            // Авто-деспавн по истечении времени
            if (Time.time - _spawnTime > lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void CheckEnemyProximity()
        {
            var colliders = Physics2D.OverlapCircleAll(transform.position, triggerRadius, enemyLayers);
            for (int i = 0; i < colliders.Length; i++)
            {
                var col = colliders[i];
                if (col == null || col.CompareTag("Player")) continue;

                if (CombatTargetResolver.IsAliveTarget(col, out _))
                {
                    Detonate(isResonance: false);
                    return;
                }
            }
        }

        /// <summary>
        /// Подрыв ловушки при наступании или детонации шипами
        /// </summary>
        public void Detonate(ICombatEntity2D primaryTarget, bool isResonance)
        {
            Detonate(isResonance);
        }

        public void Detonate(bool isResonance = false)
        {
            if (_isTriggered) return;
            _isTriggered = true;

            float effDmg = isResonance ? damage * 1.6f : damage;
            float effRoot = isResonance ? rootDuration * 1.3f : rootDuration;

            var fakeAttack = new AttackConfig(
                isResonance ? "Резонанс Печати" : "Глубинная печать",
                CombatZone.Low | CombatZone.Mid,
                Vector2.zero,
                Vector2.one,
                0f, 0.1f, 0.1f,
                effDmg,
                new Vector2(1f, 2f),
                burstColor
            );

            // Наносим урон и Root всем врагам и манекенам в радиусе взрыва
            var hits = Physics2D.OverlapCircleAll(transform.position, triggerRadius * (isResonance ? 1.5f : 1.1f), enemyLayers);
            var targets = CombatTargetResolver.GetUniqueAliveTargets(hits, gameObject);
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                target.TakeHit(fakeAttack, CombatZone.Low, transform.position, Vector2.up);
                target.ApplyRoot(effRoot);
            }

            StartCoroutine(DetonationVisualRoutine(isResonance));
        }

        private IEnumerator DetonationVisualRoutine(bool isResonance)
        {
            if (_circleRenderer != null) _circleRenderer.enabled = false;
            if (_centerRuneRenderer != null) _centerRuneRenderer.enabled = false;

            // Вспышка взрыва
            var burstObj = new GameObject("Trap_Burst");
            burstObj.transform.position = transform.position;
            var sr = burstObj.AddComponent<SpriteRenderer>();
            sr.sprite = CombatSprites.WhiteBox;
            sr.sortingOrder = 35;
            sr.color = isResonance ? new Color(1f, 0.85f, 0.2f, 1f) : burstColor;

            float duration = 0.25f;
            float elapsed = 0f;
            float targetScale = (triggerRadius * 2.2f) * (isResonance ? 1.5f : 1.0f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                if (burstObj != null)
                {
                    burstObj.transform.localScale = Vector3.Lerp(Vector3.zero, new Vector3(targetScale, targetScale * 0.4f, 1f), Mathf.Sqrt(t));
                    Color c = sr.color;
                    c.a = Mathf.Clamp01(1f - t);
                    sr.color = c;
                }
                yield return null;
            }

            if (burstObj != null) Destroy(burstObj);
            Destroy(gameObject);
        }
    }
}
