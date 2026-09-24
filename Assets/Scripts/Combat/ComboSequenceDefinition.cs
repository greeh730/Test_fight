using System;
using System.Collections.Generic;
using UnityEngine;
using Combat.UI;

namespace Combat
{
    /// <summary>
    /// Описание приёма или связки ударов, активируемой цепочкой из 8 направлений.
    /// </summary>
    [Serializable]
    public class ComboSequenceDefinition
    {
        [Tooltip("Уникальный строковый идентификатор приёма")]
        [SerializeField] private string sequenceId;

        [Tooltip("Отображаемое название приёма в HUD")]
        [SerializeField] private string sequenceName;

        [Tooltip("Последовательность стрелок в виде строки (например: '⬅⮕' или '⬋⬇⬊⮕⬈')")]
        [SerializeField] private string glyphPattern;

        [Tooltip("Список направлений 8-way для распознавания")]
        [SerializeField] private List<Direction8> requiredDirections = new List<Direction8>();

        [Tooltip("Конфигурация параметров удара (урон, тайминги, хитбокс, отталкивание)")]
        [SerializeField] private AttackConfig attackData;

        [Tooltip("Расход выносливости игрока на исполнение")]
        [SerializeField] private float staminaCost = 14f;

        [Tooltip("Подбрасывает ли приём врага в воздух (Launcher), если стамина врага на нуле?")]
        [SerializeField] private bool isLauncher = false;

        [Tooltip("Сила дополнительного выпада/рывка вперед при ударе")]
        [SerializeField] private float lungeForce = 4.0f;

        [Tooltip("Разрешено ли связывать этот приём с последующими ударами комбо")]
        [SerializeField] private bool canCancelIntoOthers = true;

        public string SequenceId => sequenceId;
        public string SequenceName => sequenceName;
        public string GlyphPattern => glyphPattern;
        public IReadOnlyList<Direction8> RequiredDirections => requiredDirections;
        public AttackConfig AttackData => attackData;
        public float StaminaCost => staminaCost;
        public bool IsLauncher => isLauncher;
        public float LungeForce => lungeForce;
        public bool CanCancelIntoOthers => canCancelIntoOthers;

        public int Length => requiredDirections != null ? requiredDirections.Count : 0;

        public ComboSequenceDefinition(
            string id,
            string name,
            string glyphs,
            AttackConfig attack,
            float stamina = 14f,
            bool launcher = false,
            float lunge = 4f)
        {
            sequenceId = id;
            sequenceName = name;
            glyphPattern = glyphs;
            requiredDirections = Direction8Extensions.ParseSequence(glyphs);
            attackData = attack;
            staminaCost = stamina;
            isLauncher = launcher;
            lungeForce = lunge;
            canCancelIntoOthers = true;
        }

        public ComboSequenceDefinition(
            string id,
            string name,
            IEnumerable<Direction8> dirs,
            AttackConfig attack,
            float stamina = 14f,
            bool launcher = false,
            float lunge = 4f)
        {
            sequenceId = id;
            sequenceName = name;
            requiredDirections = new List<Direction8>(dirs);
            glyphPattern = requiredDirections.ToGlyphString();
            attackData = attack;
            staminaCost = stamina;
            isLauncher = launcher;
            lungeForce = lunge;
            canCancelIntoOthers = true;
        }

        /// <summary>
        /// Проверяет, совпадает ли конец истории ввода с этим приёмом.
        /// </summary>
        public bool Matches(IReadOnlyList<Direction8> buffer, int bufferCount)
        {
            if (requiredDirections == null || requiredDirections.Count == 0) return false;
            if (bufferCount < requiredDirections.Count) return false;

            int patternLen = requiredDirections.Count;
            int bufferOffset = bufferCount - patternLen;

            for (int i = 0; i < patternLen; i++)
            {
                if (buffer[bufferOffset + i] != requiredDirections[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Вычисляет нечеткое соответствие буфера приему с допуском до 1 ошибки.
        /// Возвращает true, если прием совпадает с буфером (точно или с 1 ошибкой),
        /// и возвращает score ошибки (0 = идеальное совпадение, 1.0 = соседний сектор 45°, 1.2 = лишний шаг, 1.5 = пропуск).
        /// </summary>
        public bool FuzzyMatches(IReadOnlyList<Direction8> buffer, int bufferCount, out float errorScore)
        {
            errorScore = float.MaxValue;
            if (requiredDirections == null || requiredDirections.Count == 0) return false;

            int patternLen = requiredDirections.Count;

            // 1. Точное совпадение (0 ошибок)
            if (Matches(buffer, bufferCount))
            {
                errorScore = 0f;
                return true;
            }

            // Для коротких приемов из 2 стрелок (например, "⬅ ⮕") нечеткий поиск запрещен,
            // чтобы выпад вперед не путался со случайным чихом или другими направлениями
            if (patternLen < 3) return false;

            // 2. Ошибка подмены (Substitution): ровно 1 стрелка отличается (соседний сектор 45°)
            if (bufferCount >= patternLen)
            {
                int bufferOffset = bufferCount - patternLen;
                int mismatchCount = 0;
                int totalStepDist = 0;

                for (int i = 0; i < patternLen; i++)
                {
                    Direction8 bufDir = buffer[bufferOffset + i];
                    Direction8 reqDir = requiredDirections[i];
                    if (bufDir != reqDir)
                    {
                        mismatchCount++;
                        int dist = bufDir.StepDistance(reqDir);
                        totalStepDist += dist;
                        if (dist > (patternLen >= 4 ? 2 : 1))
                        {
                            mismatchCount += 2;
                            break;
                        }
                    }
                }

                if (mismatchCount == 1 && totalStepDist <= (patternLen >= 4 ? 2 : 1))
                {
                    errorScore = totalStepDist == 1 ? 1.0f : 1.4f;
                    return true;
                }
            }

            // 3. Лишний промежуточный шаг (Insertion): игрок ввел patternLen + 1 стрелок, где 1 стрелка лишняя
            if (bufferCount >= patternLen + 1)
            {
                int bufferOffset = bufferCount - (patternLen + 1);
                for (int dropIdx = 1; dropIdx < patternLen; dropIdx++)
                {
                    bool matchWithDrop = true;
                    int bufIdx = 0;
                    for (int p = 0; p < patternLen; p++)
                    {
                        if (bufIdx == dropIdx) bufIdx++;
                        if (buffer[bufferOffset + bufIdx] != requiredDirections[p])
                        {
                            matchWithDrop = false;
                            break;
                        }
                        bufIdx++;
                    }

                    if (matchWithDrop)
                    {
                        errorScore = 1.2f;
                        return true;
                    }
                }
            }

            // 4. Пропущенный шаг (Omission) для длинных приемов (patternLen >= 4):
            // игрок ввел patternLen - 1 стрелок в круговом движении
            if (patternLen >= 4 && bufferCount >= patternLen - 1)
            {
                int bufferOffset = bufferCount - (patternLen - 1);
                for (int skipIdx = 1; skipIdx < patternLen - 1; skipIdx++)
                {
                    bool matchWithSkip = true;
                    int bufIdx = 0;
                    for (int p = 0; p < patternLen; p++)
                    {
                        if (p == skipIdx) continue;
                        if (buffer[bufferOffset + bufIdx] != requiredDirections[p])
                        {
                            matchWithSkip = false;
                            break;
                        }
                        bufIdx++;
                    }

                    if (matchWithSkip)
                    {
                        errorScore = 1.5f;
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
