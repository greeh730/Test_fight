using System.Collections;
using UnityEngine;
using Combat;
using Combat.Common;
using Combat.Player;

namespace Combat.Props
{
    /// <summary>
    /// Деревянная разрушаемая бочка для уровней.
    /// Реализует IHurtboxTarget2D, поэтому разрушается от ударов мечом игрока
    /// (включая верхний, средний и нижний удары, прыжки и комбо), а также от магии и снарядов.
    /// При разрушении выбрасывает лечебное зелье, осколки дерева и воспроизводит эффекты.
    /// Количество лечения зелья настраивается индивидуально для каждой бочки в Инспекторе.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class DestructibleBarrel : MonoBehaviour, IHurtboxTarget2D
    {
        [Header("--- Содержимое: Лечебное Зелье ---")]
        [Tooltip("Количество очков здоровья (HP), которое восстановит выпадающее зелье из этой конкретной бочки")]
        [SerializeField] private float healAmount = 30f;

        [Tooltip("Префаб лечебного зелья")]
        [SerializeField] private GameObject potionPrefab;

        [Tooltip("Выпадает ли зелье при уничтожении бочки")]
        [SerializeField] private bool dropPotion = true;

        [Tooltip("Шанс выпадения зелья (1.0 = 100%)")]
        [Range(0f, 1f)]
        [SerializeField] private float dropChance = 1.0f;

        [Header("--- Прочность бочки ---")]
        [Tooltip("Количество здоровья бочки (1 = разрушается с 1 любого удара)")]
        [SerializeField] private float maxHealth = 1f;

        [Header("--- Аудио и визуальные эффекты ---")]
        [Tooltip("Звук разрушения бочки")]
        [SerializeField] private AudioClip breakSound;

        [Tooltip("Звук попадания по бочке")]
        [SerializeField] private AudioClip hitSound;

        [Tooltip("Спрайт деревянного осколка для эффекта разлета")]
        [SerializeField] private Sprite splinterSprite;

        [Tooltip("Количество разлетающихся осколков дерева")]
        [SerializeField] private int splinterCount = 6;

        private float _currentHealth;
        private bool _isDestroyed = false;
        private SpriteRenderer _sr;
        private Coroutine _wobbleRoutine;

        public float HealAmount
        {
            get => healAmount;
            set => healAmount = Mathf.Max(1f, value);
        }

        public bool DropPotion
        {
            get => dropPotion;
            set => dropPotion = value;
        }

        private void Awake()
        {
            _currentHealth = maxHealth;
            _sr = GetComponentInChildren<SpriteRenderer>();
        }

        /// <summary>
        /// Реализация интерфейса получения урона IHurtboxTarget2D от меча игрока
        /// </summary>
        public void TakeHit(AttackConfig attack, CombatZone hitZone, Vector2 hitPoint, Vector2 knockbackDirection)
        {
            if (_isDestroyed) return;

            float damage = attack != null ? attack.damage : 15f;
            Vector2 kbDir = knockbackDirection != Vector2.zero ? knockbackDirection : Vector2.up;
            ApplyDamage(damage, kbDir);
        }

        /// <summary>
        /// Альтернативный метод получения урона (для SendMessage или прямых вызовов снарядов)
        /// </summary>
        public void TakeDamage(float damage)
        {
            if (_isDestroyed) return;
            ApplyDamage(damage, Vector2.up);
        }

        private void ApplyDamage(float damage, Vector2 knockbackDir)
        {
            _currentHealth -= damage;

            if (_currentHealth <= 0f)
            {
                BreakBarrel(knockbackDir);
            }
            else
            {
                if (hitSound != null)
                {
                    AudioSource.PlayClipAtPoint(hitSound, transform.position, 0.8f);
                }

                if (_wobbleRoutine != null) StopCoroutine(_wobbleRoutine);
                _wobbleRoutine = StartCoroutine(WobbleRoutine());
            }
        }

        /// <summary>
        /// Процесс разрушения бочки: спавн осколков, вылет зелья и удаление
        /// </summary>
        public void BreakBarrel(Vector2 knockbackDir)
        {
            if (_isDestroyed) return;
            _isDestroyed = true;

            Vector3 spawnPos = transform.position;

            // 1. Воспроизведение звука разрушения дерева
            if (breakSound != null)
            {
                AudioSource.PlayClipAtPoint(breakSound, spawnPos, 1.0f);
            }
            else
            {
                PlayProceduralWoodBreakSound(spawnPos);
            }

            // 2. Создание физических разлетающихся осколков дерева
            SpawnSplinters(spawnPos, knockbackDir);

            // 3. Выпадение лечебного зелья
            if (dropPotion && (Random.value <= dropChance))
            {
                SpawnHealingPotion(spawnPos, knockbackDir);
            }

            // 4. Отключение коллизий и рендера бочки, затем уничтожение
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
            if (_sr != null) _sr.enabled = false;

            Destroy(gameObject, 0.05f);
        }

        private void SpawnHealingPotion(Vector3 pos, Vector2 knockbackDir)
        {
            GameObject potionObj = null;

            if (potionPrefab != null)
            {
                potionObj = Instantiate(potionPrefab, pos + Vector3.up * 0.25f, Quaternion.identity);
            }
            else
            {
                potionObj = CreateProceduralPotion(pos + Vector3.up * 0.25f);
            }

            if (potionObj != null)
            {
                var potion = potionObj.GetComponent<HealingPotionPickup>();
                if (potion != null)
                {
                    // Передаем настроенное значение лечения в зелье!
                    potion.SetHealAmount(healAmount);
                }

                var rb = potionObj.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    float forceX = (knockbackDir.x != 0 ? Mathf.Sign(knockbackDir.x) : (Random.value > 0.5f ? 1f : -1f)) * Random.Range(1.2f, 2.4f);
                    float forceY = Random.Range(3.8f, 5.2f);
                    rb.linearVelocity = new Vector2(forceX, forceY);
                }
            }
        }

        private void SpawnSplinters(Vector3 center, Vector2 impactDir)
        {
            int count = Mathf.Max(4, splinterCount);
            Material litMat = _sr != null ? _sr.sharedMaterial : null;

            for (int i = 0; i < count; i++)
            {
                var splinterGo = new GameObject("Barrel_Splinter");
                splinterGo.transform.position = center + (Vector3)(Random.insideUnitCircle * 0.25f);
                splinterGo.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
                splinterGo.transform.localScale = Vector3.one * Random.Range(0.7f, 1.3f);

                var splinterSr = splinterGo.AddComponent<SpriteRenderer>();
                splinterSr.sprite = splinterSprite != null ? splinterSprite : CombatSprites.WhiteBox;
                if (litMat != null) splinterSr.sharedMaterial = litMat;
                splinterSr.color = new Color(0.55f + Random.Range(-0.1f, 0.1f), 0.35f + Random.Range(-0.06f, 0.06f), 0.18f, 1f);
                splinterSr.sortingOrder = _sr != null ? _sr.sortingOrder + 1 : 15;

                var rb = splinterGo.AddComponent<Rigidbody2D>();
                rb.gravityScale = 3.5f;

                float dirBias = impactDir.x != 0 ? Mathf.Sign(impactDir.x) * 1.5f : 0f;
                Vector2 vel = new Vector2(Random.Range(-3.5f, 3.5f) + dirBias, Random.Range(3f, 7f));
                rb.linearVelocity = vel;
                rb.angularVelocity = Random.Range(-720f, 720f);

                var boxCol = splinterGo.AddComponent<BoxCollider2D>();
                boxCol.size = new Vector2(0.2f, 0.2f);

                Destroy(splinterGo, Random.Range(1.5f, 2.2f));
            }
        }

        private GameObject CreateProceduralPotion(Vector3 pos)
        {
            var go = new GameObject("HealingPotion");
            go.transform.position = pos;
            var potion = go.AddComponent<HealingPotionPickup>();
            potion.SetHealAmount(healAmount);
            return go;
        }

        private IEnumerator WobbleRoutine()
        {
            if (_sr == null) yield break;

            Vector3 origPos = transform.localPosition;
            Color origColor = _sr.color;
            _sr.color = new Color(1f, 0.8f, 0.8f, 1f);

            float elapsed = 0f;
            float duration = 0.14f;

            while (elapsed < duration)
            {
                float offset = Mathf.Sin(elapsed * 60f) * 0.06f;
                transform.localPosition = origPos + new Vector3(offset, 0f, 0f);
                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.localPosition = origPos;
            _sr.color = origColor;
            _wobbleRoutine = null;
        }

        private void PlayProceduralWoodBreakSound(Vector3 pos)
        {
            // Процедурный щелчок раскалывающегося дерева при отсутствии внешнего аудиофайла
            var sfxObj = new GameObject("Temp_WoodBreakSFX");
            sfxObj.transform.position = pos;
            var audio = sfxObj.AddComponent<AudioSource>();
            audio.spatialBlend = 0.5f;
            audio.volume = 0.85f;
            audio.pitch = Random.Range(0.85f, 1.15f);

            int sampleRate = 22050;
            float duration = 0.18f;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 26f);
                // Комбинация низкого удара и резкого древесного шума
                float lowThud = Mathf.Sin(2f * Mathf.PI * 130f * t) * 0.7f;
                float noise = (Random.value * 2f - 1f) * 0.5f;
                samples[i] = (lowThud + noise) * envelope;
            }

            var clip = AudioClip.Create("WoodCrack", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            audio.PlayOneShot(clip);
            Destroy(sfxObj, 0.5f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.6f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.35f);
        }
    }
}
