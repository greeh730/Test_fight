// Assets/Scripts/Magic/PlayerMagicManager.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fencing;

namespace Magic
{
    /// <summary>
    /// Менеджер заклинаний игрока и контроллер их ротации:
    /// - Содержит список доступных заклинаний (Огонь, Туман и др.).
    /// - При старте случайно выбирает начальное заклинание (равные шансы 50%/50%).
    /// - Управляет динамическими шансами выпадения при смене заклинания:
    ///   с каждым повторным выпадением подряд одного и того же заклинания его шанс делится на два
    ///   (50% -> 25% -> 12.5% -> 6.25% ...), а оставшаяся вероятность распределяется между остальными заклинаниями.
    /// - При смене заклинания автоматически переключает тему UI (обводку, стрелки, иконку).
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerMagicManager : MonoBehaviour
    {
        [Header("--- Список доступных заклинаний ---")]
        [Tooltip("Список заклинаний, доступных персонажу для случайной ротации")]
        [SerializeField] private List<BaseMagicSpell> availableSpells = new List<BaseMagicSpell>();

        [Header("--- Общие настройки заклинаний (Shared Magic Settings) ---")]
        [Tooltip("Время на ввод комбинации заклинания в секундах (действует на все заклинания)")]
        [SerializeField] private float regenerationInterval = 10f;

        [Tooltip("Время штрафного КД (блокировки магии) при провале в секундах (действует на все заклинания)")]
        [SerializeField] private float lockoutDuration = 5f;

        [Tooltip("Количество направлений в комбинации заклинания (действует на все заклинания)")]
        [Range(1, 6)]
        [SerializeField] private int sequenceLength = 3;

        [Tooltip("Разрешены ли повторы направлений в одной комбинации")]
        [SerializeField] private bool allowDuplicates = true;

        [Tooltip("Считать ли отпускание ЛКМ во время неполного ввода провалом")]
        [SerializeField] private bool failOnLMBRelease = true;

        [Tooltip("Размер стрелок внутри строки/счётчика в пикселях (действует на все заклинания)")]
        [SerializeField] private float arrowSize = 48f;

        [Tooltip("Расстояние между стрелками в строке в пикселях")]
        [SerializeField] private float arrowSpacing = 8f;

        [Tooltip("Цвет подсветки при успешном вводе (зеленый)")]
        [SerializeField] private Color successColor = new Color(0.2f, 1.0f, 0.4f, 1f);

        [Tooltip("Цвет подсветки при ошибке / провале (красный)")]
        [SerializeField] private Color failureColor = new Color(1.0f, 0.2f, 0.25f, 1f);

        [Tooltip("Цвет рамки при затемнении во время блокировки / КД")]
        [SerializeField] private Color dimmedBorderColor = new Color(0.35f, 0.35f, 0.4f, 0.5f);

        [Tooltip("Цвет фона при затемнении во время блокировки / КД")]
        [SerializeField] private Color dimmedFillColor = new Color(0.02f, 0.02f, 0.03f, 0.45f);

        [Tooltip("Прозрачность затемненной строки при блокировке")]
        [Range(0f, 1f)]
        [SerializeField] private float dimmedAlpha = 0.4f;

        [Header("--- Настройки вероятностей выпадения ---")]
        [Tooltip("Коэффициент уменьшения шанса при повторном выпадении (0.5 = шанс делится на 2)")]
        [Range(0.1f, 0.9f)]
        [SerializeField] private float repeatDecayFactor = 0.5f;

        [Tooltip("Минимальный возможный шанс выпадения заклинания")]
        [Range(0.01f, 0.2f)]
        [SerializeField] private float minProbabilityFloor = 0.02f;

        [Header("--- Блокировка ротации (Lock Rotation) ---")]
        [Tooltip("Заблокировать случайную смену заклинаний (используется в обучении)")]
        [SerializeField] private bool lockSpellRotation = false;

        [Tooltip("Закрепленное заклинание при блокировке ротации")]
        [SerializeField] private BaseMagicSpell lockedSpell;

        [Header("--- Текущий статус ротации (Debug) ---")]
        [Tooltip("Текущее активное заклинание")]
        [SerializeField] private BaseMagicSpell currentActiveSpell;

        [Tooltip("Последнее выпавшее заклинание")]
        [SerializeField] private BaseMagicSpell lastSelectedSpell;

        [Tooltip("Количество выпадений подряд последнего заклинания")]
        [SerializeField] private int consecutiveRepeatCount = 0;

        [Tooltip("Отображение текущих вероятностей каждого заклинания")]
        [SerializeField] private List<string> probabilityDebugList = new List<string>();

        [Header("--- Ссылки на UI ---")]
        [SerializeField] private FencingWheelUI wheelUI;
        [SerializeField] private FencingWheel wheel;

        public event Action<BaseMagicSpell> OnActiveSpellChanged;

        #region Properties
        public bool IsRotationLocked => lockSpellRotation;
        public BaseMagicSpell LockedSpell => lockedSpell;
        public IReadOnlyList<BaseMagicSpell> AvailableSpells => availableSpells;
        public BaseMagicSpell CurrentActiveSpell => currentActiveSpell;
        public BaseMagicSpell LastSelectedSpell => lastSelectedSpell;
        public int ConsecutiveRepeatCount => consecutiveRepeatCount;
        public float RepeatDecayFactor { get => repeatDecayFactor; set => repeatDecayFactor = Mathf.Clamp(value, 0.05f, 0.95f); }

        public float RegenerationInterval
        {
            get => regenerationInterval;
            set
            {
                regenerationInterval = Mathf.Max(0.5f, value);
                ApplySharedSettingsToSpells();
            }
        }

        public float LockoutDuration
        {
            get => lockoutDuration;
            set
            {
                lockoutDuration = Mathf.Max(0.5f, value);
                ApplySharedSettingsToSpells();
            }
        }

        public int SequenceLength
        {
            get => sequenceLength;
            set
            {
                sequenceLength = Mathf.Clamp(value, 1, 6);
                ApplySharedSettingsToSpells();
            }
        }

        public bool AllowDuplicates
        {
            get => allowDuplicates;
            set
            {
                allowDuplicates = value;
                ApplySharedSettingsToSpells();
            }
        }

        public bool FailOnLMBRelease
        {
            get => failOnLMBRelease;
            set
            {
                failOnLMBRelease = value;
                ApplySharedSettingsToSpells();
            }
        }

        public float ArrowSize
        {
            get => arrowSize;
            set
            {
                arrowSize = Mathf.Max(8f, value);
                ApplySharedSettingsToSpells();
            }
        }

        public float ArrowSpacing
        {
            get => arrowSpacing;
            set
            {
                arrowSpacing = Mathf.Max(0f, value);
                ApplySharedSettingsToSpells();
            }
        }

        public Color SuccessColor
        {
            get => successColor;
            set
            {
                successColor = value;
                ApplySharedSettingsToSpells();
            }
        }

        public Color FailureColor
        {
            get => failureColor;
            set
            {
                failureColor = value;
                ApplySharedSettingsToSpells();
            }
        }

        public Color DimmedBorderColor
        {
            get => dimmedBorderColor;
            set
            {
                dimmedBorderColor = value;
                ApplySharedSettingsToSpells();
            }
        }

        public Color DimmedFillColor
        {
            get => dimmedFillColor;
            set
            {
                dimmedFillColor = value;
                ApplySharedSettingsToSpells();
            }
        }

        public float DimmedAlpha
        {
            get => dimmedAlpha;
            set
            {
                dimmedAlpha = Mathf.Clamp01(value);
                ApplySharedSettingsToSpells();
            }
        }
        #endregion

        private void Awake()
        {
            EnsureReferences();
            DiscoverSpellsOnPlayer();
        }

        private void OnEnable()
        {
            Combat.Settings.CombatSettingsManager.OnSettingsChanged -= HandleSettingsChanged;
            Combat.Settings.CombatSettingsManager.OnSettingsChanged += HandleSettingsChanged;

            if (Combat.Settings.CombatSettingsManager.Instance != null && Combat.Settings.CombatSettingsManager.Instance.CurrentSettings != null)
            {
                HandleSettingsChanged(Combat.Settings.CombatSettingsManager.Instance.CurrentSettings);
            }
        }

        private void OnDisable()
        {
            Combat.Settings.CombatSettingsManager.OnSettingsChanged -= HandleSettingsChanged;
        }

        private void HandleSettingsChanged(Combat.Settings.WheelSettingsData data)
        {
            if (data != null && data.arrowIconSize > 0f)
            {
                ArrowSize = data.arrowIconSize;
            }
        }

        private void Start()
        {
            EnsureReferences();

            // Если ротация заблокирована (например, в обучении только Огонь)
            if (lockSpellRotation && lockedSpell != null)
            {
                ActivateSpell(lockedSpell);
                return;
            }

            // Если заклинание еще не выбрано — делаем начальный случайный выбор
            if (currentActiveSpell == null)
            {
                RollAndActivateNextSpell();
            }
        }

        /// <summary>
        /// Блокирует ротацию на одном конкретном заклинании (например, на Огне в обучении).
        /// Все остальные заклинания отключаются, а это заклинание бесконечно обновляет руны без смены типа.
        /// </summary>
        public void LockToSpell(BaseMagicSpell spell)
        {
            if (spell == null) return;
            lockSpellRotation = true;
            lockedSpell = spell;
            ActivateSpell(spell);
            UpdateDebugProbabilityView();
        }

        /// <summary>
        /// Разблокирует обычную случайную ротацию между всеми заклинаниями.
        /// </summary>
        public void UnlockSpellRotation()
        {
            lockSpellRotation = false;
            lockedSpell = null;
            UpdateDebugProbabilityView();
        }

        /// <summary>
        /// Автоматически находит заклинания на игроке, если список пуст.
        /// </summary>
        public void DiscoverSpellsOnPlayer()
        {
            if (availableSpells == null) availableSpells = new List<BaseMagicSpell>();

            var found = GetComponents<BaseMagicSpell>();
            foreach (var sp in found)
            {
                if (!availableSpells.Contains(sp))
                {
                    availableSpells.Add(sp);
                }
            }

            // Привязываем менеджер ко всем заклинаниям
            foreach (var sp in availableSpells)
            {
                if (sp != null)
                {
                    sp.MagicManager = this;
                }
            }

            ApplySharedSettingsToSpells();
            UpdateDebugProbabilityView();
        }

        /// <summary>
        /// Применяет общие настройки (размер стрелок, время КД, длина комбинации и др.) ко всем заклинаниям.
        /// </summary>
        public void ApplySharedSettingsToSpells()
        {
            if (availableSpells == null) return;
            foreach (var sp in availableSpells)
            {
                if (sp == null) continue;
                sp.SyncSharedSettings(
                    regenerationInterval,
                    lockoutDuration,
                    sequenceLength,
                    allowDuplicates,
                    failOnLMBRelease,
                    arrowSize,
                    arrowSpacing,
                    successColor,
                    failureColor,
                    dimmedBorderColor,
                    dimmedFillColor,
                    dimmedAlpha
                );
            }
        }

        /// <summary>
        /// Рассчитывает текущие шансы выпадения для всех доступных заклинаний.
        /// Правило:
        /// - Базовый шанс у всех равен 1 / N.
        /// - Если заклинание выпало k раз подряд (k >= 1), его шанс = (1 / N) * (repeatDecayFactor ^ k).
        /// - Оставшаяся вероятность (1 - P(last)) делится поровну между остальными (N - 1) заклинаниями.
        /// </summary>
        public Dictionary<BaseMagicSpell, float> CalculateProbabilities()
        {
            var result = new Dictionary<BaseMagicSpell, float>();
            if (lockSpellRotation && lockedSpell != null)
            {
                result[lockedSpell] = 1.0f;
                return result;
            }

            if (availableSpells == null || availableSpells.Count == 0) return result;

            int n = availableSpells.Count;
            if (n == 1)
            {
                result[availableSpells[0]] = 1.0f;
                return result;
            }

            float baseP = 1.0f / n;

            // Если еще ни одно заклинание не выпадало:
            if (lastSelectedSpell == null || consecutiveRepeatCount == 0)
            {
                for (int i = 0; i < n; i++)
                {
                    result[availableSpells[i]] = baseP;
                }
                return result;
            }

            // Шанс для последнего выпавшего заклинания с учетом затухания
            float penalty = Mathf.Pow(repeatDecayFactor, consecutiveRepeatCount);
            float pLast = baseP * penalty;
            pLast = Mathf.Clamp(pLast, minProbabilityFloor, baseP);

            float pRemaining = Mathf.Max(0.001f, 1.0f - pLast);
            float pOtherEach = pRemaining / (n - 1);

            for (int i = 0; i < n; i++)
            {
                if (availableSpells[i] == lastSelectedSpell)
                {
                    result[availableSpells[i]] = pLast;
                }
                else
                {
                    result[availableSpells[i]] = pOtherEach;
                }
            }

            return result;
        }

        /// <summary>
        /// Случайный выбор следующего заклинания на основе рассчитанных вероятностей.
        /// </summary>
        public BaseMagicSpell RollNextSpell()
        {
            if (lockSpellRotation && lockedSpell != null)
            {
                return lockedSpell;
            }

            var probs = CalculateProbabilities();
            if (probs == null || probs.Count == 0) return null;

            float rnd = UnityEngine.Random.value;
            float cumulative = 0f;
            BaseMagicSpell chosen = null;

            foreach (var kvp in probs)
            {
                cumulative += kvp.Value;
                if (rnd <= cumulative)
                {
                    chosen = kvp.Key;
                    break;
                }
            }

            if (chosen == null && availableSpells.Count > 0)
            {
                chosen = availableSpells[availableSpells.Count - 1];
            }

            // Обновляем счетчик повторов
            if (chosen == lastSelectedSpell)
            {
                consecutiveRepeatCount++;
            }
            else
            {
                lastSelectedSpell = chosen;
                consecutiveRepeatCount = 1;
            }

            UpdateDebugProbabilityView();

            float rolledChance = probs.ContainsKey(chosen) ? probs[chosen] * 100f : 0f;
            Debug.Log($"<color=#B388FF>[Магия]</color> Выпало заклинание: <b>{chosen.SpellName}</b> (шанс выпадения был <b>{rolledChance:F1}%</b>, серия повторов: {consecutiveRepeatCount})");

            return chosen;
        }

        /// <summary>
        /// Выбирает следующее заклинание и активирует его на UI и персонаже.
        /// </summary>
        [ContextMenu("Случайно сменить заклинание (Roll Next Spell)")]
        public void RollAndActivateNextSpell()
        {
            BaseMagicSpell next = RollNextSpell();
            if (next != null)
            {
                ActivateSpell(next);
            }
        }

        /// <summary>
        /// Активирует указанное заклинание, деактивируя остальные.
        /// </summary>
        public void ActivateSpell(BaseMagicSpell spellToActivate)
        {
            if (spellToActivate == null) return;
            EnsureReferences();

            currentActiveSpell = spellToActivate;

            // Отключаем все заклинания кроме активного
            foreach (var sp in availableSpells)
            {
                if (sp == null) continue;
                if (sp == spellToActivate)
                {
                    sp.enabled = true;
                }
                else
                {
                    sp.enabled = false;
                }
            }

            // Применяем тему заклинания на UI
            if (wheelUI != null)
            {
                wheelUI.ActiveSpell = spellToActivate;
                spellToActivate.ApplySpellTheme(wheelUI);
            }

            spellToActivate.RegenerateSpell();
            OnActiveSpellChanged?.Invoke(spellToActivate);

            StartCoroutine(FadeInSpellBar());
        }

        private IEnumerator FadeInSpellBar()
        {
            if (wheelUI == null || wheelUI.MagicArrowBar == null) yield break;

            var cg = wheelUI.MagicArrowBar.GetComponent<CanvasGroup>();
            if (cg == null) cg = wheelUI.MagicArrowBar.gameObject.AddComponent<CanvasGroup>();

            float elapsed = 0f;
            float duration = 0.25f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            cg.alpha = 1f;
        }

        /// <summary>
        /// Вызывается заклинанием, когда его цикл завершился (успешный каст, окончание штрафа или истечение таймера).
        /// </summary>
        public void OnSpellFinished(BaseMagicSpell spell, bool wasSuccess)
        {
            StartCoroutine(SwitchSpellRoutine(spell));
        }

        private IEnumerator SwitchSpellRoutine(BaseMagicSpell finishedSpell)
        {
            yield return new WaitForSeconds(0.05f);
            if (lockSpellRotation && lockedSpell != null)
            {
                lockedSpell.RegenerateSpell();
                OnActiveSpellChanged?.Invoke(lockedSpell);
                StartCoroutine(FadeInSpellBar());
            }
            else
            {
                RollAndActivateNextSpell();
            }
        }

        private void UpdateDebugProbabilityView()
        {
            probabilityDebugList.Clear();
            var probs = CalculateProbabilities();
            foreach (var kvp in probs)
            {
                string note = (kvp.Key == lastSelectedSpell) ? $" (последнее x{consecutiveRepeatCount})" : "";
                probabilityDebugList.Add($"{kvp.Key.SpellName}: {kvp.Value * 100f:F1}%{note}");
            }
        }

        private void EnsureReferences()
        {
            if (wheelUI == null)
            {
#if UNITY_6000_0_OR_NEWER
                wheelUI = FindAnyObjectByType<FencingWheelUI>();
#else
                wheelUI = FindObjectOfType<FencingWheelUI>();
#endif
            }

            if (wheel == null)
            {
#if UNITY_6000_0_OR_NEWER
                wheel = FindAnyObjectByType<FencingWheel>();
#else
                wheel = FindObjectOfType<FencingWheel>();
#endif
            }
        }

        private void OnValidate()
        {
            regenerationInterval = Mathf.Max(0.5f, regenerationInterval);
            lockoutDuration = Mathf.Max(0.5f, lockoutDuration);
            sequenceLength = Mathf.Clamp(sequenceLength, 1, 6);
            arrowSize = Mathf.Max(8f, arrowSize);
            arrowSpacing = Mathf.Max(0f, arrowSpacing);

            ApplySharedSettingsToSpells();
            UpdateDebugProbabilityView();
        }
    }
}
