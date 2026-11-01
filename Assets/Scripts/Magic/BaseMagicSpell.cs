// Assets/Scripts/Magic/BaseMagicSpell.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Fencing;

namespace Magic
{
    /// <summary>
    /// Базовый класс для любых магических заклинаний в игре.
    /// Предоставляет общие параметры визуального стиля (обводка, цвет стрелок, иконка заклинания),
    /// генерацию комбинаций направлений, управление таймером и блокировкой при провале.
    /// 
    /// Чтобы добавить новое заклинание (например, Лёд, Молния, Земля):
    /// 1. Создайте класс заклинания: public class IceSpell : BaseMagicSpell
    /// 2. Настройте название, иконку, цвет обводки и стрелок в Инспекторе или конструкторе.
    /// 3. Переопределите OnCastSuccess() (что происходит при успехе) и OnCastFailure() (эффект при ошибке).
    /// </summary>
    public abstract class BaseMagicSpell : MonoBehaviour, IMagicSpell
    {
        protected static readonly AttackDirection[] AvailableDirections = new AttackDirection[]
        {
            AttackDirection.UpRight,
            AttackDirection.Right,
            AttackDirection.DownRight,
            AttackDirection.DownLeft,
            AttackDirection.Left,
            AttackDirection.UpLeft
        };

        [Header("--- 1. Идентификация и иконка заклинания ---")]
        [Tooltip("Название заклинания")]
        [SerializeField] protected string spellName = "Заклинание";

        [Tooltip("Иконка заклинания (отображается слева сверху над счётчиком)")]
        [SerializeField] protected Sprite spellIcon;

        [Header("--- 2. Цвета и оформление счётчика ---")]
        [Tooltip("Цвет обводки рамки счётчика под цвет стихии заклинания")]
        [SerializeField] protected Color borderColor = new Color(1.0f, 0.42f, 0.05f, 1f);

        [Tooltip("Цвет стрелок комбинации внутри счётчика")]
        [SerializeField] protected Color arrowColor = new Color(1.0f, 0.68f, 0.15f, 1f);

        [Tooltip("Цвет фона внутри счётчика")]
        [SerializeField] protected Color fillColor = new Color(0.04f, 0.02f, 0.01f, 0.96f);

        [Tooltip("Окрашивать ли стрелки на лепестках колеса магии под цвет заклинания")]
        [SerializeField] protected bool themeWheelArrows = true;

        [Tooltip("Цвет рамки и слота при успешном вводе (зелёный)")]
        [SerializeField] protected Color successColor = new Color(0.2f, 1.0f, 0.4f, 1f);

        [Tooltip("Цвет рамки и слота при ошибке / провале (красный)")]
        [SerializeField] protected Color failureColor = new Color(1.0f, 0.2f, 0.25f, 1f);

        [Tooltip("Цвет рамки при затемнении во время блокировки")]
        [SerializeField] protected Color dimmedBorderColor = new Color(0.35f, 0.35f, 0.4f, 0.5f);

        [Tooltip("Цвет фона при затемнении во время блокировки")]
        [SerializeField] protected Color dimmedFillColor = new Color(0.02f, 0.02f, 0.03f, 0.45f);

        [Tooltip("Прозрачность затемненной строки при блокировке")]
        [Range(0f, 1f)]
        [SerializeField] protected float dimmedAlpha = 0.4f;

        [Header("--- 3. Общие настройки (синхронизируются через PlayerMagicManager) ---")]
        [Tooltip("Время на ввод комбинации заклинания в секундах (общее для всех)")]
        [SerializeField] protected float regenerationInterval = 10f;

        [Tooltip("Длительность штрафного КД / блокировки при провале в секундах (общее для всех)")]
        [SerializeField] protected float lockoutDuration = 5f;

        [Tooltip("Количество направлений в комбинации заклинания (общее для всех)")]
        [Range(1, 6)]
        [SerializeField] protected int sequenceLength = 3;

        [Tooltip("Разрешены ли повторы направлений в одной комбинации (общее для всех)")]
        [SerializeField] protected bool allowDuplicates = true;

        [Tooltip("Считать ли отпускание ЛКМ во время неполного ввода провалом (общее для всех)")]
        [SerializeField] protected bool failOnLMBRelease = true;

        [Tooltip("Размер стрелок внутри строки в пикселях (общее для всех)")]
        [SerializeField] protected float arrowSize = 48f;

        [Tooltip("Расстояние между стрелками в пикселях (общее для всех)")]
        [SerializeField] protected float arrowSpacing = 8f;

        [Header("--- 5. Статус заклинания (Debug) ---")]
        [Tooltip("Оставшееся время на ввод заклинания")]
        [SerializeField] protected float timeRemaining = 10f;

        [Tooltip("Заблокирована ли магия из-за штрафа")]
        [SerializeField] protected bool isLockedOut = false;

        [Tooltip("Оставшееся время блокировки магии")]
        [SerializeField] protected float lockoutTimeRemaining = 0f;

        [Tooltip("Сколько направлений текущей комбинации уже правильно введено")]
        [SerializeField] protected int currentInputIndex = 0;

        [Tooltip("Текущее сгенерированное заклинание")]
        [SerializeField] protected List<AttackDirection> currentSequence = new List<AttackDirection>();

        [Header("--- Ссылки на компоненты UI ---")]
        [SerializeField] protected FencingWheel wheel;
        [SerializeField] protected FencingWheelUI wheelUI;
        [SerializeField] protected PlayerMagicManager magicManager;

        protected bool isTransitioning = false;
        protected readonly List<Image> slotImages = new List<Image>();

        public event Action<IReadOnlyList<AttackDirection>> OnSpellRegenerated;
        public event Action OnSpellSucceeded;
        public event Action<string> OnSpellFailed;

        #region Public Properties
        public string SpellName => spellName;
        public Sprite SpellIcon { get => spellIcon; set => spellIcon = value; }
        public Color BorderColor { get => borderColor; set => borderColor = value; }
        public Color ArrowColor { get => arrowColor; set => arrowColor = value; }
        public Color FillColor { get => fillColor; set => fillColor = value; }
        public Color SuccessColor
        {
            get => (magicManager != null) ? magicManager.SuccessColor : successColor;
            set
            {
                successColor = value;
                if (magicManager != null) magicManager.SuccessColor = value;
            }
        }
        public Color FailureColor
        {
            get => (magicManager != null) ? magicManager.FailureColor : failureColor;
            set
            {
                failureColor = value;
                if (magicManager != null) magicManager.FailureColor = value;
            }
        }
        public Color DimmedBorderColor
        {
            get => (magicManager != null) ? magicManager.DimmedBorderColor : dimmedBorderColor;
            set
            {
                dimmedBorderColor = value;
                if (magicManager != null) magicManager.DimmedBorderColor = value;
            }
        }
        public Color DimmedFillColor
        {
            get => (magicManager != null) ? magicManager.DimmedFillColor : dimmedFillColor;
            set
            {
                dimmedFillColor = value;
                if (magicManager != null) magicManager.DimmedFillColor = value;
            }
        }
        public float DimmedAlpha
        {
            get => (magicManager != null) ? magicManager.DimmedAlpha : dimmedAlpha;
            set
            {
                dimmedAlpha = Mathf.Clamp01(value);
                if (magicManager != null) magicManager.DimmedAlpha = value;
            }
        }
        public bool ThemeWheelArrows { get => themeWheelArrows; set => themeWheelArrows = value; }
        public PlayerMagicManager MagicManager { get => magicManager; set => magicManager = value; }

        public float RegenerationInterval
        {
            get => (magicManager != null) ? magicManager.RegenerationInterval : regenerationInterval;
            set
            {
                regenerationInterval = Mathf.Max(0.5f, value);
                if (magicManager != null) magicManager.RegenerationInterval = value;
            }
        }

        public float LockoutDuration
        {
            get => (magicManager != null) ? magicManager.LockoutDuration : lockoutDuration;
            set
            {
                lockoutDuration = Mathf.Max(0.5f, value);
                if (magicManager != null) magicManager.LockoutDuration = value;
            }
        }

        public int SequenceLength
        {
            get => (magicManager != null) ? magicManager.SequenceLength : sequenceLength;
            set
            {
                sequenceLength = Mathf.Clamp(value, 1, 6);
                if (magicManager != null) magicManager.SequenceLength = value;
                if (!isLockedOut && !isTransitioning) RegenerateSpell();
            }
        }

        public bool AllowDuplicates
        {
            get => (magicManager != null) ? magicManager.AllowDuplicates : allowDuplicates;
            set
            {
                allowDuplicates = value;
                if (magicManager != null) magicManager.AllowDuplicates = value;
            }
        }

        public bool FailOnLMBRelease
        {
            get => (magicManager != null) ? magicManager.FailOnLMBRelease : failOnLMBRelease;
            set
            {
                failOnLMBRelease = value;
                if (magicManager != null) magicManager.FailOnLMBRelease = value;
            }
        }

        public float ArrowSize
        {
            get => (magicManager != null) ? magicManager.ArrowSize : arrowSize;
            set
            {
                arrowSize = Mathf.Max(8f, value);
                if (magicManager != null) magicManager.ArrowSize = value;
                UpdateSlotsUI();
            }
        }

        public float ArrowSpacing
        {
            get => (magicManager != null) ? magicManager.ArrowSpacing : arrowSpacing;
            set
            {
                arrowSpacing = Mathf.Max(0f, value);
                if (magicManager != null) magicManager.ArrowSpacing = value;
                UpdateSlotsUI();
            }
        }

        public float TimeRemaining => timeRemaining;
        public bool IsLockedOut => isLockedOut;
        public float LockoutTimeRemaining => lockoutTimeRemaining;
        public int CurrentInputIndex => currentInputIndex;
        public IReadOnlyList<AttackDirection> CurrentSequence => currentSequence;

        /// <summary>
        /// Синхронизирует общие настройки магии из PlayerMagicManager.
        /// </summary>
        public virtual void SyncSharedSettings(
            float regenInterval,
            float lockoutDur,
            int seqLen,
            bool allowDups,
            bool failLMB,
            float arrSize,
            float arrSpacing,
            Color succCol,
            Color failCol,
            Color dimBorder,
            Color dimFill,
            float dimAlp)
        {
            regenerationInterval = regenInterval;
            lockoutDuration = lockoutDur;
            sequenceLength = seqLen;
            allowDuplicates = allowDups;
            failOnLMBRelease = failLMB;
            arrowSize = arrSize;
            arrowSpacing = arrSpacing;
            successColor = succCol;
            failureColor = failCol;
            dimmedBorderColor = dimBorder;
            dimmedFillColor = dimFill;
            dimmedAlpha = dimAlp;

            if (isActiveAndEnabled && currentSequence != null && currentSequence.Count > 0 && !isLockedOut)
            {
                UpdateSlotsUI();
            }
        }
        #endregion

        protected virtual void Start()
        {
            EnsureReferences();
            timeRemaining = regenerationInterval;

            if (magicManager == null)
            {
                ApplySpellTheme(wheelUI);
                RegenerateSpell();
            }
        }

        protected virtual void OnEnable()
        {
            EnsureReferences();
            if (wheel != null)
            {
                wheel.OnMagicAction += HandleMagicAction;
            }

            ApplySpellTheme(wheelUI);

            if (currentSequence == null || currentSequence.Count == 0)
            {
                RegenerateSpell();
            }
            else if (!isLockedOut)
            {
                UpdateSlotsUI();
            }
        }

        protected virtual void OnDisable()
        {
            if (wheel != null)
            {
                wheel.OnMagicAction -= HandleMagicAction;
                wheel.IsMagicLocked = false;
            }
        }

        protected virtual void Update()
        {
            // 1. Состояние штрафной блокировки (lockout)
            if (isLockedOut)
            {
                lockoutTimeRemaining -= Time.deltaTime;
                if (lockoutTimeRemaining <= 0f)
                {
                    EndLockout();
                }
                return;
            }

            if (isTransitioning) return;

            // 2. Обратный отсчет таймера на ввод
            if (RegenerationInterval > 0f)
            {
                timeRemaining -= Time.deltaTime;

                if (timeRemaining <= 0f)
                {
                    if (currentInputIndex > 0)
                    {
                        StartCoroutine(FailureRoutine("Время на ввод истекло!"));
                    }
                    else
                    {
                        timeRemaining = RegenerationInterval;
                        if (magicManager != null)
                        {
                            magicManager.OnSpellFinished(this, wasSuccess: false);
                        }
                        else
                        {
                            RegenerateSpell();
                        }
                    }
                    return;
                }
            }

            // 3. Проверка удержания ЛКМ во время неполного ввода
            if (FailOnLMBRelease && currentInputIndex > 0 && !isTransitioning)
            {
                if (!IsLMBHeld())
                {
                    StartCoroutine(FailureRoutine("ЛКМ была отпущена до завершения комбинации!"));
                }
            }
        }

        /// <summary>
        /// Применяет тему данного заклинания (цвет обводки, стрелок, иконку) к UI.
        /// </summary>
        public virtual void ApplySpellTheme(FencingWheelUI ui)
        {
            if (ui == null) return;
            ui.SetSpellTheme(spellIcon, borderColor, arrowColor, fillColor);
        }

        /// <summary>
        /// Обработчик ввода направления на колесе магии.
        /// </summary>
        protected virtual void HandleMagicAction(AttackDirection dir)
        {
            if (isLockedOut || isTransitioning) return;
            if (!IsLMBHeld()) return;

            if (wheel != null && wheel.CurrentStance != StanceType.Magic) return;
            if (currentSequence == null || currentSequence.Count == 0) return;

            if (currentInputIndex < currentSequence.Count)
            {
                AttackDirection expected = currentSequence[currentInputIndex];

                if (dir == expected)
                {
                    SetSlotColor(currentInputIndex, SuccessColor);
                    currentInputIndex++;

                    if (currentInputIndex >= currentSequence.Count)
                    {
                        StartCoroutine(SuccessRoutine());
                    }
                }
                else
                {
                    SetSlotColor(currentInputIndex, FailureColor);
                    StartCoroutine(FailureRoutine($"Введено неверное направление: {FencingWheelUI.GetDirectionArrow(dir)} вместо {FencingWheelUI.GetDirectionArrow(expected)}"));
                }
            }
        }

        /// <summary>
        /// Логика срабатывания заклинания при успешном вводе (переопределяется в конкретном заклинании).
        /// </summary>
        protected abstract void OnCastSuccess();

        /// <summary>
        /// Логика последствий при провале заклинания (переопределяется в конкретном заклинании).
        /// </summary>
        protected virtual void OnCastFailure(string reason) { }

        protected virtual IEnumerator SuccessRoutine()
        {
            isTransitioning = true;
            OnSpellSucceeded?.Invoke();
            Debug.Log($"<color=#00FF88>[{spellName}] УСПЕХ!</color> Комбинация введена верно!");

            // Вызов конкретной реализации магии
            OnCastSuccess();

            // Вспышка строки зеленым
            if (wheelUI != null)
            {
                wheelUI.SetBarAppearance(SuccessColor, new Color(0.04f, 0.28f, 0.10f, 0.96f), 1f);
            }
            yield return new WaitForSeconds(0.35f);

            // Исчезновение
            yield return FadeBar(1f, 0f, 0.2f);

            currentInputIndex = 0;
            timeRemaining = RegenerationInterval;
            isTransitioning = false;

            if (magicManager != null)
            {
                magicManager.OnSpellFinished(this, wasSuccess: true);
            }
            else
            {
                RegenerateSpell();

                if (wheelUI != null)
                {
                    wheelUI.ResetBarAppearance();
                }

                yield return FadeBar(0f, 1f, 0.2f);
            }
        }

        protected virtual IEnumerator FailureRoutine(string reason)
        {
            isTransitioning = true;
            OnSpellFailed?.Invoke(reason);
            Debug.LogWarning($"<color=#FF3344>[{spellName}] ПРОВАЛ!</color> {reason} Блокировка магии на {LockoutDuration:F1} сек.");

            // Вызов эффектов провала конкретного заклинания
            OnCastFailure(reason);

            if (wheelUI != null)
            {
                wheelUI.SetBarAppearance(FailureColor, new Color(0.35f, 0.05f, 0.08f, 0.96f), 1f);
            }
            yield return new WaitForSeconds(0.4f);

            yield return FadeBar(1f, 0f, 0.2f);

            isLockedOut = true;
            lockoutTimeRemaining = LockoutDuration;
            currentInputIndex = 0;

            if (wheel != null)
            {
                wheel.IsMagicLocked = true;
            }

            ClearSlots();

            if (wheelUI != null)
            {
                wheelUI.SetBarAppearance(DimmedBorderColor, DimmedFillColor, DimmedAlpha);
            }

            isTransitioning = false;
        }

        protected virtual void EndLockout()
        {
            isLockedOut = false;
            lockoutTimeRemaining = 0f;
            currentInputIndex = 0;

            if (wheel != null)
            {
                wheel.IsMagicLocked = false;
            }

            Debug.Log($"<color=#33CCFF>[{spellName}]</color> Блокировка снята.");

            if (magicManager != null)
            {
                magicManager.OnSpellFinished(this, wasSuccess: false);
            }
            else
            {
                if (wheelUI != null)
                {
                    wheelUI.ResetBarAppearance();
                }

                timeRemaining = regenerationInterval;
                RegenerateSpell();
                StartCoroutine(FadeBar(0f, 1f, 0.25f));
            }
        }

        public void ResetSpellImmediate()
        {
            StopAllCoroutines();
            isTransitioning = false;
            isLockedOut = false;
            lockoutTimeRemaining = 0f;
            currentInputIndex = 0;

            if (wheel != null)
            {
                wheel.IsMagicLocked = false;
            }

            if (wheelUI != null)
            {
                wheelUI.ResetBarAppearance();
            }

            timeRemaining = regenerationInterval;
            RegenerateSpell();
        }

        public virtual void RegenerateSpell()
        {
            currentSequence.Clear();
            currentInputIndex = 0;

            int len = SequenceLength;
            bool dups = AllowDuplicates;

            for (int i = 0; i < len; i++)
            {
                AttackDirection chosen;
                if (dups)
                {
                    chosen = AvailableDirections[UnityEngine.Random.Range(0, AvailableDirections.Length)];
                }
                else
                {
                    var pool = new List<AttackDirection>();
                    foreach (var d in AvailableDirections)
                    {
                        if (!currentSequence.Contains(d)) pool.Add(d);
                    }
                    chosen = pool.Count > 0 ? pool[UnityEngine.Random.Range(0, pool.Count)] : AvailableDirections[UnityEngine.Random.Range(0, AvailableDirections.Length)];
                }
                currentSequence.Add(chosen);
            }

            UpdateSlotsUI();
            OnSpellRegenerated?.Invoke(currentSequence);

            var arrowSymbols = new List<string>();
            for (int i = 0; i < currentSequence.Count; i++)
            {
                arrowSymbols.Add(FencingWheelUI.GetDirectionArrow(currentSequence[i]));
            }
            Debug.Log($"<color=#FFAA00>[{spellName}]</color> Новая комбинация: <b>{string.Join(" ", arrowSymbols)}</b> (время на ввод: {RegenerationInterval:F1} сек.)");
        }

        public virtual void UpdateSlotsUI()
        {
            EnsureReferences();
            if (wheelUI == null) return;

            Transform container = wheelUI.ArrowSlotsContainer;
            if (container == null) return;

            var hlg = container.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.spacing = ArrowSpacing;
                hlg.childControlWidth = false;
                hlg.childControlHeight = false;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
            }

            slotImages.Clear();

            for (int i = 0; i < currentSequence.Count; i++)
            {
                Transform slotChild = (i < container.childCount) ? container.GetChild(i) : null;
                GameObject slotObj;

                if (slotChild == null)
                {
                    slotObj = new GameObject($"SpellSlot_{i}", typeof(RectTransform), typeof(Image));
                    slotObj.transform.SetParent(container, false);
                }
                else
                {
                    slotObj = slotChild.gameObject;
                    slotObj.name = $"SpellSlot_{i}";
                    slotObj.SetActive(true);
                }

                RectTransform rt = slotObj.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(ArrowSize, ArrowSize);

                Image img = slotObj.GetComponent<Image>();
                img.sprite = FencingWheelUI.GetArrowSprite(currentSequence[i]);
                img.color = arrowColor;
                img.raycastTarget = false;
                slotImages.Add(img);

                if (Application.isPlaying && isActiveAndEnabled)
                {
                    StartCoroutine(PopArrowAnimation(rt));
                }
            }

            for (int i = currentSequence.Count; i < container.childCount; i++)
            {
                container.GetChild(i).gameObject.SetActive(false);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(container.GetComponent<RectTransform>());
        }

        protected void SetSlotColor(int index, Color col)
        {
            if (index >= 0 && index < slotImages.Count && slotImages[index] != null)
            {
                slotImages[index].color = col;
            }
        }

        protected void ClearSlots()
        {
            EnsureReferences();
            if (wheelUI == null) return;
            Transform container = wheelUI.ArrowSlotsContainer;
            if (container != null)
            {
                for (int i = 0; i < container.childCount; i++)
                {
                    container.GetChild(i).gameObject.SetActive(false);
                }
            }
            slotImages.Clear();
        }

        protected IEnumerator FadeBar(float fromAlpha, float toAlpha, float duration)
        {
            EnsureReferences();
            if (wheelUI == null || wheelUI.MagicArrowBar == null) yield break;

            var cg = wheelUI.MagicArrowBar.GetComponent<CanvasGroup>();
            if (cg == null) cg = wheelUI.MagicArrowBar.gameObject.AddComponent<CanvasGroup>();

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Lerp(fromAlpha, toAlpha, elapsed / duration);
                yield return null;
            }
            cg.alpha = toAlpha;
        }

        protected IEnumerator PopArrowAnimation(RectTransform rt)
        {
            if (rt == null) yield break;
            rt.localScale = new Vector3(0.5f, 0.5f, 1f);
            float elapsed = 0f;
            float duration = 0.2f;

            while (elapsed < duration)
            {
                if (rt == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float s = Mathf.Lerp(0.5f, 1.15f, t);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }

            if (rt != null) rt.localScale = Vector3.one;
        }

        protected bool IsLMBHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                if (UnityEngine.InputSystem.Mouse.current.leftButton.isPressed) return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            try
            {
                if (Input.GetMouseButton(0)) return true;
            }
            catch { }
#endif
            if (wheel != null && wheel.Orb != null && wheel.Orb.IsHeld) return true;
            return false;
        }

        protected virtual void EnsureReferences()
        {
            if (magicManager == null)
            {
                magicManager = GetComponentInParent<PlayerMagicManager>();
                if (magicManager == null)
                {
#if UNITY_6000_0_OR_NEWER
                    magicManager = FindAnyObjectByType<PlayerMagicManager>();
#else
                    magicManager = FindObjectOfType<PlayerMagicManager>();
#endif
                }
            }

            if (wheel == null)
            {
#if UNITY_6000_0_OR_NEWER
                wheel = FindAnyObjectByType<FencingWheel>();
#else
                wheel = FindObjectOfType<FencingWheel>();
#endif
            }

            if (wheelUI == null)
            {
#if UNITY_6000_0_OR_NEWER
                wheelUI = FindAnyObjectByType<FencingWheelUI>();
#else
                wheelUI = FindObjectOfType<FencingWheelUI>();
#endif
            }
        }

        protected virtual void OnValidate()
        {
            regenerationInterval = Mathf.Max(0.5f, regenerationInterval);
            lockoutDuration = Mathf.Max(0.5f, lockoutDuration);
            sequenceLength = Mathf.Clamp(sequenceLength, 1, 6);
            arrowSize = Mathf.Max(8f, arrowSize);
            arrowSpacing = Mathf.Max(0f, arrowSpacing);

            if (!Application.isPlaying && currentSequence != null && currentSequence.Count > 0 && !isLockedOut)
            {
                UpdateSlotsUI();
            }
        }
    }
}
