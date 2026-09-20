using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Combat.Save;

namespace Combat.UI
{
    /// <summary>
    /// Контроллер меню паузы (Pause Menu).
    /// Останавливает игровой мир (Time.timeScale = 0), блокирует игровой ввод
    /// и отображает интерактивное меню с кнопками.
    /// </summary>
    [DisallowMultipleComponent]
    public class PauseMenuController : MonoBehaviour
    {
        public static PauseMenuController Instance { get; private set; }
        public static bool IsGamePaused => Instance != null && Instance.IsPaused;

        public static event Action<bool> OnPauseToggled;

        [Header("--- Hotkeys ---")]
        [Tooltip("Основная клавиша вызова паузы")]
        [SerializeField] private KeyCode pauseKey = KeyCode.Tab;

        [Tooltip("Дополнительная клавиша вызова паузы (по умолчанию None, можно включить Escape)")]
        [SerializeField] private KeyCode alternativeKey = KeyCode.None;

        [Header("--- UI References ---")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private CanvasGroup panelCanvasGroup;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button quickSaveButton;
        [SerializeField] private Button quickLoadButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Text feedbackText;
        [SerializeField] private CombatSettingsUI settingsUI;

        [Header("--- Settings ---")]
        [SerializeField] private float fadeDuration = 0.15f;

        public bool IsPaused { get; private set; } = false;

        private float _previousTimeScale = 1.0f;
        private Coroutine _fadeRoutine;
        private Coroutine _feedbackRoutine;

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

            SetupButtons();
        }

        private void OnEnable()
        {
            CombatSettingsUI.OnSettingsClosed += HandleSettingsClosed;
        }

        private void OnDisable()
        {
            CombatSettingsUI.OnSettingsClosed -= HandleSettingsClosed;
        }

        private void HandleSettingsClosed()
        {
            if (IsPaused)
            {
                SetPanelVisible(true, instant: false);
            }
        }

        private void Start()
        {
            if (settingsUI == null)
            {
                settingsUI = FindAnyObjectByType<CombatSettingsUI>();
            }

            // По умолчанию игра не на паузе, панель скрыта
            SetPanelVisible(false, instant: true);
        }

        private void SetupButtons()
        {
            if (resumeButton != null)
            {
                resumeButton.onClick.AddListener(ResumeGame);
            }

            if (quickSaveButton != null)
            {
                quickSaveButton.onClick.AddListener(OnQuickSaveClicked);
            }

            if (quickLoadButton != null)
            {
                quickLoadButton.onClick.AddListener(OnQuickLoadClicked);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(OnSettingsClicked);
            }
        }

        private void Update()
        {
            if (IsPauseKeyPressed())
            {
                // Если сейчас открыто меню настроек, то клавиша паузы закрывает настройки
                if (settingsUI != null && settingsUI.IsOpen)
                {
                    settingsUI.CloseSettings();
                    SetPanelVisible(true, instant: false);
                    return;
                }

                TogglePause();
            }
        }

        private bool IsPauseKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (pauseKey == KeyCode.Tab && kb.tabKey.wasPressedThisFrame) return true;
                if (pauseKey == KeyCode.Escape && kb.escapeKey.wasPressedThisFrame) return true;
                if (alternativeKey == KeyCode.Escape && kb.escapeKey.wasPressedThisFrame) return true;
                if (alternativeKey == KeyCode.Tab && kb.tabKey.wasPressedThisFrame) return true;
            }
#endif
            try
            {
                if (pauseKey != KeyCode.None && Input.GetKeyDown(pauseKey)) return true;
                if (alternativeKey != KeyCode.None && Input.GetKeyDown(alternativeKey)) return true;
            }
            catch
            {
                // Fallback для нестандартных платформ
            }

            return false;
        }

        public void TogglePause()
        {
            if (IsPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }

        public void PauseGame()
        {
            if (IsPaused) return;

            IsPaused = true;
            _previousTimeScale = Time.timeScale > 0.001f ? Time.timeScale : 1.0f;
            Time.timeScale = 0f;

            SetPanelVisible(true, instant: false);
            ClearFeedback();

            OnPauseToggled?.Invoke(true);
            Debug.Log("<color=#4EE2EC>[PAUSE]</color> Игра поставлена на паузу (Time.timeScale = 0).");
        }

        public void ResumeGame()
        {
            if (!IsPaused) return;

            // Если были открыты настройки, закрываем их
            if (settingsUI != null && settingsUI.IsOpen)
            {
                settingsUI.CloseSettings();
            }

            IsPaused = false;
            Time.timeScale = _previousTimeScale > 0.001f ? _previousTimeScale : 1.0f;

            SetPanelVisible(false, instant: false);

            OnPauseToggled?.Invoke(false);
            Debug.Log($"<color=#4EE2EC>[PAUSE]</color> Игра возобновлена (Time.timeScale = {Time.timeScale}).");
        }

        private void OnQuickSaveClicked()
        {
            var saveMgr = CombatSaveManager.Instance ?? FindAnyObjectByType<CombatSaveManager>();
            if (saveMgr == null)
            {
                var go = new GameObject("[CombatSaveManager]");
                saveMgr = go.AddComponent<CombatSaveManager>();
            }

            if (saveMgr.SaveGame())
            {
                ShowFeedback("Быстрое сохранение записано!");
            }
            else
            {
                ShowFeedback("Ошибка сохранения!");
            }
        }

        private void OnQuickLoadClicked()
        {
            var saveMgr = CombatSaveManager.Instance ?? FindAnyObjectByType<CombatSaveManager>();
            if (saveMgr == null || !saveMgr.HasSave())
            {
                ShowFeedback("Нет доступного сохранения!");
                return;
            }

            if (saveMgr.LoadGame())
            {
                ShowFeedback("Игра успешно загружена!");
            }
            else
            {
                ShowFeedback("Ошибка загрузки!");
            }
        }

        private void OnSettingsClicked()
        {
            if (settingsUI != null)
            {
                // Временно скрываем окно паузы, пока открыты настройки
                SetPanelVisible(false, instant: true);
                settingsUI.OpenSettings();
            }
            else
            {
                ShowFeedback("Меню настроек недоступно");
            }
        }

        private void SetPanelVisible(bool visible, bool instant)
        {
            if (pausePanel != null)
            {
                pausePanel.SetActive(visible);
            }

            if (panelCanvasGroup != null)
            {
                if (instant || fadeDuration <= 0.01f)
                {
                    panelCanvasGroup.alpha = visible ? 1f : 0f;
                    panelCanvasGroup.interactable = visible;
                    panelCanvasGroup.blocksRaycasts = visible;
                }
                else
                {
                    if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
                    _fadeRoutine = StartCoroutine(FadeRoutine(visible));
                }
            }
        }

        private System.Collections.IEnumerator FadeRoutine(bool visible)
        {
            panelCanvasGroup.blocksRaycasts = visible;
            panelCanvasGroup.interactable = visible;

            float targetAlpha = visible ? 1f : 0f;
            float startAlpha = panelCanvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
                yield return null;
            }

            panelCanvasGroup.alpha = targetAlpha;
            _fadeRoutine = null;
        }

        private void ShowFeedback(string message)
        {
            if (feedbackText == null) return;

            feedbackText.text = message;
            if (_feedbackRoutine != null) StopCoroutine(_feedbackRoutine);
            _feedbackRoutine = StartCoroutine(FeedbackRoutine());
        }

        private System.Collections.IEnumerator FeedbackRoutine()
        {
            if (feedbackText == null) yield break;
            Color c = feedbackText.color;
            c.a = 1f;
            feedbackText.color = c;

            yield return new WaitForSecondsRealtime(2.0f);

            float elapsed = 0f;
            while (elapsed < 0.5f)
            {
                elapsed += Time.unscaledDeltaTime;
                c.a = Mathf.Lerp(1f, 0f, elapsed / 0.5f);
                feedbackText.color = c;
                yield return null;
            }

            c.a = 0f;
            feedbackText.color = c;
            feedbackText.text = string.Empty;
            _feedbackRoutine = null;
        }

        private void ClearFeedback()
        {
            if (feedbackText != null)
            {
                feedbackText.text = string.Empty;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
