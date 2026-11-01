using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Combat.Dialogue
{
    /// <summary>
    /// Центральный менеджер системы диалогов.
    /// Управляет воспроизведением очередей реплик, считыванием ввода игрока (ЛКМ / Пробел),
    /// блокировкой действий игрока во время разговоров и пулом неповторяющихся фраз.
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance { get; private set; }

        public static event Action<bool> OnDialogueActiveChanged;
        public static event Action<DialogueLine> OnLineStarted;
        public static event Action<DialogueLine> OnLineFinished;

        [Header("--- UI Reference ---")]
        [SerializeField] private DialogueUI dialogueUI;

        [Header("--- Input Keys ---")]
        [SerializeField] private KeyCode advanceKey = KeyCode.Space;
        [SerializeField] private KeyCode altAdvanceKey = KeyCode.E;
        [SerializeField] private KeyCode returnAdvanceKey = KeyCode.Return;

        [Header("--- Non-Repeating Dialogue Pools ---")]
        [SerializeField] private NonRepeatingDialoguePool explosionFailPool = NonRepeatingDialoguePool.CreateExplosionFailPool();
        [SerializeField] private NonRepeatingDialoguePool spellMissPool = NonRepeatingDialoguePool.CreateSpellMissPool();

        private readonly Queue<DialogueLine> _lineQueue = new Queue<DialogueLine>();
        private DialogueLine _currentLine;
        private Action _onSequenceComplete;
        private Coroutine _autoAdvanceRoutine;
        private bool _isDialogueActive = false;

        public bool IsDialogueActive => _isDialogueActive;
        public DialogueLine CurrentLine => _currentLine;
        public NonRepeatingDialoguePool ExplosionFailPool => explosionFailPool;
        public NonRepeatingDialoguePool SpellMissPool => spellMissPool;

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

            if (dialogueUI == null)
            {
                dialogueUI = GetComponentInChildren<DialogueUI>();
                if (dialogueUI == null)
                {
                    dialogueUI = FindAnyObjectByType<DialogueUI>();
                }
            }

            if (explosionFailPool == null || explosionFailPool.TotalCount == 0)
            {
                explosionFailPool = NonRepeatingDialoguePool.CreateExplosionFailPool();
            }

            if (spellMissPool == null || spellMissPool.TotalCount == 0)
            {
                spellMissPool = NonRepeatingDialoguePool.CreateSpellMissPool();
            }
        }

        private void Update()
        {
            if (!_isDialogueActive) return;

            // Если открыто главное меню настроек или пауза — не переключаем диалог
            if (Combat.UI.PauseMenuController.IsGamePaused) return;

            if (IsAdvanceTriggered())
            {
                AdvanceLine();
            }
        }

        private bool IsAdvanceTriggered()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;

            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
            if (kb != null)
            {
                if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)
                {
                    return true;
                }
            }
#endif
            try
            {
                if (Input.GetMouseButtonDown(0)) return true;
                if (Input.GetKeyDown(advanceKey) || Input.GetKeyDown(altAdvanceKey) || Input.GetKeyDown(returnAdvanceKey))
                {
                    return true;
                }
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Запускает воспроизведение последовательности реплик диалога
        /// </summary>
        public void PlaySequence(IEnumerable<DialogueLine> lines, Action onComplete = null)
        {
            if (lines == null)
            {
                onComplete?.Invoke();
                return;
            }

            _lineQueue.Clear();
            foreach (var l in lines)
            {
                if (l != null) _lineQueue.Enqueue(l);
            }

            if (_lineQueue.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            _onSequenceComplete = onComplete;
            SetDialogueActive(true);
            ShowNextQueuedLine();
        }

        /// <summary>
        /// Воспроизводит одну отдельную реплику
        /// </summary>
        public void PlaySingleLine(DialogueLine line, Action onComplete = null)
        {
            if (line == null)
            {
                onComplete?.Invoke();
                return;
            }

            PlaySequence(new DialogueLine[] { line }, onComplete);
        }

        /// <summary>
        /// Воспроизводит одну реплику Незнакомца по тексту
        /// </summary>
        public void PlayStrangerLine(string text, Action onComplete = null)
        {
            PlaySingleLine(DialogueLine.Stranger(text), onComplete);
        }

        /// <summary>
        /// Воспроизводит следующую фразу из пула провалов взрыва (8 неповторяющихся фраз).
        /// Если фразы закончились — сразу вызывает onComplete.
        /// </summary>
        public bool PlayNextExplosionFailPhrase(Action onComplete = null)
        {
            string phrase = explosionFailPool.GetNextPhrase();
            if (string.IsNullOrEmpty(phrase))
            {
                onComplete?.Invoke();
                return false;
            }

            PlayStrangerLine(phrase, onComplete);
            return true;
        }

        /// <summary>
        /// Воспроизводит следующую фразу из пула промахов заклинания (4 неповторяющиеся фразы).
        /// </summary>
        public bool PlayNextSpellMissPhrase(Action onComplete = null)
        {
            string phrase = spellMissPool.GetNextPhrase();
            if (string.IsNullOrEmpty(phrase))
            {
                onComplete?.Invoke();
                return false;
            }

            PlayStrangerLine(phrase, onComplete);
            return true;
        }

        /// <summary>
        /// Переход к следующему состоянию диалога (завершить печать или показать следующую строку)
        /// </summary>
        public void AdvanceLine()
        {
            if (!_isDialogueActive) return;

            if (dialogueUI != null && dialogueUI.IsTyping)
            {
                // Если текст еще печатается — мгновенно завершаем печать
                dialogueUI.SkipTypewriter();
                return;
            }

            if (_autoAdvanceRoutine != null)
            {
                StopCoroutine(_autoAdvanceRoutine);
                _autoAdvanceRoutine = null;
            }

            if (_currentLine != null)
            {
                OnLineFinished?.Invoke(_currentLine);
            }

            if (_lineQueue.Count > 0)
            {
                ShowNextQueuedLine();
            }
            else
            {
                CloseDialogue();
            }
        }

        private void ShowNextQueuedLine()
        {
            _currentLine = _lineQueue.Dequeue();
            OnLineStarted?.Invoke(_currentLine);

            if (dialogueUI != null)
            {
                dialogueUI.DisplayLine(_currentLine, () =>
                {
                    if (_currentLine != null && _currentLine.autoAdvanceDelay > 0f)
                    {
                        if (_autoAdvanceRoutine != null) StopCoroutine(_autoAdvanceRoutine);
                        _autoAdvanceRoutine = StartCoroutine(AutoAdvanceRoutine(_currentLine.autoAdvanceDelay));
                    }
                });
            }
        }

        private IEnumerator AutoAdvanceRoutine(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            _autoAdvanceRoutine = null;
            AdvanceLine();
        }

        /// <summary>
        /// Закрывает диалоговое окно и завершает последовательность
        /// </summary>
        public void CloseDialogue()
        {
            if (_autoAdvanceRoutine != null)
            {
                StopCoroutine(_autoAdvanceRoutine);
                _autoAdvanceRoutine = null;
            }

            _lineQueue.Clear();
            _currentLine = null;

            if (dialogueUI != null)
            {
                dialogueUI.SetVisible(false);
            }

            SetDialogueActive(false);

            var callback = _onSequenceComplete;
            _onSequenceComplete = null;
            callback?.Invoke();
        }

        private void SetDialogueActive(bool active)
        {
            if (_isDialogueActive == active) return;

            _isDialogueActive = active;
            OnDialogueActiveChanged?.Invoke(_isDialogueActive);
        }

        public void AssignUI(DialogueUI ui)
        {
            dialogueUI = ui;
        }
    }
}
