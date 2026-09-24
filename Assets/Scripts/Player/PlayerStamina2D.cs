using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Combat.Common;

namespace Combat.Player
{
    /// <summary>
    /// Компонент выносливости игрока с механикой анти-спама и дебаффом истощения.
    /// Если игрок спамит одинаковыми действиями и истощает стамину до 0,
    /// он получает дебафф истощения (замедление всех действий в n раз),
    /// который спадает только при полном (100%) восстановлении выносливости.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerStamina2D : MonoBehaviour
    {
        [Header("--- Stamina Settings ---")]
        [SerializeField] private float maxStamina = 200f;
        [SerializeField] private float currentStamina = 200f;
        [Tooltip("Скорость восстановления стамины в секунду")]
        [SerializeField] private float regenRate = 35f;
        [Tooltip("Задержка перед началом восстановления после любого действия (сек)")]
        [SerializeField] private float regenDelay = 0.85f;

        [Header("--- Anti-Spam Settings ---")]
        [Tooltip("Окно времени для фиксации спама одинаковых действий (сек)")]
        [SerializeField] private float spamWindow = 1.4f;
        [Tooltip("Множитель штрафа за повторение одного и того же действия")]
        [SerializeField] private float spamPenaltyMultiplier = 1.5f;
        [Tooltip("Максимальный множитель стоимости при агрессивном спаме")]
        [SerializeField] private float maxSpamPenalty = 3.5f;

        [Header("--- Exhaustion Debuff Settings ---")]
        [Tooltip("Во сколько раз замедляются все действия (бег, рывок, замахи и фазы ударов) при истощении")]
        [SerializeField] private float exhaustionSlowdownFactor = 2.0f;
        [Tooltip("Цвет вспышки персонажа при получении истощения")]
        [SerializeField] private Color exhaustionPulseColor = new Color(0.75f, 0.75f, 0.85f, 0.85f);

        [Header("--- Events ---")]
        public UnityEvent<float, float> onStaminaChanged;
        public UnityEvent<bool> onExhaustionChanged;

        private float _lastActionTime = -10f;
        private string _lastActionId = string.Empty;
        private int _spamStreak = 0;
        private SpriteRenderer _sr;
        private Color _originalColor = Color.white;
        private Coroutine _exhaustionTintRoutine;

        public float MaxStamina => maxStamina;
        public float CurrentStamina => currentStamina;
        public bool IsExhausted { get; private set; } = false;

        /// <summary>
        /// Множитель скорости для действий (1.0 в норме, 1/n при истощении).
        /// </summary>
        public float ActionSpeedMultiplier
        {
            get
            {
                if (IsExhausted)
                {
                    float factor = Mathf.Max(1.1f, exhaustionSlowdownFactor);
                    return 1f / factor;
                }
                return 1.0f;
            }
        }

        public float ExhaustionSlowdownFactor => exhaustionSlowdownFactor;
        public bool IsRegenPaused { get; set; } = false;

        /// <summary>
        /// Проверяет, хватает ли выносливости для совершения действия и не находится ли игрок в истощении.
        /// </summary>
        public bool CanAfford(float cost)
        {
            if (IsExhausted) return false;
            return currentStamina >= cost;
        }

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _originalColor = _sr.color;
            currentStamina = maxStamina;
        }

        private void Update()
        {
            // Регенерация стамины (приостанавливается при удержании блока)
            if (!IsRegenPaused && Time.time - _lastActionTime >= regenDelay && currentStamina < maxStamina)
            {
                currentStamina = Mathf.MoveTowards(currentStamina, maxStamina, Time.deltaTime * regenRate);
                onStaminaChanged?.Invoke(currentStamina, maxStamina);

                // Если стамина восстановилась до 100% — снимаем дебафф истощения
                if (IsExhausted && currentStamina >= maxStamina)
                {
                    ClearExhaustion();
                }
            }
        }

        /// <summary>
        /// Списывает стамину за действие с учетом анти-спама.
        /// Возвращает итоговую стоимость действия.
        /// </summary>
        public float ConsumeForAction(string actionId, float baseCost)
        {
            float now = Time.time;
            float costMultiplier = 1.0f;

            if (!string.IsNullOrEmpty(actionId) && actionId == _lastActionId && (now - _lastActionTime <= spamWindow))
            {
                _spamStreak++;
                costMultiplier = Mathf.Min(maxSpamPenalty, Mathf.Pow(spamPenaltyMultiplier, _spamStreak));
                Debug.Log($"<color=orange>[ANTI-SPAM]</color> Спам действия '<b>{actionId}</b>' (серия: {_spamStreak})! Расход x{costMultiplier:F2}");
            }
            else
            {
                _spamStreak = 0;
            }

            _lastActionId = actionId;
            _lastActionTime = now;

            float finalCost = baseCost * costMultiplier;
            currentStamina = Mathf.Max(0f, currentStamina - finalCost);
            onStaminaChanged?.Invoke(currentStamina, maxStamina);

            // Проверка перехода в истощение
            if (currentStamina <= 0f && !IsExhausted)
            {
                TriggerExhaustion();
            }

            return finalCost;
        }

        /// <summary>
        /// Списывает фиксированное количество стамины без анти-спам множителей (например, за прыжок).
        /// </summary>
        public float ConsumeFlat(float amount)
        {
            _lastActionTime = Time.time;
            currentStamina = Mathf.Max(0f, currentStamina - amount);
            onStaminaChanged?.Invoke(currentStamina, maxStamina);

            if (currentStamina <= 0f && !IsExhausted)
            {
                TriggerExhaustion();
            }

            return amount;
        }

        /// <summary>
        /// Списывает процент от максимальной выносливости (например, 0.25f = 25%).
        /// </summary>
        public float ConsumePercent(float percent)
        {
            return ConsumeFlat(maxStamina * Mathf.Clamp01(percent));
        }

        private void TriggerExhaustion()
        {
            IsExhausted = true;
            onExhaustionChanged?.Invoke(true);

            // Всплывающий текст над игроком
            CombatFloatingText.Spawn(transform.position + Vector3.up * 1.5f, "[ИСТОЩЕНИЕ!]", new Color(0.95f, 0.25f, 0.1f), 1.6f);
            Debug.Log($"<color=red><b>[ИСТОЩЕНИЕ!]</b></color> Выносливость игрока исчерпана! Все действия замедлены в {exhaustionSlowdownFactor:F1} раз до полного восстановления стамины (100%)!");

            if (_exhaustionTintRoutine != null) StopCoroutine(_exhaustionTintRoutine);
            _exhaustionTintRoutine = StartCoroutine(ExhaustionVisualRoutine());
        }

        private void ClearExhaustion()
        {
            IsExhausted = false;
            onExhaustionChanged?.Invoke(false);

            if (_exhaustionTintRoutine != null)
            {
                StopCoroutine(_exhaustionTintRoutine);
                _exhaustionTintRoutine = null;
            }
            if (_sr != null) _sr.color = _originalColor;

            CombatFloatingText.Spawn(transform.position + Vector3.up * 1.5f, "[СИЛЫ ВОССТАНОВЛЕНЫ]", new Color(0.2f, 0.95f, 0.35f), 1.2f);
            Debug.Log("<color=green><b>[ВЫНОСЛИВОСТЬ ВОССТАНОВЛЕНА]</b></color> Стамина достигла 100%! Дебафф истощения снят.");
        }

        private IEnumerator ExhaustionVisualRoutine()
        {
            while (IsExhausted)
            {
                if (_sr != null)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
                    _sr.color = Color.Lerp(_originalColor, exhaustionPulseColor, pulse);
                }
                yield return null;
            }
            if (_sr != null) _sr.color = _originalColor;
            _exhaustionTintRoutine = null;
        }

        public void RestoreState(float stamina, bool isExhaustedState)
        {
            currentStamina = Mathf.Clamp(stamina, 0f, maxStamina);
            _lastActionTime = -10f;
            _lastActionId = string.Empty;
            _spamStreak = 0;

            if (isExhaustedState || currentStamina <= 0f)
            {
                IsExhausted = true;
                onExhaustionChanged?.Invoke(true);
                if (_exhaustionTintRoutine != null) StopCoroutine(_exhaustionTintRoutine);
                _exhaustionTintRoutine = StartCoroutine(ExhaustionVisualRoutine());
            }
            else
            {
                IsExhausted = false;
                onExhaustionChanged?.Invoke(false);
                if (_exhaustionTintRoutine != null)
                {
                    StopCoroutine(_exhaustionTintRoutine);
                    _exhaustionTintRoutine = null;
                }
                if (_sr != null) _sr.color = _originalColor;
            }

            onStaminaChanged?.Invoke(currentStamina, maxStamina);
        }

        public void ModifyMaxStamina(float deltaAmount)
        {
            maxStamina = Mathf.Max(20f, maxStamina + deltaAmount);
            if (deltaAmount > 0f)
            {
                currentStamina = Mathf.Min(maxStamina, currentStamina + deltaAmount);
            }
            else
            {
                currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
            }
            onStaminaChanged?.Invoke(currentStamina, maxStamina);
        }

        public void ModifyRegenRate(float deltaMultiplier)
        {
            regenRate = Mathf.Max(5f, regenRate * deltaMultiplier);
        }
    }
}
