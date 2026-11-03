using System.Collections;
using UnityEngine;

namespace Magic
{
    /// <summary>
    /// Снаряд заклинания "Огонь" (Фаербол):
    /// - Достаточно крупный сгусток пламени, летящий вперёд по направлению взгляда игрока.
    /// - Обладает собственным свечением (URP Light2D + HDR эмиссия).
    /// - Испускает искры, которые падают под действием гравитации на пол и постепенно гаснут.
    /// - Оставляет за собой огненный шлейф.
    /// - Взрывается/рассеивается при попадании в препятствие или по истечении времени жизни.
    /// </summary>
    [DisallowMultipleComponent]
    public class FireballProjectile : MonoBehaviour
    {
        [Header("--- Параметры полёта ---")]
        [Tooltip("Скорость полёта фаербола")]
        [SerializeField] private float speed = 14f;

        [Tooltip("Время жизни фаербола в секундах до самоликвидации")]
        [SerializeField] private float lifetime = 4f;

        [Tooltip("Направление полёта (нормализованный вектор)")]
        [SerializeField] private Vector2 direction = Vector2.right;

        [Header("--- Размеры и визуал огня ---")]
        [Tooltip("Базовый диаметр фаербола")]
        [SerializeField] private float fireballScale = 1.6f;

        [Tooltip("Скорость пульсации пламени фаербола")]
        [SerializeField] private float pulseSpeed = 16f;

        [Tooltip("Амплитуда пульсации пламени")]
        [SerializeField] private float pulseAmount = 0.08f;

        [Header("--- Урон и поджог (Damage & Burn) ---")]
        [Tooltip("Урон от прямого попадания фаербола в противника")]
        [SerializeField] private float directHitDamage = 50f;

        [Tooltip("Длительность эффекта горения в секундах")]
        [SerializeField] private float burnDuration = 3f;

        [Tooltip("Урон за один тик горения")]
        [SerializeField] private float burnDamagePerTick = 5f;

        [Tooltip("Интервал между тиками горения в секундах")]
        [SerializeField] private float burnTickInterval = 1.0f;

        [Tooltip("Радиус распространения огня на подошедших врагов или игрока")]
        [SerializeField] private float burnSpreadRadius = 1.5f;

        [Tooltip("Материал огненной обводки для горящего персонажа")]
        [SerializeField] private Material burnOutlineMaterial;

        [Header("--- Robert Hostile Fireball ---")]
        [Tooltip("Если включено, фаербол наносит урон игроку вместо врагов (для пасхалки с Робертом)")]
        [SerializeField] private bool isHostileToPlayer = false;

        public bool IsHostileToPlayer { get => isHostileToPlayer; set => isHostileToPlayer = value; }

        public void SetHostileToPlayer(bool hostile, float damage = 25f)
        {
            isHostileToPlayer = hostile;
            if (damage > 0f) directHitDamage = damage;
        }

        [Tooltip("Префаб частиц и света для горящего персонажа")]
        [SerializeField] private GameObject burnParticlesPrefab;

        [Header("--- Источник света (Light2D) ---")]
        [Tooltip("Включить динамический 2D свет фаербола")]
        [SerializeField] private bool enableDynamicLight = true;

        [Tooltip("Цвет освещения фаербола")]
        [SerializeField] private Color lightColor = new Color(1.0f, 0.55f, 0.15f, 1f);

        [Tooltip("Интенсивность света фаербола")]
        [SerializeField] private float baseLightIntensity = 2.4f;

        [Tooltip("Внешний радиус освещения")]
        [SerializeField] private float lightOuterRadius = 6.0f;

        [Header("--- Искры и частицы ---")]
        [Tooltip("Система падающих искр")]
        [SerializeField] private ParticleSystem sparksParticleSystem;

        [Tooltip("Шлейф пламени")]
        [SerializeField] private ParticleSystem trailParticleSystem;

        [Header("--- Коллизии ---")]
        [Tooltip("Слои, с которыми сталкивается фаербол (например, земля, стены, враги)")]
        [SerializeField] private LayerMask hitLayers = ~0; // По умолчанию все, кроме игрока

        [Header("--- Ссылки на дочерние элементы ---")]
        [SerializeField] private Transform coreTransform;
        [SerializeField] private Transform auraTransform;
        [SerializeField] private SpriteRenderer coreRenderer;
        [SerializeField] private SpriteRenderer auraRenderer;

        private float currentLifetime = 0f;
        private bool isDying = false;
        private bool hitTarget = false;

        public static event System.Action<FireballProjectile, Combat.CombatDummy2D> OnAnyFireballHitDummy;
        public static event System.Action<FireballProjectile> OnAnyFireballMissed;
        private Rigidbody2D rb;
        private Component light2DComp;
        private System.Reflection.PropertyInfo lightIntensityProp;
        private Vector3 initialCoreScale = Vector3.one;
        private Vector3 initialAuraScale = Vector3.one;
        private Transform caster;

        public float Speed { get => speed; set => speed = value; }
        public float Lifetime { get => lifetime; set => lifetime = value; }
        public Vector2 Direction => direction;

        public float DirectHitDamage { get => directHitDamage; set => directHitDamage = Mathf.Max(0f, value); }
        public float BurnDuration { get => burnDuration; set => burnDuration = Mathf.Max(0.1f, value); }
        public float BurnDamagePerTick { get => burnDamagePerTick; set => burnDamagePerTick = Mathf.Max(0f, value); }
        public float BurnTickInterval { get => burnTickInterval; set => burnTickInterval = Mathf.Max(0.1f, value); }
        public float BurnSpreadRadius { get => burnSpreadRadius; set => burnSpreadRadius = Mathf.Max(0.2f, value); }
        public Material BurnOutlineMaterial { get => burnOutlineMaterial; set => burnOutlineMaterial = value; }
        public GameObject BurnParticlesPrefab { get => burnParticlesPrefab; set => burnParticlesPrefab = value; }

        public static readonly System.Collections.Generic.List<FireballProjectile> ActiveProjectiles = new System.Collections.Generic.List<FireballProjectile>();

        private void OnEnable()
        {
            if (!ActiveProjectiles.Contains(this)) ActiveProjectiles.Add(this);
        }

        private void OnDisable()
        {
            ActiveProjectiles.Remove(this);
        }

        private void OnDestroy()
        {
            ActiveProjectiles.Remove(this);
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;
            }

            // Находим или инициализируем Light2D
            InitializeLight2D();

            if (coreTransform != null) initialCoreScale = coreTransform.localScale;
            if (auraTransform != null) initialAuraScale = auraTransform.localScale;
        }

        private void Start()
        {
            transform.localScale = Vector3.one * fireballScale;
            UpdateRotationAndDirection();
        }

        /// <summary>
        /// Инициализация параметров полёта и урона при спавне.
        /// </summary>
        public void Initialize(Vector2 flyDirection, float flySpeed = -1f, float scale = -1f, float damage = -1f, float burnTime = -1f, float burnDmg = -1f)
        {
            if (!ActiveProjectiles.Contains(this)) ActiveProjectiles.Add(this);
            direction = flyDirection.sqrMagnitude > 0.001f ? flyDirection.normalized : Vector2.right;
            if (flySpeed > 0f) speed = flySpeed;
            if (scale > 0f)
            {
                fireballScale = scale;
                transform.localScale = Vector3.one * fireballScale;
            }
            if (damage >= 0f) directHitDamage = damage;
            if (burnTime > 0f) burnDuration = burnTime;
            if (burnDmg >= 0f) burnDamagePerTick = burnDmg;

            UpdateRotationAndDirection();
        }

        public void Initialize(Transform casterTransform, Vector2 flyDirection, float flySpeed = -1f, float scale = -1f, float damage = -1f, float burnTime = -1f, float burnDmg = -1f)
        {
            caster = casterTransform;
            Initialize(flyDirection, flySpeed, scale, damage, burnTime, burnDmg);
        }

        private void UpdateRotationAndDirection()
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            // Ориентация эмиттера искр назад от направления полёта
            if (sparksParticleSystem != null)
            {
                var shape = sparksParticleSystem.shape;
                shape.rotation = new Vector3(0f, 0f, 180f);
            }
        }

        private void Update()
        {
            if (isDying) return;

            // 1. Движение вперед
            Vector3 delta = (Vector3)(direction * speed * Time.deltaTime);
            transform.position += delta;

            currentLifetime += Time.deltaTime;
            if (currentLifetime >= lifetime)
            {
                Dissipate();
                return;
            }

            // 2. Живая пульсация пламени фаербола
            AnimateFireballVisuals();

            // 3. Динамическое мерцание 2D света (Light2D)
            AnimateDynamicLight();
        }

        private void AnimateFireballVisuals()
        {
            float t = Time.time * pulseSpeed;
            float pulse = 1f + Mathf.Sin(t) * pulseAmount;
            float pulseCos = 1f + Mathf.Cos(t * 1.3f) * (pulseAmount * 1.2f);

            if (coreTransform != null)
            {
                coreTransform.localScale = initialCoreScale * pulse;
                coreTransform.Rotate(0f, 0f, 90f * Time.deltaTime);
            }

            if (auraTransform != null)
            {
                auraTransform.localScale = initialAuraScale * pulseCos;
                auraTransform.Rotate(0f, 0f, -135f * Time.deltaTime);
            }
        }

        private void AnimateDynamicLight()
        {
            if (!enableDynamicLight || light2DComp == null || lightIntensityProp == null) return;

            // Природное пламенное мерцание (Perlin noise + быстрый тремор)
            float flicker = Mathf.PerlinNoise(Time.time * 18f, 0f) * 0.45f - 0.22f;
            float microFlicker = (Mathf.Sin(Time.time * 42f) * 0.1f);
            float targetIntensity = Mathf.Max(0.5f, baseLightIntensity + flicker + microFlicker);

            try
            {
                lightIntensityProp.SetValue(light2DComp, targetIntensity, null);
            }
            catch { }
        }

        private void InitializeLight2D()
        {
            if (!enableDynamicLight) return;

            var lightType = System.Type.GetType("UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.2D.Runtime");
            if (lightType == null) return;

            light2DComp = GetComponent(lightType);
            if (light2DComp == null)
            {
                light2DComp = gameObject.AddComponent(lightType);
            }

            try
            {
                var colorProp = lightType.GetProperty("color");
                var outerRadiusProp = lightType.GetProperty("pointLightOuterRadius");
                var innerRadiusProp = lightType.GetProperty("pointLightInnerRadius");
                lightIntensityProp = lightType.GetProperty("intensity");

                if (colorProp != null) colorProp.SetValue(light2DComp, lightColor, null);
                if (outerRadiusProp != null) outerRadiusProp.SetValue(light2DComp, lightOuterRadius, null);
                if (innerRadiusProp != null) innerRadiusProp.SetValue(light2DComp, 0.4f, null);
                if (lightIntensityProp != null) lightIntensityProp.SetValue(light2DComp, baseLightIntensity, null);
            }
            catch { }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isDying) return;

            // Игнорируем кастера и триггеры зон
            if (other.isTrigger) return;
            if (caster != null && (other.transform == caster || other.transform.IsChildOf(caster))) return;

            // Если фаербол враждебен к игроку (снаряд Роберта)
            if (isHostileToPlayer)
            {
                var player = other.GetComponent<Combat.Player.PlayerHealth2D>() ?? other.GetComponentInParent<Combat.Player.PlayerHealth2D>();
                if (player != null && !player.IsDead)
                {
                    hitTarget = true;
                    // Включаем смертность (Роберт должен убивать)
                    player.CanDie = true;

                    Vector2 knockback = (direction + Vector2.down * 0.85f).normalized * 9.0f;
                    Combat.AttackConfig hostileAttack = new Combat.AttackConfig
                    {
                        attackName = "Огненный шар Роберта",
                        damage = directHitDamage,
                        knockbackForce = new Vector2(Mathf.Abs(knockback.x), Mathf.Abs(knockback.y)),
                        targetedZones = Combat.CombatZone.Mid
                    };
                    player.TakeHit(hostileAttack, Combat.CombatZone.Mid, other.transform.position, knockback.normalized);
                    Dissipate();
                    return;
                }
            }
            else
            {
                if (other.CompareTag("Player")) return;
            }

            // 1. Попадание во врага
            var enemy = other.GetComponentInParent<Combat.EnemyAIController2D>();
            if (enemy != null && enemy.CurrentState != Combat.EnemyState.Dead)
            {
                hitTarget = true;
                // Прямой урон фаербола (50 HP)
                enemy.TakeDirectDamage(directHitDamage, direction * 4f);

                // Накладываем эффект горения (3 сек по 5 урона)
                BurnStatusEffect.Apply(
                    enemy.gameObject,
                    burnDuration,
                    burnDamagePerTick,
                    burnTickInterval,
                    burnSpreadRadius,
                    burnOutlineMaterial,
                    burnParticlesPrefab
                );

                Dissipate();
                return;
            }

            // 1b. Попадание в тренировочный манекен (для Обучения)
            var dummy = other.GetComponentInParent<Combat.CombatDummy2D>();
            if (dummy != null && !dummy.IsDead)
            {
                hitTarget = true;
                dummy.SendMessage("TakeDamage", directHitDamage, SendMessageOptions.DontRequireReceiver);
                OnAnyFireballHitDummy?.Invoke(this, dummy);
                Dissipate();
                return;
            }

            // 2. Проверяем маску столкновений с твердыми объектами (стены, пол)
            if (((1 << other.gameObject.layer) & hitLayers) != 0)
            {
                Dissipate();
            }
        }

        /// <summary>
        /// Рассеивание фаербола при попадании или окончании времени жизни:
        /// Сгусток пламени плавно гаснет, искры догорают и падают, объект удаляется.
        /// </summary>
        public void Dissipate()
        {
            if (isDying) return;
            isDying = true;
            speed = 0f;

            if (Combat.Audio.SoundManager.Instance != null)
            {
                Combat.Audio.SoundManager.Instance.PlayFireEffect(0.9f);
            }

            if (!hitTarget)
            {
                OnAnyFireballMissed?.Invoke(this);
            }

            StartCoroutine(DissipateRoutine());
        }

        private IEnumerator DissipateRoutine()
        {
            // Отключаем новые частицы, позволяя уже вылетевшим искрам естественно упасть и догореть
            if (sparksParticleSystem != null)
            {
                var emission = sparksParticleSystem.emission;
                emission.enabled = false;
            }
            if (trailParticleSystem != null)
            {
                var emission = trailParticleSystem.emission;
                emission.enabled = false;
            }

            // Быстрое угасание ядра и свечения
            float duration = 0.25f;
            float elapsed = 0f;

            Vector3 startScale = transform.localScale;
            Color coreCol = coreRenderer != null ? coreRenderer.color : Color.white;
            Color auraCol = auraRenderer != null ? auraRenderer.color : Color.white;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;

                transform.localScale = Vector3.Lerp(startScale, startScale * 1.3f, progress);

                if (coreRenderer != null)
                {
                    coreRenderer.color = Color.Lerp(coreCol, Color.clear, progress);
                }
                if (auraRenderer != null)
                {
                    auraRenderer.color = Color.Lerp(auraCol, Color.clear, progress);
                }
                if (light2DComp != null && lightIntensityProp != null)
                {
                    try
                    {
                        lightIntensityProp.SetValue(light2DComp, Mathf.Lerp(baseLightIntensity, 0f, progress), null);
                    }
                    catch { }
                }

                yield return null;
            }

            // Прячем визуал ядра
            if (coreTransform != null) coreTransform.gameObject.SetActive(false);
            if (auraTransform != null) auraTransform.gameObject.SetActive(false);

            // Ждем завершения искр перед удалением объекта
            yield return new WaitForSeconds(1.0f);
            Destroy(gameObject);
        }
    }
}
