using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.Tutorial
{
    /// <summary>
    /// UI-виджет текущего задания и подсказки в правом верхнем углу экрана.
    /// По ТЗ: "сверху справа появляется чёрное окно UI в котором белом написано:... И снизу написано: Удар по манекену 0/3".
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialObjectiveHUD : MonoBehaviour
    {
        private static TutorialObjectiveHUD instance;
        public static TutorialObjectiveHUD Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<TutorialObjectiveHUD>(FindObjectsInactive.Include);
                }
                return instance;
            }
            private set => instance = value;
        }

        [Header("--- UI References ---")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Outline borderOutline;
        [SerializeField] private Text headerText;
        [SerializeField] private Text instructionText;
        [SerializeField] private Text progressText;

        [Header("--- Animations ---")]
        [SerializeField] private float fadeDuration = 0.25f;
        [SerializeField] private float pulseScaleAmount = 1.05f;

        private Coroutine _fadeRoutine;
        private Coroutine _pulseRoutine;
        private Vector3 _baseScale = Vector3.one;

        public bool IsShowing => canvasGroup != null && canvasGroup.alpha > 0.01f;

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

            if (panelRect != null)
            {
                _baseScale = panelRect.localScale;
            }

            HideObjective(instant: true);
        }

        public void SetVisible(bool visible, bool instant = false)
        {
            if (canvasGroup == null) return;

            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);

            float targetAlpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = false; // Виджет чисто информационный
            canvasGroup.interactable = false;

            if (instant || fadeDuration <= 0.001f)
            {
                canvasGroup.alpha = targetAlpha;
            }
            else
            {
                _fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, fadeDuration));
            }
        }

        private IEnumerator FadeRoutine(float targetAlpha, float duration)
        {
            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
            _fadeRoutine = null;
        }

        /// <summary>
        /// Показывает плашку задания с инструкцией и строкой прогресса
        /// </summary>
        public void ShowObjective(string instruction, string progress = "", string header = "ЗАДАНИЕ")
        {
            EnsureUIBuilt();

            if (headerText != null) headerText.text = header;
            if (instructionText != null) instructionText.text = instruction;

            if (progressText != null)
            {
                if (string.IsNullOrEmpty(progress))
                {
                    progressText.gameObject.SetActive(false);
                }
                else
                {
                    progressText.gameObject.SetActive(true);
                    progressText.text = progress;
                }
            }

            SetVisible(true, instant: false);
            TriggerPulse();
        }

        /// <summary>
        /// Обновляет строку прогресса/счётчика
        /// </summary>
        public void UpdateProgress(string progress)
        {
            if (progressText != null)
            {
                progressText.gameObject.SetActive(!string.IsNullOrEmpty(progress));
                progressText.text = progress;
            }
            TriggerPulse();
        }

        /// <summary>
        /// Удобный метод для числового счётчика (например: "Удар по манекену 1/3")
        /// </summary>
        public void UpdateCounter(int current, int max, string prefix = "Удар по манекену")
        {
            UpdateProgress($"{prefix} {current}/{max}");
        }

        /// <summary>
        /// Скрывает плашку задания
        /// </summary>
        public void HideObjective(bool instant = false)
        {
            SetVisible(false, instant);
        }

        /// <summary>
        /// Легкая визуальная пульсация при обновлении счётчика/цели
        /// </summary>
        public void TriggerPulse()
        {
            if (!IsShowing || panelRect == null) return;

            if (_pulseRoutine != null) StopCoroutine(_pulseRoutine);
            _pulseRoutine = StartCoroutine(PulseRoutine());
        }

        private IEnumerator PulseRoutine()
        {
            float halfDur = 0.08f;
            float elapsed = 0f;
            Vector3 popScale = _baseScale * pulseScaleAmount;

            while (elapsed < halfDur)
            {
                elapsed += Time.unscaledDeltaTime;
                panelRect.localScale = Vector3.Lerp(_baseScale, popScale, elapsed / halfDur);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < halfDur)
            {
                elapsed += Time.unscaledDeltaTime;
                panelRect.localScale = Vector3.Lerp(popScale, _baseScale, elapsed / halfDur);
                yield return null;
            }

            panelRect.localScale = _baseScale;
            _pulseRoutine = null;
        }

        public void EnsureUIBuilt()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (panelRect == null)
            {
                var p = transform.Find("ObjectivePanel");
                if (p != null) panelRect = p.GetComponent<RectTransform>();
                else panelRect = GetComponent<RectTransform>();
            }

            if (panelRect != null)
            {
                if (backgroundImage == null) backgroundImage = panelRect.GetComponent<Image>();
                if (borderOutline == null) borderOutline = panelRect.GetComponent<Outline>();

                if (headerText == null)
                {
                    var ht = panelRect.Find("HeaderText");
                    if (ht != null) headerText = ht.GetComponent<Text>();
                }

                if (instructionText == null)
                {
                    var it = panelRect.Find("InstructionText");
                    if (it != null) instructionText = it.GetComponent<Text>();
                }

                if (progressText == null)
                {
                    var pt = panelRect.Find("ProgressText");
                    if (pt != null) progressText = pt.GetComponent<Text>();
                }
            }
        }
    }
}
