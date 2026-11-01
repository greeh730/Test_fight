using System;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.UI
{
    /// <summary>
    /// Стильный счётчик пройденных комнат/арен вверху по центру экрана (Top-Center HUD).
    /// Отображает текущую арену и максимальное число (например, "АРЕНА 1 / 13").
    /// Автоматически подписывается на события генератора уровней и плавно анимирует смену арен.
    /// </summary>
    [DisallowMultipleComponent]
    public class ArenaCounterUI : MonoBehaviour
    {
        public static ArenaCounterUI Instance { get; private set; }

        [Header("--- UI References ---")]
        [SerializeField] private RectTransform rootRect;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image topGlowStrip;
        [SerializeField] private Text arenaText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("--- Colors ---")]
        [SerializeField] private Color normalTextColor = new Color(0.92f, 0.96f, 1.0f, 1.0f);
        [SerializeField] private Color finalTextColor = new Color(1.0f, 0.85f, 0.25f, 1.0f); // Золотой для 13-й финальной арены
        [SerializeField] private Color panelBgColor = new Color(0.05f, 0.08f, 0.14f, 0.88f);
        [SerializeField] private Color panelBorderColor = new Color(0.18f, 0.75f, 0.92f, 0.55f);

        [Header("--- Punch Animation ---")]
        [SerializeField] private float punchScale = 1.18f;
        [SerializeField] private float punchReturnSpeed = 7f;

        private int _currentArena = 1;
        private int _maxArenas = 13;
        private float _currentScale = 1.0f;
        private static Sprite _whiteSprite;

        private static Sprite GetWhiteSprite()
        {
            if (_whiteSprite == null)
            {
                Texture2D tex = new Texture2D(2, 2);
                tex.SetPixels(new Color[] { Color.white, Color.white, Color.white, Color.white });
                tex.Apply();
                _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 1f);
            }
            return _whiteSprite;
        }

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

            EnsureUIHierarchy();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnEnable()
        {
            LevelGeneration.LevelSequenceGenerator.OnArenaProgressChanged += HandleArenaProgressChanged;
            RefreshDisplay(instant: true);
        }

        private void OnDisable()
        {
            LevelGeneration.LevelSequenceGenerator.OnArenaProgressChanged -= HandleArenaProgressChanged;
        }

        private void Start()
        {
            var gen = LevelGeneration.LevelSequenceGenerator.Instance;
            if (gen != null)
            {
                SetProgress(gen.CurrentRun, gen.MaxRooms, instant: true);
            }
            else
            {
                RefreshDisplay(instant: true);
            }
        }

        private void Update()
        {
            var gen = LevelGeneration.LevelSequenceGenerator.Instance;
            if (gen != null && (_currentArena != gen.CurrentRun || _maxArenas != gen.MaxRooms))
            {
                SetProgress(gen.CurrentRun, gen.MaxRooms, instant: false);
            }

            // Плавное возвращение масштаба после анимации перехода
            if (rootRect != null && _currentScale > 1.001f)
            {
                _currentScale = Mathf.Lerp(_currentScale, 1.0f, Time.deltaTime * punchReturnSpeed);
                rootRect.localScale = Vector3.one * _currentScale;
            }
        }

        private void HandleArenaProgressChanged(int current, int max)
        {
            SetProgress(current, max, instant: false);
        }

        public void SetProgress(int current, int max, bool instant = false)
        {
            _currentArena = Mathf.Max(1, current);
            _maxArenas = Mathf.Max(1, max);

            if (!instant)
            {
                _currentScale = punchScale;
                if (rootRect != null) rootRect.localScale = Vector3.one * _currentScale;
            }

            RefreshDisplay(instant);
        }

        public void RefreshDisplay(bool instant = false)
        {
            EnsureUIHierarchy();
            if (arenaText == null) return;

            bool isFinal = _currentArena >= _maxArenas;
            if (isFinal)
            {
                arenaText.text = $"★  ФИНАЛ: АРЕНА {_currentArena} / {_maxArenas}  ★";
                arenaText.color = finalTextColor;
                if (topGlowStrip != null) topGlowStrip.color = new Color(1.0f, 0.82f, 0.2f, 0.95f);
            }
            else
            {
                arenaText.text = $"АРЕНА  {_currentArena} / {_maxArenas}";
                arenaText.color = normalTextColor;
                if (topGlowStrip != null) topGlowStrip.color = new Color(0.2f, 0.85f, 1.0f, 0.85f);
            }
        }

        public void EnsureUIHierarchy()
        {
            if (rootRect == null)
            {
                rootRect = GetComponent<RectTransform>();
            }

            if (rootRect == null)
            {
                rootRect = gameObject.AddComponent<RectTransform>();
            }

            // Позиционирование: строго по центру сверху экрана
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -16f);
            rootRect.sizeDelta = new Vector2(230f, 40f);

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            }

            // 1. Фон плашки
            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<Image>();
                if (backgroundImage == null) backgroundImage = gameObject.AddComponent<Image>();
                backgroundImage.sprite = GetWhiteSprite();
                backgroundImage.type = Image.Type.Simple;
                backgroundImage.color = panelBgColor;
                backgroundImage.raycastTarget = false;
            }

            // Обводка плашки
            var outline = GetComponent<Outline>();
            if (outline == null)
            {
                outline = gameObject.AddComponent<Outline>();
                outline.effectColor = panelBorderColor;
                outline.effectDistance = new Vector2(1.5f, 1.5f);
            }

            // 2. Верхняя светящаяся неоновая полоска
            if (topGlowStrip == null)
            {
                var stripTransform = transform.Find("TopGlowStrip");
                if (stripTransform != null)
                {
                    topGlowStrip = stripTransform.GetComponent<Image>();
                }
                else
                {
                    var stripGo = new GameObject("TopGlowStrip", typeof(RectTransform), typeof(Image));
                    stripGo.transform.SetParent(transform, false);
                    var sRt = stripGo.GetComponent<RectTransform>();
                    sRt.anchorMin = new Vector2(0f, 1f);
                    sRt.anchorMax = new Vector2(1f, 1f);
                    sRt.pivot = new Vector2(0.5f, 1f);
                    sRt.anchoredPosition = Vector2.zero;
                    sRt.sizeDelta = new Vector2(0f, 3f);

                    topGlowStrip = stripGo.GetComponent<Image>();
                    topGlowStrip.sprite = GetWhiteSprite();
                    topGlowStrip.color = new Color(0.2f, 0.85f, 1.0f, 0.85f);
                    topGlowStrip.raycastTarget = false;
                }
            }

            // 3. Текст счётчика
            if (arenaText == null)
            {
                var textTransform = transform.Find("ArenaText");
                if (textTransform != null)
                {
                    arenaText = textTransform.GetComponent<Text>();
                }
                else
                {
                    var textGo = new GameObject("ArenaText", typeof(RectTransform), typeof(Text));
                    textGo.transform.SetParent(transform, false);
                    var tRt = textGo.GetComponent<RectTransform>();
                    tRt.anchorMin = Vector2.zero;
                    tRt.anchorMax = Vector2.one;
                    tRt.pivot = new Vector2(0.5f, 0.5f);
                    tRt.offsetMin = new Vector2(8f, 0f);
                    tRt.offsetMax = new Vector2(-8f, -2f);

                    arenaText = textGo.GetComponent<Text>();
                    arenaText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                    arenaText.fontSize = 17;
                    arenaText.fontStyle = FontStyle.Bold;
                    arenaText.alignment = TextAnchor.MiddleCenter;
                    arenaText.color = normalTextColor;
                    arenaText.raycastTarget = false;

                    var textOutline = textGo.AddComponent<Outline>();
                    textOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
                    textOutline.effectDistance = new Vector2(1.2f, -1.2f);
                }
            }
        }
    }
}
