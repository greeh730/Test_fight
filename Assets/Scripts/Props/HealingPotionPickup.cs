using System.Collections;
using UnityEngine;
using Combat.Common;
using Combat.Player;
using UnityEngine.Rendering.Universal;

namespace Combat.Props
{
    /// <summary>
    /// Предмет лечебного зелья, выпадающий из разрушенной бочки.
    /// Физически приземляется на пол/платформы уровня и остаётся на земле.
    /// Может быть подобран как Игроком, так и Врагами (скелетами), восстанавливая заданное количество HP.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public class HealingPotionPickup : MonoBehaviour
    {
        [Header("--- Настройки лечения ---")]
        [Tooltip("Количество восстанавливаемого здоровья")]
        [SerializeField] private float healAmount = 30f;

        [Tooltip("Минимальная задержка перед возможностью подбора (дает зелью вылететь из бочки)")]
        [SerializeField] private float pickupDelay = 0.25f;

        [Header("--- Визуал и анимация ---")]
        [Tooltip("Амплитуда плавного парения вверх-вниз")]
        [SerializeField] private float bobbingAmplitude = 0.07f;

        [Tooltip("Скорость плавного парения")]
        [SerializeField] private float bobbingSpeed = 4f;

        [Tooltip("Цвет всплывающего боевого текста при исцелении игрока")]
        [SerializeField] private Color playerHealColor = new Color(0.2f, 1f, 0.45f, 1f);

        [Tooltip("Цвет всплывающего боевого текста при исцелении врага")]
        [SerializeField] private Color enemyHealColor = new Color(1f, 0.4f, 0.3f, 1f);

        [Header("--- Аудио ---")]
        [Tooltip("Звук подбора лечебного зелья")]
        [SerializeField] private AudioClip pickupSound;

        private Rigidbody2D _rb;
        private SpriteRenderer _sr;
        private Light2D _light;
        private bool _isCollected = false;
        private float _spawnTime;
        private Vector3 _basePos;
        private bool _isSettled = false;

        public float HealAmount => healAmount;

        public void SetHealAmount(float amount)
        {
            healAmount = Mathf.Max(1f, amount);
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _sr = GetComponentInChildren<SpriteRenderer>();
            _light = GetComponentInChildren<Light2D>();
            _spawnTime = Time.time;

            if (_rb != null)
            {
                _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }
        }

        private void Start()
        {
            StartCoroutine(CheckPickupOverlapRoutine());
        }

        private void Update()
        {
            if (_isCollected) return;

            // Проверка приземления на поверхность земли/тайлов
            if (!_isSettled && _rb != null)
            {
                // Если зелье летит вниз или почти остановилось
                if (_rb.linearVelocity.y <= 0.1f && (Time.time - _spawnTime > 0.15f))
                {
                    // Пускаем короткий луч вниз для гарантированного обнаружения пола/тайлов
                    var hits = Physics2D.RaycastAll(transform.position, Vector2.down, 0.45f);
                    for (int i = 0; i < hits.Length; i++)
                    {
                        var hit = hits[i];
                        if (hit.collider == null || hit.collider.isTrigger || hit.collider.transform.IsChildOf(transform)) continue;

                        // Нашли твердый пол / платформу!
                        SettleOnGround(hit.point.y + 0.22f);
                        break;
                    }
                }
            }

            if (_isSettled)
            {
                float newY = _basePos.y + Mathf.Sin((Time.time - _spawnTime) * bobbingSpeed) * bobbingAmplitude;
                transform.position = new Vector3(_basePos.x, newY, transform.position.z);
            }

            // Мягкая пульсация рубинового света
            if (_light != null)
            {
                _light.intensity = 0.85f + Mathf.Sin(Time.time * 6f) * 0.25f;
            }
        }

        private void SettleOnGround(float groundY)
        {
            _isSettled = true;
            transform.position = new Vector3(transform.position.x, groundY, transform.position.z);
            _basePos = transform.position;

            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.bodyType = RigidbodyType2D.Kinematic;
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!_isSettled && collision.collider != null && !collision.collider.isTrigger)
            {
                // При столкновении с полом (контакт снизу)
                foreach (var contact in collision.contacts)
                {
                    if (contact.normal.y > 0.6f)
                    {
                        SettleOnGround(contact.point.y + 0.22f);
                        break;
                    }
                }
            }

            TryCollect(collision.gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryCollect(other.gameObject);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryCollect(other.gameObject);
        }

        private IEnumerator CheckPickupOverlapRoutine()
        {
            yield return new WaitForSeconds(pickupDelay);

            while (!_isCollected)
            {
                var hits = Physics2D.OverlapCircleAll(transform.position, 0.65f);
                for (int i = 0; i < hits.Length; i++)
                {
                    if (TryCollect(hits[i].gameObject))
                    {
                        yield break;
                    }
                }
                yield return new WaitForSeconds(0.08f);
            }
        }

        private bool TryCollect(GameObject target)
        {
            if (_isCollected) return false;
            if (Time.time - _spawnTime < pickupDelay) return false;
            if (target == null || target == gameObject || target.transform.IsChildOf(transform)) return false;

            // 1. Проверяем Игрока
            var player = target.GetComponentInParent<PlayerHealth2D>();
            if (player == null && target.CompareTag("Player"))
            {
                player = target.GetComponent<PlayerHealth2D>();
            }

            if (player != null && !player.IsDead)
            {
                CollectForPlayer(player);
                return true;
            }

            // 2. Проверяем Врага
            var enemy = target.GetComponentInParent<EnemyAIController2D>();
            if (enemy == null && target.CompareTag("Enemy"))
            {
                enemy = target.GetComponent<EnemyAIController2D>();
            }

            if (enemy != null && !enemy.IsDead)
            {
                CollectForEnemy(enemy);
                return true;
            }

            return false;
        }

        private void CollectForPlayer(PlayerHealth2D player)
        {
            _isCollected = true;

            float before = player.CurrentHealth;
            player.Heal(healAmount);
            float actualHealed = player.CurrentHealth - before;

            string healText = actualHealed > 0 ? $"+{actualHealed:F0} HP" : $"+{healAmount:F0} HP";
            CombatFloatingText.Spawn(
                player.transform.position + Vector3.up * 1.1f,
                healText,
                playerHealColor,
                1.1f,
                1.15f,
                0.09f,
                46,
                85
            );

            FinishCollect(transform.position, playerHealColor);
        }

        private void CollectForEnemy(EnemyAIController2D enemy)
        {
            _isCollected = true;

            float before = enemy.CurrentHealth;
            enemy.Heal(healAmount);
            float actualHealed = enemy.CurrentHealth - before;

            string healText = actualHealed > 0 ? $"ВРАГ: +{actualHealed:F0} HP" : $"ВРАГ: +{healAmount:F0} HP";
            CombatFloatingText.Spawn(
                enemy.transform.position + Vector3.up * 1.5f,
                healText,
                enemyHealColor,
                1.2f,
                1.2f,
                0.09f,
                44,
                85
            );

            FinishCollect(transform.position, enemyHealColor);
        }

        private void FinishCollect(Vector3 pos, Color sparkColor)
        {
            // Звук
            if (pickupSound != null)
            {
                AudioSource.PlayClipAtPoint(pickupSound, pos, 0.9f);
            }
            else
            {
                PlayProceduralHealChime(pos);
            }

            // Частицы
            SpawnHealSparkles(pos, sparkColor);

            // Отключаем визуал и коллайдеры
            if (_sr != null) _sr.enabled = false;
            if (_light != null) _light.enabled = false;

            var cols = GetComponents<Collider2D>();
            for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;

            Destroy(gameObject, 0.1f);
        }

        private void SpawnHealSparkles(Vector3 pos, Color color)
        {
            int count = 8;
            for (int i = 0; i < count; i++)
            {
                var spark = new GameObject("Heal_Sparkle");
                spark.transform.position = pos + (Vector3)(Random.insideUnitCircle * 0.25f);
                var sr = spark.AddComponent<SpriteRenderer>();
                sr.sprite = CombatSprites.WhiteBox;
                sr.color = color;
                sr.sortingOrder = 90;

                spark.transform.localScale = Vector3.one * Random.Range(0.08f, 0.15f);

                var rb = spark.AddComponent<Rigidbody2D>();
                rb.gravityScale = -0.5f;
                rb.linearVelocity = new Vector2(Random.Range(-1.4f, 1.4f), Random.Range(1.6f, 3.4f));

                Destroy(spark, 0.55f);
            }
        }

        private void PlayProceduralHealChime(Vector3 pos)
        {
            var sfxObj = new GameObject("Temp_HealChimeSFX");
            sfxObj.transform.position = pos;
            var audio = sfxObj.AddComponent<AudioSource>();
            audio.volume = 0.75f;

            int sampleRate = 22050;
            float duration = 0.35f;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 9f);
                float tone = (Mathf.Sin(2f * Mathf.PI * 523.25f * t) * 0.5f)
                           + (Mathf.Sin(2f * Mathf.PI * 659.25f * t) * 0.35f)
                           + (Mathf.Sin(2f * Mathf.PI * 783.99f * t) * 0.25f);
                samples[i] = tone * env * 0.45f;
            }

            var clip = AudioClip.Create("HealChime", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            audio.PlayOneShot(clip);
            Destroy(sfxObj, 0.6f);
        }
    }
}
