using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Combat.UI
{
    /// <summary>
    /// Полноэкранный экран смерти игрока:
    /// - При гибели экран МОМЕНТАЛЬНО становится абсолютно чёрным (Snap to Black).
    /// - Затем тьма МЕДЛЕННО развеивается (Smooth Dissolve), открывая экран смерти:
    ///   кинематографичный фон, надпись "ВЫ УМЕРЛИ", кнопки перезапуска и выхода в меню.
    /// - Перезагружает активную сцену при нажатии R, Пробела, Enter или клике.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerDeathScreenUI : MonoBehaviour
    {
        public static PlayerDeathScreenUI Instance { get; private set; }

        [Header("--- Настройки цветов ---")]
        [SerializeField] private Color backdropColor = new Color(0.04f, 0.02f, 0.03f, 0.90f);
        [SerializeField] private Color titleColor = new Color(0.92f, 0.16f, 0.18f, 1.0f); // Crimson Red
        [SerializeField] private Color subtitleColor = new Color(0.95f, 0.92f, 0.88f, 0.95f);
        [SerializeField] private Color buttonBgColor = new Color(0.18f, 0.05f, 0.07f, 0.95f);
        [SerializeField] private Color buttonHoverColor = new Color(0.35f, 0.08f, 0.12f, 1.0f);

        [Header("--- Анимация гибели (Моментальное затемнение и развеивание) ---")]
        [Tooltip("Время паузы в полной темноте перед началом развеивания (в секундах)")]
        [Range(0f, 2.0f)]
        [SerializeField] private float blackoutHoldDuration = 0.35f;

        [Tooltip("Длительность медленного развеивания тьмы до полного проявления экрана смерти (в секундах)")]
        [Range(0.5f, 5.0f)]
        [SerializeField] private float dissolveDuration = 2.0f;

        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private GameObject _screenRoot;
        private GameObject _deathContentGo;
        private CanvasGroup _contentCanvasGroup;
        private RectTransform _contentContainerRect;
        private GameObject _blackCurtainGo;
        private CanvasGroup _curtainCanvasGroup;
        private Image _curtainImage;
        private Text _titleText;
        private Text _subtitleText;
        private bool _isShowing = false;
        private Font _uiFont;
        private Sprite _whiteSprite;

        public bool IsShowing => _isShowing;

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

            EnsureAssets();
            EnsureUIBuilt();
            if (_screenRoot != null)
            {
                _screenRoot.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void EnsureAssets()
        {
            if (_uiFont == null)
            {
                _uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_uiFont == null) _uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            if (_whiteSprite == null)
            {
                Texture2D tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            }
        }

        private void EnsureUIBuilt()
        {
            if (_screenRoot != null) return;

            // 1. Создаем Canvas поверх всех возможных виньеток и HUD (sortingOrder = 5000)
            _canvas = GetComponent<Canvas>();
            if (_canvas == null)
            {
                _canvas = gameObject.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 5000;
            }

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            // 2. Корневой объект экрана
            _screenRoot = new GameObject("DeathScreenRoot");
            _screenRoot.transform.SetParent(transform, false);

            var rootRect = _screenRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;

            _canvasGroup = _screenRoot.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;

            // 3. Контейнер контента экрана смерти (фон, надписи, кнопки)
            _deathContentGo = new GameObject("DeathContent");
            _deathContentGo.transform.SetParent(_screenRoot.transform, false);
            var contentRootRect = _deathContentGo.AddComponent<RectTransform>();
            contentRootRect.anchorMin = Vector2.zero;
            contentRootRect.anchorMax = Vector2.one;
            contentRootRect.sizeDelta = Vector2.zero;

            _contentCanvasGroup = _deathContentGo.AddComponent<CanvasGroup>();
            _contentCanvasGroup.alpha = 0f;

            // 3.1. Полупрозрачный темный кинематографичный фон
            var bgGo = new GameObject("Backdrop");
            bgGo.transform.SetParent(_deathContentGo.transform, false);
            var bgRect = bgGo.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            var bgImage = bgGo.AddComponent<Image>();
            bgImage.sprite = _whiteSprite;
            bgImage.color = backdropColor;

            // 3.2. Центровой контейнер текста и кнопок
            var contentGo = new GameObject("ContentContainer");
            contentGo.transform.SetParent(_deathContentGo.transform, false);
            _contentContainerRect = contentGo.AddComponent<RectTransform>();
            _contentContainerRect.anchorMin = new Vector2(0.5f, 0.5f);
            _contentContainerRect.anchorMax = new Vector2(0.5f, 0.5f);
            _contentContainerRect.pivot = new Vector2(0.5f, 0.5f);
            _contentContainerRect.sizeDelta = new Vector2(900f, 400f);

            // 3.3. Заголовок "ВЫ УМЕРЛИ"
            var titleGo = new GameObject("TitleText");
            titleGo.transform.SetParent(_contentContainerRect, false);
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.72f);
            titleRect.anchorMax = new Vector2(0.5f, 0.72f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.sizeDelta = new Vector2(900f, 130f);

            _titleText = titleGo.AddComponent<Text>();
            _titleText.font = _uiFont;
            _titleText.fontSize = 76;
            _titleText.fontStyle = FontStyle.Bold;
            _titleText.alignment = TextAnchor.MiddleCenter;
            _titleText.text = "ВЫ УМЕРЛИ";
            _titleText.color = titleColor;

            var titleShadow = titleGo.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
            titleShadow.effectDistance = new Vector2(3f, -3f);

            var titleOutline = titleGo.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0.35f, 0.02f, 0.04f, 0.95f);
            titleOutline.effectDistance = new Vector2(2f, -2f);

            // 3.4. Подзаголовок "Нажмите [ R ], чтобы начать заново"
            var subGo = new GameObject("SubtitleText");
            subGo.transform.SetParent(_contentContainerRect, false);
            var subRect = subGo.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.5f, 0.42f);
            subRect.anchorMax = new Vector2(0.5f, 0.42f);
            subRect.pivot = new Vector2(0.5f, 0.5f);
            subRect.sizeDelta = new Vector2(800f, 60f);

            _subtitleText = subGo.AddComponent<Text>();
            _subtitleText.font = _uiFont;
            _subtitleText.fontSize = 24;
            _subtitleText.alignment = TextAnchor.MiddleCenter;
            _subtitleText.text = "Нажмите [ R ], чтобы начать заново";
            _subtitleText.color = subtitleColor;

            var subShadow = subGo.AddComponent<Shadow>();
            subShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            subShadow.effectDistance = new Vector2(2f, -2f);

            // 3.5. Кнопка "Начать заново"
            var btnGo = new GameObject("RestartButton");
            btnGo.transform.SetParent(_contentContainerRect, false);
            var btnRect = btnGo.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.20f);
            btnRect.anchorMax = new Vector2(0.5f, 0.20f);
            btnRect.pivot = new Vector2(0.5f, 0.5f);
            btnRect.sizeDelta = new Vector2(320f, 54f);

            var btnImage = btnGo.AddComponent<Image>();
            btnImage.sprite = _whiteSprite;
            btnImage.color = buttonBgColor;

            var btn = btnGo.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = buttonBgColor;
            colors.highlightedColor = buttonHoverColor;
            colors.pressedColor = new Color(0.5f, 0.1f, 0.15f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(RestartGame);

            var btnTextGo = new GameObject("BtnText");
            btnTextGo.transform.SetParent(btnGo.transform, false);
            var btnTextRect = btnTextGo.AddComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.sizeDelta = Vector2.zero;

            var btnText = btnTextGo.AddComponent<Text>();
            btnText.font = _uiFont;
            btnText.fontSize = 18;
            btnText.fontStyle = FontStyle.Bold;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.text = "НАЧАТЬ ЗАНОВО [ R ]";
            btnText.color = Color.white;

            // 3.6. Кнопка "В главное меню"
            var menuBtnGo = new GameObject("MainMenuButton");
            menuBtnGo.transform.SetParent(_contentContainerRect, false);
            var menuBtnRect = menuBtnGo.AddComponent<RectTransform>();
            menuBtnRect.anchorMin = new Vector2(0.5f, 0.10f);
            menuBtnRect.anchorMax = new Vector2(0.5f, 0.10f);
            menuBtnRect.pivot = new Vector2(0.5f, 0.5f);
            menuBtnRect.sizeDelta = new Vector2(320f, 44f);

            var menuBtnImage = menuBtnGo.AddComponent<Image>();
            menuBtnImage.sprite = _whiteSprite;
            menuBtnImage.color = new Color(0.10f, 0.12f, 0.18f, 0.95f);

            var menuBtn = menuBtnGo.AddComponent<Button>();
            var menuColors = menuBtn.colors;
            menuColors.normalColor = new Color(0.10f, 0.12f, 0.18f, 0.95f);
            menuColors.highlightedColor = new Color(0.18f, 0.22f, 0.32f, 1.0f);
            menuColors.pressedColor = new Color(0.06f, 0.08f, 0.12f, 1f);
            menuBtn.colors = menuColors;
            menuBtn.onClick.AddListener(ReturnToMainMenu);

            var menuBtnTextGo = new GameObject("MenuBtnText");
            menuBtnTextGo.transform.SetParent(menuBtnGo.transform, false);
            var menuBtnTextRect = menuBtnTextGo.AddComponent<RectTransform>();
            menuBtnTextRect.anchorMin = Vector2.zero;
            menuBtnTextRect.anchorMax = Vector2.one;
            menuBtnTextRect.sizeDelta = Vector2.zero;

            var menuBtnText = menuBtnTextGo.AddComponent<Text>();
            menuBtnText.font = _uiFont;
            menuBtnText.fontSize = 14;
            menuBtnText.fontStyle = FontStyle.Bold;
            menuBtnText.alignment = TextAnchor.MiddleCenter;
            menuBtnText.text = "В ГЛАВНОЕ МЕНЮ [ ESC ]";
            menuBtnText.color = new Color(0.85f, 0.88f, 0.94f, 0.9f);

            // 4. Занавес моментального затемнения (рисуется ПОВЕРХ ВСЕГО и мгновенно гасит экран)
            _blackCurtainGo = new GameObject("InstantBlackCurtain");
            _blackCurtainGo.transform.SetParent(_screenRoot.transform, false);
            _blackCurtainGo.transform.SetAsLastSibling();

            var curtainRect = _blackCurtainGo.AddComponent<RectTransform>();
            curtainRect.anchorMin = Vector2.zero;
            curtainRect.anchorMax = Vector2.one;
            curtainRect.sizeDelta = Vector2.zero;

            _curtainImage = _blackCurtainGo.AddComponent<Image>();
            _curtainImage.sprite = _whiteSprite;
            _curtainImage.color = Color.black;
            _curtainImage.raycastTarget = false;

            _curtainCanvasGroup = _blackCurtainGo.AddComponent<CanvasGroup>();
            _curtainCanvasGroup.alpha = 1f;
        }

        private void Update()
        {
            if (!_isShowing) return;

            // Легкая пульсация текста подсказки
            if (_subtitleText != null)
            {
                float pulse = 0.75f + Mathf.PingPong(Time.unscaledTime * 1.5f, 0.25f);
                Color c = subtitleColor;
                c.a = pulse;
                _subtitleText.color = c;
            }

            // Обработка клавиш перезапуска и выхода в меню
            bool restartTriggered = false;
            bool menuTriggered = false;

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.rKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
                {
                    restartTriggered = true;
                }
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    menuTriggered = true;
                }
            }
#endif
            try
            {
                if (!restartTriggered && (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
                {
                    restartTriggered = true;
                }
                if (!menuTriggered && Input.GetKeyDown(KeyCode.Escape))
                {
                    menuTriggered = true;
                }
            }
            catch { }

            if (restartTriggered)
            {
                RestartGame();
            }
            else if (menuTriggered)
            {
                ReturnToMainMenu();
            }
        }

        public static void ReturnToMainMenu()
        {
            Debug.Log("<color=#4EE2EC><b>[DEATH SCREEN]</b></color> Возврат в главное меню...");
            Time.timeScale = 1.0f;
            SceneManager.LoadScene("MainMenu");
        }

        /// <summary>
        /// Показать экран гибели: моментальная тьма с последующим медленным развеиванием.
        /// </summary>
        public static void ShowDeathScreen(string customTitle = null, string customSubtitle = null)
        {
            if (Instance == null)
            {
                var existing = FindAnyObjectByType<PlayerDeathScreenUI>();
                if (existing != null)
                {
                    Instance = existing;
                }
                else
                {
                    var go = new GameObject("[Player_DeathScreen_UI]");
                    Instance = go.AddComponent<PlayerDeathScreenUI>();
                }
            }

            Instance.EnsureAssets();
            Instance.EnsureUIBuilt();

            if (!string.IsNullOrEmpty(customTitle) && Instance._titleText != null)
            {
                Instance._titleText.text = customTitle;
            }
            if (string.IsNullOrEmpty(customSubtitle))
            {
                var gen = LevelGeneration.LevelSequenceGenerator.Instance;
                if (gen != null && Instance._subtitleText != null)
                {
                    Instance._subtitleText.text = $"Погиб на Арене {gen.CurrentRun} из {gen.MaxRooms}. Нажмите [R], чтобы начать заново.";
                }
            }
            else if (Instance._subtitleText != null)
            {
                Instance._subtitleText.text = customSubtitle;
            }

            Instance.TriggerShow();
        }

        private void TriggerShow()
        {
            if (_isShowing) return;
            _isShowing = true;

            EnsureAssets();
            EnsureUIBuilt();

            if (_screenRoot != null)
            {
                _screenRoot.SetActive(true);
            }

            // 1. Моментальное затемнение: в кадр смерти экран мгновенно гаснет в 100% чёрный
            if (_blackCurtainGo != null)
            {
                _blackCurtainGo.SetActive(true);
                _blackCurtainGo.transform.SetAsLastSibling();
            }
            if (_curtainCanvasGroup != null)
            {
                _curtainCanvasGroup.alpha = 1.0f;
            }
            if (_curtainImage != null)
            {
                _curtainImage.color = Color.black;
            }

            // Содержимое экрана смерти под тьмой скрыто и плавно появится при развеивании
            if (_contentCanvasGroup != null)
            {
                _contentCanvasGroup.alpha = 0f;
            }
            if (_contentContainerRect != null)
            {
                _contentContainerRect.localScale = new Vector3(0.92f, 0.92f, 1f);
            }

            StopAllCoroutines();
            StartCoroutine(DeathSequenceRoutine());
        }

        private IEnumerator DeathSequenceRoutine()
        {
            // 1. Моментальная тьма: удерживаем экран в абсолютной темноте
            if (_curtainCanvasGroup != null) _curtainCanvasGroup.alpha = 1.0f;
            if (_contentCanvasGroup != null) _contentCanvasGroup.alpha = 0f;

            if (blackoutHoldDuration > 0.001f)
            {
                float holdTimer = 0f;
                while (holdTimer < blackoutHoldDuration)
                {
                    holdTimer += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            // 2. Медленное развеивание тьмы и проявление экрана смерти
            float elapsed = 0f;
            while (elapsed < dissolveDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dissolveDuration);

                // Плавная кинематографичная кривая рассеивания (SmoothStep)
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                // Тьма медленно развеивается (1.0 -> 0.0)
                if (_curtainCanvasGroup != null)
                {
                    _curtainCanvasGroup.alpha = 1.0f - smoothT;
                }

                // Экран смерти плавно проявляется (0.0 -> 1.0)
                if (_contentCanvasGroup != null)
                {
                    _contentCanvasGroup.alpha = Mathf.Clamp01(t * 1.15f);
                }

                // Легкое кинематографичное приближение текста "ВЫ УМЕРЛИ"
                if (_contentContainerRect != null)
                {
                    float scale = Mathf.Lerp(0.92f, 1.0f, smoothT);
                    _contentContainerRect.localScale = new Vector3(scale, scale, 1f);
                }

                yield return null;
            }

            // 3. Финал: тьма полностью рассеялась, экран смерти полностью открыт
            if (_curtainCanvasGroup != null) _curtainCanvasGroup.alpha = 0f;
            if (_blackCurtainGo != null) _blackCurtainGo.SetActive(false);
            if (_contentCanvasGroup != null) _contentCanvasGroup.alpha = 1f;
            if (_contentContainerRect != null) _contentContainerRect.localScale = Vector3.one;
        }

        /// <summary>
        /// Перезагрузить текущую активную сцену
        /// </summary>
        public static void RestartGame()
        {
            Debug.Log("<color=#FF3344><b>[DEATH SCREEN]</b></color> Перезагрузка сцены...");
            Time.timeScale = 1.0f;
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.buildIndex >= 0)
            {
                SceneManager.LoadScene(activeScene.buildIndex);
            }
            else
            {
                SceneManager.LoadScene(activeScene.name);
            }
        }
    }
}
