using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Combat.Player;

namespace Combat.UI
{
    /// <summary>
    /// Стильная полоска здоровья героя в левом верхнем углу экрана (Top-Left HUD).
    /// Включает:
    /// - Киберпанк-плашку с именем героя (EVO) и иконкой HP
    /// - Шкалу здоровья с плавной интерполяцией
    /// - Ghost-шлейф задержки урона (Ghost bar)
    /// - Вспышку исцеления (зеленый/бирюзовый) при сборе Кристалла Победы
    /// - Пульсацию тревоги при критически низком HP (<25%)
    /// - Текстовое отображение текущего и максимального здоровья ("100 / 100")
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerHealthBarUI : MonoBehaviour
    {
        private static PlayerHealthBarUI _instance;
        public static PlayerHealthBarUI Instance => _instance;

        [Header("--- Ссылка на здоровье игрока ---")]
        [SerializeField] private PlayerHealth2D playerHealth;

        [Header("--- Компоненты интерфейса ---")]
        [SerializeField] private RectTransform rootRectTransform;
        [SerializeField] private Image fillBar;
        [SerializeField] private Image ghostBar;
        [SerializeField] private Text hpText;
        [SerializeField] private Text heroNameText;
        [SerializeField] private Image badgeBackground;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("--- Цвета ---")]
        [SerializeField] private Color normalFillColor = new Color(1.0f, 0.18f, 0.32f, 1.0f);     // Неоновый малиново-красный
        [SerializeField] private Color lowHealthColor = new Color(1.0f, 0.08f, 0.12f, 1.0f);       // Тревожный темно-красный
        [SerializeField] private Color healFlashColor = new Color(0.15f, 1.0f, 0.65f, 1.0f);      // Бирюзово-зеленый при лечении
        [SerializeField] private Color ghostBarColor = new Color(1.0f, 0.85f, 0.30f, 0.88f);       // Золотисто-желтый шлейф урона
        [SerializeField] private Color panelBgColor = new Color(0.06f, 0.08f, 0.13f, 0.90f);       // Темный фон плашки
        [SerializeField] private Color borderColor = new Color(0.22f, 0.28f, 0.38f, 0.92f);        // Окантовка

        [Header("--- Анимации и сочность ---")]
        [SerializeField] private float fillLerpSpeed = 14f;
        [SerializeField] private float ghostLagDelay = 0.40f;
        [SerializeField] private float ghostCatchupSpeed = 4.5f;
        [SerializeField] private float punchScaleOnHit = 1.08f;
        [SerializeField] private float punchReturnSpeed = 9f;

        private float _targetFill = 1.0f;
        private float _currentFill = 1.0f;
        private float _ghostFill = 1.0f;
        private float _ghostTimer = 0f;
        private float _currentPunch = 1.0f;
        private float _lastHealth = 100f;
        private float _healFlashTimer = 0f;
        private static Sprite _whiteSprite;

        public static Sprite GetWhiteSprite()
        {
            if (_whiteSprite == null)
            {
                Texture2D tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }
            return _whiteSprite;
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            EnsureUIHierarchy();
            FindPlayerHealth();
        }

        private void OnEnable()
        {
            FindPlayerHealth();
            SubscribeEvents();
            RefreshDisplay(instant: true);
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void Start()
        {
            FindPlayerHealth();
            RefreshDisplay(instant: true);
        }

        private void FindPlayerHealth()
        {
            if (playerHealth == null)
            {
                playerHealth = FindAnyObjectByType<PlayerHealth2D>();
            }
        }

        private void SubscribeEvents()
        {
            if (playerHealth != null)
            {
                playerHealth.onHealthChanged.RemoveListener(HandleHealthChanged);
                playerHealth.onHealthChanged.AddListener(HandleHealthChanged);
                playerHealth.onDeath.RemoveListener(HandleDeath);
                playerHealth.onDeath.AddListener(HandleDeath);
                playerHealth.onRespawn.RemoveListener(HandleRespawn);
                playerHealth.onRespawn.AddListener(HandleRespawn);
            }

            PlayerHealth2D.OnAnyPlayerDeath -= HandleDeath;
            PlayerHealth2D.OnAnyPlayerDeath += HandleDeath;
            PlayerHealth2D.OnAnyPlayerRespawn -= HandleRespawn;
            PlayerHealth2D.OnAnyPlayerRespawn += HandleRespawn;
        }

        private void UnsubscribeEvents()
        {
            if (playerHealth != null)
            {
                playerHealth.onHealthChanged.RemoveListener(HandleHealthChanged);
                playerHealth.onDeath.RemoveListener(HandleDeath);
                playerHealth.onRespawn.RemoveListener(HandleRespawn);
            }

            PlayerHealth2D.OnAnyPlayerDeath -= HandleDeath;
            PlayerHealth2D.OnAnyPlayerRespawn -= HandleRespawn;
        }

        private void Update()
        {
            if (playerHealth == null)
            {
                FindPlayerHealth();
                if (playerHealth != null) SubscribeEvents();
            }

            // Плавное заполнение основной полосы
            _currentFill = Mathf.Lerp(_currentFill, _targetFill, Time.deltaTime * fillLerpSpeed);
            if (fillBar != null)
            {
                fillBar.fillAmount = _currentFill;
            }

            // Ghost-шлейф урона
            if (_ghostTimer > 0f)
            {
                _ghostTimer -= Time.deltaTime;
            }
            else if (_ghostFill > _targetFill)
            {
                _ghostFill = Mathf.MoveTowards(_ghostFill, _targetFill, Time.deltaTime * ghostCatchupSpeed);
                if (ghostBar != null)
                {
                    ghostBar.fillAmount = _ghostFill;
                }
            }
            else
            {
                _ghostFill = _targetFill;
                if (ghostBar != null) ghostBar.fillAmount = _ghostFill;
            }

            // Плавный возврат масштаба после удара (Punch)
            if (rootRectTransform != null && _currentPunch > 1.001f)
            {
                _currentPunch = Mathf.Lerp(_currentPunch, 1.0f, Time.deltaTime * punchReturnSpeed);
                rootRectTransform.localScale = Vector3.one * _currentPunch;
            }

            // Эффект вспышки лечения
            if (_healFlashTimer > 0f)
            {
                _healFlashTimer -= Time.deltaTime;
                float t = Mathf.Clamp01(_healFlashTimer / 0.45f);
                if (fillBar != null)
                {
                    fillBar.color = Color.Lerp(normalFillColor, healFlashColor, t);
                }
            }
            else if (fillBar != null)
            {
                // Пульсация при критически низком HP
                if (_targetFill <= 0.25f && _targetFill > 0f)
                {
                    float pulse = 0.75f + Mathf.PingPong(Time.time * 3.5f, 0.25f);
                    fillBar.color = new Color(lowHealthColor.r, lowHealthColor.g * pulse, lowHealthColor.b * pulse, 1f);
                }
                else
                {
                    fillBar.color = normalFillColor;
                }
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            float newFill = max > 0.001f ? Mathf.Clamp01(current / max) : 0f;

            if (current < _lastHealth)
            {
                // Игрок получил урон: запускаем задержку ghost-шлейфа и шейк
                _ghostTimer = ghostLagDelay;
                _currentPunch = punchScaleOnHit;
            }
            else if (current > _lastHealth)
            {
                // Игрок вылечился / собрал кристалл: мгновенный подгон ghost-полосы и вспышка исцеления
                _ghostFill = newFill;
                if (ghostBar != null) ghostBar.fillAmount = _ghostFill;
                _healFlashTimer = 0.5f;
            }

            _lastHealth = current;
            _targetFill = newFill;

            UpdateHpText(current, max);
        }

        private void HandleDeath()
        {
            _targetFill = 0f;
            _ghostFill = 0f;
            _currentFill = 0f;
            if (fillBar != null) fillBar.fillAmount = 0f;
            if (ghostBar != null) ghostBar.fillAmount = 0f;
            if (hpText != null) hpText.text = "0 HP";
        }

        private void HandleRespawn()
        {
            FindPlayerHealth();
            RefreshDisplay(instant: true);
        }

        public void TriggerHealFlash()
        {
            _healFlashTimer = 0.55f;
            _currentPunch = 1.05f;
        }

        public void RefreshDisplay(bool instant = false)
        {
            if (playerHealth == null) return;

            float cur = playerHealth.CurrentHealth;
            float max = playerHealth.MaxHealth;
            _lastHealth = cur;
            _targetFill = max > 0.001f ? Mathf.Clamp01(cur / max) : 1.0f;

            if (instant)
            {
                _currentFill = _targetFill;
                _ghostFill = _targetFill;
                if (fillBar != null) fillBar.fillAmount = _currentFill;
                if (ghostBar != null) ghostBar.fillAmount = _ghostFill;
            }

            UpdateHpText(cur, max);
        }

        private void UpdateHpText(float current, float max)
        {
            if (hpText != null)
            {
                hpText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            }
        }

        /// <summary>
        /// Автоматическое построение UI-иерархии полоски здоровья в Canvas,
        /// если компоненты не были назначены через инспектор
        /// </summary>
        public void EnsureUIHierarchy()
        {
            Sprite white = GetWhiteSprite();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            rootRectTransform = GetComponent<RectTransform>();
            if (rootRectTransform == null) rootRectTransform = gameObject.AddComponent<RectTransform>();

            // Фиксация в ЛЕВОМ ВЕРХНЕМ УГЛУ
            rootRectTransform.anchorMin = new Vector2(0f, 1f);
            rootRectTransform.anchorMax = new Vector2(0f, 1f);
            rootRectTransform.pivot = new Vector2(0f, 1f);
            rootRectTransform.anchoredPosition = new Vector2(25f, -25f);
            rootRectTransform.sizeDelta = new Vector2(260f, 44f);

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Фоновая панель
            var panelImage = GetComponent<Image>();
            if (panelImage == null) panelImage = gameObject.AddComponent<Image>();
            panelImage.sprite = white;
            panelImage.color = panelBgColor;

            var panelOutline = GetComponent<Outline>();
            if (panelOutline == null) panelOutline = gameObject.AddComponent<Outline>();
            panelOutline.effectColor = borderColor;
            panelOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // 1. Иконка-бейдж героя слева (Badge)
            Transform badgeTform = transform.Find("HeroBadge");
            GameObject badgeGo;
            if (badgeTform == null)
            {
                badgeGo = new GameObject("HeroBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                badgeGo.transform.SetParent(transform, false);
            }
            else
            {
                badgeGo = badgeTform.gameObject;
            }
            var badgeRect = badgeGo.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0f, 0.5f);
            badgeRect.anchorMax = new Vector2(0f, 0.5f);
            badgeRect.pivot = new Vector2(0f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(6f, 0f);
            badgeRect.sizeDelta = new Vector2(34f, 34f);

            badgeBackground = badgeGo.GetComponent<Image>();
            badgeBackground.sprite = white;
            badgeBackground.color = new Color(0.12f, 0.16f, 0.24f, 0.95f);

            var badgeOutline = badgeGo.GetComponent<Outline>();
            if (badgeOutline == null) badgeOutline = badgeGo.AddComponent<Outline>();
            badgeOutline.effectColor = new Color(0f, 0.85f, 1f, 0.85f); // Неоновый циан
            badgeOutline.effectDistance = new Vector2(1f, -1f);

            // Текст внутри бейджа "HP"
            Transform badgeTextTform = badgeGo.transform.Find("BadgeText");
            Text badgeText;
            if (badgeTextTform == null)
            {
                var btGo = new GameObject("BadgeText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                btGo.transform.SetParent(badgeGo.transform, false);
                badgeText = btGo.GetComponent<Text>();
            }
            else
            {
                badgeText = badgeTextTform.GetComponent<Text>();
            }
            var btRect = badgeText.GetComponent<RectTransform>();
            btRect.anchorMin = Vector2.zero;
            btRect.anchorMax = Vector2.one;
            btRect.sizeDelta = Vector2.zero;
            badgeText.font = font;
            badgeText.fontSize = 15;
            badgeText.fontStyle = FontStyle.Bold;
            badgeText.alignment = TextAnchor.MiddleCenter;
            badgeText.text = "HP";
            badgeText.color = new Color(0f, 0.95f, 1f, 1f);

            // 2. Имя героя над полоской ("EVO")
            Transform nameTform = transform.Find("HeroNameText");
            if (nameTform == null)
            {
                var hnGo = new GameObject("HeroNameText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                hnGo.transform.SetParent(transform, false);
                heroNameText = hnGo.GetComponent<Text>();
            }
            else
            {
                heroNameText = nameTform.GetComponent<Text>();
            }
            var hnRect = heroNameText.GetComponent<RectTransform>();
            hnRect.anchorMin = new Vector2(0f, 1f);
            hnRect.anchorMax = new Vector2(0f, 1f);
            hnRect.pivot = new Vector2(0f, 1f);
            hnRect.anchoredPosition = new Vector2(48f, -4f);
            hnRect.sizeDelta = new Vector2(100f, 14f);
            heroNameText.font = font;
            heroNameText.fontSize = 11;
            heroNameText.fontStyle = FontStyle.Bold;
            heroNameText.alignment = TextAnchor.UpperLeft;
            heroNameText.text = "EVO";
            heroNameText.color = new Color(0.85f, 0.9f, 1f, 0.9f);

            // 3. Контейнер шкалы здоровья (BarContainer)
            Transform barContTform = transform.Find("BarContainer");
            GameObject barContGo;
            if (barContTform == null)
            {
                barContGo = new GameObject("BarContainer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                barContGo.transform.SetParent(transform, false);
            }
            else
            {
                barContGo = barContTform.gameObject;
            }
            var barContRect = barContGo.GetComponent<RectTransform>();
            barContRect.anchorMin = new Vector2(0f, 0f);
            barContRect.anchorMax = new Vector2(1f, 0f);
            barContRect.pivot = new Vector2(0f, 0f);
            barContRect.anchoredPosition = new Vector2(48f, 6f);
            barContRect.sizeDelta = new Vector2(-56f, 16f); // 204px ширина, 16px высота

            var barContBg = barContGo.GetComponent<Image>();
            barContBg.sprite = white;
            barContBg.color = new Color(0.18f, 0.04f, 0.07f, 0.95f); // Темный винный фон

            var barContOutline = barContGo.GetComponent<Outline>();
            if (barContOutline == null) barContOutline = barContGo.AddComponent<Outline>();
            barContOutline.effectColor = new Color(0.35f, 0.12f, 0.18f, 0.85f);
            barContOutline.effectDistance = new Vector2(1f, -1f);

            // 4. Ghost Bar (задержка урона)
            Transform ghostTform = barContGo.transform.Find("GhostBar");
            GameObject ghostGo;
            if (ghostTform == null)
            {
                ghostGo = new GameObject("GhostBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                ghostGo.transform.SetParent(barContGo.transform, false);
            }
            else
            {
                ghostGo = ghostTform.gameObject;
            }
            var ghostRect = ghostGo.GetComponent<RectTransform>();
            ghostRect.anchorMin = Vector2.zero;
            ghostRect.anchorMax = Vector2.one;
            ghostRect.sizeDelta = Vector2.zero;

            ghostBar = ghostGo.GetComponent<Image>();
            ghostBar.sprite = white;
            ghostBar.type = Image.Type.Filled;
            ghostBar.fillMethod = Image.FillMethod.Horizontal;
            ghostBar.fillOrigin = 0;
            ghostBar.color = ghostBarColor;
            ghostBar.fillAmount = 1.0f;

            // 5. Fill Bar (основная полоса здоровья)
            Transform fillTform = barContGo.transform.Find("FillBar");
            GameObject fillGo;
            if (fillTform == null)
            {
                fillGo = new GameObject("FillBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                fillGo.transform.SetParent(barContGo.transform, false);
            }
            else
            {
                fillGo = fillTform.gameObject;
            }
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;

            fillBar = fillGo.GetComponent<Image>();
            fillBar.sprite = white;
            fillBar.type = Image.Type.Filled;
            fillBar.fillMethod = Image.FillMethod.Horizontal;
            fillBar.fillOrigin = 0;
            fillBar.color = normalFillColor;
            fillBar.fillAmount = 1.0f;

            // 6. Текст числового значения HP ("100 / 100")
            Transform hpTextTform = barContGo.transform.Find("HPText");
            if (hpTextTform == null)
            {
                var hptGo = new GameObject("HPText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                hptGo.transform.SetParent(barContGo.transform, false);
                hpText = hptGo.GetComponent<Text>();
            }
            else
            {
                hpText = hpTextTform.GetComponent<Text>();
            }
            var hptRect = hpText.GetComponent<RectTransform>();
            hptRect.anchorMin = Vector2.zero;
            hptRect.anchorMax = Vector2.one;
            hptRect.sizeDelta = Vector2.zero;
            hpText.font = font;
            hpText.fontSize = 11;
            hpText.fontStyle = FontStyle.Bold;
            hpText.alignment = TextAnchor.MiddleCenter;
            hpText.text = "100 / 100";
            hpText.color = Color.white;

            var hpTextOutline = hpText.GetComponent<Outline>();
            if (hpTextOutline == null) hpTextOutline = hpText.gameObject.AddComponent<Outline>();
            hpTextOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            hpTextOutline.effectDistance = new Vector2(1f, -1f);
        }

        /// <summary>
        /// Создает и настраивает компонент в переданном или найденном Canvas
        /// </summary>
        public static PlayerHealthBarUI EnsureExists(Canvas targetCanvas = null)
        {
            if (_instance != null) return _instance;

            if (targetCanvas == null)
            {
                targetCanvas = FindAnyObjectByType<Canvas>();
            }

            if (targetCanvas == null)
            {
                var cGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                targetCanvas = cGo.GetComponent<Canvas>();
                targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            var existing = targetCanvas.transform.Find("PlayerHealthBar");
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject("PlayerHealthBar");
                go.transform.SetParent(targetCanvas.transform, false);
            }

            var ui = go.GetComponent<PlayerHealthBarUI>();
            if (ui == null) ui = go.AddComponent<PlayerHealthBarUI>();
            ui.EnsureUIHierarchy();
            ui.RefreshDisplay(instant: true);
            _instance = ui;
            return ui;
        }
    }
}
