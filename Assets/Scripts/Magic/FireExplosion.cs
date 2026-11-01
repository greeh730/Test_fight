using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Combat;
using Combat.Player;
using Combat.Cameras;

namespace Magic
{
    /// <summary>
    /// Эффект и механика взрыва (эпицентр при провале заклинания Огня):
    /// - Воспроизводит огненную ударную волну, вспышку света и взрыв частиц.
    /// - Наносит 50 урона (настраивается) всем находящимся рядом целям и отбрасывает их.
    /// - Запускает кинематографичную тряску камеры.
    /// </summary>
    [DisallowMultipleComponent]
    public class FireExplosion : MonoBehaviour
    {
        [Header("--- Параметры урона и радиуса ---")]
        [Tooltip("Урон от взрыва по окружающим целям")]
        [SerializeField] private float damage = 50f;

        [Tooltip("Радиус поражения взрыва")]
        [SerializeField] private float radius = 3.0f;

        [Tooltip("Сила импульса отталкивания от эпицентра")]
        [SerializeField] private float knockbackForce = 8f;

        [Tooltip("Накладывать ли также эффект горения на попавших под взрыв")]
        [SerializeField] private bool applyBurn = true;

        [Tooltip("Длительность поджога от взрыва")]
        [SerializeField] private float burnDuration = 3f;

        [Header("--- Тряска камеры ---")]
        [SerializeField] private bool triggerCameraShake = true;
        [SerializeField] private float shakeDuration = 0.35f;
        [SerializeField] private float shakeIntensity = 0.5f;

        [Header("--- Ссылки на визуал ---")]
        [SerializeField] private Transform shockwaveTransform;
        [SerializeField] private SpriteRenderer shockwaveRenderer;
        [SerializeField] private Transform coreTransform;
        [SerializeField] private SpriteRenderer coreRenderer;
        [SerializeField] private ParticleSystem explosionParticles;

        private Component light2DComp;
        private System.Reflection.PropertyInfo lightIntensityProp;

        public float Damage { get => damage; set => damage = Mathf.Max(0f, value); }
        public float Radius { get => radius; set => radius = Mathf.Max(0.5f, value); }
        public float KnockbackForce { get => knockbackForce; set => knockbackForce = value; }

        public static readonly List<FireExplosion> ActiveExplosions = new List<FireExplosion>();

        private void OnEnable()
        {
            if (!ActiveExplosions.Contains(this)) ActiveExplosions.Add(this);
        }

        private void OnDisable()
        {
            ActiveExplosions.Remove(this);
        }

        private void OnDestroy()
        {
            ActiveExplosions.Remove(this);
        }

        private void Awake()
        {
            if (!ActiveExplosions.Contains(this)) ActiveExplosions.Add(this);
            InitializeLight2D();
        }

        private void Start()
        {
            // 0. Звук взрыва и огня через SoundManager
            if (Combat.Audio.SoundManager.Instance != null)
            {
                Combat.Audio.SoundManager.Instance.PlayExplosion();
                Combat.Audio.SoundManager.Instance.PlayFireEffect(1.0f);
            }

            // 1. Тряска камеры
            if (triggerCameraShake && Camera.main != null)
            {
                var camFollow = Camera.main.GetComponent<CameraFollow2D>();
                if (camFollow != null)
                {
                    camFollow.Shake(shakeDuration, shakeIntensity);
                }
            }

            // 2. Анимация ударной волны и вспышки
            StartCoroutine(ExplosionSequenceRoutine());
        }

        /// <summary>
        /// Инициализация взрыва с кастомными параметрами.
        /// </summary>
        public void Explode(float customDamage, float customRadius, float customKnockback, GameObject sourceToIgnore = null)
        {
            damage = customDamage;
            radius = customRadius;
            knockbackForce = customKnockback;

            DealExplosionDamage(sourceToIgnore);
        }

        /// <summary>
        /// Нанесение урона окружающим врагам и сущностям.
        /// </summary>
        public void DealExplosionDamage(GameObject sourceToIgnore = null)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
            var processed = new HashSet<GameObject>();

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D col = hits[i];
                if (col == null || col.isTrigger) continue;

                var enemy = col.GetComponentInParent<EnemyAIController2D>();
                if (enemy != null && enemy.CurrentState != EnemyState.Dead)
                {
                    if (processed.Contains(enemy.gameObject)) continue;
                    processed.Add(enemy.gameObject);

                    if (sourceToIgnore != null && enemy.gameObject == sourceToIgnore) continue;

                    Vector2 dir = (enemy.transform.position - transform.position);
                    Vector2 knockbackDir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector2.up;

                    enemy.TakeDirectDamage(damage, knockbackDir * knockbackForce);

                    if (applyBurn)
                    {
                        BurnStatusEffect.Apply(enemy.gameObject, burnDuration, 5f);
                    }

                    Debug.Log($"<color=#FF3300>[ВЗРЫВ]</color> Враг <b>{enemy.name}</b> получил {damage} урона от взрыва в эпицентре!");
                }
            }
        }

        private IEnumerator ExplosionSequenceRoutine()
        {
            if (explosionParticles != null)
            {
                explosionParticles.Play();
            }

            float duration = 0.35f;
            float elapsed = 0f;

            Vector3 startCoreScale = coreTransform != null ? coreTransform.localScale : Vector3.one * 0.5f;
            Vector3 targetCoreScale = startCoreScale * 2.5f;

            Vector3 startShockwaveScale = Vector3.one * 0.2f;
            Vector3 targetShockwaveScale = Vector3.one * (radius * 2.2f);

            Color coreStartColor = coreRenderer != null ? coreRenderer.color : Color.white;
            Color shockwaveStartColor = shockwaveRenderer != null ? shockwaveRenderer.color : Color.white;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                float easeOut = 1f - Mathf.Pow(1f - progress, 3f);

                // Расширение ядра и ударной волны
                if (coreTransform != null) coreTransform.localScale = Vector3.Lerp(startCoreScale, targetCoreScale, easeOut);
                if (shockwaveTransform != null) shockwaveTransform.localScale = Vector3.Lerp(startShockwaveScale, targetShockwaveScale, easeOut);

                // Затухание альфы
                if (coreRenderer != null) coreRenderer.color = Color.Lerp(coreStartColor, Color.clear, progress);
                if (shockwaveRenderer != null) shockwaveRenderer.color = Color.Lerp(shockwaveStartColor, Color.clear, progress);

                // Вспышка света
                SetLightIntensity(Mathf.Lerp(5.0f, 0f, progress));

                yield return null;
            }

            if (coreTransform != null) coreTransform.gameObject.SetActive(false);
            if (shockwaveTransform != null) shockwaveTransform.gameObject.SetActive(false);

            yield return new WaitForSeconds(0.7f);
            Destroy(gameObject);
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

                colorProp?.SetValue(light2DComp, new Color(1.0f, 0.7f, 0.25f, 1f), null);
                outerRadiusProp?.SetValue(light2DComp, radius * 2.2f, null);
                innerRadiusProp?.SetValue(light2DComp, 0.4f, null);
                lightIntensityProp?.SetValue(light2DComp, 5.0f, null);
            }
            catch { }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
