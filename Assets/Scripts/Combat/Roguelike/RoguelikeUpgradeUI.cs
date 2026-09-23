using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Combat.Roguelike
{
    /// <summary>
    /// Интерфейс выбора 3 карточек Roguelike-улучшений при прохождении уровня (Кристалл Победы).
    /// Полностью автономен: при отсутствии настроенного в инспекторе префаба автоматически
    /// генерирует стильный неоновый интерфейс на существующем Canvas.
    /// </summary>
    [DisallowMultipleComponent]
    public class RoguelikeUpgradeUI : MonoBehaviour
    {
        public static RoguelikeUpgradeUI Instance { get; private set; }

        [Header("--- Visual Colors ---")]
        [SerializeField] private Color tacticianColor = new Color(0f, 0.95f, 1f, 1f);      // Cyan
        [SerializeField] private Color combatBuffColor = new Color(0.2f, 1f, 0.55f, 1f);   // Emerald / Gold
        [SerializeField] private Color riskColor = new Color(1f, 0.25f, 0.35f, 1f);        // Crimson Red
        [SerializeField] private Color panelBgColor = new Color(0.04f, 0.05f, 0.08f, 0.88f);

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

            if (modalRoot == null)
            {
                var existing = targetCanvas.transform.Find("RoguelikeUpgradeModal");
                if (existing != null)
                {
                    modalRoot = existing.gameObject;
                    cardsContainer = modalRoot.transform.Find("CardsContainer");
                }
                else
                {
                    BuildUIHierarchy();
                }
            }

            if (modalRoot != null)
            {
                modalRoot.SetActive(false);
            }
        }

        private void BuildUIHierarchy()
        {
            // Корневой оверлей
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

            // Заголовок
            var headerObj = new GameObject("HeaderTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            headerObj.transform.SetParent(modalRoot.transform, false);
            var headerRt = headerObj.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0.1f, 0.82f);
            headerRt.anchorMax = new Vector2(0.9f, 0.94f);
            headerRt.offsetMin = Vector2.zero;
            headerRt.offsetMax = Vector2.zero;

            var headerText = headerObj.GetComponent<Text>();
            headerText.font = _uiFont;
            headerText.fontSize = 32;
            headerText.fontStyle = FontStyle.Bold;
            headerText.alignment = TextAnchor.MiddleCenter;
            headerText.color = new Color(0.9f, 0.95f, 1f, 1f);
            headerText.text = "РЕЗОНАНС КРИСТАЛЛА ПОБЕДЫ";

            // Подзаголовок
            var subObj = new GameObject("Subtitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            subObj.transform.SetParent(modalRoot.transform, false);
            var subRt = subObj.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0.1f, 0.77f);
            subRt.anchorMax = new Vector2(0.9f, 0.83f);
            subRt.offsetMin = Vector2.zero;
            subRt.offsetMax = Vector2.zero;

            var subText = subObj.GetComponent<Text>();
            subText.font = _uiFont;
            subText.fontSize = 17;
            subText.alignment = TextAnchor.MiddleCenter;
            subText.color = new Color(0.7f, 0.8f, 0.9f, 0.9f);
            subText.text = "Выберите одно улучшение для усиления вашей боевой мощи в новом цикле:";

            // Контейнер 3 карточек
            var containerObj = new GameObject("CardsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            containerObj.transform.SetParent(modalRoot.transform, false);
            var cRt = containerObj.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0.08f, 0.12f);
            cRt.anchorMax = new Vector2(0.92f, 0.75f);
            cRt.offsetMin = Vector2.zero;
            cRt.offsetMax = Vector2.zero;

            var hlg = containerObj.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 30f;
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

            // Ставим игру на паузу
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

            // Card Panel Root
            var cardObj = new GameObject($"Card_{index}_{card.id}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            cardObj.transform.SetParent(cardsContainer, false);

            var cardImg = cardObj.GetComponent<Image>();
            cardImg.sprite = _whiteSprite;
            cardImg.color = new Color(0.08f, 0.10f, 0.15f, 0.95f);

            var outline = cardObj.GetComponent<Outline>();
            outline.effectColor = themeColor;
            outline.effectDistance = new Vector2(3f, 3f);

            // Вертикальный контейнер для содержимого карточки
            var layout = cardObj.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 24, 20);
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // 1. Бейдж категории
            CreateTextElement(cardObj.transform, "CategoryBadge", categoryLabel, 13, FontStyle.Bold, themeColor, TextAnchor.MiddleCenter, 24f);

            // 2. Иконка-символ
            CreateTextElement(cardObj.transform, "Icon", card.iconSymbol, 44, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, 54f);

            // 3. Заголовок карточки
            CreateTextElement(cardObj.transform, "Title", card.title, 20, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, 34f);

            // 4. Описание
            CreateTextElement(cardObj.transform, "Desc", card.description, 13, FontStyle.Normal, new Color(0.85f, 0.88f, 0.92f, 0.95f), TextAnchor.MiddleCenter, 58f);

            // 5. Положительный эффект
            if (!string.IsNullOrEmpty(card.positiveEffectText))
            {
                CreateTextElement(cardObj.transform, "PositiveEffect", "✓ " + card.positiveEffectText, 14, FontStyle.Bold, new Color(0.3f, 1f, 0.6f, 1f), TextAnchor.MiddleCenter, 42f);
            }

            // 6. Негативный эффект (если перк с риском)
            if (!string.IsNullOrEmpty(card.negativeEffectText))
            {
                CreateTextElement(cardObj.transform, "NegativeEffect", "⚠ " + card.negativeEffectText, 13, FontStyle.Bold, new Color(1f, 0.35f, 0.35f, 1f), TextAnchor.MiddleCenter, 36f);
            }

            // Распорка (Spacer)
            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(cardObj.transform, false);
            var le = spacer.GetComponent<LayoutElement>();
            le.flexibleHeight = 1f;

            // 7. Кнопка "ВЫБРАТЬ"
            var btnObj = new GameObject("SelectButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            btnObj.transform.SetParent(cardObj.transform, false);
            var btnLe = btnObj.GetComponent<LayoutElement>();
            btnLe.preferredHeight = 44f;

            var btnImg = btnObj.GetComponent<Image>();
            btnImg.sprite = _whiteSprite;
            btnImg.color = themeColor * 0.75f;

            var btn = btnObj.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = themeColor * 0.75f;
            colors.highlightedColor = themeColor;
            colors.pressedColor = Color.white;
            btn.colors = colors;

            // Текст кнопки
            var btnTextObj = new GameObject("BtnText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            btnTextObj.transform.SetParent(btnObj.transform, false);
            var bRt = btnTextObj.GetComponent<RectTransform>();
            bRt.anchorMin = Vector2.zero;
            bRt.anchorMax = Vector2.one;
            bRt.offsetMin = Vector2.zero;
            bRt.offsetMax = Vector2.zero;

            var bText = btnTextObj.GetComponent<Text>();
            bText.font = _uiFont;
            bText.fontSize = 15;
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
