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

        [Header("--- База данных хитбоксов и атак (SO) ---")]
        [SerializeField] private CombatAttackDatabase attackDatabase;
        public CombatAttackDatabase AttackDatabase => attackDatabase;

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

            if (attackDatabase == null)
            {
                attackDatabase = Resources.Load<CombatAttackDatabase>("CombatAttackDatabase");
#if UNITY_EDITOR
                if (attackDatabase == null)
                {
                    attackDatabase = UnityEditor.AssetDatabase.LoadAssetAtPath<CombatAttackDatabase>("Assets/Resources/CombatAttackDatabase.asset");
                }
#endif
            }

            if (attackDatabase != null && attackDatabase.Attacks != null && attackDatabase.Attacks.Count > 0)
            {
                foreach (var entry in attackDatabase.Attacks)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.sequenceId)) continue;
                    RegisterSequence(new ComboSequenceDefinition(
                        entry.sequenceId,
                        entry.displayName,
                        entry.glyphPattern,
                        entry.attackConfig != null ? entry.attackConfig.Clone() : new AttackConfig(),
                        entry.staminaCost,
                        entry.isLauncher,
                        entry.lungeForce
                    ));
                }
            }
            else
            {
                // Резервная инициализация по умолчанию (Fallback)
                RegisterDefaultHardcodedSequences();
            }

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

        private void RegisterDefaultHardcodedSequences()
        {
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

            // 2. Средний выпад влево (Mid Left Slash): ⮕ ⬅
            RegisterSequence(new ComboSequenceDefinition(
                "strike_turnaround",
                "Выпад клинком ◀",
                "⮕⬅",
                new AttackConfig(
                    "Выпад влево",
                    CombatZone.Mid,
                    new Vector2(1.2f, 0.0f),
                    new Vector2(1.4f, 0.85f),
                    0.05f, 0.14f, 0.16f,
                    22f,
                    new Vector2(6.0f, 1.5f),
                    new Color(1f, 0.85f, 0.15f, 0.85f)
                ),
                stamina: 12f,
                launcher: false,
                lunge: 4.2f
            ));

            // 3. Верхний рубящий / Апперкот (High Slash): ⬇ ⬆ или ⬋ ⬈ / ⬊ ⬉
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
                    "Верхний диагональный вправо",
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

            RegisterSequence(new ComboSequenceDefinition(
                "strike_high_slash_diag_left",
                "Диагональный срез ↖",
                "⬊⬉",
                new AttackConfig(
                    "Верхний диагональный влево",
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

            // 4. Нижняя подсечка (Low Sweep): ⬆ ⬇ или ⬉ ⬊ / ⬈ ⬋
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
                    "Низкий срез вправо",
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

            RegisterSequence(new ComboSequenceDefinition(
                "strike_low_sweep_diag_left",
                "Низкий срез ↙",
                "⬈⬋",
                new AttackConfig(
                    "Низкий срез влево",
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

            // 5. Подбрасывающий финишер (Launcher Uppercut): ⬋ ⬇ ⬊ ⮕ ⬈ / ⬊ ⬇ ⬋ ⬅ ⬉
            RegisterSequence(new ComboSequenceDefinition(
                "finisher_launcher",
                "ВОСХОДЯЩИЙ ВИХРЬ ⚡",
                "⬋⬇⬊⮕⬈",
                new AttackConfig(
                    "Небесный апперкот вправо",
                    CombatZone.Mid | CombatZone.High,
                    new Vector2(1.1f, 0.6f),
                    new Vector2(1.6f, 1.8f),
                    0.08f, 0.20f, 0.24f,
                    38f,
                    new Vector2(3.0f, 12.0f),
                    new Color(0.95f, 0.2f, 0.95f, 0.95f)
                ),
                stamina: 24f,
                launcher: true,
                lunge: 5.5f
            ));

            RegisterSequence(new ComboSequenceDefinition(
                "finisher_launcher_left",
                "ВОСХОДЯЩИЙ ВИХРЬ ⚡",
                "⬊⬇⬋⬅⬉",
                new AttackConfig(
                    "Небесный апперкот влево",
                    CombatZone.Mid | CombatZone.High,
                    new Vector2(1.1f, 0.6f),
                    new Vector2(1.6f, 1.8f),
                    0.08f, 0.20f, 0.24f,
                    38f,
                    new Vector2(3.0f, 12.0f),
                    new Color(0.95f, 0.2f, 0.95f, 0.95f)
                ),
                stamina: 24f,
                launcher: true,
                lunge: 5.5f
            ));

            // 6. Силовой выпад клинком (Piercing Thrust): ⬊ ⮕ ⬈ / ⬋ ⬅ ⬉
            RegisterSequence(new ComboSequenceDefinition(
                "strike_piercing_thrust",
                "ПРОНЗАЮЩИЙ ШТОРМ 💥",
                "⬊⮕⬈",
                new AttackConfig(
                    "Силовой выпад вправо",
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

            RegisterSequence(new ComboSequenceDefinition(
                "strike_piercing_thrust_left",
                "ПРОНЗАЮЩИЙ ШТОРМ 💥",
                "⬋⬅⬉",
                new AttackConfig(
                    "Силовой выпад влево",
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
        }

        public ComboSequenceDefinition GetSequence(string sequenceId)
        {
            EnsureInitialized();
            return _allSequences.Find(s => s.SequenceId == sequenceId);
        }

        public void UpdateSequenceAttack(string sequenceId, AttackConfig newConfig, float stamina, bool launcher, float lunge)
        {
            EnsureInitialized();
            var seq = _allSequences.Find(s => s.SequenceId == sequenceId);
            if (seq != null)
            {
                if (newConfig != null) seq.SetAttackData(newConfig.Clone());
                seq.SetStaminaCost(stamina);
                seq.SetLauncher(launcher);
                seq.SetLungeForce(lunge);
            }
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
        /// Возвращает самый подходящий приём по взвешенному приоритету:
        /// - Точное совпадение: Score = Length * 2.0f
        /// - Нечёткое совпадение с коррекцией 1 ошибки (Length >= 3): Score = Length * 1.5f - errorScore
        /// Это гарантирует, что длинная связка или финишер с 1 ошибкой игрока не будет перехвачена случайным коротким выпадом.
        /// </summary>
        public ComboSequenceDefinition FindMatchingSequence(IReadOnlyList<Direction8> buffer, int count)
        {
            EnsureInitialized();
            if (buffer == null || count <= 0) return null;

            ComboSequenceDefinition bestMatch = null;
            float bestScore = 0f;
            bool bestIsFuzzy = false;
            float bestErrorScore = 0f;

            for (int i = 0; i < _allSequences.Count; i++)
            {
                var seq = _allSequences[i];

                // 1. Точное совпадение
                if (seq.Matches(buffer, count))
                {
                    float exactScore = seq.Length * 2.0f;
                    if (exactScore > bestScore)
                    {
                        bestScore = exactScore;
                        bestMatch = seq;
                        bestIsFuzzy = false;
                    }
                }
                // 2. Нечёткое совпадение (только для приёмов длины >= 3)
                else if (seq.Length >= 3 && seq.FuzzyMatches(buffer, count, out float errorScore))
                {
                    float fuzzyScore = (seq.Length * 1.5f) - errorScore;
                    if (fuzzyScore > bestScore)
                    {
                        bestScore = fuzzyScore;
                        bestMatch = seq;
                        bestIsFuzzy = true;
                        bestErrorScore = errorScore;
                    }
                }
            }

            if (bestMatch != null && bestIsFuzzy)
            {
                Debug.Log($"<color=#FFD700>[FUZZY COMBO MATCH]</color> Распознан приём через коррекцию ошибки: <b>{bestMatch.SequenceName}</b> ({bestMatch.GlyphPattern}) | ErrorScore: {bestErrorScore:F2}");
            }

            return bestMatch;
        }

        /// <summary>
        /// Возвращает множество направлений Direction8, которые могут продолжить текущую цепочку ввода
        /// для завершения какого-либо зарегистрированного приёма (для Smart Magnetism и Combo Compass).
        /// </summary>
        public HashSet<Direction8> GetPossibleNextDirections(IReadOnlyList<Direction8> buffer, int count)
        {
            EnsureInitialized();
            var nextSet = new HashSet<Direction8>();
            if (buffer == null || count <= 0) return nextSet;

            // Проверяем все последовательности, у которых начало (префикс) может совпадать с хвостом буфера
            for (int i = 0; i < _allSequences.Count; i++)
            {
                var seq = _allSequences[i];
                var directions = seq.RequiredDirections;
                if (directions == null || directions.Count <= 1) continue;

                // Проверяем совпадение суффикса буфера длины k с префиксом комбинации длины k (1 <= k < directions.Count)
                for (int k = Math.Min(count, directions.Count - 1); k >= 1; k--)
                {
                    bool prefixMatches = true;
                    int bufStart = count - k;
                    for (int p = 0; p < k; p++)
                    {
                        if (buffer[bufStart + p] != directions[p])
                        {
                            prefixMatches = false;
                            break;
                        }
                    }

                    if (prefixMatches)
                    {
                        // Следующее необходимое направление: directions[k]
                        nextSet.Add(directions[k]);
                        break; // Самый длинный совпавший префикс для этой комбинации найден
                    }
                }
            }

            return nextSet;
        }
    }
}
