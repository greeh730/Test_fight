using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Combat.Common;

namespace Combat
{
    /// <summary>
    /// Компонент сочной визуальной и физической обратной связи при получении урона тренировочным манекеном:
    /// - Качание/наклон на шарнирной пружине (Spring Wobble) от направления удара
    /// - Сжатие и растяжение (Squash & Stretch)
    /// - Белая вспышка урона (Damage Flash)
    /// - Разлёт деревянных щепок и опилок (Wood Splinters & Sawdust Burst)
    /// - Всплывающий текст нанесённого урона с цветом поражённой зоны
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public class DummyDamageFeedback : MonoBehaviour
    {
        [Header("--- Spring Wobble Settings ---")]
        [Tooltip("Базовый угол отклонения манекена при ударе (градусы)")]
        [SerializeField] private float baseTiltAngle = 18f;

        [Tooltip("Частота колебаний пружины манекена")]
        [SerializeField] private float wobbleFrequency = 24f;

        [Tooltip("Скорость затухания колебаний")]
        [SerializeField] private float wobbleDamping = 6f;

        [Tooltip("Длительность анимации колебания (сек)")]
        [SerializeField] private float wobbleDuration = 0.7f;

        [Header("--- Squash & Stretch ---")]
        [SerializeField] private Vector3 squashScale = new Vector3(1.18f, 0.82f, 1.0f);
        [SerializeField] private Vector3 stretchScale = new Vector3(0.92f, 1.10f, 1.0f);
        [SerializeField] private float squashDuration = 0.06f;
        [SerializeField] private float stretchDuration = 0.08f;

        [Header("--- Flash Settings ---")]
        [SerializeField] private Color hitFlashColor = new Color(1f, 0.22f, 0.22f, 1f);
        [SerializeField] private float flashDuration = 0.08f;

        [Header("--- Particles ---")]
        [SerializeField] private int minParticles = 8;
        [SerializeField] private int maxParticles = 14;

        private SpriteRenderer _sr;
        private Coroutine _wobbleRoutine;
        private Coroutine _flashRoutine;
        private Coroutine _squashRoutine;

        private Vector3 _initialLocalScale = Vector3.one;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private Vector3 _spawnBaseWorld;
        private float _baseOffset = 1.0f; // Расстояние от центра до опорного основания манекена

        private static Sprite s_PixelSprite;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _initialLocalScale = transform.localScale;
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
            _spawnBaseWorld = _spawnPosition + new Vector3(0f, -_baseOffset, 0f);

            EnsurePixelSprite();
        }

        private static void EnsurePixelSprite()
        {
            if (s_PixelSprite != null) return;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            s_PixelSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        /// <summary>
        /// Запускает полный комплекс обратной связи при попадании атаки по манекену.
        /// </summary>
        public void PlayHitFeedback(Vector2 hitPoint, Vector2 knockbackDir, float damage, CombatZone zone)
        {
            // 1. Вспышка урона
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(HitFlashRoutine(zone));

            // 2. Сжатие и растяжение (Squash & Stretch)
            if (_squashRoutine != null) StopCoroutine(_squashRoutine);
            _squashRoutine = StartCoroutine(SquashAndStretchRoutine());

            // 3. Качание манекена на пружине (Spring Wobble)
            float tiltSign = knockbackDir.x >= 0f ? -1f : 1f;
            float tiltIntensity = Mathf.Clamp(damage * 0.5f + baseTiltAngle, 12f, 26f);
            float startAngle = tiltSign * tiltIntensity;

            if (_wobbleRoutine != null) StopCoroutine(_wobbleRoutine);
            _wobbleRoutine = StartCoroutine(SpringWobbleRoutine(startAngle));

            // 4. Разлёт белых частиц удара и щепок
            Combat.Common.CombatHitSparks.SpawnWhiteSparks(hitPoint, knockbackDir, 12);
            SpawnWoodSplinters(hitPoint, knockbackDir, zone);

            // 5. Всплывающий урон с подсветкой зоны
            Color zoneColor = zone.GetZoneColor(1f);
            CombatFloatingText.Spawn(
                (Vector3)hitPoint + new Vector3(0f, 0.45f, 0f),
                $"-{damage:F0}",
                zoneColor,
                0.75f,
                0.65f,
                0.078f,
                38,
                80
            );

            // 6. Звуковой эффект удара по дереву
            if (Combat.Audio.SoundManager.Instance != null)
            {
                Combat.Audio.SoundManager.Instance.PlayEnemyHit();
            }
        }

        private IEnumerator HitFlashRoutine(CombatZone zone)
        {
            if (_sr == null) yield break;

            Color baseColor = _sr.color;
            _sr.color = hitFlashColor;

            yield return new WaitForSeconds(flashDuration);

            // Краткий переход через цвет зоны
            Color zoneColor = zone.GetZoneColor(1f);
            float blendElapsed = 0f;
            float blendDuration = 0.08f;
            while (blendElapsed < blendDuration)
            {
                blendElapsed += Time.deltaTime;
                float t = blendElapsed / blendDuration;
                if (_sr != null) _sr.color = Color.Lerp(zoneColor, Color.white, t);
                yield return null;
            }

            if (_sr != null) _sr.color = Color.white;
            _flashRoutine = null;
        }

        private IEnumerator SquashAndStretchRoutine()
        {
            // Этап 1: Сжатие (Squash)
            float elapsed = 0f;
            while (elapsed < squashDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / squashDuration);
                transform.localScale = Vector3.Lerp(_initialLocalScale, Vector3.Scale(_initialLocalScale, squashScale), t);
                yield return null;
            }

            // Этап 2: Отскок в растяжение (Stretch)
            elapsed = 0f;
            Vector3 currentScale = transform.localScale;
            Vector3 targetStretch = Vector3.Scale(_initialLocalScale, stretchScale);
            while (elapsed < stretchDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / stretchDuration);
                transform.localScale = Vector3.Lerp(currentScale, targetStretch, t);
                yield return null;
            }

            // Этап 3: Плавный возврат в норму
            elapsed = 0f;
            float returnDuration = 0.12f;
            currentScale = transform.localScale;
            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / returnDuration);
                transform.localScale = Vector3.Lerp(currentScale, _initialLocalScale, t);
                yield return null;
            }

            transform.localScale = _initialLocalScale;
            _squashRoutine = null;
        }

        private IEnumerator SpringWobbleRoutine(float initialAngle)
        {
            float elapsed = 0f;

            while (elapsed < wobbleDuration)
            {
                elapsed += Time.deltaTime;

                // Затухающая гармоническая синусоида (Damped Harmonic Oscillator)
                float decay = Mathf.Exp(-wobbleDamping * elapsed);
                float angle = initialAngle * decay * Mathf.Cos(wobbleFrequency * elapsed);

                ApplyPivotRotation(angle);
                yield return null;
            }

            // Фиксация в исходное положение
            ApplyPivotRotation(0f);
            _wobbleRoutine = null;
        }

        private void ApplyPivotRotation(float angle)
        {
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            // Смещаем позицию так, чтобы опора в (0, -_baseOffset) оставалась приколоченной к полу
            Vector3 currentBase = transform.position + transform.rotation * new Vector3(0f, -_baseOffset, 0f);
            transform.position += (_spawnBaseWorld - currentBase);
        }

        private void SpawnWoodSplinters(Vector2 hitPoint, Vector2 knockbackDir, CombatZone zone)
        {
            EnsurePixelSprite();
            if (s_PixelSprite == null) return;

            int count = Random.Range(minParticles, maxParticles + 1);
            float baseDirX = knockbackDir.x >= 0f ? 1f : -1f;

            Color[] woodColors = new Color[]
            {
                new Color(0.42f, 0.25f, 0.14f, 1f), // Тёмный дуб
                new Color(0.68f, 0.44f, 0.26f, 1f), // Тёплая древесина
                new Color(0.88f, 0.65f, 0.38f, 1f), // Сосновая стружка
                new Color(0.96f, 0.85f, 0.58f, 1f), // Золотистая солома
                zone.GetZoneColor(0.85f)             // Искра зоны попадания
            };

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Dummy_Splinter");
                go.hideFlags = HideFlags.DontSave;
                go.transform.position = (Vector3)hitPoint + new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(-0.15f, 0.15f), 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = s_PixelSprite;
                sr.color = woodColors[Random.Range(0, woodColors.Length)];
                sr.sortingOrder = _sr != null ? _sr.sortingOrder + 3 : 50;

                // Размер щепки (прямоугольные пиксельные фрагменты)
                float sizeX = Random.Range(0.06f, 0.13f);
                float sizeY = Random.Range(0.04f, 0.08f);
                go.transform.localScale = new Vector3(sizeX, sizeY, 1f);

                // Случайный вектор разлёта: вперед по удару + фонтаном вверх
                Vector2 velocity = new Vector2(
                    baseDirX * Random.Range(2.5f, 6.0f) + Random.Range(-1.5f, 1.5f),
                    Random.Range(3.5f, 7.5f)
                );
                float angularSpeed = Random.Range(-600f, 600f);
                float lifetime = Random.Range(0.35f, 0.55f);

                var runner = go.AddComponent<SplinterRunner>();
                runner.Initialize(velocity, angularSpeed, lifetime, sr);
            }
        }

        private class SplinterRunner : MonoBehaviour
        {
            private Vector2 _velocity;
            private float _angularSpeed;
            private float _lifetime;
            private float _elapsed;
            private SpriteRenderer _sr;
            private Color _startColor;
            private const float Gravity = -18f;

            public void Initialize(Vector2 velocity, float angularSpeed, float lifetime, SpriteRenderer sr)
            {
                _velocity = velocity;
                _angularSpeed = angularSpeed;
                _lifetime = Mathf.Max(0.1f, lifetime);
                _sr = sr;
                _startColor = sr != null ? sr.color : Color.white;
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_elapsed / _lifetime);

                _velocity.y += Gravity * Time.deltaTime;
                transform.position += (Vector3)(_velocity * Time.deltaTime);
                transform.Rotate(0f, 0f, _angularSpeed * Time.deltaTime);

                if (_sr != null)
                {
                    Color c = _startColor;
                    c.a = Mathf.Clamp01(1f - (t * t));
                    _sr.color = c;
                }

                if (_elapsed >= _lifetime)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
