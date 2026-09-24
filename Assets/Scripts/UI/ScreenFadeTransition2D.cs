using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Combat.UI
{
    /// <summary>
    /// Кинематографичный контроллер плавного затемнения и проявления экрана (Fade In / Fade Out).
    /// При загрузке сцены удерживает экран в темноте и плавно проявляет игровой мир,
    /// устраняя резкие скачки яркости и графические артефакты первой секунды загрузки.
    /// </summary>
    [DisallowMultipleComponent]
    public class ScreenFadeTransition2D : MonoBehaviour
    {
        public static ScreenFadeTransition2D Instance { get; private set; }

        [Header("--- Стартовое проявление сцены (Fade In) ---")]
        [Tooltip("Автоматически начинать сцену с темноты и плавно проявлять игровой мир")]
        [SerializeField] private bool fadeInOnStart = true;

        [Tooltip("Длительность проявления сцены из темноты в секундах")]
        [SerializeField] private float fadeInDuration = 0.65f;

        [Tooltip("Небольшая задержка перед началом проявления (чтобы мир успел отрисовать первый кадр)")]
        [SerializeField] private float startDelay = 0.05f;

        [Header("--- Настройки оверлея ---")]
        [SerializeField] private Color fadeColor = Color.black;

        [Header("--- UI References ---")]
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private CanvasGroup overlayCanvasGroup;
        [SerializeField] private Image overlayImage;

        private Coroutine _fadeRoutine;

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

            EnsureOverlayBuilt();

            // С самого первого кадра Awake экран должен быть абсолютно чёрным
            if (fadeInOnStart && overlayCanvasGroup != null)
            {
                overlayCanvasGroup.alpha = 1.0f;
                overlayCanvasGroup.blocksRaycasts = true;
                if (overlayImage != null) overlayImage.gameObject.SetActive(true);
            }
        }

        private void Start()
        {
            if (fadeInOnStart)
            {
                StartFadeIn(fadeInDuration, startDelay);
            }
        }

        public void EnsureOverlayBuilt()
        {
            if (targetCanvas == null)
            {
                targetCanvas = GetComponent<Canvas>() ?? GetComponentInParent<Canvas>() ?? FindAnyObjectByType<Canvas>();
            }

            if (targetCanvas == null) return;

            var existingOverlay = targetCanvas.transform.Find("SceneFadeOverlay");
            if (existingOverlay != null)
            {
                overlayCanvasGroup = existingOverlay.GetComponent<CanvasGroup>();
                overlayImage = existingOverlay.GetComponent<Image>();
            }

            if (overlayCanvasGroup == null || overlayImage == null)
            {
                var overlayObj = new GameObject("SceneFadeOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                overlayObj.transform.SetParent(targetCanvas.transform, false);

                var rt = overlayObj.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                overlayImage = overlayObj.GetComponent<Image>();
                overlayImage.color = fadeColor;
                overlayImage.raycastTarget = false;

                overlayCanvasGroup = overlayObj.GetComponent<CanvasGroup>();
                overlayCanvasGroup.alpha = fadeInOnStart ? 1.0f : 0.0f;
                overlayCanvasGroup.blocksRaycasts = fadeInOnStart;

                // Размещаем оверлей поверх обычного UI
                overlayObj.transform.SetAsLastSibling();
            }
        }

        /// <summary>
        /// Плавно проявить сцену из темноты (Fade In from Black)
        /// </summary>
        public void StartFadeIn(float duration = 0.65f, float delay = 0f, Action onComplete = null)
        {
            EnsureOverlayBuilt();
            if (overlayCanvasGroup == null) return;

            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeRoutine(1.0f, 0.0f, duration, delay, onComplete));
        }

        /// <summary>
        /// Плавно затемнить сцену в темноту (Fade Out to Black)
        /// </summary>
        public void StartFadeOut(float duration = 0.45f, float delay = 0f, Action onComplete = null)
        {
            EnsureOverlayBuilt();
            if (overlayCanvasGroup == null) return;

            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeRoutine(0.0f, 1.0f, duration, delay, onComplete));
        }

        /// <summary>
        /// Затемнить экран и загрузить указанную сцену
        /// </summary>
        public static void FadeAndLoadScene(string sceneName, float duration = 0.45f)
        {
            if (Instance != null)
            {
                Instance.StartFadeOut(duration, 0f, () =>
                {
                    Time.timeScale = 1.0f;
                    SceneManager.LoadScene(sceneName);
                });
            }
            else
            {
                Time.timeScale = 1.0f;
                SceneManager.LoadScene(sceneName);
            }
        }

        private IEnumerator FadeRoutine(float fromAlpha, float toAlpha, float duration, float delay, Action onComplete)
        {
            overlayCanvasGroup.blocksRaycasts = (toAlpha > 0.01f || fromAlpha > 0.01f);
            overlayCanvasGroup.alpha = fromAlpha;
            if (overlayImage != null) overlayImage.gameObject.SetActive(true);

            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            float elapsed = 0f;
            float dur = Mathf.Max(duration, 0.01f);

            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                // Плавная синусоидальная интерполяция (SmoothStep)
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                overlayCanvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, smoothT);
                yield return null;
            }

            overlayCanvasGroup.alpha = toAlpha;
            overlayCanvasGroup.blocksRaycasts = toAlpha > 0.5f;

            if (toAlpha <= 0.001f && overlayImage != null)
            {
                overlayImage.gameObject.SetActive(false);
            }

            _fadeRoutine = null;
            onComplete?.Invoke();
        }
    }
}
