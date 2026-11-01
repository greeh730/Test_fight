using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Combat;
using Combat.Player;

namespace Magic
{
    /// <summary>
    /// Эффект горения (Burn Status Effect):
    /// - Наносит периодический урон (DoT) каждые tickInterval секунд.
    /// - Окрашивает обводку спрайта в цвет огня (шейдер SpriteOutlineURP).
    /// - Испускает огненные частицы и мерцающий 2D свет (Light2D).
    /// - Перекидывает огонь на подошедших вплотную других врагов или игрока (Spread).
    /// </summary>
    [DisallowMultipleComponent]
    public class BurnStatusEffect : MonoBehaviour
    {
        [Header("--- Параметры горения ---")]
        [Tooltip("Длительность эффекта горения в секундах")]
        [SerializeField] private float duration = 3f;

        [Tooltip("Периодический урон за один тик")]
        [SerializeField] private float damagePerTick = 5f;

        [Tooltip("Интервал между тиками урона в секундах")]
        [SerializeField] private float tickInterval = 1.0f;

        [Tooltip("Скорость постепенного сгорания выносливости (стамины) врага в секунду")]
        [SerializeField] private float staminaDrainPerSecond = 20.0f;

        [Tooltip("Радиус распространения огня на подошедших сущностей")]
        [SerializeField] private float spreadRadius = 1.5f;

        [Tooltip("Цвет огненной обводки")]
        [SerializeField] private Color outlineColor = new Color(2.4f, 0.75f, 0.1f, 1f);

        [Header("--- Ресурсы визуализации ---")]
        [SerializeField] private Material outlineMaterial;
        [SerializeField] private GameObject burnParticlesPrefab;

        private float remainingDuration = 0f;
        private float tickTimer = 0f;
        private float spreadTimer = 0f;
        private bool isFinished = false;

        private EnemyAIController2D enemyTarget;
        private PlayerHealth2D playerTarget;

        private readonly List<SpriteRenderer> targetRenderers = new List<SpriteRenderer>();
        private readonly List<Material> originalMaterials = new List<Material>();
        private GameObject spawnedVisual;

        public float Duration { get => duration; set => duration = Mathf.Max(0.1f, value); }
        public float DamagePerTick { get => damagePerTick; set => damagePerTick = Mathf.Max(0f, value); }
        public float TickInterval { get => tickInterval; set => tickInterval = Mathf.Max(0.1f, value); }
        public float SpreadRadius { get => spreadRadius; set => spreadRadius = Mathf.Max(0.2f, value); }
        public float StaminaDrainPerSecond { get => staminaDrainPerSecond; set => staminaDrainPerSecond = Mathf.Max(0f, value); }
        public float RemainingDuration => remainingDuration;

        public static readonly List<BurnStatusEffect> ActiveEffects = new List<BurnStatusEffect>();

        private void OnEnable()
        {
            if (!ActiveEffects.Contains(this)) ActiveEffects.Add(this);
        }

        private void OnDisable()
        {
            ActiveEffects.Remove(this);
        }

        public static BurnStatusEffect Apply(GameObject target, float duration = 3f, float damagePerTick = 5f, float tickInterval = 1f, float spreadRadius = 1.5f, Material customOutlineMat = null, GameObject customVisualPrefab = null, float staminaDrainPerSecond = 20f)
        {
            if (target == null) return null;

            var existing = target.GetComponent<BurnStatusEffect>();
            if (existing != null)
            {
                existing.Refresh(duration);
                return existing;
            }

            var burn = target.AddComponent<BurnStatusEffect>();
            burn.duration = duration;
            burn.damagePerTick = damagePerTick;
            burn.tickInterval = tickInterval;
            burn.spreadRadius = spreadRadius;
            burn.staminaDrainPerSecond = staminaDrainPerSecond;
            if (customOutlineMat != null) burn.outlineMaterial = customOutlineMat;
            if (customVisualPrefab != null) burn.burnParticlesPrefab = customVisualPrefab;

            if (!ActiveEffects.Contains(burn)) ActiveEffects.Add(burn);

            return burn;
        }

        private void Awake()
        {
            enemyTarget = GetComponent<EnemyAIController2D>();
            playerTarget = GetComponent<PlayerHealth2D>();

            // Загрузка дефолтных ассетов, если не назначены
            if (outlineMaterial == null)
            {
                outlineMaterial = Resources.Load<Material>("Materials/Magic/Burn_Outline_Mat");
#if UNITY_EDITOR
                if (outlineMaterial == null)
                {
                    outlineMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Magic/Burn_Outline_Mat.mat");
                }
#endif
            }

            if (burnParticlesPrefab == null)
            {
                burnParticlesPrefab = Resources.Load<GameObject>("Prefabs/Magic/BurnEffectVisual");
#if UNITY_EDITOR
                if (burnParticlesPrefab == null)
                {
                    burnParticlesPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Magic/BurnEffectVisual.prefab");
                }
#endif
            }
        }

        private void Start()
        {
            remainingDuration = duration;
            tickTimer = tickInterval;

            ApplyVisuals();

            if (Combat.Audio.SoundManager.Instance != null)
            {
                Combat.Audio.SoundManager.Instance.PlayFireEffect(0.9f);
            }

            Debug.Log($"<color=#FF5500>[Горение]</color> Наложен эффект горения на <b>{gameObject.name}</b> (длительность: {duration:F1} сек., урон: {damagePerTick}/сек)");
        }

        public void Refresh(float newDuration)
        {
            remainingDuration = Mathf.Max(remainingDuration, newDuration);
            Debug.Log($"<color=#FF5500>[Горение]</color> Продлено горение на <b>{gameObject.name}</b>: {remainingDuration:F1} сек.");
        }

        private void Update()
        {
            if (isFinished) return;

            // Проверка гибели цели
            if (IsTargetDead())
            {
                Finish();
                return;
            }

            // 1. Таймер общей длительности
            remainingDuration -= Time.deltaTime;
            if (remainingDuration <= 0f)
            {
                Finish();
                return;
            }

            // 2. Таймер тиков урона
            tickTimer -= Time.deltaTime;
            if (tickTimer <= 0f)
            {
                tickTimer = tickInterval;
                DealBurnTickDamage();
            }

            // 3. Постепенное сгорание шкалы стамины у горящего врага
            if (enemyTarget != null && enemyTarget.CurrentState != EnemyState.Dead && enemyTarget.CurrentState != EnemyState.Stunned)
            {
                enemyTarget.DrainStamina(staminaDrainPerSecond * Time.deltaTime);
            }

            // 4. Распространение огня на подошедших рядом
            spreadTimer -= Time.deltaTime;
            if (spreadTimer <= 0f)
            {
                spreadTimer = 0.2f;
                SpreadFireToNearby();
            }
        }

        private void DealBurnTickDamage()
        {
            if (Combat.Audio.SoundManager.Instance != null)
            {
                Combat.Audio.SoundManager.Instance.PlayFireEffect(0.6f, 1.05f, 1.15f);
            }

            if (enemyTarget != null)
            {
                enemyTarget.TakeDirectDamage(damagePerTick);
            }
            else if (playerTarget != null)
            {
                playerTarget.TakeDirectDamage(damagePerTick);
            }
        }

        private void SpreadFireToNearby()
        {
            if (spreadRadius <= 0.05f) return;

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, spreadRadius);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D col = hits[i];
                if (col == null || col.isTrigger) continue;

                // Не поджигаем самого себя повторно
                if (col.gameObject == gameObject || col.transform.IsChildOf(transform)) continue;

                GameObject potentialTarget = null;
                var otherEnemy = col.GetComponentInParent<EnemyAIController2D>();
                if (otherEnemy != null && otherEnemy.gameObject != gameObject && otherEnemy.CurrentState != EnemyState.Dead)
                {
                    potentialTarget = otherEnemy.gameObject;
                }
                else
                {
                    var otherPlayer = col.GetComponentInParent<PlayerHealth2D>();
                    if (otherPlayer != null && otherPlayer.gameObject != gameObject && !otherPlayer.IsDead)
                    {
                        potentialTarget = otherPlayer.gameObject;
                    }
                }

                if (potentialTarget != null)
                {
                    var existingBurn = potentialTarget.GetComponent<BurnStatusEffect>();
                    if (existingBurn == null)
                    {
                        Apply(potentialTarget, duration, damagePerTick, tickInterval, spreadRadius, outlineMaterial, burnParticlesPrefab, staminaDrainPerSecond);
                        Debug.Log($"<color=#FF7700>[Огонь]</color> Пламя перекинулось с <b>{gameObject.name}</b> на <b>{potentialTarget.name}</b>!");
                    }
                }
            }
        }

        private void ApplyVisuals()
        {
            // 1. Огненная обводка (Outline)
            targetRenderers.Clear();
            originalMaterials.Clear();

            var srs = GetComponentsInChildren<SpriteRenderer>();
            foreach (var sr in srs)
            {
                if (sr == null) continue;
                targetRenderers.Add(sr);
                originalMaterials.Add(sr.sharedMaterial);

                if (outlineMaterial != null)
                {
                    var instMat = new Material(outlineMaterial);
                    instMat.SetColor("_OutlineColor", outlineColor);
                    sr.material = instMat;
                }
            }

            // 2. Частицы огня и динамическое освещение
            if (burnParticlesPrefab != null)
            {
                spawnedVisual = Instantiate(burnParticlesPrefab, transform);
                spawnedVisual.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            }
        }

        private void RemoveVisuals()
        {
            // Восстановление оригинальных материалов
            for (int i = 0; i < targetRenderers.Count; i++)
            {
                if (targetRenderers[i] != null && i < originalMaterials.Count)
                {
                    targetRenderers[i].material = originalMaterials[i];
                }
            }
            targetRenderers.Clear();
            originalMaterials.Clear();

            // Остановка и удаление эффекта частиц
            if (spawnedVisual != null)
            {
                var ps = spawnedVisual.GetComponentInChildren<ParticleSystem>();
                if (ps != null)
                {
                    var emission = ps.emission;
                    emission.enabled = false;
                }
                Destroy(spawnedVisual, 1.0f);
                spawnedVisual = null;
            }
        }

        private bool IsTargetDead()
        {
            if (enemyTarget != null && enemyTarget.CurrentState == EnemyState.Dead) return true;
            if (playerTarget != null && playerTarget.IsDead) return true;
            return false;
        }

        private void Finish()
        {
            if (isFinished) return;
            isFinished = true;

            RemoveVisuals();
            Destroy(this);
            Debug.Log($"<color=#888888>[Горение]</color> Эффект горения на <b>{gameObject.name}</b> завершён.");
        }

        private void OnDestroy()
        {
            ActiveEffects.Remove(this);
            if (!isFinished)
            {
                RemoveVisuals();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, spreadRadius);
        }
    }
}
