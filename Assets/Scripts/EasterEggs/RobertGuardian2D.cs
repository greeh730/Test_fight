using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Combat.Player;
using Combat.Common;

namespace EasterEggs
{
    /// <summary>
    /// Страж Запределья Роберт (Easter Egg: Robert the Out-of-Bounds Guardian).
    /// Вылетает при выходе игрока за пределы карты в T-позе (масштаб x2),
    /// преследует нарушителя, выкрикивает мемные реплики в визуальном облачке речи
    /// и яростно заспамливает летальными огненными шарами.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public class RobertGuardian2D : MonoBehaviour
    {
        public static RobertGuardian2D Instance { get; private set; }

        private enum RobertState
        {
            Dormant,
            SwoopingIn,
            HoverAndAttack,
            Retreating
        }

        [Header("--- Robert Visuals ---")]
        [Tooltip("Спрайт Роберта (T-pose)")]
        [SerializeField] private Sprite robertSprite;

        [Tooltip("Множитель масштаба Роберта (ТЗ: x2 размер)")]
        [SerializeField] private float scaleMultiplier = 2.0f;

        [Header("--- Fireball Barrage Attack ---")]
        [Tooltip("Префаб огненного шара")]
        [SerializeField] private GameObject fireballPrefab;

        [Tooltip("Интервал между выстрелами фаерболов (сек)")]
        [SerializeField] private float fireRate = 0.28f;

        [Tooltip("Прямой урон одного фаербола (ТЗ: должны убивать)")]
        [SerializeField] private float fireballDamage = 25f;

        [Tooltip("Скорость полёта фаербола Роберта")]
        [SerializeField] private float fireballSpeed = 16f;

        [Tooltip("Угол случайного разброса фаерболов в градусах")]
        [SerializeField] private float spreadAngle = 12f;

        [Header("--- Movement & Hovering ---")]
        [Tooltip("Дистанция парения над головой игрока по Y (комфортная высота в кадре камеры)")]
        [SerializeField] private float hoverHeightAbovePlayer = 2.8f;

        [Tooltip("Плавность горизонтального следования за игроком")]
        [SerializeField] private float horizontalFollowSpeed = 7.5f;

        [Tooltip("Скорость прилёта Роберта из пустоты")]
        [SerializeField] private float swoopInSpeed = 24f;

        [Tooltip("Скорость отлёта Роберта ввысь при возврате игрока")]
        [SerializeField] private float retreatSpeed = 30f;

        [Header("--- Easter Egg Quotes ---")]
        [SerializeField] private List<string> arrivalQuotes = new List<string>
        {
            "РОБЕРТ ОБНАРУЖИЛ НАРУШИТЕЛЯ!",
            "РОБЕРТ ВИДИТ ТЕБЯ!",
            "ТЫ НЕ ДОЛЖЕН БЫЛ СЮДА ПОПАСТЬ!",
            "КУДА ЭТО ТЫ СОБРАЛСЯ?!"
        };

        [SerializeField] private List<string> attackQuotes = new List<string>
        {
            "ВЕРНИСЬ НА КАРТУ, СМЕРТНЫЙ!",
            "ЗДЕСЬ ТЕБЕ НЕ РАДУШНЫЙ ПРИЁМ!",
            "ЗАПРЕТНАЯ ЗОНА!",
            "РОБЕРТ НЕ ПРОЩАЕТ!",
            "НАЗАД В ЗАГОН!"
        };

        [SerializeField] private List<string> retreatQuotes = new List<string>
        {
            "И БОЛЬШЕ НЕ СУЙСЯ СЮДА!",
            "НЕ ВОЗВРАЩАЙСЯ!",
            "РОБЕРТ СЛЕДИТ ЗА ТОБОЙ!"
        };

        private SpriteRenderer _sr;
        private RobertState _currentState = RobertState.Dormant;
        private Transform _playerTransform;
        private PlayerHealth2D _playerHealth;
        private OutOfBoundsDetector2D _detector;

        private float _fireTimer = 0f;
        private float _quoteTimer = 0f;
        private float _hoverSinTime = 0f;
        private string _pendingArrivalQuote;

        // --- Выделенное облачко речи Роберта ---
        private GameObject _speechBubbleRoot;
        private TextMesh _speechText;
        private TextMesh _speechShadow;
        private SpriteRenderer _speechBg;
        private float _speechVisibleTimer = 0f;
        private static Sprite _whiteBoxSprite;

        // --- Экранный графический баннер OnGUI ---
        private string _currentBannerText;
        private Color _currentBannerColor = Color.yellow;
        private float _bannerVisibleTimer = 0f;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _sr = GetComponent<SpriteRenderer>();

            // Загрузка спрайта Роберта, если не назначен в инспекторе
            if (robertSprite == null)
            {
#if UNITY_EDITOR
                robertSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Robert.png");
                if (robertSprite == null)
                {
                    var all = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Robert.png");
                    foreach (var a in all)
                    {
                        if (a is Sprite sp) { robertSprite = sp; break; }
                    }
                }
#endif
            }

            if (_sr != null && robertSprite != null)
            {
                _sr.sprite = robertSprite;
                _sr.sortingLayerName = "Default";
                _sr.sortingOrder = 90; // Поверх элементов уровня
            }

            // Масштабирование x2
            transform.localScale = Vector3.one * scaleMultiplier;

            // Загрузка префаба фаербола
            if (fireballPrefab == null)
            {
                fireballPrefab = Resources.Load<GameObject>("Prefabs/Fireball");
            }

            EnsureSpeechBubble();
            SetDormantState();
        }

        private void Start()
        {
            FindDetectorAndPlayer();
        }

        private void OnEnable()
        {
            FindDetectorAndPlayer();
            if (_detector != null)
            {
                _detector.OnOutOfBoundsChanged -= HandleOutOfBoundsChanged;
                _detector.OnOutOfBoundsChanged += HandleOutOfBoundsChanged;
                if (_detector.IsOutOfBounds)
                {
                    TriggerArrival();
                }
            }
            PlayerHealth2D.OnAnyPlayerDeath += HandlePlayerDied;
        }

        private void OnDisable()
        {
            if (_detector != null)
            {
                _detector.OnOutOfBoundsChanged -= HandleOutOfBoundsChanged;
            }
            PlayerHealth2D.OnAnyPlayerDeath -= HandlePlayerDied;
        }

        private void FindDetectorAndPlayer()
        {
            if (_detector == null)
            {
                _detector = FindAnyObjectByType<OutOfBoundsDetector2D>();
                if (_detector == null)
                {
                    var detGo = new GameObject("[OutOfBoundsDetector]");
                    _detector = detGo.AddComponent<OutOfBoundsDetector2D>();
                }
            }

            if (_playerTransform == null)
            {
                var p = FindAnyObjectByType<PlayerController2D>();
                if (p != null)
                {
                    _playerTransform = p.transform;
                    _playerHealth = p.GetComponent<PlayerHealth2D>();
                }
            }
        }

        private void EnsureSpeechBubble()
        {
            if (_speechBubbleRoot != null) return;

            if (_whiteBoxSprite == null)
            {
                var tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                _whiteBoxSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            }

            _speechBubbleRoot = new GameObject("[SpeechBubble]");
            _speechBubbleRoot.transform.SetParent(transform, false);
            _speechBubbleRoot.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            _speechBubbleRoot.transform.localScale = Vector3.one;

            // 1. Тёмная плашка-фон
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(_speechBubbleRoot.transform, false);
            bgGo.transform.localPosition = Vector3.zero;
            _speechBg = bgGo.AddComponent<SpriteRenderer>();
            _speechBg.sprite = _whiteBoxSprite;
            _speechBg.color = new Color(0.04f, 0.05f, 0.08f, 0.88f);
            _speechBg.sortingLayerName = "Default";
            _speechBg.sortingOrder = 198;

            // 2. Тень текста
            var shadowGo = new GameObject("ShadowText");
            shadowGo.transform.SetParent(_speechBubbleRoot.transform, false);
            shadowGo.transform.localPosition = new Vector3(0.035f, -0.035f, 0f);
            _speechShadow = shadowGo.AddComponent<TextMesh>();
            _speechShadow.alignment = TextAlignment.Center;
            _speechShadow.anchor = TextAnchor.MiddleCenter;
            _speechShadow.fontSize = 42;
            _speechShadow.characterSize = 0.045f;
            _speechShadow.fontStyle = FontStyle.Bold;
            _speechShadow.color = Color.black;
            var smr = shadowGo.GetComponent<MeshRenderer>();
            smr.sortingLayerName = "Default";
            smr.sortingOrder = 199;

            // 3. Основной текст
            var textGo = new GameObject("MainText");
            textGo.transform.SetParent(_speechBubbleRoot.transform, false);
            textGo.transform.localPosition = Vector3.zero;
            _speechText = textGo.AddComponent<TextMesh>();
            _speechText.alignment = TextAlignment.Center;
            _speechText.anchor = TextAnchor.MiddleCenter;
            _speechText.fontSize = 42;
            _speechText.characterSize = 0.045f;
            _speechText.fontStyle = FontStyle.Bold;
            _speechText.color = Color.yellow;
            var tmr = textGo.GetComponent<MeshRenderer>();
            tmr.sortingLayerName = "Default";
            tmr.sortingOrder = 200;

            _speechBubbleRoot.SetActive(false);
        }

        /// <summary>
        /// Показывает визуальное облачко с текстом над Робертом и экранный баннер
        /// </summary>
        public void ShowSpeech(string message, Color textColor, float duration = 2.5f)
        {
            _currentBannerText = message;
            _currentBannerColor = textColor;
            _bannerVisibleTimer = duration;

            EnsureSpeechBubble();
            if (_speechBubbleRoot != null)
            {
                _speechText.text = message;
                _speechText.color = textColor;
                _speechShadow.text = message;

                // Подгоняем размер тёмного фона под длину строки
                float textWidth = Mathf.Max(3.0f, message.Length * 0.24f);
                _speechBg.transform.localScale = new Vector3(textWidth, 0.85f, 1f);

                _speechVisibleTimer = duration;
                _speechBubbleRoot.SetActive(true);

                // Анимация лёгкого "удара" масштабом
                StopCoroutine("SpeechPunchRoutine");
                StartCoroutine("SpeechPunchRoutine");
            }
        }

        private IEnumerator SpeechPunchRoutine()
        {
            float elapsed = 0f;
            Vector3 baseScale = Vector3.one;
            while (elapsed < 0.15f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 0.15f;
                _speechBubbleRoot.transform.localScale = Vector3.Lerp(Vector3.one * 1.25f, baseScale, t);
                yield return null;
            }
            _speechBubbleRoot.transform.localScale = baseScale;
        }

        private void HandleOutOfBoundsChanged(bool isOutOfBounds)
        {
            if (isOutOfBounds)
            {
                TriggerArrival();
            }
            else
            {
                TriggerRetreat();
            }
        }

        private void HandlePlayerDied()
        {
            if (_currentState == RobertState.HoverAndAttack || _currentState == RobertState.SwoopingIn)
            {
                ShowSpeech("ПРАВОСУДИЕ РОБЕРТА СВЕРШИЛОСЬ.", Color.red, 3.0f);
                TriggerRetreat();
            }
        }

        /// <summary>
        /// Перевод Роберта в скрытый спящий режим
        /// </summary>
        private void SetDormantState()
        {
            _currentState = RobertState.Dormant;
            if (_sr != null) _sr.enabled = false;
            if (_speechBubbleRoot != null) _speechBubbleRoot.SetActive(false);
            transform.position = new Vector3(0f, 999f, 0f);
        }

        /// <summary>
        /// Эпичный вылет Роберта при обнаружении выхода за карту
        /// </summary>
        private void TriggerArrival()
        {
            FindDetectorAndPlayer();
            if (_playerTransform == null) return;

            if (_currentState == RobertState.HoverAndAttack || _currentState == RobertState.SwoopingIn) return;

            _currentState = RobertState.SwoopingIn;
            if (_sr != null) _sr.enabled = true;

            // Спавн над экраном
            transform.position = _playerTransform.position + new Vector3(Random.Range(-2f, 2f), 10f, 0f);
            transform.localScale = Vector3.one * scaleMultiplier;

            if (Combat.Audio.SoundManager.Instance != null)
            {
                Combat.Audio.SoundManager.Instance.PlayExplosion(1.0f);
            }

            // Реплика Роберта покажется в облачке над ним, как он долетит до позиции парения
            _pendingArrivalQuote = (arrivalQuotes.Count > 0) ? arrivalQuotes[Random.Range(0, arrivalQuotes.Count)] : "РОБЕРТ ОБНАРУЖИЛ НАРУШИТЕЛЯ!";
            _quoteTimer = 0f;
            _fireTimer = 0.35f;
        }

        /// <summary>
        /// Отлёт Роберта ввысь при возврате игрока в пределы карты
        /// </summary>
        private void TriggerRetreat()
        {
            if (_currentState == RobertState.Dormant || _currentState == RobertState.Retreating) return;

            _currentState = RobertState.Retreating;
            string quote = (retreatQuotes.Count > 0) ? retreatQuotes[Random.Range(0, retreatQuotes.Count)] : "И БОЛЬШЕ НЕ СУЙСЯ СЮДА!";
            ShowSpeech(quote, new Color(0.2f, 1f, 0.5f, 1f), 2.2f);
        }

        private void Update()
        {
            if (_bannerVisibleTimer > 0f)
            {
                _bannerVisibleTimer -= Time.deltaTime;
            }

            if (_speechVisibleTimer > 0f)
            {
                _speechVisibleTimer -= Time.deltaTime;
                if (_speechVisibleTimer <= 0f && _speechBubbleRoot != null)
                {
                    _speechBubbleRoot.SetActive(false);
                }
            }

            if (_currentState == RobertState.Dormant) return;

            FindDetectorAndPlayer();
            if (_playerTransform == null)
            {
                SetDormantState();
                return;
            }

            _hoverSinTime += Time.deltaTime;

            switch (_currentState)
            {
                case RobertState.SwoopingIn:
                    UpdateSwoopingIn();
                    break;

                case RobertState.HoverAndAttack:
                    UpdateHoverAndAttack();
                    break;

                case RobertState.Retreating:
                    UpdateRetreating();
                    break;
            }
        }

        private void UpdateSwoopingIn()
        {
            Vector3 target = _playerTransform.position + Vector3.up * hoverHeightAbovePlayer;
            transform.position = Vector3.MoveTowards(transform.position, target, swoopInSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, target) < 0.6f)
            {
                _currentState = RobertState.HoverAndAttack;

                // Долетел — сразу кричит фразу прилёта над головой!
                if (!string.IsNullOrEmpty(_pendingArrivalQuote))
                {
                    ShowSpeech(_pendingArrivalQuote, new Color(1f, 0.25f, 0.25f, 1f), 2.2f);
                    _pendingArrivalQuote = null;
                }
            }
        }

        private void UpdateHoverAndAttack()
        {
            // 1. Позиционирование: парение над игроком с синусоидальным дрейфом
            float sinY = Mathf.Sin(_hoverSinTime * 5f) * 0.35f;
            float targetX = Mathf.Lerp(transform.position.x, _playerTransform.position.x, horizontalFollowSpeed * Time.deltaTime);
            float targetY = _playerTransform.position.y + hoverHeightAbovePlayer + sinY;

            transform.position = new Vector3(targetX, targetY, 0f);

            // Наклон в сторону движения
            float tilt = Mathf.Clamp((_playerTransform.position.x - transform.position.x) * 4f, -12f, 12f);
            transform.rotation = Quaternion.Euler(0f, 0f, -tilt);

            // Облачко текста держим горизонтально без наклона тела
            if (_speechBubbleRoot != null)
            {
                _speechBubbleRoot.transform.rotation = Quaternion.identity;
            }

            // 2. Стрельба шквалом фаерболов
            _fireTimer -= Time.deltaTime;
            if (_fireTimer <= 0f)
            {
                _fireTimer = fireRate;
                ShootHostileFireball();
            }

            // 3. Периодические выкрики Роберта
            _quoteTimer += Time.deltaTime;
            if (_quoteTimer >= 2.2f)
            {
                _quoteTimer = 0f;
                if (attackQuotes.Count > 0)
                {
                    string quote = attackQuotes[Random.Range(0, attackQuotes.Count)];
                    ShowSpeech(quote, new Color(1f, 0.9f, 0.2f, 1f), 1.8f);
                }
            }
        }

        private void ShootHostileFireball()
        {
            if (fireballPrefab == null || _playerTransform == null) return;

            // Направление на игрока с разбросом
            Vector2 baseDir = (_playerTransform.position - transform.position).normalized;
            float randomSpread = Random.Range(-spreadAngle, spreadAngle);
            Vector2 finalDir = Quaternion.Euler(0f, 0f, randomSpread) * baseDir;

            Vector3 spawnOrigin = transform.position + (Vector3)finalDir * 0.8f;
            GameObject fbGo = Instantiate(fireballPrefab, spawnOrigin, Quaternion.identity);

            var projectile = fbGo.GetComponent<Magic.FireballProjectile>();
            if (projectile != null)
            {
                projectile.Initialize(transform, finalDir, fireballSpeed, 1.25f, fireballDamage);
                projectile.SetHostileToPlayer(true, fireballDamage);
            }

            if (Combat.Audio.SoundManager.Instance != null)
            {
                Combat.Audio.SoundManager.Instance.PlayFireball(0.85f);
            }
        }

        private void UpdateRetreating()
        {
            // Стремительный отлёт вверх в стратосферу
            transform.position += Vector3.up * retreatSpeed * Time.deltaTime;
            transform.rotation = Quaternion.identity;

            if (transform.position.y > 60f)
            {
                SetDormantState();
            }
        }


    }
}
