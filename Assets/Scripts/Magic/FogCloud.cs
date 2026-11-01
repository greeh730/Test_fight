// Assets/Scripts/Magic/FogCloud.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Magic
{
    /// <summary>
    /// Облако тумана / дыма (Fog Cloud):
    /// - Создается при успешном применении заклинания "Туман".
    /// - Стилизовано под геометрический дым из полупрозрачных прямоугольников разного размера.
    /// - Прямоугольники имеют случайную прозрачность и слегка пошатываются (дрейф позиции и наклон угла).
    /// - Расположено на уровне тела игрока и имеет оптимальный размер чуть больше персонажа (радиус ~1.85 м).
    /// - Перекрывает прямую видимость врагов: при сокрытии враги теряют игрока и переходят в патрулирование.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class FogCloud : MonoBehaviour
    {
        #region Static Registry & Occlusion Query
        /// <summary>
        /// Список всех активных облаков тумана в сцене.
        /// </summary>
        public static readonly List<FogCloud> ActiveClouds = new List<FogCloud>();

        /// <summary>
        /// Проверяет, скрыт ли игрок от взгляда врага (находится внутри тумана или за туманом).
        /// </summary>
        public static bool IsPlayerObscured(Vector2 enemyPos, Vector2 playerPos)
        {
            // Если активен полноэкранный густой дым на UI — видимость полностью перекрыта
            if (DenseFogUI.IsDenseFogActive) return true;

            if (ActiveClouds.Count == 0) return false;

            for (int i = 0; i < ActiveClouds.Count; i++)
            {
                var cloud = ActiveClouds[i];
                if (cloud == null || !cloud.gameObject.activeInHierarchy || !cloud.IsActive)
                    continue;

                if (cloud.BlocksVision(enemyPos, playerPos))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Проверяет, находится ли точка (например, враг) внутри хотя бы одного активного облака тумана.
        /// </summary>
        public static bool IsInsideAnyCloud(Vector2 point)
        {
            if (ActiveClouds.Count == 0) return false;

            for (int i = 0; i < ActiveClouds.Count; i++)
            {
                var cloud = ActiveClouds[i];
                if (cloud == null || !cloud.gameObject.activeInHierarchy || !cloud.IsActive)
                    continue;

                if (cloud.ContainsPoint(point))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Принудительно проверяет и поджигает любые облака тумана в радиусе огненной точки.
        /// </summary>
        public static bool CheckAndIgniteCloudsAt(Vector2 firePosition, float fireRadius = 0.5f)
        {
            bool hitAny = false;
            for (int i = 0; i < ActiveClouds.Count; i++)
            {
                var cloud = ActiveClouds[i];
                if (cloud == null || !cloud.gameObject.activeInHierarchy || !cloud.IsActive || cloud._isBurningByFire)
                    continue;

                float totalRadius = cloud.Radius + fireRadius;
                if (((Vector2)cloud.transform.position - firePosition).sqrMagnitude <= totalRadius * totalRadius)
                {
                    cloud.DissipateByFire(cloud.fireDissipateDuration);
                    hitAny = true;
                }
            }
            return hitAny;
        }
        #endregion

        [Header("--- Параметры размера и длительности ---")]
        [Tooltip("Радиус зоны сокрытия тумана в метрах (чуть больше тела игрока, ~1.85 м)")]
        [SerializeField] private float radius = 1.85f;

        [Tooltip("Общая длительность существования тумана в секундах")]
        [SerializeField] private float duration = 7.0f;

        [Tooltip("Время быстрого раскрытия тумана")]
        [SerializeField] private float fadeInDuration = 0.15f;

        [Tooltip("Время плавного рассеивания тумана")]
        [SerializeField] private float fadeOutDuration = 1.1f;

        [Header("--- Настройки прямоугольников дыма ---")]
        [Tooltip("Цвет прямоугольников дыма (белый/пепельный)")]
        [SerializeField] private Color fogColor = new Color(0.94f, 0.96f, 1.0f, 1f);

        [Tooltip("Минимальная случайная прозрачность отдельного прямоугольника")]
        [Range(0.05f, 1f)]
        [SerializeField] private float minAlpha = 0.18f;

        [Tooltip("Максимальная случайная прозрачность отдельного прямоугольника")]
        [Range(0.1f, 1f)]
        [SerializeField] private float maxAlpha = 0.58f;

        [Tooltip("Амплитуда покачивания (пошатывания) прямоугольников")]
        [SerializeField] private float swayIntensity = 1.0f;

        [Header("--- Реакция на огонь ---")]
        [Tooltip("Время полного исчезновения дыма при контакте с огнем в секундах")]
        [SerializeField] private float fireDissipateDuration = 1.0f;

        [Tooltip("Цвет вспышки / тления прямоугольников при сгорании")]
        [SerializeField] private Color fireBurnColor = new Color(1.0f, 0.48f, 0.12f, 1f);

        [Tooltip("Рендереры прямоугольников облака")]
        [SerializeField] private SpriteRenderer[] cloudRenderers;

        private float[] _primitiveBaseAlphas;
        private Vector3[] _primitiveBasePositions;
        private Vector3[] _primitiveBaseScales;
        private float[] _primitiveBaseRotations;
        private float[] _primitiveSpeedMultipliers;
        private float[] _primitiveRotSpeedMultipliers;

        private bool _isActive = true;
        private bool _isBurningByFire = false;
        private Coroutine _lifecycleRoutine;
        private float _seed;

        #region Properties
        public float Radius => radius;
        public float Duration => duration;
        public bool IsActive => _isActive;
        #endregion

        private void OnEnable()
        {
            if (!ActiveClouds.Contains(this))
            {
                ActiveClouds.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveClouds.Remove(this);
        }

        private void OnDestroy()
        {
            ActiveClouds.Remove(this);
        }

        private void Awake()
        {
            _seed = UnityEngine.Random.Range(0f, 100f);
            InitializePrimitives();

            if (Combat.Audio.SoundManager.Instance != null)
            {
                Combat.Audio.SoundManager.Instance.PlaySmoke(0.95f);
            }
        }

        private void Start()
        {
            if (Application.isPlaying && _lifecycleRoutine == null)
            {
                _lifecycleRoutine = StartCoroutine(FogLifecycleRoutine());
            }
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (!_isActive || _isBurningByFire) return;

            CheckForFireContact();
        }

        /// <summary>
        /// Проверяет контакт с любыми источниками огня: фаербол, искры/огонь на земле, горящие сущности, взрывы.
        /// При контакте дым исчезает ровно через 1 секунду. При этом сами снаряды и взрывы НЕ тушатся.
        /// </summary>
        private void CheckForFireContact()
        {
            Vector2 cloudPos = (Vector2)transform.position;
            float contactRadius = radius + 0.35f;
            float contactRadiusSqr = contactRadius * contactRadius;

            // Очищаем списки от уничтоженных объектов
            FireballProjectile.ActiveProjectiles.RemoveAll(x => x == null);
            FireEmber.ActiveEmbers.RemoveAll(x => x == null);
            BurnStatusEffect.ActiveEffects.RemoveAll(x => x == null);
            FireExplosion.ActiveExplosions.RemoveAll(x => x == null);

            // 1. Фаербол (снаряд летит сквозь дым)
            for (int i = 0; i < FireballProjectile.ActiveProjectiles.Count; i++)
            {
                var fb = FireballProjectile.ActiveProjectiles[i];
                if (fb != null && fb.gameObject.activeInHierarchy)
                {
                    if (((Vector2)fb.transform.position - cloudPos).sqrMagnitude <= contactRadiusSqr)
                    {
                        Debug.Log("<color=#FFA500>[Туман]</color> Фаербол коснулся дыма! Дым сгорает и исчезает через 1 секунду (снаряд не тушится).");
                        DissipateByFire(fireDissipateDuration);
                        return;
                    }
                }
            }

            // 2. Горящие частицы и огонь на земле
            for (int i = 0; i < FireEmber.ActiveEmbers.Count; i++)
            {
                var ember = FireEmber.ActiveEmbers[i];
                if (ember != null && ember.gameObject.activeInHierarchy)
                {
                    if (((Vector2)ember.transform.position - cloudPos).sqrMagnitude <= contactRadiusSqr)
                    {
                        Debug.Log("<color=#FFA500>[Туман]</color> Огонь коснулся дыма! Дым сгорает и исчезает через 1 секунду.");
                        DissipateByFire(fireDissipateDuration);
                        return;
                    }
                }
            }

            // 3. Горящие цели (эффект горения на игроке или враге)
            for (int i = 0; i < BurnStatusEffect.ActiveEffects.Count; i++)
            {
                var burn = BurnStatusEffect.ActiveEffects[i];
                if (burn != null && burn.gameObject.activeInHierarchy && burn.RemainingDuration > 0f)
                {
                    if (((Vector2)burn.transform.position - cloudPos).sqrMagnitude <= contactRadiusSqr)
                    {
                        Debug.Log("<color=#FFA500>[Туман]</color> Горящая цель в дыму! Дым сгорает и исчезает через 1 секунду.");
                        DissipateByFire(fireDissipateDuration);
                        return;
                    }
                }
            }

            // 4. Взрывы
            for (int i = 0; i < FireExplosion.ActiveExplosions.Count; i++)
            {
                var expl = FireExplosion.ActiveExplosions[i];
                if (expl != null && expl.gameObject.activeInHierarchy)
                {
                    float totalR = radius + expl.Radius;
                    if (((Vector2)expl.transform.position - cloudPos).sqrMagnitude <= totalR * totalR)
                    {
                        Debug.Log("<color=#FFA500>[Туман]</color> Взрыв задел дым! Дым сгорает и исчезает через 1 секунду (взрыв продолжается).");
                        DissipateByFire(fireDissipateDuration);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Инициализирует массивы случайной прозрачности, позиций, масштабов и поворотов для прямоугольников.
        /// </summary>
        public void InitializePrimitives()
        {
            if (cloudRenderers == null || cloudRenderers.Length == 0)
            {
                cloudRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            }

            int count = cloudRenderers != null ? cloudRenderers.Length : 0;
            if (count == 0) return;

            _primitiveBaseAlphas = new float[count];
            _primitiveBasePositions = new Vector3[count];
            _primitiveBaseScales = new Vector3[count];
            _primitiveBaseRotations = new float[count];
            _primitiveSpeedMultipliers = new float[count];
            _primitiveRotSpeedMultipliers = new float[count];

            for (int i = 0; i < count; i++)
            {
                if (cloudRenderers[i] == null) continue;

                // Случайная базовая прозрачность для каждого прямоугольника
                _primitiveBaseAlphas[i] = UnityEngine.Random.Range(minAlpha, maxAlpha);
                _primitiveBasePositions[i] = cloudRenderers[i].transform.localPosition;
                _primitiveBaseScales[i] = cloudRenderers[i].transform.localScale;
                _primitiveBaseRotations[i] = cloudRenderers[i].transform.localEulerAngles.z;
                _primitiveSpeedMultipliers[i] = UnityEngine.Random.Range(0.8f, 1.4f);
                _primitiveRotSpeedMultipliers[i] = UnityEngine.Random.Range(0.7f, 1.3f);

                // Применяем стартовый цвет (сразу задаем рабочую прозрачность, чтобы игрок мгновенно скрылся за дымом)
                Color c = fogColor;
                c.a = _primitiveBaseAlphas[i];
                cloudRenderers[i].color = c;
            }
        }

        /// <summary>
        /// Инициализирует параметры тумана при спавне из заклинания.
        /// </summary>
        public void Initialize(float customDuration, float customRadius, float customMinAlpha = -1f, float customMaxAlpha = -1f)
        {
            if (!ActiveClouds.Contains(this))
            {
                ActiveClouds.Add(this);
            }

            duration = Mathf.Max(1.0f, customDuration);
            radius = Mathf.Max(0.5f, customRadius);

            if (customMinAlpha > 0f && customMaxAlpha > 0f)
            {
                minAlpha = Mathf.Clamp01(customMinAlpha);
                maxAlpha = Mathf.Clamp01(customMaxAlpha);
            }

            InitializePrimitives();

            if (Combat.Audio.SoundManager.Instance != null)
            {
                Combat.Audio.SoundManager.Instance.PlaySmoke(0.95f);
            }

            if (Application.isPlaying)
            {
                if (_lifecycleRoutine != null)
                {
                    StopCoroutine(_lifecycleRoutine);
                }
                _lifecycleRoutine = StartCoroutine(FogLifecycleRoutine());
            }
        }

        /// <summary>
        /// Проверяет, блокирует ли данное облако линию обзора между двумя точками (from: враг, to: игрок).
        /// Если враг (from) сам находится внутри облака — облако НЕ скрывает от него цель!
        /// </summary>
        public bool BlocksVision(Vector2 from, Vector2 to)
        {
            if (!_isActive) return false;

            Vector2 center = (Vector2)transform.position;
            float rSqr = radius * radius;

            // Если наблюдатель (враг) сам зашел в это облако тумана — туман не скрывает от него цель!
            if ((from - center).sqrMagnitude <= rSqr)
            {
                return false;
            }

            // 1. Игрок внутри радиуса тумана (а враг снаружи) — игрок скрыт туманом
            if ((to - center).sqrMagnitude <= rSqr) return true;

            // 2. Отрезок взгляда пересекает круг тумана
            Vector2 ab = to - from;
            float l2 = ab.sqrMagnitude;
            if (l2 < 0.0001f)
            {
                return (from - center).sqrMagnitude <= rSqr;
            }

            float t = Mathf.Clamp01(Vector2.Dot(center - from, ab) / l2);
            Vector2 closestPoint = from + t * ab;

            return (closestPoint - center).sqrMagnitude <= rSqr;
        }

        /// <summary>
        /// Находится ли точка внутри радиуса тумана.
        /// </summary>
        public bool ContainsPoint(Vector2 point)
        {
            if (!_isActive) return false;
            return ((Vector2)transform.position - point).sqrMagnitude <= (radius * radius);
        }

        private IEnumerator FogLifecycleRoutine()
        {
            _isActive = true;
            Vector3 targetRootScale = Vector3.one;
            Vector3 startRootScale = Vector3.one * 0.85f;

            // 1. Быстрое раскрытие (игрок мгновенно скрыт за дымом)
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / fadeInDuration);
                float ease = Mathf.SmoothStep(0f, 1f, progress);

                transform.localScale = Vector3.Lerp(startRootScale, targetRootScale, ease);
                ApplyAlphaMultiplier(Mathf.Lerp(0.85f, 1.0f, ease));
                UpdateGentleDrift(elapsed);
                yield return null;
            }

            transform.localScale = targetRootScale;
            ApplyAlphaMultiplier(1.0f);

            // 2. Активное состояние с живым пошатыванием прямоугольников
            float lingerDuration = Mathf.Max(0.1f, duration - fadeInDuration - fadeOutDuration);
            float lingerElapsed = 0f;

            while (lingerElapsed < lingerDuration)
            {
                lingerElapsed += Time.deltaTime;
                UpdateGentleDrift(fadeInDuration + lingerElapsed);
                yield return null;
            }

            // 3. Плавное рассеивание (Fade-out & легкое расширение)
            elapsed = 0f;
            Vector3 dissipateScale = targetRootScale * 1.15f;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / fadeOutDuration);
                float ease = Mathf.SmoothStep(0f, 1f, progress);

                transform.localScale = Vector3.Lerp(targetRootScale, dissipateScale, ease);
                ApplyAlphaMultiplier(1f - ease);
                UpdateGentleDrift(fadeInDuration + lingerDuration + elapsed);

                // За 35% до конца туман теряет маскирующие свойства
                if (progress >= 0.65f && _isActive)
                {
                    _isActive = false;
                }

                yield return null;
            }

            _isActive = false;
            ActiveClouds.Remove(this);
            Destroy(gameObject);
        }

        /// <summary>
        /// Запускает процесс быстрого сгорания и исчезновения дыма за 1 секунду при контакте с огнем.
        /// </summary>
        public void DissipateByFire(float customDuration = 1.0f)
        {
            if (_isBurningByFire) return;
            _isBurningByFire = true;

            if (Combat.Audio.SoundManager.Instance != null)
            {
                Combat.Audio.SoundManager.Instance.PlayFireEffect(0.9f);
            }

            if (_lifecycleRoutine != null)
            {
                StopCoroutine(_lifecycleRoutine);
                _lifecycleRoutine = null;
            }

            StartCoroutine(BurnDissipateRoutine(Mathf.Max(0.2f, customDuration)));
        }

        private IEnumerator BurnDissipateRoutine(float duration)
        {
            float elapsed = 0f;
            Vector3 startScale = transform.localScale;
            Vector3 burnScale = startScale * 1.25f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float ease = Mathf.SmoothStep(0f, 1f, progress);

                // Окрашиваем прямоугольники в цвет тлеющих огненных углей
                Color currentColor = Color.Lerp(fogColor, fireBurnColor, Mathf.Clamp01(progress * 1.8f));
                float alphaFactor = 1f - ease;
                ApplyAlphaMultiplierWithColor(alphaFactor, currentColor);

                // Тепловое расширение и легкий подъем дыма вверх
                transform.localScale = Vector3.Lerp(startScale, burnScale, ease);
                transform.position += new Vector3(0f, 0.4f * Time.deltaTime, 0f);

                UpdateGentleDrift(elapsed * 2.2f);

                // Через половину времени горения дым истончается и перестает скрывать персонажа
                if (progress >= 0.5f && _isActive)
                {
                    _isActive = false;
                }

                yield return null;
            }

            _isActive = false;
            ActiveClouds.Remove(this);
            Destroy(gameObject);
        }

        private void ApplyAlphaMultiplierWithColor(float factor, Color tintColor)
        {
            if (cloudRenderers == null || _primitiveBaseAlphas == null) return;

            for (int i = 0; i < cloudRenderers.Length; i++)
            {
                if (cloudRenderers[i] == null || i >= _primitiveBaseAlphas.Length) continue;

                Color c = tintColor;
                c.a = factor * _primitiveBaseAlphas[i];
                cloudRenderers[i].color = c;
            }
        }

        /// <summary>
        /// Применяет общий множитель прозрачности к каждому прямоугольнику с сохранением его уникальной случайной прозрачности.
        /// </summary>
        private void ApplyAlphaMultiplier(float factor)
        {
            if (cloudRenderers == null || _primitiveBaseAlphas == null) return;

            for (int i = 0; i < cloudRenderers.Length; i++)
            {
                if (cloudRenderers[i] == null || i >= _primitiveBaseAlphas.Length) continue;

                Color c = fogColor;
                c.a = factor * _primitiveBaseAlphas[i];
                cloudRenderers[i].color = c;
            }
        }

        /// <summary>
        /// Плавное пошатывание прямоугольников: смещение позиции, легкий наклон угла и пульсация размера.
        /// </summary>
        private void UpdateGentleDrift(float time)
        {
            if (cloudRenderers == null || _primitiveBasePositions == null) return;

            for (int i = 0; i < cloudRenderers.Length; i++)
            {
                if (cloudRenderers[i] == null || i >= _primitiveBasePositions.Length) continue;

                float speed = _primitiveSpeedMultipliers != null && i < _primitiveSpeedMultipliers.Length ? _primitiveSpeedMultipliers[i] : 1.0f;
                float rotSpeed = _primitiveRotSpeedMultipliers != null && i < _primitiveRotSpeedMultipliers.Length ? _primitiveRotSpeedMultipliers[i] : 1.0f;

                // 1. Пошатывание позиции (плавное покачивание влево-вправо и вверх-вниз)
                float offsetX = Mathf.Sin((time * speed * 1.3f) + (i * 1.6f) + _seed) * (0.09f * swayIntensity);
                float offsetY = Mathf.Cos((time * speed * 1.0f) + (i * 2.2f) + _seed) * (0.07f * swayIntensity);
                cloudRenderers[i].transform.localPosition = _primitiveBasePositions[i] + new Vector3(offsetX, offsetY, 0f);

                // 2. Пошатывание угла (наклон туда-сюда на 5-8 градусов)
                if (_primitiveBaseRotations != null && i < _primitiveBaseRotations.Length)
                {
                    float rotTilt = Mathf.Sin((time * rotSpeed * 1.5f) + (i * 1.9f) + _seed) * (6.5f * swayIntensity);
                    cloudRenderers[i].transform.localRotation = Quaternion.Euler(0f, 0f, _primitiveBaseRotations[i] + rotTilt);
                }

                // 3. Легкое дыхание размера (пульсация ±5%)
                if (_primitiveBaseScales != null && i < _primitiveBaseScales.Length)
                {
                    float scaleBreath = 1.0f + Mathf.Sin((time * speed * 1.6f) + (i * 0.8f)) * (0.05f * swayIntensity);
                    cloudRenderers[i].transform.localScale = _primitiveBaseScales[i] * scaleBreath;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.85f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
