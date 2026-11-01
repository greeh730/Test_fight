using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Combat;
using Combat.Player;

namespace Magic
{
    /// <summary>
    /// Огненная частица/уголёк, вылетающая из игрока при провале заклинания:
    /// 1. В полёте: летит по дуге под действием гравитации, поджигает врагов при прямом попадании.
    /// 2. На земле: при падении остаётся гореть на полу в течение 3 секунд (настраивается).
    ///    Любой, кто наступит в этот огонь (враг или сам игрок), получает эффект горения на 3 секунды.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class FireEmber : MonoBehaviour
    {
        private enum EmberState
        {
            Flying,
            BurningOnGround,
            Extinguishing
        }

        [Header("--- Параметры горения на земле ---")]
        [Tooltip("Время горения частицы на земле в секундах")]
        [SerializeField] private float groundFireDuration = 3f;

        [Tooltip("Длительность эффекта горения при наступлении в огонь")]
        [SerializeField] private float burnEffectDuration = 3f;

        [Tooltip("Периодический урон от поджога")]
        [SerializeField] private float burnDamagePerTick = 5f;

        [Tooltip("Урон от прямого попадания летящей частицы")]
        [SerializeField] private float directHitDamage = 10f;

        [Tooltip("Слои земли/платформ для приземления")]
        [SerializeField] private LayerMask groundLayers = ~0;

        [Header("--- Ссылки на визуал ---")]
        [SerializeField] private SpriteRenderer sparkRenderer;
        [SerializeField] private ParticleSystem groundFlameParticles;
        [SerializeField] private ParticleSystem flightTrailParticles;

        private EmberState state = EmberState.Flying;
        private Rigidbody2D rb;
        private CircleCollider2D col;
        private Component light2DComp;
        private System.Reflection.PropertyInfo lightIntensityProp;
        private float groundTimer = 0f;
        private float flightTimer = 0f;
        private readonly HashSet<GameObject> recentlyIgnited = new HashSet<GameObject>();

        public float GroundFireDuration { get => groundFireDuration; set => groundFireDuration = Mathf.Max(0.5f, value); }
        public float BurnEffectDuration { get => burnEffectDuration; set => burnEffectDuration = Mathf.Max(0.5f, value); }
        public float BurnDamagePerTick { get => burnDamagePerTick; set => burnDamagePerTick = Mathf.Max(0f, value); }

        public static readonly List<FireEmber> ActiveEmbers = new List<FireEmber>();

        private void OnEnable()
        {
            if (!ActiveEmbers.Contains(this)) ActiveEmbers.Add(this);
        }

        private void OnDisable()
        {
            ActiveEmbers.Remove(this);
        }

        private void OnDestroy()
        {
            ActiveEmbers.Remove(this);
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<CircleCollider2D>();
            col.isTrigger = true;

            InitializeLight2D();
        }

        /// <summary>
        /// Запуск частицы в полёт с начальной скоростью.
        /// </summary>
        public void Launch(Vector2 initialVelocity, float groundDuration = 3f, float burnDuration = 3f, float burnDmg = 5f, LayerMask? mask = null)
        {
            if (!ActiveEmbers.Contains(this)) ActiveEmbers.Add(this);
            groundFireDuration = groundDuration;
            burnEffectDuration = burnDuration;
            burnDamagePerTick = burnDmg;
            if (mask.HasValue) groundLayers = mask.Value;

            state = EmberState.Flying;
            flightTimer = 0f;

            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 2.4f;
                rb.linearVelocity = initialVelocity;
            }

            if (flightTrailParticles != null) flightTrailParticles.Play();
            if (groundFlameParticles != null) groundFlameParticles.Stop();
        }

        private void Update()
        {
            if (state == EmberState.Flying)
            {
                flightTimer += Time.deltaTime;
                if (flightTimer > 6f)
                {
                    Destroy(gameObject);
                    return;
                }

                // Вращение искры по направлению полёта
                if (rb != null && rb.linearVelocity.sqrMagnitude > 0.1f)
                {
                    float angle = Mathf.Atan2(rb.linearVelocity.y, rb.linearVelocity.x) * Mathf.Rad2Deg;
                    transform.rotation = Quaternion.Euler(0f, 0f, angle);
                }

                // Дополнительная проверка на пол лучами вниз (чтобы не пролететь тонкий коллайдер)
                CheckGroundRaycast();
            }
            else if (state == EmberState.BurningOnGround)
            {
                groundTimer += Time.deltaTime;

                // Мерцание 2D света
                AnimateGroundLight();

                if (groundTimer >= groundFireDuration)
                {
                    Extinguish();
                }
            }
        }

        private void CheckGroundRaycast()
        {
            if (rb == null) return;
            Vector2 origin = transform.position;
            Vector2 vel = rb.linearVelocity;
            float checkDist = Mathf.Max(0.35f, vel.magnitude * Time.deltaTime * 1.5f);

            RaycastHit2D hit = Physics2D.Raycast(origin, vel.normalized, checkDist, groundLayers);
            if (hit.collider != null && !hit.collider.isTrigger && !hit.collider.CompareTag("Player") && !hit.collider.CompareTag("Enemy"))
            {
                LandOnGround(hit.point, hit.normal);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.isTrigger) return;

            if (state == EmberState.Flying)
            {
                // Попадание во врага во время полёта
                var enemy = other.GetComponentInParent<EnemyAIController2D>();
                if (enemy != null && enemy.CurrentState != EnemyState.Dead)
                {
                    enemy.TakeDirectDamage(directHitDamage, rb != null ? rb.linearVelocity.normalized * 2f : Vector2.up);
                    BurnStatusEffect.Apply(enemy.gameObject, burnEffectDuration, burnDamagePerTick);
                    Debug.Log($"<color=#FF5500>[Огонь]</color> Летящий уголёк попал во врага <b>{enemy.name}</b>!");
                    LandOnGround(transform.position, Vector2.up);
                    return;
                }

                // Столкновение с землёй/платформой
                if (((1 << other.gameObject.layer) & groundLayers) != 0 && !other.CompareTag("Player"))
                {
                    LandOnGround(transform.position, Vector2.up);
                }
            }
            else if (state == EmberState.BurningOnGround)
            {
                // Любой, кто наступает в огонь на земле (враг или игрок) — загорается
                TryIgniteCharacter(other);
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (state == EmberState.BurningOnGround)
            {
                TryIgniteCharacter(other);
            }
        }

        private void TryIgniteCharacter(Collider2D other)
        {
            if (other.isTrigger) return;

            GameObject targetObj = null;

            // 1. Проверяем врага
            var enemy = other.GetComponentInParent<EnemyAIController2D>();
            if (enemy != null && enemy.CurrentState != EnemyState.Dead)
            {
                targetObj = enemy.gameObject;
            }
            else
            {
                // 2. Проверяем игрока
                var player = other.GetComponentInParent<PlayerHealth2D>();
                if (player != null && !player.IsDead)
                {
                    targetObj = player.gameObject;
                }
            }

            if (targetObj != null && !recentlyIgnited.Contains(targetObj))
            {
                recentlyIgnited.Add(targetObj);
                BurnStatusEffect.Apply(targetObj, burnEffectDuration, burnDamagePerTick);
                Debug.Log($"<color=#FF3300>[Огонь на земле]</color> Персонаж <b>{targetObj.name}</b> наступил в огонь и загорелся!");

                // Снимаем ограничение через 1.5 сек, чтобы можно было поджечься снова, если огонь ещё горит
                StartCoroutine(ClearIgnitedCacheRoutine(targetObj, 1.5f));
            }
        }

        private IEnumerator ClearIgnitedCacheRoutine(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (recentlyIgnited.Contains(obj))
            {
                recentlyIgnited.Remove(obj);
            }
        }

        /// <summary>
        /// Приземление частицы на поверхность и переход в режим стационарного огня.
        /// </summary>
        public void LandOnGround(Vector2 contactPoint, Vector2 normal)
        {
            if (state != EmberState.Flying) return;
            state = EmberState.BurningOnGround;
            groundTimer = 0f;

            // Фиксация на поверхности
            transform.position = contactPoint + normal * 0.08f;
            transform.rotation = Quaternion.identity;

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.simulated = false; // Отключаем динамику
            }

            // Увеличиваем радиус триггера для комфортного наступления в огонь
            if (col != null)
            {
                col.radius = 0.55f;
            }

            // Переключаем визуал: скрываем полётную искру, запускаем костерок пламени на полу
            if (sparkRenderer != null) sparkRenderer.enabled = false;
            if (flightTrailParticles != null)
            {
                var emission = flightTrailParticles.emission;
                emission.enabled = false;
            }

            if (groundFlameParticles != null)
            {
                groundFlameParticles.Play();
            }

            if (Combat.Audio.SoundManager.Instance != null)
            {
                Combat.Audio.SoundManager.Instance.PlayFireEffect(0.75f);
            }

            SetLightIntensity(1.6f);
        }

        /// <summary>
        /// Затухание костра на земле по истечении 3 секунд.
        /// </summary>
        private void Extinguish()
        {
            if (state == EmberState.Extinguishing) return;
            state = EmberState.Extinguishing;

            StartCoroutine(ExtinguishRoutine());
        }

        private IEnumerator ExtinguishRoutine()
        {
            if (col != null) col.enabled = false;

            if (groundFlameParticles != null)
            {
                var emission = groundFlameParticles.emission;
                emission.enabled = false;
            }

            float duration = 0.4f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                SetLightIntensity(Mathf.Lerp(1.6f, 0f, progress));
                yield return null;
            }

            yield return new WaitForSeconds(0.6f);
            Destroy(gameObject);
        }

        private void AnimateGroundLight()
        {
            if (light2DComp == null || lightIntensityProp == null) return;
            float flicker = Mathf.PerlinNoise(Time.time * 20f, transform.position.x) * 0.5f - 0.25f;
            SetLightIntensity(1.6f + flicker);
        }

        private void SetLightIntensity(float val)
        {
            if (light2DComp != null && lightIntensityProp != null)
            {
                try { lightIntensityProp.SetValue(light2DComp, val, null); } catch { }
            }
        }

        private void InitializeLight2D()
        {
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

                colorProp?.SetValue(light2DComp, new Color(1.0f, 0.5f, 0.1f, 1f), null);
                outerRadiusProp?.SetValue(light2DComp, 3.5f, null);
                innerRadiusProp?.SetValue(light2DComp, 0.2f, null);
                lightIntensityProp?.SetValue(light2DComp, 1.4f, null);
            }
            catch { }
        }
    }
}
