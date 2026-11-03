using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Combat.Common;
using Combat.Player;

namespace Combat.ProceduralGeneration
{
    /// <summary>
    /// Механический капкан Траппера (Контроллера):
    /// - Устанавливается Траппером на платформы для зонирования и контроля.
    /// - В открытом состоянии взводится за 0.45с и пульсирует фиолетовым светом.
    /// - При наступании игрока: захлопывается с громким щелчком, наносит урон и накладывает [ROOT] (обездвиживание на 1.8с).
    ///   Игрок не может ходить и прыгать, но может бить мечом и парировать!
    /// - При наступании врага (например, если оттолкнуть его ударом в капкан): ловит врага в ловушку на 2.2с!
    /// - Разрушаем: игрок может сломать капкан одним ударом меча (IHurtboxTarget2D).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public class CombatTrap2D : MonoBehaviour, IHurtboxTarget2D
    {
        private static Sprite _openTrapSprite;
        private static Sprite _closedTrapSprite;
        private static Sprite _brokenTrapSprite;

        private static readonly List<CombatTrap2D> _activeTraps = new List<CombatTrap2D>();
        public static IReadOnlyList<CombatTrap2D> ActiveTraps => _activeTraps;

        private void Awake()
        {
            SetupComponents();
            RegisterTrap();
        }

        private void OnEnable()
        {
            RegisterTrap();
        }

        private void OnDisable()
        {
            UnregisterTrap();
        }

        private void OnDestroy()
        {
            UnregisterTrap();
        }

        public void RegisterTrap()
        {
            if (!_activeTraps.Contains(this))
            {
                _activeTraps.Add(this);
            }
        }

        public void UnregisterTrap()
        {
            _activeTraps.Remove(this);
        }

        public bool IsHazard => _isArmed && !_isTriggered && !_isDestroyed && gameObject.activeInHierarchy;

        /// <summary>
        /// Проверяет наличие опасного взведенного капкана на пути бота (учитывая высоту его ступней)
        /// </summary>
        public static CombatTrap2D GetHazardTrapAhead(Vector2 botFeetPos, float moveDir, float maxDist = 2.5f, float maxDy = 0.65f)
        {
            if (_activeTraps.Count == 0 || Mathf.Abs(moveDir) < 0.05f) return null;

            float dirSign = Mathf.Sign(moveDir);
            CombatTrap2D closest = null;
            float closestDist = float.MaxValue;

            for (int i = 0; i < _activeTraps.Count; i++)
            {
                var trap = _activeTraps[i];
                if (trap == null || !trap.IsHazard) continue;

                Vector2 trapPos = trap.transform.position;
                float dx = trapPos.x - botFeetPos.x;
                float dy = trapPos.y - botFeetPos.y;

                // Капкан находится впереди по курсу движения бота и на той же высоте платформы
                if (Mathf.Sign(dx) == dirSign && Mathf.Abs(dy) <= maxDy)
                {
                    float dist = Mathf.Abs(dx);
                    if (dist <= maxDist && dist < closestDist)
                    {
                        closestDist = dist;
                        closest = trap;
                    }
                }
            }

            return closest;
        }

        [Header("--- Настройки капкана ---")]
        [SerializeField] private float armDelay = 0.45f;
        [SerializeField] private float playerRootDuration = 1.8f;
        [SerializeField] private float playerTrapDamage = 12f;
        [SerializeField] private float enemyRootDuration = 2.2f;
        [SerializeField] private float enemyTrapDamage = 25f;

        private SpriteRenderer _sr;
        private BoxCollider2D _collider;
        private EnemyAIController2D _owner;
        private float _spawnTime;
        private float _groundY;
        private bool _isArmed = false;
        private bool _isTriggered = false;
        private bool _isDestroyed = false;
        private Transform _clampedTarget;

        public bool IsArmed => _isArmed;
        public bool IsTriggered => _isTriggered;
        public bool IsDestroyed => _isDestroyed;
        public EnemyAIController2D Owner => _owner;

        /// <summary>
        /// Спавн нового капкана в мире с привязкой к платформе
        /// </summary>
        public static CombatTrap2D SpawnTrap(Vector3 spawnPos, EnemyAIController2D owner)
        {
            // Проецируем луч вниз, чтобы капкан лег ровно на поверхность платформы
            Vector3 finalPos = spawnPos;
            var hit = Physics2D.Raycast(spawnPos + Vector3.up * 0.5f, Vector2.down, 2.5f, ~0);
            if (hit.collider != null && !hit.collider.isTrigger)
            {
                finalPos = hit.point;
            }

            var trapGo = new GameObject("Combat_BearTrap");
            trapGo.transform.position = finalPos;

            var trap = trapGo.AddComponent<CombatTrap2D>();
            trap._groundY = finalPos.y;
            trap.Initialize(owner);
            return trap;
        }

        public void SetupComponents()
        {
            EnsureSprites();

            if (_sr == null)
            {
                _sr = GetComponent<SpriteRenderer>();
                if (_sr == null)
                {
                    _sr = gameObject.AddComponent<SpriteRenderer>();
                }
                _sr.sprite = _openTrapSprite;
                _sr.sortingLayerName = "Default";
                _sr.sortingOrder = 65; // Под персонажами, но поверх пола
            }

            if (_collider == null)
            {
                _collider = GetComponent<BoxCollider2D>();
                if (_collider == null)
                {
                    _collider = gameObject.AddComponent<BoxCollider2D>();
                }
                _collider.isTrigger = true;
                _collider.size = new Vector2(1.15f, 0.45f);
                _collider.offset = new Vector2(0f, 0.22f);
            }
        }

        public void Initialize(EnemyAIController2D owner)
        {
            SetupComponents();
            RegisterTrap();
            _owner = owner;
            _spawnTime = Time.time;
            if (Application.isPlaying)
            {
                StartCoroutine(ArmingRoutine());
            }
            else
            {
                _isArmed = true;
            }
        }

        private IEnumerator ArmingRoutine()
        {
            // Плавное появление и взведение
            float elapsed = 0f;
            Color baseColor = new Color(0.9f, 0.6f, 1f, 0.4f);
            Color armedColor = new Color(1f, 1f, 1f, 1f);

            while (elapsed < armDelay)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / armDelay;
                if (_sr != null) _sr.color = Color.Lerp(baseColor, armedColor, t);
                yield return null;
            }

            _isArmed = true;
            if (_sr != null) _sr.color = armedColor;

            // Звук тихого щелчка взведения
            PlayArmClickSound();
        }

        private void Update()
        {
            if (_isDestroyed) return;

            // Если капкан взведён, но ещё не сработал — мягко пульсируем фиолетовой подсветкой глазков
            if (_isArmed && !_isTriggered && _sr != null)
            {
                float pulse = 0.85f + Mathf.PingPong(Time.time * 2.5f, 0.25f);
                _sr.color = new Color(pulse, 0.8f + pulse * 0.15f, 1f, 1f);
            }

            // Если капкан схватил цель — держимся строго у её ступней на уровне земли
            if (_isTriggered && _clampedTarget != null)
            {
                var targetCol = _clampedTarget.GetComponent<Collider2D>();
                float footY = targetCol != null ? targetCol.bounds.min.y : _groundY;
                transform.position = new Vector3(_clampedTarget.position.x, footY, transform.position.z);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_isArmed || _isTriggered || _isDestroyed) return;
            if (other.isTrigger) return;

            // 1. Проверка: наступил ли Игрок
            var pc = other.GetComponentInParent<PlayerController2D>();
            if (pc != null && !pc.IsHitstunned)
            {
                SnapOnPlayer(pc);
                return;
            }

            // 2. Проверка: наступил ли Враг (другой враг или если врага пнули в капкан)
            var enemy = other.GetComponentInParent<EnemyAIController2D>();
            if (enemy != null && !enemy.IsDead)
            {
                // Своего хозяина не ловим в первые 2.5 секунды после установки
                if (enemy == _owner && Time.time - _spawnTime < 2.5f) return;

                SnapOnEnemy(enemy);
                return;
            }
        }

        /// <summary>
        /// Захлопывание капкана на Игроке
        /// </summary>
        private void SnapOnPlayer(PlayerController2D pc)
        {
            _isTriggered = true;
            _clampedTarget = pc.transform;

            // Ступни игрока (низ коллайдера)
            var col = pc.GetComponent<Collider2D>();
            float footY = col != null ? col.bounds.min.y : _groundY;
            transform.position = new Vector3(pc.transform.position.x, footY, transform.position.z);

            // 1. Визуал: закрытый капкан поверх ботинок
            if (_sr != null)
            {
                _sr.sprite = _closedTrapSprite;
                _sr.sortingOrder = 25; // Челюсти смыкаются поверх ботинок/ступней
                _sr.color = Color.white;
            }
            StartCoroutine(SnapBounceRoutine());

            // 2. Звук: тяжелый металлический КЛАЦ!
            PlayHeavySnapSound();

            // 3. Искры от удара челюстей капкана у ступней
            SpawnTrapSparks(transform.position + Vector3.up * 0.15f);

            // 4. Эффект на игрока: Root (обездвиживание) + небольшой урон
            pc.ApplyRoot(playerRootDuration);

            var playerHealth = pc.GetComponent<PlayerHealth2D>();
            if (playerHealth != null)
            {
                playerHealth.TakeDirectDamage(playerTrapDamage);
            }

            // 5. Всплывающий боевой текст над головой игрока
            CombatFloatingText.Spawn(
                pc.transform.position + Vector3.up * 1.5f,
                $"⛓️ В КАПКАНЕ! ({playerRootDuration:F1}с)",
                new Color(0.95f, 0.45f, 1.0f, 1f),
                1.6f, 1.15f, 0.09f, 46, 95
            );

            Debug.Log($"<color=#B85CFF><b>[TRAP]</b></color> Игрок пойман в капкан Траппера на {playerRootDuration:F1}с!");

            // Самоуничтожение после окончания действия рута
            StartCoroutine(FadeAndDestroyRoutine(playerRootDuration));
        }

        /// <summary>
        /// Захлопывание капкана на Враге (если отбросили врага в капкан)
        /// </summary>
        private void SnapOnEnemy(EnemyAIController2D enemy)
        {
            _isTriggered = true;
            _clampedTarget = enemy.transform;

            var col = enemy.GetComponent<Collider2D>();
            float footY = col != null ? col.bounds.min.y : _groundY;
            transform.position = new Vector3(enemy.transform.position.x, footY, transform.position.z);

            if (_sr != null)
            {
                _sr.sprite = _closedTrapSprite;
                _sr.sortingOrder = 25; // Поверх ног врага
                _sr.color = Color.white;
            }
            StartCoroutine(SnapBounceRoutine());

            PlayHeavySnapSound();
            SpawnTrapSparks(transform.position + Vector3.up * 0.15f);

            // Враг обездвиживается и получает урон
            enemy.ApplyRoot(enemyRootDuration);
            enemy.TakeDirectDamage(enemyTrapDamage);
            enemy.FlashColor(new Color(0.85f, 0.4f, 1f, 1f), 0.4f);

            CombatFloatingText.Spawn(
                enemy.transform.position + Vector3.up * 1.5f,
                $"⛓️ ВРАГ В КАПКАНЕ! ({enemyRootDuration:F1}с)",
                new Color(0.95f, 0.5f, 1.0f, 1f),
                1.6f, 1.15f, 0.09f, 44, 95
            );

            Debug.Log($"<color=#B85CFF><b>[TRAP]</b></color> Враг {enemy.name} угодил в капкан на {enemyRootDuration:F1}с!");

            StartCoroutine(FadeAndDestroyRoutine(enemyRootDuration));
        }

        /// <summary>
        /// Реализация IHurtboxTarget2D: удар меча игрока ломает капкан!
        /// </summary>
        public void TakeHit(AttackConfig attack, CombatZone hitZone, Vector2 hitPoint, Vector2 knockbackDirection)
        {
            BreakTrap();
        }

        public void TakeDamage(float damage)
        {
            BreakTrap();
        }

        /// <summary>
        /// Разрушение капкана ударом игрока
        /// </summary>
        public void BreakTrap()
        {
            if (_isDestroyed || _isTriggered) return;
            _isDestroyed = true;

            // Звук разрушения металла
            PlayBreakSound();

            // Искры поломки
            SpawnTrapSparks(transform.position + Vector3.up * 0.2f);

            if (_sr != null)
            {
                _sr.sprite = _brokenTrapSprite;
                _sr.color = new Color(0.75f, 0.75f, 0.8f, 0.75f);
            }

            if (_collider != null) _collider.enabled = false;

            // Всплывающий боевой текст
            CombatFloatingText.Spawn(
                transform.position + Vector3.up * 0.85f,
                "💥 КАПКАН СЛОМАН!",
                new Color(1.0f, 0.75f, 0.25f, 1f),
                1.1f, 0.85f, 0.082f, 38, 80
            );

            Destroy(gameObject, 0.55f);
        }

        private IEnumerator SnapBounceRoutine()
        {
            Vector3 baseScale = Vector3.one;
            float dur = 0.20f;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                float bump = 1f + Mathf.Sin(t * Mathf.PI) * 0.22f;
                transform.localScale = new Vector3(baseScale.x * bump, baseScale.y * bump, baseScale.z);
                yield return null;
            }
            transform.localScale = baseScale;
        }

        private IEnumerator FadeAndDestroyRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            float fadeDur = 0.35f;
            float elapsed = 0f;
            Color startCol = _sr != null ? _sr.color : Color.white;

            while (elapsed < fadeDur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDur;
                if (_sr != null) _sr.color = new Color(startCol.r, startCol.g, startCol.b, 1f - t);
                yield return null;
            }

            Destroy(gameObject);
        }

        private void SpawnTrapSparks(Vector3 pos)
        {
            int count = 7;
            for (int i = 0; i < count; i++)
            {
                var spark = new GameObject("Trap_Spark");
                spark.hideFlags = HideFlags.DontSave;
                spark.transform.position = pos;
                var sr = spark.AddComponent<SpriteRenderer>();
                sr.sprite = CombatSprites.WhiteBox;
                sr.color = (i % 2 == 0) ? new Color(0.95f, 0.5f, 1f, 1f) : new Color(1f, 0.95f, 0.5f, 1f);
                sr.sortingOrder = 95;
                spark.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.06f, 0.12f);

                var rb = spark.AddComponent<Rigidbody2D>();
                rb.gravityScale = 2.5f;
                rb.linearVelocity = new Vector2(UnityEngine.Random.Range(-2.8f, 2.8f), UnityEngine.Random.Range(2.2f, 5.0f));

                Destroy(spark, 0.40f);
            }
        }

        private void PlayArmClickSound()
        {
            PlaySynthTone(transform.position, 880f, 0.08f, 0.45f);
        }

        private void PlayHeavySnapSound()
        {
            var sfxObj = new GameObject("Trap_SnapSFX");
            sfxObj.hideFlags = HideFlags.DontSave;
            sfxObj.transform.position = transform.position;
            var audio = sfxObj.AddComponent<AudioSource>();
            audio.spatialBlend = 0.2f;
            audio.volume = 0.95f;

            int sampleRate = 22050;
            float duration = 0.32f;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                // Тяжелый щелчок: низкий металлический стук (160Гц) + звон зубьев (740Гц) с резким спадом
                float env = Mathf.Exp(-t * 22f);
                float ringEnv = Mathf.Exp(-t * 8f);
                float snap = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 160f * t)) * env * 0.7f;
                float ring = Mathf.Sin(2f * Mathf.PI * 740f * t) * ringEnv * 0.4f;
                samples[i] = (snap + ring) * 0.85f;
            }

            var clip = AudioClip.Create("TrapSnap", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            audio.PlayOneShot(clip);
            Destroy(sfxObj, 0.55f);
        }

        private void PlayBreakSound()
        {
            PlaySynthTone(transform.position, 1200f, 0.15f, 0.65f);
        }

        private void PlaySynthTone(Vector3 pos, float freq, float duration, float vol)
        {
            var sfxObj = new GameObject("Trap_ToneSFX");
            sfxObj.hideFlags = HideFlags.DontSave;
            sfxObj.transform.position = pos;
            var audio = sfxObj.AddComponent<AudioSource>();
            audio.spatialBlend = 0.25f;
            audio.volume = vol;

            int sampleRate = 22050;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * (1f / duration * 4.5f));
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.5f;
            }

            var clip = AudioClip.Create("TrapTone", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            audio.PlayOneShot(clip);
            Destroy(sfxObj, duration + 0.1f);
        }

        #region Procedural Pixel-Art Sprites

        private static void EnsureSprites()
        {
            if (_openTrapSprite != null) return;

            _openTrapSprite = CreateOpenTrapSprite();
            _closedTrapSprite = CreateClosedTrapSprite();
            _brokenTrapSprite = CreateBrokenTrapSprite();
        }

        private static Sprite CreateOpenTrapSprite()
        {
            int w = 28;
            int h = 14;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color clear = new Color(0, 0, 0, 0);
            Color baseIron = new Color(0.24f, 0.24f, 0.28f, 1f);
            Color teethSteel = new Color(0.72f, 0.74f, 0.82f, 1f);
            Color highlight = new Color(0.92f, 0.94f, 1.0f, 1f);
            Color plateColor = new Color(0.35f, 0.35f, 0.42f, 1f);
            Color runeGlow = new Color(0.75f, 0.25f, 1.0f, 1f);

            var cols = new Color[w * h];
            for (int i = 0; i < cols.Length; i++) cols[i] = clear;

            // 1. Нижняя базовая планка (y = 1..2)
            for (int x = 2; x < w - 2; x++)
            {
                cols[1 * w + x] = baseIron;
                cols[2 * w + x] = baseIron;
            }

            // 2. Центральная нажимная пластина (x = 11..16, y = 2..4)
            for (int y = 2; y <= 4; y++)
            {
                for (int x = 11; x <= 16; x++)
                {
                    cols[y * w + x] = plateColor;
                }
            }
            // Руна в центре (x = 13..14, y = 3)
            cols[3 * w + 13] = runeGlow;
            cols[3 * w + 14] = runeGlow;

            // 3. Зубья слева (раскрытые челюсти)
            int[,] leftTeeth = {
                {2, 3}, {3, 4}, {3, 5}, {4, 6}, {5, 7}, {6, 8}, {7, 9}, {8, 10},
                {4, 3}, {5, 4}, {6, 5}, {7, 6}, {8, 7}, {9, 8}
            };
            for (int i = 0; i < leftTeeth.GetLength(0); i++)
            {
                int x = leftTeeth[i, 0];
                int y = leftTeeth[i, 1];
                if (x >= 0 && x < w && y >= 0 && y < h)
                {
                    cols[y * w + x] = teethSteel;
                    if (y >= 8) cols[y * w + x] = highlight;
                }
            }

            // 4. Зубья справа (раскрытые челюсти)
            for (int i = 0; i < leftTeeth.GetLength(0); i++)
            {
                int x = (w - 1) - leftTeeth[i, 0];
                int y = leftTeeth[i, 1];
                if (x >= 0 && x < w && y >= 0 && y < h)
                {
                    cols[y * w + x] = teethSteel;
                    if (y >= 8) cols[y * w + x] = highlight;
                }
            }

            tex.SetPixels(cols);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.05f), 18f);
        }

        private static Sprite CreateClosedTrapSprite()
        {
            int w = 24;
            int h = 18;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color clear = new Color(0, 0, 0, 0);
            Color baseIron = new Color(0.24f, 0.24f, 0.28f, 1f);
            Color teethSteel = new Color(0.72f, 0.74f, 0.82f, 1f);
            Color highlight = new Color(0.95f, 0.95f, 1.0f, 1f);
            Color runeGlow = new Color(0.85f, 0.35f, 1.0f, 1f);

            var cols = new Color[w * h];
            for (int i = 0; i < cols.Length; i++) cols[i] = clear;

            // База
            for (int x = 4; x < w - 4; x++)
            {
                cols[1 * w + x] = baseIron;
                cols[2 * w + x] = baseIron;
            }

            // Захлопнутые челюсти по центру (перекрещивающиеся зубья)
            for (int y = 3; y <= 16; y++)
            {
                for (int x = 8; x <= 15; x++)
                {
                    if ((x + y) % 2 == 0)
                    {
                        cols[y * w + x] = teethSteel;
                    }
                    else if (y > 10)
                    {
                        cols[y * w + x] = highlight;
                    }
                }
            }

            // Магическое свечение сквозь зубья
            cols[6 * w + 11] = runeGlow;
            cols[6 * w + 12] = runeGlow;
            cols[9 * w + 11] = runeGlow;
            cols[9 * w + 12] = runeGlow;

            tex.SetPixels(cols);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.05f), 18f);
        }

        private static Sprite CreateBrokenTrapSprite()
        {
            int w = 26;
            int h = 8;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color clear = new Color(0, 0, 0, 0);
            Color baseIron = new Color(0.24f, 0.24f, 0.28f, 0.8f);
            Color scrapSteel = new Color(0.6f, 0.6f, 0.7f, 0.8f);

            var cols = new Color[w * h];
            for (int i = 0; i < cols.Length; i++) cols[i] = clear;

            for (int x = 2; x < 10; x++) cols[1 * w + x] = baseIron;
            for (int x = 16; x < 24; x++) cols[1 * w + x] = baseIron;

            // Погнутые осколки
            cols[2 * w + 4] = scrapSteel;
            cols[3 * w + 5] = scrapSteel;
            cols[4 * w + 6] = scrapSteel;
            cols[2 * w + 19] = scrapSteel;
            cols[3 * w + 20] = scrapSteel;

            tex.SetPixels(cols);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.05f), 18f);
        }

        #endregion
    }
}
