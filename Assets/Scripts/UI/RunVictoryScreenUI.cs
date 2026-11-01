using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Combat.UI
{
    /// <summary>
    /// Экран триумфального завершения забега (после зачистки всех 13 арен).
    /// Останавливает игру, отображает торжественный визуал и предлагает
    /// начать новый забег либо вернуться в главное меню.
    /// </summary>
    [DisallowMultipleComponent]
    public class RunVictoryScreenUI : MonoBehaviour
    {
        public static RunVictoryScreenUI Instance { get; private set; }

        [Header("--- UI References ---")]
        [SerializeField] private GameObject screenRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text titleText;
        [SerializeField] private Text subtitleText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;

        private bool _isShowing = false;
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

            EnsureUIBuilt();
            if (screenRoot != null)
            {
                screenRoot.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (!_isShowing) return;

            // Горячие клавиши на экране победы
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                RestartNewRun();
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                ReturnToMainMenu();
            }
        }

        public static void ShowVictoryScreen()
        {
            if (Instance == null)
            {
                var existing = FindAnyObjectByType<RunVictoryScreenUI>();
                if (existing != null)
                {
                    Instance = existing;
                }
                else
                {
                    var go = new GameObject("[Run_VictoryScreen_UI]");
                    Instance = go.AddComponent<RunVictoryScreenUI>();
                }
            }

            Instance.EnsureUIBuilt();
            Instance.TriggerVictory();
        }

        private void TriggerVictory()
        {
            if (_isShowing) return;
            _isShowing = true;

            Time.timeScale = 0f;

            if (screenRoot != null)
            {
                screenRoot.SetActive(true);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            Debug.Log("<color=#FFD700><b>[RUN VICTORY!]</b></color> 🏆 Забег успешно завершён! Все 13 арен пройдены!");
        }

        public static void RestartNewRun()
        {
            Time.timeScale = 1.0f;
            Debug.Log("<color=#00FFAA>[RunVictoryScreen]</color> Запуск нового забега с 1-й арены...");
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public static void ReturnToMainMenu()
        {
            Time.timeScale = 1.0f;
            Debug.Log("<color=#4EE2EC>[RunVictoryScreen]</color> Возврат в главное меню...");
            SceneManager.LoadScene("MainMenu");
        }

        public void EnsureUIBuilt()
        {
            if (screenRoot != null) return;

            var canvas = GetComponentInParent<Canvas>() ?? FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            transform.SetParent(canvas.transform, false);

            var rootObj = new GameObject("VictoryScreenPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            rootObj.transform.SetParent(transform, false);
            screenRoot = rootObj;

            var rootRt = rootObj.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var bgImg = rootObj.GetComponent<Image>();
            bgImg.sprite = GetWhiteSprite();
            bgImg.color = new Color(0.03f, 0.05f, 0.09f, 0.94f);

            canvasGroup = rootObj.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            // Центральная плашка
            var cardObj = new GameObject("VictoryCard", typeof(RectTransform), typeof(Image), typeof(Outline));
            cardObj.transform.SetParent(rootObj.transform, false);
            var cardRt = cardObj.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
            cardRt.sizeDelta = new Vector2(540f, 320f);

            var cardImg = cardObj.GetComponent<Image>();
            cardImg.sprite = GetWhiteSprite();
            cardImg.color = new Color(0.07f, 0.10f, 0.18f, 0.98f);

            var cardOutline = cardObj.GetComponent<Outline>();
            cardOutline.effectColor = new Color(1.0f, 0.82f, 0.2f, 0.9f);
            cardOutline.effectDistance = new Vector2(2f, 2f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Заголовок ПОБЕДА В ЗАБЕГЕ!
            var titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(Text), typeof(Outline));
            titleGo.transform.SetParent(cardObj.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -28f);
            titleRt.sizeDelta = new Vector2(500f, 50f);

            titleText = titleGo.GetComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 28;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.text = "🏆 ПОБЕДА В ЗАБЕГЕ! 🏆";
            titleText.color = new Color(1.0f, 0.88f, 0.25f, 1.0f);

            var tOutline = titleGo.GetComponent<Outline>();
            tOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            tOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Подзаголовок
            var subGo = new GameObject("SubtitleText", typeof(RectTransform), typeof(Text));
            subGo.transform.SetParent(cardObj.transform, false);
            var subRt = subGo.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0f, 1f);
            subRt.anchorMax = new Vector2(1f, 1f);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.anchoredPosition = new Vector2(0f, -90f);
            subRt.sizeDelta = new Vector2(480f, 60f);

            subtitleText = subGo.GetComponent<Text>();
            subtitleText.font = font;
            subtitleText.fontSize = 16;
            subtitleText.alignment = TextAnchor.MiddleCenter;
            subtitleText.text = "Все 13 арен пройдены!\nБездна Эмоций успешно покорена героем.";
            subtitleText.color = new Color(0.85f, 0.92f, 1.0f, 0.95f);

            // Кнопка 1: Начать новый забег
            restartButton = CreateButton(cardObj.transform, "RestartButton", "НАЧАТЬ НОВЫЙ ЗАБЕГ [SPACE]", new Vector2(0f, -185f), new Vector2(340f, 44f),
                new Color(0.12f, 0.65f, 0.45f, 1.0f), font, RestartNewRun);

            // Кнопка 2: В главное меню
            mainMenuButton = CreateButton(cardObj.transform, "MainMenuButton", "В ГЛАВНОЕ МЕНЮ [ESC]", new Vector2(0f, -245f), new Vector2(340f, 40f),
                new Color(0.25f, 0.30f, 0.42f, 1.0f), font, ReturnToMainMenu);
        }

        private Button CreateButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color bgColor, Font font, UnityEngine.Events.UnityAction action)
        {
            var btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            btnGo.transform.SetParent(parent, false);

            var rt = btnGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var img = btnGo.GetComponent<Image>();
            img.sprite = GetWhiteSprite();
            img.color = bgColor;

            var outl = btnGo.GetComponent<Outline>();
            outl.effectColor = new Color(0f, 0f, 0f, 0.6f);
            outl.effectDistance = new Vector2(1f, -1f);

            var btn = btnGo.GetComponent<Button>();
            btn.onClick.AddListener(action);

            var colors = btn.colors;
            colors.highlightedColor = bgColor * 1.25f;
            colors.pressedColor = bgColor * 0.85f;
            btn.colors = colors;

            var txtGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            txtGo.transform.SetParent(btnGo.transform, false);
            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;

            var txt = txtGo.GetComponent<Text>();
            txt.font = font;
            txt.fontSize = 14;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = label;
            txt.color = Color.white;

            return btn;
        }
    }
}
