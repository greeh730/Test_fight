using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Combat.UI
{
    /// <summary>
    /// Контроллер главного меню игры (Main Menu).
    /// Управляет запуском игры, окном подсказок управления и выходом из приложения.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("--- Scene Loading ---")]
        [Tooltip("Имя сцены игрового процесса для загрузки")]
        [SerializeField] private string gameSceneName = "SampleScene";

        [Tooltip("Имя сцены интерактивного обучения")]
        [SerializeField] private string tutorialSceneName = "TutorialScene";

        [Header("--- Tutorial Settings ---")]
        [Tooltip("Чекбокс отключения обучения")]
        [SerializeField] private Toggle disableTutorialToggle;

        [Header("--- UI References ---")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button controlsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private GameObject controlsPanel;
        [SerializeField] private Button closeControlsButton;
        [SerializeField] private CanvasGroup fadeOverlay;

        [Header("--- Tabs in Controls Modal ---")]
        [SerializeField] private Button movesTabButton;
        [SerializeField] private Button basicsTabButton;
        [SerializeField] private GameObject movesTabContent;
        [SerializeField] private GameObject basicsTabContent;

        [Header("--- Settings ---")]
        [SerializeField] private float transitionDuration = 0.35f;

        private bool _isTransitioning = false;

        private void Awake()
        {
            Time.timeScale = 1.0f;

            if (startButton != null)
            {
                startButton.onClick.AddListener(StartGame);
            }

            if (controlsButton != null)
            {
                controlsButton.onClick.AddListener(OpenControls);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(QuitGame);
            }

            if (closeControlsButton != null)
            {
                closeControlsButton.onClick.AddListener(CloseControls);
            }

            if (movesTabButton != null)
            {
                movesTabButton.onClick.AddListener(ShowMovesTab);
            }

            if (basicsTabButton != null)
            {
                basicsTabButton.onClick.AddListener(ShowBasicsTab);
            }

            if (disableTutorialToggle != null)
            {
                bool isTutorialDisabled = PlayerPrefs.GetInt("DisableTutorial", 0) == 1;
                disableTutorialToggle.isOn = isTutorialDisabled;
                disableTutorialToggle.onValueChanged.AddListener(OnDisableTutorialToggleChanged);
            }

            if (controlsPanel != null)
            {
                controlsPanel.SetActive(false);
            }
        }

        private void OnDisableTutorialToggleChanged(bool isDisabled)
        {
            PlayerPrefs.SetInt("DisableTutorial", isDisabled ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log($"<color=#4EE2EC>[MainMenuController] Отключение обучения: {isDisabled} (сохранено в PlayerPrefs)</color>");
        }

        private void Start()
        {
            if (fadeOverlay != null)
            {
                StartCoroutine(FadeFromBlack());
            }
        }

        private void Update()
        {
            // Закрытие окна управления по Escape
            if (controlsPanel != null && controlsPanel.activeSelf && IsEscapePressed())
            {
                CloseControls();
            }
        }

        private static bool IsEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) return true;
#endif
            try { return Input.GetKeyDown(KeyCode.Escape); } catch { return false; }
        }

        public void StartGame()
        {
            if (_isTransitioning) return;
            StartCoroutine(StartGameRoutine());
        }

        private IEnumerator StartGameRoutine()
        {
            _isTransitioning = true;

            if (fadeOverlay != null)
            {
                fadeOverlay.blocksRaycasts = true;
                float elapsed = 0f;
                while (elapsed < transitionDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    fadeOverlay.alpha = Mathf.Clamp01(elapsed / transitionDuration);
                    yield return null;
                }
                fadeOverlay.alpha = 1f;
            }

            bool isTutorialDisabled = disableTutorialToggle != null
                ? disableTutorialToggle.isOn
                : (PlayerPrefs.GetInt("DisableTutorial", 0) == 1);

            string targetScene = isTutorialDisabled ? gameSceneName : tutorialSceneName;
            Debug.Log($"<color=#4EE2EC>[MainMenuController] Запуск игры: цель = '{targetScene}' (Обучение отключено: {isTutorialDisabled})</color>");

            if (!string.IsNullOrEmpty(targetScene))
            {
                SceneManager.LoadScene(targetScene);
            }
            else
            {
                SceneManager.LoadScene(1);
            }
        }

        private IEnumerator FadeFromBlack()
        {
            fadeOverlay.blocksRaycasts = true;
            fadeOverlay.alpha = 1f;
            float elapsed = 0f;

            while (elapsed < transitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                fadeOverlay.alpha = 1f - Mathf.Clamp01(elapsed / transitionDuration);
                yield return null;
            }

            fadeOverlay.alpha = 0f;
            fadeOverlay.blocksRaycasts = false;
        }

        public void OpenControls()
        {
            if (controlsPanel != null)
            {
                controlsPanel.SetActive(true);
                ShowMovesTab();
            }
        }

        public void ShowMovesTab()
        {
            if (movesTabContent != null) movesTabContent.SetActive(true);
            if (basicsTabContent != null) basicsTabContent.SetActive(false);
            UpdateTabVisuals(movesTabButton, basicsTabButton);
        }

        public void ShowBasicsTab()
        {
            if (movesTabContent != null) movesTabContent.SetActive(false);
            if (basicsTabContent != null) basicsTabContent.SetActive(true);
            UpdateTabVisuals(basicsTabButton, movesTabButton);
        }

        private void UpdateTabVisuals(Button activeTab, Button inactiveTab)
        {
            if (activeTab != null)
            {
                var img = activeTab.GetComponent<Image>();
                if (img != null) img.color = new Color(0.85f, 0.15f, 0.25f, 1f); // Neon crimson
            }
            if (inactiveTab != null)
            {
                var img = inactiveTab.GetComponent<Image>();
                if (img != null) img.color = new Color(0.12f, 0.16f, 0.22f, 1f); // Dark slate
            }
        }

        public void CloseControls()
        {
            if (controlsPanel != null)
            {
                controlsPanel.SetActive(false);
            }
        }

        public void QuitGame()
        {
            Debug.Log("[MainMenu] Выход из игры...");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
