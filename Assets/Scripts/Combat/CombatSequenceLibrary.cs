using System;
using System.Collections.Generic;
using UnityEngine;
using Combat.UI;

namespace Combat
{
    /// <summary>
    /// Библиотека зарегистрированных приёмов и моушн-комбинаций из 8 направлений.
    /// Позволяет как использовать предустановленные приёмы, так и добавлять новые прямо строками стрелок.
    /// </summary>
    public class CombatSequenceLibrary : MonoBehaviour
    {
        private static CombatSequenceLibrary _instance;
        public static CombatSequenceLibrary Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<CombatSequenceLibrary>();
                    if (_instance == null)
                    {
                        var go = new GameObject("CombatSequenceLibrary");
                        _instance = go.AddComponent<CombatSequenceLibrary>();
                    }
                }
                _instance.EnsureInitialized();
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("--- Предустановленные приёмы (Presets) ---")]
        [SerializeField] private List<ComboSequenceDefinition> customSequences = new List<ComboSequenceDefinition>();

        private readonly List<ComboSequenceDefinition> _allSequences = new List<ComboSequenceDefinition>();
        public IReadOnlyList<ComboSequenceDefinition> AllSequences
        {
            get
            {
                EnsureInitialized();
                return _allSequences;
            }
        }

        public void EnsureInitialized()
        {
            if (_allSequences.Count == 0)
            {
                InitializeDefaultLibrary();
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                EnsureInitialized();
            }
            else if (_instance != this)
            {
                Destroy(this);
            }
        }

        private void InitializeDefaultLibrary()
        {
            _allSequences.Clear();

            // 1. Средний выпад вперед (Mid Forward Slash): ⬅ ⮕
            RegisterSequence(new ComboSequenceDefinition(
                "strike_mid_forward",
                "Выпад клинком ▶",
                "⬅⮕",
                new AttackConfig(
                    "Средний выпад",
                    CombatZone.Mid,
                    new Vector2(1.2f, 0.0f),
                    new Vector2(1.4f, 0.85f),
                    0.05f, 0.14f, 0.16f,
                    22f,
                    new Vector2(6.0f, 1.5f),
                    new Color(1f, 0.85f, 0.15f, 0.85f) // Желто-золотой
                ),
                stamina: 12f,
                launcher: false,
                lunge: 4.2f
            ));

            // 2. Удар назад с разворота (Turnaround Slash): ⮕ ⬅
            RegisterSequence(new ComboSequenceDefinition(
                "strike_turnaround",
                "Удар с разворота ◀",
                "⮕⬅",
                new AttackConfig(
                    "Срез назад",
                    CombatZone.Mid,
                    new Vector2(1.2f, 0.0f),
                    new Vector2(1.4f, 0.85f),
                    0.06f, 0.15f, 0.18f,
                    24f,
                    new Vector2(6.5f, 1.5f),
                    new Color(1f, 0.55f, 0.15f, 0.85f)
                ),
                stamina: 14f,
                launcher: false,
                lunge: 3.8f
            ));

            // 3. Верхний рубящий / Апперкот (High Slash): ⬇ ⬆ или ⬋ ⬈
            RegisterSequence(new ComboSequenceDefinition(
                "strike_high_slash",
                "Верхний рубящий ▲",
                "⬇⬆",
                new AttackConfig(
                    "Верхний срез",
                    CombatZone.Mid | CombatZone.High,
                    new Vector2(1.0f, 0.5f),
                    new Vector2(1.3f, 1.4f),
                    0.06f, 0.16f, 0.20f,
                    26f,
                    new Vector2(3.5f, 7.5f), // Подбрасывает вверх
                    new Color(1f, 0.35f, 0.1f, 0.85f) // Оранжево-красный
                ),
                stamina: 15f,
                launcher: false,
                lunge: 2.8f
            ));

            RegisterSequence(new ComboSequenceDefinition(
                "strike_high_slash_diag",
                "Диагональный срез ↗",
                "⬋⬈",
                new AttackConfig(
                    "Верхний диагональный",
                    CombatZone.Mid | CombatZone.High,
                    new Vector2(1.0f, 0.5f),
                    new Vector2(1.3f, 1.4f),
                    0.06f, 0.16f, 0.20f,
                    26f,
                    new Vector2(4.0f, 7.5f),
                    new Color(1f, 0.35f, 0.1f, 0.85f)
                ),
                stamina: 15f,
                launcher: false,
                lunge: 3.2f
            ));

            // 4. Нижняя подсечка (Low Sweep): ⬆ ⬇ или ⬉ ⬊
            RegisterSequence(new ComboSequenceDefinition(
                "strike_low_sweep",
                "Нижняя подсечка ▼",
                "⬆⬇",
                new AttackConfig(
                    "Нижняя подсечка",
                    CombatZone.Low,
                    new Vector2(1.1f, -0.45f),
                    new Vector2(1.5f, 0.65f),
                    0.05f, 0.14f, 0.18f,
                    20f,
                    new Vector2(5.5f, 0.8f),
                    new Color(0.15f, 0.85f, 1f, 0.85f) // Голубой
                ),
                stamina: 12f,
                launcher: false,
                lunge: 3.5f
            ));

            RegisterSequence(new ComboSequenceDefinition(
                "strike_low_sweep_diag",
                "Низкий срез ↘",
                "⬉⬊",
                new AttackConfig(
                    "Низкий срез",
                    CombatZone.Low,
                    new Vector2(1.1f, -0.45f),
                    new Vector2(1.5f, 0.65f),
                    0.05f, 0.14f, 0.18f,
                    20f,
                    new Vector2(5.5f, 0.8f),
                    new Color(0.15f, 0.85f, 1f, 0.85f)
                ),
                stamina: 12f,
                launcher: false,
                lunge: 3.5f
            ));

            // 5. Подбрасывающий финишер (Launcher Uppercut): ⬋ ⬇ ⬊ ⮕ ⬈
            RegisterSequence(new ComboSequenceDefinition(
                "finisher_launcher",
                "ВОСХОДЯЩИЙ ВИХРЬ ⚡",
                "⬋⬇⬊⮕⬈",
                new AttackConfig(
                    "Небесный апперкот",
                    CombatZone.Mid | CombatZone.High,
                    new Vector2(1.1f, 0.6f),
                    new Vector2(1.6f, 1.8f),
                    0.08f, 0.20f, 0.24f,
                    38f,
                    new Vector2(3.0f, 12.0f), // Мощное подбрасывание в небо
                    new Color(0.95f, 0.2f, 0.95f, 0.95f) // Неоновый маджента
                ),
                stamina: 24f,
                launcher: true,
                lunge: 5.5f
            ));

            // 6. Силовой выпад клинком (Piercing Thrust): ⬊ ⮕ ⬈
            RegisterSequence(new ComboSequenceDefinition(
                "strike_piercing_thrust",
                "ПРОНЗАЮЩИЙ ШТОРМ 💥",
                "⬊⮕⬈",
                new AttackConfig(
                    "Силовой выпад",
                    CombatZone.Mid,
                    new Vector2(1.5f, 0.0f),
                    new Vector2(1.8f, 0.9f),
                    0.07f, 0.18f, 0.22f,
                    32f,
                    new Vector2(8.5f, 2.0f),
                    new Color(1f, 0.45f, 0.05f, 0.9f)
                ),
                stamina: 20f,
                launcher: false,
                lunge: 6.8f
            ));

            // Добавляем пользовательские приёмы из инспектора, если есть
            if (customSequences != null)
            {
                foreach (var seq in customSequences)
                {
                    if (seq != null && !string.IsNullOrEmpty(seq.SequenceId))
                    {
                        RegisterSequence(seq);
                    }
                }
            }

            // Сортируем: более длинные приёмы должны проверяться первыми (Longest-Match-First)
            SortSequences();
        }

        public void RegisterSequence(ComboSequenceDefinition seq)
        {
            if (seq == null) return;
            _allSequences.RemoveAll(s => s.SequenceId == seq.SequenceId);
            _allSequences.Add(seq);
            SortSequences();
        }

        private void SortSequences()
        {
            _allSequences.Sort((a, b) => b.Length.CompareTo(a.Length));
        }

        /// <summary>
        /// Ищет наиболее подходящий приём для конца буфера истории ввода.
        /// Возвращает самый длинный совпавший приём (например, "⬋⬇⬊⮕⬈" выиграет у "⬅⮕").
        /// </summary>
        public ComboSequenceDefinition FindMatchingSequence(IReadOnlyList<Direction8> buffer, int count)
        {
            EnsureInitialized();
            for (int i = 0; i < _allSequences.Count; i++)
            {
                var seq = _allSequences[i];
                if (seq.Matches(buffer, count))
                {
                    return seq;
                }
            }
            return null;
        }
    }
}
