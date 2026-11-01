using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.Roguelike
{
    /// <summary>
    /// Интерфейс выбора карточек Roguelike-улучшений при прохождении уровня (Кристалл Победы).
    /// Предлагает 5 карточек на выбор, из которых игрок выбирает ровно 2 улучшения.
    /// Карточки компактно размещены в один стильный ряд и адаптированы под Full HD экран.
    /// </summary>
    [DisallowMultipleComponent]
    public class RoguelikeUpgradeUI : MonoBehaviour
    {
        public static RoguelikeUpgradeUI Instance { get; private set; }

        [Header("--- Visual Colors ---")]
        [SerializeField] private Color tacticianColor = new Color(0f, 0.95f, 1f, 1f);      // Cyan
        [SerializeField] private Color combatBuffColor = new Color(0.2f, 1f, 0.55f, 1f);   // Emerald / Green
        [SerializeField] private Color riskColor = new Color(1f, 0.25f, 0.35f, 1f);        // Crimson Red
        [SerializeField] private Color panelBgColor = new Color(0.03f, 0.04f, 0.06f, 0.92f);
        [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.35f, 1f);

        [Header("--- Card Frames (Vintage Playing Cards) ---")]
        [SerializeField] private Sprite greenCardFrame;
        [SerializeField] private Sprite redCardFrame;

        [Header("--- Font Sizes (Размеры текста) ---")]
        [Tooltip("Размер шрифта названия карточки")]
        [SerializeField] private int titleFontSize = 16;
        [Tooltip("Размер шрифта описания карточки")]
        [SerializeField] private int descFontSize = 12;
        [Tooltip("Размер шрифта положительного баффа (зеленый текст)")]
        [SerializeField] private int buffFontSize = 14;
        [Tooltip("Размер шрифта отрицательного дебаффа (красный текст)")]
        [SerializeField] private int debuffFontSize = 14;
        [Tooltip("Размер шрифта бейджа масти")]
        [SerializeField] private int badgeFontSize = 11;
        [Tooltip("Размер шрифта текста кнопки")]
        [SerializeField] private int buttonFontSize = 13;

        [Header("--- Layout Dimensions (5 Cards Row) ---")]
        [Tooltip("Ширина каждой отдельной карточки")]
        [SerializeField] private float cardWidth = 230f;
        [Tooltip("Высота каждой отдельной карточки")]
        [SerializeField] private float cardHeight = 450f;
        [Tooltip("Отступ между карточками")]
        [SerializeField] private float cardSpacing = 16f;

        [Header("--- UI Container Elements ---")]
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private GameObject modalRoot;
        [SerializeField] private Transform cardsContainer;
        [SerializeField] private Text subtitleText;

        private class CardSlot
        {
            public UpgradeCardDefinition card;
            public GameObject cardObj;
            public Button button;
            public Text buttonText;
            public Image buttonImage;
            public Outline outline;
            public Color themeColor;
            public bool isSelected;
        }

        private Action _onSelectionFinished;
        private Font _uiFont;
        private Sprite _whiteSprite;
        private readonly Dictionary<string, Sprite> _iconCache = new Dictionary<string, Sprite>();
        private readonly List<CardSlot> _cardSlots = new List<CardSlot>();
        private int _totalPicks = 2;
        private int _picksRemaining = 2;
        private Coroutine _finishRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureAssets();
            EnsureUIBuilt();
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
                var tex = Texture2D.whiteTexture;
                _whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }

            if (greenCardFrame == null)
            {
                greenCardFrame = LoadSpriteSafe("Cards/card_frame_green", "Assets/Resources/Cards/card_frame_green.png");
            }

            if (redCardFrame == null)
            {
                redCardFrame = LoadSpriteSafe("Cards/card_frame_red", "Assets/Resources/Cards/card_frame_red.png");
            }
        }

        private void EnsureUIBuilt()
        {
            if (targetCanvas == null)
            {
                targetCanvas = FindAnyObjectByType<Canvas>();
            }

            if (targetCanvas == null)
            {
                var cGo = new GameObject("Roguelike_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                targetCanvas = cGo.GetComponent<Canvas>();
                targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            // Если модалка уже есть, удаляем старую чтобы гарантировать актуальные размеры 5 карточек
            var existing = targetCanvas.transform.Find("RoguelikeUpgradeModal");
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
                modalRoot = null;
                cardsContainer = null;
                subtitleText = null;
            }

            BuildUIHierarchy();

            if (modalRoot != null)
            {
                modalRoot.SetActive(false);
            }
        }

        private void BuildUIHierarchy()
        {
            // 1. Полноэкранный темный оверлей
            modalRoot = new GameObject("RoguelikeUpgradeModal", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            modalRoot.transform.SetParent(targetCanvas.transform, false);
            var rootRt = modalRoot.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var bgImg = modalRoot.GetComponent<Image>();
            bgImg.sprite = _whiteSprite;
            bgImg.color = panelBgColor;

            // 2. Центральная панель контента (ширина 1280px для 5 карточек)
            var centerPanelObj = new GameObject("CenterDialog", typeof(RectTransform));
            centerPanelObj.transform.SetParent(modalRoot.transform, false);
            var panelRt = centerPanelObj.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(1280f, 630f);
            panelRt.anchoredPosition = Vector2.zero;

            // 3. Заголовок
            var headerObj = new GameObject("HeaderTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            headerObj.transform.SetParent(centerPanelObj.transform, false);
            var headerRt = headerObj.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0.5f, 1.0f);
            headerRt.anchorMax = new Vector2(0.5f, 1.0f);
            headerRt.pivot = new Vector2(0.5f, 1.0f);
            headerRt.anchoredPosition = new Vector2(0f, -10f);
            headerRt.sizeDelta = new Vector2(1000f, 44f);

            var headerText = headerObj.GetComponent<Text>();
            headerText.font = _uiFont;
            headerText.fontSize = 26;
            headerText.fontStyle = FontStyle.Bold;
            headerText.alignment = TextAnchor.MiddleCenter;
            headerText.color = new Color(0.95f, 0.98f, 1f, 1f);
            headerText.text = "РЕЗОНАНС КРИСТАЛЛА ПОБЕДЫ";

            // 4. Подзаголовок (динамически отображает статус выбора)
            var subObj = new GameObject("Subtitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            subObj.transform.SetParent(centerPanelObj.transform, false);
            var subRt = subObj.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0.5f, 1.0f);
            subRt.anchorMax = new Vector2(0.5f, 1.0f);
            subRt.pivot = new Vector2(0.5f, 1.0f);
            subRt.anchoredPosition = new Vector2(0f, -54f);
            subRt.sizeDelta = new Vector2(1000f, 26f);

            subtitleText = subObj.GetComponent<Text>();
            subtitleText.font = _uiFont;
            subtitleText.fontSize = 15;
            subtitleText.alignment = TextAnchor.MiddleCenter;
            subtitleText.color = new Color(0.68f, 0.78f, 0.88f, 0.95f);
            subtitleText.text = "Выберите 2 улучшения для усиления вашей боевой мощи (выбрано: 0 из 2):";

            // 5. Контейнер 5 карточек (выровнен по центру)
            float totalWidth = 5f * cardWidth + 4f * cardSpacing;
            var containerObj = new GameObject("CardsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            containerObj.transform.SetParent(centerPanelObj.transform, false);
            var cRt = containerObj.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0.5f, 0f);
            cRt.anchorMax = new Vector2(0.5f, 0f);
            cRt.pivot = new Vector2(0.5f, 0f);
            cRt.anchoredPosition = new Vector2(0f, 20f);
            cRt.sizeDelta = new Vector2(totalWidth, cardHeight);

            var hlg = containerObj.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = cardSpacing;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            cardsContainer = containerObj.transform;
        }

        /// <summary>
        /// Показывает окно выбора карточек. По умолчанию предлагается выбрать ровно 2 карточки.
        /// </summary>
        public void ShowSelection(List<UpgradeCardDefinition> cards, int pickCount, Action onComplete)
        {
            EnsureAssets();
            EnsureUIBuilt();

            _onSelectionFinished = onComplete;
            _totalPicks = Mathf.Min(pickCount, cards.Count);
            _picksRemaining = _totalPicks;

            if (_finishRoutine != null)
            {
                StopCoroutine(_finishRoutine);
                _finishRoutine = null;
            }

            // Очищаем старые карточки
            for (int i = cardsContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(cardsContainer.GetChild(i).gameObject);
            }
            _cardSlots.Clear();

            // Создаем 5 карточек
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                var slot = CreateCardView(card, i);
                _cardSlots.Add(slot);
            }

            UpdateSubtitle();

            modalRoot.SetActive(true);
            modalRoot.transform.SetAsLastSibling();

            Canvas.ForceUpdateCanvases();
            if (cardsContainer is RectTransform containerRt)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(containerRt);
            }

            // Пауза и курсор
            Time.timeScale = 0f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void ShowSelection(List<UpgradeCardDefinition> cards, Action onComplete)
        {
            ShowSelection(cards, 2, onComplete);
        }

        private CardSlot CreateCardView(UpgradeCardDefinition card, int index)
        {
            bool isCombatBuff = IsBuffCard(card);
            Color themeColor = isCombatBuff ? combatBuffColor : riskColor;
            string categoryLabel = isCombatBuff ? "[ ♠ БОЕВОЙ БАФФ ♠ ]" : "[ ♦ РИСК И НАГРАДА ♦ ]";

            // 1. Корневой объект карточки (внутри HorizontalLayoutGroup)
            var cardObj = new GameObject($"Card_{index}_{card.id}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(LayoutElement));
            cardObj.transform.SetParent(cardsContainer, false);

            var le = cardObj.GetComponent<LayoutElement>();
            le.preferredWidth = cardWidth;
            le.preferredHeight = cardHeight;
            le.minWidth = cardWidth;
            le.minHeight = cardHeight;

            Sprite frameSprite = isCombatBuff ? greenCardFrame : redCardFrame;
            Color innerParchmentColor = isCombatBuff
                ? new Color(0.04f, 0.10f, 0.06f, 0.92f)
                : new Color(0.12f, 0.03f, 0.05f, 0.92f);

            var cardImg = cardObj.GetComponent<Image>();
            if (frameSprite != null)
            {
                cardImg.sprite = frameSprite;
                cardImg.color = Color.white;
            }
            else
            {
                cardImg.sprite = _whiteSprite;
                cardImg.color = innerParchmentColor;
            }

            var outline = cardObj.GetComponent<Outline>();
            outline.effectColor = themeColor;
            outline.effectDistance = new Vector2(2.5f, 2.5f);

            var innerBgObj = new GameObject("InnerParchment", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            innerBgObj.transform.SetParent(cardObj.transform, false);
            var innerRt = innerBgObj.GetComponent<RectTransform>();
            innerRt.anchorMin = Vector2.zero;
            innerRt.anchorMax = Vector2.one;
            innerRt.offsetMin = new Vector2(10f, 10f);
            innerRt.offsetMax = new Vector2(-10f, -10f);
            var innerImg = innerBgObj.GetComponent<Image>();
            innerImg.sprite = _whiteSprite;
            innerImg.color = innerParchmentColor;
            innerBgObj.transform.SetAsFirstSibling();

            // Playing card corner indices
            string suitSymbol = isCombatBuff ? "♠" : "♦";
            CreateCornerIndex(cardObj.transform, suitSymbol + "\n" + (index + 1), new Vector2(0f, 1f), new Vector2(10f, -8f), TextAnchor.UpperCenter);
            CreateCornerIndex(cardObj.transform, (index + 1) + "\n" + suitSymbol, new Vector2(1f, 0f), new Vector2(-10f, 8f), TextAnchor.LowerCenter);

            // 2. Внутренний контейнер контента
            var contentContainer = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
            contentContainer.transform.SetParent(cardObj.transform, false);
            var contentRt = contentContainer.GetComponent<RectTransform>();
            contentRt.anchorMin = Vector2.zero;
            contentRt.anchorMax = Vector2.one;
            contentRt.offsetMin = new Vector2(14f, 58f);
            contentRt.offsetMax = new Vector2(-14f, -12f);

            var layout = contentContainer.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // А. Бейдж категории
            CreateTextElement(contentContainer.transform, "CategoryBadge", categoryLabel, badgeFontSize, FontStyle.Bold, themeColor, TextAnchor.MiddleCenter, 18f);

            // Б. Иконка-символ
            // Pixel Art UI Icon in vignette medallion
            CreateIconMedallion(contentContainer.transform, card, themeColor);

            // C. Название улучшения
            CreateTextElement(contentContainer.transform, "Title", card.title, titleFontSize, FontStyle.Bold, new Color(1f, 0.98f, 0.92f, 1f), TextAnchor.MiddleCenter, 34f);

            // D. Винтажный декоративный разделитель
            CreateTextElement(contentContainer.transform, "Divider", "── ✦ ──", 10, FontStyle.Normal, new Color(0.85f, 0.75f, 0.45f, 0.70f), TextAnchor.MiddleCenter, 12f);

            // E. Описание карты
            CreateTextElement(contentContainer.transform, "Desc", card.description, descFontSize, FontStyle.Italic, new Color(0.85f, 0.88f, 0.92f, 0.95f), TextAnchor.MiddleCenter, 44f);

            // F. Текст эффектов прямо на карточке (без отдельных прямоугольников)
            if (!string.IsNullOrEmpty(card.positiveEffectText))
            {
                string pos = card.positiveEffectText.Trim();
                if (!pos.StartsWith("+") && !pos.StartsWith("✓")) pos = "+ " + pos;
                CreateEffectText(contentContainer.transform, "PositiveEffect", pos, new Color(0.25f, 1f, 0.55f, 1f), buffFontSize);
            }

            if (!string.IsNullOrEmpty(card.negativeEffectText))
            {
                string neg = card.negativeEffectText.Trim();
                if (!neg.StartsWith("-") && !neg.StartsWith("⚠") && !neg.StartsWith("✖")) neg = "- " + neg;
                CreateEffectText(contentContainer.transform, "NegativeEffect", neg, new Color(1f, 0.35f, 0.38f, 1f), debuffFontSize);
            }

            // G. Vintage Card Seal Button
            Color btnBaseColor = isCombatBuff
                ? new Color(0.12f, 0.30f, 0.18f, 0.98f)
                : new Color(0.34f, 0.08f, 0.12f, 0.98f);

            var btnObj = new GameObject("SelectButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(Button));
            btnObj.transform.SetParent(cardObj.transform, false);

            var btnRt = btnObj.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.06f, 0f);
            btnRt.anchorMax = new Vector2(0.94f, 0f);
            btnRt.pivot = new Vector2(0.5f, 0f);
            btnRt.anchoredPosition = new Vector2(0f, 10f);
            btnRt.sizeDelta = new Vector2(0f, 36f);

            var btnImg = btnObj.GetComponent<Image>();
            btnImg.sprite = _whiteSprite;
            btnImg.color = btnBaseColor;

            var btnOutline = btnObj.GetComponent<Outline>();
            btnOutline.effectColor = new Color(0.85f, 0.75f, 0.45f, 0.85f);
            btnOutline.effectDistance = new Vector2(1.2f, 1.2f);

            var btn = btnObj.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = btnBaseColor;
            colors.highlightedColor = btnBaseColor * 1.35f;
            colors.pressedColor = Color.white;
            btn.colors = colors;

            var btnTextObj = new GameObject("BtnText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            btnTextObj.transform.SetParent(btnObj.transform, false);
            var bRt = btnTextObj.GetComponent<RectTransform>();
            bRt.anchorMin = Vector2.zero;
            bRt.anchorMax = Vector2.one;
            bRt.offsetMin = Vector2.zero;
            bRt.offsetMax = Vector2.zero;

            var bText = btnTextObj.GetComponent<Text>();
            bText.font = _uiFont;
            bText.fontSize = buttonFontSize;
            bText.fontStyle = FontStyle.Bold;
            bText.alignment = TextAnchor.MiddleCenter;
            bText.color = new Color(1f, 0.98f, 0.92f, 1f);
            bText.text = "ВЫБРАТЬ";

            var slot = new CardSlot
            {
                card = card,
                cardObj = cardObj,
                button = btn,
                buttonText = bText,
                buttonImage = btnImg,
                outline = outline,
                themeColor = themeColor,
                isSelected = false
            };

            btn.onClick.AddListener(() => OnCardClicked(slot));

            return slot;
        }

        private Text CreateTextElement(Transform parent, string name, string text, int fontSize, FontStyle style, Color color, TextAnchor alignment, float preferredHeight)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, preferredHeight);

            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
            le.minHeight = preferredHeight;

            var txt = go.GetComponent<Text>();
            txt.font = _uiFont;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = alignment;
            txt.text = text;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Truncate;

            return txt;
        }

        private void OnCardClicked(CardSlot slot)
        {
            if (slot == null || slot.isSelected || _picksRemaining <= 0) return;

            slot.isSelected = true;
            _picksRemaining--;

            // Применяем улучшение через менеджер
            if (RoguelikeUpgradeManager.Instance != null)
            {
                RoguelikeUpgradeManager.Instance.ApplyUpgrade(slot.card);
            }

            // Визуальная фиксация выбора на карточке
            if (slot.button != null)
            {
                slot.button.interactable = false;
            }
            if (slot.buttonText != null)
            {
                slot.buttonText.text = "ВЫБРАНО ✓";
                slot.buttonText.color = Color.black;
            }
            if (slot.buttonImage != null)
            {
                slot.buttonImage.color = selectedColor;
            }
            if (slot.outline != null)
            {
                slot.outline.effectColor = selectedColor;
                slot.outline.effectDistance = new Vector2(3f, 3f);
            }

            // Обновляем статус
            UpdateSubtitle();

            // Если все выборы сделаны
            if (_picksRemaining <= 0)
            {
                // Отключаем кнопки у оставшихся карточек
                foreach (var s in _cardSlots)
                {
                    if (!s.isSelected && s.button != null)
                    {
                        s.button.interactable = false;
                    }
                }

                if (_finishRoutine != null) StopCoroutine(_finishRoutine);
                _finishRoutine = StartCoroutine(FinishSelectionRoutine());
            }
        }

        private void UpdateSubtitle()
        {
            if (subtitleText == null) return;

            int chosen = _totalPicks - _picksRemaining;
            if (_picksRemaining > 0)
            {
                if (chosen == 0)
                {
                    subtitleText.color = new Color(0.68f, 0.78f, 0.88f, 0.95f);
                    subtitleText.text = $"Выберите {_totalPicks} улучшения для усиления боевой мощи (выбрано: 0 из {_totalPicks}):";
                }
                else
                {
                    subtitleText.color = new Color(0.35f, 1.0f, 0.75f, 1f);
                    subtitleText.text = $"Отлично! Выберите еще одно улучшение (выбрано: {chosen} из {_totalPicks}):";
                }
            }
            else
            {
                subtitleText.color = new Color(0.20f, 1.0f, 0.60f, 1f);
                subtitleText.text = "Все улучшения выбраны! Загрузка нового сектора...";
            }
        }

        private IEnumerator FinishSelectionRoutine()
        {
            yield return new WaitForSecondsRealtime(0.40f);

            Hide();

            // Возобновляем время
            Time.timeScale = 1.0f;

            // Запускаем перегенерацию уровня
            _onSelectionFinished?.Invoke();
            _onSelectionFinished = null;
            _finishRoutine = null;
        }

        private static Sprite LoadSpriteSafe(string resourcePath, string fallbackDiskPath)
        {
            try
            {
                var s = Resources.Load<Sprite>(resourcePath);
                if (s != null) return s;

                var all = Resources.LoadAll<Sprite>(resourcePath);
                if (all != null && all.Length > 0 && all[0] != null) return all[0];

                string fullPath = System.IO.Path.Combine(Application.dataPath, "..", fallbackDiskPath);
                fullPath = System.IO.Path.GetFullPath(fullPath);
                if (System.IO.File.Exists(fullPath))
                {
                    byte[] bytes = System.IO.File.ReadAllBytes(fullPath);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(bytes))
                    {
                        tex.filterMode = FilterMode.Point;
                        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RoguelikeUpgradeUI] РћС€РёР±РєР° Р·Р°РіСЂСѓР·РєРё СЃРїСЂР°Р№С‚Р°: " + ex.Message);
            }
            return null;
        }

        private Sprite GetIconForCard(UpgradeCardDefinition card)
        {
            if (card == null) return null;
            if (card.iconSprite != null) return card.iconSprite;

            string id = card.id ?? "";
            if (_iconCache.TryGetValue(id, out var cached) && cached != null)
            {
                return cached;
            }

            string iconFileName = GetIconFileName(id);
            if (!string.IsNullOrEmpty(iconFileName))
            {
                var sprite = LoadSpriteSafe("Cards/Icons/" + iconFileName, "Assets/Resources/Cards/Icons/" + iconFileName + ".png");
                if (sprite == null)
                {
                    sprite = LoadSpriteSafe("Cards/Icons/" + iconFileName, "Assets/Sprites/UI/Cards/Icons/" + iconFileName + ".png");
                }
                if (sprite != null)
                {
                    _iconCache[id] = sprite;
                    return sprite;
                }
            }
            return null;
        }

        private static string GetIconFileName(string cardId)
        {
            switch (cardId)
            {
                case "buff_vitality": return "icon_vitality";
                case "buff_iron_lungs": return "icon_iron_lungs";
                case "buff_focus": return "icon_focus";
                case "buff_keen_edge": return "icon_keen_edge";
                case "buff_wind_stride": return "icon_wind_stride";
                case "risk_bloodlust": return "icon_bloodlust";
                case "risk_glass_cannon": return "icon_glass_cannon";
                case "risk_colossus_might": return "icon_colossus_might";
                case "risk_adrenal_surge": return "icon_adrenal_surge";
                default:
                    if (cardId.StartsWith("buff_") || cardId.StartsWith("risk_"))
                        return "icon_" + cardId.Substring(5);
                    return "icon_" + cardId;
            }
        }

        private void CreateIconMedallion(Transform parent, UpgradeCardDefinition card, Color themeColor)
        {
            var holder = new GameObject("IconMedallion", typeof(RectTransform), typeof(LayoutElement));
            holder.transform.SetParent(parent, false);

            var le = holder.GetComponent<LayoutElement>();
            le.preferredHeight = 56f;
            le.minHeight = 56f;

            var rt = holder.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(56f, 56f);

            var vignette = new GameObject("Vignette", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            vignette.transform.SetParent(holder.transform, false);
            var vRt = vignette.GetComponent<RectTransform>();
            vRt.anchorMin = new Vector2(0.5f, 0.5f);
            vRt.anchorMax = new Vector2(0.5f, 0.5f);
            vRt.pivot = new Vector2(0.5f, 0.5f);
            vRt.sizeDelta = new Vector2(52f, 52f);
            vRt.anchoredPosition = Vector2.zero;

            var vImg = vignette.GetComponent<Image>();
            vImg.sprite = _whiteSprite;
            vImg.color = new Color(0.04f, 0.05f, 0.07f, 0.90f);

            var vOutline = vignette.GetComponent<Outline>();
            vOutline.effectColor = new Color(themeColor.r, themeColor.g, themeColor.b, 0.65f);
            vOutline.effectDistance = new Vector2(1.5f, 1.5f);

            var sprite = GetIconForCard(card);
            if (sprite != null)
            {
                var iconObj = new GameObject("PixelArtIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObj.transform.SetParent(vignette.transform, false);
                var iRt = iconObj.GetComponent<RectTransform>();
                iRt.anchorMin = Vector2.zero;
                iRt.anchorMax = Vector2.one;
                iRt.offsetMin = new Vector2(4f, 4f);
                iRt.offsetMax = new Vector2(-4f, -4f);

                var img = iconObj.GetComponent<Image>();
                img.sprite = sprite;
                img.preserveAspect = true;
                img.color = Color.white;
            }
            else
            {
                var textObj = new GameObject("FallbackSymbol", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                textObj.transform.SetParent(vignette.transform, false);
                var tRt = textObj.GetComponent<RectTransform>();
                tRt.anchorMin = Vector2.zero;
                tRt.anchorMax = Vector2.one;
                tRt.offsetMin = Vector2.zero;
                tRt.offsetMax = Vector2.zero;

                var txt = textObj.GetComponent<Text>();
                txt.font = _uiFont;
                txt.fontSize = 24;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = Color.white;
                txt.text = !string.IsNullOrEmpty(card.iconSymbol) ? card.iconSymbol : "*";
            }
        }

        private Text CreateEffectText(Transform parent, string name, string text, Color color, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Shadow), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            float h = 34f;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, h);

            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = h;
            le.minHeight = h;

            var shadow = go.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
            shadow.effectDistance = new Vector2(1f, -1f);

            var txt = go.GetComponent<Text>();
            txt.font = _uiFont;
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = text;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Truncate;

            return txt;
        }

        private void CreateCornerIndex(Transform parent, string text, Vector2 anchor, Vector2 anchoredPos, TextAnchor align)
        {
            var go = new GameObject("CornerIndex", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(28f, 32f);

            var txt = go.GetComponent<Text>();
            txt.font = _uiFont;
            txt.fontSize = 11;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = align;
            txt.color = new Color(0.92f, 0.78f, 0.45f, 0.90f);
            txt.text = text;
        }

        private void CreateEffectBox(Transform parent, string prefix, string effectText, Color boxBgColor, Color textColor, float preferredHeight)
        {
            var box = new GameObject("EffectBox", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(LayoutElement));
            box.transform.SetParent(parent, false);

            var le = box.GetComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
            le.minHeight = preferredHeight;

            var img = box.GetComponent<Image>();
            img.sprite = _whiteSprite;
            img.color = boxBgColor;

            var outline = box.GetComponent<Outline>();
            outline.effectColor = new Color(textColor.r, textColor.g, textColor.b, 0.35f);
            outline.effectDistance = new Vector2(1f, 1f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(box.transform, false);
            var tRt = textGo.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = new Vector2(6f, 2f);
            tRt.offsetMax = new Vector2(-6f, -2f);

            var txt = textGo.GetComponent<Text>();
            txt.font = _uiFont;
            txt.fontSize = 10;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = textColor;
            txt.text = prefix + effectText;
        }

                private static bool IsBuffCard(UpgradeCardDefinition card)
        {
            if (card == null) return true;
            if (card.id != null && card.id.StartsWith("risk_")) return false;
            if (!string.IsNullOrEmpty(card.negativeEffectText)) return false;
            if (card.enemyHealthMult > 1.01f || card.enemySpeedMult > 1.01f || card.playerHealthDelta < -0.01f) return false;
            if (card.category == UpgradeCategory.RiskAndReward && (card.id == null || !card.id.StartsWith("buff_"))) return false;
            return true;
        }

                #region Editor Preview Helpers

        [ContextMenu("Превью: Показать окно карточек")]
        public void EditorPreviewShow()
        {
            EnsureAssets();
            EnsureUIBuilt();
            var mgr = FindAnyObjectByType<RoguelikeUpgradeManager>();
            var cards = (mgr != null) ? mgr.GenerateUpgradeOffer(5) : RoguelikeUpgradeManager.GetDefaultCombatBuffs();
            ShowSelection(cards, 2, () => {});
            if (modalRoot != null) modalRoot.SetActive(true);
        }

        [ContextMenu("Превью: Скрыть окно карточек")]
        public void EditorPreviewHide()
        {
            Hide();
        }

        #endregion

        public void Hide()
        {
            if (modalRoot != null)
            {
                modalRoot.SetActive(false);
            }
        }
    }
}
