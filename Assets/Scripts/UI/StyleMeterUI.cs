using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Combat.Style;

namespace Combat.UI
{
    /// <summary>
    /// Контроллер интерфейса боевого стиля (Style Meter UI).
    /// Отображает букву текущего ранга (D..SSS), шкалу прогресса, общий счет
    /// и всплывающие анимации бонусов при убийстве противников.
    /// </summary>
    [DisallowMultipleComponent]
    public class StyleMeterUI : MonoBehaviour
    {
        [Header("--- UI References ---")]
        [SerializeField] private Text rankLetterText;
        [SerializeField] private Text rankTitleText;
        [SerializeField] private Image rankFillBar;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text popupBonusText;
        [SerializeField] private RectTransform rankBadgeTransform;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("--- Анимации и сочность (Juice) ---")]
        [SerializeField] private float punchScaleOnHit = 1.32f;
        [SerializeField] private float punchScaleOnRankUp = 1.65f;
        [SerializeField] private float punchReturnSpeed = 9f;
        [SerializeField] private float fillLerpSpeed = 12f;

        private float _currentPunchScale = 1.0f;
        private float _targetFill = 0f;
        private float _currentFill = 0f;
        private Coroutine _popupRoutine;

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            StyleManager.OnStyleAdded += HandleStyleAdded;
            StyleManager.OnRankChanged += HandleRankChanged;
            StyleManager.OnTotalScoreChanged += HandleScoreChanged;

            if (StyleManager.Instance != null)
            {
                UpdateRankVisuals(StyleManager.Instance.CurrentRank);
                UpdateScore(StyleManager.Instance.TotalScore);
            }
        }

        private void OnDisable()
        {
            StyleManager.OnStyleAdded -= HandleStyleAdded;
            StyleManager.OnRankChanged -= HandleRankChanged;
            StyleManager.OnTotalScoreChanged -= HandleScoreChanged;
        }

        private void Update()
        {
            var mgr = StyleManager.Instance;
            if (mgr != null)
            {
                _targetFill = mgr.GetRankProgressNormalized();
            }

            // Плавное заполнение полосы прогресса
            if (rankFillBar != null)
            {
                _currentFill = Mathf.Lerp(_currentFill, _targetFill, Time.deltaTime * fillLerpSpeed);
                rankFillBar.fillAmount = _currentFill;
            }

            // Плавный возврат масштаба буквы после всплеска (Punch scale)
            if (rankBadgeTransform != null && _currentPunchScale > 1.001f)
            {
                _currentPunchScale = Mathf.Lerp(_currentPunchScale, 1.0f, Time.deltaTime * punchReturnSpeed);
                rankBadgeTransform.localScale = Vector3.one * _currentPunchScale;
            }
        }

        private void HandleStyleAdded(int points, string reason, StyleRank rank)
        {
            _currentPunchScale = punchScaleOnHit;

            if (popupBonusText != null)
            {
                if (_popupRoutine != null) StopCoroutine(_popupRoutine);
                _popupRoutine = StartCoroutine(ShowPopupBonusRoutine($"+{points} {reason}", StyleManager.GetRankColor(rank)));
            }
        }

        private void HandleRankChanged(StyleRank newRank)
        {
            _currentPunchScale = punchScaleOnRankUp;
            UpdateRankVisuals(newRank);
        }

        private void HandleScoreChanged(int newScore)
        {
            UpdateScore(newScore);
        }

        private void UpdateRankVisuals(StyleRank rank)
        {
            Color col = StyleManager.GetRankColor(rank);

            if (rankLetterText != null)
            {
                rankLetterText.text = rank.ToString();
                rankLetterText.color = col;
            }

            if (rankTitleText != null)
            {
                rankTitleText.text = StyleManager.GetRankTitle(rank);
                rankTitleText.color = col;
            }

            if (rankFillBar != null)
            {
                rankFillBar.color = col;
            }
        }

        private void UpdateScore(int score)
        {
            if (scoreText != null)
            {
                scoreText.text = $"STYLE: {score:N0}";
            }
        }

        private IEnumerator ShowPopupBonusRoutine(string text, Color color)
        {
            popupBonusText.text = text;
            popupBonusText.color = color;
            popupBonusText.gameObject.SetActive(true);

            var rt = popupBonusText.rectTransform;
            Vector2 basePos = new Vector2(0f, -20f);
            rt.anchoredPosition = basePos;
            rt.localScale = Vector3.one * 1.25f;

            float duration = 1.35f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                rt.anchoredPosition = basePos + new Vector2(0f, -t * 22f);
                rt.localScale = Vector3.Lerp(Vector3.one * 1.25f, Vector3.one, t * 2.5f);

                Color c = color;
                c.a = Mathf.Clamp01(1f - Mathf.Pow(t, 2f));
                popupBonusText.color = c;

                yield return null;
            }

            popupBonusText.gameObject.SetActive(false);
        }
    }
}
