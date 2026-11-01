using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.Dialogue
{
    /// <summary>
    /// UI-представление диалогового окна в нижней части экрана.
    /// Отвечает за анимацию появления/исчезновения, эффект пишущей машинки,
    /// отображение имени, текста и портрета говорящего (например, Вовы справа).
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueUI : MonoBehaviour
    {
        [Header("--- Canvas & Window ---")]
        [SerializeField] private CanvasGroup mainCanvasGroup;
        [SerializeField] private RectTransform windowRect;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image borderImage;

        [Header("--- Text Elements ---")]
        [SerializeField] private Text speakerNameText;
        [SerializeField] private Text dialogueText;
        [SerializeField] private GameObject continuePrompt;
        [SerializeField] private Text continuePromptText;

        [Header("--- Portrait Elements ---")]
        [SerializeField] private GameObject portraitContainer;
        [SerializeField] private RectTransform portraitFrameRt;
        [SerializeField] private Image portraitImage;
        [SerializeField] private RectTransform portraitImageRt;

        [Header("--- Animation Settings ---")]
        [SerializeField] private float fadeDuration = 0.2f;
        [SerializeField] private float promptPulseSpeed = 4f;

        private Coroutine _fadeRoutine;
        private Coroutine _typewriterRoutine;
        private Coroutine _promptPulseRoutine;

        private string _targetFullText = "";
        private Action _onTypewriterFinished;

        public bool IsTyping => _typewriterRoutine != null;
        public bool IsVisible => mainCanvasGroup != null && mainCanvasGroup.alpha > 0.01f;

        private void Awake()
        {
            EnsureUIBuilt();
            SetVisible(false, instant: true);
        }

        public void SetVisible(bool visible, bool instant = false)
        {
            if (mainCanvasGroup == null) return;

            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);

            float targetAlpha = visible ? 1f : 0f;
            mainCanvasGroup.blocksRaycasts = visible;
            mainCanvasGroup.interactable = visible;

            if (instant || fadeDuration <= 0.001f)
            {
                mainCanvasGroup.alpha = targetAlpha;
                if (!visible) ResetDisplay();
            }
            else
            {
                _fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, fadeDuration, () =>
                {
                    if (!visible) ResetDisplay();
                }));
            }
        }

        private IEnumerator FadeRoutine(float targetAlpha, float duration, Action onComplete)
        {
            float startAlpha = mainCanvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                mainCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                yield return null;
            }

            mainCanvasGroup.alpha = targetAlpha;
            _fadeRoutine = null;
            onComplete?.Invoke();
        }

        private void ResetDisplay()
        {
            if (dialogueText != null) dialogueText.text = "";
            if (speakerNameText != null) speakerNameText.text = "";
            if (continuePrompt != null) continuePrompt.SetActive(false);
            if (portraitContainer != null) portraitContainer.SetActive(false);
            _targetFullText = "";
        }

        /// <summary>
        /// Отображает переданную реплику, запуская эффект пишущей машинки
        /// </summary>
        public void DisplayLine(DialogueLine line, Action onTypewriterComplete = null)
        {
            if (line == null) return;

            if (!IsVisible)
            {
                SetVisible(true, instant: false);
            }

            _onTypewriterFinished = onTypewriterComplete;
            _targetFullText = line.text ?? "";

            // Имя персонажа
            if (speakerNameText != null)
            {
                speakerNameText.text = line.speakerName ?? "";
                speakerNameText.color = line.speakerColor;
            }

            // Настройка портрета (для Вовы справа или любого другого)
            SetupPortrait(line);

            // Скрываем подсказку перехода пока печатается текст
            if (continuePrompt != null)
            {
                continuePrompt.SetActive(false);
            }

            if (_typewriterRoutine != null) StopCoroutine(_typewriterRoutine);

            if (line.typingSpeed <= 0f)
            {
                // Мгновенный вывод текста
                if (dialogueText != null) dialogueText.text = _targetFullText;
                OnTypingFinished();
            }
            else
            {
                _typewriterRoutine = StartCoroutine(TypewriterRoutine(line.typingSpeed));
            }
        }

        private void SetupPortrait(DialogueLine line)
        {
            if (portraitContainer == null || portraitImage == null) return;

            if (line.portrait == null)
            {
                portraitContainer.SetActive(false);
                return;
            }

            portraitContainer.SetActive(true);
            portraitImage.sprite = line.portrait;

            // Позиционирование контейнера: справа или слева
            if (portraitFrameRt != null)
            {
                if (line.portraitOnRight)
                {
                    portraitFrameRt.anchorMin = new Vector2(1f, 0.5f);
                    portraitFrameRt.anchorMax = new Vector2(1f, 0.5f);
                    portraitFrameRt.pivot = new Vector2(1f, 0.5f);
                    portraitFrameRt.anchoredPosition = new Vector2(-15f, 0f);
                }
                else
                {
                    portraitFrameRt.anchorMin = new Vector2(0f, 0.5f);
                    portraitFrameRt.anchorMax = new Vector2(0f, 0.5f);
                    portraitFrameRt.pivot = new Vector2(0f, 0.5f);
                    portraitFrameRt.anchoredPosition = new Vector2(15f, 0f);
                }
            }

            // Масштаб и смещение для приближенного кадрирования лица
            if (portraitImageRt != null)
            {
                portraitImageRt.localScale = new Vector3(line.portraitScale.x, line.portraitScale.y, 1f);
                portraitImageRt.anchoredPosition = line.portraitOffset;
            }
        }

        private IEnumerator TypewriterRoutine(float charsPerSec)
        {
            if (dialogueText != null) dialogueText.text = "";

            float delay = 1f / Mathf.Max(1f, charsPerSec);
            int totalChars = _targetFullText.Length;

            for (int i = 1; i <= totalChars; i++)
            {
                if (dialogueText != null)
                {
                    dialogueText.text = _targetFullText.Substring(0, i);
                }

                yield return new WaitForSecondsRealtime(delay);
            }

            _typewriterRoutine = null;
            OnTypingFinished();
        }

        /// <summary>
        /// Мгновенно завершить печать текущей строки
        /// </summary>
        public void SkipTypewriter()
        {
            if (!IsTyping) return;

            if (_typewriterRoutine != null)
            {
                StopCoroutine(_typewriterRoutine);
                _typewriterRoutine = null;
            }

            if (dialogueText != null)
            {
                dialogueText.text = _targetFullText;
            }

            OnTypingFinished();
        }

        private void OnTypingFinished()
        {
            _typewriterRoutine = null;

            if (continuePrompt != null)
            {
                continuePrompt.SetActive(true);
                if (_promptPulseRoutine != null) StopCoroutine(_promptPulseRoutine);
                _promptPulseRoutine = StartCoroutine(PromptPulseRoutine());
            }

            var callback = _onTypewriterFinished;
            _onTypewriterFinished = null;
            callback?.Invoke();
        }

        private IEnumerator PromptPulseRoutine()
        {
            if (continuePromptText == null) yield break;

            Color baseColor = continuePromptText.color;
            while (continuePrompt != null && continuePrompt.activeSelf)
            {
                float alpha = 0.5f + 0.5f * Mathf.PingPong(Time.unscaledTime * promptPulseSpeed, 1f);
                continuePromptText.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                yield return null;
            }
        }

        /// <summary>
        /// Проверяет наличие компонентов и создает стандартную структуру UI, если не назначена вручную
        /// </summary>
        public void EnsureUIBuilt()
        {
            if (mainCanvasGroup == null)
            {
                mainCanvasGroup = GetComponent<CanvasGroup>();
                if (mainCanvasGroup == null) mainCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (windowRect == null)
            {
                var win = transform.Find("DialogueWindow");
                if (win != null) windowRect = win.GetComponent<RectTransform>();
                else windowRect = GetComponent<RectTransform>();
            }

            if (speakerNameText == null && windowRect != null)
            {
                var txt = windowRect.Find("SpeakerNameText");
                if (txt != null) speakerNameText = txt.GetComponent<Text>();
            }

            if (dialogueText == null && windowRect != null)
            {
                var txt = windowRect.Find("DialogueText");
                if (txt != null) dialogueText = txt.GetComponent<Text>();
            }

            if (continuePrompt == null && windowRect != null)
            {
                var pr = windowRect.Find("ContinuePrompt");
                if (pr != null)
                {
                    continuePrompt = pr.gameObject;
                    continuePromptText = pr.GetComponent<Text>();
                }
            }

            if (portraitContainer == null && windowRect != null)
            {
                var port = windowRect.Find("PortraitContainer");
                if (port != null)
                {
                    portraitContainer = port.gameObject;
                    portraitFrameRt = port.GetComponent<RectTransform>();
                    var img = port.Find("PortraitImage");
                    if (img != null)
                    {
                        portraitImage = img.GetComponent<Image>();
                        portraitImageRt = img.GetComponent<RectTransform>();
                    }
                }
            }
        }
    }
}
