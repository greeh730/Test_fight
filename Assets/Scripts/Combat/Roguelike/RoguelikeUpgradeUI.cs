using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.Roguelike
{
    /// <summary>
    /// Интерфейс выбора 3 карточек Roguelike-улучшений при прохождении уровня (Кристалл Победы).
    /// Карточки имеют фиксированный, компактный размер и центрируются на экране,
    /// идеально помещаясь при любом разрешении без вылезания за границы.
    /// </summary>
    [DisallowMultipleComponent]
    public class RoguelikeUpgradeUI : MonoBehaviour
    {
        public static RoguelikeUpgradeUI Instance { get; private set; }

        [Header("--- Visual Colors ---")]
        [SerializeField] private Color tacticianColor = new Color(0f, 0.95f, 1f, 1f);      // Cyan
        [SerializeField] private Color combatBuffColor = new Color(0.2f, 1f, 0.55f, 1f);   // Emerald / Green
        [SerializeField] private Color riskColor = new Color(1f, 0.25f, 0.35f, 1f);        // Crimson Red
        [SerializeField] private Color panelBgColor = new Color(0.03f, 0.04f, 0.06f, 0.90f);

        [Header("--- Layout Dimensions ---")]
        [Tooltip("Ширина каждой отдельной карточки")]
        [SerializeField] private float cardWidth = 310f;
        [Tooltip("Высота каждой отдельной карточки")]
        [SerializeField] private float cardHeight = 450f;
        [Tooltip("Отступ между карточками")]
        [SerializeField] private float cardSpacing = 24f;

        [Header("--- UI Container Elements ---")]
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private GameObject modalRoot;
        [SerializeField] private Transform cardsContainer;

        private Action _onSelectionFinished;
        private Font _uiFont;
        private Sprite _whiteSprite;
        private readonly List<GameObject> _cardViews = new List<GameObject>();

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

            // Если модалка уже есть, удаляем старую чтобы гарантировать актуальные размеры
            var existing = targetCanvas.transform.Find("RoguelikeUpgradeModal");
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
                modalRoot = null;
                cardsContainer = null;
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

            // 2. Центральная панель контента (фиксированная ширина и высота для предотвращения расползания)
            var centerPanelObj = new GameObject("CenterDialog", typeof(RectTransform));
            centerPanelObj.transform.SetParent(modalRoot.transform, false);
            var panelRt = centerPanelObj.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(1040f, 620f);
            panelRt.anchoredPosition = Vector2.zero;

            // 3. Заголовок
            var headerObj = new GameObject("HeaderTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            headerObj.transform.SetParent(centerPanelObj.transform, false);
            var headerRt = headerObj.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0.5f, 1.0f);
            headerRt.anchorMax = new Vector2(0.5f, 1.0f);
            headerRt.pivot = new Vector2(0.5f, 1.0f);
            headerRt.anchoredPosition = new Vector2(0f, -10f);
            headerRt.sizeDelta = new Vector2(800f, 44f);

            var headerText = headerObj.GetComponent<Text>();
            headerText.font = _uiFont;
            headerText.fontSize = 28;
            headerText.fontStyle = FontStyle.Bold;
            headerText.alignment = TextAnchor.MiddleCenter;
            headerText.color = new Color(0.95f, 0.98f, 1f, 1f);
            headerText.text = "РЕЗОНАНС КРИСТАЛЛА ПОБЕДЫ";

            // 4. Подзаголовок
            var subObj = new GameObject("Subtitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            subObj.transform.SetParent(centerPanelObj.transform, false);
            var subRt = subObj.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0.5f, 1.0f);
            subRt.anchorMax = new Vector2(0.5f, 1.0f);
            subRt.pivot = new Vector2(0.5f, 1.0f);
            subRt.anchoredPosition = new Vector2(0f, -54f);
            subRt.sizeDelta = new Vector2(800f, 26f);

            var subText = subObj.GetComponent<Text>();
            subText.font = _uiFont;
            subText.fontSize = 14;
            subText.alignment = TextAnchor.MiddleCenter;
            subText.color = new Color(0.68f, 0.78f, 0.88f, 0.95f);
            subText.text = "Выберите одно улучшение для усиления вашей боевой мощи в новом цикле:";

            // 5. Контейнер 3 карточек (компактный, выровнен по центру)
            float totalWidth = 3f * cardWidth + 2f * cardSpacing;
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

        public void ShowSelection(List<UpgradeCardDefinition> cards, Action onComplete)
        {
            EnsureAssets();
            EnsureUIBuilt();

            _onSelectionFinished = onComplete;

            // Очищаем старые карточки
            for (int i = cardsContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(cardsContainer.GetChild(i).gameObject);
            }
            _cardViews.Clear();

            // Создаем 3 карточки
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                var cardView = CreateCardView(card, i);
                _cardViews.Add(cardView);
            }

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

        private GameObject CreateCardView(UpgradeCardDefinition card, int index)
        {
            Color themeColor;
            string categoryLabel;

            switch (card.category)
            {
                case UpgradeCategory.TacticianAbility:
                    themeColor = tacticianColor;
                    categoryLabel = "[СПОСОБНОСТЬ ТАКТИКА]";
                    break;
                case UpgradeCategory.CombatBuff:
                    themeColor = combatBuffColor;
                    categoryLabel = "[БОЕВОЙ БАФФ]";
                    break;
                case UpgradeCategory.RiskAndReward:
                    themeColor = riskColor;
                    categoryLabel = "[РИСК И НАГРАДА]";
                    break;
                default:
                    themeColor = Color.white;
                    categoryLabel = "[УЛУЧШЕНИЕ]";
                    break;
            }

            // 1. Корневой объект карточки (фиксированный размер внутри HorizontalLayoutGroup)
            var cardObj = new GameObject($"Card_{index}_{card.id}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(LayoutElement));
            cardObj.transform.SetParent(cardsContainer, false);

            var le = cardObj.GetComponent<LayoutElement>();
            le.preferredWidth = cardWidth;
            le.preferredHeight = cardHeight;
            le.minWidth = cardWidth;
            le.minHeight = cardHeight;

            var cardImg = cardObj.GetComponent<Image>();
            cardImg.sprite = _whiteSprite;
            cardImg.color = new Color(0.06f, 0.08f, 0.12f, 0.95f);

            var outline = cardObj.GetComponent<Outline>();
            outline.effectColor = themeColor;
            outline.effectDistance = new Vector2(2f, 2f);

            // 2. Внутренний контейнер контента (занимает верхнюю часть карточки, оставляя снизу место под кнопку)
            var contentContainer = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
            contentContainer.transform.SetParent(cardObj.transform, false);
            var contentRt = contentContainer.GetComponent<RectTransform>();
            contentRt.anchorMin = Vector2.zero;
            contentRt.anchorMax = Vector2.one;
            // Снизу оставляем 64px для кнопки "ВЫБРАТЬ"
            contentRt.offsetMin = new Vector2(16f, 62f);
            contentRt.offsetMax = new Vector2(-16f, -14f);

            var layout = contentContainer.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // А. Бейдж категории
            CreateTextElement(contentContainer.transform, "CategoryBadge", categoryLabel, 11, FontStyle.Bold, themeColor, TextAnchor.MiddleCenter, 20f);

            // Б. Иконка-символ
            CreateTextElement(contentContainer.transform, "Icon", card.iconSymbol, 36, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, 42f);

            // В. Заголовок
            CreateTextElement(contentContainer.transform, "Title", card.title, 17, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, 26f);

            // Г. Описание
            CreateTextElement(contentContainer.transform, "Desc", card.description, 12, FontStyle.Normal, new Color(0.82f, 0.86f, 0.90f, 0.92f), TextAnchor.MiddleCenter, 52f);

            // Д. Положительный эффект (зеленый / бирюзовый)
            if (!string.IsNullOrEmpty(card.positiveEffectText))
            {
                CreateTextElement(contentContainer.transform, "PositiveEffect", "✓ " + card.positiveEffectText, 12, FontStyle.Bold, new Color(0.25f, 1f, 0.55f, 1f), TextAnchor.MiddleCenter, 38f);
            }

            // Е. Негативный эффект (красный / оранжевый)
            if (!string.IsNullOrEmpty(card.negativeEffectText))
            {
                CreateTextElement(contentContainer.transform, "NegativeEffect", "⚠ " + card.negativeEffectText, 12, FontStyle.Bold, new Color(1f, 0.35f, 0.35f, 1f), TextAnchor.MiddleCenter, 34f);
            }

            // 3. Кнопка "ВЫБРАТЬ" (ЖЕСТКО закреплена в самом низу карточки)
            var btnObj = new GameObject("SelectButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(cardObj.transform, false);

            var btnRt = btnObj.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.08f, 0f);
            btnRt.anchorMax = new Vector2(0.92f, 0f);
            btnRt.pivot = new Vector2(0.5f, 0f);
            btnRt.anchoredPosition = new Vector2(0f, 12f);
            btnRt.sizeDelta = new Vector2(0f, 38f);

            var btnImg = btnObj.GetComponent<Image>();
            btnImg.sprite = _whiteSprite;
            btnImg.color = themeColor * 0.82f;

            var btn = btnObj.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = themeColor * 0.82f;
            colors.highlightedColor = themeColor;
            colors.pressedColor = Color.white;
            btn.colors = colors;

            // Текст внутри кнопки
            var btnTextObj = new GameObject("BtnText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            btnTextObj.transform.SetParent(btnObj.transform, false);
            var bRt = btnTextObj.GetComponent<RectTransform>();
            bRt.anchorMin = Vector2.zero;
            bRt.anchorMax = Vector2.one;
            bRt.offsetMin = Vector2.zero;
            bRt.offsetMax = Vector2.zero;

            var bText = btnTextObj.GetComponent<Text>();
            bText.font = _uiFont;
            bText.fontSize = 14;
            bText.fontStyle = FontStyle.Bold;
            bText.alignment = TextAnchor.MiddleCenter;
            bText.color = Color.black;
            bText.text = "ВЫБРАТЬ";

            // Клик по кнопке
            btn.onClick.AddListener(() => OnCardClicked(card));

            return cardObj;
        }

        private Text CreateTextElement(Transform parent, string name, string text, int fontSize, FontStyle style, Color color, TextAnchor alignment, float preferredHeight)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

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

            return txt;
        }

        private void OnCardClicked(UpgradeCardDefinition card)
        {
            // Применяем улучшение через менеджер
            if (RoguelikeUpgradeManager.Instance != null)
            {
                RoguelikeUpgradeManager.Instance.ApplyUpgrade(card);
            }

            // Закрываем окно
            Hide();

            // Возобновляем время
            Time.timeScale = 1.0f;

            // Запускаем перегенерацию уровня
            _onSelectionFinished?.Invoke();
            _onSelectionFinished = null;
        }

        public void Hide()
        {
            if (modalRoot != null)
            {
                modalRoot.SetActive(false);
            }
        }
    }
}
